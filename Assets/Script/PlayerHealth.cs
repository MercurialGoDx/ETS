using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    public static PlayerHealth Instance { get; private set; }

    [Header("Здоровье")]
    public float baseMaxHealth = 100f;
    public float maxHealthMultiplier = 1f;
    public float healthRegenPerSecond = 0f;
    public float regenPer100MissingHealth = 0f;
    [SerializeField] private float healthRegenMultiplier = 1f;
    [SerializeField] private float maxHealthGlobalMultiplier = 1f;
    [SerializeField] private float healthRegenGlobalMultiplier = 1f;
    [SerializeField] private float healAmplificationPercent = 0f;

    [Header("Health - Damage Block (diminishing)")]
    [SerializeField, Range(0f, 0.95f)]
    private float blockCap = 0.90f;      // максимум 90%
    [SerializeField, Range(0f, 1f)]
    private float blockChance = 0f;      // текущий шанс блока (0..0.9)
    [SerializeField]
    private int blockUpgradeCount = 0;   // сколько раз купили апгрейд
    [SerializeField, HideInInspector]
    private float blockInitialChance = 0f;

    private const float BlockGrowthBase = 0.90f;
    private const float BlockGrowthMultiplier = 0.85f;
    [Header("Health - Damage Reduction (diminishing)")]

    [SerializeField, Range(0f, 0.98f)]
    private float damageReduction = 0f; // Итоговое уменьшение урона (0.19 = -19% урона)
    [SerializeField, HideInInspector]
    private float damageReductionScore = 0f;
    [SerializeField, HideInInspector]
    private float damageReductionScorePerEnemyHit = 0f;

    [Header("Health - Exhaustible Damage Reduction")]
    [SerializeField, HideInInspector]
    private float exhaustibleDamageReductionScore = 0f;
    [SerializeField, HideInInspector]
    private float exhaustibleDamageReductionScoreLossPerHit = 0f;

    private const float DamageReductionSoftThreshold = 0.50f;
    private const float DamageReductionPostThresholdMultiplier = 0.50f;
    private const float DamageReductionCap = 0.98f;
    private const float ExhaustibleDamageReductionRestoreDelay = 30f;
    public float DamageReduction => damageReduction;

    // Только чтение, для панелей UI. Значения меняются через Add*-методы ниже.
    public float BlockChance => blockChance;
    public float BlockCap => blockCap;
    public float HealOnKillPerEnemy => healOnKillPerEnemy;
    public float HealOnHitFromEnemy => healOnHitFromEnemyAmount;
    public float ExhaustibleDamageReductionScore => exhaustibleDamageReductionScore;

    [Header("UI")]
    [SerializeField] private Image healthBarFill;

    [Header("Attack Hit Audio")]
    [SerializeField] private AudioClip meleeHitSound;
    [SerializeField, Range(0f, 1f)] private float meleeHitVolume = 1f;
    [SerializeField, Min(0f)] private float meleeHitCooldown = 0.08f;
    [SerializeField] private AudioClip rangedProjectileHitSound;
    [SerializeField, Range(0f, 1f)] private float rangedProjectileHitVolume = 1f;
    [Tooltip("Prevents many simultaneous projectiles from stacking the same sound.")]
    [SerializeField, Min(0f)] private float rangedProjectileHitCooldown = 0.08f;

    [Header("Шипы")]
    [Tooltip("WeaponDefinition шипов — для отображения урона шипов в статистике (имя «Шипы» + иконка).")]
    [SerializeField] private WeaponDefinition spikesWeapon;
    [SerializeField] private float spikesBase = 0f;         // базовый урон шипов
    [SerializeField] private float spikesMultiplier = 1f;   // множитель шипов
    [SerializeField] private float spikesGlobalMultiplier = 1f;
    [SerializeField] private float spikesBossDamageMultiplier = 1f;
    [SerializeField] private float spikesEnemyAttackStackPercentPerHit = 0f;
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
    private float _nextMeleeHitSoundTime;
    private float _nextRangedProjectileHitSoundTime;

    private struct ExhaustibleDamageReductionLoss
    {
        public float score;
        public float restoreAt;

        public ExhaustibleDamageReductionLoss(float score, float restoreAt)
        {
            this.score = score;
            this.restoreAt = restoreAt;
        }
    }

    private readonly List<ExhaustibleDamageReductionLoss> exhaustibleDamageReductionLosses = new();

    // === Публичные свойства (для других скриптов) ===

    public float MaxHealth => baseMaxHealth * maxHealthMultiplier * maxHealthGlobalMultiplier;
    public float maxHealth => MaxHealth;   // старое имя, на всякий случай
    public float MaxHealthGlobalMultiplier => maxHealthGlobalMultiplier;
    public float HealthRegenGlobalMultiplier => healthRegenGlobalMultiplier;
    public float HealAmplificationPercent => healAmplificationPercent;

    public float CurrentHealth => currentHealth;

    // Количество шипов для инвентаря: только накопленное плоское значение,
    // без собственных и общих множителей урона.
    public float SpikesCount => spikesBase;

    // Базовый боевой урон шипов с их собственными множителями. Универсальные
    // оружейные бонусы добавляются через DamageCalculator только при нанесении урона.
    public float SpikesDamage => spikesBase * spikesMultiplier * spikesGlobalMultiplier;
    public float SpikesGlobalMultiplier => spikesGlobalMultiplier;
    public float SpikesBossDamageMultiplier => spikesBossDamageMultiplier;
    public float SpikesEnemyAttackStackPercentPerHit => spikesEnemyAttackStackPercentPerHit;

    /// <summary>
    /// Итоговый реген в секунду: базовый плюс бонус за недостающее здоровье.
    /// Единственное место с этой формулой — её же использует Update и панели UI.
    /// </summary>
    public float GetTotalRegen()
    {
        float regen = healthRegenPerSecond * healthRegenMultiplier;

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

        return regen * healthRegenGlobalMultiplier;
    }

    private List<ITakeDamageModifier> modifiers = new();


    private void Awake()
    {
        Instance = this;

        // Баланс из таблицы (если импортирован) перекрывает инспектор.
        var cfg = BalanceService.Config;
        if (cfg != null)
        {
            baseMaxHealth = cfg.player.playerHp;
            blockCap = cfg.player.blockCap;
        }

        currentHealth = MaxHealth;
        RecalculateDamageReduction();

        UpdateHealthUI();

        GetComponents(modifiers);
        modifiers.Sort((a, b) => a.Priority.CompareTo(b.Priority));
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Init(DamageCalculator calculator)
    {
        damageCalculator = calculator;
    }

    private void Update()
    {
        if (isDead || GameStateManager.Instance.CurrentState != GameState.Playing) return;

        RestoreExhaustibleDamageReduction();

        // === РЕГЕН ЗДОРОВЬЯ ===
        if (currentHealth < MaxHealth)
        {
            float regen = GetTotalRegen();

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
        add = Mathf.Clamp(add, 0f, 0.999999f);
        if (add <= 0f) return;

        AddDamageReductionScore(-Mathf.Log(1f - add));
    }

    public void AddDamageReductionPerEnemyHit(float addPerHit)
    {
        addPerHit = Mathf.Clamp(addPerHit, 0f, 0.999999f);
        if (addPerHit <= 0f) return;

        // Храним вклад каждой покупки в score, чтобы несколько экземпляров
        // улучшения складывались по тем же правилам diminishing returns.
        damageReductionScorePerEnemyHit += -Mathf.Log(1f - addPerHit);
    }

    /// <summary>
    /// Добавляет одну покупку истощаемого DR. Значения передаются как доли score:
    /// 0.75 = +75% score в стартовый запас, 0.10 = -10% score за удар.
    /// </summary>
    public void AddExhaustibleDamageReduction(float startScore, float lossScorePerHit)
    {
        startScore = Mathf.Max(0f, startScore);
        lossScorePerHit = Mathf.Max(0f, lossScorePerHit);

        if (startScore <= 0f && lossScorePerHit <= 0f)
            return;

        exhaustibleDamageReductionScore += startScore;
        exhaustibleDamageReductionScoreLossPerHit += lossScorePerHit;
        RecalculateDamageReduction();
    }

    private void AddDamageReductionScore(float score)
    {
        if (score <= 0f) return;

        damageReductionScore += score;
        RecalculateDamageReduction();
    }

    private void RecalculateDamageReduction()
    {
        damageReduction = CalculateDamageReduction(
            damageReductionScore + exhaustibleDamageReductionScore);
    }

    private void ConsumeExhaustibleDamageReductionForHit()
    {
        if (exhaustibleDamageReductionScore <= 0f ||
            exhaustibleDamageReductionScoreLossPerHit <= 0f)
            return;

        float lostScore = Mathf.Min(
            exhaustibleDamageReductionScore,
            exhaustibleDamageReductionScoreLossPerHit);

        exhaustibleDamageReductionScore -= lostScore;
        exhaustibleDamageReductionLosses.Add(
            new ExhaustibleDamageReductionLoss(
                lostScore,
                Time.time + ExhaustibleDamageReductionRestoreDelay));

        RecalculateDamageReduction();
    }

    private void RestoreExhaustibleDamageReduction()
    {
        if (exhaustibleDamageReductionLosses.Count == 0)
            return;

        float now = Time.time;
        bool restored = false;

        for (int i = exhaustibleDamageReductionLosses.Count - 1; i >= 0; i--)
        {
            var loss = exhaustibleDamageReductionLosses[i];
            if (now < loss.restoreAt)
                continue;

            exhaustibleDamageReductionScore += loss.score;
            exhaustibleDamageReductionLosses.RemoveAt(i);
            restored = true;
        }

        if (restored)
            RecalculateDamageReduction();
    }

    private static float CalculateDamageReduction(float score)
    {
        score = Mathf.Max(0f, score);

        float thresholdScore = -Mathf.Log(1f - DamageReductionSoftThreshold);
        if (score <= thresholdScore)
            return 1f - Mathf.Exp(-score);

        float scoreAfterThreshold = score - thresholdScore;
        float slowedProgress = 1f - Mathf.Exp(
            -DamageReductionPostThresholdMultiplier * scoreAfterThreshold);

        float result = DamageReductionSoftThreshold
            + (DamageReductionCap - DamageReductionSoftThreshold) * slowedProgress;

        return Mathf.Min(result, DamageReductionCap);
    }

    public void AddBlockChanceDiminishing(float firstUpgradePercent)
    {
        firstUpgradePercent = Mathf.Clamp(firstUpgradePercent, 0f, 100f);
        if (firstUpgradePercent <= 0f) return;

        // Первая покупка всегда даёт ровно значение из DamageBlockChanceUpgrade.
        // Например, valuePercent = 15 означает стартовый шанс блока 15%.
        if (blockUpgradeCount == 0 || blockInitialChance <= 0f)
            blockInitialChance = Mathf.Min(firstUpgradePercent / 100f, blockCap);

        blockUpgradeCount++;

        int diminishingPurchaseCount = blockUpgradeCount - 1;
        float additionalChanceRange = Mathf.Max(0f, blockCap - blockInitialChance);
        float diminishingProgress = 1f - Mathf.Pow(
            BlockGrowthBase,
            BlockGrowthMultiplier * diminishingPurchaseCount);

        blockChance = blockInitialChance + additionalChanceRange * diminishingProgress;
        blockChance = Mathf.Min(blockChance, blockCap);
    }

    /// <summary>
    /// Единая точка входа для любого мгновенного лечения (килл, попадание врагом, оружие
    /// вроде Chaos Wave). Усиление лечения (healAmplificationPercent) применяется здесь —
    /// значит действует на всё, что сюда попадёт, включая будущие источники, без правок в них.
    /// Реген (GetTotalRegen) через этот метод не идёт — это отдельная механика.
    /// </summary>
    public void Heal(float amount)
    {
        if (amount <= 0f) return;

        amount *= (1f + healAmplificationPercent);
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        UpdateHealthUI();
    }

    public void AddHealAmplificationPercent(float percent)
    {
        healAmplificationPercent += percent;
    }

    // === УРОН ===

    public void TakeDamage(Enemy enemy)
    {
        if (enemy == null) return;
        if (isDead) return;

        // Обрабатываем восстановление и перед ударом, чтобы точный момент
        // восстановления не зависел от частоты кадров/порядка Update.
        RestoreExhaustibleDamageReduction();

        // Счётчик урона врага увеличивается до проверки блока: заблокированная
        // атака всё равно была совершена и должна продвинуть его прогрессию.
        enemy.RegisterTowerAttack();

        if (enemy.CurrentDamageToPlayer <= 0f) return;

        // 1) Block: урон полностью игнорируем
        if (blockChance > 0f && UnityEngine.Random.value < blockChance)
        {
            return;
        }

        float remaining = enemy.CurrentDamageToPlayer;

        // 2) Damage Reduction: уменьшение входящего урона (работает и для щита)
        if (damageReduction > 0f)
        {
            remaining *= (1f - damageReduction);
            if (remaining <= 0f) return;
        }

        PlayAttackHitSound(enemy);

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

        // Заблокированная атака возвращается выше и сюда не попадает.
        // Реальный удар сначала использует текущий DR, затем усиливает следующие удары.
        AddDamageReductionScore(damageReductionScorePerEnemyHit);
        ConsumeExhaustibleDamageReductionForHit();
    }

    private void PlayAttackHitSound(Enemy enemy)
    {
        if (enemy.StatusEffects != null && enemy.StatusEffects.IsBoss)
            return;

        switch (enemy.attackType)
        {
            case EnemyAttackType.Melee:
                PlayHitSound(
                    meleeHitSound,
                    meleeHitVolume,
                    meleeHitCooldown,
                    ref _nextMeleeHitSoundTime);
                break;

            case EnemyAttackType.MidRange:
            case EnemyAttackType.LongRange:
                PlayHitSound(
                    rangedProjectileHitSound,
                    rangedProjectileHitVolume,
                    rangedProjectileHitCooldown,
                    ref _nextRangedProjectileHitSoundTime);
                break;
        }
    }

    private void PlayHitSound(
        AudioClip clip,
        float baseVolume,
        float cooldown,
        ref float nextAllowedTime)
    {
        if (clip == null || Time.unscaledTime < nextAllowedTime)
        {
            return;
        }

        float volume = baseVolume;
        if (AudioManager.Instance != null)
            volume *= AudioManager.Instance.GetSoundVolume();

        volume = Mathf.Clamp01(volume);
        if (volume <= 0f)
            return;

        nextAllowedTime = Time.unscaledTime + cooldown;
        AudioSource.PlayClipAtPoint(clip, transform.position, volume);
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

    public void AddMaxHealthGlobalMultiplierAndHeal(float percent)
    {
        float oldMax = MaxHealth;
        maxHealthGlobalMultiplier += percent;
        float newMax = MaxHealth;

        currentHealth = Mathf.Min(currentHealth + newMax - oldMax, newMax);
        UpdateHealthUI();
    }

    public void AddHealthRegen(float amountPerSecond)
    {
        healthRegenPerSecond += amountPerSecond;
    }

    public void AddHealthRegenMultiplier(float percent)
    {
        healthRegenMultiplier += percent;
    }

    public void AddHealthRegenGlobalMultiplier(float percent)
    {
        healthRegenGlobalMultiplier += percent;
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

    public void AddSpikesGlobalMultiplier(float percent)
    {
        spikesGlobalMultiplier += percent;
    }

    public void AddSpikesBossDamagePercent(float percent)
    {
        spikesBossDamageMultiplier += percent;
    }

    public void AddSpikesEnemyAttackStackPercent(float percentPerHit)
    {
        spikesEnemyAttackStackPercentPerHit += percentPerHit;
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
            damageType = default,
            itemTier = ItemTier.None,
            isSpikes = true
        });

        float bossDamageMultiplier = enemy.StatusEffects != null && enemy.StatusEffects.IsBoss
            ? spikesBossDamageMultiplier
            : 1f;

        int enemyAttackStackCount = RegisterSpikeAttackStack(enemy);
        float enemyAttackStackMultiplier = 1f
            + spikesEnemyAttackStackPercentPerHit * enemyAttackStackCount;

        damage *= bossDamageMultiplier * enemyAttackStackMultiplier;

        DamageStatsManager.Instance?.RegisterDamage(spikesWeapon, damage);

        enemy.TakeWeaponDamage(damage, spikesWeapon, true);
    }

    private int RegisterSpikeAttackStack(Enemy enemy)
    {
        if (spikesEnemyAttackStackPercentPerHit <= 0f)
            return 0;

        return enemy.IncrementSpikesAttackStack();
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
