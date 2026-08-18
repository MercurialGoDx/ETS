using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Gold/Gold Per Second Percent")]
public class GoldPerSecondPercentUpgrade : UpgradeBaseSO
{
    public float valuePercent;

    public override void Apply(UpgradeContextSO context)
    {
        context.goldManager.MultiplyPassiveIncome(valuePercent);
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valuePercent };
    }
}
