using System.Collections.Generic;

[System.Serializable]
public class UpgradesRuntimeData
{
    public int goldUpgradeCount;

    public float damageFromMaxHealthPercent;

    public float[] damageTypeMultipliers;
    public int[] damageTypeStacks;

    public EnemySpawner enemySpawner;

    public float globalDamagePercent; // +% ко всем типам
    public float generatorDamagePercent; // +% со временем
    public float totalGeneratorDamagePercent;
    public float damageWhileShieldActivePercent; // +% при щите


    public float damagePerValueHpPercent;
    public float damagePerValueGoldPercent;

    public Dictionary<ItemTier, float> damageTierPercent = new();
    public Dictionary<WeaponDamageType, float> damageTypeFlatPercent = new();
    public Dictionary<WeaponDamageType, float> damageTypePerWeaponPercent = new();
}
