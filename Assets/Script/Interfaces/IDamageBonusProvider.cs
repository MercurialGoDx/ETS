public interface IDamageBonusProvider
{
    /// Возвращает бонус в процентах
    float GetDamageBonus(DamageContext ctx);
}