using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Health/Health Regeneration")]
public class HealthRegenUpgrade : UpgradeBaseSO
{
    public float valueFlat;

    public override void Apply(UpgradeContextSO context)
    {
        context.playerHealth.AddHealthRegen(valueFlat);
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valueFlat };
    }
}
