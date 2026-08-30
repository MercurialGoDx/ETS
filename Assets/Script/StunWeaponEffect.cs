using UnityEngine;

[CreateAssetMenu(fileName = "StunEffect", menuName = "TD/Weapon Effects/Stun")]
public sealed class StunWeaponEffect : WeaponOnHitEffect
{
    [Header("Оглушение")]
    [Min(0.01f)] public float duration = 2f;
    [Tooltip("Необязательная замена стандартного префаба звёзд.")]
    public GameObject visualPrefabOverride;

    protected override void Apply(in WeaponHitContext context)
    {
        if (context.TargetWasKilled || context.Target == null)
            return;

        context.Target.StatusEffects?.ApplyStun(duration, visualPrefabOverride);
    }
}
