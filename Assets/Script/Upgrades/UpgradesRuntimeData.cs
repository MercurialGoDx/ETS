[System.Serializable]
public class UpgradesRuntimeData
{
    public float damageFromMaxHealthPercent;
    public float damageWhileShieldActivePercent;
    public float regenPer100MissingHealth;
    public float regenAuraMultiplier;
    public bool regenAuraEnabled;

    public float[] damageTypeMultipliers;
    public int[] damageTypeStacks;

    public float healOnKill;
    // ”брать логику урона Ўипов из PlayerHealth
    public float spikesDamage;
    public float spikesOnKillBonus;
}
