using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Weapon/Damage Per (100) Gold ")]
public class DamagePerGoldUpgrade : UpgradeBaseSO
{
    public float valuePercent;

    public override void Apply(UpgradeContextSO context)
    {
        context.runtime.damagePerValueGoldPercent += valuePercent;
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valuePercent };
    }
}