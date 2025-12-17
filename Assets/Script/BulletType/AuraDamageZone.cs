using UnityEngine;

public class AuraDamageZone : MonoBehaviour
{
    [Header("Урон")]
    [Tooltip("Базовый урон ОТ ОДНОГО оружия за один тик")]
    public float damagePerStack = 100f;

    [Tooltip("Интервал между тиками урона (секунды)")]
    public float interval = 1f;

    [Tooltip("Радиус области урона")]
    public float radius = 3f;

    [Header("VFX (необязательно)")]
    public GameObject impactVfx;

    [Header("Debug")]
    [Tooltip("Имя оружия/ауры для логов")]
    public string auraName = "Aura";

    [Tooltip("Включить подробный лог урона в консоль")]
    public bool debugDamage = false;
    [HideInInspector]
    public WeaponDamageType damageType;

    private int stacks = 1;          // сколько раз куплено оружие
    private float timer = 0f;

    /// <summary>
    /// Инициализация при первом спавне
    /// </summary>
    public void Init(float damagePerStackFromWeapon, int initialStacks = 1, string nameForDebug = "Aura")
    {
        damagePerStack = damagePerStackFromWeapon;
        stacks = initialStacks;
        auraName = nameForDebug;
    }

    /// <summary>
    /// Вызываем, когда игрок покупает это оружие ещё раз
    /// </summary>
    public void UpdateStacks(int newStacks, float damagePerStackFromWeapon)
    {
        stacks = newStacks;
        damagePerStack = damagePerStackFromWeapon;
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= interval)
        {
            timer = 0f;
            DoDamage();
        }
    }

    private void DoDamage()
    {
        // БАЗОВЫЙ урон ауры до всех бонусов
        float baseDamage = damagePerStack * stacks;

        // Финальный урон с учётом:
        // (Base + % от MaxHealth) * глобальный множитель
        float finalDamage = CalculateFinalDamage(baseDamage);

        Collider[] hits = Physics.OverlapSphere(transform.position, radius);

        foreach (Collider col in hits)
        {
            Enemy enemy = col.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.TakeDamage(finalDamage);
            }
        }

        if (impactVfx != null)
        {
            Instantiate(impactVfx, transform.position, Quaternion.identity);
        }
    }

    /// <summary>
    /// Повторяет логику TowerAttack.GetFinalDamage:
    /// (BaseDamage + bonusFromHP) * GlobalMultiplier
    /// </summary>
    private float CalculateFinalDamage(float baseDamage)
    {
    // 1) Множитель по времени (x1, x1.2, x2 ...)
    float timeMult = 1f;
    if (UpgradePerTick.Instance != null)
        timeMult = UpgradePerTick.Instance.DamageMultiplier;

    // 2) Бонус от MaxHealth (у тебя это уже "прибавка к множителю")
    float hpBonusToMult = 0f;
    float maxHp = 0f;
    float dmgFromHpPercent = 0f;

    if (UpgradesManager.Instance != null &&
        UpgradesManager.Instance.damageFromMaxHealthPercent > 0f &&
        UpgradesManager.Instance.playerHealth != null)
    {
        maxHp = UpgradesManager.Instance.playerHealth.MaxHealth;
        dmgFromHpPercent = UpgradesManager.Instance.damageFromMaxHealthPercent;

        hpBonusToMult = (maxHp * dmgFromHpPercent) / 100f;
    }

    // 3) Множитель по типу урона
    float typeMult = 1f;
    if (UpgradesManager.Instance != null)
        typeMult = UpgradesManager.Instance.GetDamageTypeMultiplier(damageType);

    // 4) Множитель от активного щита
    float shieldMult = 1f;
    if (UpgradesManager.Instance != null)
        shieldMult = UpgradesManager.Instance.GetShieldDamageBonusMultiplier();

    // ===== НОВАЯ ЛОГИКА: складываем бонусы =====
    float timeBonus = timeMult - 1f;
    float typeBonus = typeMult - 1f;
    float shieldBonus = shieldMult - 1f;

    float totalBonus = timeBonus + hpBonusToMult + typeBonus + shieldBonus;

    float finalMult = Mathf.Max(0f, 1f + totalBonus);
    float finalDamage = baseDamage * finalMult;

    if (debugDamage)
    {
        Debug.Log(
            $"[AuraDamage] {auraName} ({damageType}) => Base={baseDamage:F2} | " +
            $"TimeMult={timeMult:F2} (bonus {timeBonus:F2}) | " +
            $"MaxHP={maxHp:F0} | HP%={dmgFromHpPercent:F1}% | HpBonus={hpBonusToMult:F2} | " +
            $"TypeMult={typeMult:F2} (bonus {typeBonus:F2}) | " +
            $"ShieldMult={shieldMult:F2} (bonus {shieldBonus:F2}) | " +
            $"TotalBonus={totalBonus:F2} | TotalMult={finalMult:F2} | Final={finalDamage:F2}"
        );
    }

    return finalDamage;
}

    public void Init(
    float damagePerStackFromWeapon,
    int initialStacks,
    string nameForDebug,
    WeaponDamageType type)
    {
        damagePerStack = damagePerStackFromWeapon;
        stacks = initialStacks;
        auraName = nameForDebug;
        damageType = type;
    }


    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
