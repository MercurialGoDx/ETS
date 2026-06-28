using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// Запись журнала: иконка + название оружия/улучшения. При наведении показывает
/// полное описание через существующий <see cref="WeaponTooltip"/> (как слот магазина),
/// но без покупки.
/// </summary>
public class JournalEntry : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;

    private WeaponDefinition currentWeapon;
    private UpgradeBaseSO currentUpgrade;

    public void SetupWeapon(WeaponDefinition weapon)
    {
        currentWeapon = weapon;
        currentUpgrade = null;

        ApplyIcon(weapon != null ? weapon.icon : null);
        if (nameText != null)
            nameText.text = weapon != null ? weapon.GetLocalizedName() : "";
    }

    public void SetupUpgrade(UpgradeBaseSO upgrade)
    {
        currentWeapon = null;
        currentUpgrade = upgrade;

        ApplyIcon(upgrade != null ? upgrade.icon : null);
        if (nameText != null)
            nameText.text = upgrade != null ? upgrade.GetLocalizedName() : "";
    }

    private void ApplyIcon(Sprite sprite)
    {
        if (iconImage == null) return;

        iconImage.sprite = sprite;
        iconImage.enabled = sprite != null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (WeaponTooltip.Instance == null) return;

        if (currentWeapon != null)
            WeaponTooltip.Instance.Show(currentWeapon);
        else if (currentUpgrade != null)
            WeaponTooltip.Instance.Show(currentUpgrade.GetLocalizedName(), currentUpgrade.GetLocalizedDescription());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (WeaponTooltip.Instance != null)
            WeaponTooltip.Instance.Hide();
    }
}
