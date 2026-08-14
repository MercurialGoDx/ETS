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

    public DamageCalculationBreakdown(
        float baseDamage,
        float normalBonus,
        float globalBonus,
        float adaptiveBonus,
        float finalDamage,
        List<DamageBonusContribution> contributions)
    {
        BaseDamage = baseDamage;
        NormalBonus = normalBonus;
        GlobalBonus = globalBonus;
        AdaptiveBonus = adaptiveBonus;
        FinalDamage = finalDamage;
        Contributions = contributions;
    }

    public float BaseDamage { get; }
    public float NormalBonus { get; }
    public float GlobalBonus { get; }
    public float AdaptiveBonus { get; }
    public float TotalMultiplierDamage => (1f + NormalBonus) * (1f + GlobalBonus) * (1f + AdaptiveBonus);
    public float TotalBonus => TotalMultiplierDamage - 1f;
    public float FinalDamage { get; }
    public IReadOnlyList<DamageBonusContribution> Contributions { get; }

    public string ToDebugString()
    {
        var sb = new StringBuilder();
        sb.Append("base=").Append(FormatNumber(BaseDamage));
        sb.Append(", normal=x").Append(FormatMultiplier(1f + NormalBonus));
        sb.Append(", global=x").Append(FormatMultiplier(1f + GlobalBonus));
        sb.Append(", adaptive=x").Append(FormatMultiplier(1f + AdaptiveBonus));
        sb.Append(", total=x").Append(FormatMultiplier(TotalMultiplierDamage));
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

    private static string FormatMultiplier(float value)
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

        // Layer providers follow their normal counterparts in every calculator.
        if (bonus is GlobalDamageBonus global)
            bonuses.Add(new GlobalDamageMultiplierBonus(global.Runtime));
        else if (bonus is GoldDamageBonus gold)
            bonuses.Add(new GlobalGoldDamageBonus(gold.Runtime, gold.GoldGetter));
    }

    public float Calculate(DamageContext ctx)
    {
        return CalculateWithBreakdown(ctx).FinalDamage;
    }

    public DamageCalculationBreakdown CalculateWithBreakdown(DamageContext ctx)
    {
        float normalBonus = 0f;
        float globalBonus = 0f;
        float adaptiveBonus = 0f;
        var contributions = new List<DamageBonusContribution>(bonuses.Count);

        foreach (var bonus in bonuses)
        {
            float bonusValue = bonus.GetDamageBonus(ctx);
            DamageMultiplierLayer layer = bonus is IDamageMultiplierLayerProvider layerProvider
                ? layerProvider.Layer
                : DamageMultiplierLayer.Normal;

            switch (layer)
            {
                case DamageMultiplierLayer.Global:
                    globalBonus += bonusValue;
                    break;
                case DamageMultiplierLayer.Adaptive:
                    adaptiveBonus += bonusValue;
                    break;
                default:
                    normalBonus += bonusValue;
                    break;
            }

            contributions.Add(new DamageBonusContribution(GetLabel(bonus, ctx, bonusValue), bonusValue));
        }

        float finalDamage = ctx.baseDamage
            * (1f + normalBonus)
            * (1f + globalBonus)
            * (1f + adaptiveBonus);

        return new DamageCalculationBreakdown(
            ctx.baseDamage,
            normalBonus,
            globalBonus,
            adaptiveBonus,
            finalDamage,
            contributions);
    }

    private static string GetLabel(IDamageBonusProvider bonus, DamageContext ctx, float bonusValue)
    {
        if (bonus is IDamageBonusDebugProvider debugProvider)
            return debugProvider.GetDebugLabel(ctx, bonusValue);

        return bonus.GetType().Name;
    }
}
