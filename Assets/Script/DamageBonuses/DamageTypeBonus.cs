using System;

public class DamageTypeBonus : IDamageBonusProvider, IDamageBonusDebugProvider
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
        // Шипы используют все универсальные усилители урона, но собственного
        // WeaponDamageType у них нет — типовые бонусы к ним не применяются.
        if (ctx.isSpikes)
            return 0f;

        if (ctx.damageType != type)
            return 0f;

        float flat = runtime.damageTypeFlatPercent.TryGetValue(type, out float flatValue)
            ? flatValue
            : 0f;
        float perWeapon = runtime.damageTypePerWeaponPercent.TryGetValue(type, out float perWeaponValue)
            ? perWeaponValue
            : 0f;

        return flat + weaponCount() * perWeapon;
    }

    public string GetDebugLabel(DamageContext ctx, float bonusValue)
    {
        int count = weaponCount();
        return count > 0
            ? $"{type} type ({count} weapon stacks)"
            : $"{type} type";
    }
}
