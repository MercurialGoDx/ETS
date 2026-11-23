using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

/// <summary>
/// Экранные кнопки, которых на ПК нет, потому что там есть клавиатура:
/// пауза (Esc) и инвентарь (Tab) на боевом экране, лимит кадров в меню паузы.
///
/// Собирается в рантайме, а не лежит в MainScene: сцену одновременно правит ПК-команда,
/// а умного мержа Unity YAML в проекте нет (docs/Git.md). Так мобильный порт не трогает
/// ни одного объекта сцены и не конфликтует с ней. Создаёт его <see cref="MobileBootstrap"/>
/// только на мобильной платформе.
/// </summary>
public class MobileHud : MonoBehaviour
{
    // 48 dp — рекомендованный Android минимум для зоны касания; 1 dp = 1/160 дюйма.
    private const float MinTouchTargetInches = 48f / 160f;
    private const float ButtonGap = 12f;
    private const string ButtonSpriteName = "basic_button";
    private const string JournalButtonName = "JournalButton";

    private static readonly Color IconColor = new Color(0.96f, 0.92f, 0.82f);
    private static readonly Color FallbackButtonColor = new Color(0.16f, 0.13f, 0.11f, 0.92f);

    private PauseManager pauseManager;
    private InventoryUI inventory;
    private Canvas gameCanvas;
    private RectTransform anchorButton;

    private RectTransform controlsRoot;
    private RectTransform pauseButton;
    private RectTransform inventoryButton;
    private TMP_Text fpsLabel;

    private Sprite buttonSprite;
    private bool controlsVisible = true;

    private void Start()
    {
        pauseManager = FindFirstObjectByType<PauseManager>(FindObjectsInactive.Include);
        inventory = FindFirstObjectByType<InventoryUI>(FindObjectsInactive.Include);
        buttonSprite = FindLoadedSprite(ButtonSpriteName);

        if (inventory != null)
            gameCanvas = inventory.GetComponentInParent<Canvas>(true)?.rootCanvas;

        if (gameCanvas == null || pauseManager == null)
        {
            Debug.LogWarning("[MobileHud] GameCanvas или PauseManager не найдены — экранные кнопки не созданы.");
            enabled = false;
            return;
        }

        BuildBattleControls();
        BuildFrameRateButton();

        if (GameStateManager.Instance != null)
            GameStateManager.Instance.OnStateChanged += OnStateChanged;

        OnStateChanged(GameStateManager.Instance != null ? GameStateManager.Instance.CurrentState : GameState.Menu);
    }

    private void OnDestroy()
    {
        if (GameStateManager.Instance != null)
            GameStateManager.Instance.OnStateChanged -= OnStateChanged;

        Screen.sleepTimeout = SleepTimeout.SystemSetting;
    }

    private void Update()
    {
        // Кнопки видны только в живом бою. Журнал, инвентарь, обучение и награда босса
        // снимают разрешение на паузу — по нему же прячем кнопки, чтобы они не висели
        // поверх открытой панели.
        bool shouldShow = pauseManager.CanPause && !pauseManager.IsPaused
            && GameStateManager.Instance != null && GameStateManager.Instance.Is(GameState.Playing);

        if (shouldShow != controlsVisible)
        {
            controlsVisible = shouldShow;
            controlsRoot.gameObject.SetActive(shouldShow);
            if (shouldShow)
                LayoutButtons();
        }
    }

    private static void OnStateChanged(GameState state)
    {
        // Во время боя экран не гаснет: игрок может смотреть на автобой, не касаясь его.
        // В меню и на паузе телефон засыпает как обычно.
        Screen.sleepTimeout = state == GameState.Playing ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
    }

    // ===================== БОЕВЫЕ КНОПКИ =====================

    private void BuildBattleControls()
    {
        controlsRoot = new GameObject("MobileControls", typeof(RectTransform)).GetComponent<RectTransform>();
        controlsRoot.SetParent(gameCanvas.transform, false);
        Stretch(controlsRoot);

        // Встаём сразу за кнопкой журнала: панели, что идут в иерархии позже (журнал,
        // инвентарь, статистика), перекрывают нас, как и её.
        anchorButton = FindChildRect(gameCanvas.transform, JournalButtonName);
        if (anchorButton != null && anchorButton.parent == gameCanvas.transform)
            controlsRoot.SetSiblingIndex(anchorButton.GetSiblingIndex() + 1);

        pauseButton = CreateButton(controlsRoot, "MobilePauseButton", pauseManager.TogglePause);
        AddPauseIcon(pauseButton);

        if (inventory != null)
        {
            inventoryButton = CreateButton(controlsRoot, "MobileInventoryButton", inventory.Toggle);
            AddInventoryIcon(inventoryButton);
        }

        LayoutButtons();
    }

