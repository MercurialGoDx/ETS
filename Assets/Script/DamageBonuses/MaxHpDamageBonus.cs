public class MaxHpDamageBonus : IDamageBonusProvider, IDamageBonusDebugProvider
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

    public string GetDebugLabel(DamageContext ctx, float bonusValue)
    {
        return $"Max HP ({health.MaxHealth:0.###})";
    }
}
