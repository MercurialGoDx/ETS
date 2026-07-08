using System;
using System.Collections.Generic;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

/// <summary>
/// Реализация таблицы лидеров поверх Steam Leaderboards. Результаты (время забега в секундах)
/// хранятся на серверах Valve; ник и аватар берутся из Steam по SteamID автора записи.
///
/// Требования: инициализированный Steam (<see cref="SteamManager.Initialized"/>), т.е. запущенный
/// Steam-клиент и валидный App ID (steam_appid.txt в корне проекта / запуск через Steam).
/// Если Steam недоступен, <see cref="LeaderboardUI"/> сам откатывается на заглушку.
///
/// Лидерборд создаётся при первом обращении (FindOrCreateLeaderboard): сортировка по убыванию
/// (больше времени = выше), тип отображения — время в секундах.
///
/// Замечание про ник/аватар глобальных записей: для игроков, которых Steam ещё «не знает»
/// (не друзья, не встречались), имя/аватар подгружаются асинхронно — здесь мы запрашиваем их
/// (RequestUserInformation) и используем то, что уже доступно; при повторном открытии таблицы
/// данные будут заполнены. Собственная запись игрока показывается с ником/аватаром сразу.
/// </summary>
public class SteamLeaderboardService : ILeaderboardService
{
    private readonly string leaderboardName;

    public SteamLeaderboardService(string leaderboardName)
    {
        this.leaderboardName = string.IsNullOrEmpty(leaderboardName) ? "SurvivalTime" : leaderboardName;
    }

#if !DISABLESTEAMWORKS
    private SteamLeaderboard_t boardHandle;
    private bool boardResolved;

    // CallResult-ы держим полями, иначе их соберёт GC до прихода ответа от Steam.
    private CallResult<LeaderboardFindResult_t> findCall;
    private CallResult<LeaderboardScoresDownloaded_t> downloadCall;
    private CallResult<LeaderboardScoreUploaded_t> uploadCall;

    // Отложенные запросы, ждущие получения хендла лидерборда.
    private int pendingTopCount;
    private Action<List<LeaderboardEntry>> pendingTopCallback;
    private float pendingSubmitSeconds;
    private bool hasPendingSubmit;
    private Action pendingSubmitCallback;

    public void GetTop(int count, Action<List<LeaderboardEntry>> onDone)
    {
        if (!SteamManager.Initialized)
        {
            if (onDone != null) onDone(new List<LeaderboardEntry>());
            return;
        }

        pendingTopCount = count;
        pendingTopCallback = onDone;

        if (boardResolved)
            DownloadTop();
        else
            ResolveBoard();
    }

    public void SubmitTime(float timeSeconds, Action onDone = null)
    {
        if (!SteamManager.Initialized)
        {
            if (onDone != null) onDone();
            return;
        }

        pendingSubmitSeconds = timeSeconds;
        pendingSubmitCallback = onDone;
        hasPendingSubmit = true;

        if (boardResolved)
            UploadPending();
        else
            ResolveBoard();
    }

    // --- Получение/создание хендла лидерборда ---

    private void ResolveBoard()
    {
        if (findCall == null)
            findCall = CallResult<LeaderboardFindResult_t>.Create(OnBoardResolved);

        SteamAPICall_t call = SteamUserStats.FindOrCreateLeaderboard(
            leaderboardName,
            ELeaderboardSortMethod.k_ELeaderboardSortMethodDescending,
            ELeaderboardDisplayType.k_ELeaderboardDisplayTypeTimeSeconds);
        findCall.Set(call);
    }

    private void OnBoardResolved(LeaderboardFindResult_t result, bool ioFailure)
    {
        if (ioFailure || result.m_bLeaderboardFound == 0)
        {
            Debug.LogWarning("[SteamLeaderboard] Не удалось найти/создать лидерборд '" + leaderboardName + "'.");
            if (pendingTopCallback != null) { pendingTopCallback(new List<LeaderboardEntry>()); pendingTopCallback = null; }
            hasPendingSubmit = false;
            return;
        }

        boardHandle = result.m_hSteamLeaderboard;
        boardResolved = true;

        if (pendingTopCallback != null)
            DownloadTop();
        if (hasPendingSubmit)
            UploadPending();
    }

    // --- Загрузка топа ---

