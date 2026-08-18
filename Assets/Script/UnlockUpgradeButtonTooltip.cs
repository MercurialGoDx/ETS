using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Тултип для UnlockUpgradeButton — по образцу RerollButtonTooltip. Показывает название
/// и описание ТЕКУЩЕГО (следующего к покупке) апгрейда разблокировки по его
/// nameKey/descriptionKey, с ценой как запасной вариант на случай, если локализация
/// для этих апгрейдов ещё не заведена (та же логика фолбэка, что у ShopUnlockButtonUI.GetLabel).
/// </summary>
public class UnlockUpgradeButtonTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [TextArea(1, 2)]
    public string allDoneTitle = "Разблокировки";

    [TextArea(2, 4)]
    public string allDoneDescription = "Все апгрейды разблокировки уже куплены.";

    private bool isHovered;

    private void OnEnable()
    {
        if (ShopUnlockService.Instance != null)
            ShopUnlockService.Instance.OnUnlocksChanged += UpdateTooltip;
    }

    private void OnDisable()
    {
        if (ShopUnlockService.Instance != null)
            ShopUnlockService.Instance.OnUnlocksChanged -= UpdateTooltip;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        ShowTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;

        if (WeaponTooltip.Instance != null)
            WeaponTooltip.Instance.Hide();
    }

    /// <summary>Вызывается при смене разблокировок, пока курсор на кнопке — держит тултип актуальным.</summary>
    private void UpdateTooltip()
    {
        if (!isHovered) return;
        ShowTooltip();
    }

    private void ShowTooltip()
    {
        if (WeaponTooltip.Instance == null) return;

        var service = ShopUnlockService.Instance;
        var next = service != null ? service.GetNextUpgrade() : null;

        if (next == null)
        {
            WeaponTooltip.Instance.Show(allDoneTitle, allDoneDescription);
            return;
        }

        WeaponTooltip.Instance.Show(GetName(next), GetDescription(next));
    }

    private static string GetName(ShopUnlockUpgradeSO upgrade)
    {
        string localized = upgrade.GetLocalizedName();
        if (string.IsNullOrEmpty(localized) || localized == upgrade.nameKey)
            return upgrade.name;

        return localized;
    }

    private static string GetDescription(ShopUnlockUpgradeSO upgrade)
    {
        string localized = upgrade.GetLocalizedDescription();
        if (!string.IsNullOrEmpty(localized) && localized != upgrade.descriptionKey)
            return localized; // если перевод написан со смарт-плейсхолдером цены — она уже внутри

        return $"<color=#ffd700>Стоимость: {upgrade.price} золота</color>";
    }
}
