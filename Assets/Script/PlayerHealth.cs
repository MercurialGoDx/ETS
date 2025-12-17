using UnityEngine;
using UnityEngine.UI;
using System;

public class PlayerHealth : MonoBehaviour
{
    [Header("Здоровье")]
    public float baseMaxHealth = 100f;
    public float maxHealthMultiplier = 1f;
    public float healthRegenPerSecond = 0f;

    [Header("Щит")]
    [SerializeField] private float maxShield = 0f;          // базовый щит (виден в Инспекторе как Max Shield)
    [SerializeField] private float shieldMultiplier = 1f;   // множитель щита (1.0 -> 1.1 -> 1.2 ...)
    [SerializeField] private float shieldRechargeTime = 10f; // время полного восстановления щита
    [SerializeField] private float shieldRechargeDelay = 0f; // оставляем для совместимости, но в логике не используем

    [Header("UI")]
    [SerializeField] private Image healthBarFill;
    [SerializeField] private Image shieldBarFill;

    [Header("Шипы")]
    [SerializeField] private float spikesBase = 0f;         // базовый урон шипов
    [SerializeField] private float spikesMultiplier = 1f;   // множитель шипов
    [Header("Эффекты при получении урона")]
    [SerializeField] private float healOnHitFromEnemyAmount = 0f;

    private float currentHealth;
    private float currentShield;
    public event Action OnDied;
    private bool isDead = false;

    // таймер восстановления щита (0..shieldRechargeTime)
    private float shieldRegenTimer = 0f;

    // активен ли щит сейчас (если да — принимает урон вместо здоровья)
    private bool shieldActive = true;

    // === Публичные свойства (для других скриптов) ===

    public float MaxHealth => baseMaxHealth * maxHealthMultiplier;
    public float maxHealth => MaxHealth;   // старое имя, на всякий случай

    public float CurrentHealth => currentHealth;

    public float MaxShield => maxShield * shieldMultiplier;
    public float CurrentShield => currentShield;

    public float ShieldRechargeTime => shieldRechargeTime;
    public float ShieldRechargeDelay => shieldRechargeDelay;

    public float SpikesDamage => spikesBase * spikesMultiplier;
    public bool IsShieldActive => shieldActive;


    private void Awake()
    {
        currentHealth = MaxHealth;
        currentShield = MaxShield;
        shieldActive = MaxShield > 0f;
        shieldRegenTimer = 0f;

        UpdateHealthUI();
        UpdateShieldUI();
    }

    private void Update()
    {
        if (isDead) return;

        // === РЕГЕН ЗДОРОВЬЯ ===
        if (currentHealth < MaxHealth)
        {
            float regen = healthRegenPerSecond;

            // Бонусный реген за недостающее здоровье
            if (UpgradesManager.Instance != null &&
                UpgradesManager.Instance.regenPer100MissingHealth > 0f)
            {
                float missing = MaxHealth - currentHealth;
                if (missing > 0f)
                {
                    regen += UpgradesManager.Instance.regenPer100MissingHealth * (missing / 100f);
                }
            }

            if (regen > 0f)
            {
                currentHealth += regen * Time.deltaTime;
                currentHealth = Mathf.Min(currentHealth, MaxHealth);
                UpdateHealthUI();
            }
        }

        HandleShieldRegen();
    }
    public void Heal(float amount)
    {
        if (amount <= 0f) return;

        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        UpdateHealthUI();
    }

    // === УРОН ===

    public void TakeDamage(float amount)
    {
        if (amount <= 0f) return;
        if (isDead) return;

        float remaining = amount;

        // 1) Сначала щит
        if (shieldActive && MaxShield > 0f && currentShield > 0f)
        {
            float shieldUsed = Mathf.Min(remaining, currentShield);
            currentShield -= shieldUsed;
            remaining -= shieldUsed;
            UpdateShieldUI();

            if (currentShield <= 0f)
            {
                currentShield = 0f;
                shieldActive = false;
                shieldRegenTimer = 0f;
            }
        }

        // 2) Потом здоровье
        if (remaining > 0f)
        {
            float previousHealth = currentHealth;

            currentHealth -= remaining;
            if (currentHealth <= 0f)
            {
                currentHealth = 0f;
                UpdateHealthUI();
                Die();
                return;
            }

            UpdateHealthUI();

            // башня реально получила урон по ХП (health уменьшилось) и выжила
            if (healOnHitFromEnemyAmount > 0f && previousHealth > currentHealth)
            {
                Heal(healOnHitFromEnemyAmount);
            }
        }
    }


    // === АПГРЕЙДЫ ЗДОРОВЬЯ ===

    public void AddFlatMaxHealth(float amount)
    {
        baseMaxHealth += amount;
        currentHealth = Mathf.Min(currentHealth, MaxHealth);
        UpdateHealthUI();
    }

    public void AddFlatMaxHealthAndHeal(float amount)
    {
        baseMaxHealth += amount;
        currentHealth = Mathf.Min(currentHealth + amount, MaxHealth);
        UpdateHealthUI();
    }

