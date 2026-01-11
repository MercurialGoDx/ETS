using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Health/Health Regeneration")]
public class HealthRegenUpgrade : UpgradeBaseSO
{
    public float regenValue;

    public override void Apply(UpgradeContextSO context)
    {
        context.playerHealth.AddHealthRegen(regenValue);
    }
}
