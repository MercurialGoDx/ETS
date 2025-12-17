using UnityEngine;

public class UpgradesManager : MonoBehaviour
{
    public static UpgradesManager Instance { get; private set; }

    [Header("Ссылки")]
    public PlayerHealth playerHealth;
    public TowerAttack towerAttack;
    [Header("Прогресс золота")]
    public int goldUpgradeCount = 0;
    [Header("Урон от здоровья игрока")]
    [Tooltip("Дополнительный урон от максимального здоровья игрока (0.1 = 10%)")]
    public float damageFromMaxHealthPercent = 0f;
    [Header("Множители урона по типам оружия")]
    public float[] damageTypeMultipliers = new float[5];   // 5 типов урона
    public int[] damageTypeStacks = new int[5];            // сколько раз брали апгрейд по каждому типу
    [Header("Бонус урона при активном щите")]
    [Tooltip("Суммарный бонус к урону (%) пока щит активен.")]
    public float damageWhileShieldActivePercent = 0f;
    [Header("Шипы — накапливаемые за убийства шипами")]
    [Tooltip("Сколько добавлять к шипам за каждое убийство шипами.")]
    public float spikesOnKillBonus = 0f;
    public float GetDamageTypeMultiplier(WeaponDamageType type)
    {
        int index = (int)type;
        if (damageTypeMultipliers == null || index < 0 || index >= damageTypeMultipliers.Length)
            return 1f;

        return damageTypeMultipliers[index];
    }


    [Header("Хил за убийство")]
    public float healOnKillPerEnemy = 0f;  // сколько ХП лечим за 1 убитого врага\
    [Header("Реген за недостающее здоровье")]
    [Tooltip("Сколько регена в секунду даётся за каждые 100 недостающего HP.")]
    public float regenPer100MissingHealth = 0f;
    [Header("Глобальный урон по всем врагам от регена")]
    [Tooltip("Включить/выключить ауру урона от регена (включится при покупке апгрейда).")]
    public bool regenAuraEnabled = false;

    [Tooltip("Множитель урона от суммарного регена (2 = урон в 2 раза больше регена).")]
    public float regenAuraMultiplier = 0f;

    [Tooltip("Интервал между тиками урона по всем врагам (секунды).")]
    public float regenAuraTickInterval = 1f;

