using UnityEngine;

[CreateAssetMenu(fileName = "FreezeEffect", menuName = "TD/Weapon Effects/Freeze")]
public sealed class FreezeWeaponEffect : WeaponOnHitEffect
{
    [Header("Заморозка")]
    [Min(0.01f)] public float duration = 2f;
    [Tooltip("Необязательная замена стандартного префаба ледяной глыбы.")]
    public GameObject visualPrefabOverride;
    [Tooltip("Множитель размера ледяной глыбы. 1 — автоматически подобранный размер, 0.8 — меньше, 1.2 — больше.")]
    [Min(0.01f)] public float visualScaleMultiplier = 1f;

    protected override void Apply(in WeaponHitContext context)
    {
        if (context.TargetWasKilled || context.Target == null)
            return;

        context.Target.StatusEffects?.ApplyFreeze(
            duration,
            visualPrefabOverride,
            visualScaleMultiplier);
    }
}
