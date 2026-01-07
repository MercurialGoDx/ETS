using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Shield/Shield Percent")]
public class ShieldPercentUpgrade : UpgradeBaseSO
{
    public float valuePercent;

    public override void Apply(UpgradeContextSO context)
    {
        context.playerShield.AddShieldPercent(valuePercent / 100f);
    }
}
