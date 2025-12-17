using UnityEngine;

public enum UpgradeType
{
    GlobalFireRate,      // увеличение скорости атаки башни
    MaxHealthFlat,       // + к максимальному здоровью (плоское значение)
    MaxHealthPercent,    // +% к максимальному здоровью
    HealthRegen,
    RegenPerMissingHealth,
    SpikesBase,
    SpikesPercent,
    SpikesScalingOnKill,

    ShieldMax,
    ShieldPercent,
    GoldPerSecond,
    GoldGainPercent,
    AddSpikesAndHealOnHitFromEnemy,
    HealOnKill,
    DamagePerMinuteScaling,
    MaxHealthPerTick,
    RegenPerTick,
    MaxShieldPerTick,
    EnemyEffectChance,
    HpForGold,
    MaxHealthAndDamageFromHealth,
    DamageTypeScaling,
    DamageWhileShieldActive,
    AuraDamagePerRegen


}

[CreateAssetMenu(fileName = "NewUpgrade", menuName = "TD/Upgrade")]
public class UpgradeDefinition : ScriptableObject
{
    [Header("Основное")]
    public string upgradeName;
    public Sprite icon;
    public UpgradeType type;

    [Tooltip("Стоимость покупки в магазине")]
    public int price = 10;

    [Header("Основные значения")]
    public float valueFlat;      // основное число (например, урон шипов)
    public float valuePercent;   // проценты, если нужны

    [Header("Доп. значения для комбинированных апгрейдов")]
    public float extraFlat;      // второе число (например, хил за удар)
    public float extraPercent;   // запас на будущее (если пригодится)

    [Header("Бонус урона по типу")]
    public WeaponDamageType damageType;
    public float damageTypeBasePercent = 0f;          // базовый буст, 30%
    public float damageTypeExtraPerWeaponPercent = 0f; // доп. % за КАЖДОЕ оружие этого типа

    [Header("Описание")]
    [TextArea]
    public string description;

    [Header("Шанс появления")]
    [Tooltip("Вес для выбора в магазине. 1 = низкий, 10 = высокий, и т.д.")]
    public int weight = 1;
    public EnemyEffectDefinition enemyEffect;
}
