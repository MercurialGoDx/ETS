using System.Collections.Generic;

public class DamageCalculator
{
    private readonly List<IDamageBonusProvider> bonuses = new();

    public void AddBonus(IDamageBonusProvider bonus)
    {
        bonuses.Add(bonus);
    }

    public float Calculate(DamageContext ctx)
    {
        float totalBonus = 0f;

        foreach (var bonus in bonuses)
            totalBonus += bonus.GetDamageBonus(ctx);

        return ctx.baseDamage * (1f + totalBonus);
    }
}