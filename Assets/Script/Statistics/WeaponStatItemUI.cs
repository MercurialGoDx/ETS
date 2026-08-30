using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// Строка окна статистики урона: иконка оружия + название + суммарный нанесённый урон.
/// Стилистика повторяет запись журнала (<see cref="JournalEntry"/>): плашка-подложка, иконка слева,
/// название по центру-слева, значение справа. При наведении показывает описание оружия через
/// общий <see cref="WeaponTooltip"/> (как в журнале/магазине).
/// </summary>
public class WeaponStatItemUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text damageText;

    private WeaponDefinition currentWeapon;
    private UpgradeBaseSO currentUpgrade;

    public void Setup(WeaponDefinition weapon, float damage)
    {
        currentWeapon = weapon;
        currentUpgrade = null;

        SetupContent(
            weapon != null ? weapon.icon : null,
            weapon != null ? weapon.GetLocalizedName() : string.Empty,
            damage);
    }

    public void Setup(DamageStatEntry stat)
    {
        currentWeapon = stat.Weapon;
        currentUpgrade = stat.Upgrade;
        SetupContent(stat.Icon, stat.GetLocalizedName(), stat.Damage);
    }

    private void SetupContent(Sprite icon, string displayName, float damage)
    {

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = iconImage.sprite != null;
        }

        if (nameText != null)
            nameText.text = displayName;

        if (damageText != null)
            damageText.text = Mathf.RoundToInt(damage).ToString("N0", CultureInfo.InvariantCulture);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (WeaponTooltip.Instance == null)
            return;

        if (currentWeapon != null)
            WeaponTooltip.Instance.Show(currentWeapon);
        else if (currentUpgrade != null)
            WeaponTooltip.Instance.Show(
                currentUpgrade.GetLocalizedName(),
                currentUpgrade.GetLocalizedDescription());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (WeaponTooltip.Instance != null)
            WeaponTooltip.Instance.Hide();
    }
}
