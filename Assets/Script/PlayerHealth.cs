using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    [Header("Здоровье")]
    public float baseMaxHealth = 100f;
    public float maxHealthMultiplier = 1f;
    public float healthRegenPerSecond = 0f;
    public float regenPer100MissingHealth = 0f;

    [Header("Health - Damage Block (diminishing)")]
    [SerializeField, Range(0f, 0.95f)]
    private float blockCap = 0.80f;      // максимум 80%
    [SerializeField, Range(0f, 1f)]
    private float blockChance = 0f;      // текущий шанс блока (0..0.8)
    [SerializeField]
    private int blockUpgradeCount = 0;   // сколько раз купили апгрейд
    [Header("Health - Damage Reduction (diminishing)")]

    [SerializeField, Range(0f, 0.99f)]
    private float damageReduction = 0f; // 0..1 (0.19 = -19% урона)
    public float DamageReduction => damageReduction;

    [Header("UI")]
    [SerializeField] private Image healthBarFill;

    [Header("Шипы")]
    [Tooltip("WeaponDefinition шипов — для отображения урона шипов в статистике (имя «Шипы» + иконка).")]
    [SerializeField] private WeaponDefinition spikesWeapon;
    [SerializeField] private float spikesBase = 0f;         // базовый урон шипов
    [SerializeField] private float spikesMultiplier = 1f;   // множитель шипов
    [SerializeField] private float spikesOnKillBonus = 0f;  // доп. урон за убийство врага
    [SerializeField] private float spikesDamageScalePerEnemyHit = 0f;   // доп. урон за каждое получение урона от врага
    [Header("Эффекты при получении урона")]
    [SerializeField] private float healOnHitFromEnemyAmount = 0f;

    [Header("Хил за убийство врага")]
    [SerializeField] private float healOnKillPerEnemy = 0f;

    private float currentHealth;
    public event Action OnDied;
    private bool isDead = false;

    private DamageCalculator damageCalculator;

    // === Публичные свойства (для других скриптов) ===

    public float MaxHealth => baseMaxHealth * maxHealthMultiplier;
    public float maxHealth => MaxHealth;   // старое имя, на всякий случай

    public float CurrentHealth => currentHealth;

    public float SpikesDamage => spikesBase * spikesMultiplier;

    private List<ITakeDamageModifier> modifiers = new();


    private void Awake()
    {
        currentHealth = MaxHealth;

        UpdateHealthUI();

        GetComponents(modifiers);
        modifiers.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }

    public void Init(DamageCalculator calculator)
    {
        damageCalculator = calculator;
    }

    private void Update()
    {
        if (isDead || GameStateManager.Instance.CurrentState != GameState.Playing) return;

        // === РЕГЕН ЗДОРОВЬЯ ===
        if (currentHealth < MaxHealth)
        {
            float regen = healthRegenPerSecond;

            // Бонусный реген за недостающее здоровье
            if (UpgradesManager.Instance != null &&
                regenPer100MissingHealth > 0f)
            {
                float missing = MaxHealth - currentHealth;
                if (missing > 0f)
                {
                    regen += regenPer100MissingHealth * (missing / 100f);
                }
            }

            if (regen > 0f)
            {
                currentHealth += regen * Time.deltaTime;
                currentHealth = Mathf.Min(currentHealth, MaxHealth);
                UpdateHealthUI();
            }
        }

        //HandleShieldRegen();
    }

    public void AddDamageReductionDiminishing(float add)
    {
        add = Mathf.Clamp01(add);

        float remaining = 1f - damageReduction;
        if (remaining <= 0f) return;

        damageReduction += remaining * add;
        damageReduction = Mathf.Clamp01(damageReduction);
    }

    public void AddBlockChanceDiminishing(int stacks = 1)
    {
        if (stacks <= 0) return;

        blockUpgradeCount += stacks;

        const int N = 12;               // целевая точка к которой нормируем
        const float m = 0.975f;         // 78% от cap (0.78 / 0.8)
        const float a = 0.1238619f;     // подобрано: n=1 ~10%, n=12 ~78%

        float n = blockUpgradeCount;

        float denom = Mathf.Log(1f + a * N);
        blockChance = blockCap * m * (Mathf.Log(1f + a * n) / denom);

        blockChance = Mathf.Min(blockChance, blockCap);
    }

    public void Heal(float amount)
    {
        if (amount <= 0f) return;

        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        UpdateHealthUI();
    }

    // === УРОН ===

    public void TakeDamage(Enemy enemy)
    {
        if (enemy.damageToPlayer <= 0f) return;
        if (isDead) return;

        // 1) Block: урон полностью игнорируем
        if (blockChance > 0f && UnityEngine.Random.value < blockChance)
        {
            return;
        }

        float remaining = enemy.damageToPlayer;

        // 2) Damage Reduction: уменьшение входящего урона (работает и для щита)
        if (damageReduction > 0f)
        {
            remaining *= (1f - damageReduction);
            if (remaining <= 0f) return;
        }

        // 2) Сначала щит
        foreach (var mod in modifiers)
            remaining = mod.ModifyDamage(remaining);

        // 2) Потом здоровье
        if (remaining > 0f)
        {
            float previousHealth = currentHealth;

            currentHealth -= remaining;
            if (currentHealth <= 0f)
            {
                currentHealth = 0f;
                UpdateHealthUI();
                Die();
                return;
            }

            UpdateHealthUI();

            // башня реально получила урон по ХП (health уменьшилось) и выжила
            if (healOnHitFromEnemyAmount > 0f && previousHealth > currentHealth)
            {
                Heal(healOnHitFromEnemyAmount);
            }

            if (SpikesDamage > 0)
            {
                DealSpikesDamage(enemy);
            }
        }

        if (spikesDamageScalePerEnemyHit != 0)
        {
            AddSpikesDamage(spikesDamageScalePerEnemyHit);
        }
    }


    // === АПГРЕЙДЫ ЗДОРОВЬЯ ===

    public void AddFlatMaxHealth(float amount)
    {
        baseMaxHealth += amount;
        currentHealth = Mathf.Min(currentHealth, MaxHealth);
        UpdateHealthUI();
    }

    public void AddFlatMaxHealthAndHeal(float amount)
    {
        baseMaxHealth += amount;
        currentHealth = Mathf.Min(currentHealth + amount, MaxHealth);
        UpdateHealthUI();
    }

    // percent ждём как 0.1f для +10%, 0.2f для +20% и т.д.
    public void AddMaxHealthMultiplier(float percent)
    {
        maxHealthMultiplier += percent;
        currentHealth = Mathf.Min(currentHealth, MaxHealth);
        UpdateHealthUI();
    }
    public void AddMaxHealthMultiplierAndHeal(float percent)
    {
        float oldMax = MaxHealth;
        maxHealthMultiplier += percent;
        float newMax = MaxHealth;
        float delta = newMax - oldMax;

        currentHealth = Mathf.Min(currentHealth + delta, newMax);
        UpdateHealthUI();
    }

    public void AddHealthRegen(float amountPerSecond)
    {
        healthRegenPerSecond += amountPerSecond;
    }

    // старые названия для совместимости
    public void IncreaseMaxHealth(float amount)
    {
        AddFlatMaxHealth(amount);
    }

    public void AddHealOnHitFromEnemy(float amount)
    {
        healOnHitFromEnemyAmount += amount;
    }

    public void IncreaseMaxHealth(float amount, bool alsoHeal)
    {
        if (alsoHeal)
            AddFlatMaxHealthAndHeal(amount);
        else
            AddFlatMaxHealth(amount);
    }

    // === АПГРЕЙДЫ ШИПОВ ===

    // Flat
    public void AddSpikesDamage(float amount)
    {
        spikesBase += amount;
    }

    // Percent (amount = 0.1f -> +10%)
    public void AddSpikesPercent(float amount)
    {
        spikesMultiplier += amount;
    }

    public void AddSpikesDamagePerKill(float amount)
    {
        spikesOnKillBonus += amount;
    }

    public void AddSpikesDamagePerEnemyHit(float amount)
    {
        spikesDamageScalePerEnemyHit += amount;
    }

    public void OnEnemyKilledBySpikes()
    {
        if (spikesOnKillBonus <= 0f) return;

        AddSpikesDamage(spikesOnKillBonus);

        Debug.Log($"[SpikesScaling] Enemy killed by spikes. +{spikesOnKillBonus} spikes. Current spikes damage = {SpikesDamage}");
    }

    public void AddHealOnKill(float amount)
    {
        healOnKillPerEnemy += amount;
    }

    public void AddRegenPer100MissingHealth(float amount)
    {
        regenPer100MissingHealth += amount;
    }

    public void OnEnemyKilled(bool killedBySpikes)
    {
        // Хил
        if (healOnKillPerEnemy > 0f)
        {
            Heal(healOnKillPerEnemy);
        }

        // Шипы
        if (killedBySpikes)
        {
            OnEnemyKilledBySpikes();
        }

        if (UpgradesManager.Instance.playerShield.ShieldRestorePerEnemyKill > 0)
        {
            UpgradesManager.Instance.playerShield.RestoreCurrentShield(UpgradesManager.Instance.playerShield.ShieldRestorePerEnemyKill);
        }
    }

    public void DealSpikesDamage(Enemy enemy)
    {
        float damage = damageCalculator.Calculate(new DamageContext
        {
            baseDamage = SpikesDamage,
            itemTier = ItemTier.None,
            isSpikes = true
        });

        DamageStatsManager.Instance?.RegisterDamage(spikesWeapon, damage);

        enemy.TakeDamage(damage, true);
    }

    // === UI ===

    private void UpdateHealthUI()
    {
        if (healthBarFill != null)
        {
            float normalized = MaxHealth > 0f ? currentHealth / MaxHealth : 0f;
            healthBarFill.fillAmount = Mathf.Clamp01(normalized);
        }
    }

    // === СМЕРТЬ ===

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log("Player died");
        OnDied?.Invoke();
    }
}
