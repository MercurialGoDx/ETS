using System.Collections.Generic;

public class WeaponRuntime
{
    public WeaponDefinition def;
    public int stacks = 1;      // сколько раз купили это оружие
    public float cooldown = 0f; // свой независимый кулдаун
    public List<Enemy> lastTargets = new List<Enemy>(); // закреплённые цели по “стволам”
    public AuraDamageZone auraInstance;

    //для статистики
    public float totalDamageDealt;
}
