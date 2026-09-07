using System.Collections;
using System.Collections.Generic;
using ETS.Multiplayer;
using UnityEngine;

/// <summary>
/// Панель инвентаря (Tab): всё купленное за забег — оружие, улучшения и колонка
/// сводных характеристик. Только чтение.
/// На время открытия ставит игру на паузу и прячет магазин, реролл, разблокировку,
/// золото и кнопки скорости — список настраивается полем hideWhileOpen.
/// Данные перечитываются при каждом открытии; живой подписки нет, статы
/// обновляются корутиной раз в statsRefreshInterval.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("Клавиша")]
    [Tooltip("Открыть/закрыть инвентарь.")]
    public KeyCode toggleKey = KeyCode.Tab;

    [Header("Окно")]
    [Tooltip("Корень панели — включается/выключается при открытии/закрытии.")]
    [SerializeField] private GameObject panelRoot;

    [Header("Сетки")]
    [Tooltip("Контейнер сетки оружия (GridLayoutGroup, 4 колонки).")]
    [SerializeField] private Transform weaponsContent;
    [Tooltip("Контейнер сетки улучшений (GridLayoutGroup, 8 колонок).")]
    [SerializeField] private Transform upgradesContent;
    [SerializeField] private InventorySlot weaponSlotPrefab;
    [SerializeField] private InventorySlot upgradeSlotPrefab;

    [Header("Статы")]
    [Tooltip("Контейнер общих строк характеристик (VerticalLayoutGroup).")]
    [SerializeField] private Transform statsContent;
    [Tooltip("Контейнер строк «урон по типам» — идёт под своим заголовком.")]
    [SerializeField] private Transform damageTypesContent;
    [Tooltip("Заголовок блока урона по типам. Обычный объект сцены — текст правится в инспекторе.")]
    [SerializeField] private GameObject damageTypesHeader;
    [Tooltip("Контейнер строк «показатели врага» — самый нижний блок.")]
    [SerializeField] private Transform enemyStatsContent;
    [Tooltip("Заголовок блока показателей врага. Тоже объект сцены.")]
    [SerializeField] private GameObject enemyStatsHeader;
    [Tooltip("Префаб строки: подпись слева, значение справа.")]
    [SerializeField] private StatRow statRowPrefab;
    [Tooltip("Период обновления статов, пока панель открыта.")]
    [SerializeField] private float statsRefreshInterval = 0.25f;

    [Header("Оформление тиров (индекс 0 = Tier 1)")]
    [SerializeField] private Color[] tierColors =
    {
        new Color(0.53f, 0.53f, 0.50f), // Tier 1 — серый
        new Color(0.11f, 0.62f, 0.46f), // Tier 2 — бирюзовый
        new Color(0.50f, 0.47f, 0.87f), // Tier 3 — фиолетовый
        new Color(0.94f, 0.62f, 0.15f), // Tier 4 — золотой
    };
    [Tooltip("Во сколько раз рамка четвёртого тира толще остальных.")]
    [SerializeField] private float tier4FrameThickness = 2f;
    [Tooltip("Прозрачность подложки ячейки (тон берётся из цвета тира).")]
    [Range(0f, 1f)]
    [SerializeField] private float tierBackgroundAlpha = 0.18f;

    [Header("Ссылки")]
    [SerializeField] private TowerAttack towerAttack;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerShield playerShield;
    [SerializeField] private PauseManager pauseManager;
    [Tooltip("Источник показателей врага. Может быть выключен в сцене — значения всё равно считаются.")]
    [SerializeField] private EnemyStatsProgressionUI enemyStats;
    [Tooltip("Каталог оружия/улучшений — нужен только для CaptureSnapshot (компактные id для лидерборда).")]
    [SerializeField] private ShopManager shopManager;

    [Header("Скрывать, пока панель открыта")]
    [Tooltip("Магазин, реролл, разблокировка, кнопки скорости. Список правится в инспекторе — " +
             "исходное состояние каждого объекта запоминается и возвращается при закрытии.")]
    [SerializeField] private GameObject[] hideWhileOpen;

    private readonly List<GameObject> spawnedSlots = new List<GameObject>();

    // Строки статов переиспользуются между обновлениями: панель перерисовывается
    // 4 раза в секунду, Instantiate/Destroy на каждом тике был бы мусором для GC.
    // Пулов два — по одному на контейнер, потому что между ними лежит заголовок,
    // который живёт в сцене и не должен попадать в переиспользование.
    private readonly List<StatRow> statRowPool = new List<StatRow>();
    private int usedRows;
    private readonly List<StatRow> typeRowPool = new List<StatRow>();
    private int usedTypeRows;
    private readonly List<StatRow> enemyRowPool = new List<StatRow>();
    private int usedEnemyRows;

    // Что было включено до открытия панели — чтобы вернуть ровно это, а не «всё подряд».
    private bool[] hiddenPrevState;

    private GameState stateBeforeOpen = GameState.Playing;

    // Ставила ли паузу именно панель. На экране смерти игра уже остановлена сама,
    // и трогать состояние с разрешением на паузу там нельзя.
    private bool pausedByPanel;

    private Coroutine statsRoutine;
    private WaitForSecondsRealtime statsWait;

    private bool isOpen;

    // Ключи локализации типов урона в порядке enum WeaponDamageType.
    private static readonly string[] DamageTypeKeys =
    {
        "inv.dmg_magic", "inv.dmg_piercing", "inv.dmg_normal",
        "inv.dmg_projectile", "inv.dmg_heavy", "inv.dmg_chaos", "inv.dmg_holy"
    };

    private static string L(string key) => StatFormat.L(key);

    private void Awake()
    {
        // На старте окно скрыто.
        if (panelRoot != null)
            panelRoot.SetActive(false);

        statsWait = new WaitForSecondsRealtime(statsRefreshInterval);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            if (isOpen)
                Close();
            else
                TryOpen();
        }

        // Escape закрывает панель, а не открывает паузу: пока панель открыта,
        // PauseManager отключён через SetCanPause(false).
        if (isOpen && Input.GetKeyDown(KeyCode.Escape))
            Close();
    }

    // ===================== ОТКРЫТИЕ / ЗАКРЫТИЕ =====================

    /// <summary>Публичный вход для кнопок (напр. Journal_Sel_Stat) — та же логика, что и по toggleKey.</summary>
    public void Open()
    {
        if (!isOpen)
            TryOpen();
    }

    private void TryOpen()
    {
        // Открывается во время забега и на экране смерти — посмотреть, с чем закончил.
        // В меню и в обычной паузе не нужен.
        if (GameStateManager.Instance != null)
        {
            var state = GameStateManager.Instance.CurrentState;
            if (state != GameState.Playing && state != GameState.Preparing && state != GameState.GameOver)
                return;

            // На GameOver время уже стоит, а лишняя смена состояния дёрнула бы
            // OnStateChanged у подписчиков и сбила PreviousState, на который смотрит
            // PauseManager при выходе из паузы. Поэтому там состояние не трогаем.
            pausedByPanel = state != GameState.GameOver;
        }

        isOpen = true;

        // Ставим игру на паузу. GameState.Paused сам выставляет Time.timeScale = 0,
        // а возврат в прежнее состояние восстановит скорость, выбранную кнопками x1/x2/x3.
        // В дуэли пауза запрещена: пока один изучает статистику, второй горит.
        if (DuelSession.IsSeeded)
            pausedByPanel = false;

        if (pausedByPanel && GameStateManager.Instance != null)
        {
            stateBeforeOpen = GameStateManager.Instance.CurrentState;
            GameStateManager.Instance.SetState(GameState.Paused);
        }

        if (panelRoot != null)
            panelRoot.SetActive(true);

        HideDistractions();

        // История покупок стоит слева по центру и налезала бы на рамку — прячем, как журнал.
        if (PurchaseHistoryManager.Instance != null)
            PurchaseHistoryManager.Instance.SetVisible(false);

        // После смерти паузу и так нельзя открыть — не выдаём разрешение, которого не было.
        if (pausedByPanel && pauseManager != null)
            pauseManager.SetCanPause(false);

        RebuildGrids();
        RefreshStats();

        if (statsRoutine != null)
            StopCoroutine(statsRoutine);
        statsRoutine = StartCoroutine(StatsLoop());
    }

    private void Close()
    {
        isOpen = false;

        if (statsRoutine != null)
        {
            StopCoroutine(statsRoutine);
            statsRoutine = null;
        }

        if (WeaponTooltip.Instance != null)
            WeaponTooltip.Instance.Hide();

        if (panelRoot != null)
            panelRoot.SetActive(false);

        RestoreDistractions();

        if (PurchaseHistoryManager.Instance != null)
            PurchaseHistoryManager.Instance.SetVisible(true);

        // Снимаем паузу, возвращая ровно то состояние, что было до открытия.
        if (pausedByPanel && GameStateManager.Instance != null)
            GameStateManager.Instance.SetState(stateBeforeOpen);

        // Паузу возвращаем через кадр: если PauseManager.Update успеет отработать
        // после нашего в этом же кадре, он увидит тот же Escape и откроет меню паузы.
        if (pausedByPanel)
            StartCoroutine(EnablePauseNextFrame());

        pausedByPanel = false;
    }

    private IEnumerator EnablePauseNextFrame()
    {
        yield return null;
        if (pauseManager != null)
            pauseManager.SetCanPause(true);
    }

    /// <summary>
    /// Прячет магазин, реролл, разблокировку и кнопки скорости: пока игра стоит на паузе,
    /// они всё равно бесполезны, а панель ими перекрывается.
    /// </summary>
    private void HideDistractions()
    {
        if (hideWhileOpen == null) return;

        if (hiddenPrevState == null || hiddenPrevState.Length != hideWhileOpen.Length)
            hiddenPrevState = new bool[hideWhileOpen.Length];

        for (int i = 0; i < hideWhileOpen.Length; i++)
        {
            if (hideWhileOpen[i] == null) continue;

            // Запоминаем, а не включаем всё подряд при закрытии: часть объектов
            // может быть законно выключена игрой (например, магазин вне подготовки).
            hiddenPrevState[i] = hideWhileOpen[i].activeSelf;
            hideWhileOpen[i].SetActive(false);
        }
    }

    private void RestoreDistractions()
    {
        if (hideWhileOpen == null || hiddenPrevState == null) return;

        for (int i = 0; i < hideWhileOpen.Length && i < hiddenPrevState.Length; i++)
        {
            if (hideWhileOpen[i] != null)
                hideWhileOpen[i].SetActive(hiddenPrevState[i]);
        }
    }

    private IEnumerator StatsLoop()
    {
        while (isOpen)
        {
            yield return statsWait;
            RefreshStats();
        }
    }

    // ===================== СЕТКИ =====================

    private void RebuildGrids()
    {
        ClearSlots();

        if (towerAttack != null && weaponsContent != null && weaponSlotPrefab != null)
        {
            foreach (var owned in towerAttack.GetOwnedWeapons())
            {
                InventorySlot slot = Instantiate(weaponSlotPrefab, weaponsContent);
                slot.SetupWeapon(owned.Def, owned.Stacks);
                ApplyTierLook(slot, owned.Def.itemTier);
                spawnedSlots.Add(slot.gameObject);
            }
        }

        // Счётчики покупок улучшений живут в UpgradesManager.RuntimeData — это НЕ тот же
        // экземпляр, что context.runtime со статами (см. RefreshStats).
        if (UpgradesManager.Instance != null && upgradesContent != null && upgradeSlotPrefab != null)
        {
            foreach (var pair in UpgradesManager.Instance.RuntimeData.UpgradePurchaseCounts)
            {
                if (pair.Key == null) continue;

                InventorySlot slot = Instantiate(upgradeSlotPrefab, upgradesContent);
                slot.SetupUpgrade(pair.Key, pair.Value);
                ApplyTierLook(slot, pair.Key.itemTier);
                spawnedSlots.Add(slot.gameObject);
            }
        }
    }

    private void ApplyTierLook(InventorySlot slot, ItemTier tier)
    {
        // None и Tier1 красим одинаково — первым цветом.
        int index = Mathf.Clamp((int)tier - 1, 0, tierColors.Length - 1);
        float thickness = tier == ItemTier.Tier4 ? tier4FrameThickness : 1f;
        slot.SetTierFrame(tierColors[index], thickness, tierBackgroundAlpha);
    }

    private void ClearSlots()
    {
        for (int i = 0; i < spawnedSlots.Count; i++)
        {
            if (spawnedSlots[i] != null)
                Destroy(spawnedSlots[i]);
        }
        spawnedSlots.Clear();
    }

    // ===================== СТАТЫ =====================

    private void RefreshStats()
    {
        if (statsContent == null || statRowPrefab == null)
            return;

        usedRows = 0;
        usedTypeRows = 0;
        usedEnemyRows = 0;

        // ВАЖНО: статы пишутся апгрейдами в context.runtime (Apply(context)), а НЕ в
        // UpgradesManager.RuntimeData — GameInstaller создаёт под контекст отдельный
        // экземпляр. GameplayRuntimeData отдаёт именно тот, что читает DamageCalculator;
        // через второй всё показывало бы прочерки.
        var runtime = UpgradesManager.Instance != null
            ? UpgradesManager.Instance.GameplayRuntimeData
            : null;

        // Порядок осмысленный: сначала выживание (HP и лечение), затем митигация
        // (щит, блок, снижение урона), затем урон и темп, в конце экономика забега.
        if (playerHealth != null)
        {
            // Только максимум: текущее значение и так видно на полоске HP.
            Row(L("inv.health"), Int(playerHealth.MaxHealth));
            Row(L("inv.health_regen"), Rate(playerHealth.GetTotalRegen()));
            Row(L("inv.healing_from_max_health"),
                StatFormat.PercentPoints(playerHealth.HealingPercentFromMissingHealth));
            Row(L("inv.heal_on_kill"), Num(playerHealth.HealOnKillPerEnemy));
            Row(L("inv.heal_on_hit"), Num(playerHealth.HealOnHitFromEnemy));
            Row(L("inv.heal_amp"), Percent(playerHealth.HealAmplificationPercent));
        }

        if (playerShield != null)
        {
            Row(L("inv.shield"), playerShield.MaxShield > Eps ? Int(playerShield.MaxShield) : "—");
            Row(L("inv.shield_on_kill"), Num(playerShield.ShieldRestorePerEnemyKill));
        }

        if (playerHealth != null)
        {
            Row(L("inv.block_chance"), Percent(playerHealth.BlockChance));
            Row(L("inv.damage_reduction"), Percent(playerHealth.DamageReduction));
            Row(L("inv.spikes_damage"), Num(playerHealth.SpikesCount));
        }

        if (towerAttack != null)
        {
            Row(L("inv.attack_speed"), Multiplier(towerAttack.TotalFireRateMultiplier));
            // Итоговый TotalMultiplierDamage по всему арсеналу: со всеми слоями бонусов,
            // включая тир и тип каждого оружия.
            Row(L("inv.damage_multiplier"), Multiplier(towerAttack.GetFinalDamageMultiplier()));
        }

        if (runtime != null && runtime.enemySpawner != null)
            Row(L("inv.more_enemies"), Percent(runtime.enemySpawner.EnemiesPerWavePercentBonus));

        if (GoldManager.Instance != null)
        {
            // Пассивный доход намеренно не умножается на бонус золота (см. GoldManager.AddGold),
            // поэтому показываем ровно ту сумму, что капает в секунду.
            Row(L("inv.income"), Int(GoldManager.Instance.goldPerTick) + L("inv.per_second"));
        }

        // Процентный бонус к получаемому золоту (GoldenSkull и подобные). Пассивный доход
        // он не трогает — только убийства, награды и прочие разовые начисления.
        // Не путать с UpgradesManager.goldBonusPerKill: в то поле никто не пишет.
        if (GoldManager.Instance != null)
            Row(L("inv.gold_bonus"), Percent(GoldManager.Instance.GoldGainBonus));

        // Охота: шанс, что заспавнится золотой враг (EnemySpawner сверяется с этим значением).
        if (runtime != null)
            Row(L("inv.hunt_chance"), Percent(runtime.GoldenEnemyChance));

        // Блок урона по типам живёт в отдельном контейнере под своим заголовком.
        // Заголовок — объект сцены, а не сгенерированная строка: так его текст, шрифт
        // и размер правятся в инспекторе, без захода в код.
        bool showTypes = runtime != null && towerAttack != null;

        if (damageTypesHeader != null)
            damageTypesHeader.SetActive(showTypes);

        if (showTypes)
        {
            // Итог по типу — как в DamageTypeBonus: плоский бонус + за-каждое-оружие × число оружий.
            foreach (WeaponDamageType type in System.Enum.GetValues(typeof(WeaponDamageType)))
            {
                float flat = runtime.damageTypeFlatPercent.TryGetValue(type, out float flatValue)
                    ? flatValue
                    : 0f;
                float perWeapon = runtime.damageTypePerWeaponPercent.TryGetValue(type, out float perWeaponValue)
                    ? perWeaponValue
                    : 0f;
                float total = flat + towerAttack.GetTotalWeaponsOfType(type) * perWeapon;

                TypeRow(L(DamageTypeKeys[(int)type]), Percent(total));
            }
        }

        // Показатели текущей волны — самый нижний блок, тоже со своим заголовком в сцене.
        bool showEnemy = enemyStats != null;

        if (enemyStatsHeader != null)
            enemyStatsHeader.SetActive(showEnemy);

        if (showEnemy)
        {
            // Округляем, как это делал прежний текст слева: дробные HP врага только путают.
            EnemyRow(L("inv.enemy_health"), Int(enemyStats.CurrentEnemyHealth));
            EnemyRow(L("inv.enemy_damage"), Int(enemyStats.CurrentEnemyDamage));
        }

        // Лишние строки с прошлого обновления прячем, а не удаляем — переиспользуем.
        for (int i = usedRows; i < statRowPool.Count; i++)
            statRowPool[i].gameObject.SetActive(false);
        for (int i = usedTypeRows; i < typeRowPool.Count; i++)
            typeRowPool[i].gameObject.SetActive(false);
        for (int i = usedEnemyRows; i < enemyRowPool.Count; i++)
            enemyRowPool[i].gameObject.SetActive(false);
    }

    /// <summary>Обычная строка характеристик.</summary>
    private void Row(string label, string value)
    {
        Take(statRowPool, ref usedRows, statsContent).Set(label, value);
    }

    /// <summary>Строка блока «урон по типам» — в своём контейнере, со своим пулом.</summary>
    private void TypeRow(string label, string value)
    {
        Transform parent = damageTypesContent != null ? damageTypesContent : statsContent;
        Take(typeRowPool, ref usedTypeRows, parent).Set(label, value);
    }

    /// <summary>Строка блока «показатели врага».</summary>
    private void EnemyRow(string label, string value)
    {
        Transform parent = enemyStatsContent != null ? enemyStatsContent : statsContent;
        Take(enemyRowPool, ref usedEnemyRows, parent).Set(label, value);
    }

    /// <summary>
    /// Берёт следующую строку из указанного пула, создавая её при нехватке. Порядок в пуле
    /// совпадает с порядком вызовов, поэтому строки идут сверху вниз так же, как в коде.
    /// </summary>
    private StatRow Take(List<StatRow> pool, ref int used, Transform parent)
    {
        StatRow row;

        if (used < pool.Count)
        {
            row = pool[used];
        }
        else
        {
            row = Instantiate(statRowPrefab, parent);
            pool.Add(row);
        }

        row.gameObject.SetActive(true);
        row.transform.SetSiblingIndex(used);
        used++;
        return row;
    }

    // ===================== ФОРМАТИРОВАНИЕ (StatFormat — общий с BuildViewerUI) =====================

    private const float Eps = 0.0001f;

    private static string Percent(float fraction) => StatFormat.Percent(fraction);
    private static string Multiplier(float value) => StatFormat.Multiplier(value);
    private static string Int(float value) => StatFormat.Int(value);
    private static string Num(float value) => StatFormat.Num(value);
    private static string Rate(float value) => StatFormat.Rate(value);

    // ===================== СНИМОК БИЛДА (для лидерборда) =====================

    /// <summary>
    /// Снимок билда на момент вызова (обычно — смерть игрока): те же данные, что видно
    /// в этой панели — купленное оружие/улучшения (в виде компактных id из BuildCatalog)
    /// и посчитанные статы. Кодируется в int[] и уходит в Steam вместе с результатом
    /// забега (см. GameOverController, ILeaderboardService.SubmitTime).
    /// </summary>
    public BuildSnapshot CaptureSnapshot()
    {
        var snapshot = new BuildSnapshot();

        if (shopManager != null)
        {
            if (towerAttack != null)
            {
                foreach (var owned in towerAttack.GetOwnedWeapons())
                {
                    int id = BuildCatalog.WeaponId(shopManager, owned.Def);
                    if (id >= 0)
                        snapshot.items.Add(new BuildSnapshot.Item(id, owned.Stacks));
                }
            }

            if (UpgradesManager.Instance != null)
            {
                foreach (var pair in UpgradesManager.Instance.RuntimeData.UpgradePurchaseCounts)
                {
                    if (pair.Key == null) continue;

                    int id = BuildCatalog.UpgradeId(shopManager, pair.Key);
                    if (id >= 0)
                        snapshot.items.Add(new BuildSnapshot.Item(id, pair.Value));
                }
            }
        }

        var runtime = UpgradesManager.Instance != null
            ? UpgradesManager.Instance.GameplayRuntimeData
            : null;

        if (playerHealth != null)
        {
            snapshot.SetStat(BuildStat.Health, playerHealth.MaxHealth);
            snapshot.SetStat(BuildStat.HealthRegen, playerHealth.GetTotalRegen());
            snapshot.SetStat(BuildStat.HealingPercentFromMaxHealth,
                playerHealth.HealingPercentFromMissingHealth);
            snapshot.SetStat(BuildStat.HealOnKill, playerHealth.HealOnKillPerEnemy);
            snapshot.SetStat(BuildStat.HealOnHit, playerHealth.HealOnHitFromEnemy);
            snapshot.SetStat(BuildStat.HealAmp, playerHealth.HealAmplificationPercent);
            snapshot.SetStat(BuildStat.BlockChance, playerHealth.BlockChance);
            snapshot.SetStat(BuildStat.DamageReduction, playerHealth.DamageReduction);
            snapshot.SetStat(BuildStat.SpikesDamage, playerHealth.SpikesCount);
        }

        if (playerShield != null)
        {
            snapshot.SetStat(BuildStat.Shield, playerShield.MaxShield);
            snapshot.SetStat(BuildStat.ShieldOnKill, playerShield.ShieldRestorePerEnemyKill);
        }

        if (towerAttack != null)
        {
            snapshot.SetStat(BuildStat.AttackSpeed, towerAttack.TotalFireRateMultiplier);
            snapshot.SetStat(BuildStat.DamageMultiplier, towerAttack.GetFinalDamageMultiplier());
        }

        if (runtime != null && runtime.enemySpawner != null)
            snapshot.SetStat(BuildStat.MoreEnemies, runtime.enemySpawner.EnemiesPerWavePercentBonus);

        if (GoldManager.Instance != null)
        {
            snapshot.SetStat(BuildStat.Income, GoldManager.Instance.goldPerTick);
            snapshot.SetStat(BuildStat.GoldBonus, GoldManager.Instance.GoldGainBonus);
        }

        if (runtime != null)
            snapshot.SetStat(BuildStat.HuntChance, runtime.GoldenEnemyChance);

        if (runtime != null && towerAttack != null)
        {
            foreach (WeaponDamageType type in System.Enum.GetValues(typeof(WeaponDamageType)))
            {
                float flat = runtime.damageTypeFlatPercent.TryGetValue(type, out float flatValue)
                    ? flatValue
                    : 0f;
                float perWeapon = runtime.damageTypePerWeaponPercent.TryGetValue(type, out float perWeaponValue)
                    ? perWeaponValue
                    : 0f;
                float total = flat + towerAttack.GetTotalWeaponsOfType(type) * perWeapon;

                snapshot.SetStat(BuildSnapshot.GetDamageTypeStat(type), total);
            }
        }

        if (enemyStats != null)
        {
            snapshot.SetStat(BuildStat.EnemyHealth, enemyStats.CurrentEnemyHealth);
            snapshot.SetStat(BuildStat.EnemyDamage, enemyStats.CurrentEnemyDamage);
        }

        return snapshot;
    }
}
