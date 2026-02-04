using System;
using System.Collections.Generic;

public class DamageTypeBonus : IDamageBonusProvider
{
    private readonly WeaponDamageType type;
    private readonly Func<int> weaponCount;
    private readonly UpgradesRuntimeData runtime;

    public DamageTypeBonus(
        WeaponDamageType type,
        Func<int> weaponCount,
        UpgradesRuntimeData runtime)
    {
        this.type = type;
        this.weaponCount = weaponCount;
        this.runtime = runtime;
    }

    public float GetDamageBonus(DamageContext ctx)
    {
        if (ctx.damageType != type)
            return 0f;

        float flat = runtime.damageTypeFlatPercent.GetValueOrDefault(type);
        float perWeapon = runtime.damageTypePerWeaponPercent.GetValueOrDefault(type);

        return flat + weaponCount() * perWeapon;
    }
}