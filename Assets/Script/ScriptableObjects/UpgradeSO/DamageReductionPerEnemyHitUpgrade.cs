using UnityEngine;

[CreateAssetMenu(
    fileName = "Damage Reduction Per Enemy Hit",
    menuName = "Upgrades/Health/Damage Reduction Per Enemy Hit")]
public class DamageReductionPerEnemyHitUpgrade : UpgradeBaseSO
{
    [Range(0f, 100f)]
    public float valuePercent = 2f;

    public override void Apply(UpgradeContextSO context)
    {
        if (context?.playerHealth == null) return;

        context.playerHealth.AddDamageReductionPerEnemyHit(valuePercent / 100f);
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valuePercent };
    }
}
