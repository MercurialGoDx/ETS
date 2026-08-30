using System.Collections.Generic;
using UnityEngine;

public class ChainBullet : MonoBehaviour, IAttackBehaviour
{
    [Header("Основные параметры")]
    public float damage = 5f;
    public float speed = 10f;

    [Header("Время жизни")]
    [Tooltip("Максимальное время жизни снаряда в секундах (защита от зависания).")]
    public float maxLifeTime = 10f;

    [Header("Цепная логика")]
    public int maxBounces = 3;      // сколько раз пуля может ударить (кол-во целей)
    public float searchRadius = 8f; // радиус поиска следующей цели от текущей позиции

    private Transform currentTarget;
    private int remainingBounces;
    private HashSet<Enemy> hitEnemies = new HashSet<Enemy>();
    private float lifeTimer = 0f;

    // Переиспользуемый буфер для поиска целей — без аллокаций каждый вызов.
    private readonly List<Enemy> enemiesBuffer = new List<Enemy>();

    private WeaponDefinition sourceWeapon;

    private PooledObject pooledObject;

    public void Awake()
    {
        pooledObject = GetComponent<PooledObject>();
    }

    public void InitAttack(AttackContext context)
    {
        damage = context.damage;
        speed = context.projectileSpeed;

        remainingBounces = maxBounces;
        hitEnemies.Clear();
        lifeTimer = 0f;

        if (context.target == null)
        {
            pooledObject.Release();
            return;
        }

        SetTarget(context.target);

        sourceWeapon = context.weapon;
    }

    public void SetTarget(Transform target)
    {
        currentTarget = target;
    }

    private void Update()
    {
        // Проверка времени жизни
        lifeTimer += Time.deltaTime;
        if (lifeTimer >= maxLifeTime)
        {
            pooledObject.Release();
            return;
        }

        if (currentTarget == null)
        {
            TryFindNextTarget();
            if (currentTarget == null)
            {
                pooledObject.Release();
                return;
            }
        }

        Vector3 dir = GetTargetCenter(currentTarget) - transform.position;
        float distThisFrame = speed * Time.deltaTime;

        if (dir.sqrMagnitude <= distThisFrame * distThisFrame)
        {
            // считаем, что долетели до цели
            HitCurrentTarget();
        }
        else
        {
            Vector3 step = dir.normalized * distThisFrame;
            transform.position += step;
            transform.forward = dir.normalized;
        }
    }

    private void HitCurrentTarget()
    {
        if (currentTarget == null)
            return;

        Enemy enemy = currentTarget.GetComponent<Enemy>();
        if (enemy != null && enemy.gameObject.activeInHierarchy)
        {
            // урон по этому врагу, если ещё не били его этой пулей
            if (!hitEnemies.Contains(enemy))
            {
                hitEnemies.Add(enemy);
                enemy.TakeWeaponDamage(damage, sourceWeapon);
                DamageStatsManager.Instance?.RegisterDamage(sourceWeapon, damage);
            }
        }

        remainingBounces--;

        // если выстрелы закончились — умираем
        if (remainingBounces <= 0)
        {
            pooledObject.Release();
            return;
        }

        // ищем следующую цель
        currentTarget = null;
        TryFindNextTarget();

        if (currentTarget == null)
        {
            pooledObject.Release();
        }
    }

    /// <summary>
    /// Ищем ближайшего врага в радиусе, которого ещё не били.
    /// </summary>
    private void TryFindNextTarget()
    {
        EnemyManager.Instance.GetEnemiesInRange(transform.position, searchRadius, enemiesBuffer);

        Enemy best = null;
        float bestSqrDist = Mathf.Infinity;
        Vector3 fromPos = transform.position;
        float maxSqr = searchRadius * searchRadius;

        foreach (Enemy e in enemiesBuffer)
        {
            if (e == null) continue;
            if (!e.gameObject.activeInHierarchy) continue;
            if (hitEnemies.Contains(e)) continue;
            if (e.CurrentHealth <= 0f) continue;

            float sqr = (e.transform.position - fromPos).sqrMagnitude;
            if (sqr <= maxSqr && sqr < bestSqrDist)
            {
                bestSqrDist = sqr;
                best = e;
            }
        }

        currentTarget = best != null ? best.transform : null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, searchRadius);
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
