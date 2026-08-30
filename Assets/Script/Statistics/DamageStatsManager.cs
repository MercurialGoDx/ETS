using System.Collections.Generic;
using UnityEngine;

public readonly struct DamageStatEntry
{
    public DamageStatEntry(WeaponDefinition weapon, UpgradeBaseSO upgrade, float damage)
    {
        Weapon = weapon;
        Upgrade = upgrade;
        Damage = damage;
    }

    public WeaponDefinition Weapon { get; }
    public UpgradeBaseSO Upgrade { get; }
    public float Damage { get; }
    public Sprite Icon => Weapon != null ? Weapon.icon : Upgrade != null ? Upgrade.icon : null;

    public string GetLocalizedName()
    {
        if (Weapon != null)
            return Weapon.GetLocalizedName();
        if (Upgrade != null)
            return Upgrade.GetLocalizedName();

        return string.Empty;
    }
}

public class DamageStatsManager : MonoBehaviour
{
    public static DamageStatsManager Instance { get; private set; }

    private readonly Dictionary<WeaponDefinition, float> damageByWeapon = new();
    private readonly Dictionary<UpgradeBaseSO, float> damageByUpgrade = new();

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

    public void RegisterDamage(UpgradeBaseSO upgrade, float damage)
    {
        if (upgrade == null)
            return;

        if (!damageByUpgrade.ContainsKey(upgrade))
            damageByUpgrade[upgrade] = 0f;

        damageByUpgrade[upgrade] += damage;
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

    public List<DamageStatEntry> GetAllDamageSorted()
    {
        var result = new List<DamageStatEntry>(damageByWeapon.Count + damageByUpgrade.Count);

        foreach (var pair in damageByWeapon)
            result.Add(new DamageStatEntry(pair.Key, null, pair.Value));

        foreach (var pair in damageByUpgrade)
            result.Add(new DamageStatEntry(null, pair.Key, pair.Value));

        result.Sort((a, b) => b.Damage.CompareTo(a.Damage));
        return result;
    }

    public void ResetStats()
    {
        damageByWeapon.Clear();
        damageByUpgrade.Clear();
    }
}
