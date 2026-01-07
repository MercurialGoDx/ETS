using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Health/Max Health Percent")]
public class MaxHealthPercentUpgrade : UpgradeBaseSO
{
    public float percentValue;

    public override void Apply(UpgradeContextSO context)
    {
        context.playerHealth.AddMaxHealthMultiplier(percentValue / 100f);
    }
}
