using UnityEngine;

[CreateAssetMenu(
    fileName = "Exhaustible Damage Reduction",
    menuName = "Upgrades/Health/Exhaustible Damage Reduction")]
public class ExhaustibleDamageReductionUpgrade : UpgradeBaseSO
{
    [Header("Истощаемый DR score")]
    [Tooltip("Сколько процентов damageReductionScore добавляет одна покупка в стартовый запас.")]
    [Range(0f, 1000f)]
    public float startScorePercent = 75f;

    [Tooltip("Сколько процентов score одна покупка теряет при каждом ударе по башне.")]
    [Range(0f, 1000f)]
    public float lossScorePerHitPercent = 10f;

    public override void Apply(UpgradeContextSO context)
    {
        if (context?.playerHealth == null) return;

        context.playerHealth.AddExhaustibleDamageReduction(
            startScorePercent / 100f,
            lossScorePerHitPercent / 100f);
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { startScorePercent, lossScorePerHitPercent };
    }
}
