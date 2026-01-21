using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Tick Upgrade/Damage Per Tick Scaling")]
public class DamagePerTickScalingUpgrade : UpgradeBaseSO
{
    public float interval;
    public float valuePercent;

    public override void Apply(UpgradeContextSO context)
    {
        UpgradePerTick.Instance.damageIncreasePerTick += valuePercent / 100;
    }

    protected override object[] GetDescriptionArgs()
    {
        return new object[] { valuePercent };
    }
}
