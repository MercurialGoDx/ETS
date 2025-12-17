using UnityEngine;

public class UpgradePerTick : MonoBehaviour
{
    public static UpgradePerTick Instance { get; private set; }

    [Header("Ссылки")]
    public PlayerHealth playerHealth;   // повесь сюда башню в инспекторе

    // ===== 1. Урон по времени (как было) =====
    [Header("Урон по времени")]
    [Tooltip("Рост урона за минуту (0.02 = +2%) линейно.")]
    public float damageIncreasePerMinute = 0f;

    private float elapsedTime = 0f;
    private int lastAppliedMinute = 0;

    private float damageMultiplier = 1f;   // Линейный множитель урона
    public float DamageMultiplier => damageMultiplier;

    // ===== 2. ХП по времени =====
    [Header("Макс. здоровье по времени")]
    [Tooltip("Сколько ХП прибавляется за один тик (суммарно со всех апгрейдов).")]
    public float healthPerTickAmount = 0f;

    [Tooltip("Интервал тика ХП в секундах.")]
    public float healthTickInterval = 0f;

    private float healthTickTimer = 0f;

    // ===== 3. Щит по времени =====
    [Header("Макс. щит по времени")]
    [Tooltip("Сколько щита прибавляется за один тик (суммарно со всех апгрейдов).")]
    public float shieldPerTickAmount = 0f;

    [Tooltip("Интервал тика щита в секундах.")]
    public float shieldTickInterval = 0f;

    private float shieldTickTimer = 0f;

    // ===== 4. Регенерация по времени =====
    [Header("Реген хп по времени")]
    [Tooltip("Сколько регена в секунду добавляется каждые interval.")]
    public float regenPerTickAmount = 0f;

    [Tooltip("Интервал тика регена в секундах.")]
    public float regenTickInterval = 0f;

    private float regenTickTimer = 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        elapsedTime += dt;

        // === 1. УРОН ПО МИНУТАМ (как раньше) ===
        if (damageIncreasePerMinute > 0f)
        {
            int currentMinute = Mathf.FloorToInt(elapsedTime / 60f);

            if (currentMinute > lastAppliedMinute)
            {
                damageMultiplier += damageIncreasePerMinute; // линейно
                lastAppliedMinute = currentMinute;

                Debug.Log($"[DamageScaler] Минута {currentMinute}. Новый множитель: {damageMultiplier:F2}");
            }
        }

        // === 2. ХП ПО ВРЕМЕНИ ===
        if (playerHealth != null && healthPerTickAmount > 0f && healthTickInterval > 0f)
        {
            healthTickTimer += dt;
            while (healthTickTimer >= healthTickInterval)
            {
                healthTickTimer -= healthTickInterval;

                playerHealth.IncreaseMaxHealth(healthPerTickAmount, alsoHeal: true);
                Debug.Log($"[HPPerTick] +{healthPerTickAmount} HP. Max = {playerHealth.MaxHealth}, Cur = {playerHealth.CurrentHealth}");
            }
        }

        // === 3. ЩИТ ПО ВРЕМЕНИ ===
        if (playerHealth != null && shieldPerTickAmount > 0f && shieldTickInterval > 0f)
        {
            shieldTickTimer += dt;
            while (shieldTickTimer >= shieldTickInterval)
            {
                shieldTickTimer -= shieldTickInterval;

                playerHealth.AddMaxShield(shieldPerTickAmount);
                Debug.Log($"[ShieldPerTick] +{shieldPerTickAmount} Shield. Max = {playerHealth.MaxShield}, Cur = {playerHealth.CurrentShield}");
            }
        }

        // === 4. РЕГЕН ПО ВРЕМЕНИ ===
        if (playerHealth != null && regenPerTickAmount > 0f && regenTickInterval > 0f)
        {
            regenTickTimer += dt;
            while (regenTickTimer >= regenTickInterval)
            {
                regenTickTimer -= regenTickInterval;

                playerHealth.AddHealthRegen(regenPerTickAmount);
                Debug.Log($"[RegenPerTick] +{regenPerTickAmount} regen. Total regen = {playerHealth.healthRegenPerSecond}");
            }
        }
    }

    // ==== Методы, которые дергают апгрейды ====

    /// <summary>
    /// Добавить апгрейд: +amount к макс. ХП каждые intervalSeconds.
    /// Если вызывается несколько раз – amount суммируется.
    /// </summary>
    public void AddHealthPerTick(float amount, float intervalSeconds)
    {
        if (amount <= 0f || intervalSeconds <= 0f) return;

        healthPerTickAmount += amount;

        // первый апгрейд задаёт интервал; дальше можно брать минимум,
        // чтобы тики происходили не реже самого быстрого апгрейда
        if (healthTickInterval <= 0f)
            healthTickInterval = intervalSeconds;
        else
            healthTickInterval = Mathf.Min(healthTickInterval, intervalSeconds);

        Debug.Log($"[HPPerTick] Активирован. +{healthPerTickAmount} HP каждые {healthTickInterval} сек.");
    }

    /// <summary>
    /// Добавить апгрейд: +amount к макс. щиту каждые intervalSeconds.
    /// </summary>
    public void AddShieldPerTick(float amount, float intervalSeconds)
    {
        if (amount <= 0f || intervalSeconds <= 0f) return;

        shieldPerTickAmount += amount;

        if (shieldTickInterval <= 0f)
            shieldTickInterval = intervalSeconds;
        else
            shieldTickInterval = Mathf.Min(shieldTickInterval, intervalSeconds);

        Debug.Log($"[ShieldPerTick] Активирован. +{shieldPerTickAmount} Shield каждые {shieldTickInterval} сек.");
    }

    /// <summary>
    /// Добавить апгрейд: +amount к регенерации хп каждые intervalSeconds.
    /// </summary>
    public void AddRegenPerTick(float amount, float intervalSeconds)
    {
        if (amount <= 0f || intervalSeconds <= 0f) return;

        regenPerTickAmount += amount;

        if (regenTickInterval <= 0f)
            regenTickInterval = intervalSeconds;
        else
            regenTickInterval = Mathf.Min(regenTickInterval, intervalSeconds);

        Debug.Log($"[RegenPerTick] Активирован. +{regenPerTickAmount} regen каждые {regenTickInterval} сек.");
    }
}
