using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Weapon/Damage From Max Health Percent")]
public class DamageFromMaxHealthPercentUpgrade : UpgradeBaseSO
{
    public float valuePercent;

    public override void Apply(UpgradeContextSO context)
    {
        context.runtime.damagePerValueHpPercent += valuePercent / 100f;
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valuePercent };
    }
}
