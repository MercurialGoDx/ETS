using UnityEngine;

public class FallingBullet : MonoBehaviour, IAttackBehaviour
{
    [Header("Характеристики")]
    public float fallSpeed = 15f;          // скорость падения
    public float startHeight = 8f;         // высота спавна над целью
    public float damage = 5f;
    public bool homing = true;             // наведение по XZ, пока враг жив

    [Header("Время жизни")]
    [Tooltip("Максимальное время жизни снаряда в секундах (защита от зависания).")]
    public float maxLifeTime = 10f;

    [Header("AOE")]
    [Tooltip("Если включено — при ударе наносит урон по области (1 раз), без прямого урона цели.")]
    public bool useAoe = false;

    [Tooltip("Радиус AOE урона")]
    public float aoeRadius = 2f;
    [Header("Хитбокс")]
    public float hitHeightOffset = 0.3f;

    private Transform target;
    private Vector3 fallPoint;             // финальная точка падения по XZ
    private bool initialized = false;
    private float lifeTimer = 0f;

    private WeaponDefinition sourceWeapon;

    private PooledObject pooledObject;

    public void Awake()
    {
        pooledObject = GetComponent<PooledObject>();
    }

    public void InitAttack(AttackContext context)
    {
        damage = context.damage;
        target = context.target;

        if (target == null)
        {
            pooledObject.Release();
            return;
        }

        // Точка падения — позиция врага
        fallPoint = target.position;

        // Старт над врагом
        Vector3 spawnPos = fallPoint;
        spawnPos.y += startHeight;
        transform.position = spawnPos;

        lifeTimer = 0f;
        initialized = true;

        sourceWeapon = context.weapon;
    }

    private void Update()
    {
        if (!initialized)
            return;

        // Проверка времени жизни
        lifeTimer += Time.deltaTime;
        if (lifeTimer >= maxLifeTime)
        {
            pooledObject.Release();
            return;
        }

        // Если враг жив — обновляем XZ
        if (homing && target != null)
        {
            fallPoint.x = target.position.x;
            fallPoint.z = target.position.z;
        }

        // Падение вниз
        Vector3 pos = transform.position;
        pos.x = fallPoint.x; // всегда вертикальный удар
        pos.z = fallPoint.z;
        pos.y -= fallSpeed * Time.deltaTime;
        transform.position = pos;

        float groundY = fallPoint.y + hitHeightOffset;

        // Достигли точки удара
        if (transform.position.y <= groundY)
        {
            HitTarget();
        }
    }

    private void HitTarget()
    {
        // Позиция удара: точка падения + небольшой оффсет по Y
        float groundY = fallPoint.y + hitHeightOffset;
        Vector3 hitPos = new Vector3(fallPoint.x, groundY, fallPoint.z);

        if (useAoe)
        {
            // AOE урон — ОДИН РАЗ по области
            Collider[] hits = Physics.OverlapSphere(hitPos, aoeRadius);
            foreach (var col in hits)
            {
                Enemy enemy = col.GetComponent<Enemy>();
                if (enemy != null)
                {
                    enemy.TakeDamage(damage);
                    DamageStatsManager.Instance?.RegisterDamage(sourceWeapon, damage);
                }
            }
        }
        else
        {
            // Старый режим — прямой урон по цели
            if (target != null)
            {
                Enemy enemy = target.GetComponent<Enemy>();
                if (enemy != null)
                {
                    enemy.TakeDamage(damage);
                    DamageStatsManager.Instance?.RegisterDamage(sourceWeapon, damage);
                }
            }
        }

        // VFX при ударе (как было)
        OnHitVFX vfx = GetComponent<OnHitVFX>();
        if (vfx != null)
        {
            vfx.PlayAtPosition(hitPos);
        }

        pooledObject.Release();
    }

}