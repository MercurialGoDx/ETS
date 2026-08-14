using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/GlobalUpgrade/Global Fire Rate")]
public class GlobalFireRate : UpgradeBaseSO
{
    public float valuePercent;

    public override void Apply(UpgradeContextSO context)
    {
        context.towerAttack.AddGlobalFireRateMultiplier(valuePercent / 100f);
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valuePercent };
    }
}
