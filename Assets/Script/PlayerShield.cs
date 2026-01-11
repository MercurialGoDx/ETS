using UnityEngine;
using UnityEngine.UI;

public class PlayerShield : MonoBehaviour, ITakeDamageModifier
{
    [Header("Щит")]
    [SerializeField] private float maxShield = 0f;          // базовый щит (виден в Инспекторе как Max Shield)
    [SerializeField] private float shieldMultiplier = 1f;   // множитель щита (1.0 -> 1.1 -> 1.2 ...)
    [SerializeField] private float shieldRechargeTime = 10f; // время полного восстановления щита
    [SerializeField] private float shieldRechargeDelay = 0f; // оставляем для совместимости, но в логике не используем
    [SerializeField] private float damageWhileShieldActivePercent = 0f; // дополнительный урон, когда щит активен
    [SerializeField] private float shieldRestorePerEnemyKill = 0f; // восстановление щита за убийство врага

    [Header("UI")]
    [SerializeField] private Image shieldBarFill;

    private float currentShield;

    private float shieldRegenTimer = 0f;

    private bool shieldActive = true;

    public float MaxShield => maxShield * shieldMultiplier;
    public float CurrentShield => currentShield;

    public float ShieldRestorePerEnemyKill => shieldRestorePerEnemyKill;
    public float ShieldRechargeTime => shieldRechargeTime;
    public float ShieldRechargeDelay => shieldRechargeDelay;

    public bool IsShieldActive => shieldActive;

    public int Priority => 100;

    private void Awake()
    {
        currentShield = MaxShield;
        shieldActive = MaxShield > 0f;
        shieldRegenTimer = 0f;

        UpdateShieldUI();
    }

    private void Update()
    {
        HandleShieldRegen();
    }

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

    private void UpdateShieldUI()
    {
        if (shieldBarFill != null)
        {
            float normalized = MaxShield > 0f ? currentShield / MaxShield : 0f;
            shieldBarFill.fillAmount = Mathf.Clamp01(normalized);
        }
    }

    public float ModifyDamage(float damage)
    {
        float remaining = damage;

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

            return remaining;
        }
        else
        {
            return damage;
        }
    }

    public void AddDamageWhileShieldActivePercent(float amount)
    {
        damageWhileShieldActivePercent += amount;
    }

    public float GetShieldDamageBonusMultiplier()
    {
        // нет апгрейда — нет бонуса
        if (damageWhileShieldActivePercent <= 0f)
            return 1f;

        // щит не активен → бонус не работает
        if (IsShieldActive)
            return 1f;

        // есть апгрейд и щит активен
        float percent = damageWhileShieldActivePercent / 100f; // 10 → 0.1
        return 1f + percent; // 10% → 1.1, 20% → 1.2 и т.д.
    }

    public void AddShieldRestorePerEnemyKill(float amount)
    {
        shieldRestorePerEnemyKill += amount;
    }

    public void RestoreCurrentShield(float amount)
    {
        currentShield += amount;
    }
}