    private void DownloadTop()
    {
        if (downloadCall == null)
            downloadCall = CallResult<LeaderboardScoresDownloaded_t>.Create(OnTopDownloaded);

        int count = Mathf.Max(1, pendingTopCount);
        SteamAPICall_t call = SteamUserStats.DownloadLeaderboardEntries(
            boardHandle,
            ELeaderboardDataRequest.k_ELeaderboardDataRequestGlobal,
            1, count);
        downloadCall.Set(call);
    }

    private void OnTopDownloaded(LeaderboardScoresDownloaded_t result, bool ioFailure)
    {
        var list = new List<LeaderboardEntry>();
        if (ioFailure)
        {
            Debug.LogWarning("[SteamLeaderboard] Ошибка загрузки записей лидерборда.");
            if (pendingTopCallback != null) { pendingTopCallback(list); pendingTopCallback = null; }
            return;
        }

        int n = result.m_cEntryCount;
        for (int i = 0; i < n; i++)
        {
            LeaderboardEntry_t e;
            if (!SteamUserStats.GetDownloadedLeaderboardEntry(result.m_hSteamLeaderboardEntries, i, out e, null, 0))
                continue;

            CSteamID user = e.m_steamIDUser;

            // Просим Steam подгрузить инфу об игроке (для не-друзей ник/аватар придут асинхронно).
            SteamFriends.RequestUserInformation(user, false);

            string personaName = SteamFriends.GetFriendPersonaName(user);
            if (string.IsNullOrEmpty(personaName) || personaName == "[unknown]")
                personaName = "Player";

            Sprite avatar = LoadAvatar(user);

            list.Add(new LeaderboardEntry(e.m_nGlobalRank, personaName, avatar, e.m_nScore));
        }

        if (pendingTopCallback != null) { pendingTopCallback(list); pendingTopCallback = null; }
    }

    // --- Отправка результата ---

    private void UploadPending()
    {
        if (uploadCall == null)
            uploadCall = CallResult<LeaderboardScoreUploaded_t>.Create(OnScoreUploaded);

        int score = Mathf.RoundToInt(pendingSubmitSeconds);
        SteamAPICall_t call = SteamUserStats.UploadLeaderboardScore(
            boardHandle,
            ELeaderboardUploadScoreMethod.k_ELeaderboardUploadScoreMethodKeepBest,
            score, null, 0);
        uploadCall.Set(call);
        hasPendingSubmit = false;
    }

    private void OnScoreUploaded(LeaderboardScoreUploaded_t result, bool ioFailure)
    {
        if (ioFailure || result.m_bSuccess == 0)
            Debug.LogWarning("[SteamLeaderboard] Не удалось отправить результат.");

        if (pendingSubmitCallback != null) { pendingSubmitCallback(); pendingSubmitCallback = null; }
    }

    // --- Аватар из Steam (RGBA) → Sprite ---

    private static Sprite LoadAvatar(CSteamID user)
    {
        int handle = SteamFriends.GetLargeFriendAvatar(user);
        // -1 = аватар ещё грузится (придёт через AvatarImageLoaded_t), 0 = аватара нет.
        if (handle <= 0)
            return null;

        uint w, h;
        if (!SteamUtils.GetImageSize(handle, out w, out h) || w == 0 || h == 0)
            return null;

        int byteCount = (int)(w * h * 4);
        byte[] image = new byte[byteCount];
        if (!SteamUtils.GetImageRGBA(handle, image, byteCount))
            return null;

        // Steam отдаёт пиксели сверху-вниз, Unity ждёт снизу-вверх — переворачиваем по вертикали.
        var tex = new Texture2D((int)w, (int)h, TextureFormat.RGBA32, false, false);
        int rowBytes = (int)w * 4;
        var flipped = new byte[byteCount];
        for (int y = 0; y < h; y++)
            Array.Copy(image, y * rowBytes, flipped, (int)(h - 1 - y) * rowBytes, rowBytes);
        tex.LoadRawTextureData(flipped);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }
#else
    // Сборка без Steamworks — методы-заглушки, чтобы проект компилировался на не-Steam платформах.
    public void GetTop(int count, Action<List<LeaderboardEntry>> onDone)
    {
        if (onDone != null) onDone(new List<LeaderboardEntry>());
    }

    public void SubmitTime(float timeSeconds, Action onDone = null)
    {
        if (onDone != null) onDone();
    }
#endif
}
