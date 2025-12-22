using System.Collections.Generic;
using System.Collections;
using UnityEngine;

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

    private class WeaponRuntime
    {
        public WeaponDefinition def;
        public int stacks = 1;      // сколько раз купили это оружие
        public float cooldown = 0f; // свой независимый кулдаун
        public List<Enemy> lastTargets = new List<Enemy>(); // закреплённые цели по “стволам”
        public AuraDamageZone auraInstance;
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

        List<Enemy> enemiesInRange = GetEnemiesInRange();
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

        List<Enemy> usedThisVolley = new List<Enemy>();

        for (int i = 0; i < stacks; i++)
        {
            Enemy target = null;

            // --- 1) Пытаемся взять старую цель, только если режим "LockUntilDeath"
            if (!randomEachShot)
            {
                target = weapon.lastTargets[i];

                bool targetValid = false;
                if (target != null && target.gameObject.activeInHierarchy)
                {
                    float dist = Vector3.Distance(transform.position, target.transform.position);
                    if (dist <= range && enemiesInRange.Contains(target))
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

    void SpawnBullet(WeaponRuntime weapon, Enemy target)
    {
        if (target == null) return;

        // (BaseDamage + % от здоровья) * глобальный множитель
        float scaledDamage = GetFinalDamage(
            weapon.def.damagePerProjectile,
            weapon.def.weaponName,
            weapon.def.damageType
        );

        // ===== СПЕЦЛОГИКА ДЛЯ ЛАЗЕРА =====
        // если префаб — лазер, и для этой цели уже есть активный луч — не создаём новый
        LaserBeam laserPrefabComponent = weapon.def.bulletPrefab.GetComponent<LaserBeam>();
        if (laserPrefabComponent != null)
        {
            LaserBeam existingBeam = LaserBeam.GetActiveBeamFor(target);
            if (existingBeam != null)
            {
                // Луч уже висит на этом враге — просто выходим
                return;
            }
        }

        GameObject obj = Instantiate(
            weapon.def.bulletPrefab,
            firePoint.position,
            Quaternion.identity
        );

        // ==== 1) ЛАЗЕР ====
        LaserBeam laser = obj.GetComponent<LaserBeam>();
        if (laser != null)
        {
            laser.Init(
                firePoint,
                target,
                scaledDamage,                  // ⬅ урон с учётом всех бонусов
                this,                          // TowerAttack (для fireRateMultiplier)
                weapon.def.fireRate            // базовая fireRate оружия
            );
            return;
        }

        // ==== 2) НАВЕСНОЙ СНАРЯД (МОРТИРА) ====
        ArcBullet arc = obj.GetComponent<ArcBullet>();
        if (arc != null)
        {
            arc.damage = scaledDamage;
            arc.SetTarget(target.transform);
            return;
        }

        // 3) Падающий снаряд сверху
        FallingBullet falling = obj.GetComponent<FallingBullet>();
        if (falling != null)
        {
            falling.damage = scaledDamage;
            falling.fallSpeed = weapon.def.projectileSpeed;
            falling.SetTarget(target.transform);
            return;
        }

        // 4) Волна (WaveBullet)
        WaveBullet wave = obj.GetComponent<WaveBullet>();
        if (wave != null)
        {
            wave.damage = scaledDamage;
            wave.speed = weapon.def.projectileSpeed;
            wave.Init(transform, target.transform, waveForwardOffset, waveHeightOffset);
            return;
        }

        // 5) Прыгающая пуля
        ChainBullet chain = obj.GetComponent<ChainBullet>();
        if (chain != null)
        {
            chain.damage = scaledDamage;
            chain.speed = weapon.def.projectileSpeed;
            chain.Init(target.transform);   // maxBounces берём из префаба
            return;
        }

        // 6) Обычная пуля
        Bullet straight = obj.GetComponent<Bullet>();
        if (straight != null)
        {
            straight.damage = scaledDamage;
            straight.speed = weapon.def.projectileSpeed;
            straight.SetTarget(target.transform);
            return;
        }

        // 7) Ракета
        MissileBullet missile = obj.GetComponent<MissileBullet>();
        if (missile != null)
        {
            missile.damage = scaledDamage;
            missile.speed = weapon.def.projectileSpeed;
            missile.Init(target.transform);
            return;
        }

        // 8) Расширяющая волна
        SpawnBulletOffset pulse = obj.GetComponent<SpawnBulletOffset>();
        if (pulse != null)
        {
            // Позиция спавна (как для волны)
            Vector3 spawnPos = waveFirePoint != null ? waveFirePoint.position : firePoint.position;
            pulse.Init(spawnPos);

            // Пробрасываем урон во вложенный ScalingWave
            ScalingWave scaler = obj.GetComponentInChildren<ScalingWave>();
            if (scaler != null)
            {
                scaler.applyDamage = true; // бьём врагов
                scaler.damage = scaledDamage;
            }

            return;
        }

        // 9) Катапульта от HP
        Catapult hpArc = obj.GetComponent<Catapult>();
        if (hpArc != null)
        {
            // Урон он сам считает от здоровья башни, здесь ничего передавать не нужно
            hpArc.SetTarget(target.transform);
            return;
        }

        // 10) Портал по цели
        PortalBullet portal = obj.GetComponent<PortalBullet>();
        if (portal != null)
        {
            portal.damage = scaledDamage;
            portal.Init(target.transform);
            return;
        }

        // 11) Портал в случайной точке
        RandomSpawnPortal randomPortal = obj.GetComponent<RandomSpawnPortal>();
        if (randomPortal != null)
        {
            randomPortal.damage = scaledDamage;
            return;
        }
        // 12)
        LightningChainBullet lightning = obj.GetComponent<LightningChainBullet>();
        if (lightning != null)
        {
            lightning.Init(firePoint, target, scaledDamage);
            return;
        }

        // Если компонент не опознан — удаляем
        Destroy(obj);
    }


    List<Enemy> GetEnemiesInRange()
    {
        Enemy[] all = FindObjectsOfType<Enemy>();
        List<Enemy> result = new List<Enemy>();

        foreach (var e in all)
        {
            if (e == null) continue;
            float dist = Vector3.Distance(transform.position, e.transform.position);
            if (dist <= range)
            {
                result.Add(e);
            }
        }

        return result;
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
                        def.weaponName,
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

    private float GetFinalDamage(float baseDamage, string weaponName, WeaponDamageType damageType)
    {
        float maxHp = 0f;
        float dmgFromHpPercent = 0f;
        float hpBonusToMult = 0f;

        // 1) Множитель по времени (x1, x1.2, x2 ...)
        float timeMult = 1f;
        if (UpgradePerTick.Instance != null)
            timeMult = UpgradePerTick.Instance.DamageMultiplier;

        // 2) Бонус от MaxHealth (у тебя это уже НЕ множитель, а добавка к множителю)
        // hpBonusToMult = (maxHp * percent) / 100
        // т.е. это уже "плюс к множителю", оставляем как есть
        if (UpgradesManager.Instance != null &&
            UpgradesManager.Instance.damageFromMaxHealthPercent > 0f &&
            UpgradesManager.Instance.playerHealth != null)
        {
            maxHp = UpgradesManager.Instance.playerHealth.MaxHealth;
            dmgFromHpPercent = UpgradesManager.Instance.damageFromMaxHealthPercent;

            hpBonusToMult = (maxHp * dmgFromHpPercent) / 100f;
        }

        // 3) Множитель по типу урона (x1, x1.3, x2 ...)
        float typeMult = 1f;
        if (UpgradesManager.Instance != null)
            typeMult = UpgradesManager.Instance.GetDamageTypeMultiplier(damageType);

        // 4) Множитель при активном щите (x1, x1.5, x2 ...)
        float shieldMult = 1f;
        if (UpgradesManager.Instance != null)
            shieldMult = UpgradesManager.Instance.GetShieldDamageBonusMultiplier();

        // ===== НОВАЯ ЛОГИКА: ВСЕ МНОЖИТЕЛИ СКЛАДЫВАЮТСЯ =====
        // Переводим множители в бонусы:
        // x2 -> +1, x1.5 -> +0.5, x1 -> +0
        float timeBonus = timeMult - 1f;
        float typeBonus = typeMult - 1f;
        float shieldBonus = shieldMult - 1f;

        // hpBonusToMult у тебя уже рассчитан как "прибавка к множителю", т.е. бонус.
        float totalBonus = timeBonus + hpBonusToMult + typeBonus + shieldBonus;

        // Итоговый множитель всегда >= 0 (на всякий)
        float finalMult = Mathf.Max(1f, 1f + totalBonus);
        float finalDamage = baseDamage * finalMult;

        // Логи
        Debug.Log(
            $"<color=#00d9ff>[DamageCalc]</color> {weaponName} ({damageType}) → " +
            $"Base={baseDamage} | " +
            $"TimeMult={timeMult:F2} (bonus {timeBonus:F2}) | " +
            $"MaxHP={maxHp:F0} | HP%={dmgFromHpPercent}% | HpBonus={hpBonusToMult:F2} | " +
            $"TypeMult={typeMult:F2} (bonus {typeBonus:F2}) | " +
            $"ShieldMult={shieldMult:F2} (bonus {shieldBonus:F2}) | " +
            $"<b>TotalBonus={totalBonus:F2}</b> | <b>TotalMult={finalMult:F2}</b> | " +
            $"<color=yellow>Final={finalDamage:F2}</color>"
        );

        return finalDamage;
    }


    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, range);
    }
}