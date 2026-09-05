using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Окно лобби. Разметка собрана в MainScene из спрайтов проекта, сюда приходят только ссылки.
///
/// Окно одно и постоянное: элементы не появляются и не исчезают. Недоступное сейчас гасится
/// штатным <see cref="Selectable.interactable"/> с настроенным disabledColor, а подпись под
/// кнопкой объясняет причину. Прыгающая раскладка хуже, чем погашенная кнопка: игрок должен
/// видеть весь набор возможностей сразу.
///
/// Закрывается только когда забег реально пошёл (Playing). Привязка к состоянию Menu была бы
/// слишком жёсткой — игра выходит из меню ещё на фазе закупки.
/// </summary>
public class LobbyPanelUI : MonoBehaviour
{
    [Header("Корни")]
    [Tooltip("Затемнение и окно вместе. Гасится целиком, иначе затемнение остаётся " +
             "поверх экрана и съедает клики по меню.")]
    [SerializeField] private GameObject root;

    [Tooltip("Кнопка в главном меню, открывающая окно.")]
    [SerializeField] private Button openButton;

    [Header("Шапка")]
    [SerializeField] private Image barImage;
    [SerializeField] private TMP_Text barRight;

    [Header("Места игроков")]
    [SerializeField] private Image[] seatFrame = new Image[2];
    [SerializeField] private TMP_Text[] seatNick = new TMP_Text[2];
    [SerializeField] private TMP_Text[] seatTags = new TMP_Text[2];
    [SerializeField] private GameObject[] seatAvatar = new GameObject[2];
    [SerializeField] private GameObject[] seatLock = new GameObject[2];

    [Header("Код лобби")]
    [SerializeField] private Image codeFrame;
    [SerializeField] private TMP_Text codeLabel;
    [SerializeField] private TMP_Text codeSub;
    [SerializeField] private Button copyButton;

    [Header("Вход по коду")]
    [SerializeField] private TMP_InputField codeInput;
    [SerializeField] private Image codeInputFrame;
    [SerializeField] private Button joinButton;
    [SerializeField] private TMP_Text noteLabel;

    [Header("Действия")]
    [SerializeField] private Button createButton;
    [SerializeField] private Button readyButton;
    [SerializeField] private Button inviteButton;
    [SerializeField] private Button leaveButton;
    [SerializeField] private TMP_Text createSub;
    [SerializeField] private TMP_Text readyMain;
    [SerializeField] private TMP_Text readySub;
    [SerializeField] private TMP_Text leaveMain;

    [Header("Статус")]
    [SerializeField] private TMP_Text statusLabel;

    private static readonly Color FrameOn = new Color32(0xEC, 0xEA, 0xF6, 0xFF);
    private static readonly Color FrameOff = new Color32(0x4C, 0x46, 0x66, 0xFF);
    private static readonly Color FrameReady = new Color32(0x63, 0xC7, 0x4D, 0xFF);
    private static readonly Color BarLive = new Color32(0x63, 0xC7, 0x4D, 0xFF);
    private static readonly Color BarIdle = new Color32(0x4E, 0x4A, 0x74, 0xFF);
    private static readonly Color TextOn = new Color32(0xF2, 0xF0, 0xFA, 0xFF);
    private static readonly Color TextOff = new Color32(0x4C, 0x46, 0x66, 0xFF);
    private static readonly Color TextDim = new Color32(0x9A, 0x93, 0xB8, 0xFF);

    private const string NoteOutside =
        "Оба игрока должны быть на одной версии сборки — она входит в поиск лобби. " +
        "Разные версии друг друга не найдут.";

    private const string NoteInside =
        "Матч стартует сам, когда готовы оба. Дальше у каждого откроется магазин " +
        "со стартовым золотом, а волны пойдут по штатной кнопке «Готов» в игре.";

    private bool wantOpen;
    private string pendingStatus;

    private void Awake()
    {
        if (openButton != null) openButton.onClick.AddListener(Open);
        if (createButton != null) createButton.onClick.AddListener(OnCreate);
        if (joinButton != null) joinButton.onClick.AddListener(OnJoin);
        if (readyButton != null) readyButton.onClick.AddListener(OnReady);
        if (inviteButton != null) inviteButton.onClick.AddListener(OnInvite);
        if (copyButton != null) copyButton.onClick.AddListener(OnCopy);
        if (leaveButton != null) leaveButton.onClick.AddListener(OnLeave);

        if (root != null)
            root.SetActive(false);
    }

    private void Update()
    {
        if (root == null)
            return;

        bool running = GameStateManager.Instance != null
            && GameStateManager.Instance.Is(GameState.Playing);

        if (running)
            wantOpen = false;

        if (root.activeSelf != wantOpen)
            root.SetActive(wantOpen);

        if (wantOpen)
            Refresh();
    }

    public void Open()
    {
        wantOpen = true;
        pendingStatus = null;
        root.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        wantOpen = false;
        root.SetActive(false);
    }

    // ---------- Действия ----------

    private void OnCreate()
    {
        pendingStatus = "создаём лобби…";
        Lobbies.Service.Create(r => pendingStatus = Describe(r));
    }

