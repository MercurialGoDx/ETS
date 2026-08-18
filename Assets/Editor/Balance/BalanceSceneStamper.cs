using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ETS.BalanceImport
{
    /// <summary>
    /// Часть баланса живёт не на ассетах, а на компонентах прямо на сцене
    /// (EnemySpawner, BossManager, ShopManager, GoldManager, PlayerHealth,
    /// PlayerShield, TowerAttack, GameSpeedController) — их Start()/Awake()
    /// перекрывает инспекторные значения конфигом, но только в Play-режиме.
    /// В Editor-режиме инспектор после импорта оставался старым до следующего
    /// запуска игры. Здесь те же значения проставляются прямо в открытую сцену
    /// через SerializedObject — работает, только если нужная сцена сейчас
    /// открыта в редакторе (компонент физически должен быть в загруженной сцене).
    /// </summary>
    public static class BalanceSceneStamper
    {
        public static void StampSceneObjects(BalanceConfig config, List<Issue> issues)
        {
            bool anyChanged = false;

            anyChanged |= StampEnemySpawner(config, issues);
            anyChanged |= StampBossManager(config, issues);
            anyChanged |= StampShopManager(config, issues);
            anyChanged |= StampGoldManager(config, issues);
            anyChanged |= StampPlayerHealth(config, issues);
            anyChanged |= StampPlayerShield(config, issues);
            anyChanged |= StampTowerAttack(config, issues);
            anyChanged |= StampGameSpeedController(config, issues);

            if (!anyChanged)
                return;

            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                    EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        private static T Find<T>() where T : Object =>
            Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

        private static bool StampEnemySpawner(BalanceConfig config, List<Issue> issues)
        {
            var comp = Find<EnemySpawner>();
            if (comp == null)
            {
                issues.Add(Issue.Warning("scene", "EnemySpawner не найден на открытой сцене — открой сцену с игрой, чтобы значения попали в инспектор."));
                return false;
            }

            var so = new SerializedObject(comp);
            so.FindProperty("timeBetweenWaves").floatValue = config.waves.delayPerWave;
            so.FindProperty("enemiesPerWave").intValue = config.waves.enemyPerWave;
            so.FindProperty("healthAddPerWave").floatValue = config.waves.hpAddPerWave;
            so.FindProperty("damageAddPerWave").floatValue = config.waves.damageAddPerWave;
            so.FindProperty("healthGrowthPercent").floatValue = config.waves.healthDifficultStart;
            so.FindProperty("damageGrowthPercent").floatValue = config.waves.damageDifficultStart;
            so.FindProperty("growthStage1MultiplierHealth").floatValue = config.waves.growthStage1MultiplierHealth;
            so.FindProperty("growthStage1MultiplierDamage").floatValue = config.waves.growthStage1MultiplierDamage;
            so.FindProperty("growthStage2MultiplierHealth").floatValue = config.waves.growthStage2MultiplierHealth;
            so.FindProperty("growthStage2MultiplierDamage").floatValue = config.waves.growthStage2MultiplierDamage;
            so.FindProperty("growthStage3MultiplierHealth").floatValue = config.waves.growthStage3MultiplierHealth;
            so.FindProperty("growthStage3MultiplierDamage").floatValue = config.waves.growthStage3MultiplierDamage;
            so.FindProperty("timeMark1Minutes").floatValue = config.waves.timeDifficultStage1 / 60f;
            so.FindProperty("timeMark2Minutes").floatValue = config.waves.timeDifficultStage2 / 60f;
            so.FindProperty("timeMark3Minutes").floatValue = config.waves.timeDifficultStage3 / 60f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool StampBossManager(BalanceConfig config, List<Issue> issues)
        {
            var comp = Find<BossManager>();
            if (comp == null)
            {
                issues.Add(Issue.Warning("scene", "BossManager не найден на открытой сцене — открой сцену с игрой, чтобы значения попали в инспектор."));
                return false;
            }

            var so = new SerializedObject(comp);
            so.FindProperty("spawnEverySeconds").floatValue = config.boss.spawnInterval;
            so.FindProperty("bossHpMultiplier").floatValue = config.boss.hpMultiplier;
            so.FindProperty("bossDamageMultiplier").floatValue = config.boss.damageMultiplier;
            so.FindProperty("bossAdditionalDamage").floatValue = config.boss.additionalDamage;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool StampShopManager(BalanceConfig config, List<Issue> issues)
        {
            var comp = Find<ShopManager>();
            if (comp == null)
            {
                issues.Add(Issue.Warning("scene", "ShopManager не найден на открытой сцене — открой сцену с игрой, чтобы значения попали в инспектор."));
                return false;
            }

            var so = new SerializedObject(comp);
            so.FindProperty("rerollPrice").intValue = config.shop.rerollBaseCost;
            so.FindProperty("rerollPriceIncrease").intValue = config.shop.rerollCostIncrease;
            so.FindProperty("autoRerollInterval").floatValue = config.shop.autoRerollInterval;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool StampGoldManager(BalanceConfig config, List<Issue> issues)
        {
            var comp = Find<GoldManager>();
            if (comp == null)
            {
                issues.Add(Issue.Warning("scene", "GoldManager не найден на открытой сцене — открой сцену с игрой, чтобы значения попали в инспектор."));
                return false;
            }

            var so = new SerializedObject(comp);
            so.FindProperty("startGold").intValue = config.shop.startGold;
            so.FindProperty("goldPerTick").floatValue = config.shop.passiveGoldPerTick;
            so.FindProperty("incomeInterval").floatValue = config.shop.passiveIncomeInterval;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool StampPlayerHealth(BalanceConfig config, List<Issue> issues)
        {
            var comp = Find<PlayerHealth>();
            if (comp == null)
            {
                issues.Add(Issue.Warning("scene", "PlayerHealth не найден на открытой сцене — открой сцену с игрой, чтобы значения попали в инспектор."));
                return false;
            }

            var so = new SerializedObject(comp);
            so.FindProperty("baseMaxHealth").floatValue = config.player.playerHp;
            so.FindProperty("blockCap").floatValue = config.player.blockCap;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool StampPlayerShield(BalanceConfig config, List<Issue> issues)
        {
            var comp = Find<PlayerShield>();
            if (comp == null)
            {
                issues.Add(Issue.Warning("scene", "PlayerShield не найден на открытой сцене — открой сцену с игрой, чтобы значения попали в инспектор."));
                return false;
            }

            var so = new SerializedObject(comp);
            so.FindProperty("maxShield").floatValue = config.player.playerShield;
            so.FindProperty("shieldRechargeTime").floatValue = config.player.shieldRechargeTime;
            so.FindProperty("fullRestoreAfterNoShieldDamageSeconds").floatValue = config.player.shieldFullRestoreAfterNoDamage;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool StampTowerAttack(BalanceConfig config, List<Issue> issues)
        {
            var comp = Find<TowerAttack>();
            if (comp == null)
            {
                issues.Add(Issue.Warning("scene", "TowerAttack не найден на открытой сцене — открой сцену с игрой, чтобы значения попали в инспектор."));
                return false;
            }

            var so = new SerializedObject(comp);
            so.FindProperty("range").floatValue = config.player.towerRange;
            so.FindProperty("volleyWindowPercent").floatValue = config.player.volleyWindowPercent;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool StampGameSpeedController(BalanceConfig config, List<Issue> issues)
        {
            var comp = Find<GameSpeedController>();
            if (comp == null)
            {
                issues.Add(Issue.Warning("scene", "GameSpeedController не найден на открытой сцене — открой сцену с игрой, чтобы значения попали в инспектор."));
                return false;
            }

            var so = new SerializedObject(comp);
            so.FindProperty("speedX1").floatValue = config.player.gameSpeed1;
            so.FindProperty("speedX2").floatValue = config.player.gameSpeed2;
            so.FindProperty("speedX3").floatValue = config.player.gameSpeed3;
            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
    }
}
