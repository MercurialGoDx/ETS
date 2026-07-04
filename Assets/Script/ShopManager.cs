using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShopManager : MonoBehaviour
{
    [Header("Ссылки")]
    public TowerAttack tower;

    [Header("UI магазина")]
    [Tooltip("Панель магазина, которая включается/выключается по Q.")]
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

    [Header("Клавиша открытия магазина")]
    public KeyCode toggleShopKey = KeyCode.Q;

    // ===================== NEW: обратный таймер =====================
    [Header("Авто-реролл UI (текст)")]
    [Tooltip("Готовая строка таймера для вывода в TMP (например 00:20).")]
    public string AutoRerollTimerText { get; private set; } = "00:00";

    // осталось секунд до следующего авто-реролла
    private float autoRerollTimeLeft = 0f;
    // ===============================================================

    private void Start()
    {
        // Генерим содержимое магазина, но не показываем UI
        RandomizeShopContents();

        // Инициализируем текущую стоимость реролла базовой стоимостью
        currentRerollPrice = rerollPrice;

        // При старте панель и кнопка реролла скрыты
        if (shopPanel != null)
            shopPanel.SetActive(true);

        if (rerollButton != null)
            rerollButton.SetActive(true);

        // ===================== NEW: старт обратного таймера =====================
        ResetAutoRerollTimer();
        UpdateAutoRerollUI(); // чтобы текст/полоска сразу были корректны
        // =========================================================================
    }

    private void Update()
    {
        // Открытие / закрытие магазина по Q
        if (Input.GetKeyDown(toggleShopKey))
        {
            ToggleShopPanel();
        }

        // Реролл по R — только если магазин открыт
        if (enableRerollHotkey &&
            shopPanel != null &&
            shopPanel.activeSelf &&
            Input.GetKeyDown(rerollKey))
        {
            RerollShop();
        }

        // ===================== CHANGED: автоматический реролл (обратный отсчет) =====================
        HandleAutoReroll();
        // ============================================================================================
    }

    private void ToggleShopPanel()
    {
        if (shopPanel == null)
            return;

        bool newState = !shopPanel.activeSelf;
        shopPanel.SetActive(newState);

        if (rerollButton != null)
            rerollButton.SetActive(newState);

        if (!newState && WeaponTooltip.Instance != null)
        {
            WeaponTooltip.Instance.Hide();
        }
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

            var weapon = GetRandomWeaponWeighted();
            if (weapon != null)
            {
                slot.SetupWeapon(weapon, this);
            }
            else
            {
                slot.Clear();
            }
        }
    }

    private WeaponDefinition GetRandomWeaponWeighted()
    {
        if (availableWeapons == null || availableWeapons.Count == 0)
            return null;

        float totalWeight = 0f;
        for (int i = 0; i < availableWeapons.Count; i++)
        {
            var w = availableWeapons[i];
            if (w != null && UpgradesManager.Instance.RuntimeData.GetWeaponWeight(w) > 0f)
                totalWeight += UpgradesManager.Instance.RuntimeData.GetWeaponWeight(w);
        }

        if (totalWeight <= 0f)
            return null;

        float rnd = Random.value * totalWeight;
        float accum = 0f;

        for (int i = 0; i < availableWeapons.Count; i++)
        {
            var w = availableWeapons[i];
            if (w == null || UpgradesManager.Instance.RuntimeData.GetWeaponWeight(w) <= 0f)
                continue;

            accum += UpgradesManager.Instance.RuntimeData.GetWeaponWeight(w);
            if (rnd <= accum)
                return w;
        }

        for (int i = availableWeapons.Count - 1; i >= 0; i--)
        {
            if (availableWeapons[i] != null && availableWeapons[i].weight > 0f)
                return availableWeapons[i];
        }

        return null;
    }

    public void BuyWeapon(WeaponDefinition weapon, ShopSlot slot)
    {
        if (weapon == null || tower == null)
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

        if (PurchaseHistoryManager.Instance != null)
            PurchaseHistoryManager.Instance.AddWeapon(weapon);

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

            var upgrade = GetRandomUpgradeWeighted();
            if (upgrade != null)
            {
                slot.SetupUpgrade(upgrade, this);
            }
            else
            {
                slot.Clear();
            }
        }
    }

    private UpgradeBaseSO GetRandomUpgradeWeighted()
    {
        if (availableUpgrades == null || availableUpgrades.Count == 0)
            return null;

        float totalWeight = 0f;
        for (int i = 0; i < availableUpgrades.Count; i++)
        {
            var u = availableUpgrades[i];
            if (u != null && UpgradesManager.Instance.RuntimeData.GetUpgradeWeight(u) > 0f)
                totalWeight += UpgradesManager.Instance.RuntimeData.GetUpgradeWeight(u);
        }

        if (totalWeight <= 0f)
            return null;

        float rnd = Random.value * totalWeight;
        float accum = 0f;

        for (int i = 0; i < availableUpgrades.Count; i++)
        {
            var u = availableUpgrades[i];
            if (u == null || UpgradesManager.Instance.RuntimeData.GetUpgradeWeight(u) <= 0f)
                continue;

            accum += UpgradesManager.Instance.RuntimeData.GetUpgradeWeight(u);
            if (rnd <= accum)
                return u;
        }

        for (int i = availableUpgrades.Count - 1; i >= 0; i--)
        {
            if (availableUpgrades[i] != null && availableUpgrades[i].weight > 0f)
                return availableUpgrades[i];
        }

        return null;
    }

    public void BuyUpgrade(UpgradeBaseSO upgrade, ShopSlot slot)
    {
        if (upgrade == null)
            return;

        if (GoldManager.Instance != null)
        {
            int price = upgrade.price;

            if (!GoldManager.Instance.HasEnoughGold(price))
                return;

            if (!GoldManager.Instance.SpendGold(price))
                return;
        }

        if (UpgradesManager.Instance != null)
            UpgradesManager.Instance.ApplyUpgrade(upgrade);

        if (PurchaseHistoryManager.Instance != null)
            PurchaseHistoryManager.Instance.AddUpgrade(upgrade);

        Debug.Log($"weight Upgrade: {UpgradesManager.Instance.RuntimeData.GetUpgradeWeight(upgrade)}");

        if (slot != null)
            slot.Clear();
    }

    // ======================== РЕРОЛЛ / РАНДОМИЗАЦИЯ ========================

    private void RandomizeShopContents()
    {
        SetupWeaponSlotsRandom();
        SetupUpgradeSlotsRandom();
    }

    public void RerollShop()
    {
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
