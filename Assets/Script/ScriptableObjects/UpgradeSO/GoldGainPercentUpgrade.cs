using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Gold/Gold Gain Percent")]
public class GoldGainPercentUpgrade : UpgradeBaseSO
{
    public float valuePercent;

    public override void Apply(UpgradeContextSO context)
    {
        GoldManager.Instance.AddGoldGainPercent(valuePercent);
    }
}
