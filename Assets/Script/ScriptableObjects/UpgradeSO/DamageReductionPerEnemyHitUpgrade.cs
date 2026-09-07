using UnityEngine;

[CreateAssetMenu(
    fileName = "Damage Reduction Per Enemy Hit",
    menuName = "Upgrades/Health/Damage Reduction Per Enemy Hit")]
public class DamageReductionPerEnemyHitUpgrade : UpgradeBaseSO
{
    [Range(0f, 100f)]
    public float valuePercent = 2f;

    [Tooltip("Сколько секунд живёт каждый отдельный стак Damage Reduction от удара.")]
    [Min(0.01f)]
    public float stackDuration = 20f;

    public override void Apply(UpgradeContextSO context)
    {
        if (context?.playerHealth == null) return;

        context.playerHealth.AddDamageReductionPerEnemyHit(
            valuePercent / 100f,
            stackDuration);
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valuePercent };
    }
}
