using UnityEngine;
using TMPro;

public class WeaponTooltip : MonoBehaviour
{
    public static WeaponTooltip Instance;

    [Header("UI")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private RectTransform tooltipRect;

    [Header("Follow")]
    [SerializeField] private Canvas canvas; // Canvas, на котором тултип
    [SerializeField] private Vector2 offset = new Vector2(-12f, -12f); // "слева от мыши" => X отрицательный
    [SerializeField] private bool clampToScreen = true;
    [SerializeField] private float padding = 8f;

    private RectTransform canvasRect;
    private Camera uiCamera;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (tooltipRect == null)
            tooltipRect = transform as RectTransform;

        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        canvasRect = canvas != null ? canvas.transform as RectTransform : null;

        // Для Screen Space Overlay камера должна быть null
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            uiCamera = canvas.worldCamera;

        Hide();
    }

    private void Update()
    {
        if (!gameObject.activeSelf) return;
        FollowMouse(Input.mousePosition);
    }

    // Оружие
    public void Show(WeaponDefinition weapon)
    {
        if (weapon == null) return;
        ShowInternal(weapon.GetLocalizedName(), weapon.GetLocalizedDescription());
    }

    // Универсальный
    public void Show(string title, string description)
    {
        ShowInternal(title, description);
    }

    private void ShowInternal(string title, string description)
    {
        gameObject.SetActive(true);

        // Поднимаем тултип поверх всех соседних UI-элементов (включая тёмный фон журнала),
        // иначе он рисуется за оверлеем и плохо виден.
        transform.SetAsLastSibling();

        if (titleText != null) titleText.text = title;
        if (descriptionText != null) descriptionText.text = description;

        // Чтобы обновился Layout/ContentSizeFitter до позиционирования/клампа
        Canvas.ForceUpdateCanvases();
        FollowMouse(Input.mousePosition);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void FollowMouse(Vector2 mouseScreenPos)
    {
        if (canvasRect == null || tooltipRect == null) return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, mouseScreenPos, uiCamera, out var localPoint))
            return;

        Vector2 anchored = localPoint + offset;

        if (clampToScreen)
            anchored = ClampToCanvas(anchored);

        tooltipRect.anchoredPosition = anchored;
    }

    private Vector2 ClampToCanvas(Vector2 anchoredPos)
    {
        Vector2 size = tooltipRect.rect.size;
        Vector2 pivot = tooltipRect.pivot;

        float left   = anchoredPos.x - size.x * pivot.x;
        float right  = anchoredPos.x + size.x * (1f - pivot.x);
        float bottom = anchoredPos.y - size.y * pivot.y;
        float top    = anchoredPos.y + size.y * (1f - pivot.y);

        Vector2 cSize = canvasRect.rect.size;

        float cLeft   = -cSize.x * 0.5f + padding;
        float cRight  =  cSize.x * 0.5f - padding;
        float cBottom = -cSize.y * 0.5f + padding;
        float cTop    =  cSize.y * 0.5f - padding;

        if (left < cLeft)     anchoredPos.x += (cLeft - left);
        if (right > cRight)   anchoredPos.x -= (right - cRight);
        if (bottom < cBottom) anchoredPos.y += (cBottom - bottom);
        if (top > cTop)       anchoredPos.y -= (top - cTop);

        return anchoredPos;
    }
}
