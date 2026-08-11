using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class UpgradesRuntimeData
{
    public int goldUpgradeCount;

    public float damageFromMaxHealthPercent;

    public float[] damageTypeMultipliers;
    public int[] damageTypeStacks;

    public EnemySpawner enemySpawner;

    public float globalDamagePercent; // +% ко всем типам
    public float generatorDamagePercent; // +% со временем
    public float totalGeneratorDamagePercent;
    public float damageWhileShieldActivePercent; // +% при щите


    public float damagePerValueHpPercent;
    public float damagePerValueGoldPercent;

    public Dictionary<ItemTier, float> damageTierPercent = new();
    public Dictionary<WeaponDamageType, float> damageTypeFlatPercent = new();
    public Dictionary<WeaponDamageType, float> damageTypePerWeaponPercent = new();

    public UpgradesRuntimeData()
    {
        // Засеиваем ключи заранее: апгрейды пишут через `dict[key] += x`,
        // что на отсутствующем ключе бросает KeyNotFoundException.
        foreach (WeaponDamageType t in System.Enum.GetValues(typeof(WeaponDamageType)))
        {
            damageTypeFlatPercent[t] = 0f;
            damageTypePerWeaponPercent[t] = 0f;
        }

        foreach (ItemTier t in System.Enum.GetValues(typeof(ItemTier)))
            damageTierPercent[t] = 0f;
    }

    private Dictionary<UpgradeBaseSO, int> upgradePurchaseCounts = new();
    private Dictionary<WeaponDefinition, int> weaponPurchaseCounts = new();

    /// <summary>
    /// Купленные улучшения и сколько раз каждое бралось. Наполняется в
    /// <see cref="RegisterUpgradePurchase"/>, то есть учитывает и магазин, и награды
    /// за боссов (оба пути идут через UpgradesManager.ApplyUpgrade). Только чтение.
    /// </summary>
    public IReadOnlyDictionary<UpgradeBaseSO, int> UpgradePurchaseCounts => upgradePurchaseCounts;

    // Отслеживание модификаторов веса от купленных апгрейдов/оружий
    // Ключ: целевой апгрейд/оружие, Значение: список (источник модификатора, процент, количество применений)
    private Dictionary<UpgradeBaseSO, List<WeightModifier>> upgradeWeightModifiers = new();
    private Dictionary<WeaponDefinition, List<WeightModifier>> weaponWeightModifiers = new();

    public void Initialize()
    {
        upgradeWeightModifiers = new Dictionary<UpgradeBaseSO, List<WeightModifier>>();
        weaponWeightModifiers = new Dictionary<WeaponDefinition, List<WeightModifier>>();
    }

    public float GetUpgradeWeight(UpgradeBaseSO upgrade)
    {
        if (upgrade == null)
            return 0;

        int purchases = upgradePurchaseCounts.TryGetValue(upgrade, out var count)
            ? count
            : 0;

        int effectivePurchases = Mathf.Min(purchases, upgrade.maxWeightIncreasePurchases);

        // Базовый вес с учетом индивидуального увеличения
        float baseWeight = upgrade.weight + effectivePurchases * upgrade.weightIncreasePerPurchase;

        // Применяем модификаторы веса
        float finalWeight = ApplyUpgradeWeightModifiers(upgrade, baseWeight);

        return Mathf.Max(0, finalWeight);
    }

    // ---------- Weapon Weight ----------

    public float GetWeaponWeight(WeaponDefinition weapon)
    {
        if (weapon == null)
            return 0;

        int purchases = weaponPurchaseCounts.TryGetValue(weapon, out var count)
            ? count
            : 0;

        int effectivePurchases = Mathf.Min(purchases, weapon.maxWeightIncreasePurchases);

        // Базовый вес с учетом индивидуального увеличения
        float baseWeight = weapon.weight + effectivePurchases * weapon.weightIncreasePerPurchase;

        // Применяем модификаторы веса
        float finalWeight = ApplyWeaponWeightModifiers(weapon, baseWeight);

        return Mathf.Max(0, finalWeight);
    }

    // ---------- Применение модификаторов веса ----------

    private float ApplyUpgradeWeightModifiers(UpgradeBaseSO targetUpgrade, float baseWeight)
    {
        if (!upgradeWeightModifiers.ContainsKey(targetUpgrade))
            return baseWeight;

        var modifiers = upgradeWeightModifiers[targetUpgrade];
        float totalPercent = 0f;

        foreach (var modifier in modifiers)
        {
            // Ограничение применений для каждого модификатора (из целевого апгрейда)
            int effectiveApplications = Mathf.Min(modifier.applications, targetUpgrade.maxWeightIncreasePurchases);
            totalPercent += modifier.percent * effectiveApplications;
        }

        return baseWeight * (1f + totalPercent / 100f);
    }

    private float ApplyWeaponWeightModifiers(WeaponDefinition targetWeapon, float baseWeight)
    {
        if (!weaponWeightModifiers.ContainsKey(targetWeapon))
            return baseWeight;

        var modifiers = weaponWeightModifiers[targetWeapon];
        float totalPercent = 0f;

        foreach (var modifier in modifiers)
        {
            // Ограничение применений для каждого модификатора (из целевого оружия)
            int effectiveApplications = Mathf.Min(modifier.applications, targetWeapon.maxWeightIncreasePurchases);
            totalPercent += modifier.percent * effectiveApplications;
        }

        return baseWeight * (1f + totalPercent / 100f);
    }

    // ---------- Регистрация модификаторов веса при покупке ----------

    private void RegisterUpgradeWeightModifiers(UpgradeBaseSO purchasedUpgrade)
    {
        if (purchasedUpgrade == null)
            return;

        // Уменьшение веса для других апгрейдов
        if (purchasedUpgrade.weightDecreaseUpgrades != null)
        {
            foreach (var targetUpgrade in purchasedUpgrade.weightDecreaseUpgrades)
            {
                if (targetUpgrade == null) continue;

                if (!upgradeWeightModifiers.ContainsKey(targetUpgrade))
                    upgradeWeightModifiers[targetUpgrade] = new List<WeightModifier>();

                // Проверяем, есть ли уже такой модификатор от этого источника
                var existingModifier = upgradeWeightModifiers[targetUpgrade]
                    .Find(m => m.sourceUpgrade == purchasedUpgrade);

                if (existingModifier != null)
                {
                    existingModifier.applications++;
                }
                else
                {
                    upgradeWeightModifiers[targetUpgrade].Add(new WeightModifier
                    {
                        sourceUpgrade = purchasedUpgrade,
                        percent = -purchasedUpgrade.weightDecreasePercent,
                        applications = 1
                    });
                }
            }
        }

        // Увеличение веса для других апгрейдов
        if (purchasedUpgrade.weightIncreaseUpgrades != null)
        {
            foreach (var targetUpgrade in purchasedUpgrade.weightIncreaseUpgrades)
            {
                if (targetUpgrade == null) continue;

                if (!upgradeWeightModifiers.ContainsKey(targetUpgrade))
                    upgradeWeightModifiers[targetUpgrade] = new List<WeightModifier>();

                var existingModifier = upgradeWeightModifiers[targetUpgrade]
                    .Find(m => m.sourceUpgrade == purchasedUpgrade);

                if (existingModifier != null)
                {
                    existingModifier.applications++;
                }
                else
                {
                    upgradeWeightModifiers[targetUpgrade].Add(new WeightModifier
                    {
                        sourceUpgrade = purchasedUpgrade,
                        percent = purchasedUpgrade.weightIncreasePercent,
                        applications = 1
                    });
                }
            }
        }

        // Уменьшение веса для оружий
        if (purchasedUpgrade.weightDecreaseWeapons != null)
        {
            foreach (var targetWeapon in purchasedUpgrade.weightDecreaseWeapons)
            {
                if (targetWeapon == null) continue;

                if (!weaponWeightModifiers.ContainsKey(targetWeapon))
                    weaponWeightModifiers[targetWeapon] = new List<WeightModifier>();

                var existingModifier = weaponWeightModifiers[targetWeapon]
                    .Find(m => m.sourceUpgrade == purchasedUpgrade);

                if (existingModifier != null)
                {
                    existingModifier.applications++;
                }
                else
                {
                    weaponWeightModifiers[targetWeapon].Add(new WeightModifier
                    {
                        sourceUpgrade = purchasedUpgrade,
                        percent = -purchasedUpgrade.weightDecreaseWeaponPercent,
                        applications = 1
                    });
                }
            }
        }

        // Увеличение веса для оружий
        if (purchasedUpgrade.weightIncreaseWeapons != null)
        {
            foreach (var targetWeapon in purchasedUpgrade.weightIncreaseWeapons)
            {
                if (targetWeapon == null) continue;

                if (!weaponWeightModifiers.ContainsKey(targetWeapon))
                    weaponWeightModifiers[targetWeapon] = new List<WeightModifier>();

                var existingModifier = weaponWeightModifiers[targetWeapon]
                    .Find(m => m.sourceUpgrade == purchasedUpgrade);

                if (existingModifier != null)
                {
                    existingModifier.applications++;
                }
                else
                {
                    weaponWeightModifiers[targetWeapon].Add(new WeightModifier
                    {
                        sourceUpgrade = purchasedUpgrade,
                        percent = purchasedUpgrade.weightIncreaseWeaponPercent,
                        applications = 1
                    });
                }
            }
        }
    }

    private void RegisterWeaponWeightModifiers(WeaponDefinition purchasedWeapon)
    {
        if (purchasedWeapon == null)
            return;

        // Уменьшение веса для апгрейдов
        if (purchasedWeapon.weightDecreaseUpgrades != null)
        {
            foreach (var targetUpgrade in purchasedWeapon.weightDecreaseUpgrades)
            {
                if (targetUpgrade == null) continue;

                if (!upgradeWeightModifiers.ContainsKey(targetUpgrade))
                    upgradeWeightModifiers[targetUpgrade] = new List<WeightModifier>();

                var existingModifier = upgradeWeightModifiers[targetUpgrade]
                    .Find(m => m.sourceWeapon == purchasedWeapon);

                if (existingModifier != null)
                {
                    existingModifier.applications++;
                }
                else
                {
                    upgradeWeightModifiers[targetUpgrade].Add(new WeightModifier
                    {
                        sourceWeapon = purchasedWeapon,
                        percent = -purchasedWeapon.weightDecreaseUpgradePercent,
                        applications = 1
                    });
                }
            }
        }

        // Увеличение веса для апгрейдов
        if (purchasedWeapon.weightIncreaseUpgrades != null)
        {
            foreach (var targetUpgrade in purchasedWeapon.weightIncreaseUpgrades)
            {
                if (targetUpgrade == null) continue;

                if (!upgradeWeightModifiers.ContainsKey(targetUpgrade))
                    upgradeWeightModifiers[targetUpgrade] = new List<WeightModifier>();

                var existingModifier = upgradeWeightModifiers[targetUpgrade]
                    .Find(m => m.sourceWeapon == purchasedWeapon);

                if (existingModifier != null)
                {
                    existingModifier.applications++;
                }
                else
                {
                    upgradeWeightModifiers[targetUpgrade].Add(new WeightModifier
                    {
                        sourceWeapon = purchasedWeapon,
                        percent = purchasedWeapon.weightIncreaseUpgradePercent,
                        applications = 1
                    });
                }
            }
        }

        // Уменьшение веса для других оружий
        if (purchasedWeapon.weightDecreaseWeapons != null)
        {
            foreach (var targetWeapon in purchasedWeapon.weightDecreaseWeapons)
            {
                if (targetWeapon == null) continue;

                if (!weaponWeightModifiers.ContainsKey(targetWeapon))
                    weaponWeightModifiers[targetWeapon] = new List<WeightModifier>();

                var existingModifier = weaponWeightModifiers[targetWeapon]
                    .Find(m => m.sourceWeapon == purchasedWeapon);

                if (existingModifier != null)
                {
                    existingModifier.applications++;
                }
                else
                {
                    weaponWeightModifiers[targetWeapon].Add(new WeightModifier
                    {
                        sourceWeapon = purchasedWeapon,
                        percent = -purchasedWeapon.weightDecreaseWeaponPercent,
                        applications = 1
                    });
                }
            }
        }

        // Увеличение веса для других оружий
        if (purchasedWeapon.weightIncreaseWeapons != null)
        {
            foreach (var targetWeapon in purchasedWeapon.weightIncreaseWeapons)
            {
                if (targetWeapon == null) continue;

                if (!weaponWeightModifiers.ContainsKey(targetWeapon))
                    weaponWeightModifiers[targetWeapon] = new List<WeightModifier>();

                var existingModifier = weaponWeightModifiers[targetWeapon]
                    .Find(m => m.sourceWeapon == purchasedWeapon);

                if (existingModifier != null)
                {
                    existingModifier.applications++;
                }
                else
                {
                    weaponWeightModifiers[targetWeapon].Add(new WeightModifier
                    {
                        sourceWeapon = purchasedWeapon,
                        percent = purchasedWeapon.weightIncreaseWeaponPercent,
                        applications = 1
                    });
                }
            }
        }
    }

    // ---------- Register Purchases ----------

    public void RegisterUpgradePurchase(UpgradeBaseSO upgrade)
    {
        if (upgrade == null)
            return;

        if (!upgradePurchaseCounts.ContainsKey(upgrade))
            upgradePurchaseCounts[upgrade] = 0;

        upgradePurchaseCounts[upgrade]++;

        // Регистрируем модификаторы веса от этого апгрейда
        RegisterUpgradeWeightModifiers(upgrade);
    }

    public void RegisterWeaponPurchase(WeaponDefinition weapon)
    {
        if (weapon == null)
            return;

        if (!weaponPurchaseCounts.ContainsKey(weapon))
            weaponPurchaseCounts[weapon] = 0;

        weaponPurchaseCounts[weapon]++;

        // Регистрируем модификаторы веса от этого оружия
        RegisterWeaponWeightModifiers(weapon);
    }
}

[System.Serializable]
public class WeightModifier
{
    public UpgradeBaseSO sourceUpgrade;  // Источник модификатора (апгрейд)
    public WeaponDefinition sourceWeapon; // Источник модификатора (оружие)
    public float percent;                 // Процент изменения (-5 = -5%, 7 = +7%)
    public int applications;              // Сколько раз применен этот модификатор
}
