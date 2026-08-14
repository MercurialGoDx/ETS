using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Health/Health Regeneration Percent")]
public class HealthRegenPercentUpgrade : UpgradeBaseSO
{
    public float valuePercent;

    public override void Apply(UpgradeContextSO context)
    {
        context.playerHealth.AddHealthRegenMultiplier(valuePercent / 100f);
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valuePercent };
    }
}
