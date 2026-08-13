using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

public readonly struct DamageBonusContribution
{
    public DamageBonusContribution(string label, float bonusValue)
    {
        Label = label;
        BonusValue = bonusValue;
    }

    public string Label { get; }
    public float BonusValue { get; }
}

public sealed class DamageCalculationBreakdown
{
    private const float Epsilon = 0.0001f;

    public DamageCalculationBreakdown(float baseDamage, float totalBonus, float finalDamage, List<DamageBonusContribution> contributions)
    {
        BaseDamage = baseDamage;
        TotalBonus = totalBonus;
        FinalDamage = finalDamage;
        Contributions = contributions;
    }

    public float BaseDamage { get; }
    public float TotalBonus { get; }
    public float FinalDamage { get; }
    public IReadOnlyList<DamageBonusContribution> Contributions { get; }

    public string ToDebugString()
    {
        var sb = new StringBuilder();
        sb.Append("base=").Append(FormatNumber(BaseDamage));
        sb.Append(", totalBonus=").Append(FormatPercent(TotalBonus));
        sb.Append(", final=").Append(FormatNumber(FinalDamage));

        bool hasNonZeroContributions = false;
        for (int i = 0; i < Contributions.Count; i++)
        {
            if (Math.Abs(Contributions[i].BonusValue) > Epsilon)
            {
                hasNonZeroContributions = true;
                break;
            }
        }

        if (!hasNonZeroContributions)
            return sb.ToString();

        sb.Append(" | ");

        bool isFirst = true;
        for (int i = 0; i < Contributions.Count; i++)
        {
            DamageBonusContribution contribution = Contributions[i];
            if (Math.Abs(contribution.BonusValue) <= Epsilon)
                continue;

            if (!isFirst)
                sb.Append(", ");

            sb.Append(contribution.Label)
              .Append(": ")
              .Append(FormatPercent(contribution.BonusValue));

            isFirst = false;
        }

        return sb.ToString();
    }

    private static string FormatNumber(float value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string FormatPercent(float value)
    {
        return (value >= 0f ? "+" : string.Empty)
            + (value * 100f).ToString("0.##", CultureInfo.InvariantCulture)
            + "%";
    }
}

public class DamageCalculator
{
    private readonly List<IDamageBonusProvider> bonuses = new();

    public void AddBonus(IDamageBonusProvider bonus)
    {
        bonuses.Add(bonus);
    }

    public float Calculate(DamageContext ctx)
    {
        return CalculateWithBreakdown(ctx).FinalDamage;
    }

    public DamageCalculationBreakdown CalculateWithBreakdown(DamageContext ctx)
    {
        float totalBonus = 0f;
        var contributions = new List<DamageBonusContribution>(bonuses.Count);

        foreach (var bonus in bonuses)
        {
            float bonusValue = bonus.GetDamageBonus(ctx);
            totalBonus += bonusValue;
            contributions.Add(new DamageBonusContribution(GetLabel(bonus, ctx, bonusValue), bonusValue));
        }

        float finalDamage = ctx.baseDamage * (1f + totalBonus);
        return new DamageCalculationBreakdown(ctx.baseDamage, totalBonus, finalDamage, contributions);
    }

    private static string GetLabel(IDamageBonusProvider bonus, DamageContext ctx, float bonusValue)
    {
        if (bonus is IDamageBonusDebugProvider debugProvider)
            return debugProvider.GetDebugLabel(ctx, bonusValue);

        return bonus.GetType().Name;
    }
}
