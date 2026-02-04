using System.Collections.Generic;

public class TierDamageBonus : IDamageBonusProvider
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


        return runtime.damageTierPercent.GetValueOrDefault(ctx.itemTier, 0f);
    }
}
