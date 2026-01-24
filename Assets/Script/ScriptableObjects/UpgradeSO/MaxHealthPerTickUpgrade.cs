using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Tick Upgrade/Health Per Minute Scaling")]
public class MaxHealthPerTickUpgrade : UpgradeBaseSO
{
    public float interval;
    public float valueFlat;

    public override void Apply(UpgradeContextSO context)
    {
        UpgradePerTick.Instance.AddHealthPerTick(valueFlat, interval);
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valueFlat, interval };
    }
}
