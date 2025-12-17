using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossManager : MonoBehaviour
{
    [Header("Boss Prefabs")]
    [Tooltip("Список префабов боссов. Будет выбран случайный.")]
    public List<GameObject> bossPrefabs = new List<GameObject>();

    [Header("Boss Rewards")]
    [Header("Spawn around Player")]
    public string playerTag = "Player";
    public float spawnRadius = 20f;
    public float spawnY = 6f;

    [Header("Timing")]
    public float spawnEverySeconds = 300f; // 5 минут
    public bool waitBossDeathBeforeNextSpawn = true;

    [Header("Difficulty scaling")]
    public EnemySpawner enemySpawner;
    public float bossExtraMultiplier = 1f;

    private Transform player;
    public BossRewardUI bossRewardUI;

    private class BossEntry
    {
        public GameObject instance;
        public Enemy enemy;
        public float baseHealth;
        public float baseDamage;
    }

    private readonly List<BossEntry> bosses = new List<BossEntry>();
    private Enemy activeBossEnemy = null;

    private void Start()
    {
        FindPlayer();
        BuildBossPool();
        StartCoroutine(BossLoop());
    }

    private void FindPlayer()
    {
        GameObject obj = GameObject.FindGameObjectWithTag(playerTag);
        if (obj != null)
            player = obj.transform;
        else
            Debug.LogError($"[BossManager] Player with tag '{playerTag}' not found");
    }

    private void BuildBossPool()
    {
        bosses.Clear();

        if (bossPrefabs == null || bossPrefabs.Count == 0)
        {
            Debug.LogError("[BossManager] Boss prefabs list is empty");
            enabled = false;
            return;
        }

        foreach (var prefab in bossPrefabs)
        {
            if (prefab == null) continue;

            GameObject inst = Instantiate(prefab);
            inst.SetActive(false);

            Enemy enemy = inst.GetComponent<Enemy>();
            if (enemy == null)
            {
                Debug.LogError($"[BossManager] Prefab '{prefab.name}' has no Enemy component");
                Destroy(inst);
                continue;
            }

            enemy.returnToPoolInsteadOfDestroy = true;

            float baseHp = enemy.maxHealth;
            float baseDmg = enemy.damageToPlayer;

            enemy.OnDeath -= HandleBossDeath;
            enemy.OnDeath += HandleBossDeath;

            bosses.Add(new BossEntry
            {
                instance = inst,
                enemy = enemy,
                baseHealth = baseHp,
                baseDamage = baseDmg
            });
        }

        if (bosses.Count == 0)
        {
            Debug.LogError("[BossManager] No valid bosses created");
            enabled = false;
        }
    }

    private IEnumerator BossLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnEverySeconds);

            if (waitBossDeathBeforeNextSpawn && activeBossEnemy != null)
                continue;

            SpawnRandomBoss();
        }
    }

    private void SpawnRandomBoss()
    {
        if (player == null)
        {
            FindPlayer();
            if (player == null) return;
        }

        List<BossEntry> available = new List<BossEntry>();
        foreach (var b in bosses)
        {
            if (!b.instance.activeInHierarchy)
                available.Add(b);
        }

        if (available.Count == 0)
        {
            Debug.LogWarning("[BossManager] No available boss in pool");
            return;
        }

        BossEntry chosen = available[Random.Range(0, available.Count)];

        Vector3 spawnPos = GetRandomPointAroundPlayer();
        chosen.instance.transform.position = spawnPos;
        chosen.instance.transform.rotation = Quaternion.identity;

        float difficultyMult = 1f;
        if (enemySpawner != null)
            difficultyMult = enemySpawner.difficultyMultiplier;

        float totalMult = difficultyMult * bossExtraMultiplier;

        chosen.enemy.InitStats(
            chosen.baseHealth * totalMult,
            chosen.baseDamage * totalMult
        );

        chosen.instance.SetActive(true);
        activeBossEnemy = chosen.enemy;

        Debug.Log($"[BossManager] Boss spawned ({chosen.instance.name}) | totalMult={totalMult:F2}");
    }

    private Vector3 GetRandomPointAroundPlayer()
    {
        Vector2 dir2 = Random.insideUnitCircle.normalized;
        Vector3 offset = new Vector3(dir2.x, 0f, dir2.y) * spawnRadius;

        Vector3 pos = player.position + offset;
        pos.y = spawnY;
        return pos;
    }

    private void HandleBossDeath(Enemy deadEnemy)
    {
    if (activeBossEnemy != deadEnemy)
        return;

    activeBossEnemy = null;

    if (bossRewardUI != null)
        bossRewardUI.Open();
    else
        Debug.LogWarning("[BossManager] BossRewardUI not assigned");

    Debug.Log("[BossManager] Boss defeated → reward selection opened");
}

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (player != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(player.position, spawnRadius);
        }
    }
#endif
}
