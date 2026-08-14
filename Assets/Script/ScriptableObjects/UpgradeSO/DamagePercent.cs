using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Weapon/Damage Percent")]
public class DamagePercent : UpgradeBaseSO
{
    public float valuePercent;

    public override void Apply(UpgradeContextSO context)
    {
        context.runtime.damagePercent += valuePercent / 100f;
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valuePercent };
    }
}
