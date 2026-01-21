using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Shield/Max Shield Flat")]
public class MaxShieldFlatUpgrade : UpgradeBaseSO
{
    public float valueFlat;

    public override void Apply(UpgradeContextSO context)
    {
        context.playerShield.AddMaxShield(valueFlat);
    }

    protected override object[] GetDescriptionArgs()
    {
        return new object[] { valueFlat };
    }
}
