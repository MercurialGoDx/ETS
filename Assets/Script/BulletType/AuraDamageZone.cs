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

    [Tooltip("Включить подробный лог урона в консоль")]
    public bool debugDamage = false;
    [HideInInspector]
    private DamageCalculator damageCalculator;
    private WeaponDamageType damageType;
    private ItemTier itemTier;

    private bool isInitialized = false;

    private int stacks = 1;          // сколько раз куплено оружие
    private float timer = 0f;

    // Переиспользуемый буфер для OverlapSphereNonAlloc — без аллокаций каждый тик.
    private static readonly Collider[] overlapBuffer = new Collider[64];

    /// <summary>
    /// Инициализация при первом спавне
    /// </summary>
    public void Init(
        float damagePerStackFromWeapon,
        int initialStacks,
        WeaponDamageType type,
        ItemTier tier,
        DamageCalculator calculator
    )
    {
        damagePerStack = damagePerStackFromWeapon;
        stacks = initialStacks;

        damageType = type;
        itemTier = tier;
        damageCalculator = calculator;

        isInitialized = true;
    }

    public void UpdateStacks(int newStacks, float damagePerStackFromWeapon)
    {
        stacks = newStacks;
        damagePerStack = damagePerStackFromWeapon;
    }

    private void Update()
    {
        if (!isInitialized)
            return;

        timer += Time.deltaTime;
        if (timer >= interval)
        {
            timer = 0f;
            DoDamage();
        }
    }

    private void DoDamage()
    {
        float baseDamage = damagePerStack * stacks;

        float finalDamage = damageCalculator.Calculate(new DamageContext
        {
            baseDamage = baseDamage,
            damageType = damageType,
            itemTier = itemTier,
            isSpikes = false
        });

        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, radius, overlapBuffer);

        if (debugDamage)
            Debug.Log($"Enemies amount is {hitCount}");

        for (int i = 0; i < hitCount; i++)
        {
            Enemy enemy = overlapBuffer[i].GetComponent<Enemy>();
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
