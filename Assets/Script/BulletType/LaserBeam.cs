using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum LaserBeamAxis
{
    PositiveZ,
    PositiveY
}

public class LaserBeam : MonoBehaviour, IAttackBehaviour
{
    [Header("Laser Settings")]
    [Tooltip("Damage dealt by one tick. Set from WeaponDefinition at runtime.")]
    public float damagePerTick = 10f;

    [Tooltip("Visual transform stretched along the configured local axis.")]
    public Transform beamMesh;

    [SerializeField] private LaserBeamAxis beamAxis = LaserBeamAxis.PositiveZ;
    [SerializeField, Min(0.01f)] private float referenceLength = 1f;
    [SerializeField] private bool centerBeamMesh = true;
    [SerializeField] private bool reverseBeamVisual;

    [Header("Lifetime")]
    [Tooltip("Maximum beam lifetime in seconds.")]
    public float maxLifeTime = 15f;

    [Header("Damage Ramp")]
    [SerializeField, Tooltip("Enables damage growth while this beam continuously holds the same target.")]
    private bool enableDamageRamp;
    [SerializeField, Min(0f), Tooltip("Bonus base damage gained for each completed second on the same target.")]
    private float damageGainPerSecondPercent = 20f;
    [SerializeField, Min(0f), Tooltip("Maximum bonus base damage. 200% means up to 3x total base damage.")]
    private float maxDamageBonusPercent = 200f;

    [Header("Kill Effect")]
    public bool increasePlayerMaxHealthOnKill = false;
    public float maxHealthIncreaseAmount = 10f;

    private static readonly Dictionary<BeamKey, LaserBeam> ActiveBeams = new Dictionary<BeamKey, LaserBeam>();

    private Transform firePoint;
    private Enemy targetEnemy;
    private Transform targetTransform;
    private TowerAttack ownerTower;
    private DamageCalculator damageCalculator;
    private float baseDamagePerTick;
    private float baseFireRate = 1f;
    private float currentTickInterval = 1f;
    private Coroutine damageRoutine;
    private PlayerHealth cachedPlayerHealth;
    private float lifeTimer;
    private float targetLockStartTime;
    private WeaponDefinition sourceWeapon;
    private int weaponStackIndex;
    private PooledObject pooledObject;

    private Vector3 initialBeamLocalPosition;
    private Vector3 initialBeamLocalScale;
    private Quaternion initialBeamLocalRotation;
    private bool hasInitialBeamTransform;
    private bool isReleasing;
    private BeamKey activeBeamKey;
    private bool hasActiveBeamKey;

    public static LaserBeam GetActiveBeamFor(Enemy enemy, WeaponDefinition weapon, int stackIndex)
    {
        if (enemy == null || weapon == null)
            return null;

        ActiveBeams.TryGetValue(new BeamKey(enemy, weapon, stackIndex), out var beam);
        return beam;
    }

    private void Awake()
    {
        pooledObject = GetComponent<PooledObject>();

        if (beamMesh != null)
        {
            initialBeamLocalPosition = beamMesh.localPosition;
            initialBeamLocalScale = beamMesh.localScale;
            initialBeamLocalRotation = beamMesh.localRotation;
            hasInitialBeamTransform = true;
        }

        if (increasePlayerMaxHealthOnKill)
            FindPlayerHealth();
    }

    private void OnEnable()
    {
        lifeTimer = 0f;
        targetLockStartTime = 0f;
        isReleasing = false;
    }

    private void OnDisable()
    {
        if (damageRoutine != null)
        {
            StopCoroutine(damageRoutine);
            damageRoutine = null;
        }

        if (targetEnemy != null)
            targetEnemy.OnDeath -= HandleTargetDeath;

        if (hasActiveBeamKey &&
            ActiveBeams.TryGetValue(activeBeamKey, out var beam) &&
            beam == this)
        {
            ActiveBeams.Remove(activeBeamKey);
        }

        ResetBeamVisual();

        firePoint = null;
        targetEnemy = null;
        targetTransform = null;
        ownerTower = null;
        damageCalculator = null;
        baseDamagePerTick = 0f;
        sourceWeapon = null;
        weaponStackIndex = 0;
        targetLockStartTime = 0f;
        hasActiveBeamKey = false;
    }

    private void Update()
    {
        lifeTimer += Time.deltaTime;
        if (lifeTimer >= maxLifeTime)
        {
            ReleaseBeam();
            return;
        }

        if (!IsTargetValid())
        {
            ReleaseBeam();
            return;
        }

        UpdateBeamTransform();
    }

