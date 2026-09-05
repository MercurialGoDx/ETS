using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Логика окна лобби. Сама разметка собрана в MainScene из спрайтов проекта, сюда приходят
/// только ссылки — так дизайнер правит вид в инспекторе, а не в коде.
///
/// Окно закрывается, когда забег реально пошёл (Playing). Привязывать его к состоянию Menu
/// нельзя: игра выходит из меню ещё на фазе закупки, и окно захлопывалось бы само.
/// </summary>
public class LobbyPanelUI : MonoBehaviour
{
    [Header("Корни")]
    [Tooltip("Контейнер: затемнение и окно. Гасится целиком — иначе затемнение " +
             "остаётся поверх экрана и съедает клики по меню.")]
    [SerializeField] private GameObject root;

    [Tooltip("Кнопка в главном меню, открывающая окно.")]
    [SerializeField] private Button openButton;

    [Header("Шапка")]
    [SerializeField] private TMP_Text readyCounter;

    [Header("Игроки")]
    [SerializeField] private TMP_Text[] seatNick = new TMP_Text[2];
    [SerializeField] private TMP_Text[] seatTags = new TMP_Text[2];
    [SerializeField] private Image[] seatFrame = new Image[2];

    [Header("Группы состояний")]
    [SerializeField] private GameObject inLobbyGroup;
    [SerializeField] private GameObject noLobbyGroup;

    [Header("Код")]
    [SerializeField] private TMP_Text codeLabel;
    [SerializeField] private TMP_InputField codeInput;

    [Header("Кнопки")]
    [SerializeField] private Button createButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button readyButton;
    [SerializeField] private Button inviteButton;
    [SerializeField] private Button copyButton;
    [SerializeField] private Button leaveButton;
    [SerializeField] private TMP_Text readyLabel;
    [SerializeField] private TMP_Text leaveLabel;

    [Header("Статус")]
    [SerializeField] private TMP_Text statusLabel;

    private static readonly Color Line = new Color32(0xEC, 0xEA, 0xF6, 0xFF);
    private static readonly Color LineDim = new Color32(0x4C, 0x46, 0x66, 0xFF);
    private static readonly Color Green = new Color32(0x63, 0xC7, 0x4D, 0xFF);
    private static readonly Color Bone = new Color32(0xF2, 0xF0, 0xFA, 0xFF);
    private static readonly Color Dim = new Color32(0x9A, 0x93, 0xB8, 0xFF);

    private bool wantOpen;

    private void Awake()
    {
        if (openButton != null)
            openButton.onClick.AddListener(Open);

        if (createButton != null) createButton.onClick.AddListener(OnCreate);
        if (joinButton != null) joinButton.onClick.AddListener(OnJoin);
        if (readyButton != null) readyButton.onClick.AddListener(OnReady);
        if (inviteButton != null) inviteButton.onClick.AddListener(() => Lobbies.Service.InviteFriend());
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
        SetStatus("создаём лобби…");
        Lobbies.Service.Create(r => SetStatus(Describe(r)));
    }

    private void OnJoin()
    {
        SetStatus("ищем лобби…");
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
        SetStatus("код скопирован");
    }

    private void OnLeave()
    {
        if (Lobbies.Service.IsInLobby)
        {
            Lobbies.Service.Leave();
            SetStatus("вышли из лобби");
            return;
        }

        Close();
    }

    // ---------- Обновление ----------

    private void Refresh()
    {
        var service = Lobbies.Service;
        bool inLobby = service.IsInLobby;

        if (inLobbyGroup != null) inLobbyGroup.SetActive(inLobby);
        if (noLobbyGroup != null) noLobbyGroup.SetActive(!inLobby);

        if (createButton != null) createButton.gameObject.SetActive(!inLobby);
        if (joinButton != null) joinButton.gameObject.SetActive(!inLobby);
        if (readyButton != null) readyButton.gameObject.SetActive(inLobby);
        if (inviteButton != null) inviteButton.gameObject.SetActive(inLobby);
        if (copyButton != null) copyButton.gameObject.SetActive(inLobby);

        if (leaveLabel != null)
            leaveLabel.text = inLobby ? "ПОКИНУТЬ ЛОББИ" : "ЗАКРЫТЬ";

        if (codeLabel != null)
            codeLabel.text = string.IsNullOrEmpty(service.Code) ? "------" : service.Code;

        IReadOnlyList<LobbyMember> members = service.Members;
        int readyCount = 0;

        for (int i = 0; i < 2; i++)
        {
            bool filled = i < members.Count;
            bool ready = false;

            if (filled)
            {
                ready = service.GetMemberValue(members[i].Id, DuelSession.KeyReady) == "1";
                if (ready)
                    readyCount++;
            }

            if (seatNick[i] != null)
            {
                seatNick[i].text = filled ? members[i].Name : (inLobby ? "Свободно" : "—");
                seatNick[i].color = filled ? Bone : Dim;
            }

            if (seatTags[i] != null)
            {
                seatTags[i].text = filled
                    ? (members[i].IsHost ? "<color=#E0A33A>ХОСТ</color>   " : "")
                        + (ready ? "<color=#63C74D>ГОТОВ</color>" : "ждём")
                    : (inLobby ? "ждём второго игрока" : "");
            }

            if (seatFrame[i] != null)
                seatFrame[i].color = filled ? (ready ? Green : Line) : LineDim;
        }

        if (readyCounter != null)
            readyCounter.text = $"Готовы {readyCount} / 2";

        if (readyLabel != null && DuelSession.Instance != null)
            readyLabel.text = DuelSession.Instance.IsReady ? "НЕ ГОТОВ" : "ГОТОВ";

        SetStatus(null);
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
}
