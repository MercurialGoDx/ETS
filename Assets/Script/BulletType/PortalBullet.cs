using System.Collections.Generic;
using UnityEngine;

public class PortalBullet : MonoBehaviour, IAttackBehaviour
{
    [Header("Позиционирование")]
    [Tooltip("На каком расстоянии от врага появится портал (по XZ). 0 = прямо под ним.")]
    public float offsetDistance = 1.5f;

    [Tooltip("Смещение портала по высоте относительно позиции врага.")]
    public float heightOffset = 0f;

    [Header("Тайминги")]
    [Tooltip("Через сколько секунд после появления портал нанесёт урон.")]
    public float delayBeforeStrike = 1.5f;

    [Tooltip("Через сколько секунд портал исчезнет (независимо от удара).")]
    public float portalLifeTime = 3f;

    [Header("Урон")]
    [Tooltip("Сколько урона нанести всем врагам в радиусе.")]
    public float damage = 10f;

    [Tooltip("Радиус AOE урона.")]
    public float radius = 3f;

    private Transform target;
    private Vector3 strikePosition;

    private float strikeTimer;   // до удара
    private float lifeTimer;     // до исчезновения
    private bool initialized = false;
    private bool hasStruck = false;

    private WeaponDefinition sourceWeapon;

    private PooledObject pooledObject;
    private readonly HashSet<Enemy> damagedEnemies = new();

    public void Awake()
    {
        pooledObject = GetComponent<PooledObject>();
    }

    /// <summary>
    /// Вызывается из TowerAttack сразу после Instantiate.
    /// </summary>
    public void InitAttack(AttackContext context)
    {
        Transform target = context.target;

        // базовая точка — центр врага, если есть
        Vector3 basePos = (target != null) ? GetTargetCenter(target) : context.firePoint.position;

        // рассчитываем смещение по кругу вокруг врага в плоскости XZ
        Vector3 spawnPos = basePos;

        if (offsetDistance > 0.01f)
        {
            float angle = Random.Range(0f, 360f);
            float rad = angle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * offsetDistance;
            spawnPos += offset;
        }

        // высота
        spawnPos.y = basePos.y + heightOffset;

        transform.position = spawnPos;
        strikePosition = spawnPos;

        // получаем урон из AttackContext
        damage = context.damage;

        strikeTimer = delayBeforeStrike;
        lifeTimer = portalLifeTime;
        hasStruck = false;
        initialized = true;

        sourceWeapon = context.weapon;
    }


    private void Update()
    {
        if (!initialized)
            return;

        float dt = Time.deltaTime;

        // таймер до удара
        if (!hasStruck)
        {
            strikeTimer -= dt;
            if (strikeTimer <= 0f)
            {
                Strike();
            }
        }

        // таймер жизни портала
        lifeTimer -= dt;
        if (lifeTimer <= 0f)
        {
            pooledObject.Release();
        }
    }

    private void Strike()
    {
        if (hasStruck) return;   // защита от повторного вызова
        hasStruck = true;

        // Находим всех врагов в радиусе
        Collider[] hits = Physics.OverlapSphere(strikePosition, radius);
        damagedEnemies.Clear();
        foreach (var col in hits)
        {
            Enemy enemy = col.GetComponent<Enemy>();
            if (enemy != null && damagedEnemies.Add(enemy))
            {
                enemy.TakeWeaponDamage(damage, sourceWeapon);
                DamageStatsManager.Instance?.RegisterDamage(sourceWeapon, damage);
            }
        }

        // VFX при срабатывании, если есть
        OnHitVFX vfx = GetComponent<OnHitVFX>();
        if (vfx != null)
        {
            // можно играть в позиции портала
            vfx.Play(transform);
        }
    }

    // Гизмо, чтобы в редакторе видеть радиус урона
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;

        Vector3 pos = Application.isPlaying ? strikePosition : transform.position;
        Gizmos.DrawWireSphere(pos, radius);
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
