using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

/// <summary>
/// Панель инвентаря (Tab): всё купленное за забег — оружие со стеками, улучшения
/// со счётчиком покупок и колонка сводных характеристик. Только чтение.
/// Время НЕ останавливает: как и магазин, панель живёт поверх идущей игры,
/// иначе появляется стратегия «висеть в инвентаре, пока думаешь».
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

    [Header("Заголовки секций")]
    [Tooltip("«Оружие · 7 из 8». Необязательно.")]
    [SerializeField] private TMP_Text weaponsHeader;
    [Tooltip("«Улучшения · 11». Необязательно.")]
    [SerializeField] private TMP_Text upgradesHeader;
    [Tooltip("Источник общего числа доступного оружия для счётчика «N из M». Необязательно.")]
    [SerializeField] private ShopManager shopSource;

    [Header("Статы")]
    [Tooltip("Контейнер строк характеристик (VerticalLayoutGroup).")]
    [SerializeField] private Transform statsContent;
    [Tooltip("Префаб строки: подпись слева, значение справа.")]
    [SerializeField] private StatRow statRowPrefab;
    [Tooltip("Префаб заголовка секции. Пусто — возьмётся обычная строка.")]
    [SerializeField] private StatRow statHeaderPrefab;
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

    private readonly List<GameObject> spawnedSlots = new List<GameObject>();

    // Строки статов переиспользуются между обновлениями: панель перерисовывается
    // 4 раза в секунду, Instantiate/Destroy на каждом тике был бы мусором для GC.
    private readonly List<StatRow> statRowPool = new List<StatRow>();
    private int usedRows;

    // Разделитель тысяч — неразрывный пробел: выглядит как пробел, но не даёт
    // разорвать число, даже если где-то включится перенос.
    private static readonly NumberFormatInfo NumFormat = new NumberFormatInfo
    {
        NumberGroupSeparator = " ",
        NumberDecimalSeparator = ".",
    };

    private Coroutine statsRoutine;
    private WaitForSecondsRealtime statsWait;

    private bool isOpen;

    // Русские подписи типов урона в порядке enum WeaponDamageType
    // (Magic, Piercing, Normal, Projectile, Heavy, Chaos).
    private static readonly string[] DamageTypeLabels =
    {
        "Магия", "Колющий", "Обычный", "Снаряды", "Тяжёлый", "Хаос"
    };

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

    private void TryOpen()
    {
        // Вне забега (меню, пауза, game over) инвентарь не открывается.
        if (GameStateManager.Instance != null)
        {
            var state = GameStateManager.Instance.CurrentState;
            if (state != GameState.Playing && state != GameState.Preparing)
                return;
        }

        isOpen = true;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        // История покупок стоит слева по центру и налезала бы на рамку — прячем, как журнал.
        if (PurchaseHistoryManager.Instance != null)
            PurchaseHistoryManager.Instance.SetVisible(false);

        if (pauseManager != null)
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

        if (PurchaseHistoryManager.Instance != null)
            PurchaseHistoryManager.Instance.SetVisible(true);

        // Паузу возвращаем через кадр: если PauseManager.Update успеет отработать
        // после нашего в этом же кадре, он увидит тот же Escape и откроет меню паузы.
        StartCoroutine(EnablePauseNextFrame());
    }

    private IEnumerator EnablePauseNextFrame()
    {
        yield return null;
        if (pauseManager != null)
            pauseManager.SetCanPause(true);
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

        int weaponKinds = 0;
        if (towerAttack != null && weaponsContent != null && weaponSlotPrefab != null)
        {
            foreach (var owned in towerAttack.GetOwnedWeapons())
            {
                InventorySlot slot = Instantiate(weaponSlotPrefab, weaponsContent);
                slot.SetupWeapon(owned.Def, owned.Stacks);
                ApplyTierLook(slot, owned.Def.itemTier);
                spawnedSlots.Add(slot.gameObject);
                weaponKinds++;
            }
        }

        // Счётчики покупок улучшений живут в UpgradesManager.RuntimeData — это НЕ тот же
        // экземпляр, что context.runtime со статами (см. RefreshStats).
        int upgradeTotal = 0;
        if (UpgradesManager.Instance != null && upgradesContent != null && upgradeSlotPrefab != null)
        {
            foreach (var pair in UpgradesManager.Instance.RuntimeData.UpgradePurchaseCounts)
            {
                if (pair.Key == null) continue;

                InventorySlot slot = Instantiate(upgradeSlotPrefab, upgradesContent);
                slot.SetupUpgrade(pair.Key, pair.Value);
                ApplyTierLook(slot, pair.Key.itemTier);
                spawnedSlots.Add(slot.gameObject);
                upgradeTotal += pair.Value;
            }
        }

        if (weaponsHeader != null)
        {
            int available = shopSource != null && shopSource.availableWeapons != null
                ? shopSource.availableWeapons.Count
                : 0;
            weaponsHeader.text = available > 0
                ? $"Оружие · {weaponKinds} из {available}"
                : $"Оружие · {weaponKinds}";
        }

        if (upgradesHeader != null)
            upgradesHeader.text = $"Улучшения · {upgradeTotal}";
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

        if (playerHealth != null)
        {
            Row("Здоровье", Pair(playerHealth.CurrentHealth, playerHealth.MaxHealth));
            float regen = playerHealth.GetTotalRegen();
            Row("Реген", regen > Eps ? Num(regen) + "/с" : "—");
            Row("Шипы", Num(playerHealth.SpikesDamage));
        }

        if (playerShield != null)
            Row("Щит", Pair(playerShield.CurrentShield, playerShield.MaxShield));

        if (towerAttack != null)
            Row("Скорость атаки", Percent(towerAttack.TotalFireRateMultiplier - 1f));

        // ВАЖНО: статы пишутся апгрейдами в context.runtime (Apply(context)), а НЕ в
        // UpgradesManager.RuntimeData — GameInstaller создаёт под контекст отдельный
        // экземпляр. Читаем тот же, что и DamageCalculator, иначе всё показывает прочерки.
        var runtime = UpgradesManager.Instance != null && UpgradesManager.Instance.context != null
            ? UpgradesManager.Instance.context.runtime
            : null;

        if (runtime != null)
        {
            // Доли: 0.45 = +45%, как их читает DamageCalculator.
            Row("Общий урон", Percent(runtime.damagePercent));
            Row("Урон со временем", Percent(runtime.totalGeneratorDamagePercent));

            float maxHpBonus = playerHealth != null
                ? (playerHealth.MaxHealth / 100f) * runtime.damagePerValueHpPercent
                : 0f;
            Row("Урон от макс. HP", Percent(maxHpBonus));
            Row("Урон при щите", Percent(runtime.adaptiveDamageWhileShieldPercent));
        }

        if (GoldManager.Instance != null)
        {
            Row("Золото", Percent(GoldManager.Instance.GoldGainBonus));
            Row("Доход", Int(GoldManager.Instance.goldPerTick) + "/с");
        }

        if (runtime != null && towerAttack != null)
        {
            Header("Бонус по типам");

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

                Row(DamageTypeLabels[(int)type], Percent(total));
            }
        }

        bool hasLayerMultipliers =
            playerHealth != null &&
            (!Mathf.Approximately(playerHealth.MaxHealthGlobalMultiplier, 1f) ||
             !Mathf.Approximately(playerHealth.HealthRegenGlobalMultiplier, 1f) ||
             !Mathf.Approximately(playerHealth.SpikesGlobalMultiplier, 1f));
        hasLayerMultipliers |= playerShield != null &&
            !Mathf.Approximately(playerShield.ShieldGlobalMultiplier, 1f);
        hasLayerMultipliers |= towerAttack != null &&
            !Mathf.Approximately(towerAttack.GlobalFireRateMultiplier, 1f);
        hasLayerMultipliers |= runtime != null &&
            (!Mathf.Approximately(runtime.globalDamagePercent, 0f) ||
             !Mathf.Approximately(runtime.globalDamagePer100GoldPercent, 0f) ||
             !Mathf.Approximately(runtime.adaptiveDamageWhileShieldPercent, 0f));

        if (hasLayerMultipliers)
        {
            Header("Global / Adaptive");

            if (playerHealth != null)
            {
                Row("Здоровье", Multiplier(playerHealth.MaxHealthGlobalMultiplier));
                Row("Реген", Multiplier(playerHealth.HealthRegenGlobalMultiplier));
                Row("Шипы", Multiplier(playerHealth.SpikesGlobalMultiplier));
            }

            if (playerShield != null)
                Row("Щит", Multiplier(playerShield.ShieldGlobalMultiplier));

            if (towerAttack != null)
                Row("Скорость атаки", Multiplier(towerAttack.GlobalFireRateMultiplier));

            if (runtime != null)
            {
                int currentGold = GoldManager.Instance != null ? GoldManager.Instance.currentGold : 0;
                float goldBonus = (currentGold / 100f) * runtime.globalDamagePer100GoldPercent;
                float globalDamageMultiplier = 1f + runtime.globalDamagePercent + goldBonus;
                float adaptiveMultiplier = playerShield != null && playerShield.IsShieldActive
                    ? 1f + runtime.adaptiveDamageWhileShieldPercent
                    : 1f;

                Row("Урон: global", Multiplier(globalDamageMultiplier));
                Row("Мидас (текущее золото)", Percent(goldBonus));
                Row("Урон: adaptive", Multiplier(adaptiveMultiplier));
            }
        }

        // Лишние строки с прошлого обновления прячем, а не удаляем — переиспользуем.
        for (int i = usedRows; i < statRowPool.Count; i++)
            statRowPool[i].gameObject.SetActive(false);
    }

    /// <summary>Обычная строка: подпись слева, значение справа.</summary>
    private void Row(string label, string value)
    {
        NextRow(statRowPrefab).Set(label, value);
    }

    /// <summary>Заголовок секции — отдельным префабом, если он задан.</summary>
    private void Header(string label)
    {
        NextRow(statHeaderPrefab != null ? statHeaderPrefab : statRowPrefab).SetHeader(label);
    }

    /// <summary>
    /// Берёт следующую строку из пула, создавая её при нехватке. Порядок в пуле совпадает
    /// с порядком вызовов, поэтому строки идут сверху вниз в том же порядке, что и код.
    /// </summary>
    private StatRow NextRow(StatRow prefab)
    {
        StatRow row;

        if (usedRows < statRowPool.Count)
        {
            row = statRowPool[usedRows];
        }
        else
        {
            row = Instantiate(prefab, statsContent);
            statRowPool.Add(row);
        }

        row.gameObject.SetActive(true);
        row.transform.SetSiblingIndex(usedRows);
        usedRows++;
        return row;
    }

    // ===================== ФОРМАТИРОВАНИЕ =====================

    private const float Eps = 0.0001f;

    /// <summary>Прочерк вместо +0%: видно, куда ещё можно вложиться.</summary>
    private static string Percent(float fraction)
    {
        return fraction > Eps ? "+" + Mathf.RoundToInt(fraction * 100f) + "%" : "—";
    }

    private static string Multiplier(float value)
    {
        return "x" + value.ToString("0.###", NumFormat);
    }

    /// <summary>Целое с разделителем тысяч: 22 190.</summary>
    private static string Int(float value)
    {
        return Mathf.RoundToInt(value).ToString("#,0", NumFormat);
    }

    /// <summary>Пара «текущее/максимум» без пробелов вокруг слэша. Нулевой максимум — прочерк.</summary>
    private static string Pair(float current, float max)
    {
        return max > Eps ? Int(current) + "/" + Int(max) : "—";
    }

    // Инвариантная культура: на русской локали ОС "0.#" дал бы запятую.
    private static string Num(float value)
    {
        return value > Eps ? value.ToString("0.#", CultureInfo.InvariantCulture) : "—";
    }
}
