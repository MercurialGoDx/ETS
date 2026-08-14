public class ShieldDamageBonus : IDamageBonusProvider, IDamageBonusDebugProvider, IDamageMultiplierLayerProvider
{
    private readonly UpgradesRuntimeData runtime;
    private readonly PlayerShield shield;

    public ShieldDamageBonus(UpgradesRuntimeData runtime, PlayerShield shield)
    {
        this.runtime = runtime;
        this.shield = shield;
    }

    public DamageMultiplierLayer Layer => DamageMultiplierLayer.Adaptive;

    public float GetDamageBonus(DamageContext ctx)
    {
        return shield.IsShieldActive
            ? runtime.adaptiveDamageWhileShieldPercent
            : 0f;
    }

    public string GetDebugLabel(DamageContext ctx, float bonusValue)
    {
        return "Adaptive damage while shield active";
    }
}
