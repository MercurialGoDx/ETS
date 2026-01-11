using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Ссылки")]
    public Transform tower;             // Башня (игрок)
    public WaveTimerUI waveTimerUI;   // 🔹 ДОБАВИЛИ

    [Header("Волны")]
    public GameObject[] enemyPrefabs;   // 0 - type1, 1 - type2, 2 - type3
    public int enemiesPerWave = 10;
    public float timeBetweenWaves = 10f;

    [Header("Где спавним")]
    public float spawnRadiusMin = 18f;
    public float spawnRadiusMax = 20f;

    [Header("Прогрессия сложности")]
    [Tooltip("Текущий множитель сложности (здоровье/урон врагов)")]
    public float difficultyMultiplier = 1f;
    [Tooltip("На сколько процентов увеличивать множитель после каждой волны")]
    public float multiplierGrowthPercent = 10f;   // 10% = *1.1

    private float waveTimer = 0f;
    private int currentWaveIndex = 0;   // 0 -> type1, 1 -> type2, 2 -> type3 -> потом снова 0

    private void Start()
    {
        if (tower == null)
        {
            // Если не задано в инспекторе — пробуем найти по тегу
            GameObject towerObj = GameObject.FindGameObjectWithTag("Player");
            if (towerObj != null)
                tower = towerObj.transform;
        }
    }

    private void Update()
    {
        if (tower == null || enemyPrefabs == null || enemyPrefabs.Length == 0)
            return;

        waveTimer += Time.deltaTime;

        // 🔹 обновляем шкалу каждый кадр
        if (waveTimerUI != null)
            waveTimerUI.SetProgress(waveTimer / timeBetweenWaves);

        if (waveTimer >= timeBetweenWaves)
        {
            SpawnWave();
            waveTimer = 0f;

            // 🔹 после спавна волны обнуляем шкалу
            if (waveTimerUI != null)
                waveTimerUI.SetProgress(0f);
        }
    }

    void SpawnWave()
    {
        // Тип врага для этой волны
        int enemyTypeIndex = currentWaveIndex % enemyPrefabs.Length;
        GameObject enemyPrefab = enemyPrefabs[enemyTypeIndex];

        // Берём базовые статы с ПРЕФАБА (они не умножены)
        Enemy prefabEnemy = enemyPrefab.GetComponent<Enemy>();
        float baseHealth = prefabEnemy.maxHealth;
        float baseDamage = prefabEnemy.damageToPlayer;

        // Сколько *сейчас* множитель
        float currentMult = difficultyMultiplier;

        for (int i = 0; i < enemiesPerWave; i++)
        {
            Vector3 spawnPos = GetSpawnPositionAroundTower();

            GameObject obj = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
            Enemy enemyInstance = obj.GetComponent<Enemy>();
            enemyInstance.isDead = false;

            if (enemyInstance != null)
            {
                float hp = baseHealth * currentMult;
                float dmg = baseDamage * currentMult;

                enemyInstance.InitStats(hp, dmg);

                if (EnemyEffectManager.Instance != null)
                {
                    EnemyEffectManager.Instance.ApplyEffectsToEnemy(enemyInstance);
                }
            }
        }

        // Переходим к следующей волне другого типа
        currentWaveIndex++;

        // Увеличиваем множитель: +10% от текущего
        float k = 1f + (multiplierGrowthPercent / 100f); // 1.1 при 10%
        difficultyMultiplier *= k;
    }

    Vector3 GetSpawnPositionAroundTower()
    {
        // Случайное направление
        float angle = Random.Range(0f, 360f);
        float rad = angle * Mathf.Deg2Rad;

        float x = Mathf.Cos(rad);
        float z = Mathf.Sin(rad);

        Vector3 dir = new Vector3(x, 0f, z).normalized;

        float radius = Random.Range(spawnRadiusMin, spawnRadiusMax);

        Vector3 pos = tower.position + dir * radius;

        // Высота
        pos.y = 6f;

        return pos;
    }

}