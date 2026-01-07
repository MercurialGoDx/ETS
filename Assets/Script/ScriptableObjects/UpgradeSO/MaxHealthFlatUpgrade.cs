using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Health/Max Health Flat")]
public class MaxHealthFlatUpgrade : UpgradeBaseSO
{
    public float healthValue;

    public override void Apply(UpgradeContextSO context)
    {
        context.playerHealth.AddFlatMaxHealthAndHeal(healthValue);
    }
}