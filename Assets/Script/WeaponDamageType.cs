using UnityEngine;

public enum WeaponDamageType
{
    Magic = 0,
    Piercing = 1,
    Normal = 2,
    Projectile = 3,
    Heavy = 4,
    Chaos = 5,
    Holy = 6
}

public static class WeaponDamageTypeExtensions
{
    public static Color GetDisplayColor(this WeaponDamageType damageType)
    {
        switch (damageType)
        {
            case WeaponDamageType.Magic:
                return new Color32(88, 203, 248, 255); // #58CBF8
            case WeaponDamageType.Piercing:
                return new Color32(112, 209, 96, 255); // #70D160
            case WeaponDamageType.Normal:
                return new Color32(199, 128, 16, 255); // #C78010
            case WeaponDamageType.Projectile:
                return new Color32(255, 148, 154, 255); // #FF949A
            case WeaponDamageType.Heavy:
                return new Color32(173, 172, 172, 255); // #ADACAC
            case WeaponDamageType.Chaos:
                return new Color32(183, 144, 255, 255); // #B790FF
            case WeaponDamageType.Holy:
                return new Color32(255, 244, 164, 255); // #FFF4A4
            default:
                return Color.white;
        }
    }
}
