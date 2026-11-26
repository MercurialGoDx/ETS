#if PLATFORM_MOBILE
using System;
using System.Collections.Generic;
using Firebase;
using Firebase.Analytics;
using Firebase.Auth;
using Firebase.Crashlytics;
using Firebase.Extensions;
using Firebase.RemoteConfig;
using UnityEngine;

/// <summary>
/// Подключение Firebase на телефоне: проверка зависимостей Google Play, Crashlytics,
/// Analytics, Remote Config и анонимный вход для таблицы рекордов.
///
/// Всё асинхронно и ничего не блокирует: игра стартует сразу, а сервисы подтягиваются
/// по мере готовности. Без сети или при сбое Firebase игра работает на локальных данных
/// (<see cref="LocalLeaderboardService"/>, значения Remote Config по умолчанию).
/// На ПК класса нет — PC/Steam-сборка Firebase не трогает.
/// </summary>
public static class FirebaseServices
{
    public const string LeaderboardEnabledKey = "leaderboard_enabled";
    public const string ConfigVersionKey = "config_version";

    private static readonly Dictionary<string, object> RemoteDefaults = new Dictionary<string, object>
    {
        { ConfigVersionKey, 1L },
        // Выключатель онлайн-таблицы: если с ней что-то пойдёт не так после релиза,
        // её можно погасить из консоли без новой сборки — игра откатится на локальные рекорды.
        { LeaderboardEnabledKey, true },
    };

    private static bool initializationStarted;

    /// <summary>Firebase поднят и зависимости Google Play на месте.</summary>
    public static bool IsAvailable { get; private set; }

    /// <summary>UID анонимного игрока или null, пока вход не выполнен.</summary>
    public static string UserId { get; private set; }

    /// <summary>Вызывается на главном потоке после успешного анонимного входа.</summary>
    public static event Action SignedIn;

    public static bool LeaderboardEnabled =>
        !IsAvailable || FirebaseRemoteConfig.DefaultInstance.GetValue(LeaderboardEnabledKey).BooleanValue;

    public static void Initialize()
    {
        if (initializationStarted)
            return;
        initializationStarted = true;

        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled || task.Result != DependencyStatus.Available)
            {
                Debug.LogWarning("[Firebase] Недоступен: "
                    + (task.Exception != null ? task.Exception.GetBaseException().Message : task.Result.ToString())
                    + ". Игра продолжает работать без онлайн-функций.");
                return;
            }

            try
            {
                // Без google-services.json (он не в репозитории) DefaultInstance бросит —
                // такая сборка просто живёт без онлайна.
                _ = FirebaseApp.DefaultInstance;
                Crashlytics.ReportUncaughtExceptionsAsFatal = true;
                Crashlytics.SetCustomKey("build_type", Debug.isDebugBuild ? "development" : "release");
                FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
                IsAvailable = true;

                InitializeRemoteConfig();
                SignInAnonymously();
                SendDevelopmentSmokeReport();
                Debug.Log("[Firebase] Готов.");
            }
            catch (Exception e)
            {
                IsAvailable = false;
                Debug.LogWarning("[Firebase] Не настроен (нет google-services.json?): " + e.Message);
            }
        });
    }

    public static void LogEvent(string name, params Parameter[] parameters)
    {
        if (!IsAvailable)
            return;
        FirebaseAnalytics.LogEvent(name, parameters);
    }

    private static void InitializeRemoteConfig()
    {
        var config = FirebaseRemoteConfig.DefaultInstance;
        config.SetDefaultsAsync(RemoteDefaults).ContinueWithOnMainThread(_ =>
            config.FetchAndActivateAsync().ContinueWithOnMainThread(fetch =>
            {
                if (fetch.IsFaulted)
                    Debug.LogWarning("[Firebase] Remote Config не загрузился, используются значения по умолчанию.");
            }));
    }

    private static void SignInAnonymously()
    {
        var auth = FirebaseAuth.DefaultInstance;
        if (auth.CurrentUser != null)
        {
            OnSignedIn(auth.CurrentUser.UserId);
            return;
        }

        auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted || task.IsCanceled)
            {
                // Без сети на первом запуске — попробуем при следующем старте;
                // рекорды до тех пор копятся локально.
                Debug.LogWarning("[Firebase] Анонимный вход не удался: "
                    + (task.Exception != null ? task.Exception.GetBaseException().Message : "отменён"));
                return;
            }

            OnSignedIn(task.Result.User.UserId);
        });
    }

    private static void OnSignedIn(string userId)
    {
        UserId = userId;
        Crashlytics.SetUserId(userId);
        Debug.Log("[Firebase] Анонимный вход выполнен.");
        SignedIn?.Invoke();
    }

    /// <summary>
    /// Один нефатальный отчёт на Development-сборке, чтобы Crashlytics в консоли
    /// активировался и было видно, что связка работает. В релизе не отправляется.
    /// </summary>
    private static void SendDevelopmentSmokeReport()
    {
        const string key = "firebase_crashlytics_smoke_sent";
        if (!Debug.isDebugBuild || PlayerPrefs.GetInt(key, 0) == 1)
            return;

        Crashlytics.LogException(new Exception("Crashlytics smoke test (development build)"));
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
    }
}
#endif
