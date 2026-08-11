using UnityEngine;

[DefaultExecutionOrder(-100)]
public class GameInstaller : MonoBehaviour
{
    [SerializeField] private UpgradeContextSO upgradeContext;

    private void Awake()
    {
        upgradeContext.playerHealth = FindFirstObjectByType<PlayerHealth>();
        upgradeContext.playerShield = FindFirstObjectByType<PlayerShield>();
        upgradeContext.towerAttack = FindFirstObjectByType<TowerAttack>();
        upgradeContext.goldManager = FindFirstObjectByType<GoldManager>();
        upgradeContext.regenAuraDamage = FindFirstObjectByType<RegenAuraDamage>();
        upgradeContext.runtime = new UpgradesRuntimeData();
        upgradeContext.runtime.enemySpawner = FindFirstObjectByType<EnemySpawner>();

        // Собираем DamageCalculator и раздаём его потребителям.
        // Бонусы читают upgradeContext.runtime — тот же экземпляр, в который пишут
        // апгрейды через Apply(context), иначе купленные бонусы не влияли бы на урон.
        var calculator = new DamageCalculator();
        var runtime = upgradeContext.runtime;
        var health = upgradeContext.playerHealth;
        var shield = upgradeContext.playerShield;
        var tower = upgradeContext.towerAttack;
        var gold = upgradeContext.goldManager;

        calculator.AddBonus(new GlobalDamageBonus(runtime));
        calculator.AddBonus(new TierDamageBonus(runtime));
        calculator.AddBonus(new GeneratorDamageBonus(runtime));

        if (health != null)
            calculator.AddBonus(new MaxHpDamageBonus(runtime, health));

        if (shield != null)
            calculator.AddBonus(new ShieldDamageBonus(runtime, shield));

        if (gold != null)
            calculator.AddBonus(new GoldDamageBonus(runtime, () => gold.currentGold));

        if (tower != null)
        {
            // По одному бонусу на каждый тип урона: "+X% типу" и "+Y% за каждое оружие типа".
            foreach (WeaponDamageType type in System.Enum.GetValues(typeof(WeaponDamageType)))
            {
                WeaponDamageType captured = type; // нельзя замыкать переменную цикла
                calculator.AddBonus(new DamageTypeBonus(
                    captured,
                    () => tower.GetTotalWeaponsOfType(captured),
                    runtime));
            }

            tower.Init(calculator);
        }

        if (health != null)
            health.Init(calculator);
    }
}
