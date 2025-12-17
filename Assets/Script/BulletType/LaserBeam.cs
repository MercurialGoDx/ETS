using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LaserBeam : MonoBehaviour
{
    [Header("Параметры лазера")]
    [Tooltip("Урон за один тик (будет задаваться из WeaponDefinition)")]
    public float damagePerTick = 10f;

    [Tooltip("Ребёнок, вытянутый вдоль локальной оси Z (визуал луча)")]
    public Transform beamMesh;

    [Header("Время жизни")]
    [Tooltip("Максимальное время жизни лазера в секундах (защита от зависания).")]
    public float maxLifeTime = 15f;

    [Header("Эффект при убийстве")]
    [Tooltip("Если true, при убийстве врага этим лучом игроку увеличится максимальное здоровье.")]
    public bool increasePlayerMaxHealthOnKill = false;

    [Tooltip("На сколько увеличивать максимальное здоровье при каждом убийстве.")]
    public float maxHealthIncreaseAmount = 10f;

    private Transform firePoint;
    private Enemy targetEnemy;
    private Transform targetTransform;

    private TowerAttack ownerTower;
    private float baseFireRate = 1f;       // fireRate из WeaponDefinition
    private float currentTickInterval = 1f;

    private Coroutine damageRoutine;

    private PlayerHealth cachedPlayerHealth;
    private float lifeTimer = 0f;

    // ====== ОДИН ЛУЧ НА ОДНОГО ВРАГА ======
    private static Dictionary<Enemy, LaserBeam> activeBeams = new Dictionary<Enemy, LaserBeam>();

    public static LaserBeam GetActiveBeamFor(Enemy enemy)
    {
        if (enemy == null) return null;
        activeBeams.TryGetValue(enemy, out var beam);
        return beam;
    }

    private void Awake()
    {
        if (increasePlayerMaxHealthOnKill)
        {
            FindPlayerHealth();
        }
    }

    private void FindPlayerHealth()
    {
        if (cachedPlayerHealth != null)
            return;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            cachedPlayerHealth = playerObj.GetComponent<PlayerHealth>();
        }
    }

    /// <summary>
    /// firePoint      — точка на башне, откуда рисуем луч
    /// target         — цель (враг)
    /// damagePerTick  — урон за один тик
    /// owner          — TowerAttack, чтобы брать fireRateMultiplier
    /// weaponFireRate — базовая fireRate из WeaponDefinition (выстрелов в секунду)
    /// </summary>
    public void Init(
        Transform firePoint,
        Enemy target,
        float damagePerTick,
        TowerAttack owner,
        float weaponFireRate)
    {
        this.firePoint       = firePoint;
        this.targetEnemy     = target;
        this.targetTransform = target != null ? target.transform : null;
        this.ownerTower      = owner;
        this.damagePerTick   = damagePerTick;
        this.baseFireRate    = Mathf.Max(0.01f, weaponFireRate);

        if (targetEnemy == null || targetTransform == null)
        {
            Debug.LogWarning("[LASER] Init: targetEnemy == null, уничтожаем луч");
            Destroy(gameObject);
            return;
        }

        // Регистрируем этот луч как активный для данного врага
        if (activeBeams.TryGetValue(targetEnemy, out var existing) && existing != null && existing != this)
        {
            // на всякий случай удаляем предыдущий (если каким-то образом остался)
            Destroy(existing.gameObject);
        }
        activeBeams[targetEnemy] = this;

        RecalculateTickInterval();

        // запускаем корутину урона
        damageRoutine = StartCoroutine(DamageLoop());
    }

    private void Update()
    {
        // Проверка времени жизни
        lifeTimer += Time.deltaTime;
        if (lifeTimer >= maxLifeTime)
        {
            Destroy(gameObject);
            return;
        }

        // если цель или точка вылета потерялись — гасим луч
        if (firePoint == null || targetEnemy == null || targetTransform == null)
        {
            Destroy(gameObject);
            return;
        }

        // если враг выключен/умер — гасим луч
        if (!targetEnemy.gameObject.activeInHierarchy)
        {
            Destroy(gameObject);
            return;
        }

        UpdateBeamTransform();
    }

    private void OnDestroy()
    {
        // снимаем регистрацию этого луча
        if (targetEnemy != null &&
            activeBeams.TryGetValue(targetEnemy, out var beam) &&
            beam == this)
        {
            activeBeams.Remove(targetEnemy);
        }
    }

    // ====== ВИЗУАЛ ЛАЗЕРА ======
    private void UpdateBeamTransform()
    {
        if (firePoint == null || targetTransform == null)
            return;

        Vector3 start = firePoint.position;
        Vector3 end   = targetTransform.position;
        Vector3 dir   = end - start;
        float dist    = dir.magnitude;

        if (dist <= 0.001f)
            return;

        // позиция — в точке вылета
        transform.position = start;
        // поворот — в сторону цели
        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

        if (beamMesh != null)
        {
            beamMesh.localPosition = new Vector3(0f, 0f, dist * 0.5f);

            Vector3 ls = beamMesh.localScale;
            ls.z = dist;
            beamMesh.localScale = ls;
        }
    }

    // ====== ЛОГИКА ТИКОВ УРОНА ======
    private IEnumerator DamageLoop()
    {
        while (true)
        {
            if (targetEnemy == null || !targetEnemy.gameObject.activeInHierarchy)
                break;

            RecalculateTickInterval();

            yield return new WaitForSeconds(currentTickInterval);

            if (targetEnemy == null || !targetEnemy.gameObject.activeInHierarchy)
                break;

            // сохраняем здоровье до нанесения урона
            float hpBefore = targetEnemy.CurrentHealth;

            // наносим урон
            targetEnemy.TakeDamage(damagePerTick);

            // если этот тик добил врага — бафаем игрока (если включено)
            if (increasePlayerMaxHealthOnKill &&
                hpBefore > 0f &&
                targetEnemy.CurrentHealth <= 0f)
            {
                if (cachedPlayerHealth == null)
                    FindPlayerHealth();

                if (cachedPlayerHealth != null)
                {
                    cachedPlayerHealth.IncreaseMaxHealth(maxHealthIncreaseAmount, alsoHeal: true);
                }
            }

            // просто для примера, multiplier можно использовать для доп.логики
            float multiplier = ownerTower != null ? ownerTower.fireRateMultiplier : 1f;
        }

        Destroy(gameObject);
    }

    /// <summary>
    /// N — задержка между тиками.
    /// N = 1 / (fireRate * fireRateMultiplier).
    /// </summary>
    private void RecalculateTickInterval()
    {
        float multiplier = 1f;
        if (ownerTower != null)
            multiplier = Mathf.Max(0.01f, ownerTower.fireRateMultiplier);

        currentTickInterval = 1f / (baseFireRate * multiplier);

        if (currentTickInterval < 0.05f)
            currentTickInterval = 0.05f;
    }
}
