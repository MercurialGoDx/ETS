using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/TickUpgrade/Shield Per Minute Scaling")]
public class MaxShieldPerTickUpgrade : UpgradeBaseSO
{
    public float valuePercent;
    public float valueFlat;

    public override void Apply(UpgradeContextSO context)
    {
        UpgradePerTick.Instance.AddShieldPerTick(valueFlat, valuePercent);
    }
}
