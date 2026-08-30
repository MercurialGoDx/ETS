using System;

public class GoldDamageBonus : IDamageBonusProvider, IDamageBonusDebugProvider
{
    private readonly UpgradesRuntimeData runtime;
    private readonly Func<int> goldGetter;


    public GoldDamageBonus(UpgradesRuntimeData runtime, Func<int> goldGetter)
    {
        this.runtime = runtime;
        this.goldGetter = goldGetter;
    }

    public UpgradesRuntimeData Runtime => runtime;
    public Func<int> GoldGetter => goldGetter;


    public float GetDamageBonus(DamageContext ctx)
    {
        return (goldGetter() / 100f) * runtime.damagePerValueGoldPercent;
    }

    public string GetDebugLabel(DamageContext ctx, float bonusValue)
    {
        return $"Gold ({goldGetter()})";
    }
}

public class GlobalGoldDamageBonus : IDamageBonusProvider, IDamageBonusDebugProvider, IDamageMultiplierLayerProvider
{
    private readonly UpgradesRuntimeData runtime;
    private readonly Func<int> goldGetter;

    public GlobalGoldDamageBonus(UpgradesRuntimeData runtime, Func<int> goldGetter)
    {
        this.runtime = runtime;
        this.goldGetter = goldGetter;
    }

    public DamageMultiplierLayer Layer => DamageMultiplierLayer.Global;

    public float GetDamageBonus(DamageContext ctx)
    {
        return (goldGetter() / 100f) * runtime.globalDamagePer100GoldPercent;
    }

    public string GetDebugLabel(DamageContext ctx, float bonusValue)
    {
        return $"Global damage per gold ({goldGetter()})";
    }
}
