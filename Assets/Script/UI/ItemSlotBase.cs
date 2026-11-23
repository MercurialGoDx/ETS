using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Общая часть слота предмета (магазин, инвентарь): иконка, ссылка на текущий предмет
/// и тултип по наведению. Клик по умолчанию ничего не делает — наследники
/// переопределяют <see cref="OnPointerClick"/> (магазин — покупкой).
/// Поле iconImage намеренно называется так же, как раньше в ShopSlot, — на него
/// завязаны сериализованные ссылки в сцене.
///
/// На сенсорном экране наведения нет: касание сразу и «наводит», и «кликает». Поэтому
/// там короткое касание — действие слота (покупка), а удержание — осмотр: тултип
/// показывается, пока палец лежит, и отпускание после осмотра не покупает предмет.
/// </summary>
public abstract class ItemSlotBase : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler,
    IPointerDownHandler, IPointerUpHandler
{
    // Дольше обычного тапа (~0.1–0.2 с), но заметно короче системного long press (0.5 с).
    private const float TouchInspectDelay = 0.35f;

    [Header("UI")]
    [SerializeField] protected Image iconImage;

    protected WeaponDefinition currentWeapon;
    protected UpgradeBaseSO currentUpgrade;

    private Coroutine touchInspectRoutine;
    private bool touchInspected;

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
        // Палец «наводится» в момент касания — тултип для него покажет удержание.
        if (MobilePlatform.IsTouch(eventData)) return;

        ShowTooltip();
    }

    public virtual void OnPointerExit(PointerEventData eventData)
    {
        if (WeaponTooltip.Instance != null)
            WeaponTooltip.Instance.Hide();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!MobilePlatform.IsTouch(eventData)) return;

        touchInspected = false;
        StopTouchInspect();
        touchInspectRoutine = StartCoroutine(TouchInspectAfterDelay());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!MobilePlatform.IsTouch(eventData)) return;

        StopTouchInspect();
        if (WeaponTooltip.Instance != null)
            WeaponTooltip.Instance.Hide();
    }

    /// <summary>
    /// Наследник зовёт в начале OnPointerClick: true — касание было осмотром,
    /// действие выполнять не нужно. Флаг одноразовый.
    /// </summary>
    protected bool ConsumeTouchInspect()
    {
        bool inspected = touchInspected;
        touchInspected = false;
        return inspected;
    }

    private IEnumerator TouchInspectAfterDelay()
    {
        // Реальное время: на подготовке магазин открыт при Time.timeScale = 0.
        yield return new WaitForSecondsRealtime(TouchInspectDelay);
        touchInspectRoutine = null;
        touchInspected = true;
        ShowTooltip();
    }

    private void StopTouchInspect()
    {
        if (touchInspectRoutine == null) return;
        StopCoroutine(touchInspectRoutine);
        touchInspectRoutine = null;
    }

    private void ShowTooltip()
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
}
