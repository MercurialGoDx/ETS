using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Health/Health Regeneration Percent")]
public class HealthRegenPercentUpgrade : UpgradeBaseSO
{
    public float valuePercent;

    public override void Apply(UpgradeContextSO context)
    {
        context.playerHealth.AddHealthRegen(context.playerHealth.healthRegenPerSecond * (valuePercent / 100));
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valuePercent };
    }
}
