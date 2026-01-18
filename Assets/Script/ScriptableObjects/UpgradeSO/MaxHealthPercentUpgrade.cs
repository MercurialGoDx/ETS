using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Health/Max Health Percent")]
public class MaxHealthPercentUpgrade : UpgradeBaseSO
{
    public float percentValue;

    public override void Apply(UpgradeContextSO context)
    {
        context.playerHealth.AddMaxHealthMultiplierAndHeal(percentValue / 100f);
    }
}
