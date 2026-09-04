using System.Collections.Generic;
using UnityEngine;

public enum EnemyAttackType
{
    Melee,      // ближний бой
    MidRange,   // средняя дистанция
    LongRange   // дальняя дистанция
}

public class Enemy : MonoBehaviour
{
    private sealed class RendererPropertyState
    {
        public Renderer renderer;
        public int materialIndex;
        public MaterialPropertyBlock originalProperties;
    }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

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

    [Header("Death Audio")]
    [SerializeField] private AudioClip deathSound;
    [SerializeField, Range(0f, 1f)] private float deathSoundVolume = 0.8f;

    private float currentHealth;
    public float CurrentHealth => currentHealth;
    public uint ActivationVersion { get; private set; }
    public int SpikesAttackStackCount { get; private set; }
    public int TowerAttackCount { get; private set; }

    private Transform player;
    private PlayerHealth playerHealth;

    private float attackTimer = 0f;
    public bool isDead = false;
    private EnemyAttackFeedback attackFeedback;
    [HideInInspector]
    public int bonusGold = 0;

    private readonly List<RendererPropertyState> variantRendererStates = new();
    private float goldRewardMultiplier = 1f;
    public bool IsGolden { get; private set; }


    // ---- Замедление ----
    private float currentSpeed;   // фактическая скорость сейчас
    private bool isSlowed = false;
    private float slowEndTime = 0f; // время, когда закончится замедление

    private bool isKnockedBack = false;
    private Vector3 knockbackDirection;
    private float knockbackSpeed = 0f;        // сколько юнитов в сек
    private float knockbackTimeLeft = 0f;
    public System.Action<Enemy> OnDeath;
    public static event System.Action<float, WeaponDamageType, Vector3> WeaponDamageTaken;
    public bool returnToPoolInsteadOfDestroy = false;
    
    private Animator animator;
    private EnemyStatusEffectController statusEffects;
    public EnemyStatusEffectController StatusEffects => statusEffects;
    public bool IsStunned => statusEffects != null && statusEffects.IsStunned;
    private float towerAttackDamageMultiplier = 1f;
    public float CurrentDamageToPlayer => damageToPlayer * towerAttackDamageMultiplier *
        (statusEffects != null ? statusEffects.OutgoingDamageMultiplier : 1f) *
        GetSlowAuraMultiplier();

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
    private static readonly int IdleHash = Animator.StringToHash("Idle");
    private static readonly int IdleLowerHash = Animator.StringToHash("idle");
    private static readonly int Idle01Hash = Animator.StringToHash("Idle01");
    private static readonly int Idle01LowerHash = Animator.StringToHash("idle01");
    private int currentAnimHash = 0; // какое состояние сейчас играем (чтобы не дёргать Play повторно)

    private void OnEnable()
    {
        unchecked
        {
            ActivationVersion++;
            if (ActivationVersion == 0)
                ActivationVersion = 1;
        }

        ResetState();
        EnemyManager.Instance?.RegisterEnemy(this);
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
        statusEffects = GetComponent<EnemyStatusEffectController>();
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

        if (statusEffects != null && statusEffects.BlocksActions)
        {
            SnapToGround();
            return;
        }

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
        if (animator == null) return;

        // Slow Aura affects only attack animation. Run/Idle/Death keep their authored speed.
        animator.speed = stateHash == AttackHash ? GetSlowAuraMultiplier() : 1f;

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

        transform.position += dir * currentSpeed * GetSlowAuraMultiplier() * Time.deltaTime;

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
        float attackSpeedMultiplier = GetSlowAuraMultiplier();
        if (animator != null)
            animator.speed = attackSpeedMultiplier;

        // Замедляем таймер тем же множителем, что и клип атаки. Иначе следующий
        // запуск Attack мог бы оборвать клип до animation event с уроном.
        attackTimer -= Time.deltaTime * attackSpeedMultiplier;

        if (attackSpeedMultiplier <= 0f)
            return;

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
        // Защита от animation event старого Attack-клипа в кадр Stun/Freeze.
        if (isDead || (statusEffects != null && statusEffects.BlocksActions))
            return;

        attackLogic.Attack(this, player);
    }

