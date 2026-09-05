using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Окно лобби и кнопка «Создать лобби» в главном меню.
///
/// Всё строится кодом на собственном канвасе, а не собирается в MainScene. Причина
/// практическая: сцену активно правит команда (последний влив — больше четырёх тысяч строк
/// в MainScene), и параллельная сборка панели там столкнулась бы с чужой работой.
/// Побочная выгода — панель переживает любые правки сцены.
///
/// Кнопка в меню не рисуется с нуля, а клонируется с существующей кнопки меню: так она
/// наследует спрайт, шрифт и размеры проекта вместо приблизительной имитации.
///
/// Свой канвас намеренно масштабируется от 1920×1080. У GameCanvas в проекте референс
/// 800×600 при геометрии под 1080p — известный баг, из-за которого HUD раздувается;
/// сюда его тянуть незачем.
/// </summary>
public class LobbyPanelUI : MonoBehaviour
{
    // Палитра снята с магазина: светлая кладка, тёмный колодец, зелёная полоса, латунь.
    private static readonly Color Brick = new Color32(0x90, 0x96, 0xC8, 0xFF);
    private static readonly Color BrickEdge = new Color32(0x57, 0x5C, 0x8C, 0xFF);
    private static readonly Color Well = new Color32(0x2C, 0x24, 0x44, 0xFF);
    private static readonly Color Slot = new Color32(0x17, 0x13, 0x1F, 0xFF);
    private static readonly Color Line = new Color32(0xEC, 0xEA, 0xF6, 0xFF);
    private static readonly Color LineDim = new Color32(0x4C, 0x46, 0x66, 0xFF);
    private static readonly Color Green = new Color32(0x63, 0xC7, 0x4D, 0xFF);
    private static readonly Color GreenEdge = new Color32(0x2A, 0x66, 0x20, 0xFF);
    private static readonly Color Slate = new Color32(0x4E, 0x4A, 0x74, 0xFF);
    private static readonly Color SlateEdge = new Color32(0x2A, 0x27, 0x45, 0xFF);
    private static readonly Color Red = new Color32(0xB8, 0x41, 0x2F, 0xFF);
    private static readonly Color Gold = new Color32(0xE0, 0xA3, 0x3A, 0xFF);
    private static readonly Color Bone = new Color32(0xF2, 0xF0, 0xFA, 0xFF);
    private static readonly Color Dim = new Color32(0x9A, 0x93, 0xB8, 0xFF);

    public static LobbyPanelUI Instance { get; private set; }

    private Canvas canvas;
    private GameObject root;
    private GameObject panel;
    private TMP_FontAsset font;

    // Динамические части.
    private TMP_Text barRight, codeLabel, statusLabel, hintLabel;
    private TMP_Text[] seatNick = new TMP_Text[2];
    private TMP_Text[] seatTags = new TMP_Text[2];
    private Image[] seatFrame = new Image[2];
    private Button readyButton, inviteButton, copyButton, leaveButton, createButton, joinButton;
    private TMP_Text readyLabel;
    private TMP_InputField codeInput;
    private GameObject inLobbyGroup, noLobbyGroup;

    private GameObject menuButton;
    private bool built;

    /// <summary>Открыл ли игрок окно. Отдельный флаг, а не activeSelf: состояние объекта
    /// меняет и автозакрытие, и по нему уже нельзя понять намерение игрока.</summary>
    private bool wantOpen;

    /// <summary>Шаг между кнопками меню в его собственных координатах.</summary>
    private const float MenuStep = 100f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;

        var host = new GameObject(nameof(LobbyPanelUI));
        Instance = host.AddComponent<LobbyPanelUI>();
        DontDestroyOnLoad(host);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        EnsureMenuButton();

        // Перезагрузка домена может оставить объект живым, а ссылки на построенный UI —
        // пустыми. Тогда собираем заново, а не падаем каждый кадр.
        if (built && root == null)
            built = false;

        if (!built)
            return;

        // Закрываем только когда забег реально пошёл. Привязка к состоянию Menu была
        // слишком жёсткой: игра выходит из него ещё на закупке, и окно захлопывалось само.
        bool running = GameStateManager.Instance != null
            && GameStateManager.Instance.Is(GameState.Playing);

        if (running)
            wantOpen = false;

        if (root.activeSelf != wantOpen)
            root.SetActive(wantOpen);

