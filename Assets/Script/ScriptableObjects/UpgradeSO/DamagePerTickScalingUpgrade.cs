using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Tick Upgrade/Damage Per Tick Scaling")]
public class DamagePerTickScalingUpgrade : UpgradeBaseSO
{
    public float interval;
    public float valuePercent;

    public override void Apply(UpgradeContextSO context)
    {
        context.runtime.generatorDamagePercent += valuePercent / 100;
        UpgradePerTick.Instance.damageTickInterval = interval;
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valuePercent, interval };
    }
}
