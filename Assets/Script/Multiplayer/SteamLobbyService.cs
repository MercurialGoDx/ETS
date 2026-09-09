using System;
using System.Collections.Generic;
using ETS.Multiplayer;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

/// <summary>
/// Лобби поверх Steam Matchmaking.
///
/// Код лобби сделан без своего сервера: хост кладёт шесть символов в данные лобби, гость
/// ищет по ним через AddRequestLobbyListStringFilter. Побочный эффект — лобби обязано быть
/// публичным, иначе оно не попадёт в выдачу поиска. Практически это приватно (найти можно
/// только зная код), но лобби видно в общем списке, пока в него не вошёл второй игрок:
/// после этого вызывается SetLobbyJoinable(false).
///
/// Фильтр по версии сборки стоит намеренно: одинаковый сид на разных версиях даст разные
/// забеги, и матч превратится во взаимные обвинения.
/// </summary>
public class SteamLobbyService : ILobbyService
{
    public const string KeyCode = "ets_code";
    public const string KeyBuild = "ets_build";

    private const int MaxMembers = 2;

    public event Action MembersChanged;
    public event Action<ulong, LobbyDeparture> MemberLeft;
    public event Action LobbyDataChanged;

#if !DISABLESTEAMWORKS
    private CSteamID lobbyId;
    private bool inLobby;
    private bool isHost;
    private string code = string.Empty;

    private readonly List<LobbyMember> members = new List<LobbyMember>();

    // CallResult-ы и Callback-и держим полями: иначе их соберёт GC до ответа Steam.
    private CallResult<LobbyCreated_t> createCall;
    private CallResult<LobbyMatchList_t> listCall;
    private CallResult<LobbyEnter_t> enterCall;
    private Callback<LobbyChatUpdate_t> chatUpdate;
    private Callback<LobbyDataUpdate_t> dataUpdate;
    private Callback<GameLobbyJoinRequested_t> joinRequested;

    private Action<LobbyResult> pendingCreate;
    private Action<LobbyResult> pendingJoin;
    private string pendingCode;

    public bool IsInLobby => inLobby;
    public bool IsHost => isHost;
    public string Code => code;
    public IReadOnlyList<LobbyMember> Members => members;

    public SteamLobbyService()
    {
        if (!SteamManager.Initialized)
            return;

        chatUpdate = Callback<LobbyChatUpdate_t>.Create(OnChatUpdate);
        dataUpdate = Callback<LobbyDataUpdate_t>.Create(OnDataUpdate);
        joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
    }

    // ---------- Создание ----------

    public void Create(Action<LobbyResult> onDone)
    {
        if (!SteamManager.Initialized)
        {
            onDone?.Invoke(LobbyResult.NoSteam);
            return;
        }

        pendingCreate = onDone;
        pendingCode = LobbyCode.Generate(bound => UnityEngine.Random.Range(0, bound));

        if (createCall == null)
            createCall = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);