    /// <summary>
    /// Пауза — слева от журнала, инвентарь — под ним. Без журнала в сцене — в правом верхнем углу.
    /// Размер не меньше 48 dp на этом экране и не меньше кнопки журнала.
    /// </summary>
    private void LayoutButtons()
    {
        float size = TouchTargetSize(anchorButton != null ? anchorButton.rect.height : 100f);

        Vector2 anchor = new Vector2(1f, 1f);
        Vector2 origin = new Vector2(-24f, -24f - size);
        float journalSize = size;

        if (anchorButton != null)
        {
            anchor = anchorButton.anchorMin;
            journalSize = Mathf.Max(anchorButton.rect.width, anchorButton.rect.height);
            // Левый нижний угол кнопки журнала в координатах её якоря.
            origin = anchorButton.anchoredPosition - Vector2.Scale(anchorButton.pivot, anchorButton.rect.size);
        }

        Place(pauseButton, anchor, size, origin + new Vector2(-ButtonGap - size, 0f));
        if (inventoryButton != null)
            Place(inventoryButton, anchor, size, origin + new Vector2(journalSize - size, -ButtonGap - size));
    }

    private float TouchTargetSize(float designSize)
    {
        float scale = gameCanvas.scaleFactor > 0f ? gameCanvas.scaleFactor : 1f;
        float minPixels = Screen.dpi > 0f ? Screen.dpi * MinTouchTargetInches : 0f;
        return Mathf.Max(designSize, minPixels / scale);
    }

    private static void Place(RectTransform rect, Vector2 anchor, float size, Vector2 bottomLeft)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = Vector2.zero;
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = bottomLeft;
    }

    // ===================== ЛИМИТ КАДРОВ В МЕНЮ ПАУЗЫ =====================

    /// <summary>
    /// 60/30 FPS — замена настольному выбору разрешения. Копирует кнопку «Продолжить»,
    /// чтобы совпасть по оформлению, и встаёт под «Главное меню».
    /// </summary>
    private void BuildFrameRateButton()
    {
        Button resume = pauseManager.resumeButton;
        Button mainMenu = pauseManager.mainMenuButton;
        if (resume == null || mainMenu == null)
            return;

        GameObject clone = Instantiate(resume.gameObject, resume.transform.parent, false);
        clone.name = "MobileFpsButton";

        // У копии остались бы локализация «Продолжить» и вызов ResumeGame из инспектора.
        foreach (var localized in clone.GetComponentsInChildren<LocalizeStringEvent>(true))
            Destroy(localized);

        var button = clone.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(ToggleFrameRate);

        var rect = (RectTransform)clone.transform;
        var resumeRect = (RectTransform)resume.transform;
        var mainMenuRect = (RectTransform)mainMenu.transform;
        rect.anchoredPosition = mainMenuRect.anchoredPosition + (mainMenuRect.anchoredPosition - resumeRect.anchoredPosition);
        clone.transform.SetSiblingIndex(mainMenu.transform.GetSiblingIndex() + 1);

        fpsLabel = clone.GetComponentInChildren<TMP_Text>(true);
        RefreshFrameRateLabel();
    }

    private void ToggleFrameRate()
    {
        MobilePlatform.TargetFps = MobilePlatform.TargetFps == MobilePlatform.HighFps
            ? MobilePlatform.BatterySaverFps
            : MobilePlatform.HighFps;
        RefreshFrameRateLabel();
    }

    private void RefreshFrameRateLabel()
    {
        if (fpsLabel != null)
            fpsLabel.text = MobilePlatform.TargetFps + " FPS";
    }

    // ===================== ПОСТРОЕНИЕ КНОПОК =====================

    private RectTransform CreateButton(Transform parent, string name, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        var image = go.GetComponent<Image>();
        if (buttonSprite != null)
        {
            image.sprite = buttonSprite;
            image.type = buttonSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        }
        else
        {
            image.color = FallbackButtonColor;
        }

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);
        return rect;
    }

    private static void AddPauseIcon(RectTransform button)
    {
        AddBar(button, new Vector2(0.30f, 0.26f), new Vector2(0.44f, 0.74f));
        AddBar(button, new Vector2(0.56f, 0.26f), new Vector2(0.70f, 0.74f));
    }

    private static void AddInventoryIcon(RectTransform button)
    {
        // Сетка 2×2 — те же ячейки, что в самой панели инвентаря.
        AddBar(button, new Vector2(0.26f, 0.54f), new Vector2(0.46f, 0.74f));
        AddBar(button, new Vector2(0.54f, 0.54f), new Vector2(0.74f, 0.74f));
        AddBar(button, new Vector2(0.26f, 0.26f), new Vector2(0.46f, 0.46f));
        AddBar(button, new Vector2(0.54f, 0.26f), new Vector2(0.74f, 0.46f));
    }

    private static void AddBar(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = go.GetComponent<Image>();
        image.color = IconColor;
        image.raycastTarget = false;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static RectTransform FindChildRect(Transform root, string name)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == name)
                return child as RectTransform;
        }
        return null;
    }

    /// <summary>
    /// Спрайт кнопок игры. Отдельной ссылки на него у нас нет (сцену не трогаем), но сцена
    /// использует его в «Готов» и «Продолжить», поэтому к этому моменту он уже загружен.
    /// </summary>
    private static Sprite FindLoadedSprite(string spriteName)
    {
        foreach (var sprite in Resources.FindObjectsOfTypeAll<Sprite>())
        {
            if (sprite.name == spriteName)
                return sprite;
        }
        return null;
    }
}