    private float regenAuraTimer = 0f;
    public int goldBonusPerKill = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        InitDamageTypeArrays();
    }

    private void Update()
    {
        // ... если у тебя тут уже что-то есть — оставляем ...

        HandleRegenAuraDamage();
    }
    private void HandleRegenAuraDamage()
    {
        if (!regenAuraEnabled)
            return;

        if (regenAuraMultiplier <= 0f)
            return;

        if (playerHealth == null)
            return;

        regenAuraTimer += Time.deltaTime;
        if (regenAuraTimer < regenAuraTickInterval)
            return;

        regenAuraTimer = 0f;
        ApplyRegenAuraDamage();
    }
    private void ApplyRegenAuraDamage()
    {
        // 1) Считаем общий реген игрока в секунду
        float baseRegen = playerHealth.healthRegenPerSecond; // базовый реген из апгрейдов

        float bonusRegen = 0f;
        // если мы делали улучшение "реген за недостающее здоровье"
        if (regenPer100MissingHealth > 0f)
        {
            float missing = playerHealth.MaxHealth - playerHealth.CurrentHealth;
            if (missing > 0f)
            {
                bonusRegen = regenPer100MissingHealth * (missing / 100f);
            }
        }

        float totalRegen = baseRegen + bonusRegen;
        if (totalRegen <= 0f)
            return;

        // 2) Считаем урон от ауры
        float damagePerEnemy = totalRegen * regenAuraMultiplier;

        // 3) Наносим урон всем врагам на сцене
        Enemy[] enemies = Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);

        if (enemies.Length == 0)
            return;

        foreach (var e in enemies)
        {
            if (e == null) continue;
            e.TakeDamage(damagePerEnemy);
        }

        Debug.Log($"[RegenAura] Tick: regen={totalRegen:F1}, mult={regenAuraMultiplier:F2}, " +
                  $"damage={damagePerEnemy:F1}, enemies={enemies.Length}");
    }

    public float GetShieldDamageBonusMultiplier()
    {
        // нет апгрейда — нет бонуса
        if (damageWhileShieldActivePercent <= 0f)
            return 1f;

        // нет playerHealth — подстраховка
        if (playerHealth == null)
            return 1f;

        // щит не активен → бонус не работает
        if (!playerHealth.IsShieldActive)
            return 1f;

        // есть апгрейд и щит активен
        float percent = damageWhileShieldActivePercent / 100f; // 10 → 0.1
        return 1f + percent; // 10% → 1.1, 20% → 1.2 и т.д.
    }

    /// <summary>
    /// Вызывается, когда враг умер именно от урона шипов.
    /// </summary>
    public void OnEnemyKilledBySpikes()
    {
        if (playerHealth == null) return;
        if (spikesOnKillBonus <= 0f) return;

        playerHealth.AddSpikesDamage(spikesOnKillBonus);

        Debug.Log($"[SpikesScaling] Enemy killed by spikes. +{spikesOnKillBonus} spikes. Current spikes damage = {playerHealth.SpikesDamage}");
    }

    private void InitDamageTypeArrays()
    {
        // сколько значений в enum WeaponDamageType (Magic, Chaos, Slash, Heavy и т.д.)
        int typeCount = System.Enum.GetValues(typeof(WeaponDamageType)).Length;

        // если ещё не создано или неверная длина — создаём заново
        if (damageTypeMultipliers == null || damageTypeMultipliers.Length != typeCount)
        {
            damageTypeMultipliers = new float[typeCount];
            damageTypeStacks = new int[typeCount];
        }

        // базовый множитель = 1 для каждого типа
        for (int i = 0; i < typeCount; i++)
        {
            if (damageTypeMultipliers[i] <= 0f)
                damageTypeMultipliers[i] = 1f;

            // стеки можно оставить как есть, но на всякий случай
            if (damageTypeStacks[i] < 0)
                damageTypeStacks[i] = 0;
        }
    }
    public void ApplyUpgrade(UpgradeDefinition upgrade)
    {
        if (upgrade == null)
        {
            Debug.LogWarning("UpgradesManager.ApplyUpgrade: upgrade is null");
            return;
        }

        switch (upgrade.type)
        {
            // 🔥 Глобальная скорость атаки
            case UpgradeType.GlobalFireRate:
                {
                    if (towerAttack == null) break;

                    float percent = upgrade.valuePercent / 100f; // 10 → 0.1
                    towerAttack.fireRateMultiplier += percent;   // 1.0 → 1.1 → 1.2 → 1.3...
                    break;
                }

            // ❤️ ХП
            case UpgradeType.MaxHealthFlat:
                {
                    if (playerHealth == null) break;
                    playerHealth.AddFlatMaxHealthAndHeal(upgrade.valueFlat);
                    break;
                }

            case UpgradeType.MaxHealthPercent:
                {
                    if (playerHealth == null) break;
                    float percent = upgrade.valuePercent / 100f;
                    playerHealth.AddMaxHealthMultiplier(percent);
                    break;
                }

            case UpgradeType.HealthRegen:
                {
                    if (playerHealth == null) break;
                    playerHealth.AddHealthRegen(upgrade.valueFlat);
                    break;
                }

            // 🗡 Шипы
            case UpgradeType.SpikesBase:
                {
                    if (playerHealth == null) break;
                    playerHealth.AddSpikesDamage(upgrade.valueFlat);
                    break;
                }

            case UpgradeType.SpikesPercent:
                {
                    if (playerHealth == null) break;
                    float percent = upgrade.valuePercent / 100f; // 10 → 0.1
                    playerHealth.AddSpikesPercent(percent);
                    break;
                }

            // 🛡 Щит
            case UpgradeType.ShieldMax:
                {
                    if (playerHealth == null) break;
                    playerHealth.AddMaxShield(upgrade.valueFlat);
                    break;
                }

            case UpgradeType.ShieldPercent:
                {
                    if (playerHealth == null) break;
                    float percent = upgrade.valuePercent / 100f; // 100 → 1.0 (x2), 10 → 0.1 (+10%)
                    playerHealth.AddShieldPercent(percent);
                    break;
                }

            // 💰 Пассивный доход золота
            case UpgradeType.GoldPerSecond:
                {
                    if (GoldManager.Instance != null)
                    {
                        // valueFlat = сколько золота/сек даёт один уровень апгрейда
                        GoldManager.Instance.AddPassiveIncome((int)upgrade.valueFlat);
                    }
                    goldUpgradeCount++;
                    break;
                }
            case UpgradeType.GoldGainPercent:
                {
                    if (GoldManager.Instance != null)
                    {
                        GoldManager.Instance.AddGoldGainPercent(upgrade.valuePercent);
                    }
                    break;
                }

            // 💖 Хил за убийство врага
            case UpgradeType.HealOnKill:
                {
                    // каждый апгрейд добавляет, например, 10
                    healOnKillPerEnemy += Mathf.Max(0f, upgrade.valueFlat);
                    Debug.Log($"HealOnKill upgrade applied. Now healOnKillPerEnemy = {healOnKillPerEnemy}");
                    break;
                }

            // 🗡 + 💖 Комбинированный апгрейд: шипы + хил при получении урона по ХП
            case UpgradeType.AddSpikesAndHealOnHitFromEnemy:
                {
                    if (playerHealth == null) break;

                    // valueFlat  -> урон шипов
                    // extraFlat  -> хил за удар по ХП
                    if (upgrade.valueFlat != 0f)
                        playerHealth.AddSpikesDamage(upgrade.valueFlat);

                    if (upgrade.extraFlat != 0f)
                        playerHealth.AddHealOnHitFromEnemy(upgrade.extraFlat);

                    break;
                }
            case UpgradeType.SpikesScalingOnKill:
                {
                    if (playerHealth == null) break;

                    // valueFlat — базовое добавление шипов при покупке
                    if (upgrade.valueFlat != 0f)
                        playerHealth.AddSpikesDamage(upgrade.valueFlat);

                    // extraFlat — сколько добавлять к шипам за каждое убийство шипами
                    if (upgrade.extraFlat != 0f)
                        spikesOnKillBonus += upgrade.extraFlat;

                    Debug.Log($"[Upgrade] SpikesScalingOnKill: base +{upgrade.valueFlat}, per kill +{upgrade.extraFlat} (total per kill = {spikesOnKillBonus})");
                    break;
                }
            case UpgradeType.DamagePerMinuteScaling:
                {
                    if (UpgradePerTick.Instance != null)
                    {
                        float add = upgrade.valuePercent / 100f; // 2 → 0.02
                        UpgradePerTick.Instance.damageIncreasePerMinute += add;
                    }
                    break;
                }
            case UpgradeType.EnemyEffectChance:
                {
                    // 1) как было — апгрейдим шанс эффекта
                    if (upgrade.enemyEffect != null && EnemyEffectManager.Instance != null)
                        EnemyEffectManager.Instance.AddUpgradeForEffect(upgrade.enemyEffect);
                    else
                        Debug.LogWarning("EnemyEffectChance: не задан enemyEffect или нет EnemyEffectManager");

                    // 2) НОВОЕ — + золото за убийство
                    // Вариант А: всегда +1
                    goldBonusPerKill += 1;

                    // Вариант Б (гибче): брать из valueFlat, и в SO поставить valueFlat = 1
                    // goldBonusPerKill += Mathf.RoundToInt(upgrade.valueFlat);

                    break;
                }

            case UpgradeType.HpForGold:
                {
                    if (playerHealth == null || GoldManager.Instance == null)
                        break;

                    // Сколько максимального здоровья забираем (берём из valueFlat)
                    float healthToLose = upgrade.valueFlat;

                    if (healthToLose > 0f)
                    {
                        // уменьшаем базовый MaxHealth (метод уже сам аккуратно клампит currentHealth)
                        playerHealth.AddFlatMaxHealth(-healthToLose);
                    }

                    int bonusGold = 200 + 5 * goldUpgradeCount;

                    GoldManager.Instance.AddGold(bonusGold);

                    break;
                }
            case UpgradeType.MaxHealthAndDamageFromHealth:
                {
                    if (playerHealth == null)
                        break;

                    // 1) +% к максимальному здоровью И сразу подхиливаем на прирост
                    float hpPercent = upgrade.valuePercent / 100f;
                    if (hpPercent > 0f)
                    {
                        playerHealth.AddMaxHealthMultiplierAndHeal(hpPercent);
                    }

                    // 2) Бонус к урону от здоровья (как было)
                    float dmgPercent = upgrade.valueFlat / 50f;
                    if (dmgPercent > 0f)
                    {
                        damageFromMaxHealthPercent += dmgPercent;
                    }

                    break;
                }
            case UpgradeType.DamageTypeScaling:
                {
                    if (towerAttack == null)
                    {
                        Debug.LogWarning("DamageTypeScaling: towerAttack == null");
                        break;
                    }

                    WeaponDamageType dmgType = upgrade.damageType;
                    int index = (int)dmgType;

                    if (index < 0 || index >= damageTypeMultipliers.Length)
                        break;

                    // Сколько оружий этого типа уже куплено (с учётом стеков)
                    int weaponsOfThisType = towerAttack.GetTotalWeaponsOfType(dmgType);

                    float basePercent = upgrade.damageTypeBasePercent;             // 30
                    float extraPerWeapon = upgrade.damageTypeExtraPerWeaponPercent;  // 1

                    // Сколько % добавляем ЭТОЙ покупкой:
                    // если 0 оружий → только 30%
                    // если 3 оружия → 30 + 1*3 = 33%
                    float addPercent = basePercent + extraPerWeapon * weaponsOfThisType;
                    float addMult = addPercent / 100f;

                    damageTypeMultipliers[index] += addMult;

                    // просто чтобы знать, сколько раз взяли этот апгрейд (если пригодится ещё)
                    damageTypeStacks[index]++;

                    Debug.Log(
                        $"[DamageTypeBonus] {dmgType}: +{addPercent:F1}% " +
                        $"(оружий этого типа = {weaponsOfThisType}), " +
                        $"итоговый множитель = {damageTypeMultipliers[index]:F3}"
                    );
                    break;
                }
            case UpgradeType.MaxHealthPerTick:
                {
                    if (UpgradePerTick.Instance != null)
                    {
                        // valueFlat = сколько HP добавляем
                        // valuePercent = каждые сколько секунд
                        UpgradePerTick.Instance.AddHealthPerTick(
                            upgrade.valueFlat,
                            upgrade.valuePercent
                        );
                    }
                    break;
                }

            case UpgradeType.MaxShieldPerTick:
                {
                    if (UpgradePerTick.Instance != null)
                    {
                        // valueFlat = сколько щита
                        // valuePercent = каждые сколько секунд
                        UpgradePerTick.Instance.AddShieldPerTick(
                            upgrade.valueFlat,
                            upgrade.valuePercent
                        );
                    }
                    break;
                }
            case UpgradeType.RegenPerTick:
                {
                    if (UpgradePerTick.Instance != null)
                    {
                        // valueFlat = сколько регена
                        // valuePercent = каждые сколько секунд
                        UpgradePerTick.Instance.AddRegenPerTick(
                            upgrade.valueFlat,
                            upgrade.valuePercent
                        );
                    }
                    break;
                }
            case UpgradeType.DamageWhileShieldActive:
                {
                    // valuePercent = X (например 10 = +10%)
                    damageWhileShieldActivePercent += upgrade.valuePercent;

                    Debug.Log($"[Upgrade] DamageWhileShieldActive: +{upgrade.valuePercent}% " +
                              $"(total = {damageWhileShieldActivePercent}%)");
                    break;
                }
            case UpgradeType.RegenPerMissingHealth:
                {
                    // valueFlat = X, то самое "X к регену за каждые 100 недостающего HP"
                    regenPer100MissingHealth += upgrade.valueFlat;

                    Debug.Log($"[Upgrade] RegenPerMissingHealth +{upgrade.valueFlat} per 100 missing HP. " +
                              $"Total = {regenPer100MissingHealth} / 100 HP");
                    break;
                }
            case UpgradeType.AuraDamagePerRegen:
                {
                    // valueFlat = множитель (например 2 = 2x от регена)
                    regenAuraEnabled = true;
                    regenAuraMultiplier += upgrade.valueFlat;

                    Debug.Log($"[Upgrade] GlobalRegenAuraDamage: +{upgrade.valueFlat}x regen " +
                              $"(total multiplier = {regenAuraMultiplier}x)");
                    break;
                }
            default:
                {
                    Debug.LogWarning($"UpgradesManager: type {upgrade.type} не обработан");
                    break;
                }
        }
    }
}