    private void OnJoin()
    {
        pendingStatus = "ищем лобби…";
        Lobbies.Service.JoinByCode(codeInput != null ? codeInput.text : string.Empty,
            r => pendingStatus = Describe(r));
    }

    private void OnReady()
    {
        if (DuelSession.Instance != null)
            DuelSession.Instance.ToggleReady();
    }

    private void OnInvite()
    {
        Lobbies.Service.InviteFriend();
        pendingStatus = "открыт оверлей Steam";
    }

    private void OnCopy()
    {
        GUIUtility.systemCopyBuffer = Lobbies.Service.Code;
        pendingStatus = "код скопирован";
    }

    private void OnLeave()
    {
        if (Lobbies.Service.IsInLobby)
        {
            Lobbies.Service.Leave();
            pendingStatus = "вышли из лобби";
            return;
        }

        Close();
    }

    // ---------- Обновление ----------

    private void Refresh()
    {
        var service = Lobbies.Service;
        bool inLobby = service.IsInLobby;
        IReadOnlyList<LobbyMember> members = service.Members;

        int readyCount = 0;
        for (int i = 0; i < 2; i++)
        {
            bool filled = i < members.Count;
            bool ready = filled && service.GetMemberValue(members[i].Id, DuelSession.KeyReady) == "1";
            if (ready)
                readyCount++;

            if (seatAvatar[i] != null) seatAvatar[i].SetActive(filled);
            if (seatLock[i] != null) seatLock[i].SetActive(!filled);

            if (seatNick[i] != null)
            {
                seatNick[i].text = filled ? members[i].Name : "Свободно";
                seatNick[i].color = filled ? TextOn : TextDim;
            }

            if (seatTags[i] != null)
            {
                seatTags[i].text = filled
                    ? (members[i].IsHost ? "<color=#E0A33A>ХОСТ</color>   " : "")
                        + (ready ? "<color=#63C74D>ГОТОВ</color>" : "ждём")
                    : (i == 0 ? "место хоста" : "место соперника");
            }

            if (seatFrame[i] != null)
                seatFrame[i].color = !filled ? FrameOff : (ready ? FrameReady : FrameOn);
        }

        // Шапка: живая только когда есть с кем считать готовность.
        // Во время отсчёта она перехватывается — это единственное, что сейчас важно.
        float countdown = DuelSession.Instance != null ? DuelSession.Instance.CountdownLeft : -1f;
        bool counting = countdown >= 0f;

        if (barImage != null)
            barImage.color = (inLobby || counting) ? BarLive : BarIdle;
        if (barRight != null)
        {
            barRight.text = counting
                ? $"Старт через {Mathf.CeilToInt(countdown)}…"
                : (inLobby ? $"Готовы {readyCount} / 2" : "Дуэль на двоих");
        }

        // Код: сам блок остаётся на месте, просто гаснет.
        if (codeLabel != null)
        {
            codeLabel.text = inLobby ? service.Code : "— — — — — —";
            codeLabel.color = inLobby ? TextOn : TextOff;
        }
        if (codeSub != null)
            codeSub.text = inLobby ? "продиктуй сопернику" : "появится после создания";
        if (codeFrame != null)
            codeFrame.color = inLobby ? FrameOn : FrameOff;
        SetInteractable(copyButton, inLobby);

        // Вход по коду доступен только снаружи.
        if (codeInput != null)
        {
            codeInput.interactable = !inLobby;
            if (inLobby)
                codeInput.text = string.Empty;
        }
        if (codeInputFrame != null)
            codeInputFrame.color = inLobby ? FrameOff : FrameOn;
        SetInteractable(joinButton, !inLobby);

        SetInteractable(createButton, !inLobby);
        SetInteractable(readyButton, inLobby);
        SetInteractable(inviteButton, inLobby);

        if (createSub != null)
            createSub.text = inLobby ? "вы уже в лобби" : "код сгенерируется сам";

        bool isReady = DuelSession.Instance != null && DuelSession.Instance.IsReady;
        if (readyMain != null)
            readyMain.text = isReady ? "НЕ ГОТОВ" : "ГОТОВ";
        if (readySub != null)
            readySub.text = inLobby ? "F2" : "сначала войдите в лобби";

        if (leaveMain != null)
            leaveMain.text = inLobby ? "ПОКИНУТЬ ЛОББИ" : "ЗАКРЫТЬ";

        if (noteLabel != null)
            noteLabel.text = inLobby ? NoteInside : NoteOutside;

        UpdateStatus();
    }

    /// <summary>Гасим штатно: disabledColor настроен на кнопке, руками цвет не трогаем.</summary>
    private static void SetInteractable(Selectable target, bool value)
    {
        if (target != null && target.interactable != value)
            target.interactable = value;
    }

    private void UpdateStatus()
    {
        if (statusLabel == null)
            return;

        string backend = Lobbies.IsSteamBacked
            ? "<color=#63C74D>●</color> Steam"
            : "<color=#E0A33A>●</color> Заглушка — игроки не соединятся";

        statusLabel.text = string.IsNullOrEmpty(pendingStatus)
            ? $"{backend} · сборка {Application.version}"
            : $"{backend} · {pendingStatus}";
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
}
