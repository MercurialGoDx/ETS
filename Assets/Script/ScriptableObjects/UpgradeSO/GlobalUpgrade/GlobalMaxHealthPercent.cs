using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/GlobalUpgrade/Global Max Health Percent")]
public class GlobalMaxHealthPercent : UpgradeBaseSO
{
    public float percentValue;

    public override void Apply(UpgradeContextSO context)
    {
        context.playerHealth.AddMaxHealthGlobalMultiplierAndHeal(percentValue / 100f);
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { percentValue };
    }
}
