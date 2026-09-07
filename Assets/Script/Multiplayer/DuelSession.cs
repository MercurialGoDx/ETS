using System;
using System.Collections.Generic;
using ETS.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Ход дуэли поверх лобби: готовность, синхронный старт, сообщение о поражении соперника
/// и его статистика по F3.
///
/// Объект создаёт себя сам и переживает перезагрузку сцены — правки MainScene не требуется,
/// а перезагрузка нужна, когда матч запускают повторно: сбросить состояние прошлого забега
/// в проекте можно только ею.
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
    public const string KeySpeed = "duel_speed";

    // Данные участника (пишет каждый о себе).
    public const string KeyReady = "duel_ready";
    public const string KeyStat = "duel_stat";
    public const string KeyResult = "duel_result";
    public const string KeyBuild = "duel_build";

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
    /// <summary>Идёт ли сейчас матч по сиду. Всё сидированное поведение живёт под этим флагом.</summary>
    public static bool IsSeeded => Instance != null && Instance.seedActive;

    /// <summary>Сид матча. Осмыслен только при <see cref="IsSeeded"/>.</summary>
    public static int Seed => Instance != null ? Instance.matchSeed : 0;

    /// <summary>Темпы, доступные в настройках лобби. После старта выбор блокируется.</summary>
    public static readonly float[] SpeedOptions = { 1f, 1.5f, 2f, 3f };

    public const float DefaultSpeed = 2f;

    /// <summary>Сколько ждём вернувшегося соперника, прежде чем продолжить без него.</summary>
    public const float DisconnectWaitSeconds = 60f;

    /// <summary>Сколько секунд идёт отсчёт перед стартом.</summary>
    public const float CountdownSeconds = 3f;

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

    // Ждём кадр после перезагрузки сцены: Start() у StartMenu должен успеть отработать,
    // иначе он выставит timeScale = 0 и вернёт меню поверх уже запущенного забега.
    private int startDelayFrames = -1;
    private bool seedActive;
    private int matchSeed;
    private float countdownLeft = -1f;
    private float waitLeft = -1f;
    private bool opponentWasPresent;

    /// <summary>Сколько осталось до старта. Отрицательное — отсчёта нет.</summary>
    public float CountdownLeft => countdownLeft;

    /// <summary>Сколько осталось ждать соперника. Отрицательное — не ждём.</summary>
    public float WaitLeft => waitLeft;

    /// <summary>Выбранный темп матча. До старта его меняет хост.</summary>
    public float SelectedSpeed
    {
        get
        {
            float value = ParseSpeed(Lobbies.Service.GetLobbyValue(KeySpeed));
            return value > 0f ? value : DefaultSpeed;
        }
    }

    /// <summary>
    /// Матч уже нельзя настраивать. Считаем от состояния лобби, а не от <see cref="runStarted"/>:
    /// между «хост нажал старт» и концом отсчёта проходят три секунды, и правка темпа в этот
    /// зазор развела бы стороны — каждая прочитала бы своё значение в момент своего StartRun.
    /// </summary>
    public bool MatchStarted => runStarted || Lobbies.Service.GetLobbyValue(KeyState) == StateRunning;

    /// <summary>Меняет темп до старта. Пишет только хост — данные лобби его.</summary>
    public void SetSpeed(float value)
    {
        if (MatchStarted || !Lobbies.Service.IsHost)
            return;

        Lobbies.Service.SetLobbyValue(KeySpeed, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static float ParseSpeed(string raw)
    {
        return float.TryParse(raw, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 0f;
    }

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
            ToggleOpponentBuild();

        if (lossBannerLeft > 0f)
            lossBannerLeft -= Time.unscaledDeltaTime;

        if (startDelayFrames >= 0)
        {
            startDelayFrames--;
            if (startDelayFrames < 0)
                OpenRun();
        }

        var service = Lobbies.Service;
        if (!service.IsInLobby)
        {
            runStarted = false;
            isReady = false;
            seedActive = false;
            matchSeed = 0;
            countdownLeft = -1f;
            waitLeft = -1f;
            opponentWasPresent = false;

            if (GameSpeedController.Instance != null)
                GameSpeedController.Instance.UnlockSpeed();

            return;
        }

        if (service.IsHost)
            HostTick();

        if (!runStarted && service.GetLobbyValue(KeyState) == StateRunning)
            TickCountdown();

        if (runStarted)
            PublishTick();

        WatchOpponent();
        WatchDisconnect();
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

    /// <summary>
    /// Отсчёт перед стартом: обе стороны видят одинаковые три секунды и входят в забег
    /// практически одновременно. Считаем в нескалированном времени — таймскейл в меню нулевой.
    /// </summary>
    private void TickCountdown()
    {
        if (countdownLeft < 0f)
        {
            countdownLeft = CountdownSeconds;
            matchSeed = ParseSeed(Lobbies.Service.GetLobbyValue(KeySeed));
            seedActive = matchSeed != 0;
            Debug.Log($"[Duel] Старт через {CountdownSeconds:0} с. Сид {matchSeed}.");
            return;
        }

        countdownLeft -= Time.unscaledDeltaTime;
        if (countdownLeft > 0f)
            return;

        countdownLeft = -1f;
        StartRun();
    }

    private static int ParseSeed(string raw)
    {
        return int.TryParse(raw, out int value) ? value : 0;
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

        // Из меню открываем забег на месте. Если предыдущий матч уже шёл или закончился,
        // состояние надо сбросить, а сделать это в проекте можно только перезагрузкой сцены.
        bool inMenu = GameStateManager.Instance != null
            && GameStateManager.Instance.Is(GameState.Menu);

        if (inMenu)
        {
            OpenRun();
            return;
        }

        startDelayFrames = 2;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Ведёт забег штатным путём: <see cref="StartMenu.StartGame"/> переводит состояние в
    /// Preparing, открывает игровой канвас и магазин. Мимо него ходить нельзя — GameState
    /// остался бы Menu, а из-за этого timeScale держится нулевым, StateRestrictedButton
    /// гасит кнопки, и ShopSlot отказывается продавать.
    /// </summary>
    private void OpenRun()
    {
        var menu = FindFirstObjectByType<StartMenu>(FindObjectsInactive.Include);
        if (menu == null)
        {
            Debug.LogWarning("[Duel] StartMenu не найден — забег не открылся.");
            runStarted = false;
            return;
        }

        menu.StartGame();

        // В дуэли кнопки «Готов» нет: забег начинается сразу у обоих, иначе один
        // закупается минуту, другой десять секунд, и старт волн разъезжается.
        var starter = FindFirstObjectByType<GameStartController>(FindObjectsInactive.Include);
        if (starter != null)
            starter.OnReadyClicked();
        else
            Debug.LogWarning("[Duel] GameStartController не найден — волны не запущены.");

        ApplyDuelSpeed();
        Debug.Log("[Duel] Забег начался у обоих.");
    }

    /// <summary>
    /// Скорость в дуэли фиксирована и не переключается игроком: разный темп сделал бы
    /// сравнение бессмысленным. Позже её можно вынести в настройки лобби — тогда обе
    /// стороны возьмут значение из данных лобби.
    /// </summary>
    private void ApplyDuelSpeed()
    {
        var speed = GameSpeedController.Instance;
        if (speed == null)
            return;

        speed.LockSpeed(SelectedSpeed);
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
        PublishBuild();
    }

    /// <summary>
    /// Отправляем полный снимок билда — тот же, что уходит в таблицу лидеров. Формат уже
    /// написан и умеет декодироваться, а BuildViewerUI умеет его показывать: своего кода
    /// для просмотра чужого билда писать не нужно.
    /// </summary>
    private void PublishBuild()
    {
        var inventory = FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
        if (inventory == null)
            return;

        BuildSnapshot snapshot = inventory.CaptureSnapshot();
        if (snapshot == null)
            return;

        int[] details = snapshot.Encode();
        var text = new System.Text.StringBuilder(details.Length * 4);
        for (int i = 0; i < details.Length; i++)
        {
            if (i > 0)
                text.Append(',');
            text.Append(details[i]);
        }

        Lobbies.Service.SetMemberValue(KeyBuild, text.ToString());
    }

    /// <summary>Снимок билда соперника или null, если он ещё ничего не прислал.</summary>
    public BuildSnapshot GetOpponentBuild()
    {
        if (!TryGetOpponent(out LobbyMember opponent))
            return null;

        string raw = Lobbies.Service.GetMemberValue(opponent.Id, KeyBuild);
        if (string.IsNullOrEmpty(raw))
            return null;

        string[] parts = raw.Split(',');
        var details = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            if (!int.TryParse(parts[i], out details[i]))
                return null;

        return BuildSnapshot.Decode(details);
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

    /// <summary>
    /// Соперник вышел — ставим забег на паузу и ждём минуту. Вернулся раньше — продолжаем
    /// сразу. Не вернулся — забег идёт дальше без него: держать игрока в паузе бесконечно
    /// из-за чужого разрыва нельзя.
    /// </summary>
    private void WatchDisconnect()
    {
        if (!runStarted)
        {
            waitLeft = -1f;
            return;
        }

        bool hasOpponent = Lobbies.Service.Members.Count >= 2;

        if (hasOpponent)
        {
            opponentWasPresent = true;

            if (waitLeft >= 0f)
            {
                waitLeft = -1f;
                ResumeAfterWait();
                Debug.Log("[Duel] Соперник вернулся, продолжаем.");
            }

            return;
        }

        if (!opponentWasPresent)
            return;

        if (waitLeft < 0f)
        {
            waitLeft = DisconnectWaitSeconds;
            PauseForWait();
            Debug.Log($"[Duel] Соперник отключился. Ждём {DisconnectWaitSeconds:0} с.");
            return;
        }

        waitLeft -= Time.unscaledDeltaTime;
        if (waitLeft > 0f)
            return;

        waitLeft = -1f;
        opponentWasPresent = false;
        ResumeAfterWait();
        Debug.Log("[Duel] Соперник не вернулся — забег продолжается без него.");
    }

    private static void PauseForWait()
    {
        var gsm = GameStateManager.Instance;
        if (gsm != null && gsm.Is(GameState.Playing))
            gsm.SetState(GameState.Paused);
    }

    private static void ResumeAfterWait()
    {
        var gsm = GameStateManager.Instance;
        if (gsm != null && gsm.Is(GameState.Paused))
            gsm.SetState(GameState.Playing);
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

    /// <summary>
    /// F3 — тот же экран, каким игрок смотрит чужие билды в таблице лидеров: всё купленное
    /// оружие, улучшения и полная статистика. Своего вида для этого не заводим.
    /// </summary>
    private void ToggleOpponentBuild()
    {
        var viewer = FindFirstObjectByType<BuildViewerUI>(FindObjectsInactive.Include);
        if (viewer == null)
        {
            Debug.LogWarning("[Duel] BuildViewerUI не найден в сцене.");
            return;
        }

        if (showStats)
        {
            viewer.Close();
            showStats = false;
            return;
        }

        string name = TryGetOpponent(out LobbyMember opponent) ? opponent.Name : "Соперник";
        viewer.Show(GetOpponentBuild(), name);
        showStats = true;
    }

    // ---------- Экран ----------

    private void OnGUI()
    {
        if (!Lobbies.Service.IsInLobby)
            return;

        if (!runStarted)
            DrawLobbyStrip();

        if (waitLeft >= 0f)
            DrawWaitBanner();

        if (lossBannerLeft > 0f)
            DrawLossBanner();

    }

    private void DrawLobbyStrip()
    {
        string text = isReady
            ? "Готов — ждём соперника (F2 отменить)"
            : "F2 — готов к дуэли";

        GUI.Label(new Rect(12f, 12f, 420f, 22f), $"[Дуэль] {text}");
    }

    private void DrawWaitBanner()
    {
        var rect = new Rect(Screen.width * 0.5f - 240f, Screen.height * 0.5f - 40f, 480f, 80f);
        GUI.Box(rect, string.Empty);
        GUI.Label(new Rect(rect.x + 16f, rect.y + 14f, rect.width - 32f, 24f),
            "Соперник отключился");
        GUI.Label(new Rect(rect.x + 16f, rect.y + 42f, rect.width - 32f, 24f),
            $"Ждём возвращения: {Mathf.CeilToInt(waitLeft)} с");
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


    private static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{total / 60:00}:{total % 60:00}";
    }
}
