using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Weapon/Shield Active Damage")]
public class ShieldDamageUpgrade : UpgradeBaseSO
{
    public float valuePercent;

    public override void Apply(UpgradeContextSO context)
    {
        context.runtime.damageWhileShieldActivePercent += valuePercent;
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valuePercent };
    }
}