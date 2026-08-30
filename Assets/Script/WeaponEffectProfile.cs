using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WeaponEffects", menuName = "TD/Weapon Effects/Profile")]
public sealed class WeaponEffectProfile : ScriptableObject
{
    [SerializeField] private List<WeaponOnHitEffect> effects = new();

    public IReadOnlyList<WeaponOnHitEffect> Effects => effects;

    public void ProcessHit(in WeaponHitContext context)
    {
        for (int i = 0; i < effects.Count; i++)
        {
            WeaponOnHitEffect effect = effects[i];
            if (effect != null)
                effect.TryApply(context);
        }
    }
}
