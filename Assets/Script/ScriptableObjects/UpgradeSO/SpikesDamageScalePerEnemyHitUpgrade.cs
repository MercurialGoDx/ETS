using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Spikes Upgrades/Spikes Scaling Damage Per Enemy Hit")]
public class SpikesDamageScalePerEnemyHitUpgrade : UpgradeBaseSO
{
    public float valueFlat;

    public override void Apply(UpgradeContextSO context)
    {
        context.playerHealth.AddSpikesDamagePerEnemyHit(valueFlat);
    }

    protected override object[] GetDescriptionArgs()
    {
        return new object[] { valueFlat };
    }
}
