using System;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Ссылки")]
    public Transform tower;
    public WaveTimerUI waveTimerUI;

    [Header("Волны")]
    public GameObject[] enemyPrefabs;
    public int enemiesPerWave = 10;
    public float timeBetweenWaves = 10f;

    [Header("Где спавним")]
    public float spawnRadiusMin = 18f;
    public float spawnRadiusMax = 20f;

    [Header("Прогрессия сложности (проценты)")]
    public float difficultyMultiplier = 1f;

    [Tooltip("Базовый % роста множителя за волну. (Напр. 5 = +5% за волну)")]
    public float multiplierGrowthPercent = 10f;

    [Header("Скейлинг сложности по времени (минуты)")]
    [Tooltip("После этого времени рост сложности умножается на 1.5")]
    public float timeMark1Minutes = 10f;

    [Tooltip("После этого времени рост сложности умножается ещё на 2 от текущего (итого x3 от базы)")]
    public float timeMark2Minutes = 20f;

    [Tooltip("Множитель роста после 1-го порога (1.5 = +50%)")]
    public float growthStage1Multiplier = 1.5f;

    [Tooltip("Множитель роста после 2-го порога ОТ БАЗЫ (3 = 1.5 * 2)")]
    public float growthStage2Multiplier = 3f;

    [Header("Прогрессия сложности (фикс. прибавка)")]
    public float healthAddPerWave = 5f;
    public float damageAddPerWave = 1f;

    [Header("Текущее накопление (не трогать руками)")]
    [SerializeField] private float flatHealthBonus = 0f;
    [SerializeField] private float flatDamageBonus = 0f;

    [Header("Апгрейды (runtime)")]
    [SerializeField] private float enemiesPerWavePercentBonus = 0f; // 0.25 = +25%

    // ✅ Событие для UI: волна, множитель, фиксHP, фиксDMG
    public event Action<int, float, float, float> OnWaveSpawned;

    private float waveTimer = 0f;
    private int currentWaveIndex = 0;

    // --- ДОБАВЛЕНО ---
    private float runTimeSeconds = 0f;
    private float baseMultiplierGrowthPercent; // запоминаем инспекторное значение как "базу"
    // ---------------

    public int CurrentWaveNumber => currentWaveIndex + 1;
    public float CurrentMultiplier => difficultyMultiplier;
    public float CurrentFlatHealthBonus => flatHealthBonus;
    public float CurrentFlatDamageBonus => flatDamageBonus;

    /// <summary>Накопленный прирост числа врагов в волне (0.25 = +25%). Только чтение, для UI.</summary>
    public float EnemiesPerWavePercentBonus => enemiesPerWavePercentBonus;

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
        float baseDamageMultiplier = 1f)
    {
        if (enemy == null)
            return;

        float health = (baseHealth * baseHealthMultiplier * difficultyMultiplier) + flatHealthBonus;
        float damage = (baseDamage * baseDamageMultiplier * difficultyMultiplier) + flatDamageBonus;

        enemy.InitStats(health, damage);

        // Every spawned enemy receives the same wave rewards and runtime effects.
        enemy.bonusGold = currentWaveIndex / 20;
        EnemyEffectManager.Instance?.ApplyEffectsToEnemy(enemy);
    }

    private void Start()
    {
        // Баланс из таблицы (если импортирован) перекрывает инспектор.
        // Маппинг вкладки enemy: difficult_* — абсолютные проценты роста за волну,
        // в коде это база (difficult_start) и два множителя от неё; пороги в таблице
        // в секундах, в коде — в минутах.
        var cfg = BalanceService.Config;
        if (cfg != null)
        {
            timeBetweenWaves = cfg.waves.delayPerWave;
            enemiesPerWave = cfg.waves.enemyPerWave;
            healthAddPerWave = cfg.waves.hpAddPerWave;
            damageAddPerWave = cfg.waves.damageAddPerWave;

            multiplierGrowthPercent = cfg.waves.difficultStart;
            if (cfg.waves.difficultStart > 0f)
            {
                growthStage1Multiplier = cfg.waves.difficultMid / cfg.waves.difficultStart;
                growthStage2Multiplier = cfg.waves.difficultEnd / cfg.waves.difficultStart;
            }
            timeMark1Minutes = cfg.waves.timeDifficultMid / 60f;
            timeMark2Minutes = cfg.waves.timeDifficultEnd / 60f;
        }

        // --- ДОБАВЛЕНО ---
        baseMultiplierGrowthPercent = multiplierGrowthPercent;
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

        if (waveTimerUI != null)
            waveTimerUI.SetProgress(waveTimer / timeBetweenWaves);

        if (waveTimer >= timeBetweenWaves)
        {
            SpawnWave();
            waveTimer = 0f;

            if (waveTimerUI != null)
                waveTimerUI.SetProgress(0f);
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
        float k = 1f + (multiplierGrowthPercent / 100f);
        difficultyMultiplier *= k;

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

        float stageMultiplier = 1f;

        if (minutes >= timeMark2Minutes)
            stageMultiplier = growthStage2Multiplier;      // по умолчанию 3x от базы (1.5*2)
        else if (minutes >= timeMark1Minutes)
            stageMultiplier = growthStage1Multiplier;      // по умолчанию 1.5x от базы

        multiplierGrowthPercent = baseMultiplierGrowthPercent * stageMultiplier;
    }
    // ---------------

    private void NotifyUI()
    {
        OnWaveSpawned?.Invoke(CurrentWaveNumber, difficultyMultiplier, flatHealthBonus, flatDamageBonus);
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
