using System.Collections.Generic;

public class TierDamageBonus : IDamageBonusProvider, IDamageBonusDebugProvider
{
    private readonly UpgradesRuntimeData runtime;

    public TierDamageBonus(UpgradesRuntimeData runtime)
    {
        this.runtime = runtime;
    }

    public float GetDamageBonus(DamageContext ctx)
    {
        if (ctx.itemTier == ItemTier.None)
            return 0f;

        return runtime.damageTierPercent.TryGetValue(ctx.itemTier, out float tierValue)
            ? tierValue
            : 0f;
    }

    public string GetDebugLabel(DamageContext ctx, float bonusValue)
    {
        return $"Tier {ctx.itemTier}";
    }
}
