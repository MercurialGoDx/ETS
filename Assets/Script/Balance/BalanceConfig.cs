using System;
using UnityEngine;

/// <summary>
/// Снапшот баланса, сгенерированный импортёром из Google Sheets
/// (Tools → Balance → Import). Руками не редактируется — источник правды
/// это таблица; поля здесь соответствуют колонкам вкладок enemy/boss/shop/player_constant.
/// Числа оружий и улучшений сюда не попадают: импортёр стампит их напрямую
/// в WeaponDefinition/UpgradeBaseSO ассеты.
/// </summary>
public class BalanceConfig : ScriptableObject
{
    public const string ResourcesPath = "Balance/BalanceConfig";

    public int version = 1;

    public WavesSection waves = new WavesSection();
    public BossSection boss = new BossSection();
    public ShopSection shop = new ShopSection();
    public PlayerSection player = new PlayerSection();

    [Serializable]
    public class WavesSection
    {
        public float delayPerWave = 10f;         // delay_per_wave
        public int enemyPerWave = 12;            // enemy_per_wave

        // Раздельный рост сложности для HP и урона врагов: у каждого свой базовый
        // % роста за волну (health_difficult_start / damage_difficult_start) и свои
        // множители ускорения по порогам времени (growth_stage*_multiplier_health/damage).
        // Пороги времени (timeDifficultStage*), после которых включается очередной
        // множитель, — общие для HP и урона.
        public float healthDifficultStart = 3.5f;   // health_difficult_start: % роста HP за волну (база)
        public float damageDifficultStart = 3.5f;   // damage_difficult_start: % роста урона за волну (база)
        public float growthStage1MultiplierHealth = 2f;   // growth_stage1_multiplier_health: во сколько раз ускоряется рост HP после timeDifficultStage1
        public float growthStage1MultiplierDamage = 2f;   // growth_stage1_multiplier_damage: во сколько раз ускоряется рост урона после timeDifficultStage1
        public float growthStage2MultiplierHealth = 3f;   // growth_stage2_multiplier_health: после timeDifficultStage2
        public float growthStage2MultiplierDamage = 3f;   // growth_stage2_multiplier_damage: после timeDifficultStage2
        public float growthStage3MultiplierHealth = 3f;   // growth_stage3_multiplier_health: после timeDifficultStage3
        public float growthStage3MultiplierDamage = 3f;   // growth_stage3_multiplier_damage: после timeDifficultStage3
        public float timeDifficultStage1 = 720f;    // time_difficult_stage1 (сек)
        public float timeDifficultStage2 = 1500f;   // time_difficult_stage2 (сек)
        public float timeDifficultStage3 = 2400f;   // time_difficult_stage3 (сек)

        public float hpAddPerWave = 2f;          // hp_add_per_wave: фикс. прибавка HP врагов за волну
        public float damageAddPerWave = 0.05f;   // damage_add_per_wave
    }

    [Serializable]
    public class BossSection
    {
        // Статы босса (hp/damage/gold/speed) стампятся в BossMonster.prefab импортёром.
        public float spawnInterval = 300f;       // spawn_interval (сек)
        public float hpMultiplier = 1.5f;        // hp_multiplier
        public float damageMultiplier = 1f;      // damage_multiplier

        // Плоская прибавка к урону босса, не участвующая в умножении на damageMultiplier
        // и на волновой множитель сложности — формула: additionalDamage + flatDamageBonus
        // + (damage_boss * damageMultiplier * damageDifficultyMultiplier).
        public float additionalDamage = 0f;      // additional_damage_boss
    }

    [Serializable]
    public class ShopSection
    {
        // Цены/эффекты апгрейдов разблокировки стампятся в ShopUnlock_*.asset.
        public int rerollBaseCost = 10;          // reroll_base_cost
        public int rerollCostIncrease = 10;      // reroll_cost_increase
        public float autoRerollInterval = 20f;   // auto_reroll_interval (сек)
        public int startGold = 300;              // start_gold
        public int passiveGoldPerTick = 6;       // passive_gold_per_tick
        public float passiveIncomeInterval = 1f; // passive_income_interval (сек)
    }

    [Serializable]
    public class PlayerSection
    {
        public float playerHp = 200f;                        // player_hp
        public float playerShield = 0f;                      // player_shield
        public float blockCap = 0.8f;                        // block_cap (0..0.95)
        public float shieldRechargeTime = 10f;               // shield_recharge_time (сек)
        public float shieldFullRestoreAfterNoDamage = 30f;   // shield_full_restore_after_no_damage (сек)
        public float towerRange = 20f;                       // tower_range
        public float volleyWindowPercent = 0.5f;             // volley_window_percent (0..1)
        public float gameSpeed1 = 1f;                        // game_speed_1
        public float gameSpeed2 = 2f;                        // game_speed_2
        public float gameSpeed3 = 3f;                        // game_speed_3
    }
}
