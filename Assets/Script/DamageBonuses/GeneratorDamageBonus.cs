public class GeneratorDamageBonus : IDamageBonusProvider, IDamageBonusDebugProvider
{
    private readonly UpgradesRuntimeData runtime;

    public GeneratorDamageBonus(UpgradesRuntimeData runtime)
    {
        this.runtime = runtime;
    }

    public float GetDamageBonus(DamageContext ctx)
    {
        if (ctx.isSpikes)
            return 0f;

        return runtime.totalGeneratorDamagePercent;
    }

    public string GetDebugLabel(DamageContext ctx, float bonusValue)
    {
        return "Generator";
    }
}
