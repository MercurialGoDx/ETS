using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Health/Heal Per Enemy Hit")]
public class HealPerEnemyHitUpgrade : UpgradeBaseSO
{
    public float valueFlat;

    public override void Apply(UpgradeContextSO context)
    {
        context.playerHealth.AddHealOnHitFromEnemy(valueFlat);
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valueFlat };
    }
}
