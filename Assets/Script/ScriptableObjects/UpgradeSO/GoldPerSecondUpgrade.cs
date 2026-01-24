using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Gold/Gold Per Second")]
public class GoldPerSecondUpgrade : UpgradeBaseSO
{
    public int goldPerSecond;

    public override void Apply(UpgradeContextSO context)
    {
        context.goldManager.AddPassiveIncome(goldPerSecond);
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { goldPerSecond };
    }
}