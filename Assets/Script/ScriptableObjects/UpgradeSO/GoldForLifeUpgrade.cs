using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Gold/Gold For Life Upgrade")]
public class GoldForLifeUpgrade : UpgradeBaseSO
{
    public float lifeLoseValue;

    public override void Apply(UpgradeContextSO context)
    {
        context.playerHealth.AddFlatMaxHealth(-lifeLoseValue);

        context.runtime.goldUpgradeCount++;

        GoldManager.Instance.AddGold(200 + 5 * context.runtime.goldUpgradeCount);
    }
}
