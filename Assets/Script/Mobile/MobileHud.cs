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
    private const float ButtonGap = 28f;
    private const string ButtonSpriteName = "basic_button";
    private const string JournalButtonName = "JournalButton";
    private const string KeyboardHintKey = "inv.hint_close";
    private const float CloseButtonSize = 64f;
    private const float CloseButtonInset = 12f;

    private static readonly Color IconColor = new Color(0.96f, 0.92f, 0.82f);
    private static readonly Color FallbackButtonColor = new Color(0.16f, 0.13f, 0.11f, 0.92f);

    private PauseManager pauseManager;
    private InventoryUI inventory;
    private BuildViewerUI buildViewer;
    private JournalManager journal;
    private LeaderboardUI leaderboard;
    private DamageStatisticsUI statisticsWindow;
    private StartMenu startMenu;
    private bool buildViewerWasOpen;
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
        journal = FindFirstObjectByType<JournalManager>(FindObjectsInactive.Include);
        leaderboard = FindFirstObjectByType<LeaderboardUI>(FindObjectsInactive.Include);
        statisticsWindow = FindFirstObjectByType<DamageStatisticsUI>(FindObjectsInactive.Include);
        startMenu = FindFirstObjectByType<StartMenu>(FindObjectsInactive.Include);
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
        GuardGoldSpendingButtons();

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

        if (Input.GetKeyDown(KeyCode.Escape))
            HandleBack();
    }

    private void LateUpdate()
    {
        // Просмотр билда закрывается по Esc сам. Запоминаем, был ли он открыт к концу кадра,
        // чтобы то же нажатие «Назад» не закрыло ещё и таблицу под ним.
        buildViewerWasOpen = IsActive(buildViewer != null ? buildViewer.PanelRoot : null);
    }

    /// <summary>
    /// «Назад» Android приходит как Esc. Пауза, инвентарь и просмотр билда ловят его сами;
    /// здесь — окна, которые на ПК закрывались только мышью: журнал, статистика урона
    /// на экране поражения и таблица рекордов в главном меню.
    /// </summary>
    private void HandleBack()
    {
        if (journal != null && journal.IsOpen)
        {
            journal.Close();
            return;
        }

        if (statisticsWindow != null && statisticsWindow.gameObject.activeInHierarchy && startMenu != null)
        {
            startMenu.CloseStatistics();
            return;
        }

        if (leaderboard != null && leaderboard.gameObject.activeInHierarchy && !buildViewerWasOpen
            && !IsActive(buildViewer != null ? buildViewer.PanelRoot : null))
        {
            leaderboard.Close();
        }
    }

    private static bool IsActive(GameObject go) => go != null && go.activeInHierarchy;

    private static void OnStateChanged(GameState state)
    {
#if PLATFORM_MOBILE
        // Начало боя после подготовки — в аналитику; конец пишет таблица рекордов вместе со временем.
        if (state == GameState.Playing && GameStateManager.Instance.PreviousState == GameState.Preparing)
            FirebaseServices.LogEvent("run_start");
#endif

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
    /// Пауза — слева от журнала, инвентарь — под ним, того же видимого размера, что книга.
    /// Границы книги берутся по мировым углам: в сцене она увеличена через localScale,
    /// и rect без масштаба давал наложение. Зону касания до 48 dp добирает MinTouchArea.
    /// </summary>
    private void LayoutButtons()
    {
        Vector2 bottomLeft;
        Vector2 topRight;
        if (anchorButton != null)
        {
            var corners = new Vector3[4];
            anchorButton.GetWorldCorners(corners);
            bottomLeft = controlsRoot.InverseTransformPoint(corners[0]);
            topRight = controlsRoot.InverseTransformPoint(corners[2]);
        }
        else
        {
            Rect area = controlsRoot.rect;
            topRight = new Vector2(area.xMax - 24f, area.yMax - 24f);
            bottomLeft = topRight - new Vector2(110f, 110f);
        }

        float size = topRight.y - bottomLeft.y;
        Vector2 center = controlsRoot.rect.center;
        Place(pauseButton, size, new Vector2(bottomLeft.x - ButtonGap - size, bottomLeft.y) - center);
        if (inventoryButton != null)
            Place(inventoryButton, size, new Vector2(topRight.x - size, bottomLeft.y - ButtonGap - size) - center);
    }

    private static void Place(RectTransform rect, float size, Vector2 bottomLeft)
    {
        var middle = new Vector2(0.5f, 0.5f);
        rect.anchorMin = middle;
        rect.anchorMax = middle;
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

            // Крестик — в правый верхний угол самой панели и не выше шапки:
            // привязка к подписи с размером 48 dp вылезала за рамку и на заголовок.
            var panel = (RectTransform)CloseTargetPanel(label.transform).transform;
            RectTransform button = CreateButton(panel, "MobileCloseButton", close);
            button.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            button.anchorMin = Vector2.one;
            button.anchorMax = Vector2.one;
            button.pivot = Vector2.one;
            button.sizeDelta = new Vector2(CloseButtonSize, CloseButtonSize);
            button.anchoredPosition = new Vector2(-CloseButtonInset, -CloseButtonInset);
            AddCloseIcon(button);
        }
    }

    private GameObject CloseTargetPanel(Transform label)
    {
        if (inventory != null && inventory.PanelRoot != null && label.IsChildOf(inventory.PanelRoot.transform))
            return inventory.PanelRoot;
        return buildViewer.PanelRoot;
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
        Vector2 size = languageButton.rect.size;
        rect.sizeDelta = new Vector2(size.x * 0.75f, size.y);
        // Видимый размер — как у кнопки языка, зона касания до 48 dp — невидимым отступом.
        clone.AddComponent<MinTouchArea>();
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

    // ===================== КНОПКИ С ЦЕНОЙ =====================

    /// <summary>
    /// Реролл и разблокировка слота тратят золото по клику, а описание с ценой показывают
    /// только «наведением». На телефоне удержание должно быть осмотром, а не покупкой.
    /// </summary>
    private static void GuardGoldSpendingButtons()
    {
        foreach (var tooltip in FindObjectsByType<RerollButtonTooltip>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            AddHoldInspect(tooltip.gameObject);
        foreach (var tooltip in FindObjectsByType<UnlockUpgradeButtonTooltip>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            AddHoldInspect(tooltip.gameObject);
    }

    private static void AddHoldInspect(GameObject target)
    {
        if (target.GetComponent<Selectable>() != null && target.GetComponent<TouchHoldInspect>() == null)
            target.AddComponent<TouchHoldInspect>();
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
        go.AddComponent<MinTouchArea>();
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
