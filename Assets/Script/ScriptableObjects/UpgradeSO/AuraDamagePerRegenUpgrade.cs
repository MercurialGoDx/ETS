using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Weapon/Damage Aura Per Regen")]
public class AuraDamagePerRegenUpgrade : UpgradeBaseSO
{
    public float valueFlat;

    [Header("Постоянный визуал Degen Aura на врагах")]
    [Tooltip("Particle System prefab, который постоянно висит над каждым врагом после покупки улучшения. Скрипт на prefab не требуется.")]
    public GameObject enemyVisualPrefab;

    [Tooltip("Множитель размера prefab относительно ширины коллайдера врага.")]
    [Min(0.01f)] public float enemyVisualScaleMultiplier = 1f;

    [Tooltip("Дополнительная высота над верхней точкой коллайдера. Значение умножается на итоговый размер визуала.")]
    public float enemyVisualHeightOffset = 0.15f;

    public override void Apply(UpgradeContextSO context)
    {
        context.regenAuraDamage.SetStatisticsSource(this);
        context.regenAuraDamage.regenAuraEnabled = true;
        context.regenAuraDamage.regenAuraMultiplier += valueFlat;
        context.regenAuraDamage.ConfigureEnemyVisual(
            enemyVisualPrefab,
            enemyVisualScaleMultiplier,
            enemyVisualHeightOffset);
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valueFlat };
    }
}
