using UnityEngine;

/// <summary>
/// Единая точка доступа к таблице лидеров на весь сеанс. Держит один экземпляр
/// <see cref="ILeaderboardService"/> живым (важно для Steam: асинхронные CallResult-ы не должны
/// собираться GC до ответа), которым пользуются и окно таблицы (<see cref="LeaderboardUI"/>, чтение),
/// и экран поражения (<see cref="GameOverController"/>, отправка результата).
///
/// Реализация выбирается автоматически: если Steam инициализирован — <see cref="SteamLeaderboardService"/>,
/// иначе (например, в редакторе без Steam) — <see cref="StubLeaderboardService"/>.
/// </summary>
public static class Leaderboards
{
    private static ILeaderboardService s_service;
    private static string s_steamLeaderboardName = "SurvivalTime_v2";
    private static bool s_createIfMissing = true;

    /// <summary>
    /// Задать параметры Steam-лидерборда до первого обращения к <see cref="Service"/>. После создания
    /// сервиса вызов игнорируется (параметры уже зафиксированы). Обычно вызывается из
    /// <see cref="LeaderboardConfig"/> на старте сцены.
    /// </summary>
    /// <param name="createIfMissing">
    /// true — FindOrCreateLeaderboard (создать, если нет; для разработки);
    /// false — FindLeaderboard (только искать; для релиза).
    /// </param>
    public static void Configure(string steamLeaderboardName, bool createIfMissing)
    {
        if (s_service != null)
            return;

        if (!string.IsNullOrEmpty(steamLeaderboardName))
            s_steamLeaderboardName = steamLeaderboardName;
        s_createIfMissing = createIfMissing;
    }

    public static ILeaderboardService Service
    {
        get
        {
            if (s_service == null)
            {
                if (SteamManager.Initialized)
                    s_service = new SteamLeaderboardService(s_steamLeaderboardName, s_createIfMissing);
                else
                    s_service = new StubLeaderboardService();
            }
            return s_service;
        }
    }

    // Сброс статики при входе в Play Mode (на случай отключённого Domain Reload).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_service = null;
        s_steamLeaderboardName = "SurvivalTime_v2";
        s_createIfMissing = true;
    }
}
