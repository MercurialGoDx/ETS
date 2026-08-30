using UnityEngine;

[CreateAssetMenu(fileName = "WeaknessEffect", menuName = "TD/Weapon Effects/Weakness")]
public sealed class WeaknessWeaponEffect : WeaponOnHitEffect
{
    [Header("Слабость")]
    [Tooltip("Насколько уменьшается исходящий урон врага. 25 = -25%.")]
    [Range(0f, 100f)] public float damageReductionPercent = 25f;
    [Min(0.01f)] public float duration = 3f;
    [Tooltip("Необязательный визуальный префаб слабости.")]
    public GameObject visualPrefab;

    protected override void Apply(in WeaponHitContext context)
    {
        if (context.TargetWasKilled || context.Target == null)
            return;

        context.Target.StatusEffects?.ApplyWeakness(
            damageReductionPercent,
            duration,
            visualPrefab);
    }
}