    public void InitAttack(AttackContext context)
    {
        Enemy enemy = context.target != null
            ? context.target.GetComponent<Enemy>()
            : null;

        if (enemy == null || context.weapon == null)
        {
            ReleaseBeam();
            return;
        }

        sourceWeapon = context.weapon;
        weaponStackIndex = context.weaponStackIndex;
        firePoint = context.firePoint;
        targetEnemy = enemy;
        targetTransform = enemy.transform;
        ownerTower = context.ownerTower;
        damageCalculator = context.damageCalculator;
        baseDamagePerTick = context.baseDamage;
        damagePerTick = context.damage;
        baseFireRate = Mathf.Max(0.01f, context.weaponFireRate);
        targetLockStartTime = Time.time;

        targetEnemy.OnDeath += HandleTargetDeath;

        activeBeamKey = new BeamKey(targetEnemy, sourceWeapon, weaponStackIndex);
        hasActiveBeamKey = true;

        if (ActiveBeams.TryGetValue(activeBeamKey, out var existing) &&
            existing != null &&
            existing != this)
        {
            existing.ReleaseBeam();
        }

        ActiveBeams[activeBeamKey] = this;

        RecalculateTickInterval();
        UpdateBeamTransform();
        damageRoutine = StartCoroutine(DamageLoop());
    }

    public void RefreshContext(
        Transform newFirePoint,
        float newBaseDamagePerTick,
        float newDamagePerTick,
        TowerAttack owner,
        float weaponFireRate)
    {
        firePoint = newFirePoint;
        baseDamagePerTick = newBaseDamagePerTick;
        damagePerTick = newDamagePerTick;
        ownerTower = owner;
        baseFireRate = Mathf.Max(0.01f, weaponFireRate);
        lifeTimer = 0f;

        RecalculateTickInterval();
    }

    private IEnumerator DamageLoop()
    {
        while (IsTargetValid())
        {
            RecalculateTickInterval();
            yield return new WaitForSeconds(currentTickInterval);

            if (!IsTargetValid())
                break;

            Enemy attackedEnemy = targetEnemy;
            WeaponDefinition attackingWeapon = sourceWeapon;
            float healthBefore = attackedEnemy.CurrentHealth;
            float rampBonusPercent = GetCurrentRampBonusPercent();
            float rampedBaseDamage = baseDamagePerTick * (1f + rampBonusPercent / 100f);
            float currentDamage = CalculateRampedDamage(rampedBaseDamage);

            if (ownerTower != null && ownerTower.DebugDamageEnabled && enableDamageRamp)
            {
                float elapsed = Mathf.Max(0f, Time.time - targetLockStartTime);
                Debug.Log(
                    $"[DamageDebug] {attackingWeapon.name}: laser ramp time={elapsed:0.###}s, " +
                    $"bonus=+{rampBonusPercent:0.###}%, base={baseDamagePerTick:0.###}, " +
                    $"ramped base={rampedBaseDamage:0.###}, final={currentDamage:0.###}, " +
                    $"stack={weaponStackIndex}.");
            }

            attackedEnemy.TakeDamage(currentDamage);
            DamageStatsManager.Instance?.RegisterDamage(attackingWeapon, currentDamage);

            if (increasePlayerMaxHealthOnKill &&
                healthBefore > 0f &&
                attackedEnemy.CurrentHealth <= 0f)
            {
                if (cachedPlayerHealth == null)
                    FindPlayerHealth();

                if (cachedPlayerHealth != null)
                    cachedPlayerHealth.IncreaseMaxHealth(maxHealthIncreaseAmount, alsoHeal: true);
            }
        }

        ReleaseBeam();
    }

    private float CalculateRampedDamage(float rampedBaseDamage)
    {
        if (!enableDamageRamp || damageCalculator == null || sourceWeapon == null)
            return damagePerTick;

        return damageCalculator.Calculate(new DamageContext
        {
            baseDamage = rampedBaseDamage,
            damageType = sourceWeapon.damageType,
            itemTier = sourceWeapon.itemTier,
            isSpikes = false
        });
    }

    private float GetCurrentRampBonusPercent()
    {
        if (!enableDamageRamp || damageGainPerSecondPercent <= 0f || maxDamageBonusPercent <= 0f)
            return 0f;

        float elapsed = Mathf.Max(0f, Time.time - targetLockStartTime);
        int completedSeconds = Mathf.FloorToInt(elapsed + 0.0001f);
        return Mathf.Min(maxDamageBonusPercent, completedSeconds * damageGainPerSecondPercent);
    }

