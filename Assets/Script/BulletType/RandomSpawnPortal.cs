using System.Collections.Generic;
using UnityEngine;

public class RandomSpawnPortal : MonoBehaviour, IAttackBehaviour
{
    [Header("Позиция")]
    public float spawnRadius = 20f;

    [Header("Параметры урона")]
    public float damage = 5f;
    public float interval = 1f;        // базовый интервал между тиками
    public float damageRadius = 3f;
    public float lifeTime = 5f;

    [Header("VFX")]
    public GameObject impactVfx;

    private float timer = 0f;
    private float aliveTimer = 0f;

    private Transform tower;

    private WeaponDefinition sourceWeapon;

    private PooledObject pooledObject;
    private readonly HashSet<Enemy> damagedEnemies = new();

    public void Awake()
    {
        pooledObject = GetComponent<PooledObject>();
    }

    public void InitAttack(AttackContext context)
    {
        // Урон из контекста
        damage = context.damage;

        // Сброс таймеров для Object Pooling
        timer = 0f;
        aliveTimer = 0f;

        // Определяем точку спавна
        Transform origin = context.firePoint != null ? context.firePoint : context.owner;
        if (origin == null)
        {
            pooledObject.Release();
            return;
        }

        // Рандомная позиция вокруг origin. Высоту берём от цели (плоскость врагов),
        // а не от firePoint — иначе зона урона висит над башней и никого не задевает.
        float groundY = context.target != null ? context.target.position.y : origin.position.y;
        Vector2 circle = Random.insideUnitCircle * spawnRadius;
        Vector3 pos = new Vector3(
            origin.position.x + circle.x,
            groundY,
            origin.position.z + circle.y
        );
        transform.position = pos;

        sourceWeapon = context.weapon;
    }

    private void Update()
    {
        timer += Time.deltaTime;
        aliveTimer += Time.deltaTime;

        if (timer >= interval)
        {
            timer = 0f;
            DoDamage();
        }

        if (aliveTimer >= lifeTime)
            pooledObject.Release();
    }

    private void DoDamage()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, damageRadius);
        damagedEnemies.Clear();

        foreach (Collider col in hits)
        {
            Enemy enemy = col.GetComponent<Enemy>();
            if (enemy != null && damagedEnemies.Add(enemy))
            {
                enemy.TakeWeaponDamage(damage, sourceWeapon);
                DamageStatsManager.Instance?.RegisterDamage(sourceWeapon, damage);
            }
        }

        if (impactVfx != null)
            Instantiate(impactVfx, transform.position, Quaternion.identity);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, damageRadius);
    }
}