        createCall.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, MaxMembers));
    }

    private void OnLobbyCreated(LobbyCreated_t result, bool ioFailure)
    {
        if (ioFailure || result.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogWarning($"[SteamLobby] Не удалось создать лобби: {result.m_eResult}.");
            Finish(ref pendingCreate, LobbyResult.Failed);
            return;
        }

        lobbyId = new CSteamID(result.m_ulSteamIDLobby);
        inLobby = true;
        isHost = true;
        code = pendingCode;

        SteamMatchmaking.SetLobbyData(lobbyId, KeyCode, code);
        SteamMatchmaking.SetLobbyData(lobbyId, KeyBuild, BuildVersion);

        RefreshMembers();
        Debug.Log($"[SteamLobby] Лобби создано, код {code}.");
        Finish(ref pendingCreate, LobbyResult.Ok);
    }

    // ---------- Вход по коду ----------

    public void JoinByCode(string rawCode, Action<LobbyResult> onDone)
    {
        if (!SteamManager.Initialized)
        {
            onDone?.Invoke(LobbyResult.NoSteam);
            return;
        }

        string normalized = LobbyCode.Normalize(rawCode);
        if (normalized == null)
        {
            onDone?.Invoke(LobbyResult.InvalidCode);
            return;
        }

        pendingJoin = onDone;
        pendingCode = normalized;

        if (listCall == null)
            listCall = CallResult<LobbyMatchList_t>.Create(OnLobbyList);

        SteamMatchmaking.AddRequestLobbyListStringFilter(
            KeyCode, normalized, ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListStringFilter(
            KeyBuild, BuildVersion, ELobbyComparison.k_ELobbyComparisonEqual);

        listCall.Set(SteamMatchmaking.RequestLobbyList());
    }

    private void OnLobbyList(LobbyMatchList_t result, bool ioFailure)
    {
        if (ioFailure || result.m_nLobbiesMatching == 0)
        {
            Debug.LogWarning($"[SteamLobby] Лобби с кодом {pendingCode} не найдено.");
            Finish(ref pendingJoin, LobbyResult.NotFound);
            return;
        }

        JoinLobbyId(SteamMatchmaking.GetLobbyByIndex(0));
    }

    private void JoinLobbyId(CSteamID target)
    {
        if (enterCall == null)
            enterCall = CallResult<LobbyEnter_t>.Create(OnLobbyEnter);

        enterCall.Set(SteamMatchmaking.JoinLobby(target));
    }

    private void OnLobbyEnter(LobbyEnter_t result, bool ioFailure)
    {
        if (ioFailure || result.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            Debug.LogWarning($"[SteamLobby] Вход не удался: ответ {result.m_EChatRoomEnterResponse}.");
            Finish(ref pendingJoin, LobbyResult.Failed);
            return;
        }

        lobbyId = new CSteamID(result.m_ulSteamIDLobby);
        inLobby = true;
        isHost = SteamMatchmaking.GetLobbyOwner(lobbyId) == SteamUser.GetSteamID();
        code = SteamMatchmaking.GetLobbyData(lobbyId, KeyCode);

        RefreshMembers();

        // Лобби на двоих: как только оба внутри, закрываем его от поиска.
        if (isHost && members.Count >= MaxMembers)
            SteamMatchmaking.SetLobbyJoinable(lobbyId, false);

        Debug.Log($"[SteamLobby] Вошли в лобби {code}, участников {members.Count}.");
        Finish(ref pendingJoin, LobbyResult.Ok);
    }

    // ---------- Приглашения ----------

    public void InviteFriend()
    {
        if (!inLobby)
            return;

        SteamFriends.ActivateGameOverlayInviteDialog(lobbyId);
    }

    /// <summary>Игрок принял приглашение, пока игра уже запущена.</summary>
    private void OnJoinRequested(GameLobbyJoinRequested_t param)
    {
        Debug.Log("[SteamLobby] Принято приглашение из оверлея.");
        JoinLobbyId(param.m_steamIDLobby);
    }

    // ---------- Выход ----------

    public void Leave()
    {
        if (!inLobby)
            return;

        SteamMatchmaking.LeaveLobby(lobbyId);
        inLobby = false;
        isHost = false;
        code = string.Empty;
        members.Clear();
        MembersChanged?.Invoke();
    }

    // ---------- Данные ----------

    public void SetLobbyValue(string key, string value)
    {
        if (inLobby && isHost)
            SteamMatchmaking.SetLobbyData(lobbyId, key, value ?? string.Empty);
    }

    public string GetLobbyValue(string key)
    {
        return inLobby ? SteamMatchmaking.GetLobbyData(lobbyId, key) : string.Empty;
    }

    public void SetMemberValue(string key, string value)
    {
        if (inLobby)
            SteamMatchmaking.SetLobbyMemberData(lobbyId, key, value ?? string.Empty);
    }

    public string GetMemberValue(ulong memberId, string key)
    {
        return inLobby
            ? SteamMatchmaking.GetLobbyMemberData(lobbyId, new CSteamID(memberId), key)
            : string.Empty;
    }

    // ---------- Участники ----------

    private void OnChatUpdate(LobbyChatUpdate_t param)
    {
        if (!inLobby || param.m_ulSteamIDLobby != lobbyId.m_SteamID)
            return;

        RefreshMembers();
        ReportDeparture(param);

        if (isHost && members.Count >= MaxMembers)
            SteamMatchmaking.SetLobbyJoinable(lobbyId, false);
        else if (isHost)
            SteamMatchmaking.SetLobbyJoinable(lobbyId, true);
    }


    /// <summary>
    /// Steam различает уход и обрыв флагами состояния, и это единственное место, где причина
    /// вообще известна: по составу лобби потом уже не понять, ушёл человек сам или упала сеть.
    /// </summary>
    private void ReportDeparture(LobbyChatUpdate_t param)
    {
        var change = (EChatMemberStateChange)param.m_rgfChatMemberStateChange;

        if ((change & EChatMemberStateChange.k_EChatMemberStateChangeDisconnected) != 0)
        {
            MemberLeft?.Invoke(param.m_ulSteamIDUserChanged, LobbyDeparture.Disconnected);
            return;
        }

        const EChatMemberStateChange gone =
            EChatMemberStateChange.k_EChatMemberStateChangeLeft
            | EChatMemberStateChange.k_EChatMemberStateChangeKicked
            | EChatMemberStateChange.k_EChatMemberStateChangeBanned;

        if ((change & gone) != 0)
            MemberLeft?.Invoke(param.m_ulSteamIDUserChanged, LobbyDeparture.Left);
    }

    private void OnDataUpdate(LobbyDataUpdate_t param)
    {
        if (!inLobby || param.m_ulSteamIDLobby != lobbyId.m_SteamID)
            return;

        LobbyDataChanged?.Invoke();
    }

    private void RefreshMembers()
    {
        members.Clear();

        CSteamID owner = SteamMatchmaking.GetLobbyOwner(lobbyId);
        int count = SteamMatchmaking.GetNumLobbyMembers(lobbyId);

        for (int i = 0; i < count; i++)
        {
            CSteamID member = SteamMatchmaking.GetLobbyMemberByIndex(lobbyId, i);

            // Для не-друзей имя приходит асинхронно; просим Steam и берём, что уже есть.
            SteamFriends.RequestUserInformation(member, false);
            string name = SteamFriends.GetFriendPersonaName(member);
            if (string.IsNullOrEmpty(name) || name == "[unknown]")
                name = "Player";

            members.Add(new LobbyMember(member.m_SteamID, name, member == owner));
        }

        MembersChanged?.Invoke();
    }

    private static string BuildVersion => Application.version;

    private static void Finish(ref Action<LobbyResult> callback, LobbyResult result)
    {
        Action<LobbyResult> local = callback;
        callback = null;
        local?.Invoke(result);
    }
#else
    // Сборка без Steamworks: интерфейс есть, мультиплеера нет.
    public bool IsInLobby => false;
    public bool IsHost => false;
    public string Code => string.Empty;
    public IReadOnlyList<LobbyMember> Members => Array.Empty<LobbyMember>();

    public void Create(Action<LobbyResult> onDone) => onDone?.Invoke(LobbyResult.NoSteam);
    public void JoinByCode(string code, Action<LobbyResult> onDone) => onDone?.Invoke(LobbyResult.NoSteam);
    public void Leave() { }
    public void InviteFriend() { }
    public void SetLobbyValue(string key, string value) { }
    public string GetLobbyValue(string key) => string.Empty;
    public void SetMemberValue(string key, string value) { }
    public string GetMemberValue(ulong memberId, string key) => string.Empty;
#endif
}
