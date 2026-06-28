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
    private Rigidbody enemyRigidbody;
    private Collider enemyCollider;

    private LayerMask groundMask;   // слой рельефа для "приземления"
    private float footOffset = 0f;  // смещение pivot над низом коллайдера

    private Vector3 originalScale = Vector3.one; // исходный масштаб префаба (Death-анимация ужимает root в 0)

    // Кэш хэшей состояний аниматора — чтобы не хэшировать строку в Play(string) каждый кадр.
    private static readonly int RunHash = Animator.StringToHash("Run");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int DeathHash = Animator.StringToHash("Death");
    private int currentAnimHash = 0; // какое состояние сейчас играем (чтобы не дёргать Play повторно)

    private void OnEnable()
    {
        EnemyManager.Instance?.RegisterEnemy(this);
        ResetState();
    }
    private void OnDisable()
    {
        EnemyManager.Instance?.UnregisterEnemy(this);
    }

    private void Awake()
    {
        currentHealth = maxHealth;
        currentSpeed = speed; // стартовая скорость
        animator = GetComponent<Animator>();
        attackLogic = GetComponent<IEnemyAttack>();
        pooledObject = GetComponent<PooledObject>();
        enemyRigidbody = GetComponent<Rigidbody>();
        enemyCollider = GetComponent<Collider>();
        originalScale = transform.localScale; // запоминаем до того, как анимация смерти изменит scale

        // Враги кинематические (без гравитации), поэтому "приземляем" их сами по рельефу.
        groundMask = LayerMask.GetMask("Terrain");
        if (enemyCollider != null)
            footOffset = transform.position.y - enemyCollider.bounds.min.y; // расстояние от pivot до низа коллайдера
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

        // sqrMagnitude вместо Vector3.Distance — убираем sqrt каждый кадр на каждого врага
        float sqrDistance = (transform.position - player.position).sqrMagnitude;

        if (sqrDistance > attackRange * attackRange)
        {
            MoveTowardsPlayer();
            PlayAnim(RunHash);
        }
        else
        {
            HandleAttack();
        }

        SnapToGround();
    }

    // Запускаем состояние аниматора только при смене (для непрерывных состояний вроде Run).
    private void PlayAnim(int stateHash)
    {
        if (currentAnimHash == stateHash) return;
        currentAnimHash = stateHash;
        animator.Play(stateHash);
    }

    private void MoveTowardsPlayer()
    {
        // Двигаемся только в горизонтальной плоскости; высоту задаёт SnapToGround.
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        dir = dir.normalized;

        transform.position += dir * currentSpeed * Time.deltaTime;

        if (dir != Vector3.zero)
        {
            transform.forward = dir;
        }
    }

    // Держим врага на поверхности рельефа (Rigidbody кинематический, гравитации нет).
    private void SnapToGround()
    {
        if (groundMask == 0) return;

        Vector3 origin = transform.position + Vector3.up * 5f;
        RaycastHit hit;
        if (Physics.Raycast(origin, Vector3.down, out hit, 50f, groundMask, QueryTriggerInteraction.Ignore))
        {
            Vector3 p = transform.position;
            p.y = hit.point.y + footOffset;
            transform.position = p;
        }
    }

    private void HandleAttack()
    {
        attackTimer -= Time.deltaTime;

        if (attackTimer <= 0f)
        {
            //AttackPlayer();
            animator.Play(AttackHash);   // перезапускаем анимацию атаки на каждый удар
            currentAnimHash = AttackHash;
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

        // Отключаем физику, чтобы объект не проваливался при масштабировании
        DisablePhysics();

        // Золото
        if (GoldManager.Instance != null)
        {
            int perKillBonus = 0;
            if (UpgradesManager.Instance != null)
                perKillBonus = UpgradesManager.Instance.goldBonusPerKill;

            int goldReward = baseGold + bonusGold + perKillBonus;
            GoldManager.Instance.AddGold(goldReward, GoldSource.Kill, transform.position);
        }

        // Хил за килл + улучшение шипов (если апгрейд куплен).
        // Без null-проверки исключение здесь прервало бы OnDeath ниже — и награда за босса не открылась бы.
        if (playerHealth != null)
            playerHealth.OnEnemyKilled(killedBySpikes);

        if (animator != null)
        {
            animator.Play(DeathHash);
            currentAnimHash = DeathHash;
        }

        OnDeath?.Invoke(this);

        if (returnToPoolInsteadOfDestroy)
        {
            transform.localScale = originalScale; // см. OnDeathAnimationFinished: нельзя уходить в пул ужатыми
            gameObject.SetActive(false);
            return;
        }
    }

    private void DisablePhysics()
    {
        if (enemyRigidbody != null)
        {
            enemyRigidbody.isKinematic = true;
            enemyRigidbody.linearVelocity = Vector3.zero;
            enemyRigidbody.angularVelocity = Vector3.zero;
        }
        if (enemyCollider != null)
        {
            enemyCollider.enabled = false;
        }
    }

    private void ResetState()
    {
        isDead = false;
        attackTimer = 0f;
        isSlowed = false;
        isKnockedBack = false;
        bonusGold = 0;

        // Враги двигаются через transform, поэтому держим Rigidbody кинематическим:
        // так физика не отбрасывает их друг от друга и от башни и они не застревают
        // на статичных объектах (забор/фонарь). Коллайдер нужен для попаданий снарядов.
        if (enemyRigidbody != null)
        {
            enemyRigidbody.isKinematic = true;
            enemyRigidbody.linearVelocity = Vector3.zero;
            enemyRigidbody.angularVelocity = Vector3.zero;
        }
        if (enemyCollider != null)
        {
            enemyCollider.enabled = true;
        }

        // Сбрасываем скорость на базовую
        currentSpeed = speed;

        // Сбрасываем animator в "Run" на случай, если объект ушёл в пул в середине другой анимации.
        if (animator != null)
        {
            animator.Rebind();
            animator.Play("Run", 0, 0f);
            animator.Update(0f);
            currentAnimHash = RunHash;
        }

        // Подстраховка: основной сброс масштаба выполняется ДО ухода в пул (см. OnDeathAnimationFinished).
        transform.localScale = originalScale;
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
        // Death-клип ужал root localScale в ~0. Возвращаем исходный масштаб ДО деактивации:
        // при следующей реактивации из пула Animator (Write Defaults = On) запоминает текущий
        // localScale root как "дефолт". Если деактивировать ужатым — дефолтом станет ~0, и состояние
        // Run будет писать этот ~0 каждый кадр (баг: призраки реюзятся почти нулевыми и проваливаются).
        transform.localScale = originalScale;
        gameObject.SetActive(false);
        pooledObject?.Release();
    }

    /// <summary>
    /// Возвращает центр врага (используется для наведения снарядов и лазера).
    /// </summary>
    public Vector3 GetCenterPosition()
    {
        if (enemyCollider != null)
        {
            return enemyCollider.bounds.center;
        }
        // фоллбэк: середина между позицией и верхней точкой (примерно центр высоты)
        return transform.position + Vector3.up * 1f;
    }
}
