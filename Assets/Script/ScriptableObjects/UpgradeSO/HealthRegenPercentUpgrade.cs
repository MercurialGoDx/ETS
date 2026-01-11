using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Health/Health Regeneration Percent")]
public class HealthRegenPercentUpgrade : UpgradeBaseSO
{
    public float valueFlat;

    public override void Apply(UpgradeContextSO context)
    {
        context.playerHealth.AddHealthRegen(context.playerHealth.healthRegenPerSecond * (valueFlat / 100));
    }
}