    // percent ждём как 0.1f для +10%, 0.2f для +20% и т.д.
    public void AddMaxHealthMultiplier(float percent)
    {
        maxHealthMultiplier += percent;
        currentHealth = Mathf.Min(currentHealth, MaxHealth);
        UpdateHealthUI();
    }
    public void AddMaxHealthMultiplierAndHeal(float percent)
    {
        float oldMax = MaxHealth;
        maxHealthMultiplier += percent;
        float newMax = MaxHealth;
        float delta = newMax - oldMax;

        currentHealth = Mathf.Min(currentHealth + delta, newMax);
        UpdateHealthUI();
    }

    public void AddHealthRegen(float amountPerSecond)
    {
        healthRegenPerSecond += amountPerSecond;
    }

    // старые названия для совместимости
    public void IncreaseMaxHealth(float amount)
    {
        AddFlatMaxHealth(amount);
    }
    public void AddHealOnHitFromEnemy(float amount)
    {
        healOnHitFromEnemyAmount += amount;
    }

    public void IncreaseMaxHealth(float amount, bool alsoHeal)
    {
        if (alsoHeal)
            AddFlatMaxHealthAndHeal(amount);
        else
            AddFlatMaxHealth(amount);
    }

    // === АПГРЕЙДЫ ШИПОВ ===

    // Flat
    public void AddSpikesDamage(float amount)
    {
        spikesBase += amount;
    }

    // Percent (amount = 0.1f -> +10%)
    public void AddSpikesPercent(float amount)
    {
        spikesMultiplier += amount;
    }

    // === АПГРЕЙДЫ ЩИТА ===

    // Flat: добавляем к базовому щиту (то, что видно в инспекторе)
    public void AddMaxShield(float amount)
    {
        float oldMax = MaxShield;

        maxShield += amount;

        float newMax = MaxShield;
        float delta = newMax - oldMax; // обычно == amount * shieldMultiplier

        if (oldMax <= 0f)
        {
            // щит появился впервые
            shieldActive = true;
            currentShield = newMax;      // первый раз можно дать полный щит
            shieldRegenTimer = 0f;
        }
        else
        {
            // было => добавляем к текущему, а не фулим
            currentShield = Mathf.Min(currentShield + delta, newMax);

            // если щит был "выключен", оставляем его выключенным — пусть восстановится по твоей логике
            // (ничего не меняем в shieldActive)
        }

        UpdateShieldUI();
    }

    // Percent: увеличиваем множитель щита (amount = 0.1f → +10%)
    public void AddShieldPercent(float amount)
    {
        float oldMax = MaxShield;

        shieldMultiplier += amount;

        float newMax = MaxShield;
        float delta = newMax - oldMax;

        if (oldMax <= 0f)
        {
            shieldActive = true;
            currentShield = newMax; // первый раз — полный
            shieldRegenTimer = 0f;
        }
        else
        {
            currentShield = Mathf.Min(currentShield + delta, newMax);
        }

        UpdateShieldUI();
    }

    public void SetShieldRechargeTime(float newTime)
    {
        shieldRechargeTime = Mathf.Max(0.1f, newTime);
    }

    public void SetShieldRechargeDelay(float newDelay)
    {
        // оставляем просто как хранение значения
        shieldRechargeDelay = Mathf.Max(0f, newDelay);
    }

    // === РЕГЕН ЩИТА ПО НОВОЙ ЛОГИКЕ ===

    private void HandleShieldRegen()
    {
        // если щит как механика не задан — просто убеждаемся, что его нет
        if (MaxShield <= 0f)
        {
            currentShield = 0f;
            shieldActive = false;
            shieldRegenTimer = 0f;
            UpdateShieldUI();
            return;
        }

        // если щит активен — он уже работает, ничего не делаем (кроме ограничения максимума)
        if (shieldActive)
        {
            if (currentShield > MaxShield)
            {
                currentShield = MaxShield;
                UpdateShieldUI();
            }
            return;
        }

        // Здесь щит "в отключке" и восстанавливается.
        // Весь урон в этом состоянии уже идёт по хп (логика в TakeDamage).

        shieldRegenTimer += Time.deltaTime;

        float duration = Mathf.Max(0.01f, shieldRechargeTime);
        float t = Mathf.Clamp01(shieldRegenTimer / duration);

        currentShield = MaxShield * t;
        UpdateShieldUI();

        // Добрали 100% или вышли по времени — снова включаем щит
        if (t >= 1f)
        {
            shieldActive = true;
            currentShield = MaxShield;
            shieldRegenTimer = 0f;
            UpdateShieldUI();
        }
    }

    // === UI ===

    private void UpdateHealthUI()
    {
        if (healthBarFill != null)
        {
            float normalized = MaxHealth > 0f ? currentHealth / MaxHealth : 0f;
            healthBarFill.fillAmount = Mathf.Clamp01(normalized);
        }
    }

    private void UpdateShieldUI()
    {
        if (shieldBarFill != null)
        {
            float normalized = MaxShield > 0f ? currentShield / MaxShield : 0f;
            shieldBarFill.fillAmount = Mathf.Clamp01(normalized);
        }
    }

    // === СМЕРТЬ ===

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log("Player died");
        OnDied?.Invoke();
    }
}
