using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Health/Damage Reduction")]
public class DamageReductionUpgrade : UpgradeBaseSO
{
    [Range(0f, 100f)]
    public float valuePercent = 10f; // 10% от остатка

    public override void Apply(UpgradeContextSO context)
    {
        if (context?.playerHealth == null) return;

        context.playerHealth.AddDamageReductionDiminishing(valuePercent / 100f);
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valuePercent };
    }
}
