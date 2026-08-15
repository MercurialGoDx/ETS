using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.GraphicsBuffer;

public class TowerAttack : MonoBehaviour
{
    [Header("Общее")]
    public float range = 10f;
    public Transform firePoint;

    [Header("Дополнительные точки огня")]
    public Transform waveFirePoint;  // ← нижняя точка для Wave

    [Header("Stagger Fire (залп с задержками)")]
    [Range(0f, 1f)]
    public float volleyWindowPercent = 0.5f; // 50% от времени между атаками

    [Header("Глобальные бонусы")]
    public float fireRateMultiplier = 1f;   // 1 = без бонусов
    [SerializeField] private float globalFireRateMultiplier = 1f;
    public float GlobalFireRateMultiplier => globalFireRateMultiplier;
    public float TotalFireRateMultiplier => fireRateMultiplier * globalFireRateMultiplier;

    [Header("Wave настройки")]
    public float waveForwardOffset = 1.5f;   // Насколько вынести вперёд от башни
    public float waveHeightOffset = 0f;      // Смещение волны по высоте (обычно 0)

    [Header("Debug")]
    [SerializeField] private bool debugDamage = false;

    private DamageCalculator damageCalculator;

    private List<WeaponRuntime> weapons = new List<WeaponRuntime>();

    public bool DebugDamageEnabled => debugDamage;

    public int GetTotalWeaponsOfType(WeaponDamageType type)
    {
        int total = 0;

        foreach (var w in weapons)
        {
            if (w.def != null && w.def.damageType == type)
            {
                total += w.stacks;   // учитываем все стеки этого оружия
            }
        }

        return total;
    }

    /// <summary>
    /// Снимок купленного оружия для панелей UI. Только чтение: наружу уходят копии,
    /// внутренний список и WeaponRuntime мутировать извне нельзя.
    /// </summary>
    public readonly struct OwnedWeapon
    {
        public readonly WeaponDefinition Def;
        public readonly int Stacks;

        public OwnedWeapon(WeaponDefinition def, int stacks)
        {
            Def = def;
            Stacks = stacks;
        }
    }

    public List<OwnedWeapon> GetOwnedWeapons()
    {
        var result = new List<OwnedWeapon>(weapons.Count);

        foreach (var w in weapons)
        {
            if (w.def != null)
                result.Add(new OwnedWeapon(w.def, w.stacks));
        }

        return result;
    }

    private void Awake()
    {
        // Баланс из таблицы (если импортирован) перекрывает инспектор.
        var cfg = BalanceService.Config;
        if (cfg != null)
        {
            range = cfg.player.towerRange;
            volleyWindowPercent = cfg.player.volleyWindowPercent;
        }
    }

    public void Init(DamageCalculator calculator)
    {
        damageCalculator = calculator;
    }

    private void Update()
    {
        if (weapons.Count == 0) return;

        foreach (var weapon in weapons)
        {
            // Если это аура, башня её не "стреляет" — она работает сама по себе
            if (weapon.auraInstance != null)
                continue;

            weapon.cooldown -= Time.deltaTime;
            if (weapon.cooldown <= 0f)
            {
                bool fired = FireWeapon(weapon);
                if (fired)
                    weapon.cooldown = 1f / (weapon.def.fireRate * TotalFireRateMultiplier);
            }
        }
    }

    bool FireWeapon(WeaponRuntime weapon)
    {
        if (weapon.def.bulletPrefab == null || firePoint == null)
            return false;

        if (weapon.isVolleyInProgress)
            return false;

        List<Enemy> enemiesInRange = EnemyManager.Instance.GetEnemiesInRange(transform.position, range);
        if (enemiesInRange.Count == 0)
            return false;

        // Стартуем "залп с задержками"
        weapon.isVolleyInProgress = true;
        StartCoroutine(FireWeaponStaggered(weapon, enemiesInRange));
        return true; // важно: чтобы кулдаун поставился, как и раньше
    }

