using UnityEngine;

public class Catapult : MonoBehaviour, IAttackBehaviour 
{
    [Header("Базовые параметры дуги")]
    public float baseArcHeight = 3f;          // минимальная высота дуги
    public float arcHeightMultiplier = 0.1f;  // на сколько увеличивать дугу за единицу дистанции

    [Header("Наведение")]
    public bool homing = true;

    [Header("Фиксированное время полёта")]
    public bool useFixedFlightTime = true;
    public float fixedFlightTime = 5f;

    [Header("Время жизни")]
    [Tooltip("Максимальное время жизни снаряда в секундах (защита от зависания).")]
    public float maxLifeTime = 10f;

    [Header("AOE от здоровья башни")]
    [Tooltip("Радиус взрыва вокруг точки приземления")]
    public float aoeRadius = 2f;

    [Tooltip("Процент от максимального здоровья башни, который наносится как урон (0.15 = 15%)")]
    public float hpPercentAsDamage = 0.15f;

    private Transform target;
    private Vector3 startPos;
    private Vector3 targetPos;
    private float travelTime;
    private float currentArcHeight;
    private float t = 0f;
    private float lifeTimer = 0f;

    private PlayerHealth playerHealth;

    private WeaponDefinition sourceWeapon;

    public void InitAttack(AttackContext context)
    {
        // Безопасно сбрасываем состояние для Object Pooling
        t = 0f;
        lifeTimer = 0f;

        target = context.target;
        startPos = context.firePoint != null
            ? context.firePoint.position
            : context.owner.position;

        if (target != null)
            targetPos = GetTargetCenter(target);
        else
            targetPos = startPos + context.owner.forward * 5f;

        // плоская дистанция по XZ
        Vector3 startFlat = startPos; startFlat.y = 0f;
        Vector3 targetFlat = targetPos; targetFlat.y = 0f;

        float distance = Vector3.Distance(startFlat, targetFlat);

        // время полёта
        if (useFixedFlightTime)
            travelTime = Mathf.Max(0.01f, fixedFlightTime);
        else
            travelTime = Mathf.Max(0.1f, distance / 10f);

        currentArcHeight = baseArcHeight + distance * arcHeightMultiplier;

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
            Destroy(gameObject);
            return;
        }

        // подруливание (обновляем позицию цели)
        if (homing && target != null)
            targetPos = GetTargetCenter(target);

        t += Time.deltaTime / travelTime;

        if (t >= 1f)
        {
            Explode();
            return;
        }

        // линейная интерполяция
        Vector3 linearPos = Vector3.Lerp(startPos, targetPos, t);

        // парабола 0 → 1 → 0
        float parabola = 4f * t * (1f - t);
        Vector3 heightOffset = Vector3.up * (parabola * currentArcHeight);

        Vector3 nextPos = linearPos + heightOffset;

        // поворот по ходу движения
        Vector3 dir = nextPos - transform.position;
        if (dir.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(dir);
        }

        transform.position = nextPos;
    }

    private void Explode()
    {
        if (playerHealth == null)
            FindPlayerHealth();

        // если не нашли PlayerHealth — просто безопасно уничтожаемся
        if (playerHealth != null)
        {
            float maxHP = playerHealth.MaxHealth;
            float aoeDamage = maxHP * hpPercentAsDamage;

            if (aoeDamage > 0f)
            {
                Vector3 center = transform.position;
                Collider[] hits = Physics.OverlapSphere(center, aoeRadius);

                foreach (var col in hits)
                {
                    Enemy enemy = col.GetComponent<Enemy>();
                    if (enemy == null)
                        continue;

                    enemy.TakeDamage(aoeDamage);
                    DamageStatsManager.Instance?.RegisterDamage(sourceWeapon, aoeDamage);
                }
            }
        }

        Destroy(gameObject);
    }

    private void FindPlayerHealth()
    {
        if (playerHealth != null)
            return;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            playerHealth = playerObj.GetComponent<PlayerHealth>();
    }

    private void OnDrawGizmosSelected()
    {
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
