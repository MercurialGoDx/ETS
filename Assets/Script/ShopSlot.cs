using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class ShopSlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI")]
    [SerializeField] private Image iconImage;    
    [SerializeField] private TMP_Text priceText; 

    [Header("Данные")]
    private WeaponDefinition currentWeapon;
    private UpgradeDefinition currentUpgrade;
    private ShopManager shopManager;

    // ==== ОРУЖИЕ ====
    public void SetupWeapon(WeaponDefinition weapon, ShopManager manager)
    {
        currentWeapon = weapon;
        currentUpgrade = null;
        shopManager = manager;

        if (iconImage != null)
        {
            if (weapon != null && weapon.icon != null)
            {
                iconImage.sprite = weapon.icon;
                iconImage.enabled = true;
            }
            else
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }
        }

        if (priceText != null)
            priceText.text = weapon != null ? weapon.price.ToString() : "";
    }

    // ==== УЛУЧШЕНИЕ ====
    public void SetupUpgrade(UpgradeDefinition upgrade, ShopManager manager)
    {
        currentWeapon = null;
        currentUpgrade = upgrade;
        shopManager = manager;

        if (iconImage != null)
        {
            if (upgrade != null && upgrade.icon != null)
            {
                iconImage.sprite = upgrade.icon;
                iconImage.enabled = true;
            }
            else
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }
        }

        if (priceText != null)
            priceText.text = upgrade != null ? upgrade.price.ToString() : "";
    }

    // ==== ОЧИСТКА ====
    public void Clear()
    {
        currentWeapon = null;
        currentUpgrade = null;
        shopManager = null;

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        if (priceText != null)
            priceText.text = "";

        // на всякий — прячем тултип, если вдруг висел
        if (WeaponTooltip.Instance != null)
            WeaponTooltip.Instance.Hide();
    }

    // ==== КЛИК ====
    public void OnPointerClick(PointerEventData eventData)
    {
        if (shopManager == null) return;

        if (currentWeapon != null)
        {
            shopManager.BuyWeapon(currentWeapon, this);
        }
        else if (currentUpgrade != null)
        {
            shopManager.BuyUpgrade(currentUpgrade, this);
        }
    }

    // ==== НАВЕДЕНИЕ ДЛЯ ТУЛТИПА ====
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (WeaponTooltip.Instance == null)
            return;

        if (currentWeapon != null)
        {
            WeaponTooltip.Instance.Show(currentWeapon, eventData.position);
        }
        else if (currentUpgrade != null)
        {
            // тут подставь реальные поля из UpgradeDefinition
            WeaponTooltip.Instance.Show(
                currentUpgrade.upgradeName, 
                currentUpgrade.description, 
                eventData.position
            );
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (WeaponTooltip.Instance != null)
            WeaponTooltip.Instance.Hide();
    }
}
