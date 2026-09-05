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

    private float prevTimeScale = 1f;
    private int remainingSelections;

    // Номер босса и порядковый номер выбора — вместе адресуют бросок наград в дуэли.
    private int currentBossOrdinal;
    private int currentSelectionIndex;
    private bool isResolvingSelection;
    private CanvasGroup panelCanvasGroup;

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
        if (gameSpeedPanel != null) gameSpeedPanel.SetActive(false);

        prevTimeScale = Time.timeScale;

        // В дуэли время не останавливаем: у игроков боссы умирают в разные моменты,
        // и пауза на выбор досталась бы им несимметрично.
        if (!DuelSession.IsSeeded)
            Time.timeScale = 0f;

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

        List<UpgradeBaseSO> picks = provider.PickThreeUnique(currentBossOrdinal, currentSelectionIndex);
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
        UpdateRerollUI();

        Debug.Log($"[BossRewardUI] Next selection opened; remaining={remainingSelections}.");
    }

    public void Close()
    {
        StopAllCoroutines();
        remainingSelections = 0;
        isResolvingSelection = false;
        IsSelectionOpen = false;

        if (panelRoot != null) panelRoot.SetActive(false);
        if (!DuelSession.IsSeeded)
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
