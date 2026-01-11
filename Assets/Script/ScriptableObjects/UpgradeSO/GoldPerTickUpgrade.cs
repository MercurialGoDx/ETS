using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Tick Upgrade/Gold Per Tick")]
public class GoldPerTickUpgrade : UpgradeBaseSO
{
    private float interval;
    private int valueFlat;

    public override void Apply(UpgradeContextSO context)
    {
        UpgradePerTick.Instance.AddGoldPerTick(valueFlat, interval);
    }
}
