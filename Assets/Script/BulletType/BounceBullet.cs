using System.Collections.Generic;
using UnityEngine;

public class ChainBullet : MonoBehaviour
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

    /// <summary>
    /// Вызываем сразу после спавна с первой целью.
    /// </summary>
    public void Init(Transform firstTarget)
    {
        remainingBounces = maxBounces;
        SetTarget(firstTarget);
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
            Destroy(gameObject);
            return;
        }

        if (currentTarget == null)
        {
            TryFindNextTarget();
            if (currentTarget == null)
            {
                Destroy(gameObject);
                return;
            }
        }

        Vector3 dir = currentTarget.position - transform.position;
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
                enemy.TakeDamage(damage);
            }
        }

        remainingBounces--;

        // если выстрелы закончились — умираем
        if (remainingBounces <= 0)
        {
            Destroy(gameObject);
            return;
        }

        // ищем следующую цель
        currentTarget = null;
        TryFindNextTarget();

        if (currentTarget == null)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Ищем ближайшего врага в радиусе, которого ещё не били.
    /// </summary>
    private void TryFindNextTarget()
    {
        Enemy[] allEnemies = FindObjectsOfType<Enemy>();

        Enemy best = null;
        float bestSqrDist = Mathf.Infinity;
        Vector3 fromPos = transform.position;
        float maxSqr = searchRadius * searchRadius;

        foreach (Enemy e in allEnemies)
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
}
