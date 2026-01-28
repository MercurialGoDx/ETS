public class GlobalDamageBonus : IDamageBonusProvider
{
    private readonly UpgradesRuntimeData runtime;

    public GlobalDamageBonus(UpgradesRuntimeData runtime)
    {
        this.runtime = runtime;
    }

    public float GetDamageBonus(DamageContext ctx)
    {
        if (ctx.isSpikes)
            return 0f;

        return runtime.globalDamagePercent;
    }
}