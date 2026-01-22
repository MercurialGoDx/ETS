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
    public float multiplierGrowthPercent = 10f;

    [Header("Прогрессия сложности (фикс. прибавка)")]
    public float healthAddPerWave = 5f;
    public float damageAddPerWave = 1f;

    [Header("Текущее накопление (не трогать руками)")]
    [SerializeField] private float flatHealthBonus = 0f;
    [SerializeField] private float flatDamageBonus = 0f;

    // ✅ Событие для UI: волна, множитель, фиксHP, фиксDMG
    public event Action<int, float, float, float> OnWaveSpawned;

    private float waveTimer = 0f;
    private int currentWaveIndex = 0;

    // ✅ Чтобы UI мог забрать актуальные значения
    public int CurrentWaveNumber => currentWaveIndex + 1;
    public float CurrentMultiplier => difficultyMultiplier;
    public float CurrentFlatHealthBonus => flatHealthBonus;
    public float CurrentFlatDamageBonus => flatDamageBonus;

    private void Start()
    {
        if (tower == null)
        {
            GameObject towerObj = GameObject.FindGameObjectWithTag("Player");
            if (towerObj != null)
                tower = towerObj.transform;
        }

        // ✅ Чтобы UI показал значения уже до первой волны
        NotifyUI();
    }

    private void Update()
    {
        if (tower == null || enemyPrefabs == null || enemyPrefabs.Length == 0)
            return;

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

    void SpawnWave()
    {
        int enemyTypeIndex = currentWaveIndex % enemyPrefabs.Length;
        GameObject enemyPrefab = enemyPrefabs[enemyTypeIndex];

        Enemy prefabEnemy = enemyPrefab.GetComponent<Enemy>();
        float baseHealth = prefabEnemy.maxHealth;
        float baseDamage = prefabEnemy.damageToPlayer;

        float currentMult = difficultyMultiplier;

        for (int i = 0; i < enemiesPerWave; i++)
        {
            Vector3 spawnPos = GetSpawnPositionAroundTower();

            GameObject obj = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
            Enemy enemyInstance = obj.GetComponent<Enemy>();
            if (enemyInstance == null) continue;

            enemyInstance.isDead = false;

            float hp = (baseHealth * currentMult) + flatHealthBonus;
            float dmg = (baseDamage * currentMult) + flatDamageBonus;

            enemyInstance.InitStats(hp, dmg);

            if (EnemyEffectManager.Instance != null)
                EnemyEffectManager.Instance.ApplyEffectsToEnemy(enemyInstance);
        }

        // ✅ Сообщаем UI: "эта волна заспавнена" (UI сам пересчитает по своим базовым 15/2)
        NotifyUI();

        // след. волна
        currentWaveIndex++;

        // рост %
        float k = 1f + (multiplierGrowthPercent / 100f);
        difficultyMultiplier *= k;

        // рост фикс
        flatHealthBonus += healthAddPerWave;
        flatDamageBonus += damageAddPerWave;
    }

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
