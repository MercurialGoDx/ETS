using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Utility/Slow Aura")]
public sealed class SlowAuraUpgrade : UpgradeBaseSO
{
    [Header("Enemy Debuff")]
    [Min(0f)]
    [Tooltip("Процент снижения от первого стака.")]
    public float reductionPercent = 25f;

    [Min(0f)]
    [Tooltip("Множитель снижения для каждого стака после первого. 0.5 = половина первого значения.")]
    public float repeatedStackMultiplier = 0.5f;

    [Min(0f)]
    [Tooltip("Горизонтальный радиус действия в Unity units.")]
    public float effectRadius = 12f;

    [Header("Visual")]
    [Tooltip("Создаётся один раз как дочерний объект башни при получении первого стака.")]
    public GameObject auraVisualPrefab;

    [Tooltip("Насколько поднять визуал ауры над позицией башни. Не влияет на радиус дебаффа.")]
    public float visualHeightOffset = 0.25f;

    public override void Apply(UpgradeContextSO context)
    {
        if (context?.runtime == null)
        {
            Debug.LogWarning($"{name}: runtime data is not available.");
            return;
        }

        bool isFirstStack = context.runtime.SlowAuraStacks == 0;
        context.runtime.AddSlowAura(reductionPercent, repeatedStackMultiplier, effectRadius);

        if (isFirstStack)
            CreateAuraVisual(context);

        Debug.Log(
            $"[SlowAura] Applied: stacks={context.runtime.SlowAuraStacks}, " +
            $"reduction={context.runtime.SlowAuraReductionPercent:0.###}%, " +
            $"radius={context.runtime.SlowAuraRadius:0.###}.");
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        UpgradesRuntimeData runtime = UpgradesManager.Instance?.GameplayRuntimeData;
        float nextReduction = runtime != null
            ? runtime.GetSlowAuraReductionAfterNextStack(reductionPercent, repeatedStackMultiplier)
            : Mathf.Max(0f, reductionPercent);

        return new object[] { nextReduction };
    }

    private void CreateAuraVisual(UpgradeContextSO context)
    {
        if (auraVisualPrefab == null)
        {
            Debug.LogWarning($"{name}: Slow Aura visual prefab is not assigned.");
            return;
        }

        if (context.towerAttack == null)
        {
            Debug.LogWarning($"{name}: TowerAttack is not available for the Slow Aura visual.");
            return;
        }

        GameObject visual = Instantiate(auraVisualPrefab, context.towerAttack.transform, false);
        visual.name = auraVisualPrefab.name;
        visual.transform.localPosition += Vector3.up * visualHeightOffset;
    }
}
