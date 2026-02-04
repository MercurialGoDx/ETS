public class GeneratorDamageBonus : IDamageBonusProvider
{
    private readonly UpgradesRuntimeData runtime;

    public GeneratorDamageBonus(UpgradesRuntimeData runtime)
    {
        this.runtime = runtime;
    }

    public float GetDamageBonus(DamageContext ctx)
    {
        return runtime.totalGeneratorDamagePercent;
    }
}