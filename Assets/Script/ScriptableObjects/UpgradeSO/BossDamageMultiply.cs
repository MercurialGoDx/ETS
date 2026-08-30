using UnityEngine;

[CreateAssetMenu(
    fileName = "Boss Damage Multiply",
    menuName = "Upgrades/Spikes Upgrades/Boss Damage Multiply")]
public class BossDamageMultiply : UpgradeBaseSO
{
    [Min(0f)]
    public float valuePercent;

    public override void Apply(UpgradeContextSO context)
    {
        if (context.playerHealth != null)
            context.playerHealth.AddSpikesBossDamagePercent(valuePercent / 100f);
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valuePercent };
    }
}
