using UnityEngine;

[CreateAssetMenu(
    menuName = "Upgrades/Boss Reward/Boss Reward Gold",
    fileName = "Boss Reward Gold")]
public sealed class BossRewardGoldUpgrade : UpgradeBaseSO
{
    [Header("Boss Reward Gold")]
    [Tooltip("Количество золота за первого побеждённого босса.")]
    [Min(0)] public int baseGold = 500;

    [Tooltip("Сколько золота добавляется за каждого следующего босса.")]
    [Min(0)] public int goldIncreasePerBoss = 250;

    public override void Apply(UpgradeContextSO context)
    {
        GoldManager goldManager = context != null && context.goldManager != null
            ? context.goldManager
            : GoldManager.Instance;

        if (goldManager == null)
        {
            Debug.LogWarning($"{name}: GoldManager is not available.");
            return;
        }

        int bossNumber = GetCurrentBossNumber();
        int amount = GetGoldForBossNumber(bossNumber);
        if (amount <= 0)
            return;

        // Boss reward is a fixed payout: its displayed amount must match the amount
        // actually received, independently of global gold-gain bonuses.
        goldManager.AddGold(amount, GoldSource.Fixed);
        Debug.Log($"[BossRewardGold] Boss #{bossNumber}: granted {amount} gold.");
    }

    public int GetGoldForBossNumber(int bossNumber)
    {
        long safeBaseGold = Mathf.Max(0, baseGold);
        long safeIncrease = Mathf.Max(0, goldIncreasePerBoss);
        long amount = safeBaseGold + safeIncrease * Mathf.Max(0, bossNumber - 1);
        return amount >= int.MaxValue ? int.MaxValue : (int)amount;
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { GetGoldForBossNumber(GetCurrentBossNumber()) };
    }

    private static int GetCurrentBossNumber()
    {
        BossManager bossManager = FindFirstObjectByType<BossManager>();
        return bossManager != null
            ? Mathf.Max(1, bossManager.DefeatedBossCount)
            : 1;
    }
}