    private bool IsTargetValid()
    {
        return firePoint != null &&
            targetEnemy != null &&
            targetTransform != null &&
            !targetEnemy.isDead &&
            targetEnemy.gameObject.activeInHierarchy;
    }

    private void UpdateBeamTransform()
    {
        if (!IsTargetValid())
            return;

        Vector3 start = firePoint.position;
        Vector3 direction = targetEnemy.GetCenterPosition() - start;
        float distance = direction.magnitude;

        if (distance <= 0.001f)
            return;

        transform.position = start;
        transform.rotation = beamAxis == LaserBeamAxis.PositiveY
            ? Quaternion.FromToRotation(Vector3.up, direction.normalized)
            : Quaternion.LookRotation(direction.normalized, Vector3.up);

        if (beamMesh == null)
            return;

        Vector3 localAxis = beamAxis == LaserBeamAxis.PositiveY ? Vector3.up : Vector3.forward;
        float worldAxisScale = beamAxis == LaserBeamAxis.PositiveY
            ? Mathf.Abs(transform.lossyScale.y)
            : Mathf.Abs(transform.lossyScale.z);
        float localDistance = distance / Mathf.Max(0.0001f, worldAxisScale);
        float lengthScale = localDistance / Mathf.Max(0.01f, referenceLength);
        Vector3 localScale = hasInitialBeamTransform ? initialBeamLocalScale : Vector3.one;

        if (beamAxis == LaserBeamAxis.PositiveY)
            localScale.y = lengthScale;
        else
            localScale.z = lengthScale;

        beamMesh.localScale = localScale;

        if (reverseBeamVisual)
        {
            Vector3 reversalAxis = beamAxis == LaserBeamAxis.PositiveY ? Vector3.forward : Vector3.up;
            beamMesh.localRotation = initialBeamLocalRotation * Quaternion.AngleAxis(180f, reversalAxis);
            beamMesh.localPosition = initialBeamLocalPosition + localAxis * localDistance;
        }
        else
        {
            beamMesh.localRotation = initialBeamLocalRotation;
            beamMesh.localPosition = centerBeamMesh
                ? initialBeamLocalPosition + localAxis * (localDistance * 0.5f)
                : initialBeamLocalPosition;
        }
    }

    private void RecalculateTickInterval()
    {
        float multiplier = ownerTower != null
            ? Mathf.Max(0.01f, ownerTower.TotalFireRateMultiplier)
            : 1f;

        currentTickInterval = Mathf.Max(0.05f, 1f / (baseFireRate * multiplier));
    }

    private void HandleTargetDeath(Enemy enemy)
    {
        if (enemy != targetEnemy)
            return;

        ownerTower?.RequestImmediateRetarget(sourceWeapon);
        ReleaseBeam();
    }

    private void FindPlayerHealth()
    {
        if (cachedPlayerHealth != null)
            return;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
            cachedPlayerHealth = playerObject.GetComponent<PlayerHealth>();
    }

    private void ReleaseBeam()
    {
        if (isReleasing || !gameObject.activeSelf)
            return;

        isReleasing = true;

        if (pooledObject != null)
            pooledObject.Release();
        else
            gameObject.SetActive(false);
    }

    private void ResetBeamVisual()
    {
        if (!hasInitialBeamTransform || beamMesh == null)
            return;

        beamMesh.localPosition = initialBeamLocalPosition;
        beamMesh.localScale = initialBeamLocalScale;
        beamMesh.localRotation = initialBeamLocalRotation;
    }

    private readonly struct BeamKey
    {
        private readonly Enemy enemy;
        private readonly WeaponDefinition weapon;
        private readonly int stackIndex;

        public BeamKey(Enemy enemy, WeaponDefinition weapon, int stackIndex)
        {
            this.enemy = enemy;
            this.weapon = weapon;
            this.stackIndex = stackIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is BeamKey other &&
                ReferenceEquals(enemy, other.enemy) &&
                ReferenceEquals(weapon, other.weapon) &&
                stackIndex == other.stackIndex;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int enemyHash = !ReferenceEquals(enemy, null) ? enemy.GetInstanceID() : 0;
                int weaponHash = !ReferenceEquals(weapon, null) ? weapon.GetInstanceID() : 0;
                return (((enemyHash * 397) ^ weaponHash) * 397) ^ stackIndex;
            }
        }
    }
}
