using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Одна кнопка покупки апгрейда разблокировки. Всегда показывает первый некупленный
/// апгрейд из ShopUnlockConfig, поэтому после покупки первого сама переключается на второй.
/// </summary>
public class ShopUnlockButtonUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button button;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text priceText;

    [Header("Тонировка по состоянию")]
    [SerializeField] private Color availableTint = Color.white;
    [SerializeField] private Color notEnoughGoldTint = new Color(1f, 0.45f, 0.45f, 1f);
    [SerializeField] private Color lockedTint = new Color(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private Color purchasedTint = new Color(0.5f, 0.75f, 0.5f, 1f);

    [Header("Подписи-заглушки")]
    [Tooltip("Пока у апгрейдов нет таблицы локализации — что писать вместо названия.")]
    [SerializeField] private string purchasedLabel = "КУПЛЕНО";
    [SerializeField] private string allDoneLabel = "—";

    // Последнее, что отрисовали: чтобы не дёргать UI каждый кадр без нужды.
    private ShopUnlockUpgradeSO shownUpgrade;
    private ShopUnlockState shownState;
    private bool hasDrawn;

    private void OnEnable()
    {
        if (ShopUnlockService.Instance != null)
            ShopUnlockService.Instance.OnUnlocksChanged += Refresh;

        if (button != null)
            button.onClick.AddListener(OnClicked);

        Refresh();
    }

    private void OnDisable()
    {
        if (ShopUnlockService.Instance != null)
            ShopUnlockService.Instance.OnUnlocksChanged -= Refresh;

        if (button != null)
            button.onClick.RemoveListener(OnClicked);
    }

    /// <summary>
    /// Состояние "не хватает золота" меняется само по себе, а события на трату золота
    /// в GoldManager нет — поэтому пересчитываем каждый кадр, но перерисовываем
    /// только когда состояние или апгрейд действительно сменились.
    /// </summary>
    private void Update()
    {
        Refresh();
    }

    private void OnClicked()
    {
        var service = ShopUnlockService.Instance;
        if (service == null)
            return;

        service.TryPurchase(service.GetNextUpgrade());
    }

    private void Refresh()
    {
        var service = ShopUnlockService.Instance;
        if (service == null)
            return;

        var next = service.GetNextUpgrade();
        var state = next != null ? service.GetState(next) : ShopUnlockState.Purchased;

        if (hasDrawn && next == shownUpgrade && state == shownState)
            return;

        shownUpgrade = next;
        shownState = state;
        hasDrawn = true;

        Draw(next, state);
    }

    private void Draw(ShopUnlockUpgradeSO upgrade, ShopUnlockState state)
    {
        bool allDone = upgrade == null;

        if (button != null)
            button.interactable = !allDone && state == ShopUnlockState.Available;

        if (icon != null)
        {
            if (!allDone && upgrade.icon != null)
            {
                icon.sprite = upgrade.icon;
                icon.enabled = true;
            }
            else
            {
                icon.enabled = false;
            }
        }

        if (priceText != null)
            priceText.text = allDone ? "" : upgrade.price.ToString();

        if (labelText != null)
            labelText.text = allDone ? allDoneLabel : GetLabel(upgrade, state);

        var tint = allDone ? purchasedTint : GetTint(state);
        var target = icon != null && icon.enabled ? icon : GetComponent<Image>();
        if (target != null)
            target.color = tint;
    }

    private string GetLabel(ShopUnlockUpgradeSO upgrade, ShopUnlockState state)
    {
        if (state == ShopUnlockState.Purchased)
            return purchasedLabel;

        // Локализация у этих апгрейдов ещё не заведена: GetLocalizedName вернёт сам ключ.
        // В этом случае показываем имя ассета — оно понятнее, чем "shopunlock.1.name".
        string localized = upgrade.GetLocalizedName();
        if (string.IsNullOrEmpty(localized) || localized == upgrade.nameKey)
            return upgrade.name;

        return localized;
    }

    private Color GetTint(ShopUnlockState state)
    {
        switch (state)
        {
            case ShopUnlockState.Available: return availableTint;
            case ShopUnlockState.NotEnoughGold: return notEnoughGoldTint;
            case ShopUnlockState.LockedByRequirement: return lockedTint;
            default: return purchasedTint;
        }
    }
}
