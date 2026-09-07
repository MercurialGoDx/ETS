using UnityEngine;

public class RegenAuraDamage : MonoBehaviour
{
    private const float BossVisualScaleMultiplier = 1f / 3f;

    [Header("Глобальный урон по всем врагам от регена")]
    [Tooltip("Включить/выключить ауру урона от регена (включится при покупке апгрейда).")]
    public bool regenAuraEnabled = false;

    [Tooltip("Множитель урона от суммарного регена (2 = урон в 2 раза больше регена).")]
    public float regenAuraMultiplier = 0f;

    [Tooltip("Интервал между тиками урона по всем врагам (секунды).")]
    public float regenAuraTickInterval = 1f;

    private float regenAuraTimer = 0f;
    private UpgradeBaseSO statisticsSource;

    private readonly System.Collections.Generic.Dictionary<Enemy, GameObject> enemyVisuals = new();
    private readonly System.Collections.Generic.List<Enemy> enemyBuffer = new();
    private EnemyManager subscribedEnemyManager;
    private GameObject enemyVisualPrefab;
    private float enemyVisualScaleMultiplier = 1f;
    private float enemyVisualHeightOffset = 0.15f;

    public void SetStatisticsSource(UpgradeBaseSO source)
    {
        statisticsSource = source;
    }

    public void ConfigureEnemyVisual(
        GameObject visualPrefab,
        float scaleMultiplier,
        float heightOffset)
    {
        bool prefabChanged = enemyVisualPrefab != visualPrefab;
        enemyVisualPrefab = visualPrefab;
        enemyVisualScaleMultiplier = Mathf.Max(0.01f, scaleMultiplier);
        enemyVisualHeightOffset = heightOffset;

        EnsureEnemyManagerSubscription();

        if (prefabChanged)
            ClearEnemyVisuals();

        AttachVisualToExistingEnemies();
    }

    private void OnEnable()
    {
        EnsureEnemyManagerSubscription();
    }

    private void OnDisable()
    {
        UnsubscribeFromEnemyManager();
        ClearEnemyVisuals();
    }

    private void Update()
    {
        EnsureEnemyManagerSubscription();
        HandleRegenAuraDamage();
    }

    private void EnsureEnemyManagerSubscription()
    {
        EnemyManager currentManager = EnemyManager.Instance;
        if (subscribedEnemyManager == currentManager)
            return;

        UnsubscribeFromEnemyManager();
        subscribedEnemyManager = currentManager;

        if (subscribedEnemyManager == null)
            return;

        subscribedEnemyManager.EnemyRegistered += HandleEnemyRegistered;
        subscribedEnemyManager.EnemyUnregistered += HandleEnemyUnregistered;

        if (regenAuraEnabled)
            AttachVisualToExistingEnemies();
    }

    private void UnsubscribeFromEnemyManager()
    {
        if (subscribedEnemyManager == null)
            return;

        subscribedEnemyManager.EnemyRegistered -= HandleEnemyRegistered;
        subscribedEnemyManager.EnemyUnregistered -= HandleEnemyUnregistered;
        subscribedEnemyManager = null;
    }

    private void HandleEnemyRegistered(Enemy enemy)
    {
        if (regenAuraEnabled)
            AttachVisual(enemy);
    }

    private void HandleEnemyUnregistered(Enemy enemy)
    {
        RemoveVisual(enemy);
    }

    private void HandleEnemyDeath(Enemy enemy)
    {
        RemoveVisual(enemy);
    }

    private void AttachVisualToExistingEnemies()
    {
        if (!regenAuraEnabled || enemyVisualPrefab == null || subscribedEnemyManager == null)
            return;

        subscribedEnemyManager.GetActiveEnemies(enemyBuffer);
        foreach (Enemy enemy in enemyBuffer)
            AttachVisual(enemy);
    }

    private void AttachVisual(Enemy enemy)
    {
        if (!regenAuraEnabled || enemyVisualPrefab == null || enemy == null || enemy.isDead)
            return;

        if (enemyVisuals.TryGetValue(enemy, out GameObject existingVisual))
        {
            if (existingVisual != null)
                return;

            enemyVisuals.Remove(enemy);
        }

        GameObject visual = Instantiate(enemyVisualPrefab, enemy.transform);
        PlaceVisualAboveEnemy(enemy, visual.transform);
        KeepParticleVisualPlaying(visual);
        enemyVisuals.Add(enemy, visual);

        enemy.OnDeath -= HandleEnemyDeath;
        enemy.OnDeath += HandleEnemyDeath;
    }

