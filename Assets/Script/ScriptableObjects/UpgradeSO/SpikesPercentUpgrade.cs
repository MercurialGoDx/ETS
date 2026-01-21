using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Spikes Upgrades/Spike Percent Damage Upgrade")]
public class SpikesPercentUpgrade : UpgradeBaseSO
{
    public float valuePercent;

    public override void Apply(UpgradeContextSO context)
    {
        context.playerHealth.AddSpikesDamage(valuePercent / 100);
    }

    protected override object[] GetDescriptionArgs()
    {
        return new object[] { valuePercent };
    }
}
