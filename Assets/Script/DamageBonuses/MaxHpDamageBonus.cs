public class MaxHpDamageBonus : IDamageBonusProvider
{
    private readonly UpgradesRuntimeData runtime;
    private readonly PlayerHealth health;

    public MaxHpDamageBonus(UpgradesRuntimeData runtime, PlayerHealth health)
    {
        this.runtime = runtime;
        this.health = health;
    }

    public float GetDamageBonus(DamageContext ctx)
    {
        return (health.MaxHealth / 100f) * runtime.damagePerValueHpPercent;
    }
}