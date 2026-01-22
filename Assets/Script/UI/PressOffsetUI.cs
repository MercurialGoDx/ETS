using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class PressOffsetUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Header("What to move (optional)")]
    [Tooltip("If empty, will move this object's RectTransform (the button). " +
             "If you assign a child (e.g., Text/TMP), only it will move.")]
    [SerializeField] private RectTransform target;

    [Header("Offset on press")]
    [SerializeField] private Vector2 pressedOffset = new Vector2(0f, -6f);

    [Header("Behaviour")]
    [Tooltip("If true, returns to start position when pointer leaves button while pressed.")]
    [SerializeField] private bool resetOnExit = true;

    private Vector2 _startPos;
    private bool _hasStartPos;

    private void Awake()
    {
        if (target == null) target = transform as RectTransform;
        CacheStartPos();
    }

    private void OnEnable()
    {
        // На случай если позицию поменяли в редакторе/в рантайме до включения
        CacheStartPos();
        ApplyNormal();
    }

    private void CacheStartPos()
    {
        if (target == null) return;
        _startPos = target.anchoredPosition;
        _hasStartPos = true;
    }

    public void OnPointerDown(PointerEventData eventData) => ApplyPressed();
    public void OnPointerUp(PointerEventData eventData) => ApplyNormal();

    public void OnPointerExit(PointerEventData eventData)
    {
        if (resetOnExit) ApplyNormal();
    }

    private void ApplyPressed()
    {
        if (!_hasStartPos || target == null) return;
        target.anchoredPosition = _startPos + pressedOffset;
    }

    private void ApplyNormal()
    {
        if (!_hasStartPos || target == null) return;
        target.anchoredPosition = _startPos;
    }
}
