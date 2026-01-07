using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/TickUpgrade/Damage Per Minute Scaling")]
public class DamagePerMinuteScalingUpgrade : UpgradeBaseSO
{
    public float valuePercent;

    public override void Apply(UpgradeContextSO context)
    {
        UpgradePerTick.Instance.damageIncreasePerMinute += valuePercent / 100;
    }
}
