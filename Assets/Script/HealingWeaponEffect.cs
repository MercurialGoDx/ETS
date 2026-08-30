using UnityEngine;

[CreateAssetMenu(fileName = "HealingEffect", menuName = "TD/Weapon Effects/Healing")]
public sealed class HealingWeaponEffect : WeaponOnHitEffect
{
    [Header("Лечение башни")]
    [Min(0f)] public float healAmount = 1f;

    protected override void Apply(in WeaponHitContext context)
    {
        PlayerHealth.Instance?.Heal(healAmount);
    }
}
