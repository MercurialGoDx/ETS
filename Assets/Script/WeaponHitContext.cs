public readonly struct WeaponHitContext
{
    public readonly Enemy Target;
    public readonly WeaponDefinition Weapon;
    public readonly float DirectDamage;
    public readonly bool TargetWasKilled;

    public WeaponHitContext(
        Enemy target,
        WeaponDefinition weapon,
        float directDamage,
        bool targetWasKilled)
    {
        Target = target;
        Weapon = weapon;
        DirectDamage = directDamage;
        TargetWasKilled = targetWasKilled;
    }
}
