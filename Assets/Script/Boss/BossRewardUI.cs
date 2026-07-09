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

    private float prevTimeScale = 1f;

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

    public void Open()
    {
        gameSpeedPanel.SetActive(false);

        prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        IsSelectionOpen = true;

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

        List<UpgradeBaseSO> picks = provider.PickThreeUnique();
        while (picks.Count < 3) picks.Add(null);

        if (card1 != null) card1.Bind(picks[0], OnChosen);
        if (card2 != null) card2.Bind(picks[1], OnChosen);
        if (card3 != null) card3.Bind(picks[2], OnChosen);
    }

    private void OnChosen(UpgradeBaseSO reward)
    {
        // применяем апгрейд (через UpgradesManager, как у тебя уже в проекте)
        if (reward != null && UpgradesManager.Instance != null)
        {
            UpgradesManager.Instance.ApplyUpgrade(reward);
        }

        Close();
    }

    public void Close()
    {
        IsSelectionOpen = false;

        if (panelRoot != null) panelRoot.SetActive(false);
        Time.timeScale = prevTimeScale;
        gameSpeedPanel.SetActive(true);
    }
}
