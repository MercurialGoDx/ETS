using System.Collections;
using System.Collections.Generic;
using TMPro;
using ETS.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

public class BossRewardUI : MonoBehaviour
{
    public GameObject gameSpeedPanel;

    [Header("Root")]
    public GameObject panelRoot;

    [Header("Provider (manual list like shop)")]
    public BossRewardProvider provider;

    [Header("Cards")]
    public BossRewardCardUI card1;
    public BossRewardCardUI card2;
    public BossRewardCardUI card3;

    [Header("Boss Reward Rerolls")]
    [SerializeField] private Button rerollButton;
    [SerializeField] private TMP_Text rerollButtonText;
    [SerializeField] private TMP_Text remainingRerollsText;
    [SerializeField] private string rerollButtonLabel = "Обновить";
    [SerializeField] private string remainingRerollsFormat = "Осталось обновлений: {0}";

    [Header("Multiple Selections")]
    [SerializeField, Min(0f)] private float selectionTransitionDelay = 0.08f;

    [Header("Таймер выбора (только дуэль)")]
    [Tooltip("Сколько секунд даётся на выбор награды в дуэли. Не успел — награда берётся сама.")]
    [SerializeField, Min(1f)] private float duelSelectionSeconds = 10f;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private string timerFormat = "Выбор: {0} с";

    private float prevTimeScale = 1f;
    private int remainingSelections;

    // Номер босса и порядковый номер выбора — вместе адресуют бросок наград в дуэли.
    private int currentBossOrdinal;
    private int currentSelectionIndex;
    private int currentRerollIndex;
    private bool isResolvingSelection;
    private CanvasGroup panelCanvasGroup;

    // Отрицательное — таймера нет. Считаем в нескалированном времени: на выборе стоит пауза.
    private float selectionTimeLeft = -1f;

