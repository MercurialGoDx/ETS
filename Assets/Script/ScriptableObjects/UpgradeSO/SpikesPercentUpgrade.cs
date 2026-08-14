using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Spikes Upgrades/Spike Percent Damage Upgrade")]
public class SpikesPercentUpgrade : UpgradeBaseSO
{
    public float valuePercent;

    public override void Apply(UpgradeContextSO context)
    {
        context.playerHealth.AddSpikesPercent(valuePercent / 100f);
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valuePercent };
    }
}
