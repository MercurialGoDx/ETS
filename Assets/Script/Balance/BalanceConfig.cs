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
        public float difficultStart = 3.5f;      // difficult_start: % роста множителя за волну (база)
        public float difficultMid = 5.25f;       // difficult_mid: % после timeDifficultMid
        public float difficultEnd = 10.5f;       // difficult_end: % после timeDifficultEnd
        public float timeDifficultStart = 0f;    // time_difficult_start (сек): база действует с этого времени
        public float timeDifficultMid = 720f;    // time_difficult_mid (сек)
        public float timeDifficultEnd = 1200f;   // time_difficult_end (сек)
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
