using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Weapon/Damage Type Percent")]
public class DamageTypePercentUpgrade : UpgradeBaseSO
{
    public WeaponDamageType damageType;
    public float valuePercent;

    public override void Apply(UpgradeContextSO context)
    {
        context.runtime.damageTypeFlatPercent.TryAdd(damageType, 0f);
        context.runtime.damageTypeFlatPercent[damageType] += valuePercent / 100;
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { GetLocalizedDamageType(damageType), valuePercent };
    }
}
