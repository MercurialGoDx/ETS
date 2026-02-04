public class ShieldDamageBonus : IDamageBonusProvider
{
    private readonly UpgradesRuntimeData runtime;
    private readonly PlayerShield shield;

    public ShieldDamageBonus(UpgradesRuntimeData runtime, PlayerShield shield)
    {
        this.runtime = runtime;
        this.shield = shield;
    }

    public float GetDamageBonus(DamageContext ctx)
    {
        return shield.IsShieldActive
            ? runtime.damageWhileShieldActivePercent
            : 0f;
    }
}