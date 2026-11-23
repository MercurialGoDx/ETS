using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Экран победы: соперник выбыл, а мы ещё играем. Предлагает доиграть забег в одиночку
/// или уйти в меню.
///
/// Живёт в сцене, а не в <see cref="DuelSession"/>: тот создаёт себя сам и переживает
/// перезагрузку сцены, ссылок на UI у него быть не может.
/// </summary>
public class DuelVictoryPanel : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text detailText;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button menuButton;

    [SerializeField] private string title = "Победа";
    [SerializeField] private string detailFormat = "Соперник выбыл: волна {0}, {1}";
    [SerializeField] private string detailFallback = "Соперник выбыл";

    private bool isOpen;

    private void Awake()
    {
        if (root != null)
            root.SetActive(false);

        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinue);

        if (menuButton != null)
            menuButton.onClick.AddListener(OnMenu);
    }

    private void OnEnable()
    {
        Subscribe(true);
    }

    private void OnDisable()
    {
        Subscribe(false);
    }

    private void OnDestroy()
    {
        if (continueButton != null)
            continueButton.onClick.RemoveListener(OnContinue);

        if (menuButton != null)
            menuButton.onClick.RemoveListener(OnMenu);
    }

    private void Subscribe(bool on)
    {
        var duel = DuelSession.Instance;
        if (duel == null)
            return;

        duel.OpponentLost -= Show;
        if (on)
            duel.OpponentLost += Show;
    }

    // Подписка в OnEnable может не застать сессию: она поднимается своим бутстрапом.
    private void Update()
    {
        if (!isOpen)
            Subscribe(true);
    }

    private void Show()
    {
        if (isOpen)
            return;

        isOpen = true;

        var duel = DuelSession.Instance;
        if (titleText != null)
            titleText.text = title;

        if (detailText != null)
        {
            DuelSession.OpponentStat stat = duel != null
                ? duel.GetOpponentStat()
                : default;

            detailText.text = stat.HasData
                ? string.Format(detailFormat, stat.Wave, FormatTime(stat.Time))
                : detailFallback;
        }

        if (root != null)
            root.SetActive(true);

        // Пока игрок решает, забег стоит: соперника уже нет, торопить некому.
        var gsm = GameStateManager.Instance;
        if (gsm != null && gsm.Is(GameState.Playing))
            gsm.SetState(GameState.Paused);
    }

    private void Close()
    {
        isOpen = false;

        if (root != null)
            root.SetActive(false);
    }

    private void OnContinue()
    {
        Close();

        var gsm = GameStateManager.Instance;
        if (gsm != null && gsm.Is(GameState.Paused))
            gsm.SetState(GameState.Playing);

        DuelSession.Instance?.ContinueSolo();
    }

    private void OnMenu()
    {
        Close();
        DuelSession.Instance?.LeaveToMenu();
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{total / 60:00}:{total % 60:00}";
    }
}
