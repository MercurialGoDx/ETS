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
    [Tooltip("Интервал спавна босса. Таймер и решение \"волна или босс\" держит EnemySpawner — здесь только значение.")]
    public float spawnEverySeconds = 300f; // 5 минут

    [Header("Difficulty scaling")]
    public EnemySpawner enemySpawner;

    [Tooltip("Множитель базового HP босса до применения общей сложности.")]
    public float bossHpMultiplier = 1.5f;

    [Tooltip("Множитель базового урона босса до применения общей сложности.")]
    public float bossDamageMultiplier = 1f;

    [Tooltip("Плоская прибавка к урону босса, не участвующая в умножении (не растёт вместе с damageMultiplier/сложностью).")]
    public float bossAdditionalDamage = 0f;

    private Transform player;
    public BossRewardUI bossRewardUI;

    [Header("Boss Health Bar")]
    public BossHealthBar bossHealthBar;

    private class BossEntry
    {
        public GameObject instance;
        public Enemy enemy;
        public float baseHealth;
        public float baseDamage;
    }

    private readonly List<BossEntry> bosses = new List<BossEntry>();
    private Enemy activeBossEnemy = null;
    private int activeBossRewardSelections = 1;

    private void Start()
    {
        // Баланс из таблицы (если импортирован) перекрывает инспектор.
        // Статы самого босса (hp/damage/gold/speed) импортёр стампит в префаб.
        var cfg = BalanceService.Config;
        if (cfg != null)
        {
            spawnEverySeconds = cfg.boss.spawnInterval;
            bossHpMultiplier = cfg.boss.hpMultiplier;
            bossDamageMultiplier = cfg.boss.damageMultiplier;
            bossAdditionalDamage = cfg.boss.additionalDamage;
        }

        FindPlayer();
        BuildBossPool();
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

    /// <summary>
    /// Вызывается извне (EnemySpawner — по расписанию раз в spawnEverySeconds, вместо
    /// очередной волны) и из читов. Спавнит нового босса, даже если предыдущий ещё жив —
    /// расписание боссов ни от чего не зависит.
    /// </summary>
    public void SpawnRandomBoss()
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

        chosen.instance.SetActive(true);
        activeBossEnemy = chosen.enemy;
        activeBossEnemy.isDead = false;

        float healthDifficultyMult = 1f;
        float damageDifficultyMult = 1f;
        float flatHealth = 0f;
        float flatDamage = 0f;

        if (enemySpawner != null)
        {
            enemySpawner.InitializeSpawnedEnemy(
                chosen.enemy,
                chosen.baseHealth,
                chosen.baseDamage,
                bossHpMultiplier,
                bossDamageMultiplier,
                bossAdditionalDamage);

            healthDifficultyMult = enemySpawner.CurrentHealthMultiplier;
            damageDifficultyMult = enemySpawner.CurrentDamageMultiplier;
            flatHealth = enemySpawner.CurrentFlatHealthBonus;
            flatDamage = enemySpawner.CurrentFlatDamageBonus;
        }
        else
        {
            chosen.enemy.InitStats(
                chosen.baseHealth * bossHpMultiplier,
                chosen.baseDamage * bossDamageMultiplier + bossAdditionalDamage);

            EnemyEffectManager.Instance?.ApplyEffectsToEnemy(chosen.enemy);
            Debug.LogWarning("[BossManager] EnemySpawner is not assigned; boss spawned without wave scaling");
        }

        activeBossRewardSelections = 1;
        UpgradesRuntimeData runtime = UpgradesManager.Instance?.GameplayRuntimeData;
        if (runtime != null && runtime.TryConsumeBossContract(
                out int contractStacks,
                out float contractHealthMultiplier,
                out float contractDamageMultiplier,
                out Material contractOutlineMaterial,
                out Color contractOutlineColor,
                out float contractOutlineWidth,
                out float contractGlowIntensity,
                out float contractScaleMultiplier))
        {
            chosen.enemy.InitStats(
                chosen.enemy.maxHealth * contractHealthMultiplier,
                chosen.enemy.damageToPlayer * contractDamageMultiplier);
            chosen.enemy.ApplyBossContractVisual(
                contractOutlineMaterial,
                contractOutlineColor,
                contractOutlineWidth,
                contractGlowIntensity,
                contractScaleMultiplier);
            activeBossRewardSelections += contractStacks;

            Debug.Log(
                $"[BossContract] Applied to {chosen.instance.name}: stacks={contractStacks}, " +
                $"HP=x{contractHealthMultiplier:0.##}, damage=x{contractDamageMultiplier:0.##}, " +
                $"outline={contractOutlineWidth:0.###}, glow={contractGlowIntensity:0.##}, " +
                $"reward selections={activeBossRewardSelections}.");
        }

        if (bossHealthBar != null)
            bossHealthBar.Show(activeBossEnemy);

        Debug.Log(
            $"[BossManager] Boss spawned ({chosen.instance.name}) | " +
            $"baseHp={chosen.baseHealth:F1}, baseDmg={chosen.baseDamage:F1}, " +
            $"difficultyHp=x{healthDifficultyMult:F3}, difficultyDmg=x{damageDifficultyMult:F3}, " +
            $"flatHp={flatHealth:F1}, flatDmg={flatDamage:F1}, " +
            $"bossHp=x{bossHpMultiplier:F2}, bossDmg=x{bossDamageMultiplier:F2}, additionalDmg={bossAdditionalDamage:F1}, " +
            $"finalHp={chosen.enemy.maxHealth:F1}, finalDmg={chosen.enemy.damageToPlayer:F1}");
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

        if (AchievementManager.Instance != null)
            AchievementManager.Instance.NotifyBossDefeated();

        if (bossHealthBar != null)
            bossHealthBar.Hide();

        int rewardSelections = activeBossRewardSelections;
        activeBossRewardSelections = 1;

        if (bossRewardUI != null)
            bossRewardUI.Open(rewardSelections);
        else
            Debug.LogWarning("[BossManager] BossRewardUI not assigned");

        Debug.Log($"[BossManager] Boss defeated -> reward selections opened: {rewardSelections}.");
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