    private static void KeepParticleVisualPlaying(GameObject visual)
    {
        ParticleSystem[] particleSystems = visual.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem particleSystem in particleSystems)
        {
            ParticleSystem.MainModule main = particleSystem.main;
            main.stopAction = ParticleSystemStopAction.None;

            if (!particleSystem.gameObject.activeSelf)
                particleSystem.gameObject.SetActive(true);

            particleSystem.Play(true);
        }
    }

    private void PlaceVisualAboveEnemy(Enemy enemy, Transform visualTransform)
    {
        float targetTypeScaleMultiplier = IsBoss(enemy)
            ? BossVisualScaleMultiplier
            : 1f;

        Collider targetCollider = enemy.GetComponent<Collider>();
        if (targetCollider == null || !targetCollider.enabled)
        {
            visualTransform.SetLocalPositionAndRotation(
                Vector3.zero,
                enemyVisualPrefab.transform.localRotation);
            visualTransform.localScale = Vector3.Scale(
                enemyVisualPrefab.transform.localScale,
                Vector3.one * enemyVisualScaleMultiplier * targetTypeScaleMultiplier);
            return;
        }

        Bounds worldBounds = targetCollider.bounds;
        bool hasBounds = false;
        Vector3 minimum = Vector3.zero;
        Vector3 maximum = Vector3.zero;
        Vector3 center = worldBounds.center;
        Vector3 extents = worldBounds.extents;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 worldCorner = center + Vector3.Scale(
                        extents,
                        new Vector3(x, y, z));
                    Vector3 localCorner = enemy.transform.InverseTransformPoint(worldCorner);

                    if (!hasBounds)
                    {
                        minimum = localCorner;
                        maximum = localCorner;
                        hasBounds = true;
                    }
                    else
                    {
                        minimum = Vector3.Min(minimum, localCorner);
                        maximum = Vector3.Max(maximum, localCorner);
                    }
                }
            }
        }

        Bounds localBounds = new Bounds(
            (minimum + maximum) * 0.5f,
            maximum - minimum);
        float targetWidth = Mathf.Max(localBounds.size.x, localBounds.size.z);
        float scale = Mathf.Max(
            0.01f,
            targetWidth * enemyVisualScaleMultiplier * targetTypeScaleMultiplier);

        visualTransform.localPosition = new Vector3(
            localBounds.center.x,
            localBounds.max.y + enemyVisualHeightOffset * scale,
            localBounds.center.z);
        visualTransform.localRotation = enemyVisualPrefab.transform.localRotation;
        visualTransform.localScale = Vector3.Scale(
            enemyVisualPrefab.transform.localScale,
            Vector3.one * scale);
    }

    private static bool IsBoss(Enemy enemy)
    {
        return enemy.TryGetComponent(out EnemyStatusEffectController statusEffects) &&
               statusEffects.IsBoss;
    }

    private void RemoveVisual(Enemy enemy)
    {
        if (enemy == null)
            return;

        enemy.OnDeath -= HandleEnemyDeath;

        if (!enemyVisuals.TryGetValue(enemy, out GameObject visual))
            return;

        enemyVisuals.Remove(enemy);
        if (visual != null)
            Destroy(visual);
    }

    private void ClearEnemyVisuals()
    {
        foreach (System.Collections.Generic.KeyValuePair<Enemy, GameObject> pair in enemyVisuals)
        {
            if (pair.Key != null)
                pair.Key.OnDeath -= HandleEnemyDeath;

            if (pair.Value != null)
                Destroy(pair.Value);
        }

        enemyVisuals.Clear();
        enemyBuffer.Clear();
    }

    private void HandleRegenAuraDamage()
    {
        if (!regenAuraEnabled)
            return;

        if (regenAuraMultiplier <= 0f)
            return;

        if (UpgradesManager.Instance.playerHealth == null)
            return;

        regenAuraTimer += Time.deltaTime;
        if (regenAuraTimer < regenAuraTickInterval)
            return;

        regenAuraTimer = 0f;
        ApplyRegenAuraDamage();
    }

    private void ApplyRegenAuraDamage()
    {
        var upgrades = UpgradesManager.Instance;
        var playerHealth = upgrades.playerHealth;

        // 1) Use the same final regeneration value as healing and the stats UI.
        float totalRegen = playerHealth.GetTotalRegen();
        if (totalRegen <= 0f)
            return;

        // 2) Degen Aura использует тот же набор универсальных множителей, что и шипы:
        // общий урон, генератор, бонусы от HP/золота, global- и adaptive-слои.
        // Тип урона и тир намеренно исключены — аура не является оружием.
        float baseDamagePerEnemy = totalRegen * regenAuraMultiplier;
        TowerAttack towerAttack = upgrades.context != null
            ? upgrades.context.towerAttack
            : null;
        if (towerAttack == null)
            towerAttack = upgrades.towerAttack;

        float universalDamageMultiplier = towerAttack != null
            ? towerAttack.GetGlobalDamageMultiplier()
            : 1f;
        float damagePerEnemy = baseDamagePerEnemy * universalDamageMultiplier;

        // 3) Наносим урон всем врагам на сцене
        // Хранить всех доступных врагов в одном листе
        Enemy[] enemies = Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);

        if (enemies.Length == 0)
            return;

        foreach (var e in enemies)
        {
            if (e == null || e.isDead)
                continue;

            e.TakeWeaponDamage(damagePerEnemy, WeaponDamageType.Chaos);
            DamageStatsManager.Instance?.RegisterDamage(statisticsSource, damagePerEnemy);
        }

        Debug.Log(
            $"[RegenAura] Tick: regen={totalRegen:F1}, mult={regenAuraMultiplier:F2}, " +
            $"base={baseDamagePerEnemy:F1}, universal=x{universalDamageMultiplier:F3}, " +
            $"final={damagePerEnemy:F1}, enemies={enemies.Length}");
    }
}

