using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

public enum WeaponTargetingMode
{
    LockUntilDeath,   // стреляет в одну цель, пока она не умрёт / не выйдет из range
    RandomEachShot    // каждый выстрел выбирает новую цель
}

[CreateAssetMenu(fileName = "Weapon", menuName = "TD/Weapon")]
public class WeaponDefinition : ScriptableObject
{
    [Header("Основное")]
    public Sprite icon;

    [Header("Название с локализацией")]
    public LocalizedStringTable localizedStringTable;
    public string nameKey;
    public string descriptionKey;

    [Header("Характеристики")]
    public int price = 10;
    public float damagePerProjectile = 5f;
    public float fireRate = 1f;
    public float projectileSpeed = 10f;

    [Header("Тир оружия")]
    public ItemTier itemTier = ItemTier.Tier1;

    [Header("Тип урона")]
    public WeaponDamageType damageType;

    // Префаб снаряда, который использует TowerAttack (bulletPrefab)
    public GameObject bulletPrefab;

    [Header("Шанс появления в магазине")]
    public int weight = 1;       // используется в рандомизации слотов

    [Header("Weight Scaling")]
    [Tooltip("На сколько увеличивается вес при каждой покупке этого оружия")]
    public int weightIncreasePerPurchase = 2;
    [Tooltip("Максимальное количество увеличений веса при покупках")]
    public int maxWeightIncreasePurchases = 5;

    [Header("Weight Modifiers")]
    [Tooltip("Список апгрейдов, вес которых уменьшается при покупке этого оружия")]
    public List<UpgradeBaseSO> weightDecreaseUpgrades = new List<UpgradeBaseSO>();
    [Tooltip("Процент уменьшения веса для каждого апгрейда из списка выше (5 = -5% к весу)")]
    public float weightDecreaseUpgradePercent = 0f;

    [Tooltip("Список апгрейдов, вес которых увеличивается при покупке этого оружия")]
    public List<UpgradeBaseSO> weightIncreaseUpgrades = new List<UpgradeBaseSO>();
    [Tooltip("Процент увеличения веса для каждого апгрейда из списка выше (7 = +7% к весу)")]
    public float weightIncreaseUpgradePercent = 0f;

    [Tooltip("Список оружий, вес которых уменьшается при покупке этого оружия")]
    public List<WeaponDefinition> weightDecreaseWeapons = new List<WeaponDefinition>();
    [Tooltip("Процент уменьшения веса для каждого оружия из списка выше (5 = -5% к весу)")]
    public float weightDecreaseWeaponPercent = 0f;

    [Tooltip("Список оружий, вес которых увеличивается при покупке этого оружия")]
    public List<WeaponDefinition> weightIncreaseWeapons = new List<WeaponDefinition>();
    [Tooltip("Процент увеличения веса для каждого оружия из списка выше (7 = +7% к весу)")]
    public float weightIncreaseWeaponPercent = 0f;

    [Header("Поведение наведения")]
    public WeaponTargetingMode targetingMode = WeaponTargetingMode.LockUntilDeath;


    public virtual string GetLocalizedName()
    {
        // Получаем таблицу для текущей локали
        var stringTable = localizedStringTable.GetTable();
        if (stringTable == null)
        {
            Debug.LogError($"Localization table not found for upgrade: {name}");
            return nameKey;
        }

        // Получаем строку по ключу
        var entry = stringTable.GetEntry(nameKey);
        return entry?.GetLocalizedString() ?? nameKey;
    }

    public virtual string GetLocalizedDescription()
    {
        var stringTable = localizedStringTable.GetTable();
        if (stringTable == null)
        {
            Debug.LogError($"Localization table not found for upgrade: {name}");
            return descriptionKey;
        }

        var entry = stringTable.GetEntry(descriptionKey);
        if (entry == null) return descriptionKey;

        return entry.GetLocalizedString(GetDescriptionArgs());
    }

    protected virtual object[] GetDescriptionArgs()
    {
        return new object[] { damagePerProjectile, damageType, fireRate, price };
    }
}
