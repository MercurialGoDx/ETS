using UnityEngine;

[CreateAssetMenu(fileName = "BurningEffect", menuName = "TD/Weapon Effects/Burning")]
public sealed class BurningWeaponEffect : WeaponOnHitEffect
{
    [Header("Горение")]
    [Min(1)] public int stacksPerHit = 1;
    [Tooltip("Суммарный урон одного стака относительно прямого попадания. 100 = повторить 100% урона.")]
    [Min(0f)] public float damagePercentPerStack = 100f;
    [Min(0.01f)] public float duration = 5f;
    [Tooltip("Префаб огня под ногами. Можно назначить позднее.")]
    public GameObject visualPrefab;

    protected override void Apply(in WeaponHitContext context)
    {
        if (context.TargetWasKilled || context.Target == null)
            return;

        context.Target.StatusEffects?.AddBurning(
            context.DirectDamage,
            stacksPerHit,
            damagePercentPerStack,
            duration,
            context.Weapon,
            visualPrefab);
    }
}
