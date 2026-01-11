using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Tick Upgrade/Shield Per Tick Scaling")]
public class MaxShieldPerTickUpgrade : UpgradeBaseSO
{
    public float interval;
    public float valueFlat;

    public override void Apply(UpgradeContextSO context)
    {
        UpgradePerTick.Instance.AddShieldPerTick(valueFlat, interval);
    }
}
