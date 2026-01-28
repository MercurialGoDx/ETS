using System;

public class GoldDamageBonus : IDamageBonusProvider
{
    private readonly UpgradesRuntimeData runtime;
    private readonly Func<int> goldGetter;


    public GoldDamageBonus(UpgradesRuntimeData runtime, Func<int> goldGetter)
    {
        this.runtime = runtime;
        this.goldGetter = goldGetter;
    }


    public float GetDamageBonus(DamageContext ctx)
    {
        return (goldGetter() / 100f) * runtime.damagePerValueGoldPercent;
    }
}