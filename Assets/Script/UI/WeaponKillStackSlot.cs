using TMPro;
using UnityEngine;

/// <summary>
/// Одна ячейка HUD-панели килл-стаков: иконка оружия сверху, число стаков под ней.
/// Тултип по наведению (имя/описание оружия) достаётся от ItemSlotBase бесплатно.
/// </summary>
public class WeaponKillStackSlot : ItemSlotBase
{
    [SerializeField] private TMP_Text stacksText;

    public void SetValue(WeaponDefinition weapon, int stacks)
    {
        currentWeapon = weapon;
        currentUpgrade = null;

        SetIcon(weapon != null ? weapon.icon : null);

        if (stacksText != null)
            stacksText.text = stacks.ToString();
    }

    /// <summary>Индикатор заряженного дубликатора — иконка апгрейда, без числа стаков.</summary>
    public void SetDuplicator(UpgradeBaseSO duplicatorUpgrade)
    {
        currentWeapon = null;
        currentUpgrade = duplicatorUpgrade;

        SetIcon(duplicatorUpgrade != null ? duplicatorUpgrade.icon : null);

        if (stacksText != null)
            stacksText.text = string.Empty;
    }
}
