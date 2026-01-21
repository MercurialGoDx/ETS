using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Weapon/Damage While Shield Active Percent")]
public class DamageWhileShieldActivePercentUpgrade : UpgradeBaseSO
{
    public float valuePercent;

    public override void Apply(UpgradeContextSO context)
    {
        context.playerShield.AddDamageWhileShieldActivePercent(valuePercent);
    }

    protected override object[] GetDescriptionArgs()
    {
        return new object[] { valuePercent };
    }
}
