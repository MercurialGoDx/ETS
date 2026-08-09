using UnityEngine;

/// <summary>
/// ВРЕМЕННЫЙ диагностический оверлей: показывает по F1 текущее состояние экрана.
/// Нужен, чтобы поймать момент, когда разрешение или режим окна меняются сами —
/// например при возврате в главное меню и старте нового забега.
///
/// Создаётся автоматически при запуске приложения и переживает перезагрузку сцены,
/// поэтому видит и то, что происходит ВО ВРЕМЯ смены сцены. Ничего в сцене настраивать
/// не нужно. Чтобы убрать — удалить этот файл.
/// </summary>
public class ScreenDebugOverlay : MonoBehaviour
{
    private const KeyCode ToggleKey = KeyCode.F1;

    private bool visible;
    private GUIStyle style;

    // Предыдущее состояние — чтобы поймать сам факт изменения и записать его в лог.
    private int lastWidth, lastHeight;
    private FullScreenMode lastMode;
    private string lastChange = "изменений не было";
    private float lastChangeTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        var go = new GameObject("~ScreenDebugOverlay");
        go.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(go);
        go.AddComponent<ScreenDebugOverlay>();
    }

    private void Awake()
    {
        CaptureState();
        Debug.Log($"[ScreenDebug] старт: {Describe()}");
    }

    private void Update()
    {
        if (Input.GetKeyDown(ToggleKey))
            visible = !visible;

        // Логируем каждое изменение, даже когда оверлей скрыт: в Player.log останется след
        // с временем, а рядом будут строки о смене сцены.
        if (Screen.width != lastWidth || Screen.height != lastHeight || Screen.fullScreenMode != lastMode)
        {
            string from = $"{lastWidth}x{lastHeight} {lastMode}";
            CaptureState();
            lastChange = $"{from}  ->  {Screen.width}x{Screen.height} {Screen.fullScreenMode}";
            lastChangeTime = Time.realtimeSinceStartup;
            Debug.Log($"[ScreenDebug] ИЗМЕНЕНИЕ на {lastChangeTime:F1}с: {lastChange}");
        }
    }

    private void CaptureState()
    {
        lastWidth = Screen.width;
        lastHeight = Screen.height;
        lastMode = Screen.fullScreenMode;
    }

    private string Describe()
    {
        var cur = Screen.currentResolution;
        return $"Screen {Screen.width}x{Screen.height}, mode {Screen.fullScreenMode}, " +
               $"currentResolution {cur.width}x{cur.height}@{cur.refreshRateRatio.value:F0}, " +
               $"display {Display.main.systemWidth}x{Display.main.systemHeight}";
    }

    private void OnGUI()
    {
        if (!visible)
            return;

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(14, Screen.height / 55),
                normal = { textColor = Color.white },
                padding = new RectOffset(12, 12, 8, 8)
            };
        }

        var cur = Screen.currentResolution;
        string text =
            $"F1 — скрыть\n" +
            $"Screen.width x height      {Screen.width} x {Screen.height}\n" +
            $"Screen.fullScreenMode      {Screen.fullScreenMode}\n" +
            $"Screen.fullScreen          {Screen.fullScreen}\n" +
            $"Screen.currentResolution   {cur.width} x {cur.height} @ {cur.refreshRateRatio.value:F0} Hz\n" +
            $"Display.main.system        {Display.main.systemWidth} x {Display.main.systemHeight}\n" +
            $"Screen.dpi                 {Screen.dpi:F0}\n" +
            $"сцена                      {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}\n" +
            $"последнее изменение        {lastChange}" +
            (lastChangeTime > 0f ? $"  ({lastChangeTime:F1}с)" : "");

        var size = style.CalcSize(new GUIContent(text));
        var rect = new Rect(10, 10, size.x + 24, size.y + 16);

        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = prev;

        GUI.Label(rect, text, style);
    }
}
