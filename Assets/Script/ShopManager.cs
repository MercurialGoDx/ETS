using System.Collections.Generic;
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
    public List<UpgradeDefinition> availableUpgrades;

    [Header("Реролл")]
    [Tooltip("Базовая стоимость реролла магазина (золото).")]
    public int rerollPrice = 20;

    [Tooltip("На сколько увеличивается стоимость реролла после каждого использования.")]
    public int rerollPriceIncrease = 5;

    [Tooltip("Разрешить реролл по горячей клавише.")]
    public bool enableRerollHotkey = true;

    [Tooltip("Клавиша для реролла магазина.")]
    public KeyCode rerollKey = KeyCode.R;

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

    private float autoRerollTimer = 0f;

    private void Start()
    {
        // Генерим содержимое магазина, но не показываем UI
        RandomizeShopContents();

        // Инициализируем текущую стоимость реролла базовой стоимостью
        currentRerollPrice = rerollPrice;

        // При старте панель и кнопка реролла скрыты
        if (shopPanel != null)
            shopPanel.SetActive(false);

        if (rerollButton != null)
            rerollButton.SetActive(false);
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

        // Автоматический реролл каждые N секунд
        if (autoRerollInterval > 0f)
        {
            autoRerollTimer += Time.deltaTime;
            
            // Обновляем прогресс-бар
            if (autoRerollProgressBar != null)
            {
                float progress = autoRerollTimer / autoRerollInterval;
                autoRerollProgressBar.fillAmount = Mathf.Clamp01(progress);
            }
            
            if (autoRerollTimer >= autoRerollInterval)
            {
                autoRerollTimer = 0f;
                AutoRerollShop();
            }
        }
        else
        {
            // Если автоматический реролл отключен, скрываем прогресс-бар
            if (autoRerollProgressBar != null)
            {
                autoRerollProgressBar.fillAmount = 0f;
            }
        }
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
            if (w != null && w.weight > 0f)
                totalWeight += w.weight;
        }

        if (totalWeight <= 0f)
            return null;

        float rnd = Random.value * totalWeight;
        float accum = 0f;

        for (int i = 0; i < availableWeapons.Count; i++)
        {
            var w = availableWeapons[i];
            if (w == null || w.weight <= 0f)
                continue;

            accum += w.weight;
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

        tower.AddWeapon(weapon);

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

    private UpgradeDefinition GetRandomUpgradeWeighted()
    {
        if (availableUpgrades == null || availableUpgrades.Count == 0)
            return null;

        float totalWeight = 0f;
        for (int i = 0; i < availableUpgrades.Count; i++)
        {
            var u = availableUpgrades[i];
            if (u != null && u.weight > 0f)
                totalWeight += u.weight;
        }

        if (totalWeight <= 0f)
            return null;

        float rnd = Random.value * totalWeight;
        float accum = 0f;

        for (int i = 0; i < availableUpgrades.Count; i++)
        {
            var u = availableUpgrades[i];
            if (u == null || u.weight <= 0f)
                continue;

            accum += u.weight;
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

    public void BuyUpgrade(UpgradeDefinition upgrade, ShopSlot slot)
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

        // ВАЖНО: НЕ трогаем autoRerollTimer и progressBar
        // Авто-реролл должен жить своей жизнью
    }

    /// <summary>
    /// Автоматический реролл магазина без траты золота.
    /// Вызывается по таймеру каждые autoRerollInterval секунд.
    /// </summary>
    private void AutoRerollShop()
    {
        RandomizeShopContents();
    }
}
