using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class MissileBullet : MonoBehaviour, IAttackBehaviour 
{
    [Header("Полет")]
    public float speed = 12f;             // скорость ракеты
    public float launchDistance = 5f;     // сколько пролететь в стартовой фазе
    public float verticalBias = 4f;       // насколько сильно тянуть вверх при старте
    public float turnSpeed = 360f;        // скорость поворота "носа" (градусов в секунду)
    public float maxLifeTime = 8f;        // защита от вечной ракеты
    private float homingSpeedMultiplier = 1.5f;

    [Header("Детонация у цели")]
    [Tooltip("Радиус прямого попадания: если ракета ближе — детонирует.")]
    public float hitRadius = 0.7f;
    [Tooltip("Радиус дистанционного взрывателя: если ракета уже ближе этого и начала удаляться " +
             "от цели (точка наибольшего сближения) — детонирует, чтобы не кружить вечно.")]
    public float proximityFuse = 2.5f;

    private float lastDistanceToTarget;


    [Header("Урон")]
    public float damage = 10f;

    private Transform target;
    private Vector3 launchDir;
    private float launchTraveled = 0f;
    private bool inLaunchPhase = true;
    private float lifeTimer = 0f;

    private WeaponDefinition sourceWeapon;

    private PooledObject pooledObject;

    public void InitAttack(AttackContext context)
    {
        target = context.target;
        damage = context.damage;
        speed = context.projectileSpeed;

        // направление к цели по XZ
        Vector3 dirToTarget = target != null ? GetTargetCenter(target) - context.firePoint.position : context.firePoint.forward;
        Vector3 dirXZ = dirToTarget;
        dirXZ.y = 0f;
        if (dirXZ.sqrMagnitude < 0.0001f) dirXZ = context.firePoint.forward;

        dirXZ.Normalize();

        // стартовое направление с вертикальным смещением
        launchDir = (dirXZ + Vector3.up * verticalBias).normalized;

        transform.position = context.firePoint.position;
        transform.rotation = Quaternion.LookRotation(launchDir, Vector3.up);

        launchTraveled = 0f;
        inLaunchPhase = true;
        lifeTimer = 0f;
        lastDistanceToTarget = float.MaxValue;

        sourceWeapon = context.weapon;
    }

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;

        var rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        pooledObject = GetComponent<PooledObject>();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        lifeTimer += dt;
        if (lifeTimer >= maxLifeTime)
        {
            pooledObject.Release();
            return;
        }

        if (inLaunchPhase)
        {
            // стартовая фаза — летим по launchDir
            Vector3 move = launchDir * speed * dt;
            transform.position += move;
            launchTraveled += move.magnitude;

            if (launchTraveled >= launchDistance)
            {
                inLaunchPhase = false;
            }
        }
        else
        {
            // --- фаза наведения ---
            float homingSpeed = speed * homingSpeedMultiplier;

            if (target != null)
            {
                Vector3 targetCenter = GetTargetCenter(target);
                float dist = Vector3.Distance(transform.position, targetCenter);

                // Детонация: прямое попадание ИЛИ точка наибольшего сближения (ракета
                // промахнулась и начала удаляться, но всё ещё близко) — чтобы не кружить вечно.
                bool receding = dist > lastDistanceToTarget;
                if (dist <= hitRadius || (receding && dist <= proximityFuse))
                {
                    HitEnemy(target.GetComponent<Enemy>());
                    return;
                }
                lastDistanceToTarget = dist;

                Vector3 desiredDir = (targetCenter - transform.position).normalized;
                if (desiredDir.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(desiredDir, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, turnSpeed * dt);
                }
            }
            // если цели нет (враг погиб) — летим прямо, пока не истечёт maxLifeTime

            // Движение вперёд c ускорением 1.5×
            transform.position += transform.forward * homingSpeed * dt;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Enemy enemy = other.GetComponent<Enemy>();
        if (enemy == null)
            return;

        HitEnemy(enemy);
    }

    /// <summary>
    /// Наносит урон врагу, проигрывает VFX и возвращает ракету в пул.
    /// Вызывается и из триггера, и из детонации по близости.
    /// </summary>
    private void HitEnemy(Enemy enemy)
    {
        if (enemy != null)
        {
            enemy.TakeWeaponDamage(damage, sourceWeapon);
            DamageStatsManager.Instance?.RegisterDamage(sourceWeapon, damage);

            // VFX при попадании (если есть)
            OnHitVFX vfx = GetComponent<OnHitVFX>();
            if (vfx != null)
            {
                vfx.Play(enemy.transform);
            }
        }

        pooledObject.Release();
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
