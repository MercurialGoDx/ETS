using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Gold/Gold For Life Upgrade")]
public class GoldForLifeUpgrade : UpgradeBaseSO
{
    public float lifeLoseValue;
    public int basicGold = 200;
    public int multGold = 5;

    public override void Apply(UpgradeContextSO context)
    {
        context.playerHealth.AddFlatMaxHealth(-lifeLoseValue);

        context.runtime.goldUpgradeCount++;

        GoldManager.Instance.AddGold(basicGold + multGold * context.runtime.goldUpgradeCount);
    }

    protected override object[] GetDescriptionArgs()
    {
        return new object[] { lifeLoseValue };
    }
}
