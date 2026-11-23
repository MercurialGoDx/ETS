using System.Collections.Generic;
using ETS.Multiplayer;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Управляет ассортиментом, покупками и текущими ценами магазина.</summary>
public class ShopManager : MonoBehaviour
{
    [Header("Ссылки")]
    public TowerAttack tower;

    [Header("UI магазина")]
    [Tooltip("Панель магазина. Показывается на весь забег.")]
    public GameObject shopPanel;

    [Tooltip("Кнопка реролла, которая показывается вместе с магазином.")]
    public GameObject rerollButton;

    [Tooltip("Прогресс-бар автоматического реролла (Image с типом Filled).")]
    public Image autoRerollProgressBar;

    [Header("Слоты магазина")]
    [Tooltip("Слоты под оружие (ShopSlot1–ShopSlot5).")]
    public List<ShopSlot> weaponSlots;

    [Tooltip("Слоты под улучшения (ShopSlot6–ShopSlot10).")]
    public List<ShopSlot> upgradeSlots;

    [Header("Доступные товары (пул для рандома)")]
    public List<WeaponDefinition> availableWeapons;
    public List<UpgradeBaseSO> availableUpgrades;

    // Номер розыгрыша магазина. Нужен только дуэли: предложение адресуется парой
    // (номер ролла, номер слота), поэтому лишний реролл у одного игрока не сдвигает другого.
    private int shopRollIndex;

    [Header("Реролл")]
    [Tooltip("Базовая стоимость реролла магазина (золото).")]
    public int rerollPrice = 20;

    [Tooltip("На сколько увеличивается стоимость реролла после каждого использования.")]
    public int rerollPriceIncrease = 5;

    [Tooltip("Разрешить реролл по горячей клавише.")]
    public bool enableRerollHotkey = true;

    [Tooltip("Клавиша для реролла магазина.")]
    public KeyCode rerollKey = KeyCode.R;

    [Header("Авто-реролл UI (текст)")]
    [SerializeField] private TMP_Text autoRerollTimerText;

    private int currentRerollPrice;

    /// <summary>
    /// Текущая стоимость реролла (увеличивается при каждом использовании).
    /// </summary>
    public int CurrentRerollPrice => currentRerollPrice;

    [Header("Автоматический реролл")]
    [Tooltip("Интервал автоматического реролла в секундах (0 = отключено).")]
    public float autoRerollInterval = 20f;

    [Header("Клавиша покупки апгрейда разблокировки")]
    [Tooltip("Покупает следующий апгрейд из ShopUnlockConfig: слоты + тир.")]
    public KeyCode buyUnlockUpgradeKey = KeyCode.Q;

    // ===================== NEW: обратный таймер =====================
    [Header("Авто-реролл UI (текст)")]
    [Tooltip("Готовая строка таймера для вывода в TMP (например 00:20).")]
    public string AutoRerollTimerText { get; private set; } = "00:00";

    // осталось секунд до следующего авто-реролла
    private float autoRerollTimeLeft = 0f;
    // ===============================================================

    private void Start()
    {
        // Баланс из таблицы (если импортирован) перекрывает инспектор.
        var cfg = BalanceService.Config;
        if (cfg != null)
        {
            rerollPrice = cfg.shop.rerollBaseCost;
            rerollPriceIncrease = cfg.shop.rerollCostIncrease;
            autoRerollInterval = cfg.shop.autoRerollInterval;
        }

        // Генерим содержимое магазина, но не показываем UI
        RandomizeShopContents();

        // Инициализируем текущую стоимость реролла базовой стоимостью
        currentRerollPrice = rerollPrice;

        // Магазин видно всё время забега — скрывать его больше нечем
        if (shopPanel != null)
            shopPanel.SetActive(true);

        if (rerollButton != null)
            rerollButton.SetActive(true);

        // ===================== NEW: старт обратного таймера =====================
        ResetAutoRerollTimer();
        UpdateAutoRerollUI(); // чтобы текст/полоска сразу были корректны
        // =========================================================================
    }

    private void OnEnable()
    {
        if (ShopUnlockService.Instance != null)
            ShopUnlockService.Instance.OnUnlocksChanged += HandleUnlocksChanged;
    }

