using System;
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
                s_service = Create();

            return s_service;
        }
    }

    /// <summary>
    /// Результат кэшируется в любом случае, даже неудачный. Steamworks бросает
    /// «Callback dispatcher is not initialized», если регистрировать колбэки, пока диспетчер
    /// не поднят или уже снят (например, на переходе в Play Mode и обратно). Без кэша
    /// конструктор падал бы каждый кадр, а вызывающий код зовёт Service из Update.
    /// </summary>
    private static ILobbyService Create()
    {
        if (!SteamManager.Initialized)
            return new StubLobbyService();

        try
        {
            return new SteamLobbyService();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Lobbies] Steam не отдал колбэки (" + e.Message
                + ") — сессия работает на заглушке, игроки не соединятся.");
            return new StubLobbyService();
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
