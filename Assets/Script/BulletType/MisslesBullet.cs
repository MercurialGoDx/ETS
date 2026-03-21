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
        Vector3 dirToTarget;

        if (target != null)
        {
            dirToTarget = GetTargetCenter(target) - transform.position;
        }
        else
        {
            dirToTarget = transform.forward;
        }

        if (dirToTarget.sqrMagnitude > 0.0001f)
        {
            Vector3 desiredDir = dirToTarget.normalized;
            Quaternion targetRot = Quaternion.LookRotation(desiredDir, Vector3.up);

            // плавный поворот носа
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRot,
                turnSpeed * dt
            );
    }

        // Движение вперёд c ускорением 1.5×
        float homingSpeed = speed * homingSpeedMultiplier;
        transform.position += transform.forward * homingSpeed * dt;
}
    }

    private void OnTriggerEnter(Collider other)
    {
        Enemy enemy = other.GetComponent<Enemy>();
        if (enemy == null)
            return;

        // наносим урон
        enemy.TakeDamage(damage);
        DamageStatsManager.Instance?.RegisterDamage(sourceWeapon, damage);

        // VFX при попадании (если есть)
        OnHitVFX vfx = GetComponent<OnHitVFX>();
        if (vfx != null)
        {
            vfx.Play(enemy.transform);
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
