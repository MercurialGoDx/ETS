using System.Collections.Generic;
using UnityEngine;

public class DamageStatsManager : MonoBehaviour
{
    public static DamageStatsManager Instance { get; private set; }

    // Храним как Dictionary для быстрого накопления
    private Dictionary<WeaponDefinition, float> damageByWeapon =
        new Dictionary<WeaponDefinition, float>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // DontDestroyOnLoad(gameObject); // если нужно
    }

    /// <summary>
    /// Регистрируем нанесённый урон
    /// </summary>
    public void RegisterDamage(WeaponDefinition weapon, float damage)
    {
        if (weapon == null || damage <= 0f)
            return;

        if (!damageByWeapon.TryGetValue(weapon, out float current))
            current = 0f;

        damageByWeapon[weapon] = current + damage;
    }

    /// <summary>
    /// Получить суммарный урон конкретного оружия
    /// </summary>
    public float GetTotalDamage(WeaponDefinition weapon)
    {
        if (weapon == null)
            return 0f;

        return damageByWeapon.TryGetValue(weapon, out var dmg) ? dmg : 0f;
    }

    /// <summary>
    /// Получить ВСЮ статистику, отсортированную по убыванию урона
    /// </summary>
    public List<WeaponDamageStat> GetDamageSorted()
    {
        var list = new List<WeaponDamageStat>(damageByWeapon.Count);

        foreach (var kvp in damageByWeapon)
        {
            list.Add(new WeaponDamageStat(kvp.Key, kvp.Value));
        }

        // сортировка по убыванию урона
        list.Sort((a, b) => b.damage.CompareTo(a.damage));

        return list;
    }

    /// <summary>
    /// Очистить статистику (например, при рестарте рана)
    /// </summary>
    public void Clear()
    {
        damageByWeapon.Clear();
    }
}
