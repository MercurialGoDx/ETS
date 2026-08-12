public interface IDamageBonusProvider
{
    /// Возвращает бонус в виде доли: 0.1 = +10%
    float GetDamageBonus(DamageContext ctx);
}

public interface IDamageBonusDebugProvider
{
    string GetDebugLabel(DamageContext ctx, float bonusValue);
}
