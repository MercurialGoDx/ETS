using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Weapon/Global Damage Percent Upgrade")]
public class GlobalDamagePercentUpgrade : UpgradeBaseSO
{
    public float valuePercent;

    public override void Apply(UpgradeContextSO context)
    {
        context.runtime.globalDamagePercent += valuePercent;
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valuePercent };
    }
}
