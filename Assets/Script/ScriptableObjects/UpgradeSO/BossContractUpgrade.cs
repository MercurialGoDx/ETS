using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Utility/Boss Contract")]
public class BossContractUpgrade : UpgradeBaseSO
{
    [Header("Next Boss")]
    [Min(1f)]
    public float healthMultiplier = 2f;

    [Min(1f)]
    public float damageMultiplier = 2f;

    [Header("Neon Outline")]
    public Material outlineMaterial;

    [ColorUsage(true, true)]
    public Color outlineColor = new Color(1f, 0.02f, 0f, 1f);

    [Min(0f)]
    public float outlineWidth = 0.035f;

    [Min(0f)]
    public float glowIntensity = 1.5f;

    [Min(1f)]
    public float scaleMultiplier = 1.15f;

    public override void Apply(UpgradeContextSO context)
    {
        if (context?.runtime == null)
        {
            Debug.LogWarning($"{name}: runtime data is not available.");
            return;
        }

        context.runtime.AddBossContract(
            healthMultiplier,
            damageMultiplier,
            outlineMaterial,
            outlineColor,
            outlineWidth,
            glowIntensity,
            scaleMultiplier);
        Debug.Log(
            $"[BossContract] Added: stacks={context.runtime.PendingBossContractStacks}, " +
            $"HP contribution=x{healthMultiplier:0.##}, damage contribution=x{damageMultiplier:0.##}.");
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { healthMultiplier, damageMultiplier };
    }
}
