using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ход дуэли поверх лобби: готовность, синхронный старт, сообщение о поражении соперника
/// и его статистика по F3.
///
/// Объект создаёт себя сам и переживает перезагрузку сцены — правки MainScene не требуется,
/// а перезагрузка тут неизбежна: старт забега в проекте сделан именно через неё
/// (<see cref="UIFlowManager.StartButtonClicked"/>).
///
/// Вся статистика уезжает в ОДНО поле member data одной строкой. Steam ограничивает частоту
/// обновления данных лобби, и шесть отдельных ключей вместо одного упёрлись бы в лимит на
/// ровном месте.
/// </summary>
public class DuelSession : MonoBehaviour
{
    // Данные лобби (пишет только хост).
    public const string KeyState = "duel_state";
    public const string KeySeed = "duel_seed";

    // Данные участника (пишет каждый о себе).
    public const string KeyReady = "duel_ready";
    public const string KeyStat = "duel_stat";
    public const string KeyResult = "duel_result";

    public const string StateRunning = "running";

    /// <summary>Как часто публикуем свою статистику. Чаще нельзя — Steam режет частые обновления.</summary>
    private const float PublishInterval = 2f;

    private const float LossBannerSeconds = 8f;

    private static DuelSession s_instance;

    /// <summary>
    /// Ищет живой объект, если статик пуст. Присваивания при бутстрапе недостаточно:
    /// перезагрузка домена (а она случается при каждой перекомпиляции в редакторе)
    /// обнуляет статику, объект DontDestroyOnLoad при этом выживает, а
    /// RuntimeInitializeOnLoadMethod повторно не вызывается — ссылка терялась насовсем.
    /// </summary>
    public static DuelSession Instance
    {
        get
        {
            if (s_instance == null)
                s_instance = FindFirstObjectByType<DuelSession>(FindObjectsInactive.Include);

            return s_instance;
        }
        private set => s_instance = value;
    }

    /// <summary>Снимок состояния соперника, разобранный из его member data.</summary>
    public readonly struct OpponentStat
    {
        public readonly bool HasData;
        public readonly int Wave;
        public readonly float Time;
        public readonly float Hp;
        public readonly float HpMax;
        public readonly float Damage;
        public readonly bool IsAlive;

        public OpponentStat(int wave, float time, float hp, float hpMax, float damage, bool isAlive)
        {
            HasData = true;
            Wave = wave;
            Time = time;
            Hp = hp;
            HpMax = hpMax;
            Damage = damage;
            IsAlive = isAlive;
        }
    }

    private bool isReady;
    private bool runStarted;
    private float publishTimer;
    private float lossBannerLeft;
    private bool showStats;
    private bool opponentWasAlive = true;

    private PlayerHealth trackedHealth;
    private GameTimeUI timeUi;
    private EnemySpawner spawner;

    public bool IsReady => isReady;
    public bool RunStarted => runStarted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        // Обращение к Instance заодно подберёт объект, переживший перезагрузку домена.
        if (Instance != null)
            return;

