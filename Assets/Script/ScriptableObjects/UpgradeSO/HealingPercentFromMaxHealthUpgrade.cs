using UnityEngine;

[CreateAssetMenu(
    fileName = "Healing Percent From Max Health",
    menuName = "Upgrades/Health/Healing Percent From Max Health")]
public sealed class HealingPercentFromMaxHealthUpgrade : UpgradeBaseSO
{
    [Header("Healing")]
    [Min(0f)]
    [Tooltip("Процент текущего недостающего здоровья, который лечится за каждый тик.")]
    public float valuePercent = 2f;

    [Range(0f, 1f)]
    [Tooltip("Множитель вклада каждой следующей покупки. 0.85 = каждая следующая покупка на 15% слабее предыдущей.")]
    public float repeatedPurchaseMultiplier = 0.85f;

    [Min(0.05f)]
    [Tooltip("Интервал между лечениями в секундах.")]
    public float tickInterval = 1f;

    [Header("Healing VFX")]
    [Tooltip("Префаб эффекта лечения. Создаётся на башне один раз и затем переиспользуется.")]
    public GameObject healingVfxPrefab;

    [Min(0.05f)]
    [Tooltip("Сколько секунд VFX остаётся активным после последнего лечения.")]
    public float healingVfxLifetime = 2.5f;

    [Tooltip("Локальное смещение VFX относительно башни.")]
    public Vector3 healingVfxLocalOffset = Vector3.zero;

    public override void Apply(UpgradeContextSO context)
    {
        if (context?.playerHealth == null)
        {
            Debug.LogWarning($"{name}: PlayerHealth is not available.");
            return;
        }

        context.playerHealth.AddHealingPercentFromMissingHealth(
            valuePercent,
            repeatedPurchaseMultiplier,
            tickInterval,
            healingVfxPrefab,
            healingVfxLifetime,
            healingVfxLocalOffset);
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valuePercent, tickInterval };
    }
}
