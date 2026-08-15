using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace ETS.BalanceImport
{
    /// <summary>
    /// Валидация всех вкладок. Копит ВСЕ проблемы, не останавливаясь на первой.
    /// Импортёр пишет что-либо только при нуле ошибок; warnings не блокируют.
    /// </summary>
    public static class BalanceValidator
    {
        public const int MaxTier = 4; // ItemTier: Tier1..Tier4

        private static readonly string[] WeaponCols = {
            "name", "id", "attack_speed", "damage", "tier", "weight", "cost",
            "damage_type", "projectile_speed", "weight_increase_per_purchase",
            "max_weight_increase_purchases", "asset_name" };

        private static readonly string[] EnemyCols = {
            "damage_melee", "hp_melee", "damage_mid", "hp_mid", "damage_range", "hp_range",
            "gold_for_enemy", "delay_per_wave", "enemy_per_wave",
            "difficult_start", "difficult_mid", "difficult_end",
            "time_difficult_start", "time_difficult_mid", "time_difficult_end",
            "speed_melee", "speed_mid", "speed_range", "attack_interval",
            "hp_add_per_wave", "damage_add_per_wave" };

        private static readonly string[] BossCols = {
            "damage_boss", "hp_boss", "gold_for_boss", "speed_boss",
            "attack_interval", "attack_range", "spawn_interval", "hp_multiplier", "damage_multiplier" };

        private static readonly string[] ShopCols = {
            "first_upgrade_cost", "second_upgrade_cost", "reroll_base_cost", "reroll_cost_increase",
            "auto_reroll_interval", "start_gold", "passive_gold_per_tick", "passive_income_interval",
            "start_unlocked_columns", "start_unlocked_tiers",
            "first_upgrade_unlocks_column", "first_upgrade_unlocks_tier",
            "second_upgrade_unlocks_column", "second_upgrade_unlocks_tier" };

        private static readonly string[] PlayerCols = {
            "player_hp", "player_shield", "block_cap", "shield_recharge_time",
            "shield_full_restore_after_no_damage", "tower_range", "volley_window_percent",
            "game_speed_1", "game_speed_2", "game_speed_3" };

        private static readonly string[] UpgradeCols = {
            "name", "id", "tier", "weight", "cost",
            "effect_1", "value_1", "effect_2", "value_2", "interval",
            "weight_increase_per_purchase", "max_weight_increase_purchases", "asset_name" };

        /// <summary>
        /// Обязательные колонки вкладки. Нужны не только валидатору: по ним
        /// скачивание опознаёт, ту ли вкладку вернул Google (см. BalanceImporter),
        /// и по ним же строится шапка при экспорте.
        /// </summary>
        public static string[] RequiredColumns(string tab) => tab switch
        {
            "weapon" => WeaponCols,
            "enemy" => EnemyCols,
            "boss" => BossCols,
            "shop" => ShopCols,
            "player_constant" => PlayerCols,
            "upgrades" => UpgradeCols,
            _ => Array.Empty<string>(),
        };

        public static List<Issue> Validate(BalanceSheets sheets)
        {
            var issues = new List<Issue>();

            CheckColumns(sheets.Weapon, WeaponCols, issues);
            CheckColumns(sheets.Enemy, EnemyCols, issues);
            CheckColumns(sheets.Boss, BossCols, issues);
            CheckColumns(sheets.Shop, ShopCols, issues);
            CheckColumns(sheets.Player, PlayerCols, issues);
            CheckColumns(sheets.Upgrades, UpgradeCols, issues);

            if (sheets.Weapon != null) ValidateWeapons(sheets.Weapon, issues);
            if (sheets.Enemy != null) ValidateEnemy(sheets.Enemy, issues);
            if (sheets.Boss != null) ValidateBoss(sheets.Boss, issues);
            if (sheets.Shop != null) ValidateShop(sheets.Shop, issues);
            if (sheets.Player != null) ValidatePlayer(sheets.Player, issues);
            if (sheets.Upgrades != null) ValidateUpgrades(sheets.Upgrades, issues);

            return issues;
        }

        private static void CheckColumns(SheetTable table, string[] required, List<Issue> issues)
        {
            if (table == null) return; // отсутствие файла уже зарепорчено при загрузке

            foreach (var col in required)
                if (!table.HasColumn(col))
                    issues.Add(Issue.Error(table.Name, $"нет обязательной колонки '{col}'"));

            foreach (var col in table.Columns)
                if (!string.IsNullOrWhiteSpace(col) && Array.IndexOf(required, col) < 0)
                    issues.Add(Issue.Warning(table.Name, $"неизвестная колонка '{col}' — опечатка? Импортёр её игнорирует"));
        }

        // ---------------- weapon ----------------

        private static void ValidateWeapons(SheetTable t, List<Issue> issues)
        {
            if (t.Rows.Count == 0)
            {
                issues.Add(Issue.Error(t.Name, "вкладка пуста"));
                return;
            }

            var seenIds = new HashSet<int>();
            var seenAssets = new HashSet<string>();
            var sheetAssetNames = new HashSet<string>();

            for (int i = 0; i < t.Rows.Count; i++)
            {
                var row = t.Rows[i];
                string where = $"{t.Name}!строка {i + 2}";

                string assetName = Cell.Text(row, "asset_name");
                if (string.IsNullOrEmpty(assetName))
                    issues.Add(Issue.Error(where, "пустой asset_name — не к чему привязать строку"));
                else
                {
                    if (!seenAssets.Add(assetName))
                        issues.Add(Issue.Error(where, $"дубль asset_name '{assetName}'"));
                    sheetAssetNames.Add(assetName);

                    if (BalanceAssetIndex.FindWeapon(assetName) == null)
                        issues.Add(Issue.Error(where, $"WeaponDefinition-ассет '{assetName}' не найден в проекте"));
                }

                if (string.IsNullOrEmpty(Cell.Text(row, "name")))
                    issues.Add(Issue.Warning(where, "пустое name (не блокирует, но дизайнеру будет неудобно)"));

                int id = Cell.Int(row, "id", where, issues);
                if (id <= 0) issues.Add(Issue.Error(where, $"id = {id}, ожидается целое > 0"));
                else if (!seenIds.Add(id)) issues.Add(Issue.Error(where, $"дубль id {id}"));

                float atkSpeed = Cell.Float(row, "attack_speed", where, issues);
                if (atkSpeed <= 0f) issues.Add(Issue.Error(where, $"attack_speed = {atkSpeed}, ожидается > 0"));

                float damage = Cell.Float(row, "damage", where, issues);
                if (damage < 0f) issues.Add(Issue.Error(where, $"damage = {damage}, ожидается >= 0"));
                else if (damage == 0f && assetName != "Catapult")
                    issues.Add(Issue.Warning(where, "damage = 0 — оружие не будет наносить урона (исключение в проекте только Catapult: его урон — % HP цели на префабе снаряда)"));

                int tier = Cell.Int(row, "tier", where, issues);
                if (tier < 1 || tier > MaxTier) issues.Add(Issue.Error(where, $"tier = {tier}, ожидается 1..{MaxTier}"));

                float weight = Cell.Float(row, "weight", where, issues);
                if (weight <= 0f) issues.Add(Issue.Error(where, $"weight = {weight}, ожидается > 0 (иначе предмет никогда не выпадет)"));

                int cost = Cell.Int(row, "cost", where, issues);
                if (cost < 0) issues.Add(Issue.Error(where, $"cost = {cost}, ожидается >= 0"));

                string dtype = Cell.Text(row, "damage_type");
                if (!Enum.TryParse<WeaponDamageType>(dtype, out _))
                    issues.Add(Issue.Error(where, $"damage_type = '{dtype}', допустимо: {string.Join(", ", Enum.GetNames(typeof(WeaponDamageType)))}"));

                float projSpeed = Cell.Float(row, "projectile_speed", where, issues);
                if (projSpeed <= 0f) issues.Add(Issue.Error(where, $"projectile_speed = {projSpeed}, ожидается > 0"));

                Cell.Int(row, "weight_increase_per_purchase", where, issues); // может быть отрицательным — это легально
                int maxInc = Cell.Int(row, "max_weight_increase_purchases", where, issues);
                if (maxInc < 0) issues.Add(Issue.Error(where, $"max_weight_increase_purchases = {maxInc}, ожидается >= 0"));
            }

            // Ассеты, забытые в таблице: иначе предмет молча останется на старых числах.
            foreach (var name in BalanceAssetIndex.AllWeaponNames())
                if (!sheetAssetNames.Contains(name))
                    issues.Add(Issue.Warning(t.Name, $"в проекте есть оружие '{name}', а строки в таблице нет — его числа не обновятся"));
        }

        // ---------------- upgrades ----------------

        private static void ValidateUpgrades(SheetTable t, List<Issue> issues)
        {
            if (t.Rows.Count == 0)
            {
                issues.Add(Issue.Error(t.Name, "вкладка пуста"));
                return;
            }

            var seenIds = new HashSet<int>();
            var seenAssets = new HashSet<string>();
            var sheetAssetNames = new HashSet<string>();

            for (int i = 0; i < t.Rows.Count; i++)
            {
                var row = t.Rows[i];
                string where = $"{t.Name}!строка {i + 2}";

                string assetName = Cell.Text(row, "asset_name");
                if (string.IsNullOrEmpty(assetName))
                {
                    issues.Add(Issue.Error(where, "пустой asset_name"));
                    continue;
                }

                if (!seenAssets.Add(assetName))
                    issues.Add(Issue.Error(where, $"дубль asset_name '{assetName}'"));
                sheetAssetNames.Add(assetName);

                var asset = BalanceAssetIndex.FindUpgrade(assetName);
                if (asset == null)
                {
                    issues.Add(Issue.Error(where, $"UpgradeBaseSO-ассет '{assetName}' не найден в проекте"));
                    continue;
                }

                int id = Cell.Int(row, "id", where, issues);
                if (id <= 0) issues.Add(Issue.Error(where, $"id = {id}, ожидается целое > 0"));
                else if (!seenIds.Add(id)) issues.Add(Issue.Error(where, $"дубль id {id}"));

                int tier = Cell.Int(row, "tier", where, issues);
                if (tier < 1 || tier > MaxTier) issues.Add(Issue.Error(where, $"tier = {tier}, ожидается 1..{MaxTier}"));

                float weight = Cell.Float(row, "weight", where, issues);
                if (weight <= 0f) issues.Add(Issue.Error(where, $"weight = {weight}, ожидается > 0"));

                int cost = Cell.Int(row, "cost", where, issues);
                if (cost < 0) issues.Add(Issue.Error(where, $"cost = {cost}, ожидается >= 0"));

                // Проверяем связку значений с картой полей.
                var bindings = UpgradeValueMap.Get(assetName);
                if (bindings == null)
                {
                    issues.Add(Issue.Error(where, $"'{assetName}' не описан в карте значений импортёра (UpgradeValueMap) — добавьте маппинг value_1/value_2 на поля ассета"));
                }
                else
                {
                    foreach (var b in bindings)
                        Cell.Float(row, b.Column, where, issues); // обязательность + число
                }
            }

            foreach (var name in BalanceAssetIndex.AllUpgradeNames())
                if (!sheetAssetNames.Contains(name))
                    issues.Add(Issue.Warning(t.Name, $"в проекте есть улучшение '{name}', а строки в таблице нет — его числа не обновятся"));
        }

        // ---------------- enemy ----------------

        private static void ValidateEnemy(SheetTable t, List<Issue> issues)
        {
            var row = SingleRow(t, issues);
            if (row == null) return;
            string w = t.Name;

            foreach (var col in new[] { "hp_melee", "hp_mid", "hp_range" })
                if (Cell.Float(row, col, w, issues) <= 0f)
                    issues.Add(Issue.Error(w, $"{col} должно быть > 0"));

            foreach (var col in new[] { "damage_melee", "damage_mid", "damage_range", "gold_for_enemy", "hp_add_per_wave", "damage_add_per_wave" })
                if (Cell.Float(row, col, w, issues) < 0f)
                    issues.Add(Issue.Error(w, $"{col} должно быть >= 0"));

            foreach (var col in new[] { "speed_melee", "speed_mid", "speed_range", "attack_interval", "delay_per_wave" })
                if (Cell.Float(row, col, w, issues) <= 0f)
                    issues.Add(Issue.Error(w, $"{col} должно быть > 0"));

            if (Cell.Int(row, "enemy_per_wave", w, issues) <= 0)
                issues.Add(Issue.Error(w, "enemy_per_wave должно быть > 0"));

            float dStart = Cell.Float(row, "difficult_start", w, issues);
            float dMid = Cell.Float(row, "difficult_mid", w, issues);
            float dEnd = Cell.Float(row, "difficult_end", w, issues);
            if (dStart <= 0f) issues.Add(Issue.Error(w, "difficult_start должно быть > 0"));
            if (!(dStart <= dMid && dMid <= dEnd))
                issues.Add(Issue.Error(w, $"ожидается difficult_start <= difficult_mid <= difficult_end (сейчас {dStart} / {dMid} / {dEnd})"));

            float tStart = Cell.Float(row, "time_difficult_start", w, issues);
            float tMid = Cell.Float(row, "time_difficult_mid", w, issues);
            float tEnd = Cell.Float(row, "time_difficult_end", w, issues);
            if (!(tStart < tMid && tMid < tEnd))
                issues.Add(Issue.Error(w, $"ожидается time_difficult_start < time_difficult_mid < time_difficult_end (сейчас {tStart} / {tMid} / {tEnd})"));
            if (tStart != 0f)
                issues.Add(Issue.Warning(w, $"time_difficult_start = {tStart}, но игра применяет difficult_start с нулевой секунды — значение игнорируется"));
        }

        // ---------------- boss ----------------

        private static void ValidateBoss(SheetTable t, List<Issue> issues)
        {
            var row = SingleRow(t, issues);
            if (row == null) return;
            string w = t.Name;

            if (Cell.Float(row, "hp_boss", w, issues) <= 0f) issues.Add(Issue.Error(w, "hp_boss должно быть > 0"));
            if (Cell.Float(row, "damage_boss", w, issues) < 0f) issues.Add(Issue.Error(w, "damage_boss должно быть >= 0"));
            if (Cell.Float(row, "gold_for_boss", w, issues) < 0f) issues.Add(Issue.Error(w, "gold_for_boss должно быть >= 0"));

            foreach (var col in new[] { "speed_boss", "attack_interval", "attack_range", "spawn_interval", "hp_multiplier", "damage_multiplier" })
                if (Cell.Float(row, col, w, issues) <= 0f)
                    issues.Add(Issue.Error(w, $"{col} должно быть > 0"));
        }

        // ---------------- shop ----------------

        private static void ValidateShop(SheetTable t, List<Issue> issues)
        {
            var row = SingleRow(t, issues);
            if (row == null) return;
            string w = t.Name;

            foreach (var col in new[] { "first_upgrade_cost", "second_upgrade_cost", "reroll_base_cost", "reroll_cost_increase", "start_gold", "passive_gold_per_tick" })
                if (Cell.Int(row, col, w, issues) < 0)
                    issues.Add(Issue.Error(w, $"{col} должно быть >= 0"));

            if (Cell.Float(row, "passive_income_interval", w, issues) <= 0f)
                issues.Add(Issue.Error(w, "passive_income_interval должно быть > 0"));

            float autoReroll = Cell.Float(row, "auto_reroll_interval", w, issues);
            if (autoReroll < 0f)
                issues.Add(Issue.Error(w, "auto_reroll_interval должно быть >= 0 (0 = авто-реролл выключен)"));

            ParseIntList(Cell.Text(row, "start_unlocked_columns"), w, "start_unlocked_columns", issues);
            var tiers = ParseIntList(Cell.Text(row, "start_unlocked_tiers"), w, "start_unlocked_tiers", issues);
            foreach (var tier in tiers)
                if (tier < 1 || tier > MaxTier)
                    issues.Add(Issue.Error(w, $"start_unlocked_tiers содержит {tier}, ожидается 1..{MaxTier}"));

            foreach (var col in new[] { "first_upgrade_unlocks_tier", "second_upgrade_unlocks_tier" })
            {
                int tier = Cell.Int(row, col, w, issues);
                if (tier < 1 || tier > MaxTier)
                    issues.Add(Issue.Error(w, $"{col} = {tier}, ожидается 1..{MaxTier}"));
            }

            foreach (var col in new[] { "first_upgrade_unlocks_column", "second_upgrade_unlocks_column" })
            {
                int c = Cell.Int(row, col, w, issues);
                if (c < 0) issues.Add(Issue.Error(w, $"{col} = {c}, ожидается >= 0 (индекс колонки магазина с нуля)"));
            }
        }

        // ---------------- player ----------------

        private static void ValidatePlayer(SheetTable t, List<Issue> issues)
        {
            var row = SingleRow(t, issues);
            if (row == null) return;
            string w = t.Name;

            if (Cell.Float(row, "player_hp", w, issues) <= 0f)
                issues.Add(Issue.Error(w, "player_hp должно быть > 0"));
            if (Cell.Float(row, "player_shield", w, issues) < 0f)
                issues.Add(Issue.Error(w, "player_shield должно быть >= 0"));

            float blockCap = Cell.Float(row, "block_cap", w, issues);
            if (blockCap < 0f || blockCap > 0.95f)
                issues.Add(Issue.Error(w, $"block_cap = {blockCap}, ожидается 0..0.95"));

            foreach (var col in new[] { "shield_recharge_time", "shield_full_restore_after_no_damage", "tower_range", "game_speed_1", "game_speed_2", "game_speed_3" })
                if (Cell.Float(row, col, w, issues) <= 0f)
                    issues.Add(Issue.Error(w, $"{col} должно быть > 0"));

            float volley = Cell.Float(row, "volley_window_percent", w, issues);
            if (volley < 0f || volley > 1f)
                issues.Add(Issue.Error(w, $"volley_window_percent = {volley}, ожидается 0..1"));
        }

        // ---------------- helpers ----------------

        private static Dictionary<string, string> SingleRow(SheetTable t, List<Issue> issues)
        {
            if (t.Rows.Count == 0)
            {
                issues.Add(Issue.Error(t.Name, "нет строки со значениями"));
                return null;
            }
            if (t.Rows.Count > 1)
                issues.Add(Issue.Warning(t.Name, $"строк со значениями {t.Rows.Count}, используется только первая"));
            return t.Rows[0];
        }

        public static List<int> ParseIntList(string s, string where, string col, List<Issue> issues)
        {
            var result = new List<int>();
            if (string.IsNullOrWhiteSpace(s))
            {
                issues.Add(Issue.Error(where, $"пустая ячейка '{col}' (ожидается список через ';', например 0;1;2)"));
                return result;
            }

            foreach (var part in s.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(part.Trim(), out int v)) result.Add(v);
                else issues.Add(Issue.Error(where, $"'{col}' содержит '{part}' — не целое число"));
            }
            return result;
        }
    }
}
