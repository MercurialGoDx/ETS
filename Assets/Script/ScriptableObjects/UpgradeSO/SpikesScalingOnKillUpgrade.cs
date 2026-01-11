using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Spikes Upgrades/Spikes Scaling Damage Per Kill")]
public class SpikesScalingOnKillUpgrade : UpgradeBaseSO
{
    public float valueFlat;

    public override void Apply(UpgradeContextSO context)
    {
        context.playerHealth.AddSpikesDamagePerKill(valueFlat);
    }
}