    private IEnumerator FireWeaponStaggered(WeaponRuntime weapon, List<Enemy> enemiesInRange)
    {
        int stacks = weapon.stacks;
        if (stacks <= 0)
        {
            FinishWeaponVolley(weapon);
            yield break;
        }

        // Время между атаками (учитывает GlobalFireRate через fireRateMultiplier)
        float attackInterval = 1f / (weapon.def.fireRate * TotalFireRateMultiplier);

        // Окно залпа = 50% (или сколько поставишь в инспекторе)
        float volleyWindow = attackInterval * Mathf.Clamp01(volleyWindowPercent);

        float stepDelay = 0f;
        if (stacks > 1)
            stepDelay = volleyWindow / (stacks - 1);

        // гарантируем размер списка закреплённых целей
        while (weapon.lastTargets.Count < stacks)
            weapon.lastTargets.Add(null);
        if (weapon.lastTargets.Count > stacks)
            weapon.lastTargets.RemoveRange(stacks, weapon.lastTargets.Count - stacks);

        bool randomEachShot = (weapon.def.targetingMode == WeaponTargetingMode.RandomEachShot);

        var usedThisVolley = new HashSet<Enemy>();

        for (int i = 0; i < stacks; i++)
        {
            Enemy target = null;

            // --- 1) Пытаемся взять старую цель, только если режим "LockUntilDeath"
            if (!randomEachShot)
            {
                target = weapon.lastTargets[i];

                bool targetValid = false;
                if (target != null && !target.isDead)
                {
                    float sqrRange = range * range;
                    float sqrDist = (transform.position - target.transform.position).sqrMagnitude;
                    if (sqrDist <= sqrRange && target != null && !target.isDead)
                        targetValid = true;
                }

                if (!targetValid)
                    target = null;
            }

            // --- 2) Если цели нет (или режим random) — выбираем новую
            if (target == null)
            {
                Enemy newTarget = null;

                if (enemiesInRange.Count == 1)
                {
                    newTarget = enemiesInRange[0];
                }
                else if (enemiesInRange.Count > 1)
                {
                    // стараемся раздать по разным врагам в этом залпе
                    List<Enemy> candidates = new List<Enemy>();
                    foreach (var e in enemiesInRange)
                    {
                        if (!usedThisVolley.Contains(e))
                            candidates.Add(e);
                    }

                    if (candidates.Count > 0)
                        newTarget = candidates[Random.Range(0, candidates.Count)];
                    else
                        newTarget = enemiesInRange[Random.Range(0, enemiesInRange.Count)];
                }

                target = newTarget;
            }

            if (target != null)
            {
                usedThisVolley.Add(target);

                if (!randomEachShot)
                    weapon.lastTargets[i] = target;

                SpawnBullet(weapon, target, i);
            }

            // задержка до следующего выстрела в залпе
            if (stepDelay > 0f && i < stacks - 1)
                yield return new WaitForSeconds(stepDelay);
        }

        FinishWeaponVolley(weapon);
    }

    private void SpawnBullet(WeaponRuntime weapon, Enemy target, int stackIndex)
    {
        if (target == null || target.isDead)
            return;

        float runtimeBaseDamage = GetRuntimeBaseDamage(weapon);
        float damage = GetBaseProjectileDamage(weapon);

        LaserBeam existingBeam = LaserBeam.GetActiveBeamFor(target, weapon.def, stackIndex);
        if (existingBeam != null && existingBeam.gameObject.activeSelf)
        {
            // обновляем только параметры, не создаём новый
            existingBeam.RefreshContext(
                firePoint,
                runtimeBaseDamage,
                damage,
                this,
                weapon.def.fireRate);
            return;
        }

        GameObject obj = PoolManager.Instance.Spawn(weapon.def.bulletPrefab, firePoint.position, Quaternion.identity);

        IAttackBehaviour attack = obj.GetComponent<IAttackBehaviour>();
        if (attack == null)
            return;

        attack.InitAttack(new AttackContext
        {
            firePoint = firePoint,
            target = target.transform,
            baseDamage = runtimeBaseDamage,
            damage = damage,
            projectileSpeed = weapon.def.projectileSpeed,
            damageCalculator = damageCalculator,
            ownerTower = this,
            weaponFireRate = weapon.def.fireRate,
            owner = transform,
            heightOffset = waveHeightOffset,
            forwardOffset = waveForwardOffset,
            weapon = weapon.def,
            weaponStackIndex = stackIndex
        });
    }

    public void RequestImmediateRetarget(WeaponDefinition weaponDefinition)
    {
        if (weaponDefinition == null)
            return;

        foreach (var weapon in weapons)
        {
            if (weapon.def != weaponDefinition)
                continue;

            for (int i = 0; i < weapon.lastTargets.Count; i++)
            {
                Enemy target = weapon.lastTargets[i];
                if (target == null || target.isDead || !target.gameObject.activeInHierarchy)
                    weapon.lastTargets[i] = null;
            }

            if (weapon.isVolleyInProgress)
                weapon.immediateRetargetRequested = true;
            else
                weapon.cooldown = 0f;

            if (debugDamage)
                Debug.Log($"[Laser] Immediate retarget requested for {weaponDefinition.name}.");

            return;
        }
    }

    private static void FinishWeaponVolley(WeaponRuntime weapon)
    {
        weapon.isVolleyInProgress = false;

        if (!weapon.immediateRetargetRequested)
            return;

        weapon.immediateRetargetRequested = false;
        weapon.cooldown = 0f;
    }

