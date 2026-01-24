using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Spikes Upgrades/Spike Base Damage Upgrade")]
public class SpikesBaseUpgrade : UpgradeBaseSO
{
    public float valueFlat;

    public override void Apply(UpgradeContextSO context)
    {
        context.playerHealth.AddSpikesDamage(valueFlat);
    }

    protected override object[] GetDescriptionArgs()
    {
        return new object[] { valueFlat };
    }
}