    private void OnDisable()
    {
        if (ShopUnlockService.Instance != null)
            ShopUnlockService.Instance.OnUnlocksChanged -= HandleUnlocksChanged;
    }

    /// <summary>
    /// Купили апгрейд — заполняем только те слоты, которые до этого были закрыты.
    /// Уже показанные предметы не трогаем: по ТЗ разблокировка не действует задним числом.
    /// </summary>
    private void HandleUnlocksChanged()
    {
        FillNewlyUnlockedSlots(weaponSlots, true);
        FillNewlyUnlockedSlots(upgradeSlots, false);
    }

    private void FillNewlyUnlockedSlots(List<ShopSlot> slots, bool weapons)
    {
        if (slots == null)
            return;

        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot == null || !slot.IsLocked || !IsColumnUnlocked(i))
                continue;

            if (weapons)
            {
                var weapon = GetRandomWeaponWeighted(i);
                if (weapon == null) slot.Clear();
                else slot.SetupWeapon(weapon, this);
            }
            else
            {
                var upgrade = GetRandomUpgradeWeighted(i);
                if (upgrade == null) slot.Clear();
                else slot.SetupUpgrade(upgrade, this);
            }
        }
    }

    /// <summary>
    /// Колонка открыта? Без сервиса считаем всё открытым — магазин работает как до фичи.
    /// </summary>
    private static bool IsColumnUnlocked(int column)
    {
        return ShopUnlockService.Instance == null
            || ShopUnlockService.Instance.IsColumnUnlocked(column);
    }

    /// <summary>
    /// Тир разблокирован? Без сервиса доступны все — магазин работает как до фичи.
    /// </summary>
    private static bool IsTierUnlocked(ItemTier tier)
    {
        return ShopUnlockService.Instance == null
            || ShopUnlockService.Instance.IsTierUnlocked(tier);
    }

    /// <summary>
    /// Вес предмета для рулетки магазина. Заблокированный тир даёт 0 — предмет не участвует
    /// ни в сумме весов, ни в розыгрыше, ни в запасном переборе.
    ///
    /// В дуэли берётся базовый вес из ассета, без надбавки за прошлые покупки: надбавка —
    /// состояние конкретного игрока, и из-за неё n-е предложения у двоих разъехались бы уже
    /// после первой разной покупки. Фильтр по тиру, наоборот, остаётся: пока разблокировки
    /// одинаковы, одинаков и пул, а кто открыл тир раньше — тот раньше и увидит его предметы.
    /// </summary>
    private static float GetOfferWeight(WeaponDefinition weapon)
    {
        if (weapon == null || !IsTierUnlocked(weapon.itemTier))
            return 0f;

        return DuelSession.IsSeeded
            ? weapon.weight
            : UpgradesManager.Instance.RuntimeData.GetWeaponWeight(weapon);
    }

    private static float GetOfferWeight(UpgradeBaseSO upgrade)
    {
        if (upgrade == null || !IsTierUnlocked(upgrade.itemTier))
            return 0f;

        return DuelSession.IsSeeded
            ? upgrade.weight
            : UpgradesManager.Instance.RuntimeData.GetUpgradeWeight(upgrade);
    }

    private void Update()
    {
        // Покупка апгрейда разблокировки по Q
        if (Input.GetKeyDown(buyUnlockUpgradeKey))
        {
            BuyNextUnlockUpgrade();
        }

        // Реролл по R
        if (enableRerollHotkey && Input.GetKeyDown(rerollKey))
        {
            RerollShop();
        }

        // ===================== CHANGED: автоматический реролл (обратный отсчет) =====================
        HandleAutoReroll();
        // ============================================================================================
    }

    /// <summary>
    /// Покупает первый некупленный апгрейд разблокировки. Сервис сам проверит требование,
    /// хватает ли золота и не куплен ли апгрейд уже — при отказе ничего не списывается.
    /// </summary>
    public void BuyNextUnlockUpgrade()
    {
        // Во время выбора награды с босса магазин заблокирован — то же правило, что у покупок.
        if (BossRewardUI.IsSelectionOpen)
            return;

        if (GameStateManager.Instance.CurrentState != GameState.Preparing &&
            GameStateManager.Instance.CurrentState != GameState.Playing)
            return;

        var service = ShopUnlockService.Instance;
        if (service == null)
            return;

        var next = service.GetNextUpgrade();
        if (next == null)
        {
            Debug.Log("[Shop] Все апгрейды разблокировки уже куплены");
            return;
        }

        var state = service.GetState(next);
        if (!service.TryPurchase(next))
        {
            Debug.Log($"[Shop] Апгрейд {next.name} не куплен: {state}");
            return;
        }

        Debug.Log($"[Shop] Куплен {next.name} за {next.price}");
    }

    // ======================== ОРУЖИЕ ========================

    private void SetupWeaponSlotsRandom()
    {
        if (weaponSlots == null || availableWeapons == null || availableWeapons.Count == 0)
            return;

        for (int i = 0; i < weaponSlots.Count; i++)
        {
            var slot = weaponSlots[i];
            if (slot == null)
                continue;

            if (!IsColumnUnlocked(i))
            {
                slot.SetLocked();
                continue;
            }

            var weapon = GetRandomWeaponWeighted(i);
            if (weapon == null)
            {
                slot.Clear();
            }
            else
            {
                slot.SetupWeapon(weapon, this);
            }
        }
    }

    /// <summary>
    /// Бросок для розыгрыша оффера. В дуэли число берётся из сида матча и адресуется парой
    /// (номер розыгрыша, номер слота) — лишний реролл у одного игрока не сдвигает другого.
    /// Слоты заполняются обычным путём, поэтому магазин выглядит и работает как всегда;
    /// от состояния игрока предложение зависит только через разблокировки (см. GetOfferWeight),
    /// так что при одинаковых разблокировках n-е предложение у обоих совпадает.
    /// </summary>
    private float OfferRoll(DuelStream stream, int slotIndex)
    {
        return DuelSession.IsSeeded
            ? DuelRandom.Value01(DuelSession.Seed, stream, DuelRandom.Compose(shopRollIndex, slotIndex))
            : Random.value;
    }

    private WeaponDefinition GetRandomWeaponWeighted(int slotIndex)
    {
        if (availableWeapons == null || availableWeapons.Count == 0)
            return null;

        float totalWeight = 0f;
        for (int i = 0; i < availableWeapons.Count; i++)
            totalWeight += GetOfferWeight(availableWeapons[i]);

        if (totalWeight <= 0f)
            return null;

        float rnd = OfferRoll(DuelStream.ShopWeapon, slotIndex) * totalWeight;
        float accum = 0f;

        for (int i = 0; i < availableWeapons.Count; i++)
        {
            float weight = GetOfferWeight(availableWeapons[i]);
            if (weight <= 0f)
                continue;

            accum += weight;
            if (rnd <= accum)
                return availableWeapons[i];
        }

        for (int i = availableWeapons.Count - 1; i >= 0; i--)
        {
            if (GetOfferWeight(availableWeapons[i]) > 0f)
                return availableWeapons[i];
        }

        return null;
    }

    public void BuyWeapon(WeaponDefinition weapon, ShopSlot slot)
    {
        if (weapon == null || tower == null)
            return;

        // Во время выбора награды с босса магазин заблокирован.
        if (BossRewardUI.IsSelectionOpen)
            return;

        if (GoldManager.Instance != null)
        {
            int price = weapon.price;
            if (!GoldManager.Instance.HasEnoughGold(price))
                return;

            if (!GoldManager.Instance.SpendGold(price))
                return;
        }

        UpgradesManager.Instance.RegisterWeaponPurchase(weapon);

        tower.AddWeapon(weapon);
        DuplicateWeaponIfArmed(weapon);

        if (PurchaseHistoryManager.Instance != null)
            PurchaseHistoryManager.Instance.AddWeapon(weapon);

        if (AchievementManager.Instance != null)
            AchievementManager.Instance.NotifyWeaponPurchased(weapon);

        Debug.Log($"weight Weapon: {UpgradesManager.Instance.RuntimeData.GetWeaponWeight(weapon)}");

        if (slot != null)
            slot.Clear();
    }

    // ======================== УЛУЧШЕНИЯ ========================

    private void SetupUpgradeSlotsRandom()
    {
        if (upgradeSlots == null || availableUpgrades == null || availableUpgrades.Count == 0)
            return;

        for (int i = 0; i < upgradeSlots.Count; i++)
        {
            var slot = upgradeSlots[i];
            if (slot == null)
                continue;

            if (!IsColumnUnlocked(i))
            {
                slot.SetLocked();
                continue;
            }

            var upgrade = GetRandomUpgradeWeighted(i);
            if (upgrade == null)
            {
                slot.Clear();
            }
            else
            {
                slot.SetupUpgrade(upgrade, this);
            }
        }
    }

    private UpgradeBaseSO GetRandomUpgradeWeighted(int slotIndex)
    {
        if (availableUpgrades == null || availableUpgrades.Count == 0)
            return null;

        float totalWeight = 0f;
        for (int i = 0; i < availableUpgrades.Count; i++)
            totalWeight += GetOfferWeight(availableUpgrades[i]);

        if (totalWeight <= 0f)
            return null;

        float rnd = OfferRoll(DuelStream.ShopUpgrade, slotIndex) * totalWeight;
        float accum = 0f;

        for (int i = 0; i < availableUpgrades.Count; i++)
        {
            float weight = GetOfferWeight(availableUpgrades[i]);
            if (weight <= 0f)
                continue;

            accum += weight;
            if (rnd <= accum)
                return availableUpgrades[i];
        }

        for (int i = availableUpgrades.Count - 1; i >= 0; i--)
        {
            if (GetOfferWeight(availableUpgrades[i]) > 0f)
                return availableUpgrades[i];
        }

        return null;
    }

    /// <summary>Текущая цена следующей покупки улучшения.</summary>
    public int GetUpgradePrice(UpgradeBaseSO upgrade)
    {
        if (upgrade == null)
            return 0;

        UpgradeContextSO context = UpgradesManager.Instance != null
            ? UpgradesManager.Instance.context
            : null;
        return upgrade.GetCurrentPrice(context);
    }

    public void BuyUpgrade(UpgradeBaseSO upgrade, ShopSlot slot)
    {
        if (upgrade == null)
            return;

        // Во время выбора награды с босса магазин заблокирован.
        if (BossRewardUI.IsSelectionOpen)
            return;

        // Нельзя купить улучшение, которое сейчас нельзя применить
        // (например, недостаточно здоровья для "золото за жизнь").
        if (UpgradesManager.Instance != null && !UpgradesManager.Instance.CanApplyUpgrade(upgrade))
            return;

        if (GoldManager.Instance != null)
        {
            int price = GetUpgradePrice(upgrade);

            if (!GoldManager.Instance.HasEnoughGold(price))
                return;

            if (!GoldManager.Instance.SpendGold(price))
                return;
        }

        if (UpgradesManager.Instance != null)
            UpgradesManager.Instance.ApplyUpgrade(upgrade);

        if (upgrade is not DuplicatorUpgrade)
            DuplicateUpgradeIfArmed(upgrade);

        if (PurchaseHistoryManager.Instance != null)
            PurchaseHistoryManager.Instance.AddUpgrade(upgrade);

        if (AchievementManager.Instance != null)
            AchievementManager.Instance.NotifyUpgradePurchased(upgrade);

        RefreshUpgradePrices();

        Debug.Log($"weight Upgrade: {UpgradesManager.Instance.RuntimeData.GetUpgradeWeight(upgrade)}");

        if (slot != null)
            slot.Clear();
    }

    private void RefreshUpgradePrices()
    {
        if (upgradeSlots == null)
            return;

        foreach (ShopSlot upgradeSlot in upgradeSlots)
            upgradeSlot?.RefreshPrice();
    }

    private void DuplicateWeaponIfArmed(WeaponDefinition weapon)
    {
        UpgradesManager upgradesManager = UpgradesManager.Instance;
        UpgradesRuntimeData runtime = upgradesManager?.GameplayRuntimeData;
        if (runtime == null || !runtime.TryConsumeDuplicator(weapon.itemTier, out int copies))
            return;

        for (int i = 0; i < copies; i++)
        {
            upgradesManager.RegisterWeaponPurchase(weapon);
            tower.AddWeapon(weapon);
        }

        Debug.Log($"[Duplicator] Granted {copies} free weapon copies: {weapon.name}.");
    }

    private void DuplicateUpgradeIfArmed(UpgradeBaseSO upgrade)
    {
        UpgradesManager upgradesManager = UpgradesManager.Instance;
        UpgradesRuntimeData runtime = upgradesManager?.GameplayRuntimeData;
        if (runtime == null || !runtime.TryConsumeDuplicator(upgrade.itemTier, out int copies))
            return;

        int appliedCopies = 0;
        for (int i = 0; i < copies; i++)
        {
            if (!upgradesManager.CanApplyUpgrade(upgrade))
                break;

            upgradesManager.ApplyUpgrade(upgrade);
            appliedCopies++;
        }

        Debug.Log($"[Duplicator] Granted {appliedCopies}/{copies} free upgrade copies: {upgrade.name}.");
    }

    // ======================== РЕРОЛЛ / РАНДОМИЗАЦИЯ ========================

    private void RandomizeShopContents()
    {
        SetupWeaponSlotsRandom();
        SetupUpgradeSlotsRandom();
        shopRollIndex++;
    }

    public void RerollShop()
    {
        // Во время выбора награды с босса реролл заблокирован.
        if (BossRewardUI.IsSelectionOpen)
            return;

        if (GoldManager.Instance != null && currentRerollPrice > 0)
        {
            if (!GoldManager.Instance.HasEnoughGold(currentRerollPrice))
                return;

            if (!GoldManager.Instance.SpendGold(currentRerollPrice))
                return;
        }

        RandomizeShopContents();

        // Увеличиваем стоимость реролла после успешного использования
        currentRerollPrice += rerollPriceIncrease;

        // Обновляем тултип кнопки реролла с новой стоимостью
        RerollButtonTooltip rerollTooltip = FindObjectOfType<RerollButtonTooltip>();
        if (rerollTooltip != null)
        {
            rerollTooltip.UpdateTooltip();
        }

        // ВАЖНО: НЕ трогаем авто-таймер — он живет своей жизнью (как ты и хотела)
    }

    /// <summary>
    /// Автоматический реролл магазина без траты золота.
    /// Вызывается по таймеру каждые autoRerollInterval секунд.
    /// </summary>
    private void AutoRerollShop()
    {
        RandomizeShopContents();
    }

    // ===================== NEW: вся новая логика только про авто-таймер/UI =====================

    private void HandleAutoReroll()
    {
        if (autoRerollInterval <= 0f)
        {
            // Отключено
            if (autoRerollProgressBar != null)
                autoRerollProgressBar.fillAmount = 0f;

            AutoRerollTimerText = "00:00";
            return;
        }

        autoRerollTimeLeft -= Time.deltaTime;
        if (autoRerollTimeLeft < 0f)
            autoRerollTimeLeft = 0f;

        UpdateAutoRerollUI();

        if (autoRerollTimeLeft <= 0f)
        {
            AutoRerollShop();
            ResetAutoRerollTimer();
            UpdateAutoRerollUI(); // чтобы сразу стало "00:20" и fill=1
        }
    }

    private void ResetAutoRerollTimer()
    {
        autoRerollTimeLeft = autoRerollInterval;
    }

    private void UpdateAutoRerollUI()
    {
        // Полоска: 1 -> 0
        if (autoRerollProgressBar != null)
        {
            float t = autoRerollInterval <= 0f ? 0f : (autoRerollTimeLeft / autoRerollInterval);
            autoRerollProgressBar.fillAmount = Mathf.Clamp01(t);
        }

        // Строка таймера
        int seconds = Mathf.CeilToInt(autoRerollTimeLeft);
        AutoRerollTimerText = FormatMMSS(seconds);

        // Прямой вывод в TMP из инспектора
        if (autoRerollTimerText != null)
            autoRerollTimerText.text = AutoRerollTimerText;
    }

    private static string FormatMMSS(int totalSeconds)
    {
        if (totalSeconds < 0) totalSeconds = 0;
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }

    // ==========================================================================================
}
