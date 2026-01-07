using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    [Header("Здоровье")]
    public float baseMaxHealth = 100f;
    public float maxHealthMultiplier = 1f;
    public float healthRegenPerSecond = 0f;

    [Header("UI")]
    [SerializeField] private Image healthBarFill;

    [Header("Шипы")]
    [SerializeField] private float spikesBase = 0f;         // базовый урон шипов
    [SerializeField] private float spikesMultiplier = 1f;   // множитель шипов
    [Header("Эффекты при получении урона")]
    [SerializeField] private float healOnHitFromEnemyAmount = 0f;

    private float currentHealth;
    public event Action OnDied;
    private bool isDead = false;

    // === Публичные свойства (для других скриптов) ===

    public float MaxHealth => baseMaxHealth * maxHealthMultiplier;
    public float maxHealth => MaxHealth;   // старое имя, на всякий случай

    public float CurrentHealth => currentHealth;

    public float SpikesDamage => spikesBase * spikesMultiplier;

    private List<ITakeDamageModifier> modifiers = new();


    private void Awake()
    {
        currentHealth = MaxHealth;

        UpdateHealthUI();

        GetComponents(modifiers);
        modifiers.Sort((a, b) => a.Priority.CompareTo(b.Priority));
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

        //HandleShieldRegen();
    }
    public void Heal(float amount)
    {
        if (amount <= 0f) return;

        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        UpdateHealthUI();
    }

    // === УРОН ===

    public void TakeDamage(float damage)
    {
        if (damage <= 0f) return;
        if (isDead) return;

        float remaining = damage;

        // 1) Сначала щит

        foreach (var mod in modifiers)
            remaining = mod.ModifyDamage(remaining);

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

    // === UI ===

    private void UpdateHealthUI()
    {
        if (healthBarFill != null)
        {
            float normalized = MaxHealth > 0f ? currentHealth / MaxHealth : 0f;
            healthBarFill.fillAmount = Mathf.Clamp01(normalized);
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
