using System.Collections.Generic;
using ETS.Multiplayer;
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

    [Tooltip("Тип обычного врага, чьи итоговые статы служат основой босса: melee, mid или range.")]
    public string bossReferenceEnemy = "melee";

    [Tooltip("Множитель итогового HP обычного врага из bossReferenceEnemy. Базовое HP босса прибавляется после и не масштабируется.")]
    public float bossHpMultiplier = 1.5f;

    [Tooltip("Множитель итогового урона обычного врага из bossReferenceEnemy.")]
    public float bossDamageMultiplier = 4f;

    [Tooltip("Базовый урон босса, который прибавляется после умножения урона обычного врага и ни на что не умножается.")]
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
    }

    private readonly List<BossEntry> bosses = new List<BossEntry>();
    private Enemy activeBossEnemy = null;
    private int activeBossRewardSelections = 1;

    // Порядковый номер спавна и номер босса, который сейчас на поле. В дуэли по ним
    // адресуются выбор босса и его награда: пропущенный босс не должен сдвигать остальные.
    private int bossSpawnIndex;
    private int activeBossOrdinal;

    /// <summary>Номер босса на поле, начиная с нуля. Нужен награде.</summary>
    public int ActiveBossOrdinal => activeBossOrdinal;

    /// <summary>
    /// Number of bosses defeated in the current run. The first opened reward screen
    /// therefore has index 1, the second index 2, and so on.
    /// </summary>
    public int DefeatedBossCount { get; private set; }

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
            bossReferenceEnemy = cfg.boss.referenceEnemy;
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
            enemy.OnDeath -= HandleBossDeath;
            enemy.OnDeath += HandleBossDeath;

            bosses.Add(new BossEntry
            {
                instance = inst,
                enemy = enemy,
                baseHealth = baseHp
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

        int pickIndex = DuelSession.IsSeeded
            ? DuelRandom.Range(DuelSession.Seed, DuelStream.BossPick,
                DuelRandom.Compose(bossSpawnIndex, 0), 0, available.Count)
            : Random.Range(0, available.Count);

        BossEntry chosen = available[pickIndex];

        Vector3 spawnPos = GetRandomPointAroundPlayer();
        chosen.instance.transform.position = spawnPos;
        chosen.instance.transform.rotation = Quaternion.identity;

        chosen.instance.SetActive(true);
        activeBossOrdinal = bossSpawnIndex;
        activeBossEnemy = chosen.enemy;
        activeBossEnemy.isDead = false;

        float referenceEnemyHealth = 0f;
        float referenceEnemyDamage = 0f;

        if (enemySpawner != null)
        {
            if (!enemySpawner.InitializeSpawnedBoss(
                chosen.enemy,
                bossReferenceEnemy,
                chosen.baseHealth,
                bossAdditionalDamage,
                bossHpMultiplier,
                bossDamageMultiplier,
                out referenceEnemyHealth,
                out referenceEnemyDamage))
            {
                chosen.enemy.InitStats(chosen.baseHealth, bossAdditionalDamage);
                EnemyEffectManager.Instance?.ApplyEffectsToEnemy(chosen.enemy);
                Debug.LogWarning($"[BossManager] Enemy reference '{bossReferenceEnemy}' is unavailable; boss spawned with base stats only");
            }
        }
        else
        {
            chosen.enemy.InitStats(
                chosen.baseHealth,
                bossAdditionalDamage);

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
            $"referenceEnemy={bossReferenceEnemy}, referenceHp={referenceEnemyHealth:F1}, referenceDmg={referenceEnemyDamage:F1}, " +
            $"baseBossHp={chosen.baseHealth:F1}, baseBossDmg={bossAdditionalDamage:F1}, " +
            $"bossHp=x{bossHpMultiplier:F2}, bossDmg=x{bossDamageMultiplier:F2}, " +
            $"finalHp={chosen.enemy.maxHealth:F1}, finalDmg={chosen.enemy.damageToPlayer:F1}");

        // Считаем после спавна: и выбор босса, и его награда адресуются текущим номером.
        bossSpawnIndex++;
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
        DefeatedBossCount++;

        if (AchievementManager.Instance != null)
            AchievementManager.Instance.NotifyBossDefeated();

        if (bossHealthBar != null)
            bossHealthBar.Hide();

        int rewardSelections = activeBossRewardSelections;
        activeBossRewardSelections = 1;

        if (bossRewardUI != null)
            bossRewardUI.Open(rewardSelections, activeBossOrdinal);
        else
            Debug.LogWarning("[BossManager] BossRewardUI not assigned");

        Debug.Log(
            $"[BossManager] Boss #{DefeatedBossCount} defeated -> " +
            $"reward selections opened: {rewardSelections}.");
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
