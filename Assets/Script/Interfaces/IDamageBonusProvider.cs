public enum DamageMultiplierLayer
{
    Normal,
    Global,
    Adaptive
}

public interface IDamageBonusProvider
{
    /// Возвращает бонус в виде доли: 0.1 = +10%
    float GetDamageBonus(DamageContext ctx);
}

public interface IDamageMultiplierLayerProvider
{
    DamageMultiplierLayer Layer { get; }
}

public interface IDamageBonusDebugProvider
{
    string GetDebugLabel(DamageContext ctx, float bonusValue);
}
