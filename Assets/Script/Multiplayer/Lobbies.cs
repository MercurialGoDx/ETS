using UnityEngine;

/// <summary>
/// Единая точка доступа к лобби на весь сеанс. Держит один экземпляр сервиса живым — для
/// Steam это обязательно: асинхронные CallResult-ы и Callback-и не должны собираться GC
/// до ответа. Устроено так же, как <see cref="Leaderboards"/>.
///
/// Реализация выбирается автоматически: есть Steam — <see cref="SteamLobbyService"/>,
/// нет (редактор без клиента) — <see cref="StubLobbyService"/>.
/// </summary>
public static class Lobbies
{
    private static ILobbyService s_service;

    public static ILobbyService Service
    {
        get
        {
            if (s_service == null)
            {
                if (SteamManager.Initialized)
                    s_service = new SteamLobbyService();
                else
                    s_service = new StubLobbyService();
            }
            return s_service;
        }
    }

    /// <summary>Steam ли под нами. UI показывает разные подсказки: заглушка не соединяет игроков.</summary>
    public static bool IsSteamBacked => Service is SteamLobbyService;

    // Сброс статики при входе в Play Mode — на случай отключённого Domain Reload,
    // иначе лобби прошлой сессии редактора считалось бы живым.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_service = null;
    }
}
