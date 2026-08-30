using UnityEngine;

public class Bullet : MonoBehaviour, IAttackBehaviour
{
    [Header("Характеристики")]
    public float speed = 15f;
    public float damage = 5f;
    public bool homing = true;   // летит за целью или по прямой

    [Header("Ориентация модели")]
    [Tooltip("Доп. локальный поворот поверх наведения. По умолчанию остриё должно смотреть вдоль +Y. " +
             "Если у меша остриё вдоль +X (напр. ледяная стрела) — выставить (0, 0, 90).")]
    public Vector3 rotationOffsetEuler = Vector3.zero;

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

    protected WeaponDefinition sourceWeapon;
    protected TowerAttack ownerTower;

    private PooledObject pooledObject;

    protected virtual void Awake()
    {
        pooledObject = GetComponent<PooledObject>();

        if (increasePlayerMaxHealthOnKill)
        {
            FindPlayerHealth();
        }
    }

    public void InitAttack(AttackContext context)
    {
        damage = context.damage;
        speed = context.projectileSpeed;
        SetTarget(context.target);
        sourceWeapon = context.weapon;
        ownerTower = context.ownerTower;
        lifeTimer = 0f;
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
            Vector3 targetPos = GetTargetCenter(target);
            moveDir = (targetPos - transform.position).normalized;
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
            pooledObject.Release();
            return;
        }

        // самонаведение
        if (homing && target != null)
        {
            Vector3 targetPos = GetTargetCenter(target);
            moveDir = (targetPos - transform.position).normalized;
            UpdateRotation();
        }

        float distanceThisFrame = speed * Time.deltaTime;

        // Если почти долетели до цели — считаем, что попали
        if (target != null)
        {
            Vector3 targetCenter = GetTargetCenter(target);
            float distToTarget = Vector3.Distance(transform.position, targetCenter);
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
            // Поворачиваем так, чтобы локальная ось Y смотрела по направлению движения,
            // плюс поправка на ориентацию меша (rotationOffsetEuler).
            transform.rotation = Quaternion.FromToRotation(Vector3.up, moveDir) * Quaternion.Euler(rotationOffsetEuler);
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
            enemy.TakeWeaponDamage(damage, sourceWeapon);
            DamageStatsManager.Instance?.RegisterDamage(sourceWeapon, damage);

            // именно этот удар убил врага (а не долетевший позже снаряд по уже мёртвому)
            bool killedByThisHit = hpBefore > 0f && enemy.CurrentHealth <= 0f;

            // хук для спец-пуль (IceBullet, ядовитые и т.п.)
            OnEnemyHit(enemy, killedByThisHit);

            // бонус к здоровью при убийстве
            if (increasePlayerMaxHealthOnKill && killedByThisHit)
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

        pooledObject.Release();
    }

    /// <summary>
    /// Переопределяем в наследниках (IceBullet и т.п.), чтобы добавить эффект при попадании.
    /// Базовая реализация — ничего не делает.
    /// </summary>
    /// <param name="enemy">Враг, по которому попали.</param>
    /// <param name="killedByThisHit">Именно этот удар убил врага (переход жив -> мёртв).</param>
    protected virtual void OnEnemyHit(Enemy enemy, bool killedByThisHit)
    {
        // по умолчанию — ничего
    }

    /// <summary>
    /// Возвращает центр цели для наведения.
    /// </summary>
    protected Vector3 GetTargetCenter(Transform targetTransform)
    {
        Enemy enemy = targetTransform.GetComponent<Enemy>();
        if (enemy != null)
        {
            return enemy.GetCenterPosition();
        }
        return targetTransform.position;
    }
}