    /// <summary>
    /// True, пока открыт экран выбора награды с босса. Пока он открыт, магазин
    /// (покупки и реролл) заблокирован — см. ShopManager.
    /// </summary>
    public static bool IsSelectionOpen { get; private set; }

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);

        if (rerollButton != null)
        {
            rerollButton.onClick.RemoveListener(RerollRewards);
            rerollButton.onClick.AddListener(RerollRewards);
        }

        UpdateRerollUI();
    }

    private void OnDestroy()
    {
        if (rerollButton != null)
            rerollButton.onClick.RemoveListener(RerollRewards);
    }

    private void OnDisable()
    {
        // Страховка: если объект выключили/сцену выгрузили с открытым экраном.
        IsSelectionOpen = false;
    }

    public void Open(int selectionCount = 1, int bossOrdinal = 0)
    {
        currentBossOrdinal = bossOrdinal;
        currentSelectionIndex = 0;
        currentRerollIndex = 0;
        if (gameSpeedPanel != null) gameSpeedPanel.SetActive(false);

        // Запоминаем темп только на первом открытии: повторный Open поверх уже открытого
        // экрана записал бы сюда ноль, и после закрытия игра осталась бы стоять навсегда.
        if (!IsSelectionOpen)
            prevTimeScale = Time.timeScale;

        Time.timeScale = 0f;

        // В дуэли выбор ограничен по времени: пауза одинаковая у обоих, потому что дольше
        // отведённого продумать нельзя — по истечении награда берётся сама.
        StartSelectionTimer();

        remainingSelections = Mathf.Max(1, selectionCount);
        isResolvingSelection = false;
        IsSelectionOpen = true;

        if (panelRoot != null) panelRoot.SetActive(true);
        SetPanelVisible(true);

        ShowRewards();
        UpdateRerollUI();
    }

    private void ShowRewards()
    {
        if (provider == null)
        {
            Debug.LogError("[BossRewardUI] provider is null");
            return;
        }

        List<UpgradeBaseSO> picks = provider.PickThreeUnique(
            currentBossOrdinal, currentSelectionIndex, currentRerollIndex);
        while (picks.Count < 3) picks.Add(null);

        if (card1 != null) card1.Bind(picks[0], OnChosen);
        if (card2 != null) card2.Bind(picks[1], OnChosen);
        if (card3 != null) card3.Bind(picks[2], OnChosen);
    }

    private void OnChosen(UpgradeBaseSO reward)
    {
        if (isResolvingSelection)
            return;

        isResolvingSelection = true;

        // применяем апгрейд (через UpgradesManager, как у тебя уже в проекте)
        if (reward != null && UpgradesManager.Instance != null)
        {
            UpgradesManager.Instance.ApplyBossReward(reward);
        }

        remainingSelections--;
        currentSelectionIndex++;
        currentRerollIndex = 0;
        if (remainingSelections > 0)
        {
            StartCoroutine(ShowNextSelection());
            return;
        }

        Close();
    }

    /// <summary>
    /// Тратит заряд и разыгрывает новую уникальную тройку. Прошлая тройка не
    /// исключается, поэтому отдельная награда может снова выпасть после реролла.
    /// </summary>
    public void RerollRewards()
    {
        if (!IsSelectionOpen || isResolvingSelection)
            return;

        UpgradesRuntimeData runtime = UpgradesManager.Instance?.GameplayRuntimeData;
        if (runtime == null || !runtime.TryConsumeBossRewardReroll())
        {
            UpdateRerollUI();
            return;
        }

        isResolvingSelection = true;
        currentRerollIndex++;
        UpdateRerollUI();
        StartCoroutine(ShowRerolledRewards());
    }

    private IEnumerator ShowRerolledRewards()
    {
        SetPanelVisible(false);

        if (selectionTransitionDelay > 0f)
            yield return new WaitForSecondsRealtime(selectionTransitionDelay);
        else
            yield return null;

        ShowRewards();
        SetPanelVisible(true);
        isResolvingSelection = false;
        UpdateRerollUI();

        Debug.Log($"[BossRewardUI] Rewards rerolled; remaining={GetRemainingRerolls()}.");
    }

    private IEnumerator ShowNextSelection()
    {
        SetPanelVisible(false);

        if (selectionTransitionDelay > 0f)
            yield return new WaitForSecondsRealtime(selectionTransitionDelay);
        else
            yield return null;

        ShowRewards();
        SetPanelVisible(true);
        isResolvingSelection = false;
        StartSelectionTimer();
        UpdateRerollUI();

        Debug.Log($"[BossRewardUI] Next selection opened; remaining={remainingSelections}.");
    }

    /// <summary>
    /// Заводит отсчёт на очередной выбор. Видимостью подписи управляем здесь, а не в Update:
    /// компонент висит на самом panelRoot, и с закрытым экраном Update уже не вызывается —
    /// подпись так и осталась бы включённой до следующего кадра открытия.
    /// </summary>
    private void StartSelectionTimer()
    {
        selectionTimeLeft = DuelSession.IsSeeded ? duelSelectionSeconds : -1f;

        if (timerText == null)
            return;

        timerText.gameObject.SetActive(selectionTimeLeft > 0f);
        if (selectionTimeLeft > 0f)
            timerText.text = string.Format(timerFormat, Mathf.CeilToInt(selectionTimeLeft));
    }

    private void Update()
    {
        if (selectionTimeLeft < 0f)
            return;

        // Время на выборе остановлено, поэтому только нескалированное.
        selectionTimeLeft -= Time.unscaledDeltaTime;

        if (timerText != null)
            timerText.text = string.Format(timerFormat, Mathf.CeilToInt(Mathf.Max(0f, selectionTimeLeft)));

        if (selectionTimeLeft > 0f)
            return;

        selectionTimeLeft = -1f;
        AutoPick();
    }

    /// <summary>
    /// Время вышло — награду берём за игрока. Бросок сидированный: карточки у обоих одни и те
    /// же, поэтому и доставшаяся по таймауту награда у них совпадёт, а матч не разъедется
    /// из-за того, что кто-то отвлёкся.
    /// </summary>
    private void AutoPick()
    {
        if (!IsSelectionOpen || isResolvingSelection)
            return;

        var cards = new[] { card1, card2, card3 };
        var available = new List<UpgradeBaseSO>(3);
        foreach (var c in cards)
        {
            if (c != null && c.Reward != null)
                available.Add(c.Reward);
        }

        if (available.Count == 0)
        {
            Debug.LogWarning("[BossRewardUI] Время вышло, но выбирать не из чего — закрываю экран.");
            Close();
            return;
        }

        int draw = DuelRandom.Compose(
            DuelRandom.Compose(currentBossOrdinal, currentSelectionIndex), currentRerollIndex);
        int index = DuelRandom.Range(DuelSession.Seed, DuelStream.BossReward, AutoPickSalt + draw,
            0, available.Count);

        Debug.Log($"[BossRewardUI] Время вышло — награда выбрана автоматически: {available[index].name}.");
        OnChosen(available[index]);
    }

    /// <summary>
    /// Сдвиг, чтобы автовыбор не тянул то же число, каким разыгрывались сами карточки:
    /// иначе он был бы жёстко привязан к первой карточке в списке.
    /// </summary>
    private const int AutoPickSalt = 9001;

    public void Close()
    {
        StopAllCoroutines();
        remainingSelections = 0;
        isResolvingSelection = false;
        IsSelectionOpen = false;

        selectionTimeLeft = -1f;
        if (timerText != null) timerText.gameObject.SetActive(false);

        if (panelRoot != null) panelRoot.SetActive(false);
        Time.timeScale = prevTimeScale;
        if (gameSpeedPanel != null) gameSpeedPanel.SetActive(true);
    }

    private int GetRemainingRerolls()
    {
        UpgradesRuntimeData runtime = UpgradesManager.Instance?.GameplayRuntimeData;
        return runtime != null ? runtime.BossRewardRerolls : 0;
    }

    private void UpdateRerollUI()
    {
        int remaining = GetRemainingRerolls();

        if (rerollButtonText != null)
            rerollButtonText.text = rerollButtonLabel;

        if (remainingRerollsText != null)
            remainingRerollsText.text = string.Format(remainingRerollsFormat, remaining);

        if (rerollButton != null)
            rerollButton.interactable = IsSelectionOpen && !isResolvingSelection && remaining > 0;
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelRoot == null)
            return;

        if (panelCanvasGroup == null)
        {
            panelCanvasGroup = panelRoot.GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null)
                panelCanvasGroup = panelRoot.AddComponent<CanvasGroup>();
        }

        panelCanvasGroup.alpha = visible ? 1f : 0f;
        panelCanvasGroup.interactable = visible;
        panelCanvasGroup.blocksRaycasts = visible;
    }
}
