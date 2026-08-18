using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Gold/Hunt")]
public class HuntUpgrade : UpgradeBaseSO
{
    [Header("Golden Enemy")]
    [Range(0f, 100f)]
    public float chancePercent = 3f;

    [Range(0f, 1f)]
    public float diminishingFactor = 0.9f;

    [Min(1f)]
    public float healthMultiplier = 5f;

    [Min(1f)]
    public float damageMultiplier = 2f;

    [Min(1f)]
    public float goldMultiplier = 10f;

    [Header("Visual")]
    public Color goldenTint = new Color(1f, 0.72f, 0.2f, 1f);

    [Range(0f, 1f)]
    public float tintStrength = 0.35f;

    public override void Apply(UpgradeContextSO context)
    {
        if (context?.runtime == null)
        {
            Debug.LogWarning($"{name}: runtime data is not available.");
            return;
        }

        context.runtime.AddHunt(
            chancePercent,
            diminishingFactor,
            healthMultiplier,
            damageMultiplier,
            goldMultiplier,
            goldenTint,
            tintStrength);

        Debug.Log(
            $"[Hunt] Activated: stacks={context.runtime.HuntStacks}, " +
            $"chance={context.runtime.GoldenEnemyChance * 100f:0.##}%, " +
            $"diminishing={diminishingFactor:0.##}, " +
            $"HP=x{healthMultiplier:0.##}, damage=x{damageMultiplier:0.##}, gold=x{goldMultiplier:0.##}");
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { chancePercent, healthMultiplier, damageMultiplier, goldMultiplier };
    }
}
