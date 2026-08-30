using UnityEngine;

public abstract class WeaponOnHitEffect : ScriptableObject
{
    [Header("Срабатывание")]
    [Tooltip("Шанс срабатывания отдельно для каждого задетого врага. 20 = 20%.")]
    [Range(0f, 100f)]
    public float chancePercent = 100f;

    public bool TryApply(in WeaponHitContext context)
    {
        if (context.Weapon == null || context.DirectDamage <= 0f || chancePercent <= 0f)
            return false;

        if (chancePercent < 100f && Random.value * 100f >= chancePercent)
            return false;

        Apply(context);
        return true;
    }

    protected abstract void Apply(in WeaponHitContext context);
}
