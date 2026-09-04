using UnityEngine;

[CreateAssetMenu(
    menuName = "Upgrades/Utility/Boss Reward Reroll",
    fileName = "Boss Reward Reroll")]
public sealed class BossRewardRerollUpgrade : UpgradeBaseSO
{
    [Header("Boss Reward Rerolls")]
    [Tooltip("Сколько бесплатных обновлений наград босса даёт одна покупка.")]
    [Min(1)] public int rerollsPerPurchase = 1;

    [Tooltip("На сколько золота дорожает каждая следующая покупка.")]
    [Min(0)] public int priceIncreasePerPurchase = 1000;

    public override int GetCurrentPrice(UpgradeContextSO context)
    {
        int purchases = UpgradesManager.Instance?.RuntimeData != null
            ? UpgradesManager.Instance.RuntimeData.GetUpgradePurchaseCount(this)
            : 0;

        return GetPriceForPurchaseCount(purchases);
    }

    public int GetPriceForPurchaseCount(int purchases)
    {
        long currentPrice = (long)Mathf.Max(0, price)
            + (long)Mathf.Max(0, priceIncreasePerPurchase) * Mathf.Max(0, purchases);
        return currentPrice >= int.MaxValue ? int.MaxValue : (int)currentPrice;
    }

    public override void Apply(UpgradeContextSO context)
    {
        if (context?.runtime == null)
        {
            Debug.LogWarning($"{name}: runtime data is not available.");
            return;
        }

        int amount = Mathf.Max(1, rerollsPerPurchase);
        context.runtime.AddBossRewardRerolls(amount);
        Debug.Log(
            $"[BossRewardReroll] Added {amount}; " +
            $"available={context.runtime.BossRewardRerolls}.");
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { rerollsPerPurchase, priceIncreasePerPurchase };
    }
}
