using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Gold/Gold For Life Upgrade")]
public class GoldForLifeUpgrade : UpgradeBaseSO
{
    public float lifeLoseValue;
    public int basicGold = 200;
    public int multGold = 5;

    private const float MinAllowedMaxHealthAfterPurchase = 0f;

    /// <summary>
    /// Нельзя купить, если после покупки макс. HP упадёт до 0 или ниже —
    /// магазин заблокирует покупку ещё до списания цены.
    /// </summary>
    public override bool CanApply(UpgradeContextSO context)
    {
        if (context == null || context.playerHealth == null) return false;
        return context.playerHealth.MaxHealth - lifeLoseValue > MinAllowedMaxHealthAfterPurchase;
    }

    public override void Apply(UpgradeContextSO context)
    {
        if (context == null || context.playerHealth == null) return;

        float maxHp = context.playerHealth.MaxHealth;

        // Запрет: если после покупки maxHP станет 50 или меньше
        if (maxHp - lifeLoseValue <= MinAllowedMaxHealthAfterPurchase)
        {
            return;
        }

        context.playerHealth.AddFlatMaxHealth(-lifeLoseValue);

        context.runtime.goldUpgradeCount++;

        // GoldSource.Fixed: эта выдача — ровно объявленное число (напр. Sacrifice: 200
        // золота), бонусы % к получаемому золоту на неё намеренно не действуют.
        int amount = basicGold + multGold * context.runtime.goldUpgradeCount;
        GoldManager.Instance.AddGold(amount, GoldSource.Fixed);
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { lifeLoseValue };
    }
}
