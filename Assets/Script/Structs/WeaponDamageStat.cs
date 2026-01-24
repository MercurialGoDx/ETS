using System;

[Serializable]
public struct WeaponDamageStat
{
    public WeaponDefinition weapon;
    public float damage;

    public WeaponDamageStat(WeaponDefinition weapon, float damage)
    {
        this.weapon = weapon;
        this.damage = damage;
    }
}
