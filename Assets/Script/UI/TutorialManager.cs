using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Одноразовое обучение при первом заходе в игру. Срабатывает при первом переходе
/// в состояние <see cref="GameState.Playing"/> (после кнопки «Готов»): ставит игру на
/// паузу (timeScale = 0), затемняет экран, обводит ключевые элементы интерфейса рамками
/// с подписями и выводит общий текст про волны, боссов и цель. Закрывается кнопкой
/// «Понятно» и больше не показывается (запоминается в <see cref="PlayerPrefs"/>).
/// </summary>
public class TutorialManager : MonoBehaviour
{
    private const string SeenKey = "tutorial_seen";

    [System.Serializable]
    public class Highlight
    {
        public RectTransform target;                 // основной элемент (рамка охватывает его)
        public List<RectTransform> union = new List<RectTransform>(); // доп. элементы: рамка охватит target + все из union (для групп вроде ×1/×2/×3)
        [TextArea] public string caption;            // подпись рядом с рамкой
        public Vector2 labelOffset = new Vector2(0f, 40f); // смещение подписи от центра рамки
        public Vector2 frameOffset = Vector2.zero;   // ручной сдвиг рамки (когда текст прижат к краю своего rect, напр. таймер)
    }

    [Header("Overlay")]
    [Tooltip("Корень оверлея (затемнение + рамки + кнопка). Выключен по умолчанию.")]
    [SerializeField] private GameObject overlayRoot;
    [Tooltip("Полноэкранный контейнер, в который складываются рамки и подписи.")]
    [SerializeField] private RectTransform framesContainer;
    [Tooltip("Кнопка «Понятно» — снимает паузу и закрывает обучение.")]
    [SerializeField] private Button gotItButton;

    [Header("Highlights")]
    [SerializeField] private List<Highlight> highlights = new List<Highlight>();

    [Header("Frame style")]
    [SerializeField] private Color frameColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private float frameThickness = 6f;
    [SerializeField] private float framePadding = 12f;
    [SerializeField] private TMP_FontAsset labelFont;
    [SerializeField] private float labelFontSize = 28f;

    [Header("Integration")]
    [Tooltip("Чтобы запретить Esc-паузу пока показывается обучение.")]
    [SerializeField] private PauseManager pauseManager;
    [Tooltip("Для отладки: показывать обучение каждый запуск, игнорируя PlayerPrefs.")]
    [SerializeField] private bool alwaysShow = false;

    private bool shown = false;
    private float prevTimeScale = 1f;

    private void Awake()
    {
        if (overlayRoot != null) overlayRoot.SetActive(false);
        if (gotItButton != null) gotItButton.onClick.AddListener(Dismiss);
    }

    private void Start()
    {
        // Подписываемся в Start: GameStateManager.Instance уже создан в своём Awake.
        if (GameStateManager.Instance != null)
            GameStateManager.Instance.OnStateChanged += HandleStateChanged;
    }

    private void OnDestroy()
    {
        if (GameStateManager.Instance != null)
            GameStateManager.Instance.OnStateChanged -= HandleStateChanged;
        if (gotItButton != null) gotItButton.onClick.RemoveListener(Dismiss);
    }

    private void HandleStateChanged(GameState state)
    {
        if (shown || state != GameState.Playing) return;
        if (!alwaysShow && PlayerPrefs.GetInt(SeenKey, 0) != 0) return;
        Show();
    }

    private void Show()
    {
        shown = true;

        prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        if (pauseManager != null) pauseManager.SetCanPause(false);
        if (overlayRoot != null) overlayRoot.SetActive(true);

        // Ждём кадр, чтобы лэйаут целей успел пересчитаться (корректно при timeScale = 0).
        StartCoroutine(BuildFramesNextFrame());
    }

    private IEnumerator BuildFramesNextFrame()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        BuildFrames();
    }

    /// <summary>Закрыть обучение, снять паузу и запомнить, что игрок его видел.</summary>
    public void Dismiss()
    {
        PlayerPrefs.SetInt(SeenKey, 1);
        PlayerPrefs.Save();

        if (overlayRoot != null) overlayRoot.SetActive(false);

        Time.timeScale = prevTimeScale;
        if (pauseManager != null) pauseManager.SetCanPause(true);
    }

    private void BuildFrames()
    {
        if (framesContainer == null) return;

        // Чистим прошлые рамки (на случай повторного показа в отладке).
        for (int i = framesContainer.childCount - 1; i >= 0; i--)
            Destroy(framesContainer.GetChild(i).gameObject);

        Canvas canvas = framesContainer.GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? canvas.worldCamera : null;

        foreach (var h in highlights)
        {
            if (h == null || h.target == null) continue;

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            bool any = false;

            any |= Accumulate(h.target, framesContainer, cam, ref min, ref max);
            if (h.union != null)
                for (int i = 0; i < h.union.Count; i++)
                    any |= Accumulate(h.union[i], framesContainer, cam, ref min, ref max);

            if (!any) continue;
            Rect r = new Rect(min + h.frameOffset, max - min);
            CreateOutline(r);
            CreateLabel(r, h.caption, h.labelOffset);
        }
    }

    // Добавляет углы цели (в локальных координатах контейнера) в общий bounding box.
    private bool Accumulate(RectTransform target, RectTransform space, Camera cam, ref Vector2 min, ref Vector2 max)
    {
        if (target == null || !target.gameObject.activeInHierarchy) return false;

        Vector3[] corners = new Vector3[4];
        target.GetWorldCorners(corners);
        for (int i = 0; i < 4; i++)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(space, screen, cam, out local);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }
        return true;
    }

    private void CreateOutline(Rect r)
    {
        float t = frameThickness;
        float pad = framePadding;
        float w = r.width + pad * 2f;
        float h = r.height + pad * 2f;
        Vector2 c = r.center;

        // 4 линии: верх / низ / лево / право (перекрываются на углах за счёт +t).
        CreateEdge(new Vector2(c.x, c.y + h * 0.5f), new Vector2(w + t, t));
        CreateEdge(new Vector2(c.x, c.y - h * 0.5f), new Vector2(w + t, t));
        CreateEdge(new Vector2(c.x - w * 0.5f, c.y), new Vector2(t, h + t));
        CreateEdge(new Vector2(c.x + w * 0.5f, c.y), new Vector2(t, h + t));
    }

    private void CreateEdge(Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject("Edge", typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(framesContainer, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.AddComponent<Image>();
        img.color = frameColor;
        img.raycastTarget = false;
    }

    private void CreateLabel(Rect r, string caption, Vector2 offset)
    {
        if (string.IsNullOrEmpty(caption)) return;

        GameObject go = new GameObject("Label", typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(framesContainer, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(380f, 60f);

        // Подпись ставится свободным смещением от центра рамки — так можно вручную
        // развести подписи в загруженных областях (низ-центр: скорость/время/реролл).
        rt.anchoredPosition = r.center + offset;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = labelFont != null ? labelFont : TMP_Settings.defaultFontAsset;
        tmp.text = caption;
        tmp.fontSize = labelFontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = frameColor;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = true;
    }

    // Для тестов: сбросить флаг «обучение показано».
    [ContextMenu("Reset Tutorial Flag")]
    private void ResetTutorialFlag()
    {
        PlayerPrefs.DeleteKey(SeenKey);
        PlayerPrefs.Save();
        Debug.Log("[TutorialManager] tutorial_seen сброшен.");
    }
}
