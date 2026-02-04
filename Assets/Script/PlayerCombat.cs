using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    public DamageCalculator DamageCalculator { get; private set; }

    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerShield playerShield;
    [SerializeField] private TowerAttack towerAttack;
    [SerializeField] private UpgradeContextSO upgradeContext;

    private UpgradesRuntimeData runtime;



    private void Awake()
    {
        runtime = upgradeContext.runtime;
        DamageCalculator = new DamageCalculator();

        // регистрируем ВСЕ бонусы ОДИН РАЗ
        DamageCalculator.AddBonus(new GlobalDamageBonus(runtime));
        DamageCalculator.AddBonus(new GeneratorDamageBonus(runtime));
        DamageCalculator.AddBonus(new ShieldDamageBonus(runtime, playerShield));
        DamageCalculator.AddBonus(new MaxHpDamageBonus(runtime, playerHealth));
        DamageCalculator.AddBonus(
            new GoldDamageBonus(runtime, () => GoldManager.Instance.currentGold)
        );

        // типы урона
        foreach (WeaponDamageType type in System.Enum.GetValues(typeof(WeaponDamageType)))
        {
            DamageCalculator.AddBonus(
                new DamageTypeBonus(
                    type,
                    () => towerAttack.GetTotalWeaponsOfType(type),
                    runtime
                )
            );
        }

        // тир оружия
        DamageCalculator.AddBonus(new TierDamageBonus(runtime));


        // раздаём ссылку
        towerAttack.Init(DamageCalculator);
        playerHealth.Init(DamageCalculator);
    }

    public UpgradesRuntimeData Runtime => runtime;
}