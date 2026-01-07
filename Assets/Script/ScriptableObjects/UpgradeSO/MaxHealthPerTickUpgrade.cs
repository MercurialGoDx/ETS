using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/TickUpgrade/Health Per Minute Scaling")]
public class MaxHealthPerTickUpgrade : UpgradeBaseSO
{
    public float valuePercent;
    public float valueFlat;

    public override void Apply(UpgradeContextSO context)
    {
        UpgradePerTick.Instance.AddHealthPerTick(valueFlat, valuePercent);
    }
}
