using System.Collections.Generic;
using UnityEngine;

public class DamageStatsManager : MonoBehaviour
{
    public static DamageStatsManager Instance { get; private set; }

    private readonly Dictionary<WeaponDefinition, float> damageByWeapon = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void RegisterDamage(WeaponDefinition weapon, float damage)
    {
        if (weapon == null)
            return;

        if (!damageByWeapon.ContainsKey(weapon))
            damageByWeapon[weapon] = 0f;

        damageByWeapon[weapon] += damage;
    }

    public List<(WeaponDefinition weapon, float damage)> GetDamageSorted()
    {
        var result = new List<(WeaponDefinition weapon, float damage)>();

        foreach (var pair in damageByWeapon)
        {
            result.Add((pair.Key, pair.Value));
        }

        result.Sort((a, b) => b.damage.CompareTo(a.damage));

        return result;
    }

    public void ResetStats()
    {
        damageByWeapon.Clear();
    }
}