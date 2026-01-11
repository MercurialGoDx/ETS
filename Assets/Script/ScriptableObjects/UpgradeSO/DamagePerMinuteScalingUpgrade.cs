using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Tick Upgrade/Damage Per Tick Scaling")]
public class DamagePerMinuteScalingUpgrade : UpgradeBaseSO
{
    public float valuePercent;

    public override void Apply(UpgradeContextSO context)
    {
        UpgradePerTick.Instance.damageIncreasePerMinute += valuePercent / 100;
    }
}
