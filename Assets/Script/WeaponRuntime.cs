using System.Collections.Generic;

public class WeaponRuntime
{
    public WeaponDefinition def;
    public int stacks = 1;
    public float cooldown = 0f;
    public List<Enemy> lastTargets = new List<Enemy>();
    public AuraDamageZone auraInstance;
    public bool isVolleyInProgress;
    public bool immediateRetargetRequested;
    public float baseDamageBonus;

    public float totalDamageDealt;
}
