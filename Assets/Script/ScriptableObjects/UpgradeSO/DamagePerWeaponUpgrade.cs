using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Weapon/Damage Per Weapon Type")]
public class DamagePerWeaponUpgrade : UpgradeBaseSO
{
    public WeaponDamageType damageType;
    public float valuePercentPerWeapon;

    public override void Apply(UpgradeContextSO context)
    {
        context.runtime.damageTypePerWeaponPercent.TryAdd(damageType, 0f);
        context.runtime.damageTypePerWeaponPercent[damageType] += valuePercentPerWeapon;
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { damageType, valuePercentPerWeapon };
    }
}