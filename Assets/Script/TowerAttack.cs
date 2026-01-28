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

    [Header("Wave настройки")]
    public float waveForwardOffset = 1.5f;   // Насколько вынести вперёд от башни
    public float waveHeightOffset = 0f;      // Смещение волны по высоте (обычно 0)

    private bool debugDamage = true;

    private DamageCalculator damageCalculator;

    private readonly List<Enemy> usedThisVolley = new List<Enemy>();

    private List<WeaponRuntime> weapons = new List<WeaponRuntime>();

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
                    weapon.cooldown = 1f / (weapon.def.fireRate * fireRateMultiplier);
            }
        }
    }

    bool FireWeapon(WeaponRuntime weapon)
    {
        if (weapon.def.bulletPrefab == null || firePoint == null)
            return false;

        List<Enemy> enemiesInRange = EnemyManager.Instance.GetEnemiesInRange(transform.position, range);
        if (enemiesInRange.Count == 0)
            return false;

        // Стартуем "залп с задержками"
        StartCoroutine(FireWeaponStaggered(weapon, enemiesInRange));
        return true; // важно: чтобы кулдаун поставился, как и раньше
    }

    private IEnumerator FireWeaponStaggered(WeaponRuntime weapon, List<Enemy> enemiesInRange)
    {
        int stacks = weapon.stacks;
        if (stacks <= 0) yield break;

        // Время между атаками (учитывает GlobalFireRate через fireRateMultiplier)
        float attackInterval = 1f / (weapon.def.fireRate * fireRateMultiplier);

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

        usedThisVolley.Clear();

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
                    //float dist = Vector3.Distance(transform.position, target.transform.position);
                    float sqrRange = range * range;
                    float sqrDist = (transform.position - target.transform.position).sqrMagnitude;
                    if (sqrDist <= sqrRange && target != null && !target.isDead)
                        targetValid = true;
                }

                if (!targetValid)
                    target = null;
            }

            // --- 2) Если цели нет (или режим random) — выбираем новую
            // Разобраться в правильности нахождения candidates.
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

                SpawnBullet(weapon, target);
            }

            // задержка до следующего выстрела в залпе
            if (stepDelay > 0f && i < stacks - 1)
                yield return new WaitForSeconds(stepDelay);
        }
    }

    private void SpawnBullet(WeaponRuntime weapon, Enemy target)
    {
        if (target == null || target.isDead)
        {
            // Сбрасываем цель
            target = null;
            return;
        }

        float damage = GetFinalDamage(weapon);

        LaserBeam existingBeam = LaserBeam.GetActiveBeamFor(target);
        if (existingBeam != null)
        {
            // обновляем только параметры, не создаём новый
            existingBeam.RefreshContext(firePoint, damage, this, weapon.def.fireRate);
            return;
        }

        GameObject obj = Instantiate(
            weapon.def.bulletPrefab,
            firePoint.position,
            Quaternion.identity
        );

        IAttackBehaviour attack = obj.GetComponent<IAttackBehaviour>();
        if (attack == null)
        {
            Debug.LogError(
                $"{weapon.def.bulletPrefab.name} does not implement IAttackBehaviour"
            );
            Destroy(obj);
            return;
        }

        attack.InitAttack(new AttackContext
        {
            firePoint = firePoint,
            target = target.transform,
            damage = damage,
            projectileSpeed = weapon.def.projectileSpeed,

            ownerTower = this,
            weaponFireRate = weapon.def.fireRate,
            owner = transform,

            heightOffset = waveHeightOffset,
            forwardOffset = waveForwardOffset,

            weapon = weapon.def
        });
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
            auraInstance = null
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
                        def.GetLocalizedName(),
                        def.damageType          // ← тип урона берём из SO оружия
                    );
                    newWeapon.auraInstance = auraInstance;
                }
            }
        }

        weapons.Add(newWeapon);
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

    private float GetFinalDamage(WeaponRuntime weapon)
    {
        float finalDamage = damageCalculator.Calculate(new DamageContext
        {
            baseDamage = weapon.def.damagePerProjectile,
            damageType = weapon.def.damageType,
            itemTier = weapon.def.itemTier,
            isSpikes = false
        });

        Debug.Log($"Final damage for {weapon.def.name} is {finalDamage}");

        //float maxHp = 0f;
        //float dmgFromHpPercent = 0f;
        //float hpBonusToMult = 0f;

        //// 1) Множитель по времени (x1, x1.2, x2 ...)
        //float timeMult = 1f;
        //if (UpgradePerTick.Instance != null)
        //    timeMult = UpgradePerTick.Instance.DamageMultiplier;

        //// 2) Бонус от MaxHealth (у тебя это уже НЕ множитель, а добавка к множителю)
        //// hpBonusToMult = (maxHp * percent) / 100
        //// т.е. это уже "плюс к множителю", оставляем как есть
        //if (UpgradesManager.Instance != null &&
        //    UpgradesManager.Instance.context.runtime.damageFromMaxHealthPercent > 0f &&
        //    UpgradesManager.Instance.playerHealth != null)
        //{
        //    maxHp = UpgradesManager.Instance.playerHealth.MaxHealth;
        //    dmgFromHpPercent = UpgradesManager.Instance.context.runtime.damageFromMaxHealthPercent;

        //    hpBonusToMult = (maxHp * dmgFromHpPercent) / 100f;
        //}

        //// 3) Множитель по типу урона (x1, x1.3, x2 ...)
        //float typeMult = 1f;
        //if (UpgradesManager.Instance != null)
        //    typeMult = UpgradesManager.Instance.GetDamageTypeMultiplier(damageType);

        //// 4) Множитель при активном щите (x1, x1.5, x2 ...)
        //float shieldMult = 1f;
        //if (UpgradesManager.Instance != null)
        //    shieldMult = UpgradesManager.Instance.playerShield.GetShieldDamageBonusMultiplier();

        //// ===== НОВАЯ ЛОГИКА: ВСЕ МНОЖИТЕЛИ СКЛАДЫВАЮТСЯ =====
        //// Переводим множители в бонусы:
        //// x2 -> +1, x1.5 -> +0.5, x1 -> +0
        //float timeBonus = timeMult - 1f;
        //float typeBonus = typeMult - 1f;
        //float shieldBonus = shieldMult - 1f;

        //// hpBonusToMult у тебя уже рассчитан как "прибавка к множителю", т.е. бонус.
        //float totalBonus = timeBonus + hpBonusToMult + typeBonus + shieldBonus;

        //// Итоговый множитель всегда >= 0 (на всякий)
        //float finalMult = Mathf.Max(1f, 1f + totalBonus);
        //float finalDamage = baseDamage * finalMult;

        // Логи
        //Debug.Log(
        //    $"<color=#00d9ff>[DamageCalc]</color> {weaponName} ({damageType}) → " +
        //    $"Base={baseDamage} | " +
        //    $"TimeMult={timeMult:F2} (bonus {timeBonus:F2}) | " +
        //    $"MaxHP={maxHp:F0} | HP%={dmgFromHpPercent}% | HpBonus={hpBonusToMult:F2} | " +
        //    $"TypeMult={typeMult:F2} (bonus {typeBonus:F2}) | " +
        //    $"ShieldMult={shieldMult:F2} (bonus {shieldBonus:F2}) | " +
        //    $"<b>TotalBonus={totalBonus:F2}</b> | <b>TotalMult={finalMult:F2}</b> | " +
        //    $"<color=yellow>Final={finalDamage:F2}</color>"
        //);

        return finalDamage;
    }


    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}