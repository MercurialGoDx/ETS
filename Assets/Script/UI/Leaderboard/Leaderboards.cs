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
    private static string s_steamLeaderboardName = "SurvivalTime";

    /// <summary>
    /// Задать имя Steam-лидерборда до первого обращения к <see cref="Service"/>. После создания
    /// сервиса вызов игнорируется (имя уже зафиксировано).
    /// </summary>
    public static void Configure(string steamLeaderboardName)
    {
        if (s_service == null && !string.IsNullOrEmpty(steamLeaderboardName))
            s_steamLeaderboardName = steamLeaderboardName;
    }

    public static ILeaderboardService Service
    {
        get
        {
            if (s_service == null)
            {
                if (SteamManager.Initialized)
                    s_service = new SteamLeaderboardService(s_steamLeaderboardName);
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
    }
}
