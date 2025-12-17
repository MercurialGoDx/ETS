using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("Характеристики")]
    public float speed = 15f;
    public float damage = 5f;
    public bool homing = true;   // летит за целью или по прямой

    [Header("Время жизни")]
    [Tooltip("Максимальное время жизни снаряда в секундах (защита от зависания).")]
    public float maxLifeTime = 10f;

    [Header("Эффект при убийстве")]
    [Tooltip("Если true, при убийстве врага этой пулей игроку увеличится максимальное здоровье.")]
    public bool increasePlayerMaxHealthOnKill = false;

    [Tooltip("На сколько увеличивать максимальное здоровье при каждом убийстве.")]
    public float maxHealthIncreaseAmount = 10f;

    protected Transform target;
    protected Vector3 moveDir;

    protected PlayerHealth cachedPlayerHealth;
    private float lifeTimer = 0f;

    protected virtual void Awake()
    {
        if (increasePlayerMaxHealthOnKill)
        {
            FindPlayerHealth();
        }
    }

    protected void FindPlayerHealth()
    {
        if (cachedPlayerHealth != null)
            return;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            cachedPlayerHealth = playerObj.GetComponent<PlayerHealth>();
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;

        if (target != null)
        {
            moveDir = (target.position - transform.position).normalized;
        }
        else
        {
            // если цели нет – летим туда, куда смотрит локальная ось вверх
            moveDir = transform.up;
        }

        UpdateRotation();
    }

    protected virtual void Update()
    {
        // Проверка времени жизни
        lifeTimer += Time.deltaTime;
        if (lifeTimer >= maxLifeTime)
        {
            Destroy(gameObject);
            return;
        }

        // самонаведение
        if (homing && target != null)
        {
            moveDir = (target.position - transform.position).normalized;
            UpdateRotation();
        }

        float distanceThisFrame = speed * Time.deltaTime;

        // Если почти долетели до цели — считаем, что попали
        if (target != null)
        {
            float distToTarget = Vector3.Distance(transform.position, target.position);
            if (distToTarget <= distanceThisFrame)
            {
                HitTarget();
                return;
            }
        }

        // Двигаем пулю
        transform.position += moveDir * distanceThisFrame;
    }

    protected void UpdateRotation()
    {
        if (moveDir.sqrMagnitude > 0.0001f)
        {
            // Поворачиваем так, чтобы локальная ось Y смотрела по направлению движения
            transform.rotation = Quaternion.FromToRotation(Vector3.up, moveDir);
        }
    }

    protected void HitTarget()
    {
        Enemy enemy = null;

        if (target != null)
        {
            enemy = target.GetComponent<Enemy>();
        }

        if (enemy != null)
        {
            // сохраняем HP до удара
            float hpBefore = enemy.CurrentHealth;

            // наносим урон
            enemy.TakeDamage(damage);

            // хук для спец-пуль (IceBullet, ядовитые и т.п.)
            OnEnemyHit(enemy);

            // бонус к здоровью при убийстве
            if (increasePlayerMaxHealthOnKill && hpBefore > 0f && enemy.CurrentHealth <= 0f)
            {
                if (cachedPlayerHealth == null)
                {
                    FindPlayerHealth();
                }

                if (cachedPlayerHealth != null)
                {
                    cachedPlayerHealth.IncreaseMaxHealth(maxHealthIncreaseAmount, alsoHeal: true);
                }
            }

            // VFX при попадании, если есть
            OnHitVFX vfx = GetComponent<OnHitVFX>();
            if (vfx != null)
            {
                vfx.Play(enemy.transform);
            }
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// Переопределяем в наследниках (IceBullet и т.п.), чтобы добавить эффект при попадании.
    /// Базовая реализация — ничего не делает.
    /// </summary>
    /// <param name="enemy">Враг, по которому попали.</param>
    protected virtual void OnEnemyHit(Enemy enemy)
    {
        // по умолчанию — ничего
    }
}
