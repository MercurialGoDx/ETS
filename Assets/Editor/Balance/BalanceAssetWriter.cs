using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ETS.BalanceImport
{
    /// <summary>
    /// Запись провалидированных данных в ассеты. Только через SerializedObject:
    /// Unity сама корректно сериализует дефолты и упакованные списки, чего не
    /// гарантирует текстовая правка YAML.
    /// Вызывается ТОЛЬКО после валидации без ошибок.
    /// </summary>
    public static class BalanceAssetWriter
    {
        public static int StampWeapons(SheetTable t)
        {
            int count = 0;

            foreach (var row in t.Rows)
            {
                var asset = BalanceAssetIndex.FindWeapon(Cell.Text(row, "asset_name"));
                if (asset == null) continue; // валидатор гарантирует, что таких нет

                var so = new SerializedObject(asset);
                so.FindProperty("price").intValue = ParseInt(row["cost"]);
                so.FindProperty("damagePerProjectile").floatValue = ParseFloat(row["damage"]);
                so.FindProperty("fireRate").floatValue = ParseFloat(row["attack_speed"]);
                so.FindProperty("projectileSpeed").floatValue = ParseFloat(row["projectile_speed"]);
                so.FindProperty("itemTier").intValue = ParseInt(row["tier"]);
                so.FindProperty("weight").intValue = ParseInt(row["weight"]);
                so.FindProperty("weightIncreasePerPurchase").intValue = ParseInt(row["weight_increase_per_purchase"]);
                so.FindProperty("maxWeightIncreasePurchases").intValue = ParseInt(row["max_weight_increase_purchases"]);
                so.FindProperty("damageType").intValue = (int)Enum.Parse(typeof(WeaponDamageType), row["damage_type"]);
                so.ApplyModifiedPropertiesWithoutUndo();
                count++;
            }

            return count;
        }

        public static int StampUpgrades(SheetTable t, List<Issue> issues)
        {
            int count = 0;

            foreach (var row in t.Rows)
            {
                string assetName = Cell.Text(row, "asset_name");
                var asset = BalanceAssetIndex.FindUpgrade(assetName);
                if (asset == null) continue;

                var so = new SerializedObject(asset);
                so.FindProperty("price").intValue = ParseInt(row["cost"]);
                so.FindProperty("weight").intValue = ParseInt(row["weight"]);
                so.FindProperty("itemTier").intValue = ParseInt(row["tier"]);
                so.FindProperty("weightIncreasePerPurchase").intValue = ParseInt(row["weight_increase_per_purchase"]);
                so.FindProperty("maxWeightIncreasePurchases").intValue = ParseInt(row["max_weight_increase_purchases"]);
                so.ApplyModifiedPropertiesWithoutUndo();

                foreach (var b in UpgradeValueMap.Get(assetName))
                {
                    float sheetValue = ParseFloat(row[b.Column]) * b.Scale;

                    UnityEngine.Object target = asset;
                    if (b.ChildType != null)
                    {
                        target = FindCompositeChild(asset, b.ChildType);
                        if (target == null)
                        {
                            issues.Add(Issue.Error("upgrades",
                                $"'{assetName}': в composite не найден дочерний ассет типа {b.ChildType} — значение {b.Column} не записано"));
                            continue;
                        }
                    }

                    var targetSo = new SerializedObject(target);
                    var prop = targetSo.FindProperty(b.Field);
                    if (prop == null)
                    {
                        issues.Add(Issue.Error("upgrades",
                            $"'{assetName}': у {target.name} нет поля {b.Field} — значение {b.Column} не записано"));
                        continue;
                    }

                    if (prop.propertyType == SerializedPropertyType.Integer)
                        prop.intValue = (int)Math.Round(sheetValue);
                    else
                        prop.floatValue = sheetValue;

                    targetSo.ApplyModifiedPropertiesWithoutUndo();
                }

                count++;
            }

            return count;
        }

        internal static UnityEngine.Object FindCompositeChild(UpgradeBaseSO parent, string childTypeName)
        {
            if (parent is not CompositeUpgrade composite || composite.upgrades == null)
                return null;

            foreach (var child in composite.upgrades)
                if (child != null && child.GetType().Name == childTypeName)
                    return child;

            return null;
        }

        // ---------------- враги и босс ----------------

        internal static readonly (string prefab, string dmgCol, string hpCol, string speedCol)[] EnemyPrefabs =
        {
            ("Assets/Prefab/Enemy/MeleeEnemy.prefab", "damage_melee", "hp_melee", "speed_melee"),
            ("Assets/Prefab/Enemy/MidrangeEnemy.prefab", "damage_mid", "hp_mid", "speed_mid"),
            ("Assets/Prefab/Enemy/RangeEnemy.prefab", "damage_range", "hp_range", "speed_range"),
        };

        public const string BossPrefabPath = "Assets/Prefab/Enemy/Boss/BossMonster.prefab";

        public static int StampEnemies(SheetTable t, List<Issue> issues)
        {
            var row = t.Rows[0];
            int count = 0;

            foreach (var (path, dmgCol, hpCol, speedCol) in EnemyPrefabs)
            {
                if (!StampEnemyPrefab(path, ParseFloat(row[dmgCol]), ParseFloat(row[hpCol]),
                        ParseInt(row["gold_for_enemy"]), ParseFloat(row[speedCol]),
                        ParseFloat(row["attack_interval"]), null, issues))
                    continue;
                count++;
            }

            return count;
        }

        public static bool StampBoss(SheetTable t, List<Issue> issues)
        {
            var row = t.Rows[0];
            return StampEnemyPrefab(BossPrefabPath, ParseFloat(row["damage_boss"]), ParseFloat(row["hp_boss"]),
                ParseInt(row["gold_for_boss"]), ParseFloat(row["speed_boss"]),
                ParseFloat(row["attack_interval"]), ParseFloat(row["attack_range"]), issues);
        }

        private static bool StampEnemyPrefab(string path, float damage, float hp, int gold, float speed,
            float attackInterval, float? attackRange, List<Issue> issues)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                issues.Add(Issue.Error("enemy", $"префаб не найден: {path}"));
                return false;
            }

            var enemy = prefab.GetComponent<Enemy>();
            if (enemy == null)
            {
                issues.Add(Issue.Error("enemy", $"{path}: нет компонента Enemy"));
                return false;
            }

            var so = new SerializedObject(enemy);
            so.FindProperty("damageToPlayer").floatValue = damage;
            so.FindProperty("maxHealth").floatValue = hp;
            so.FindProperty("baseGold").intValue = gold;
            so.FindProperty("speed").floatValue = speed;
            so.FindProperty("attackInterval").floatValue = attackInterval;
            if (attackRange.HasValue)
                so.FindProperty("attackRange").floatValue = attackRange.Value;
            so.ApplyModifiedPropertiesWithoutUndo();

            return true;
        }

        // ---------------- ShopUnlock ----------------

        public const string ShopUnlockDir = "Assets/Prefab/Upgrade/ShopUnlock";

        public static void StampShopUnlocks(SheetTable t, List<Issue> issues)
        {
            var row = t.Rows[0];

            StampUnlockUpgrade($"{ShopUnlockDir}/ShopUnlock_1.asset",
                ParseInt(row["first_upgrade_cost"]),
                ParseInt(row["first_upgrade_unlocks_column"]),
                ParseInt(row["first_upgrade_unlocks_tier"]), issues);

            StampUnlockUpgrade($"{ShopUnlockDir}/ShopUnlock_2.asset",
                ParseInt(row["second_upgrade_cost"]),
                ParseInt(row["second_upgrade_unlocks_column"]),
                ParseInt(row["second_upgrade_unlocks_tier"]), issues);

            // Стартовое состояние магазина
            var config = AssetDatabase.LoadAssetAtPath<ShopUnlockConfigSO>($"{ShopUnlockDir}/ShopUnlockConfig.asset");
            if (config == null)
            {
                issues.Add(Issue.Error("shop", $"не найден {ShopUnlockDir}/ShopUnlockConfig.asset"));
                return;
            }

            var dummy = new List<Issue>(); // уже провалидировано
            var columns = BalanceValidator.ParseIntList(Cell.Text(row, "start_unlocked_columns"), "shop", "start_unlocked_columns", dummy);
            var tiers = BalanceValidator.ParseIntList(Cell.Text(row, "start_unlocked_tiers"), "shop", "start_unlocked_tiers", dummy);

            var so = new SerializedObject(config);
            WriteIntList(so.FindProperty("startingUnlockedColumns"), columns);
            WriteIntList(so.FindProperty("startingUnlockedTiers"), tiers);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void StampUnlockUpgrade(string path, int price, int column, int tier, List<Issue> issues)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ShopUnlockUpgradeSO>(path);
            if (asset == null)
            {
                issues.Add(Issue.Error("shop", $"не найден {path}"));
                return;
            }

            var so = new SerializedObject(asset);
            so.FindProperty("price").intValue = price;
            WriteIntList(so.FindProperty("unlockedColumns"), new List<int> { column });
            so.FindProperty("unlockedTier").intValue = tier;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WriteIntList(SerializedProperty listProp, List<int> values)
        {
            listProp.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                listProp.GetArrayElementAtIndex(i).intValue = values[i];
        }

        // ---------------- BalanceConfig ----------------

        public const string BalanceConfigPath = "Assets/Resources/Balance/BalanceConfig.asset";

        public static BalanceConfig WriteBalanceConfig(BalanceSheets sheets)
        {
            var config = AssetDatabase.LoadAssetAtPath<BalanceConfig>(BalanceConfigPath);
            if (config == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(BalanceConfigPath)!);
                config = ScriptableObject.CreateInstance<BalanceConfig>();
                AssetDatabase.CreateAsset(config, BalanceConfigPath);
            }

            var enemy = sheets.Enemy.Rows[0];
            var boss = sheets.Boss.Rows[0];
            var shop = sheets.Shop.Rows[0];
            var player = sheets.Player.Rows[0];

            config.waves.delayPerWave = ParseFloat(enemy["delay_per_wave"]);
            config.waves.enemyPerWave = ParseInt(enemy["enemy_per_wave"]);
            config.waves.difficultStart = ParseFloat(enemy["difficult_start"]);
            config.waves.difficultMid = ParseFloat(enemy["difficult_mid"]);
            config.waves.difficultEnd = ParseFloat(enemy["difficult_end"]);
            config.waves.timeDifficultStart = ParseFloat(enemy["time_difficult_start"]);
            config.waves.timeDifficultMid = ParseFloat(enemy["time_difficult_mid"]);
            config.waves.timeDifficultEnd = ParseFloat(enemy["time_difficult_end"]);
            config.waves.hpAddPerWave = ParseFloat(enemy["hp_add_per_wave"]);
            config.waves.damageAddPerWave = ParseFloat(enemy["damage_add_per_wave"]);

            config.boss.spawnInterval = ParseFloat(boss["spawn_interval"]);
            config.boss.hpMultiplier = ParseFloat(boss["hp_multiplier"]);
            config.boss.damageMultiplier = ParseFloat(boss["damage_multiplier"]);

            config.shop.rerollBaseCost = ParseInt(shop["reroll_base_cost"]);
            config.shop.rerollCostIncrease = ParseInt(shop["reroll_cost_increase"]);
            config.shop.autoRerollInterval = ParseFloat(shop["auto_reroll_interval"]);
            config.shop.startGold = ParseInt(shop["start_gold"]);
            config.shop.passiveGoldPerTick = ParseInt(shop["passive_gold_per_tick"]);
            config.shop.passiveIncomeInterval = ParseFloat(shop["passive_income_interval"]);

            config.player.playerHp = ParseFloat(player["player_hp"]);
            config.player.playerShield = ParseFloat(player["player_shield"]);
            config.player.blockCap = ParseFloat(player["block_cap"]);
            config.player.shieldRechargeTime = ParseFloat(player["shield_recharge_time"]);
            config.player.shieldFullRestoreAfterNoDamage = ParseFloat(player["shield_full_restore_after_no_damage"]);
            config.player.towerRange = ParseFloat(player["tower_range"]);
            config.player.volleyWindowPercent = ParseFloat(player["volley_window_percent"]);
            config.player.gameSpeed1 = ParseFloat(player["game_speed_1"]);
            config.player.gameSpeed2 = ParseFloat(player["game_speed_2"]);
            config.player.gameSpeed3 = ParseFloat(player["game_speed_3"]);

            EditorUtility.SetDirty(config);
            return config;
        }

        // ---------------- helpers ----------------

        private static float ParseFloat(string s) =>
            float.Parse(s.Replace(',', '.'), CultureInfo.InvariantCulture);

        private static int ParseInt(string s) =>
            (int)Math.Round(ParseFloat(s));
    }
}
