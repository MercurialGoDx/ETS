using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class MissileBullet : MonoBehaviour
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

    public void Init(Transform newTarget)
    {
        target = newTarget;

        // базовое направление к цели по XZ
        Vector3 dirToTarget = target != null
            ? (target.position - transform.position)
            : transform.forward;

        Vector3 dirXZ = dirToTarget;
        dirXZ.y = 0f;
        if (dirXZ.sqrMagnitude < 0.0001f)
            dirXZ = transform.forward;

        dirXZ.Normalize();

        // добавляем сильный вертикальный компонент
        Vector3 initialDir = dirXZ + Vector3.up * verticalBias;
        launchDir = initialDir.normalized;

        // смотрим носом по направлению старта
        transform.rotation = Quaternion.LookRotation(launchDir, Vector3.up);

        // на всякий случай обнулим счетчики
        launchTraveled = 0f;
        inLaunchPhase = true;
        lifeTimer = 0f;
    }

    private void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;

        var rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        lifeTimer += dt;
        if (lifeTimer >= maxLifeTime)
        {
            Destroy(gameObject);
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
        dirToTarget = target.position - transform.position;
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

        // VFX при попадании (если есть)
        OnHitVFX vfx = GetComponent<OnHitVFX>();
        if (vfx != null)
        {
            vfx.Play(enemy.transform);
        }

        Destroy(gameObject);
    }
}
