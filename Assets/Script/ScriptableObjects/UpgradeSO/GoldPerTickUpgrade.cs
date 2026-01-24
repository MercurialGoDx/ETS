using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Tick Upgrade/Gold Per Tick")]
public class GoldPerTickUpgrade : UpgradeBaseSO
{
    public float interval;
    public int valueFlat;

    public override void Apply(UpgradeContextSO context)
    {
        UpgradePerTick.Instance.AddGoldPerTick(valueFlat, interval);
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valueFlat, interval };
    }
}