    #region Урон / смерть
    public void InitStats(float newMaxHealth, float newDamageToPlayer)
    {
        maxHealth = newMaxHealth;
        damageToPlayer = newDamageToPlayer;
        currentHealth = maxHealth;   // важно обновить текущее здоровье под новый максимум
    }

    public bool ApplyGoldenModifiers(
        float healthMultiplier,
        float damageMultiplier,
        float rewardMultiplier,
        Color tint,
        float tintStrength)
    {
        if (IsGolden)
            return false;

        healthMultiplier = Mathf.Max(1f, healthMultiplier);
        damageMultiplier = Mathf.Max(1f, damageMultiplier);
        goldRewardMultiplier = Mathf.Max(1f, rewardMultiplier);

        InitStats(maxHealth * healthMultiplier, damageToPlayer * damageMultiplier);
        ApplyVariantTint(tint, Mathf.Clamp01(tintStrength), Color.black, 0f);
        IsGolden = true;
        return true;
    }

    public void ApplyBossContractVisual(
        Material outlineMaterial,
        Color outlineColor,
        float outlineWidth,
        float glowIntensity,
        float scaleMultiplier)
    {
        BossContractOutline outline = GetComponent<BossContractOutline>();
        if (outline == null)
            outline = gameObject.AddComponent<BossContractOutline>();

        outline.Show(outlineMaterial, outlineColor, outlineWidth, glowIntensity);
        transform.localScale = originalScale * Mathf.Max(1f, scaleMultiplier);
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
    /// Регистрирует одну фактическую попытку атаки по башне. Вызывается до проверки
    /// блока, поэтому заблокированная атака тоже считается. Первые N атак не меняют
    /// урон; начиная с N+1 текущий урон умножается на (1 + процент / 100).
    /// </summary>
    public void RegisterTowerAttack()
    {
        TowerAttackCount++;

        int threshold = EnemyManager.Instance != null
            ? EnemyManager.Instance.DamageGrowthStartAfterAttacks
            : 3;
        float growthPercent = EnemyManager.Instance != null
            ? EnemyManager.Instance.DamageGrowthPercentPerAttack
            : 0f;

        if (TowerAttackCount <= threshold || growthPercent <= 0f)
            return;

        towerAttackDamageMultiplier *= 1f + (growthPercent / 100f);
    }

    public int IncrementSpikesAttackStack()
    {
        SpikesAttackStackCount++;
        return SpikesAttackStackCount;
    }

    public void TakeWeaponDamage(
        float amount,
        WeaponDefinition sourceWeapon,
        bool isFromSpikes = false)
    {
        if (sourceWeapon == null)
        {
            TakeDamage(amount, isFromSpikes);
            return;
        }

        if (isDead)
            return;

        Vector3 hitPosition = GetCenterPosition();
        TakeDamage(amount, isFromSpikes);

        if (amount > 0f)
            WeaponDamageTaken?.Invoke(amount, sourceWeapon.damageType, hitPosition);

        if (!isFromSpikes && amount > 0f && sourceWeapon.effectProfile != null)
        {
            var hitContext = new WeaponHitContext(
                this,
                sourceWeapon,
                amount,
                isDead);
            sourceWeapon.effectProfile.ProcessHit(hitContext);
        }
    }

    public void TakeWeaponDamage(
        float amount,
        WeaponDamageType damageType,
        bool isFromSpikes = false)
    {
        if (isDead)
            return;

        Vector3 hitPosition = GetCenterPosition();
        TakeDamage(amount, isFromSpikes);

        if (amount > 0f)
            WeaponDamageTaken?.Invoke(amount, damageType, hitPosition);
    }

    /// <summary>
    /// Урон от уже наложенного статуса. Показывает popup, но намеренно не запускает
    /// эффекты оружия повторно.
    /// </summary>
    public void TakeStatusDamage(float amount, WeaponDefinition sourceWeapon)
    {
        if (isDead || amount <= 0f)
            return;

        WeaponDamageType damageType = sourceWeapon != null
            ? sourceWeapon.damageType
            : WeaponDamageType.Normal;
        Vector3 hitPosition = GetCenterPosition();
        TakeDamage(amount);
        WeaponDamageTaken?.Invoke(amount, damageType, hitPosition);
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

    /// <summary>
    /// Полностью останавливает действия врага на duration секунд. Текущая атака
    /// отменяется, Animator переходит в Idle, а после окончания новая атака
    /// начинается с первого кадра.
    /// </summary>
    public void ApplyStun(float duration)
    {
        statusEffects?.ApplyStun(duration);
    }

    public void ApplyFreeze(float duration)
    {
        statusEffects?.ApplyFreeze(duration);
    }

    private void Die(bool killedBySpikes = false)
    {
        if (isDead) return;
        isDead = true;
        ResetTowerAttackDamageGrowth();

        PlayDeathSound();

        statusEffects?.ClearAll(false);

        // Отключаем физику, чтобы объект не проваливался при масштабировании
        DisablePhysics();

        // Золото
        if (GoldManager.Instance != null)
        {
            int perKillBonus = 0;
            if (UpgradesManager.Instance != null)
                perKillBonus = UpgradesManager.Instance.goldBonusPerKill;

            int baseReward = baseGold + bonusGold + perKillBonus;
            int goldReward = Mathf.Max(0, Mathf.RoundToInt(baseReward * goldRewardMultiplier));
            GoldManager.Instance.AddGold(goldReward, GoldSource.Kill, transform.position);
        }

        // Хил за килл + улучшение шипов (если апгрейд куплен).
        // Без null-проверки исключение здесь прервало бы OnDeath ниже — и награда за босса не открылась бы.
        if (playerHealth != null)
            playerHealth.OnEnemyKilled(killedBySpikes);

        if (animator != null)
        {
            animator.speed = 1f;
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

    private void PlayDeathSound()
    {
        if (deathSound == null)
            return;

        float volume = deathSoundVolume;
        if (AudioManager.Instance != null)
            volume *= AudioManager.Instance.GetSoundVolume();

        AudioSource.PlayClipAtPoint(deathSound, transform.position, Mathf.Clamp01(volume));
    }

    private void DisablePhysics()
    {
        if (enemyRigidbody != null)
        {
            // Скорости сбрасываем только у динамического тела: запись velocity в kinematic
            // запрещена и сыплет ошибками в консоль. У kinematic обнулять уже нечего.
            if (!enemyRigidbody.isKinematic)
            {
                enemyRigidbody.linearVelocity = Vector3.zero;
                enemyRigidbody.angularVelocity = Vector3.zero;
            }
            enemyRigidbody.isKinematic = true;
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
        SpikesAttackStackCount = 0;
        ResetTowerAttackDamageGrowth();
        isSlowed = false;
        isKnockedBack = false;
        statusEffects?.ClearAll(false);
        bonusGold = 0;
        goldRewardMultiplier = 1f;
        RestoreVariantVisual();

        // Враги двигаются через transform, поэтому держим Rigidbody кинематическим:
        // так физика не отбрасывает их друг от друга и от башни и они не застревают
        // на статичных объектах (забор/фонарь). Коллайдер нужен для попаданий снарядов.
        if (enemyRigidbody != null)
        {
            // См. DisablePhysics: velocity пишем только пока тело динамическое.
            if (!enemyRigidbody.isKinematic)
            {
                enemyRigidbody.linearVelocity = Vector3.zero;
                enemyRigidbody.angularVelocity = Vector3.zero;
            }
            enemyRigidbody.isKinematic = true;
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
            animator.speed = 1f;
            animator.Rebind();
            animator.Play("Run", 0, 0f);
            animator.Update(0f);
            currentAnimHash = RunHash;
        }

        // Подстраховка: основной сброс масштаба выполняется ДО ухода в пул (см. OnDeathAnimationFinished).
        transform.localScale = originalScale;
    }

    private void ResetTowerAttackDamageGrowth()
    {
        TowerAttackCount = 0;
        towerAttackDamageMultiplier = 1f;
    }

    private void ApplyVariantTint(
        Color tint,
        float strength,
        Color emission,
        float emissionIntensity)
    {
        RestoreVariantVisual();

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer is ParticleSystemRenderer ||
                targetRenderer is TrailRenderer ||
                targetRenderer is LineRenderer)
            {
                continue;
            }

            Material[] materials = targetRenderer.sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material == null)
                    continue;

                int colorProperty;
                if (material.HasProperty(BaseColorId))
                    colorProperty = BaseColorId;
                else if (material.HasProperty(ColorId))
                    colorProperty = ColorId;
                else
                    continue;

                var originalProperties = new MaterialPropertyBlock();
                targetRenderer.GetPropertyBlock(originalProperties, materialIndex);
                variantRendererStates.Add(new RendererPropertyState
                {
                    renderer = targetRenderer,
                    materialIndex = materialIndex,
                    originalProperties = originalProperties
                });

                var goldenProperties = new MaterialPropertyBlock();
                targetRenderer.GetPropertyBlock(goldenProperties, materialIndex);

                Color originalColor = material.GetColor(colorProperty);
                Color goldenColor = Color.Lerp(originalColor, tint, strength);
                goldenColor.a = originalColor.a;
                goldenProperties.SetColor(colorProperty, goldenColor);

                if (emissionIntensity > 0f && material.HasProperty(EmissionColorId))
                    goldenProperties.SetColor(EmissionColorId, emission * emissionIntensity);

                targetRenderer.SetPropertyBlock(goldenProperties, materialIndex);
            }
        }
    }

    private void RestoreVariantVisual()
    {
        foreach (RendererPropertyState state in variantRendererStates)
        {
            if (state.renderer != null)
                state.renderer.SetPropertyBlock(state.originalProperties, state.materialIndex);
        }

        variantRendererStates.Clear();
        IsGolden = false;
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
        if (statusEffects != null && statusEffects.BlocksActions) return;
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

    public void PlayStunIdlePose()
    {
        if (animator == null)
            return;

        animator.speed = 1f;

        int idleStateHash = GetAvailableIdleState();
        if (idleStateHash != 0)
        {
            animator.Play(idleStateHash, 0, 0f);
            animator.Update(0f);
            currentAnimHash = idleStateHash;
            return;
        }

        // У Skeleton-контроллера нет отдельного Idle. Для него берём первый
        // нейтральный кадр Run и удерживаем его — но никогда не кадр Attack.
        if (animator.HasState(0, RunHash))
        {
            animator.Play(RunHash, 0, 0f);
            animator.Update(0f);
            animator.speed = 0f;
            currentAnimHash = RunHash;
        }
    }

    private int GetAvailableIdleState()
    {
        if (animator.HasState(0, IdleHash)) return IdleHash;
        if (animator.HasState(0, IdleLowerHash)) return IdleLowerHash;
        if (animator.HasState(0, Idle01Hash)) return Idle01Hash;
        if (animator.HasState(0, Idle01LowerHash)) return Idle01LowerHash;
        return 0;
    }

    private float GetSlowAuraMultiplier()
    {
        return UpgradesManager.Instance != null
            ? UpgradesManager.Instance.GetEnemySlowAuraMultiplier(transform.position)
            : 1f;
    }

    public void PauseAnimationForFreeze()
    {
        attackTimer = 0f;
        if (animator != null)
            animator.speed = 0f;
    }

    public void ResumeAfterControlEffect()
    {
        attackTimer = 0f;
        currentAnimHash = 0;
        if (animator != null)
            animator.speed = 1f;
    }

    public void CancelKnockbackForControlEffect()
    {
        isKnockedBack = false;
        knockbackSpeed = 0f;
        knockbackTimeLeft = 0f;
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
