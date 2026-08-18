using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ETS.BalanceImport
{
    /// <summary>
    /// Обратное направление пайплайна: Unity → таблица.
    /// Собирает текущие значения из ассетов/префабов/BalanceConfig в тот же
    /// формат вкладок, что читает импортёр.
    ///
    /// Меню:
    ///  - Tools/Balance/Export (Unity → Config CSV)      — перезаписать Config/*.csv (виден git diff)
    ///  - Tools/Balance/Copy Tab For Google Sheets/…     — TSV вкладки в буфер обмена
    ///
    /// Про запись прямо в Google Sheets: gviz, на котором держится Pull, работает
    /// только на чтение, а запись через Sheets API требует OAuth/сервисного аккаунта
    /// (ключа в проекте нет). Поэтому «залив» — это буфер обмена: открыть вкладку,
    /// встать на A1 и Ctrl+V, данные лягут ровно по колонкам.
    ///
    /// Колонок, которых в Unity не существует (name, id, effect_1, effect_2),
    /// экспорт НЕ придумывает: он берёт их из текущего Config/*.csv по asset_name.
    /// </summary>
    public static class BalanceExporter
    {
        private static string ConfigDir => Path.Combine(Directory.GetCurrentDirectory(), "Config");

        // ---------------- меню ----------------

        [MenuItem("Tools/Balance/Export (Unity → Config CSV)")]
        public static void ExportToConfigCsv()
        {
            var issues = new List<Issue>();
            var tables = BuildAll(issues);

            if (issues.Any(i => i.Severity == IssueSeverity.Error))
            {
                LogIssues(issues);
                Debug.LogError("[Balance] Экспорт отменён: не удалось собрать данные из проекта. Ни один файл не изменён.");
                return;
            }

            Directory.CreateDirectory(ConfigDir);
            var changed = new List<string>();

            foreach (var (tab, table) in tables)
            {
                string path = Path.Combine(ConfigDir, tab + ".csv");
                string csv = ToCsv(table);

                if (File.Exists(path) && File.ReadAllText(path, Encoding.UTF8) == csv)
                    continue;

                File.WriteAllText(path, csv, new UTF8Encoding(false));
                changed.Add(tab);
            }

            LogIssues(issues);

            Debug.Log(changed.Count == 0
                ? "[Balance] Экспорт: Config/*.csv уже совпадают со значениями в проекте — менять нечего."
                : $"[Balance] Экспорт завершён. Обновлены вкладки: {string.Join(", ", changed)}.\n" +
                  "Посмотри git diff по Config/, затем перенеси в таблицу: " +
                  "Tools → Balance → Copy Tab For Google Sheets → нужная вкладка, и Ctrl+V в A1.");
        }

        [MenuItem("Tools/Balance/Copy Tab For Google Sheets/weapon")]
        public static void CopyWeapon() => CopyTab("weapon");

        [MenuItem("Tools/Balance/Copy Tab For Google Sheets/upgrades")]
        public static void CopyUpgrades() => CopyTab("upgrades");

        [MenuItem("Tools/Balance/Copy Tab For Google Sheets/enemy")]
        public static void CopyEnemy() => CopyTab("enemy");

        [MenuItem("Tools/Balance/Copy Tab For Google Sheets/boss")]
        public static void CopyBoss() => CopyTab("boss");

        [MenuItem("Tools/Balance/Copy Tab For Google Sheets/shop")]
        public static void CopyShop() => CopyTab("shop");

        [MenuItem("Tools/Balance/Copy Tab For Google Sheets/player_constant")]
        public static void CopyPlayer() => CopyTab("player_constant");

        private static void CopyTab(string tab)
        {
            var issues = new List<Issue>();
            var tables = BuildAll(issues);
            LogIssues(issues);

            if (!tables.TryGetValue(tab, out var table))
            {
                Debug.LogError($"[Balance] Вкладку '{tab}' собрать не удалось — см. Console.");
                return;
            }

            EditorGUIUtility.systemCopyBuffer = ToTsv(table);
            Debug.Log($"[Balance] Вкладка '{tab}' ({table.Rows.Count} строк) скопирована как TSV. " +
                      $"Открой вкладку '{tab}' в таблице, встань на A1 и нажми Ctrl+V.");
        }

        // ---------------- сборка вкладок ----------------

        private static Dictionary<string, SheetTable> BuildAll(List<Issue> issues)
        {
            BalanceAssetIndex.Invalidate();

            var config = AssetDatabase.LoadAssetAtPath<BalanceConfig>(BalanceAssetWriter.BalanceConfigPath);
            if (config == null)
                issues.Add(Issue.Error("export",
                    $"не найден {BalanceAssetWriter.BalanceConfigPath} — вкладки enemy/boss/shop/player_constant собрать не из чего. Сначала запусти импорт."));

            var result = new Dictionary<string, SheetTable>();

            var weapon = BuildWeapon(issues);
            if (weapon != null) result["weapon"] = weapon;

            var upgrades = BuildUpgrades(issues);
            if (upgrades != null) result["upgrades"] = upgrades;

            if (config != null)
            {
                var enemy = BuildEnemy(config, issues);
                if (enemy != null) result["enemy"] = enemy;

                var boss = BuildBoss(config, issues);
                if (boss != null) result["boss"] = boss;

                var shop = BuildShop(config, issues);
                if (shop != null) result["shop"] = shop;

                result["player_constant"] = BuildPlayer(config);
            }

            return result;
        }

        // ---- weapon ----

        private static SheetTable BuildWeapon(List<Issue> issues)
        {
            var table = NewTable("weapon");
            var baseRows = BaseRowsByAsset("weapon");

            foreach (string assetName in BalanceAssetIndex.AllWeaponNames().OrderBy(n => n, StringComparer.Ordinal))
            {
                var asset = BalanceAssetIndex.FindWeapon(assetName);
                var row = TakeBaseRow(baseRows, assetName, table, issues, "weapon");

                row["asset_name"] = assetName;
                row["attack_speed"] = Num(asset.fireRate);
                row["damage"] = Num(asset.damagePerProjectile);
                row["tier"] = Num((int)asset.itemTier);
                row["weight"] = Num(asset.weight);
                row["cost"] = Num(asset.price);
                row["damage_type"] = asset.damageType.ToString();
                row["projectile_speed"] = Num(asset.projectileSpeed);
                row["weight_increase_per_purchase"] = Num(asset.weightIncreasePerPurchase);
                row["max_weight_increase_purchases"] = Num(asset.maxWeightIncreasePurchases);

                table.Rows.Add(row);
            }

            SortByIdIfPossible(table);
            ReportOrphanBaseRows(baseRows, "weapon", issues);
            return table;
        }

        // ---- upgrades ----

        private static SheetTable BuildUpgrades(List<Issue> issues)
        {
            var table = NewTable("upgrades");
            var baseRows = BaseRowsByAsset("upgrades");

            foreach (string assetName in BalanceAssetIndex.AllUpgradeNames().OrderBy(n => n, StringComparer.Ordinal))
            {
                var asset = BalanceAssetIndex.FindUpgrade(assetName);
                var row = TakeBaseRow(baseRows, assetName, table, issues, "upgrades");

                row["asset_name"] = assetName;
                row["tier"] = Num((int)asset.itemTier);
                row["weight"] = Num(asset.weight);
                row["cost"] = Num(asset.price);
                row["weight_increase_per_purchase"] = Num(asset.weightIncreasePerPurchase);
                row["max_weight_increase_purchases"] = Num(asset.maxWeightIncreasePurchases);

                var bindings = UpgradeValueMap.Get(assetName);
                if (bindings == null)
                {
                    issues.Add(Issue.Warning("upgrades",
                        $"'{assetName}' не описан в UpgradeValueMap — value_1/value_2/interval выгружены как есть из CSV, из ассета не прочитаны"));
                }
                else
                {
                    foreach (var b in bindings)
                    {
                        if (!TryReadBinding(asset, b, out float value, issues))
                            continue;
                        row[b.Column] = Num(value / (b.Scale == 0f ? 1f : b.Scale));
                    }
                }

                table.Rows.Add(row);
            }

            SortByIdIfPossible(table);
            ReportOrphanBaseRows(baseRows, "upgrades", issues);
            return table;
        }

        private static bool TryReadBinding(UpgradeBaseSO asset, ValueBinding b, out float value, List<Issue> issues)
        {
            value = 0f;

            UnityEngine.Object target = asset;
            if (b.ChildType != null)
            {
                target = BalanceAssetWriter.FindCompositeChild(asset, b.ChildType);
                if (target == null)
                {
                    issues.Add(Issue.Warning("upgrades",
                        $"'{asset.name}': в composite нет дочернего ассета типа {b.ChildType} — {b.Column} взят из CSV"));
                    return false;
                }
            }

            var prop = new SerializedObject(target).FindProperty(b.Field);
            if (prop == null)
            {
                issues.Add(Issue.Warning("upgrades",
                    $"'{asset.name}': у {target.name} нет поля {b.Field} — {b.Column} взят из CSV"));
                return false;
            }

            value = prop.propertyType == SerializedPropertyType.Integer ? prop.intValue : prop.floatValue;
            return true;
        }

        // ---- enemy ----

        private static SheetTable BuildEnemy(BalanceConfig config, List<Issue> issues)
        {
            var table = NewTable("enemy");
            var row = SingleBaseRow("enemy", table);

            foreach (var (path, dmgCol, hpCol, speedCol) in BalanceAssetWriter.EnemyPrefabs)
            {
                var enemy = LoadEnemy(path, "enemy", issues);
                if (enemy == null) return null;

                row[dmgCol] = Num(enemy.damageToPlayer);
                row[hpCol] = Num(enemy.maxHealth);
                row[speedCol] = Num(enemy.speed);
            }

            // gold_for_enemy и attack_interval общие на все три типа врагов —
            // импортёр пишет их во все префабы, поэтому читаем с первого.
            var first = LoadEnemy(BalanceAssetWriter.EnemyPrefabs[0].prefab, "enemy", issues);
            if (first == null) return null;
            row["gold_for_enemy"] = Num(first.baseGold);
            row["attack_interval"] = Num(first.attackInterval);

            row["delay_per_wave"] = Num(config.waves.delayPerWave);
            row["enemy_per_wave"] = Num(config.waves.enemyPerWave);
            // Рост сложности разделён на HP и урон, у каждого своя база и по три
            // множителя ускорения; пороги времени общие. Колонки те же, что читает
            // BalanceAssetWriter, иначе экспорт разошёлся бы с импортом.
            row["health_difficult_start"] = Num(config.waves.healthDifficultStart);
            row["damage_difficult_start"] = Num(config.waves.damageDifficultStart);
            row["growth_stage1_multiplier_health"] = Num(config.waves.growthStage1MultiplierHealth);
            row["growth_stage1_multiplier_damage"] = Num(config.waves.growthStage1MultiplierDamage);
            row["growth_stage2_multiplier_health"] = Num(config.waves.growthStage2MultiplierHealth);
            row["growth_stage2_multiplier_damage"] = Num(config.waves.growthStage2MultiplierDamage);
            row["growth_stage3_multiplier_health"] = Num(config.waves.growthStage3MultiplierHealth);
            row["growth_stage3_multiplier_damage"] = Num(config.waves.growthStage3MultiplierDamage);
            row["time_difficult_stage1"] = Num(config.waves.timeDifficultStage1);
            row["time_difficult_stage2"] = Num(config.waves.timeDifficultStage2);
            row["time_difficult_stage3"] = Num(config.waves.timeDifficultStage3);
            row["hp_add_per_wave"] = Num(config.waves.hpAddPerWave);
            row["damage_add_per_wave"] = Num(config.waves.damageAddPerWave);

            table.Rows.Add(row);
            return table;
        }

        // ---- boss ----

        private static SheetTable BuildBoss(BalanceConfig config, List<Issue> issues)
        {
            var table = NewTable("boss");
            var row = SingleBaseRow("boss", table);

            var boss = LoadEnemy(BalanceAssetWriter.BossPrefabPath, "boss", issues);
            if (boss == null) return null;

            row["damage_boss"] = Num(boss.damageToPlayer);
            row["hp_boss"] = Num(boss.maxHealth);
            row["gold_for_boss"] = Num(boss.baseGold);
            row["speed_boss"] = Num(boss.speed);
            row["attack_interval"] = Num(boss.attackInterval);
            row["attack_range"] = Num(boss.attackRange);

            row["spawn_interval"] = Num(config.boss.spawnInterval);
            row["hp_multiplier"] = Num(config.boss.hpMultiplier);
            row["damage_multiplier"] = Num(config.boss.damageMultiplier);

            table.Rows.Add(row);
            return table;
        }

        // ---- shop ----

        private static SheetTable BuildShop(BalanceConfig config, List<Issue> issues)
        {
            var table = NewTable("shop");
            var row = SingleBaseRow("shop", table);

            if (!ReadUnlock($"{BalanceAssetWriter.ShopUnlockDir}/ShopUnlock_1.asset", row, "first", issues)) return null;
            if (!ReadUnlock($"{BalanceAssetWriter.ShopUnlockDir}/ShopUnlock_2.asset", row, "second", issues)) return null;

            var unlockConfig = AssetDatabase.LoadAssetAtPath<ShopUnlockConfigSO>(
                $"{BalanceAssetWriter.ShopUnlockDir}/ShopUnlockConfig.asset");
            if (unlockConfig == null)
            {
                issues.Add(Issue.Error("shop", $"не найден {BalanceAssetWriter.ShopUnlockDir}/ShopUnlockConfig.asset"));
                return null;
            }

            row["start_unlocked_columns"] = JoinInts(unlockConfig.startingUnlockedColumns);
            row["start_unlocked_tiers"] = JoinInts(unlockConfig.startingUnlockedTiers.Select(t => (int)t));

            row["reroll_base_cost"] = Num(config.shop.rerollBaseCost);
            row["reroll_cost_increase"] = Num(config.shop.rerollCostIncrease);
            row["auto_reroll_interval"] = Num(config.shop.autoRerollInterval);
            row["start_gold"] = Num(config.shop.startGold);
            row["passive_gold_per_tick"] = Num(config.shop.passiveGoldPerTick);
            row["passive_income_interval"] = Num(config.shop.passiveIncomeInterval);

            table.Rows.Add(row);
            return table;
        }

        private static bool ReadUnlock(string path, Dictionary<string, string> row, string prefix, List<Issue> issues)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ShopUnlockUpgradeSO>(path);
            if (asset == null)
            {
                issues.Add(Issue.Error("shop", $"не найден {path}"));
                return false;
            }

            row[$"{prefix}_upgrade_cost"] = Num(asset.price);
            row[$"{prefix}_upgrade_unlocks_tier"] = Num((int)asset.unlockedTier);

            // В ассете это список, в таблице — одна колонка (так же читает импортёр).
            if (asset.unlockedColumns == null || asset.unlockedColumns.Count == 0)
            {
                issues.Add(Issue.Error("shop", $"{path}: unlockedColumns пуст — нечего выгрузить в {prefix}_upgrade_unlocks_column"));
                return false;
            }
            if (asset.unlockedColumns.Count > 1)
                issues.Add(Issue.Warning("shop",
                    $"{path}: в unlockedColumns {asset.unlockedColumns.Count} значений, а в таблице колонка одна — выгружено только {asset.unlockedColumns[0]}"));

            row[$"{prefix}_upgrade_unlocks_column"] = Num(asset.unlockedColumns[0]);
            return true;
        }

        // ---- player_constant ----

        private static SheetTable BuildPlayer(BalanceConfig config)
        {
            var table = NewTable("player_constant");
            var row = SingleBaseRow("player_constant", table);

            row["player_hp"] = Num(config.player.playerHp);
            row["player_shield"] = Num(config.player.playerShield);
            row["block_cap"] = Num(config.player.blockCap);
            row["shield_recharge_time"] = Num(config.player.shieldRechargeTime);
            row["shield_full_restore_after_no_damage"] = Num(config.player.shieldFullRestoreAfterNoDamage);
            row["tower_range"] = Num(config.player.towerRange);
            row["volley_window_percent"] = Num(config.player.volleyWindowPercent);
            row["game_speed_1"] = Num(config.player.gameSpeed1);
            row["game_speed_2"] = Num(config.player.gameSpeed2);
            row["game_speed_3"] = Num(config.player.gameSpeed3);

            table.Rows.Add(row);
            return table;
        }

        private static void LogIssues(List<Issue> issues)
        {
            foreach (var issue in issues)
            {
                if (issue.Severity == IssueSeverity.Error) Debug.LogError("[Balance] " + issue);
                else Debug.LogWarning("[Balance] " + issue);
            }
        }

        private static Enemy LoadEnemy(string path, string where, List<Issue> issues)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                issues.Add(Issue.Error(where, $"префаб не найден: {path}"));
                return null;
            }

            var enemy = prefab.GetComponent<Enemy>();
            if (enemy == null)
                issues.Add(Issue.Error(where, $"{path}: нет компонента Enemy"));

            return enemy;
        }

        // ---------------- база из текущего CSV ----------------

        /// <summary>
        /// Шапка вкладки: берём из существующего CSV (сохраняем порядок и «лишние»
        /// колонки таблицы), иначе — обязательный набор валидатора.
        /// </summary>
        private static SheetTable NewTable(string tab)
        {
            var table = new SheetTable { Name = tab };
            var existing = LoadExisting(tab);

            table.Columns = existing != null && existing.Columns.Count > 0
                ? existing.Columns.Where(c => !string.IsNullOrWhiteSpace(c)).ToList()
                : BalanceValidator.RequiredColumns(tab).ToList();

            foreach (var col in BalanceValidator.RequiredColumns(tab))
                if (!table.Columns.Contains(col))
                    table.Columns.Add(col);

            return table;
        }

        private static SheetTable LoadExisting(string tab)
        {
            string path = Path.Combine(ConfigDir, tab + ".csv");
            if (!File.Exists(path)) return null;

            try { return BalanceSheets.ParseCsv(tab, File.ReadAllText(path, Encoding.UTF8)); }
            catch { return null; }
        }

        private static Dictionary<string, Dictionary<string, string>> BaseRowsByAsset(string tab)
        {
            var result = new Dictionary<string, Dictionary<string, string>>();
            var existing = LoadExisting(tab);
            if (existing == null) return result;

            foreach (var row in existing.Rows)
            {
                string key = Cell.Text(row, "asset_name");
                if (!string.IsNullOrEmpty(key))
                    result[key] = row;
            }

            return result;
        }

        /// <summary>
        /// Строка под ассет: текстовые колонки (name, id, effect_*) переносим из
        /// текущего CSV — в Unity их нет. Для нового ассета они останутся пустыми,
        /// об этом предупреждаем, чтобы дизайнер заполнил вручную.
        /// </summary>
        private static Dictionary<string, string> TakeBaseRow(
            Dictionary<string, Dictionary<string, string>> baseRows, string assetName,
            SheetTable table, List<Issue> issues, string tab)
        {
            if (baseRows.Remove(assetName, out var existing))
                return new Dictionary<string, string>(existing);

            issues.Add(Issue.Warning(tab,
                $"'{assetName}' есть в проекте, но нет строки в Config/{tab}.csv — колонки name/id/effect_* останутся пустыми, заполни их в таблице"));

            return table.Columns.ToDictionary(c => c, _ => "");
        }

        private static void ReportOrphanBaseRows(
            Dictionary<string, Dictionary<string, string>> leftovers, string tab, List<Issue> issues)
        {
            foreach (var key in leftovers.Keys)
                issues.Add(Issue.Warning(tab,
                    $"строка '{key}' есть в Config/{tab}.csv, но такого ассета в проекте нет — из выгрузки исключена"));
        }

        private static Dictionary<string, string> SingleBaseRow(string tab, SheetTable table)
        {
            var existing = LoadExisting(tab);
            return existing != null && existing.Rows.Count > 0
                ? new Dictionary<string, string>(existing.Rows[0])
                : table.Columns.ToDictionary(c => c, _ => "");
        }

        private static void SortByIdIfPossible(SheetTable table)
        {
            if (!table.HasColumn("id")) return;

            table.Rows = table.Rows
                .OrderBy(r => int.TryParse(Cell.Text(r, "id"), out int id) ? id : int.MaxValue)
                .ThenBy(r => Cell.Text(r, "asset_name"), StringComparer.Ordinal)
                .ToList();
        }

        // ---------------- сериализация ----------------

        // CRLF — ровно то, что отдаёт Google и что кладёт на диск Pull. Иначе
        // экспорт переписывал бы все файлы даже когда значения не поменялись.
        private const string Newline = "\r\n";

        private static string ToCsv(SheetTable table)
        {
            var sb = new StringBuilder();
            sb.Append(string.Join(",", table.Columns.Select(Quote)));

            foreach (var row in table.Rows)
            {
                sb.Append(Newline);
                sb.Append(string.Join(",", table.Columns.Select(c => Quote(row.GetValueOrDefault(c, "")))));
            }

            return sb.ToString();
        }

        private static string ToTsv(SheetTable table)
        {
            var sb = new StringBuilder();
            sb.Append(string.Join("\t", table.Columns));

            foreach (var row in table.Rows)
            {
                sb.Append(Newline);
                sb.Append(string.Join("\t", table.Columns.Select(c => row.GetValueOrDefault(c, ""))));
            }

            return sb.ToString();
        }

        private static string Quote(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";

        private static string JoinInts(IEnumerable<int> values) => string.Join(";", values);

        /// <summary>
        /// Дроби пишем через запятую — так их отдаёт сама таблица (локаль ru),
        /// и так они лежат в текущих Config/*.csv; импортёр принимает оба разделителя.
        /// </summary>
        private static string Num(float value) =>
            value.ToString("0.#####", CultureInfo.InvariantCulture).Replace('.', ',');

        private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