    public void AddWeapon(WeaponDefinition def)
    {
        // 1) Если оружие такого типа уже есть
        foreach (var w in weapons)
        {
            if (w.def == def)
            {
                w.stacks++;

                // 🔹 Если это аура – просто увеличиваем её стеки и обновляем урон
                if (w.auraInstance != null)
                {
                    w.auraInstance.UpdateStacks(w.stacks, def.damagePerProjectile);
                }

                return;
            }
        }

        // 2) Если оружия ещё не было — добавляем новое
        WeaponRuntime newWeapon = new WeaponRuntime
        {
            def = def,
            stacks = 1,
            cooldown = 0f,
            lastTargets = new List<Enemy>(),
            auraInstance = null,
            isVolleyInProgress = false,
            immediateRetargetRequested = false,
            baseDamageBonus = 0f
        };

        // 🔹 Проверяем, является ли пулей для этого оружия аура
        if (def.bulletPrefab != null)
        {
            AuraDamageZone auraPrefab = def.bulletPrefab.GetComponent<AuraDamageZone>();
            if (auraPrefab != null)
            {
                GameObject auraObj = Instantiate(def.bulletPrefab, transform.position, Quaternion.identity);
                auraObj.transform.SetParent(transform);

                AuraDamageZone auraInstance = auraObj.GetComponent<AuraDamageZone>();
                if (auraInstance != null)
                {
                    auraInstance.Init(
                        def.damagePerProjectile,
                        1,
                        def.damageType,
                        def.itemTier,
                        damageCalculator   //  ключевой момент
                    );
                    auraInstance.debugDamage = auraInstance.debugDamage || debugDamage;
                    newWeapon.auraInstance = auraInstance;
                }
            }
        }

        weapons.Add(newWeapon);
    }

    public bool AddWeaponBaseDamage(WeaponDefinition def, float amount)
    {
        if (def == null || amount <= 0f)
            return false;

        foreach (var weapon in weapons)
        {
            if (weapon.def != def)
                continue;

            weapon.baseDamageBonus += amount;

            if (debugDamage)
            {
                float currentBaseDamage = GetRuntimeBaseDamage(weapon);
                Debug.Log(
                    $"[DamageDebug] {def.name}: kill scaling +{amount:0.###} base damage, " +
                    $"SO base={def.damagePerProjectile:0.###}, accumulated bonus={weapon.baseDamageBonus:0.###}, " +
                    $"runtime base={currentBaseDamage:0.###}, stacks={weapon.stacks}.");
            }

            return true;
        }

        if (debugDamage)
            Debug.LogWarning($"[DamageDebug] Cannot add base damage: {def.name} is not owned by the tower.");

        return false;
    }

    public void ApplyUpgrade(UpgradeDefinition upgrade)
    {
        if (upgrade == null) return;

        switch (upgrade.type)
        {
            case UpgradeType.GlobalFireRate:
                float add = upgrade.valuePercent / 100f;  // 10% → 0.1
                fireRateMultiplier += add;
                break;
        }
    }

    public void AddGlobalFireRateMultiplier(float percent)
    {
        globalFireRateMultiplier += percent;
    }

    private float GetFinalDamage(WeaponRuntime weapon)
    {
        DamageContext context = new DamageContext
        {
            baseDamage = GetRuntimeBaseDamage(weapon),
            damageType = weapon.def.damageType,
            itemTier = weapon.def.itemTier,
            isSpikes = false
        };

        var breakdown = damageCalculator.CalculateWithBreakdown(context);

        if (debugDamage)
        {
            bool usesCatapultDamage = weapon.def.damagePerProjectile == 0f
                && weapon.def.bulletPrefab != null
                && weapon.def.bulletPrefab.GetComponent<Catapult>() != null;

            if (usesCatapultDamage)
                Debug.Log($"[DamageDebug] {weapon.def.name}: special catapult damage, see projectile explode log.");
            else
                Debug.Log($"[DamageDebug] {weapon.def.name}: {breakdown.ToDebugString()}");
        }

        return breakdown.FinalDamage;
    }

    private float GetBaseProjectileDamage(WeaponRuntime weapon)
    {
        float runtimeBaseDamage = GetRuntimeBaseDamage(weapon);
        bool usesCatapultDamage = weapon.def.bulletPrefab != null
            && weapon.def.bulletPrefab.GetComponent<Catapult>() != null;

        if (usesCatapultDamage)
        {
            if (debugDamage)
                Debug.Log($"[DamageDebug] {weapon.def.name}: runtime base = {runtimeBaseDamage:0.###}, HP bonus will be added on explode before multipliers.");

            return runtimeBaseDamage;
        }

        return GetFinalDamage(weapon);
    }

    private static float GetRuntimeBaseDamage(WeaponRuntime weapon)
    {
        return weapon.def.damagePerProjectile + weapon.baseDamageBonus;
    }


    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}

