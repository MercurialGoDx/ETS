using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/GlobalUpgrade/Global Spikes Percent")]
public class GlobalSpikesPercent : UpgradeBaseSO
{
    public float valuePercent;

    public override void Apply(UpgradeContextSO context)
    {
        context.playerHealth.AddSpikesGlobalMultiplier(valuePercent / 100f);
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valuePercent };
    }
}
