using UnityEngine;

[CreateAssetMenu(
    fileName = "Spike Enemy Attack Stacking",
    menuName = "Upgrades/Spikes Upgrades/Spike Enemy Attack Stacking")]
public class SpikeEnemyAttackStacking : UpgradeBaseSO
{
    [Min(0f)]
    public float valuePercent;

    public override void Apply(UpgradeContextSO context)
    {
        if (context.playerHealth != null)
            context.playerHealth.AddSpikesEnemyAttackStackPercent(valuePercent / 100f);
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valuePercent };
    }
}
