using System.Collections.Generic;
using UnityEngine;

public class ArcBullet : MonoBehaviour, IAttackBehaviour
{
    [Header("Базовые параметры дуги")]
    public float baseArcHeight = 3f;          // минимальная высота дуги
    public float arcHeightMultiplier = 0.1f;  // на сколько увеличивать дугу за единицу дистанции

    [Header("Характеристики урона")]
    public float damage = 5f;

    [Header("Наведение")]
    public bool homing = true;

    [Header("Фиксированное время полёта")]
    public bool useFixedFlightTime = true;
    public float fixedFlightTime = 5f;

    [Header("Время жизни")]
    [Tooltip("Максимальное время жизни снаряда в секундах (защита от зависания).")]
    public float maxLifeTime = 10f;

    [Header("AOE при взрыве")]
    [Tooltip("Если включено — при достижении точки полёта наносится урон по площади, а прямого урона цели нет.")]
    public bool useAoe = false;

    [Tooltip("Радиус взрыва по площади, если AOE включён")]
    public float aoeRadius = 2f;

    private Transform target;
    private Vector3 startPos;
    private Vector3 targetPos;
    private float travelTime;
    private float currentArcHeight;
    private float t = 0f;
    private float lifeTimer = 0f;

    private WeaponDefinition sourceWeapon;

    private PooledObject pooledObject;
    private readonly HashSet<Enemy> damagedEnemies = new();

    public void Awake()
    {
        pooledObject = GetComponent<PooledObject>();
    }

    public void InitAttack(AttackContext context)
    {
        damage = context.damage;

        target = context.target;
        startPos = transform.position;

        if (target != null)
            targetPos = GetTargetCenter(target);
        else
            targetPos = startPos + transform.forward * 5f;

        // Плоская дистанция
        Vector3 startFlat = startPos; startFlat.y = 0f;
        Vector3 targetFlat = targetPos; targetFlat.y = 0f;

        float distance = Vector3.Distance(startFlat, targetFlat);

        // Время полёта
        if (useFixedFlightTime)
            travelTime = Mathf.Max(0.01f, fixedFlightTime);
        else
            travelTime = Mathf.Max(0.1f, distance / context.projectileSpeed);

        // Высота дуги
        currentArcHeight = baseArcHeight + distance * arcHeightMultiplier;

        t = 0f;
        lifeTimer = 0f;

        sourceWeapon = context.weapon;
    }

    private void Update()
    {
        if (travelTime <= 0f)
            return;

        // Проверка времени жизни
        lifeTimer += Time.deltaTime;
        if (lifeTimer >= maxLifeTime)
        {
            pooledObject.Release();
            return;
        }

        // подруливание (обновляет позицию цели)
        if (homing && target != null && target.gameObject.activeInHierarchy)
        {
            targetPos = GetTargetCenter(target);
        }

        t += Time.deltaTime / travelTime;

        if (t >= 1f)
        {
            HitTarget();
            return;
        }

        // Линейная интерполяция
        Vector3 linearPos = Vector3.Lerp(startPos, targetPos, t);

        // Парабола 0 → 1 → 0
        float parabola = 4f * t * (1f - t);
        Vector3 heightOffset = Vector3.up * (parabola * currentArcHeight);

        Vector3 nextPos = linearPos + heightOffset;

        // Поворот в сторону движения
        Vector3 dir = nextPos - transform.position;
        if (dir.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir);

        transform.position = nextPos;
    }

    private void HitTarget()
    {
        if (useAoe)
        {
            // Только АОЕ-урон
            DoAoEDamage();
        }
        else
        {
            // Только прямое попадание по цели
            if (target != null)
            {
                Enemy e = target.GetComponent<Enemy>();
                if (e != null)
                {
                    e.TakeWeaponDamage(damage, sourceWeapon);
                    DamageStatsManager.Instance?.RegisterDamage(sourceWeapon, damage);
                }
            }
        }

        pooledObject.Release();
    }

    private void DoAoEDamage()
    {
        Vector3 explosionPos = transform.position;

        Collider[] hits = Physics.OverlapSphere(explosionPos, aoeRadius);
        damagedEnemies.Clear();
        foreach (var col in hits)
        {
            Enemy enemy = col.GetComponent<Enemy>();
            if (enemy == null || !damagedEnemies.Add(enemy))
                continue;

            enemy.TakeWeaponDamage(damage, sourceWeapon);
            DamageStatsManager.Instance?.RegisterDamage(sourceWeapon, damage);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!useAoe)
            return;

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, aoeRadius);
    }

    /// <summary>
    /// Возвращает центр цели для наведения.
    /// </summary>
    private Vector3 GetTargetCenter(Transform targetTransform)
    {
        Enemy enemy = targetTransform.GetComponent<Enemy>();
        if (enemy != null)
        {
            return enemy.GetCenterPosition();
        }
        return targetTransform.position;
    }
}