        if (wantOpen)
            Refresh();
    }

    // ---------- Кнопка в главном меню ----------

    /// <summary>
    /// Клонирует существующую кнопку меню, чтобы попасть в стиль проекта без угадывания.
    /// Меню пересоздаётся при каждой перезагрузке сцены, поэтому проверяем каждый кадр —
    /// операция дешёвая, пока кнопка на месте.
    /// </summary>
    private void EnsureMenuButton()
    {
        if (menuButton != null)
            return;

        var menu = FindFirstObjectByType<StartMenu>(FindObjectsInactive.Include);
        if (menu == null || menu.startMenuCanvas == null)
            return;

        Button sample = PickSample(menu.startMenuCanvas);
        if (sample == null)
            return;

        var sampleRect = sample.GetComponent<RectTransform>();
        Transform row = sample.transform.parent;

        menuButton = Instantiate(sample.gameObject, row);
        menuButton.name = "CreateLobbyButton";

        // Ставим под самую нижнюю кнопку ряда, шагом как у остальных.
        var rect = menuButton.GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(sampleRect.anchoredPosition.x, LowestY(row) - MenuStep);
        rect.localScale = sampleRect.localScale;

        // Локализация переписала бы наш текст своим ключом на первом же обновлении.
        foreach (var behaviour in menuButton.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour != null && behaviour.GetType().Name.IndexOf("Localiz", System.StringComparison.OrdinalIgnoreCase) >= 0)
                Destroy(behaviour);
        }

        var label = menuButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = "Лобби";
            font = label.font;
        }

        var button = menuButton.GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OpenPanel);

        // Кнопка-образец могла зависеть от состояния игры — наш клон не должен гаснуть
        // по чужим правилам.
        var restricted = menuButton.GetComponent<StateRestrictedButton>();
        if (restricted != null)
            Destroy(restricted);

        // Ряд кнопок лежит не в корне канваса, и соседи ряда рисуются поверх него —
        // в меню это, например, подпись громкости, которая перехватывала клик.
        // Переносим кнопку в конец канваса, сохранив положение на экране: так она
        // оказывается выше всех по порядку отрисовки, а чужие элементы не тронуты.
        Transform canvasRoot = menu.startMenuCanvas.transform;
        menuButton.transform.SetParent(canvasRoot, true);
        menuButton.transform.SetAsLastSibling();

        if (!built)
            Build();
    }

    /// <summary>Основная кнопка меню как образец стиля; запасной вариант — любая в том же ряду.</summary>
    private static Button PickSample(GameObject menuCanvas)
    {
        Button fallback = null;

        foreach (var b in menuCanvas.GetComponentsInChildren<Button>(true))
        {
            if (b.name == "CreateLobbyButton")
                continue;

            if (b.name == "StartGame")
                return b;

            if (fallback == null && b.GetComponentInChildren<TMP_Text>(true) != null)
                fallback = b;
        }

        return fallback;
    }

    /// <summary>Нижняя граница ряда кнопок — чтобы встать под них, а не поверх.</summary>
    private static float LowestY(Transform row)
    {
        float lowest = float.MaxValue;

        for (int i = 0; i < row.childCount; i++)
        {
            var child = row.GetChild(i);
            if (child.GetComponent<Button>() == null)
                continue;

            var rt = child as RectTransform;
            if (rt != null && rt.anchoredPosition.y < lowest)
                lowest = rt.anchoredPosition.y;
        }

        return lowest == float.MaxValue ? 0f : lowest;
    }

    private void OpenPanel()
    {
        if (!built)
            Build();

        wantOpen = true;
        root.SetActive(true);
        Refresh();
    }

    // ---------- Сборка панели ----------

    private void Build()
    {
        var canvasGo = new GameObject("LobbyCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Затемнение и окно живут в общем контейнере: гасить надо оба разом. Иначе
        // затемнение остаётся включённым, перекрывает экран своим raycastTarget и
        // молча съедает клики по главному меню.
        root = Rect("LobbyRoot", canvasGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        GameObject shade = Rect("Shade", root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Fill(shade, new Color(0f, 0f, 0f, 0.55f));

        panel = Rect("LobbyPanel", root.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(1120f, 620f), Vector2.zero);
        Fill(panel, Brick).sprite = null;
        Outline(panel, BrickEdge, 4f);

        GameObject well = Rect("Well", panel.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 18f);
        Fill(well, Well);
        Outline(well, new Color32(0x1B, 0x16, 0x30, 0xFF), 3f);

        BuildBar(well.transform);
        BuildSeats(well.transform);
        BuildBody(well.transform);
        BuildStatus(well.transform);

        root.SetActive(false);
        built = true;
    }

    private void BuildBar(Transform parent)
    {
        GameObject bar = Rect("Bar", parent, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 54f), Vector2.zero);
        var barRect = bar.GetComponent<RectTransform>();
        barRect.offsetMin = new Vector2(14f, barRect.offsetMin.y);
        barRect.offsetMax = new Vector2(-14f, -14f);
        Fill(bar, Green);
        Outline(bar, GreenEdge, 3f);

        Label(bar.transform, "ЛОББИ", 28f, Bone, TextAlignmentOptions.MidlineLeft,
            new Vector2(0f, 0f), new Vector2(0.6f, 1f), new Vector2(18f, 0f), new Vector2(0f, 0f));

        barRight = Label(bar.transform, "Готовы 0 / 2", 22f, Bone, TextAlignmentOptions.MidlineRight,
            new Vector2(0.4f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(-18f, 0f));
    }

    private void BuildSeats(Transform parent)
    {
        for (int i = 0; i < 2; i++)
        {
            float x0 = i == 0 ? 0f : 0.5f;
            GameObject seat = Rect($"Seat{i}", parent, new Vector2(x0, 1f), new Vector2(x0 + 0.5f, 1f),
                new Vector2(0f, 86f), Vector2.zero);
            var r = seat.GetComponent<RectTransform>();
            r.offsetMin = new Vector2(i == 0 ? 14f : 7f, r.offsetMin.y);
            r.offsetMax = new Vector2(i == 0 ? -7f : -14f, -76f);

            Fill(seat, Slot);
            seatFrame[i] = Outline(seat, Line, 3f);

            GameObject pic = Rect("Pic", seat.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(58f, 58f), new Vector2(40f, 0f));
            Fill(pic, new Color32(0x5B, 0x64, 0x91, 0xFF));

            seatNick[i] = Label(seat.transform, "—", 20f, Bone, TextAlignmentOptions.BottomLeft,
                new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(78f, 0f), new Vector2(-10f, -12f));

            seatTags[i] = Label(seat.transform, "", 15f, Dim, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(78f, 12f), new Vector2(-10f, -2f));
        }
    }

    private void BuildBody(Transform parent)
    {
        // ── левая колонка: код и подсказка ──
        inLobbyGroup = Rect("InLobby", parent, new Vector2(0f, 0f), new Vector2(0.66f, 1f), Vector2.zero, Vector2.zero);
        var g = inLobbyGroup.GetComponent<RectTransform>();
        g.offsetMin = new Vector2(14f, 60f);
        g.offsetMax = new Vector2(-7f, -170f);

        Label(inLobbyGroup.transform, "КОД ЛОББИ", 15f, Dim, TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(2f, -22f), new Vector2(0f, 0f));

        GameObject codeWell = Rect("CodeWell", inLobbyGroup.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 96f), new Vector2(0f, -74f));
        Fill(codeWell, Slot);
        Outline(codeWell, Line, 3f);

        codeLabel = Label(codeWell.transform, "------", 52f, Bone, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(0f, 14f), Vector2.zero);
        codeLabel.characterSpacing = 18f;

        Label(codeWell.transform, "продиктуй сопернику", 15f, Dim, TextAlignmentOptions.Bottom,
            Vector2.zero, Vector2.one, new Vector2(0f, 8f), new Vector2(0f, -66f));

        hintLabel = Label(inLobbyGroup.transform,
            "Матч стартует сам, когда готовы оба. Дальше у каждого откроется магазин со стартовым золотом, "
            + "а волны пойдут по штатной кнопке «Готов» в игре.",
            17f, Dim, TextAlignmentOptions.TopLeft,
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(2f, 0f), new Vector2(-4f, -186f));

        // ── левая колонка, когда лобби ещё нет ──
        noLobbyGroup = Rect("NoLobby", parent, new Vector2(0f, 0f), new Vector2(0.66f, 1f), Vector2.zero, Vector2.zero);
        var n = noLobbyGroup.GetComponent<RectTransform>();
        n.offsetMin = new Vector2(14f, 60f);
        n.offsetMax = new Vector2(-7f, -170f);

        Label(noLobbyGroup.transform, "ВОЙТИ ПО КОДУ", 15f, Dim, TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(2f, -22f), new Vector2(0f, 0f));

        codeInput = BuildInput(noLobbyGroup.transform);

        Label(noLobbyGroup.transform,
            "Оба игрока должны быть на одной версии сборки — она входит в поиск лобби. "
            + "Разные версии друг друга не найдут.",
            17f, Dim, TextAlignmentOptions.TopLeft,
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(2f, 0f), new Vector2(-4f, -150f));

        // ── правая колонка: кнопки ──
        GameObject actions = Rect("Actions", parent, new Vector2(0.66f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        var a = actions.GetComponent<RectTransform>();
        a.offsetMin = new Vector2(7f, 60f);
        a.offsetMax = new Vector2(-14f, -170f);

        Label(actions.transform, "ДЕЙСТВИЯ", 15f, Dim, TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(2f, -22f), new Vector2(0f, 0f));

        float y = -34f;
        createButton = MakeButton(actions.transform, "СОЗДАТЬ ЛОББИ", Green, GreenEdge, ref y, out _);
        joinButton = MakeButton(actions.transform, "ВОЙТИ", Slate, SlateEdge, ref y, out _);
        readyButton = MakeButton(actions.transform, "ГОТОВ", Green, GreenEdge, ref y, out readyLabel);
        inviteButton = MakeButton(actions.transform, "ПРИГЛАСИТЬ ДРУГА", Slate, SlateEdge, ref y, out _);
        copyButton = MakeButton(actions.transform, "СКОПИРОВАТЬ КОД", Slate, SlateEdge, ref y, out _);
        leaveButton = MakeButton(actions.transform, "ЗАКРЫТЬ", Red, new Color32(0x67, 0x20, 0x0F, 0xFF), ref y, out _);

        createButton.onClick.AddListener(OnCreate);
        joinButton.onClick.AddListener(OnJoin);
        readyButton.onClick.AddListener(OnReady);
        inviteButton.onClick.AddListener(() => Lobbies.Service.InviteFriend());
        copyButton.onClick.AddListener(OnCopy);
        leaveButton.onClick.AddListener(OnLeave);
    }

    private void BuildStatus(Transform parent)
    {
        GameObject strip = Rect("Status", parent, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(0f, 36f), Vector2.zero);
        var r = strip.GetComponent<RectTransform>();
        r.offsetMin = new Vector2(14f, 14f);
        r.offsetMax = new Vector2(-14f, r.offsetMax.y);
        Fill(strip, new Color32(0x1B, 0x16, 0x30, 0xFF));
        Outline(strip, new Color32(0x12, 0x0E, 0x22, 0xFF), 2f);

        statusLabel = Label(strip.transform, "", 15f, Dim, TextAlignmentOptions.MidlineLeft,
            Vector2.zero, Vector2.one, new Vector2(14f, 0f), new Vector2(-14f, 0f));
    }

    // ---------- Обработчики ----------

    private void OnCreate()
    {
        SetStatus("Создаём лобби…");
        Lobbies.Service.Create(r => SetStatus(Describe(r)));
    }

    private void OnJoin()
    {
        SetStatus("Ищем лобби…");
        Lobbies.Service.JoinByCode(codeInput != null ? codeInput.text : string.Empty,
            r => SetStatus(Describe(r)));
    }

    private void OnReady()
    {
        if (DuelSession.Instance != null)
            DuelSession.Instance.ToggleReady();
    }

    private void OnCopy()
    {
        GUIUtility.systemCopyBuffer = Lobbies.Service.Code;
        SetStatus("Код скопирован в буфер обмена.");
    }

    private void OnLeave()
    {
        if (Lobbies.Service.IsInLobby)
        {
            Lobbies.Service.Leave();
            SetStatus("Вышли из лобби.");
            return;
        }

        wantOpen = false;
        root.SetActive(false);
    }

    // ---------- Обновление ----------

    private void Refresh()
    {
        var service = Lobbies.Service;
        bool inLobby = service.IsInLobby;

        inLobbyGroup.SetActive(inLobby);
        noLobbyGroup.SetActive(!inLobby);

        createButton.gameObject.SetActive(!inLobby);
        joinButton.gameObject.SetActive(!inLobby);
        readyButton.gameObject.SetActive(inLobby);
        inviteButton.gameObject.SetActive(inLobby);
        copyButton.gameObject.SetActive(inLobby);

        var leaveText = leaveButton.GetComponentInChildren<TMP_Text>(true);
        if (leaveText != null)
            leaveText.text = inLobby ? "ПОКИНУТЬ ЛОББИ" : "ЗАКРЫТЬ";

        codeLabel.text = string.IsNullOrEmpty(service.Code) ? "------" : service.Code;

        IReadOnlyList<LobbyMember> members = service.Members;
        int readyCount = 0;

        for (int i = 0; i < 2; i++)
        {
            if (i < members.Count)
            {
                LobbyMember m = members[i];
                bool ready = service.GetMemberValue(m.Id, DuelSession.KeyReady) == "1";
                if (ready)
                    readyCount++;

                seatNick[i].text = m.Name;
                seatNick[i].color = Bone;
                seatTags[i].text = (m.IsHost ? "<color=#E0A33A>ХОСТ</color>   " : "")
                    + (ready ? "<color=#63C74D>ГОТОВ</color>" : "ждём");
                seatFrame[i].color = ready ? Green : Line;
            }
            else
            {
                seatNick[i].text = inLobby ? "Свободно" : "—";
                seatNick[i].color = Dim;
                seatTags[i].text = inLobby ? "ждём второго игрока" : "";
                seatFrame[i].color = LineDim;
            }
        }

        barRight.text = $"Готовы {readyCount} / 2";

        if (readyLabel != null && DuelSession.Instance != null)
            readyLabel.text = DuelSession.Instance.IsReady ? "НЕ ГОТОВ" : "ГОТОВ";
    }

    private void SetStatus(string text)
    {
        if (statusLabel == null)
            return;

        string backend = Lobbies.IsSteamBacked
            ? "<color=#63C74D>●</color> Steam"
            : "<color=#E0A33A>●</color> Заглушка — игроки не соединятся";

        statusLabel.text = string.IsNullOrEmpty(text)
            ? $"{backend} · сборка {Application.version}"
            : $"{backend} · {text}";
    }

    private static string Describe(LobbyResult result)
    {
        switch (result)
        {
            case LobbyResult.Ok: return "готово";
            case LobbyResult.NoSteam: return "Steam недоступен";
            case LobbyResult.InvalidCode: return "код неверного формата — нужно 6 символов";
            case LobbyResult.NotFound: return "лобби с таким кодом нет (или версия сборки другая)";
            default: return "не удалось: лобби закрыто, заполнено или отказал Steam";
        }
    }

    // ---------- Мелкие строители ----------

    private static GameObject Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 size, Vector2 pos, float inset = 0f)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        if (inset > 0f)
        {
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        return go;
    }

    private static Image Fill(GameObject go, Color color)
    {
        var image = go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    /// <summary>Обводка отдельным объектом позади: Image в uGUI своей рамки не имеет.</summary>
    private static Image Outline(GameObject go, Color color, float width)
    {
        var frame = new GameObject("Frame", typeof(RectTransform), typeof(Image));
        var rt = frame.GetComponent<RectTransform>();
        rt.SetParent(go.transform, false);
        rt.SetAsFirstSibling();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-width, -width);
        rt.offsetMax = new Vector2(width, width);

        var image = frame.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private TMP_Text Label(Transform parent, string text, float size, Color color,
        TextAlignmentOptions align, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;

        var label = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
            label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = align;
        label.raycastTarget = false;
        label.richText = true;
        return label;
    }

    private Button MakeButton(Transform parent, string text, Color face, Color edge,
        ref float y, out TMP_Text label)
    {
        const float height = 56f;

        GameObject go = Rect("Button", parent, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, height), new Vector2(0f, y - height * 0.5f));
        Fill(go, face);
        Outline(go, edge, 3f);

        label = Label(go.transform, text, 20f, Bone, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var button = go.AddComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();

        var colors = button.colors;
        colors.highlightedColor = Color.Lerp(face, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(face, Color.black, 0.22f);
        colors.fadeDuration = 0.05f;
        button.colors = colors;

        y -= height + 12f;
        return button;
    }

    private TMP_InputField BuildInput(Transform parent)
    {
        GameObject go = Rect("CodeInput", parent, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0f, 62f), new Vector2(0f, -62f));
        Fill(go, Slot);
        Outline(go, LineDim, 3f);

        GameObject area = Rect("TextArea", go.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 12f);
        area.AddComponent<RectMask2D>();

        TMP_Text placeholder = Label(area.transform, "ВВЕДИ КОД", 24f, LineDim, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        TMP_Text textComponent = Label(area.transform, "", 24f, Bone, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        textComponent.raycastTarget = true;

        var input = go.AddComponent<TMP_InputField>();
        input.textViewport = area.GetComponent<RectTransform>();
        input.textComponent = (TextMeshProUGUI)textComponent;
        input.placeholder = placeholder;
        input.characterLimit = 8;
        input.targetGraphic = go.GetComponent<Image>();
        return input;
    }
}
