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

    public float damagePercent;
    public float globalDamagePercent;
    public float globalDamagePer100GoldPercent;
    public float generatorDamagePercent; // +% со временем
    public float totalGeneratorDamagePercent;
    public float adaptiveDamageWhileShieldPercent;


    public float damagePerValueHpPercent;
    public float damagePerValueGoldPercent;

    public Dictionary<ItemTier, float> damageTierPercent = new();
    public Dictionary<WeaponDamageType, float> damageTypeFlatPercent = new();
    public Dictionary<WeaponDamageType, float> damageTypePerWeaponPercent = new();

    private float goldenEnemyChance;
    private int huntStacks;
    private float huntDiminishingFactor = 0.9f;
    private float goldenEnemyHealthMultiplier = 1f;
    private float goldenEnemyDamageMultiplier = 1f;
    private float goldenEnemyGoldMultiplier = 1f;
    private Color goldenEnemyTint = new Color(1f, 0.72f, 0.2f, 1f);
    private float goldenEnemyTintStrength = 0.35f;

    private bool isDuplicatorArmed;
    private ItemTier duplicatorTier1 = ItemTier.None;
    private int duplicatorCopies1;
    private ItemTier duplicatorTier2 = ItemTier.None;
    private int duplicatorCopies2;

    private int pendingBossContractStacks;
    private float pendingBossContractHealthMultiplier;
    private float pendingBossContractDamageMultiplier;
    private Material pendingBossContractOutlineMaterial;
    private Color pendingBossContractOutlineColor = new(1f, 0.02f, 0f, 1f);
    private float pendingBossContractOutlineWidth;
    private float pendingBossContractGlowIntensity;
    private float pendingBossContractScaleMultiplier = 1f;

    public float GoldenEnemyChance => goldenEnemyChance;
    public int HuntStacks => huntStacks;
    public float HuntDiminishingFactor => huntDiminishingFactor;
    public float GoldenEnemyHealthMultiplier => goldenEnemyHealthMultiplier;
    public float GoldenEnemyDamageMultiplier => goldenEnemyDamageMultiplier;
    public float GoldenEnemyGoldMultiplier => goldenEnemyGoldMultiplier;
    public bool IsDuplicatorArmed => isDuplicatorArmed;
    public ItemTier DuplicatorTier1 => duplicatorTier1;
    public int DuplicatorCopies1 => duplicatorCopies1;
    public ItemTier DuplicatorTier2 => duplicatorTier2;
    public int DuplicatorCopies2 => duplicatorCopies2;
    public int PendingBossContractStacks => pendingBossContractStacks;
    public float PendingBossContractHealthMultiplier => pendingBossContractHealthMultiplier;
    public float PendingBossContractDamageMultiplier => pendingBossContractDamageMultiplier;

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

    public void AddHunt(
        float chancePercent,
        float diminishingFactor,
        float healthMultiplier,
        float damageMultiplier,
        float goldMultiplier,
        Color tint,
        float tintStrength)
    {
        huntStacks++;
        huntDiminishingFactor = Mathf.Clamp01(diminishingFactor);

        float firstIncrease = Mathf.Max(0f, chancePercent) / 100f;
        if (Mathf.Approximately(huntDiminishingFactor, 1f))
        {
            goldenEnemyChance = Mathf.Clamp01(firstIncrease * huntStacks);
        }
        else
        {
            goldenEnemyChance = Mathf.Clamp01(
                firstIncrease *
                (1f - Mathf.Pow(huntDiminishingFactor, huntStacks)) /
                (1f - huntDiminishingFactor));
        }

        goldenEnemyHealthMultiplier = Mathf.Max(goldenEnemyHealthMultiplier, Mathf.Max(healthMultiplier, 1f));
        goldenEnemyDamageMultiplier = Mathf.Max(goldenEnemyDamageMultiplier, Mathf.Max(damageMultiplier, 1f));
        goldenEnemyGoldMultiplier = Mathf.Max(goldenEnemyGoldMultiplier, Mathf.Max(goldMultiplier, 1f));
        goldenEnemyTint = tint;
        goldenEnemyTintStrength = Mathf.Clamp01(tintStrength);
    }

    public bool TryGetHuntModifiers(
        out float healthMultiplier,
        out float damageMultiplier,
        out float goldMultiplier,
        out Color tint,
        out float tintStrength)
    {
        healthMultiplier = goldenEnemyHealthMultiplier;
        damageMultiplier = goldenEnemyDamageMultiplier;
        goldMultiplier = goldenEnemyGoldMultiplier;
        tint = goldenEnemyTint;
        tintStrength = goldenEnemyTintStrength;
        return goldenEnemyChance > 0f;
    }

    /// <summary>
    /// Армит оба слота одновременно (разные тиры или один и тот же — на выбор апгрейда).
    /// Слот срабатывает от покупки предмета своего тира; какой из двух сработает первым,
    /// тот и даёт копии — оба слота гаснут одновременно (см. TryConsumeDuplicator).
    /// </summary>
    public bool TryArmDuplicator(ItemTier tier1, int copies1, ItemTier tier2, int copies2)
    {
        if (isDuplicatorArmed)
            return false;
        if (tier1 == ItemTier.None || copies1 <= 0)
            return false;
        if (tier2 == ItemTier.None || copies2 <= 0)
            return false;

        isDuplicatorArmed = true;
        duplicatorTier1 = tier1;
        duplicatorCopies1 = copies1;
        duplicatorTier2 = tier2;
        duplicatorCopies2 = copies2;
        return true;
    }

    /// <summary>
    /// Проверяет купленный тир против обоих слотов. Срабатывает только один (первый
    /// совпавший), но заряд одноразовый целиком — второй слот гаснет вместе с ним,
    /// даже если сам не сработал.
    /// </summary>
    public bool TryConsumeDuplicator(ItemTier purchasedTier, out int copies)
    {
        copies = 0;
        if (!isDuplicatorArmed)
            return false;

        if (purchasedTier == duplicatorTier1)
            copies = duplicatorCopies1;
        else if (purchasedTier == duplicatorTier2)
            copies = duplicatorCopies2;
        else
            return false;

        isDuplicatorArmed = false;
        duplicatorTier1 = ItemTier.None;
        duplicatorCopies1 = 0;
        duplicatorTier2 = ItemTier.None;
        duplicatorCopies2 = 0;
        return copies > 0;
    }

    public void AddBossContract(
        float healthMultiplier,
        float damageMultiplier,
        Material outlineMaterial,
        Color outlineColor,
        float outlineWidth,
        float glowIntensity,
        float scaleMultiplier)
    {
        pendingBossContractStacks++;
        pendingBossContractHealthMultiplier += Mathf.Max(1f, healthMultiplier);
        pendingBossContractDamageMultiplier += Mathf.Max(1f, damageMultiplier);
        pendingBossContractOutlineMaterial = outlineMaterial;
        pendingBossContractOutlineColor = outlineColor;
        pendingBossContractOutlineWidth = Mathf.Max(
            pendingBossContractOutlineWidth,
            Mathf.Max(0f, outlineWidth));
        pendingBossContractGlowIntensity = Mathf.Max(
            pendingBossContractGlowIntensity,
            Mathf.Max(0f, glowIntensity));
        pendingBossContractScaleMultiplier = Mathf.Max(
            pendingBossContractScaleMultiplier,
            Mathf.Max(1f, scaleMultiplier));
    }

    public bool TryConsumeBossContract(
        out int stacks,
        out float healthMultiplier,
        out float damageMultiplier,
        out Material outlineMaterial,
        out Color outlineColor,
        out float outlineWidth,
        out float glowIntensity,
        out float scaleMultiplier)
    {
        stacks = pendingBossContractStacks;
        healthMultiplier = pendingBossContractHealthMultiplier;
        damageMultiplier = pendingBossContractDamageMultiplier;
        outlineMaterial = pendingBossContractOutlineMaterial;
        outlineColor = pendingBossContractOutlineColor;
        outlineWidth = pendingBossContractOutlineWidth;
        glowIntensity = pendingBossContractGlowIntensity;
        scaleMultiplier = pendingBossContractScaleMultiplier;

        pendingBossContractStacks = 0;
        pendingBossContractHealthMultiplier = 0f;
        pendingBossContractDamageMultiplier = 0f;
        pendingBossContractOutlineMaterial = null;
        pendingBossContractOutlineWidth = 0f;
        pendingBossContractGlowIntensity = 0f;
        pendingBossContractScaleMultiplier = 1f;
        return stacks > 0;
    }

    private Dictionary<UpgradeBaseSO, int> upgradePurchaseCounts = new();
    private Dictionary<UpgradeBaseSO, int> bossRewardCounts = new();
    private Dictionary<WeaponDefinition, int> weaponPurchaseCounts = new();

    /// <summary>
    /// Купленные улучшения и сколько раз каждое бралось. Наполняется в
    /// <see cref="RegisterUpgradePurchase"/>, то есть учитывает и магазин, и награды
    /// за боссов (оба пути идут через UpgradesManager.ApplyUpgrade). Только чтение.
    /// </summary>
    public IReadOnlyDictionary<UpgradeBaseSO, int> UpgradePurchaseCounts => upgradePurchaseCounts;
    public IReadOnlyDictionary<UpgradeBaseSO, int> BossRewardCounts => bossRewardCounts;

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

    public void RegisterBossReward(UpgradeBaseSO reward)
    {
        if (reward == null)
            return;

        RegisterUpgradePurchase(reward);

        if (!bossRewardCounts.ContainsKey(reward))
            bossRewardCounts[reward] = 0;

        bossRewardCounts[reward]++;
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
