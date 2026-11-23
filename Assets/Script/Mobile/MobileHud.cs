using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

/// <summary>
/// Экранные элементы, которых на ПК нет, потому что там есть клавиатура:
/// пауза (Esc) и инвентарь (Tab) на боевом экране, крестик вместо подсказки «Tab — закрыть»
/// в инвентаре и просмотре билда, лимит кадров 60/30 в главном меню.
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
    private const float ButtonGap = 28f;
    private const string ButtonSpriteName = "basic_button";
    private const string JournalButtonName = "JournalButton";
    private const string KeyboardHintKey = "inv.hint_close";

    private static readonly Color IconColor = new Color(0.96f, 0.92f, 0.82f);
    private static readonly Color FallbackButtonColor = new Color(0.16f, 0.13f, 0.11f, 0.92f);

    private PauseManager pauseManager;
    private InventoryUI inventory;
    private BuildViewerUI buildViewer;
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
        buildViewer = FindFirstObjectByType<BuildViewerUI>(FindObjectsInactive.Include);
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
        ReplaceKeyboardHintsWithCloseButtons();
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
        float size = TouchTargetSize(gameCanvas, anchorButton != null ? anchorButton.rect.height : 100f);

        Vector2 anchor = new Vector2(1f, 1f);
        Vector2 origin = new Vector2(-24f - size, -24f - size);
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

    private static float TouchTargetSize(Canvas canvas, float designSize)
    {
        float scale = canvas != null && canvas.rootCanvas.scaleFactor > 0f ? canvas.rootCanvas.scaleFactor : 1f;
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

    // ===================== КРЕСТИК ВМЕСТО «TAB — ЗАКРЫТЬ» =====================

    /// <summary>
    /// Инвентарь и просмотр билда на ПК закрываются клавишей Tab, и в шапке написано
    /// «Tab — закрыть». На телефоне клавиши нет: текст гасим и на его месте ставим крестик.
    /// Крестик — дочерний к подписи и выровнен по её правому краю, поэтому следует
    /// за раскладкой шапки, не вмешиваясь в неё.
    /// </summary>
    private void ReplaceKeyboardHintsWithCloseButtons()
    {
        var labels = FindObjectsByType<LocalizedTMP>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var label in labels)
        {
            if (label.localizedString == null || label.localizedString.TableEntryReference.Key != KeyboardHintKey)
                continue;

            UnityAction close = CloseActionFor(label.transform);
            if (close == null)
                continue;

            label.enabled = false;
            var text = label.GetComponent<TMP_Text>();
            if (text != null)
                text.enabled = false;

            var rect = (RectTransform)label.transform;
            RectTransform button = CreateButton(rect, "MobileCloseButton", close);
            button.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            float size = TouchTargetSize(label.GetComponentInParent<Canvas>(true), Mathf.Max(64f, rect.rect.height * 1.6f));
            button.anchorMin = new Vector2(1f, 0.5f);
            button.anchorMax = new Vector2(1f, 0.5f);
            button.pivot = new Vector2(1f, 0.5f);
            button.sizeDelta = new Vector2(size, size);
            button.anchoredPosition = Vector2.zero;
            AddCloseIcon(button);
        }
    }

    private UnityAction CloseActionFor(Transform label)
    {
        if (inventory != null && inventory.PanelRoot != null && label.IsChildOf(inventory.PanelRoot.transform))
            return inventory.Toggle;

        if (buildViewer != null && buildViewer.PanelRoot != null && label.IsChildOf(buildViewer.PanelRoot.transform))
            return buildViewer.Close;

        return null;
    }

    // ===================== ЛИМИТ КАДРОВ В ГЛАВНОМ МЕНЮ =====================

    /// <summary>
    /// 60/30 FPS — замена настольному выбору разрешения. Стоит рядом с выбором языка:
    /// это второй и последний «системный» переключатель меню. Оформление копируется
    /// с кнопки «Продолжить» меню паузы.
    /// </summary>
    private void BuildFrameRateButton()
    {
        // В сцене есть и старый, выключенный переключатель языка — берём видимый.
        LanguageSelector language = null;
        foreach (var candidate in FindObjectsByType<LanguageSelector>(FindObjectsSortMode.None))
        {
            if (candidate.isActiveAndEnabled)
            {
                language = candidate;
                break;
            }
        }

        Button template = pauseManager.resumeButton;
        if (language == null || template == null)
        {
            Debug.LogWarning("[MobileHud] Переключатель языка или кнопка «Продолжить» не найдены — кнопки FPS не будет.");
            return;
        }

        RectTransform languageButton = FindVisibleLanguageButton(language);
        var parent = languageButton.parent as RectTransform;
        if (parent == null)
            return;

        GameObject clone = Instantiate(template.gameObject, parent, false);
        clone.name = "MobileFpsButton";

        // У копии остались бы локализация «Продолжить» и вызов ResumeGame из инспектора.
        foreach (var localized in clone.GetComponentsInChildren<LocalizedTMP>(true))
            Destroy(localized);
        foreach (var localized in clone.GetComponentsInChildren<LocalizeStringEvent>(true))
            Destroy(localized);

        var button = clone.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(ToggleFrameRate);

        // Того же размера, что кнопка языка, и справа от неё.
        var rect = (RectTransform)clone.transform;
        rect.anchorMin = languageButton.anchorMin;
        rect.anchorMax = languageButton.anchorMax;
        rect.pivot = languageButton.pivot;
        // Кнопка языка в сцене уменьшена через localScale — повторяем масштаб, иначе
        // копия выйдет втрое крупнее. Высота не меньше 48 dp с учётом этого масштаба.
        Vector3 scale = languageButton.localScale;
        rect.localScale = scale;
        float scaleY = Mathf.Max(0.01f, scale.y);
        Vector2 size = languageButton.rect.size;
        size.y = Mathf.Max(size.y, TouchTargetSize(parent.GetComponentInParent<Canvas>(true), 0f) / scaleY);
        rect.sizeDelta = new Vector2(size.x * 0.75f, size.y);
        float gap = 16f;
        rect.anchoredPosition = languageButton.anchoredPosition + new Vector2(
            (1f - languageButton.pivot.x) * languageButton.rect.width * scale.x
                + rect.pivot.x * rect.sizeDelta.x * scale.x + gap,
            0f);

        fpsLabel = clone.GetComponentInChildren<TMP_Text>(true);
        if (fpsLabel != null)
        {
            var labelOfLanguage = languageButton.GetComponentInChildren<TMP_Text>(true);
            fpsLabel.enableAutoSizing = false;
            fpsLabel.fontSize = labelOfLanguage != null ? labelOfLanguage.fontSize : fpsLabel.fontSize * 0.6f;
        }

        RefreshFrameRateLabel();
        Debug.Log($"[MobileHud] FPS button under '{parent.name}' at {rect.anchoredPosition}, size {rect.sizeDelta}.");
    }

    /// <summary>
    /// У LanguageSelector корень растянут шире видимой кнопки, а сама кнопка — дочерний
    /// объект с подписью. Берём прямоугольник, внутри которого лежит текст «Russian (ru)».
    /// </summary>
    private static RectTransform FindVisibleLanguageButton(LanguageSelector language)
    {
        var label = language.GetComponentInChildren<TMP_Text>(true);
        if (label != null && label.transform.parent is RectTransform labelParent && labelParent.GetComponent<Image>() != null)
            return labelParent;
        return label != null ? (RectTransform)label.transform : (RectTransform)language.transform;
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

    private RectTransform CreateButton(Transform parent, string name, UnityAction onClick)
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
        AddBar(button, new Vector2(0.30f, 0.26f), new Vector2(0.44f, 0.74f), 0f);
        AddBar(button, new Vector2(0.56f, 0.26f), new Vector2(0.70f, 0.74f), 0f);
    }

    private static void AddInventoryIcon(RectTransform button)
    {
        // Сетка 2×2 — те же ячейки, что в самой панели инвентаря.
        AddBar(button, new Vector2(0.26f, 0.54f), new Vector2(0.46f, 0.74f), 0f);
        AddBar(button, new Vector2(0.54f, 0.54f), new Vector2(0.74f, 0.74f), 0f);
        AddBar(button, new Vector2(0.26f, 0.26f), new Vector2(0.46f, 0.46f), 0f);
        AddBar(button, new Vector2(0.54f, 0.26f), new Vector2(0.74f, 0.46f), 0f);
    }

    private static void AddCloseIcon(RectTransform button)
    {
        AddBar(button, new Vector2(0.44f, 0.20f), new Vector2(0.56f, 0.80f), 45f);
        AddBar(button, new Vector2(0.44f, 0.20f), new Vector2(0.56f, 0.80f), -45f);
    }

    private static void AddBar(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, float angle)
    {
        var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localRotation = Quaternion.Euler(0f, 0f, angle);

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
