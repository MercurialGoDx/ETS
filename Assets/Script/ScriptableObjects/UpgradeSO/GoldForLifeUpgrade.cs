using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Gold/Gold For Life Upgrade")]
public class GoldForLifeUpgrade : UpgradeBaseSO
{
    public float lifeLoseValue;
    public int basicGold = 200;
    public int multGold = 5;

    private const float MinAllowedMaxHealthAfterPurchase = 0f;

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
        GoldManager.Instance.AddGold(basicGold + multGold * context.runtime.goldUpgradeCount);
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { lifeLoseValue };
    }
}
