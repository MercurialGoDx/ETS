using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    [Header("Multiple Selections")]
    [SerializeField, Min(0f)] private float selectionTransitionDelay = 0.08f;

    private float prevTimeScale = 1f;
    private int remainingSelections;
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
    }

    private void OnDisable()
    {
        // Страховка: если объект выключили/сцену выгрузили с открытым экраном.
        IsSelectionOpen = false;
    }

    public void Open(int selectionCount = 1)
    {
        if (gameSpeedPanel != null) gameSpeedPanel.SetActive(false);

        prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        remainingSelections = Mathf.Max(1, selectionCount);
        isResolvingSelection = false;
        IsSelectionOpen = true;

        if (panelRoot != null) panelRoot.SetActive(true);
        SetPanelVisible(true);

        ShowRewards();
    }

    private void ShowRewards()
    {
        if (provider == null)
        {
            Debug.LogError("[BossRewardUI] provider is null");
            return;
        }

        List<UpgradeBaseSO> picks = provider.PickThreeUnique();
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
        if (remainingSelections > 0)
        {
            StartCoroutine(ShowNextSelection());
            return;
        }

        Close();
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

        Debug.Log($"[BossRewardUI] Next selection opened; remaining={remainingSelections}.");
    }

    public void Close()
    {
        StopAllCoroutines();
        remainingSelections = 0;
        isResolvingSelection = false;
        IsSelectionOpen = false;

        if (panelRoot != null) panelRoot.SetActive(false);
        Time.timeScale = prevTimeScale;
        if (gameSpeedPanel != null) gameSpeedPanel.SetActive(true);
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
