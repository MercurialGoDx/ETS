using System;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Ссылки")]
    public Transform tower;
    public WaveTimerUI waveTimerUI;
    public BossManager bossManager;

    [Header("Волны")]
    public GameObject[] enemyPrefabs;
    public int enemiesPerWave = 10;
    public float timeBetweenWaves = 10f;

    [Header("Где спавним")]
    public float spawnRadiusMin = 18f;
    public float spawnRadiusMax = 20f;

    [Header("Прогрессия сложности (проценты, за волну)")]
    [Tooltip("Базовый % роста множителя ЗДОРОВЬЯ за волну. (Напр. 5 = +5% за волну)")]
    public float healthGrowthPercent = 3.5f;

    [Tooltip("Базовый % роста множителя УРОНА за волну. (Напр. 5 = +5% за волну)")]
    public float damageGrowthPercent = 3.5f;

    [Header("Скейлинг сложности по времени (минуты)")]
    [Tooltip("После этого времени рост сложности HP и урона умножается на свои stage1-множители (от базы)")]
    public float timeMark1Minutes = 12f;

    [Tooltip("После этого времени рост сложности HP и урона умножается на свои stage2-множители (от базы)")]
    public float timeMark2Minutes = 25f;

    [Tooltip("После этого времени рост сложности HP и урона умножается на свои stage3-множители (от базы)")]
    public float timeMark3Minutes = 40f;

    [Tooltip("Множитель роста ЗДОРОВЬЯ после 1-го порога (от базы)")]
    public float growthStage1MultiplierHealth = 2f;

    [Tooltip("Множитель роста УРОНА после 1-го порога (от базы)")]
    public float growthStage1MultiplierDamage = 2f;

    [Tooltip("Множитель роста ЗДОРОВЬЯ после 2-го порога (от базы)")]
    public float growthStage2MultiplierHealth = 3f;

    [Tooltip("Множитель роста УРОНА после 2-го порога (от базы)")]
    public float growthStage2MultiplierDamage = 3f;

    [Tooltip("Множитель роста ЗДОРОВЬЯ после 3-го порога (от базы). По умолчанию равен stage2 — включается только если задать больше")]
    public float growthStage3MultiplierHealth = 3f;

    [Tooltip("Множитель роста УРОНА после 3-го порога (от базы). По умолчанию равен stage2 — включается только если задать больше")]
    public float growthStage3MultiplierDamage = 3f;

    [Header("Прогрессия сложности (фикс. прибавка)")]
    public float healthAddPerWave = 5f;
    public float damageAddPerWave = 1f;

    [Header("Золото за врага (бонус по волнам)")]
    [Tooltip("Каждые N волн бонусное золото за врага растёт. 0 = бонус отключён.")]
    public int goldBonusWavePeriod = 20;

    [Tooltip("На сколько золота растёт бонус за каждый период волн.")]
    public int goldBonusPerPeriod = 1;

    [Header("Текущее накопление (не трогать руками)")]
    [SerializeField] private float healthDifficultyMultiplier = 1f;
    [SerializeField] private float damageDifficultyMultiplier = 1f;
    [SerializeField] private float flatHealthBonus = 0f;
    [SerializeField] private float flatDamageBonus = 0f;

    [Header("Апгрейды (runtime)")]
    [SerializeField] private float enemiesPerWavePercentBonus = 0f; // 0.25 = +25%

    // ✅ Событие для UI: волна, множитель здоровья, множитель урона, фиксHP, фиксDMG
    public event Action<int, float, float, float, float> OnWaveSpawned;

    private float waveTimer = 0f;
    private float bossTimer = 0f;
    private int currentWaveIndex = 0;

    // --- ДОБАВЛЕНО ---
    private float runTimeSeconds = 0f;
    private float baseHealthGrowthPercent; // запоминаем инспекторное значение как "базу"
    private float baseDamageGrowthPercent;
    // ---------------

    public int CurrentWaveNumber => currentWaveIndex + 1;
    public float CurrentHealthMultiplier => healthDifficultyMultiplier;
    public float CurrentDamageMultiplier => damageDifficultyMultiplier;
    public float CurrentFlatHealthBonus => flatHealthBonus;
    public float CurrentFlatDamageBonus => flatDamageBonus;

    // ✅ Итоговое кол-во врагов
    public int CurrentEnemiesPerWave
    {
        get
        {
            float factor = 1f + enemiesPerWavePercentBonus;
            int result = Mathf.CeilToInt(enemiesPerWave * factor);
            return Mathf.Max(0, result);
        }
    }

    public void InitializeSpawnedEnemy(
        Enemy enemy,
        float baseHealth,
        float baseDamage,
        float baseHealthMultiplier = 1f,
        float baseDamageMultiplier = 1f,
        float additionalDamage = 0f)
    {
        if (enemy == null)
            return;

        float health = (baseHealth * baseHealthMultiplier * healthDifficultyMultiplier) + flatHealthBonus;
        // additionalDamage — плоская прибавка (у босса — additional_damage_boss из конфига),
        // не участвует в умножении на baseDamageMultiplier/damageDifficultyMultiplier.
        float damage = additionalDamage + flatDamageBonus + (baseDamage * baseDamageMultiplier * damageDifficultyMultiplier);

        enemy.InitStats(health, damage);

        // Every spawned enemy receives the same wave rewards and runtime effects.
        enemy.bonusGold = goldBonusWavePeriod > 0
            ? (currentWaveIndex / goldBonusWavePeriod) * goldBonusPerPeriod
            : 0;
        EnemyEffectManager.Instance?.ApplyEffectsToEnemy(enemy);
    }

    private void Start()
    {
        // Баланс из таблицы (если импортирован) перекрывает инспектор.
        // Вкладка enemy: health_difficult_start/damage_difficult_start — раздельная база
        // роста % за волну для HP и урона; growth_stage*_multiplier_health/damage —
        // раздельные множители ускорения роста для HP и урона на каждом пороге;
        // пороги времени общие, в таблице в секундах, в коде — в минутах.
        var cfg = BalanceService.Config;
        if (cfg != null)
        {
            timeBetweenWaves = cfg.waves.delayPerWave;
            enemiesPerWave = cfg.waves.enemyPerWave;
            healthAddPerWave = cfg.waves.hpAddPerWave;
            damageAddPerWave = cfg.waves.damageAddPerWave;

            healthGrowthPercent = cfg.waves.healthDifficultStart;
            damageGrowthPercent = cfg.waves.damageDifficultStart;
            growthStage1MultiplierHealth = cfg.waves.growthStage1MultiplierHealth;
            growthStage1MultiplierDamage = cfg.waves.growthStage1MultiplierDamage;
            growthStage2MultiplierHealth = cfg.waves.growthStage2MultiplierHealth;
            growthStage2MultiplierDamage = cfg.waves.growthStage2MultiplierDamage;
            growthStage3MultiplierHealth = cfg.waves.growthStage3MultiplierHealth;
            growthStage3MultiplierDamage = cfg.waves.growthStage3MultiplierDamage;
            timeMark1Minutes = cfg.waves.timeDifficultStage1 / 60f;
            timeMark2Minutes = cfg.waves.timeDifficultStage2 / 60f;
            timeMark3Minutes = cfg.waves.timeDifficultStage3 / 60f;
        }

        // --- ДОБАВЛЕНО ---
        baseHealthGrowthPercent = healthGrowthPercent;
        baseDamageGrowthPercent = damageGrowthPercent;
        // ---------------

        if (tower == null)
        {
            GameObject towerObj = GameObject.FindGameObjectWithTag("Player");
            if (towerObj != null)
                tower = towerObj.transform;
        }

        NotifyUI();
    }

    private void Update()
    {
        if (tower == null || enemyPrefabs == null || enemyPrefabs.Length == 0)
            return;

        // --- ДОБАВЛЕНО ---
        runTimeSeconds += Time.deltaTime;
        UpdateGrowthPercentByTime();
        // ---------------

        waveTimer += Time.deltaTime;
        bossTimer += Time.deltaTime;

        if (waveTimerUI != null)
            waveTimerUI.SetProgress(waveTimer / timeBetweenWaves);

        if (waveTimer >= timeBetweenWaves)
        {
            // Вычитаем интервал, а не сбрасываем в 0 — иначе "перелёт" кадра за порог
            // (Time.deltaTime почти никогда не попадает ровно в 10.000) теряется каждый раз
            // и накапливается в растущий дрейф между реальным и ожидаемым временем волны.
            waveTimer -= timeBetweenWaves;

            if (waveTimerUI != null)
                waveTimerUI.SetProgress(0f);

            // Босс строго раз в bossManager.spawnEverySeconds: если время пришло, этот тик
            // спавнит босса вместо обычной волны — не ждём, пока умрёт предыдущий босс.
            if (bossManager != null && bossTimer >= bossManager.spawnEverySeconds)
            {
                bossTimer -= bossManager.spawnEverySeconds;
                bossManager.SpawnRandomBoss();
            }
            else
            {
                SpawnWave();
            }
        }
    }

    // ✅ Вызывается апгрейдом
    public void AddEnemiesPerWavePercent(float addPercent)
    {
        enemiesPerWavePercentBonus += addPercent;
        enemiesPerWavePercentBonus = Mathf.Max(0f, enemiesPerWavePercentBonus);
    }

    void SpawnWave()
    {
        int enemyTypeIndex = currentWaveIndex % enemyPrefabs.Length;
        GameObject enemyPrefab = enemyPrefabs[enemyTypeIndex];

        Enemy prefabEnemy = enemyPrefab.GetComponent<Enemy>();
        float baseHealth = prefabEnemy.maxHealth;
        float baseDamage = prefabEnemy.damageToPlayer;

        int countToSpawn = CurrentEnemiesPerWave;

        for (int i = 0; i < countToSpawn; i++)
        {
            Vector3 spawnPos = GetSpawnPositionAroundTower();

            GameObject obj = PoolManager.Instance.Spawn(enemyPrefab, spawnPos, Quaternion.identity);
            //obj.transform.localScale = Vector3.one;
            Enemy enemyInstance = obj.GetComponent<Enemy>();
            if (enemyInstance == null) continue;

            enemyInstance.isDead = false;

            InitializeSpawnedEnemy(enemyInstance, baseHealth, baseDamage);
            TryApplyHunt(enemyInstance);
        }

        NotifyUI();

        currentWaveIndex++;

        // Важно: рост процента уже обновлён по времени в UpdateGrowthPercentByTime()
        float healthK = 1f + (healthGrowthPercent / 100f);
        float damageK = 1f + (damageGrowthPercent / 100f);
        healthDifficultyMultiplier *= healthK;
        damageDifficultyMultiplier *= damageK;

        flatHealthBonus += healthAddPerWave;
        flatDamageBonus += damageAddPerWave;
    }

    private static void TryApplyHunt(Enemy enemy)
    {
        UpgradesRuntimeData runtime = UpgradesManager.Instance?.GameplayRuntimeData;
        if (runtime == null || enemy == null || enemy.IsGolden)
            return;

        if (!runtime.TryGetHuntModifiers(
                out float healthMultiplier,
                out float damageMultiplier,
                out float goldMultiplier,
                out Color tint,
                out float tintStrength))
        {
            return;
        }

        if (UnityEngine.Random.value > runtime.GoldenEnemyChance)
            return;

        if (!enemy.ApplyGoldenModifiers(
                healthMultiplier,
                damageMultiplier,
                goldMultiplier,
                tint,
                tintStrength))
        {
            return;
        }

        Debug.Log(
            $"[Hunt] Golden enemy spawned: {enemy.name}, " +
            $"HP={enemy.maxHealth:0.##}, damage={enemy.damageToPlayer:0.##}, gold=x{goldMultiplier:0.##}.");
    }

    // --- ДОБАВЛЕНО ---
    private void UpdateGrowthPercentByTime()
    {
        float minutes = runTimeSeconds / 60f;

        // Пороги по времени общие для здоровья и урона, а множитель ускорения на
        // каждом пороге — свой для HP и свой для урона.
        float healthStageMultiplier = 1f;
        float damageStageMultiplier = 1f;

        if (minutes >= timeMark3Minutes)
        {
            healthStageMultiplier = growthStage3MultiplierHealth;
            damageStageMultiplier = growthStage3MultiplierDamage;
        }
        else if (minutes >= timeMark2Minutes)
        {
            healthStageMultiplier = growthStage2MultiplierHealth;
            damageStageMultiplier = growthStage2MultiplierDamage;
        }
        else if (minutes >= timeMark1Minutes)
        {
            healthStageMultiplier = growthStage1MultiplierHealth;
            damageStageMultiplier = growthStage1MultiplierDamage;
        }

        healthGrowthPercent = baseHealthGrowthPercent * healthStageMultiplier;
        damageGrowthPercent = baseDamageGrowthPercent * damageStageMultiplier;
    }
    // ---------------

    private void NotifyUI()
    {
        OnWaveSpawned?.Invoke(CurrentWaveNumber, healthDifficultyMultiplier, damageDifficultyMultiplier, flatHealthBonus, flatDamageBonus);
    }

    Vector3 GetSpawnPositionAroundTower()
    {
        float angle = UnityEngine.Random.Range(0f, 360f);
        float rad = angle * Mathf.Deg2Rad;

        float x = Mathf.Cos(rad);
        float z = Mathf.Sin(rad);

        Vector3 dir = new Vector3(x, 0f, z).normalized;
        float radius = UnityEngine.Random.Range(spawnRadiusMin, spawnRadiusMax);

        Vector3 pos = tower.position + dir * radius;
        pos.y = 6f;
        return pos;
    }
}
