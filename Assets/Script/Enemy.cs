using UnityEngine;

public enum EnemyAttackType
{
    Melee,      // ближний бой
    MidRange,   // средняя дистанция
    LongRange   // дальняя дистанция
}

public class Enemy : MonoBehaviour
{
    [Header("Параметры врага")]
    public float speed = 2f;
    public float maxHealth = 20f;
    public float damageToPlayer = 10f;
    [Tooltip("Базовое золото за убийство этого врага.")]
    public int baseGold = 1;

    [Header("Эффекты")]
    public bool canBeSlowed = true;   // можно ли замедлять этого врага
    public bool canBeKnocked = true;    // можно ли отталкивать

    [Header("Атака по башне")]
    public EnemyAttackType attackType = EnemyAttackType.Melee;
    public float attackRange = 2.5f;
    public float attackInterval = 1.0f;

    [Header("Дополнительно")]
    public bool overrideAttackRange = false;

    private float currentHealth;
    public float CurrentHealth => currentHealth;

    private Transform player;
    private PlayerHealth playerHealth;

    private float attackTimer = 0f;
    public bool isDead = false;
    private EnemyAttackFeedback attackFeedback;
    [HideInInspector]
    public int bonusGold = 0;


    // ---- Замедление ----
    private float currentSpeed;   // фактическая скорость сейчас
    private bool isSlowed = false;
    private float slowEndTime = 0f; // время, когда закончится замедление

    private bool isKnockedBack = false;
    private Vector3 knockbackDirection;
    private float knockbackSpeed = 0f;        // сколько юнитов в сек
    private float knockbackTimeLeft = 0f;
    public System.Action<Enemy> OnDeath;
    public bool returnToPoolInsteadOfDestroy = false;
    
    private Animator animator;

    private IEnemyAttack attackLogic;

    private PooledObject pooledObject;

    private void OnEnable() => EnemyManager.Instance?.RegisterEnemy(this);
    private void OnDisable() => EnemyManager.Instance?.UnregisterEnemy(this);

    private void Awake()
    {
        currentHealth = maxHealth;
        currentSpeed = speed; // стартовая скорость
        animator = GetComponent<Animator>();
        attackLogic = GetComponent<IEnemyAttack>();
        pooledObject = GetComponent<PooledObject>();
    }

    private void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerHealth = playerObj.GetComponent<PlayerHealth>();
        }

        if (!overrideAttackRange)
        {
            switch (attackType)
            {
                case EnemyAttackType.Melee:
                    attackRange = 2.5f;
                    break;
                case EnemyAttackType.MidRange:
                    attackRange = 6f;
                    break;
                case EnemyAttackType.LongRange:
                    attackRange = 10f;
                    break;
            }
        }

        attackTimer = 0f;
        attackFeedback = GetComponent<EnemyAttackFeedback>();
    }

    private void Update()
    {
        if (isDead || player == null || GameStateManager.Instance.CurrentState != GameState.Playing) return;

        // снимаем замедление, если время вышло
        if (isKnockedBack)
        {
            float dt = Time.deltaTime;
            float move = knockbackSpeed * dt;

            transform.position += knockbackDirection * move;

            knockbackTimeLeft -= dt;
            if (knockbackTimeLeft <= 0f)
            {
                isKnockedBack = false;
            }

            return; // пока отталкиваемся – НЕ идём к башне и не атакуем
        }
        if (isSlowed && Time.time >= slowEndTime)
        {
            isSlowed = false;
            currentSpeed = speed; // возвращаем базовую скорость
        }

        float distance = Vector3.Distance(transform.position, player.position);

        if (distance > attackRange)
        {
            MoveTowardsPlayer();
            animator.Play("Run");
        }
        else
        {
            HandleAttack();
        }
    }

    private void MoveTowardsPlayer()
    {
        Vector3 dir = (player.position - transform.position).normalized;
        transform.position += dir * currentSpeed * Time.deltaTime;

        if (dir != Vector3.zero)
        {
            transform.forward = new Vector3(dir.x, 0f, dir.z);
        }
    }

    private void HandleAttack()
    {
        attackTimer -= Time.deltaTime;

        if (attackTimer <= 0f)
        {
            //AttackPlayer();
            animator.Play("Attack");
            attackTimer = attackInterval;
        }
    }

    // вызывается в анимации атаки
    private void AttackPlayer()
    {
        attackLogic.Attack(this, player);
    }

    #region Урон / смерть
    public void InitStats(float newMaxHealth, float newDamageToPlayer)
    {
        maxHealth = newMaxHealth;
        damageToPlayer = newDamageToPlayer;
        currentHealth = maxHealth;   // важно обновить текущее здоровье под новый максимум
    }

    public void TakeDamage(float amount, bool isFromSpikes = false)
    {
        if (isDead) return;

        currentHealth -= amount;
        if (currentHealth <= 0f)
        {
            Die(isFromSpikes);
        }
    }

    /// <summary>
    /// Замедление врага: slowMultiplier &lt; 1 = медленнее (0.5 = в 2 раза медленнее).
    /// </summary>
    public void ApplySlow(float slowMultiplier, float duration)
    {
        if (!canBeSlowed) return;
        if (duration <= 0f) return;

        slowMultiplier = Mathf.Clamp(slowMultiplier, 0.01f, 1f);

        currentSpeed = speed * slowMultiplier;
        isSlowed = true;
        slowEndTime = Time.time + duration;
    }

    private void Die(bool killedBySpikes = false)
    {
        if (isDead) return;
        isDead = true;

        // Золото
        if (GoldManager.Instance != null)
        {
            int perKillBonus = 0;
            if (UpgradesManager.Instance != null)
                perKillBonus = UpgradesManager.Instance.goldBonusPerKill;

            int goldReward = baseGold + bonusGold + perKillBonus;
            GoldManager.Instance.AddGold(goldReward, GoldSource.Kill, transform.position);
        }

        // Хил за килл + улучшение шипов (если апгрейд куплен)

        playerHealth.OnEnemyKilled(killedBySpikes);

        animator.Play("Death");

        OnDeath?.Invoke(this);

        if (returnToPoolInsteadOfDestroy)
        {
            gameObject.SetActive(false);
            return;
        }        
    }

    #endregion

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
    public void ApplyKnockback(Vector3 sourcePosition, float distance, float duration)
    {
        if (isDead) return;
        if (!canBeKnocked) return;
        if (distance <= 0f || duration <= 0f) return;

        // направление: от башни к врагу
        knockbackDirection = (transform.position - sourcePosition).normalized;
        if (knockbackDirection.sqrMagnitude < 0.0001f)
            return;

        knockbackSpeed = distance / duration;            // юнит/сек
        knockbackTimeLeft = duration;
        isKnockedBack = true;
    }

    public void OnDeathAnimationFinished()
    {
        gameObject.SetActive(false);
        pooledObject.Release();
    }

    /// <summary>
    /// Возвращает центр врага (используется для наведения снарядов и лазера).
    /// </summary>
    public Vector3 GetCenterPosition()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            return col.bounds.center;
        }
        // фоллбэк: середина между позицией и верхней точкой (примерно центр высоты)
        return transform.position + Vector3.up * 1f;
    }
}
