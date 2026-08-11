using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Общая часть слота предмета (магазин, инвентарь): иконка, ссылка на текущий предмет
/// и тултип по наведению. Клик по умолчанию ничего не делает — наследники
/// переопределяют <see cref="OnPointerClick"/> (магазин — покупкой).
/// Поле iconImage намеренно называется так же, как раньше в ShopSlot, — на него
/// завязаны сериализованные ссылки в сцене.
/// </summary>
public abstract class ItemSlotBase : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI")]
    [SerializeField] protected Image iconImage;

    protected WeaponDefinition currentWeapon;
    protected UpgradeBaseSO currentUpgrade;

    /// <summary>Можно ли сейчас показывать тултип (магазин глушит его на закрытом слоте).</summary>
    protected virtual bool TooltipAllowed => true;

    /// <summary>Иконка предмета; null прячет Image, чтобы не висел белый квадрат.</summary>
    protected void SetIcon(Sprite sprite)
    {
        if (iconImage == null)
            return;

        iconImage.sprite = sprite;
        iconImage.enabled = sprite != null;
    }

    public virtual void OnPointerClick(PointerEventData eventData)
    {
        // по умолчанию слот только для чтения
    }

    public virtual void OnPointerEnter(PointerEventData eventData)
    {
        if (!TooltipAllowed) return;

        if (WeaponTooltip.Instance == null)
            return;

        if (currentWeapon != null)
        {
            WeaponTooltip.Instance.Show(currentWeapon);
        }
        else if (currentUpgrade != null)
        {
            WeaponTooltip.Instance.Show(
                currentUpgrade.GetLocalizedName(),
                currentUpgrade.GetLocalizedDescription()
            );
        }
    }

    public virtual void OnPointerExit(PointerEventData eventData)
    {
        if (WeaponTooltip.Instance != null)
            WeaponTooltip.Instance.Hide();
    }
}
