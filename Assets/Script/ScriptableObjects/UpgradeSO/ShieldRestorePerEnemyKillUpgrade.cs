using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Shield/Shield Restore Per Enemy Kill")]
public class ShieldRestorePerEnemyKillUpgrade : UpgradeBaseSO
{
    public float valueFlat;

    public override void Apply(UpgradeContextSO context)
    {
        context.playerShield.AddShieldRestorePerEnemyKill(valueFlat);
    }

    protected override object[] GetDescriptionArgs()
    {
        return new object[] { valueFlat };
    }
}
