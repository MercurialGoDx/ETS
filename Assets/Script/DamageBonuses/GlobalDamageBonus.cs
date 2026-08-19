public class GlobalDamageBonus : IDamageBonusProvider, IDamageBonusDebugProvider
{
    private readonly UpgradesRuntimeData runtime;

    public GlobalDamageBonus(UpgradesRuntimeData runtime)
    {
        this.runtime = runtime;
    }

    public UpgradesRuntimeData Runtime => runtime;

    public float GetDamageBonus(DamageContext ctx)
    {
        if (ctx.isSpikes)
            return 0f;

        return runtime.damagePercent;
    }

    public string GetDebugLabel(DamageContext ctx, float bonusValue)
    {
        return "Damage";
    }
}

public class GlobalDamageMultiplierBonus : IDamageBonusProvider, IDamageBonusDebugProvider, IDamageMultiplierLayerProvider
{
    private readonly UpgradesRuntimeData runtime;

    public GlobalDamageMultiplierBonus(UpgradesRuntimeData runtime)
    {
        this.runtime = runtime;
    }

    public DamageMultiplierLayer Layer => DamageMultiplierLayer.Global;

    public float GetDamageBonus(DamageContext ctx)
    {
        // Global-слой намеренно действует и на шипы — в отличие от Normal-слоя выше.
        return runtime.globalDamagePercent;
    }

    public string GetDebugLabel(DamageContext ctx, float bonusValue)
    {
        return "Global damage";
    }
}
