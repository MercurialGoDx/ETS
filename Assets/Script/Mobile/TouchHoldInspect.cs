using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Для кнопок с тултипом, которые тратят золото (реролл, разблокировка слота): на телефоне
/// тултип появляется при касании, а отпускание тут же жмёт кнопку — прочитать цену до покупки
/// нельзя. С этим компонентом удержание дольше <see cref="HoldSeconds"/> — только осмотр:
/// кнопка на время отпускания становится неактивной, и клик не проходит. Короткий тап работает как раньше.
/// Добавляется из кода (<see cref="MobileHud"/>), сцену не меняет.
/// </summary>
[RequireComponent(typeof(Selectable))]
public class TouchHoldInspect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    private const float HoldSeconds = 0.35f;

    private Selectable selectable;
    private Coroutine holdRoutine;
    private bool suppressed;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!MobilePlatform.IsTouch(eventData)) return;
        if (holdRoutine != null) StopCoroutine(holdRoutine);
        holdRoutine = StartCoroutine(SuppressAfterHold());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (holdRoutine != null)
        {
            StopCoroutine(holdRoutine);
            holdRoutine = null;
        }

        // Клик приходит после PointerUp в том же кадре — возвращаем кнопку кадром позже.
        if (suppressed)
            StartCoroutine(RestoreNextFrame());
    }

    private IEnumerator SuppressAfterHold()
    {
        yield return new WaitForSecondsRealtime(HoldSeconds);
        holdRoutine = null;
        if (!selectable.interactable)
            yield break; // кнопку уже выключила игра (нет золота) — не трогаем её состояние

        suppressed = true;
        selectable.interactable = false;
    }

    private IEnumerator RestoreNextFrame()
    {
        yield return null;
        suppressed = false;
        selectable.interactable = true;
    }

    private void OnDisable()
    {
        if (suppressed && selectable != null)
            selectable.interactable = true;
        suppressed = false;
        holdRoutine = null;
    }
}
