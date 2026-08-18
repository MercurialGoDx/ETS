using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Health/Heal Amplification Percent")]
public class HealAmplificationPercentUpgrade : UpgradeBaseSO
{
    public float valuePercent;

    public override void Apply(UpgradeContextSO context)
    {
        context.playerHealth.AddHealAmplificationPercent(valuePercent / 100f);
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valuePercent };
    }
}
