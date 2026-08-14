using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/GlobalUpgrade/Global Shield Percent")]
public class GlobalShieldPercent : UpgradeBaseSO
{
    public float valuePercent;

    public override void Apply(UpgradeContextSO context)
    {
        context.playerShield.AddShieldGlobalMultiplier(valuePercent / 100f);
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valuePercent };
    }
}
