using UnityEngine;

public class UpgradesManager : MonoBehaviour
{
    public static UpgradesManager Instance { get; private set; }

    [Header("Runtime Data")]
    public UpgradesRuntimeData RuntimeData = new UpgradesRuntimeData();

    [Header("Ссылки")]
    public PlayerHealth playerHealth;
    public PlayerShield playerShield;
    public TowerAttack towerAttack;
    public UpgradeContextSO context;
    [Header("Прогресс золота")]
    public int goldUpgradeCount = 0;
    [Header("Урон от здоровья игрока")]
    [Tooltip("Дополнительный урон от максимального здоровья игрока (0.1 = 10%)")]
    public float damageFromMaxHealthPercent = 0f;
    [Header("Множители урона по типам оружия")]
    public float[] damageTypeMultipliers = new float[5];   // 5 типов урона
    public int[] damageTypeStacks = new int[5];            // сколько раз брали апгрейд по каждому типу
    [Header("Бонус урона при активном щите")]
    [Tooltip("Суммарный бонус к урону (%) пока щит активен.")]
    public float damageWhileShieldActivePercent = 0f;
    [Header("Шипы — накапливаемые за убийства шипами")]
    [Tooltip("Сколько добавлять к шипам за каждое убийство шипами.")]
    public float spikesOnKillBonus = 0f;

    public float GetDamageTypeMultiplier(WeaponDamageType type)
    {
        int index = (int)type;
        if (damageTypeMultipliers == null || index < 0 || index >= damageTypeMultipliers.Length)
            return 1f;

        return damageTypeMultipliers[index];
    }


    [Header("Хил за убийство")]
    public float healOnKillPerEnemy = 0f;  // сколько ХП лечим за 1 убитого врага\
    [Header("Реген за недостающее здоровье")]
    [Tooltip("Сколько регена в секунду даётся за каждые 100 недостающего HP.")]
    public float regenPer100MissingHealth = 0f;
    [Header("Глобальный урон по всем врагам от регена")]
    [Tooltip("Включить/выключить ауру урона от регена (включится при покупке апгрейда).")]
    public bool regenAuraEnabled = false;

    [Tooltip("Множитель урона от суммарного регена (2 = урон в 2 раза больше регена).")]
    public float regenAuraMultiplier = 0f;

    [Tooltip("Интервал между тиками урона по всем врагам (секунды).")]
    public float regenAuraTickInterval = 1f;

    private float regenAuraTimer = 0f;
    public int goldBonusPerKill = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        InitDamageTypeArrays();
    }

    /// <summary>
    /// Вызывается, когда враг умер именно от урона шипов.
    /// </summary>


    private void InitDamageTypeArrays()
    {
        // сколько значений в enum WeaponDamageType (Magic, Chaos, Slash, Heavy и т.д.)
        int typeCount = System.Enum.GetValues(typeof(WeaponDamageType)).Length;

        // если ещё не создано или неверная длина — создаём заново
        if (damageTypeMultipliers == null || damageTypeMultipliers.Length != typeCount)
        {
            damageTypeMultipliers = new float[typeCount];
            damageTypeStacks = new int[typeCount];
        }

        // базовый множитель = 1 для каждого типа
        for (int i = 0; i < typeCount; i++)
        {
            if (damageTypeMultipliers[i] <= 0f)
                damageTypeMultipliers[i] = 1f;

            // стеки можно оставить как есть, но на всякий случай
            if (damageTypeStacks[i] < 0)
                damageTypeStacks[i] = 0;
        }
    }

    public void ApplyUpgrade(UpgradeBaseSO upgrade)
    {
        if (upgrade == null) return;

        RuntimeData.RegisterUpgradePurchase(upgrade);
        upgrade.Apply(context);
    }

    /// <summary>
    /// Можно ли сейчас купить это улучшение (например, хватает ли здоровья
    /// для "золото за жизнь"). Проверяется магазином до списания цены.
    /// </summary>
    public bool CanApplyUpgrade(UpgradeBaseSO upgrade)
    {
        return upgrade != null && upgrade.CanApply(context);
    }

    public void RegisterWeaponPurchase(WeaponDefinition weapon)
    {
        RuntimeData.RegisterWeaponPurchase(weapon);
    }
}
