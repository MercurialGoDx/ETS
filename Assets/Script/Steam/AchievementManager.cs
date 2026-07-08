using System.Collections.Generic;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

/// <summary>
/// Разблокировка достижений Steam по игровым событиям. Сами достижения должны быть заведены на
/// Steamworks partner-сайте с ТОЧНО такими же API-именами (через API достижения не создаются) —
/// код лишь вызывает SetAchievement по имени.
///
/// Набор:
///   SURVIVE_10..60 — выжить N минут (опрос времени забега);
///   ALL_WEAPONS / ALL_UPGRADES — купить все виды оружия / все улучшения (кумулятивно, PlayerPrefs);
///   BOSS_SLAYER — победить босса;
///   SHOPAHOLIC — сделать 30 покупок за один забег.
///
/// Вне Steam (редактор без клиента) — безопасный no-op. Достижения-до-готовности-статистики
/// ставятся в очередь и разблокируются после UserStatsReceived.
/// </summary>
public class AchievementManager : MonoBehaviour
{
    public static AchievementManager Instance { get; private set; }

    // --- Конфигурация достижений ---
    private static readonly int[] SurvivalMinutes = { 10, 20, 30, 40, 50, 60 };
    private const string AllWeapons = "ALL_WEAPONS";
    private const string AllUpgrades = "ALL_UPGRADES";
    private const string BossSlayer = "BOSS_SLAYER";
    private const string Shopaholic = "SHOPAHOLIC";
    private const int ShopaholicTarget = 30;

    private const string PrefWeapons = "ach_bought_weapons";
    private const string PrefUpgrades = "ach_bought_upgrades";

    private readonly HashSet<string> unlocked = new HashSet<string>();
    private readonly HashSet<string> boughtWeapons = new HashSet<string>();
    private readonly HashSet<string> boughtUpgrades = new HashSet<string>();

    private int runPurchases;
    private float pollTimer;
    private GameTimeUI gameTimeUI;

    private bool statsReady;
    private readonly List<string> pending = new List<string>();

#if !DISABLESTEAMWORKS
    private Callback<UserStatsReceived_t> statsReceivedCallback;
#endif

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        LoadSet(PrefWeapons, boughtWeapons);
        LoadSet(PrefUpgrades, boughtUpgrades);
    }

    private void Start()
    {
#if !DISABLESTEAMWORKS
        if (SteamManager.Initialized)
        {
            statsReceivedCallback = Callback<UserStatsReceived_t>.Create(OnUserStatsReceived);
            // Запрос актуальных статов/достижений игрока. До ответа разблокировки копятся в pending.
            SteamUserStats.RequestCurrentStats();
        }
#endif
        if (GameStateManager.Instance != null)
            GameStateManager.Instance.OnStateChanged += OnStateChanged;
    }

    private void OnDestroy()
    {
        if (GameStateManager.Instance != null)
            GameStateManager.Instance.OnStateChanged -= OnStateChanged;
        if (Instance == this)
            Instance = null;
    }

    private void OnStateChanged(GameState state)
    {
        // Начало забега — обнуляем счётчик покупок для SHOPAHOLIC.
        if (state == GameState.Playing && GameStateManager.Instance.PreviousState != GameState.Paused)
            runPurchases = 0;
    }

    private void Update()
    {
        // Достижения за выживание: раз в секунду сверяем время забега с порогами.
        pollTimer += Time.unscaledDeltaTime;
        if (pollTimer < 1f)
            return;
        pollTimer = 0f;

        if (gameTimeUI == null)
            gameTimeUI = FindObjectOfType<GameTimeUI>();
        if (gameTimeUI == null)
            return;

        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState != GameState.Playing)
            return;

        float minutes = gameTimeUI.ElapsedTime / 60f;
        for (int i = 0; i < SurvivalMinutes.Length; i++)
        {
            if (minutes >= SurvivalMinutes[i])
                Unlock("SURVIVE_" + SurvivalMinutes[i]);
        }
    }

    // ---------- Публичные уведомления из игровых систем ----------

    public void NotifyWeaponPurchased(WeaponDefinition weapon)
    {
        if (weapon != null && boughtWeapons.Add(weapon.name))
            SaveSet(PrefWeapons, boughtWeapons);

        CountRunPurchase();

        if (HasAll(boughtWeapons, GetCatalogNames(true)))
            Unlock(AllWeapons);
    }

    public void NotifyUpgradePurchased(UpgradeBaseSO upgrade)
    {
        if (upgrade != null && boughtUpgrades.Add(upgrade.name))
            SaveSet(PrefUpgrades, boughtUpgrades);

        CountRunPurchase();

        if (HasAll(boughtUpgrades, GetCatalogNames(false)))
            Unlock(AllUpgrades);
    }

    public void NotifyBossDefeated()
    {
        Unlock(BossSlayer);
    }

    private void CountRunPurchase()
    {
        runPurchases++;
        if (runPurchases >= ShopaholicTarget)
            Unlock(Shopaholic);
    }

    // ---------- Каталог (сколько всего оружия/улучшений в игре) ----------

    private ShopManager shop;

    private HashSet<string> GetCatalogNames(bool weapons)
    {
        if (shop == null)
            shop = FindObjectOfType<ShopManager>();

        var names = new HashSet<string>();
        if (shop == null)
            return names;

        if (weapons)
        {
            if (shop.availableWeapons != null)
                foreach (var w in shop.availableWeapons)
                    if (w != null) names.Add(w.name);
        }
        else
        {
            if (shop.availableUpgrades != null)
                foreach (var u in shop.availableUpgrades)
                    if (u != null) names.Add(u.name);
        }
        return names;
    }

    private static bool HasAll(HashSet<string> bought, HashSet<string> catalog)
    {
        if (catalog == null || catalog.Count == 0)
            return false; // каталог ещё не известен — не засчитываем
        foreach (var name in catalog)
            if (!bought.Contains(name))
                return false;
        return true;
    }

    // ---------- Разблокировка ----------

    private void Unlock(string api)
    {
        if (string.IsNullOrEmpty(api) || unlocked.Contains(api))
            return;

#if !DISABLESTEAMWORKS
        if (!SteamManager.Initialized)
            return;

        if (!statsReady)
        {
            if (!pending.Contains(api))
                pending.Add(api);
            return;
        }

        SteamUserStats.SetAchievement(api);
        SteamUserStats.StoreStats();
#endif
        unlocked.Add(api);
    }

#if !DISABLESTEAMWORKS
    private void OnUserStatsReceived(UserStatsReceived_t result)
    {
        // Статы текущего приложения получены — можно ставить достижения.
        if (SteamUtils.GetAppID().m_AppId != result.m_nGameID)
            return;

        statsReady = true;

        if (pending.Count > 0)
        {
            var flush = new List<string>(pending);
            pending.Clear();
            foreach (var api in flush)
                Unlock(api);
        }
    }
#endif

    // ---------- Персист набора купленного (PlayerPrefs) ----------

    private static void LoadSet(string key, HashSet<string> set)
    {
        string raw = PlayerPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(raw))
            return;
        foreach (var name in raw.Split('\n'))
            if (!string.IsNullOrEmpty(name))
                set.Add(name);
    }

    private static void SaveSet(string key, HashSet<string> set)
    {
        PlayerPrefs.SetString(key, string.Join("\n", new List<string>(set).ToArray()));
        PlayerPrefs.Save();
    }
}