        var host = new GameObject(nameof(DuelSession));
        Instance = host.AddComponent<DuelSession>();
        DontDestroyOnLoad(host);
    }

    private void OnDestroy()
    {
        Untrack();
        if (s_instance == this)
            s_instance = null;
    }

    // ---------- Готовность ----------

    /// <summary>Переключает свою готовность. Хост стартует матч, когда готовы все.</summary>
    public void ToggleReady()
    {
        if (!Lobbies.Service.IsInLobby)
            return;

        isReady = !isReady;
        Lobbies.Service.SetMemberValue(KeyReady, isReady ? "1" : "0");
        Debug.Log($"[Duel] Готовность: {(isReady ? "да" : "нет")}.");
    }

    private bool EveryoneReady()
    {
        var service = Lobbies.Service;
        IReadOnlyList<LobbyMember> members = service.Members;

        if (members.Count < 2)
            return false;

        for (int i = 0; i < members.Count; i++)
        {
            if (service.GetMemberValue(members[i].Id, KeyReady) != "1")
                return false;
        }

        return true;
    }

    // ---------- Ход матча ----------

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F2))
            ToggleReady();

        if (Input.GetKeyDown(KeyCode.F3))
            showStats = !showStats;

        if (lossBannerLeft > 0f)
            lossBannerLeft -= Time.unscaledDeltaTime;

        var service = Lobbies.Service;
        if (!service.IsInLobby)
        {
            runStarted = false;
            isReady = false;
            return;
        }

        if (service.IsHost)
            HostTick();

        if (!runStarted && service.GetLobbyValue(KeyState) == StateRunning)
            StartRun();

        if (runStarted)
            PublishTick();

        WatchOpponent();
    }

    /// <summary>Хост решает, когда матч начался: все готовы — пишет сид и состояние.</summary>
    private void HostTick()
    {
        var service = Lobbies.Service;

        if (service.GetLobbyValue(KeyState) == StateRunning || !EveryoneReady())
            return;

        int seed = Environment.TickCount ^ (int)DateTime.UtcNow.Ticks;

        // Сид кладём уже сейчас, чтобы обе стороны договорились об одном числе.
        // Пока он ни на что не влияет: забег по сиду живёт в отдельной ветке.
        service.SetLobbyValue(KeySeed, seed.ToString());
        service.SetLobbyValue(KeyState, StateRunning);

        Debug.Log($"[Duel] Все готовы, матч стартует. Сид {seed} (пока не применяется).");
    }

    private void StartRun()
    {
        runStarted = true;
        isReady = false;

        var service = Lobbies.Service;
        service.SetMemberValue(KeyResult, string.Empty);
        service.SetMemberValue(KeyReady, "0");
        service.SetMemberValue(KeyStat, string.Empty);

        opponentWasAlive = true;

        var flow = FindFirstObjectByType<UIFlowManager>();
        if (flow != null)
        {
            flow.StartButtonClicked();
        }
        else
        {
            Debug.LogWarning("[Duel] UIFlowManager не найден — забег не запустился.");
            runStarted = false;
        }
    }

    // ---------- Публикация своего состояния ----------

    private void PublishTick()
    {
        TrackHealth();

        publishTimer -= Time.unscaledDeltaTime;
        if (publishTimer > 0f)
            return;

        publishTimer = PublishInterval;
        Lobbies.Service.SetMemberValue(KeyStat, BuildStatLine(isAlive: true));
    }

    private string BuildStatLine(bool isAlive)
    {
        int wave = spawner != null ? spawner.CurrentWaveNumber : 0;
        float time = timeUi != null ? timeUi.ElapsedTime : 0f;
        float hp = trackedHealth != null ? trackedHealth.CurrentHealth : 0f;
        float hpMax = trackedHealth != null ? trackedHealth.MaxHealth : 0f;

        return string.Join(",", new[]
        {
            wave.ToString(),
            time.ToString("0", System.Globalization.CultureInfo.InvariantCulture),
            hp.ToString("0", System.Globalization.CultureInfo.InvariantCulture),
            hpMax.ToString("0", System.Globalization.CultureInfo.InvariantCulture),
            TotalDamage().ToString("0", System.Globalization.CultureInfo.InvariantCulture),
            isAlive ? "1" : "0",
        });
    }

    private static float TotalDamage()
    {
        if (DamageStatsManager.Instance == null)
            return 0f;

        float total = 0f;
        foreach (var entry in DamageStatsManager.Instance.GetDamageSorted())
            total += entry.damage;

        return total;
    }

    /// <summary>
    /// Ссылки живут ровно один забег: перезагрузка сцены их обнуляет, поэтому ищем заново,
    /// а на смерть подписываемся у того экземпляра, который сейчас в сцене.
    /// </summary>
    private void TrackHealth()
    {
        if (timeUi == null)
            timeUi = FindFirstObjectByType<GameTimeUI>();
        if (spawner == null)
            spawner = FindFirstObjectByType<EnemySpawner>();

        var health = FindFirstObjectByType<PlayerHealth>();
        if (health == trackedHealth)
            return;

        Untrack();
        trackedHealth = health;

        if (trackedHealth != null)
            trackedHealth.OnDied += OnLocalDeath;
    }

    private void Untrack()
    {
        if (trackedHealth != null)
            trackedHealth.OnDied -= OnLocalDeath;
        trackedHealth = null;
    }

    private void OnLocalDeath()
    {
        var service = Lobbies.Service;
        if (!service.IsInLobby)
            return;

        float time = timeUi != null ? timeUi.ElapsedTime : 0f;

        service.SetMemberValue(KeyStat, BuildStatLine(isAlive: false));
        service.SetMemberValue(KeyResult, time.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));

        Debug.Log($"[Duel] Забег окончен, результат {time:0} с отправлен сопернику.");
    }

    // ---------- Соперник ----------

    /// <summary>
    /// Соперник — тот участник, чья роль отличается от нашей: если мы хост, значит он не хост,
    /// и наоборот. Свой SteamID сервис наружу не отдаёт, а лобби рассчитано ровно на двоих,
    /// поэтому роли достаточно.
    /// </summary>
    public bool TryGetOpponent(out LobbyMember opponent)
    {
        var service = Lobbies.Service;
        IReadOnlyList<LobbyMember> members = service.Members;

        for (int i = 0; i < members.Count; i++)
        {
            if (members[i].IsHost != service.IsHost)
            {
                opponent = members[i];
                return true;
            }
        }

        opponent = default;
        return false;
    }

    public OpponentStat GetOpponentStat()
    {
        if (!TryGetOpponent(out LobbyMember opponent))
            return default;

        string raw = Lobbies.Service.GetMemberValue(opponent.Id, KeyStat);
        if (string.IsNullOrEmpty(raw))
            return default;

        string[] parts = raw.Split(',');
        if (parts.Length < 6)
            return default;

        var culture = System.Globalization.CultureInfo.InvariantCulture;
        int.TryParse(parts[0], out int wave);
        float.TryParse(parts[1], System.Globalization.NumberStyles.Float, culture, out float time);
        float.TryParse(parts[2], System.Globalization.NumberStyles.Float, culture, out float hp);
        float.TryParse(parts[3], System.Globalization.NumberStyles.Float, culture, out float hpMax);
        float.TryParse(parts[4], System.Globalization.NumberStyles.Float, culture, out float damage);

        return new OpponentStat(wave, time, hp, hpMax, damage, parts[5] == "1");
    }

    /// <summary>Ловим момент, когда соперник перестал быть живым, и показываем сообщение один раз.</summary>
    private void WatchOpponent()
    {
        OpponentStat stat = GetOpponentStat();
        if (!stat.HasData)
            return;

        if (opponentWasAlive && !stat.IsAlive)
            lossBannerLeft = LossBannerSeconds;

        opponentWasAlive = stat.IsAlive;
    }

    // ---------- Экран ----------

    private void OnGUI()
    {
        if (!Lobbies.Service.IsInLobby)
            return;

        if (!runStarted)
            DrawLobbyStrip();

        if (lossBannerLeft > 0f)
            DrawLossBanner();

        if (showStats)
            DrawOpponentPanel();
    }

    private void DrawLobbyStrip()
    {
        string text = isReady
            ? "Готов — ждём соперника (F2 отменить)"
            : "F2 — готов к дуэли";

        GUI.Label(new Rect(12f, 12f, 420f, 22f), $"[Дуэль] {text}");
    }

    private void DrawLossBanner()
    {
        OpponentStat stat = GetOpponentStat();
        string result = stat.HasData
            ? $"Соперник выбыл: волна {stat.Wave}, {FormatTime(stat.Time)}"
            : "Соперник выбыл";

        var rect = new Rect(Screen.width * 0.5f - 220f, 60f, 440f, 34f);
        GUI.Box(rect, string.Empty);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 22f), result);
    }

    private void DrawOpponentPanel()
    {
        var rect = new Rect(Screen.width - 272f, 12f, 260f, 150f);
        GUI.Box(rect, "Соперник (F3)");

        GUILayout.BeginArea(new Rect(rect.x + 10f, rect.y + 24f, rect.width - 20f, rect.height - 34f));

        if (!TryGetOpponent(out LobbyMember opponent))
        {
            GUILayout.Label("Соперника нет в лобби.");
            GUILayout.EndArea();
            return;
        }

        GUILayout.Label(opponent.Name);

        OpponentStat stat = GetOpponentStat();
        if (!stat.HasData)
        {
            GUILayout.Label("Данных пока нет.");
            GUILayout.Label("Появятся, когда он начнёт забег.");
            GUILayout.EndArea();
            return;
        }

        GUILayout.Label(stat.IsAlive ? "В забеге" : "Выбыл");
        GUILayout.Label($"Волна: {stat.Wave}");
        GUILayout.Label($"Время: {FormatTime(stat.Time)}");
        GUILayout.Label($"HP: {stat.Hp:0} / {stat.HpMax:0}");
        GUILayout.Label($"Урон: {stat.Damage:0}");

        GUILayout.EndArea();
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{total / 60:00}:{total % 60:00}";
    }
}
