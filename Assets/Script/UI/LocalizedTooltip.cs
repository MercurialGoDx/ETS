using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;

/// <summary>
/// Тултип по наведению с текстом из системы локализации (пара ключей: название + описание).
/// В отличие от LocalizedTMP (постоянно пишет в TMP_Text), показывает во всплывающем
/// WeaponTooltip только пока курсор на объекте — тот же приём, что у RerollButtonTooltip,
/// только текст берётся не из инспектора, а по локализационным ключам.
/// </summary>
public class LocalizedTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Tooltip("Ключ локализованного названия — заголовок тултипа.")]
    public LocalizedString nameString = new LocalizedString();

    [Tooltip("Ключ локализованного описания — тело тултипа.")]
    public LocalizedString descriptionString = new LocalizedString();

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (WeaponTooltip.Instance == null)
            return;

        string title = nameString.IsEmpty ? string.Empty : nameString.GetLocalizedString();
        string description = descriptionString.IsEmpty ? string.Empty : descriptionString.GetLocalizedString();

        WeaponTooltip.Instance.Show(title, description);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (WeaponTooltip.Instance != null)
            WeaponTooltip.Instance.Hide();
    }
}
