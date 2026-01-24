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
    
    [Header("Тип урона")]
    public WeaponDamageType damageType;

    // Префаб снаряда, который использует TowerAttack (bulletPrefab)
    public GameObject bulletPrefab;

    [Header("Шанс появления в магазине")]
    public int weight = 1;       // используется в рандомизации слотов

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
