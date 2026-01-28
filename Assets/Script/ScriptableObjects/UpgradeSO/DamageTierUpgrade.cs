using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Damage/Damage Tier")]
public class DamageTierUpgrade : UpgradeBaseSO
{
    public ItemTier tier;
    public float valuePercent;

    public override void Apply(UpgradeContextSO context)
    {
        context.runtime.damageTierPercent.TryAdd(tier, 0f);
        context.runtime.damageTierPercent[tier] += valuePercent;
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { tier, valuePercent };
    }
}