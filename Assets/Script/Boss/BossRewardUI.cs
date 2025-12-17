using System.Collections.Generic;
using UnityEngine;

public class BossRewardUI : MonoBehaviour
{
    [Header("Root")]
    public GameObject panelRoot;

    [Header("Provider (manual list like shop)")]
    public BossRewardProvider provider;

    [Header("Cards")]
    public BossRewardCardUI card1;
    public BossRewardCardUI card2;
    public BossRewardCardUI card3;

    private float prevTimeScale = 1f;

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    public void Open()
    {
        prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        if (panelRoot != null) panelRoot.SetActive(true);

        ShowRewards();
    }

    private void ShowRewards()
    {
        if (provider == null)
        {
            Debug.LogError("[BossRewardUI] provider is null");
            return;
        }

        List<BossRewardDefinition> picks = provider.PickThreeUnique();
        while (picks.Count < 3) picks.Add(null);

        if (card1 != null) card1.Bind(picks[0], OnChosen);
        if (card2 != null) card2.Bind(picks[1], OnChosen);
        if (card3 != null) card3.Bind(picks[2], OnChosen);
    }

    private void OnChosen(BossRewardDefinition reward)
    {
        // применяем апгрейд (через UpgradesManager, как у тебя уже в проекте)
        if (reward != null && reward.upgrade != null && UpgradesManager.Instance != null)
        {
            UpgradesManager.Instance.ApplyUpgrade(reward.upgrade);
        }

        Close();
    }

    public void Close()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        Time.timeScale = prevTimeScale;
    }
}
