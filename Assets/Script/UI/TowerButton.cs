using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class TowerButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private Outline outline;

    [Header("Окно, которое открывается при клике")]
    public GameObject windowToOpen;

    private void Awake()
    {
        outline = GetComponent<Outline>();

        // Если Outline нет — создаём
        if (outline == null)
        {
            outline = gameObject.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(4, -4);
        }

        // По умолчанию выключен
        outline.enabled = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (outline != null)
            outline.enabled = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (outline != null)
            outline.enabled = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (windowToOpen == null)
            return;

        // Переключение окна
        bool newState = !windowToOpen.activeSelf;
        windowToOpen.SetActive(newState);
    }
}
