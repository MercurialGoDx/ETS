using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ETS.BalanceImport
{
    /// <summary>
    /// Пайплайн баланса: Google Sheets → Config/*.csv → валидация → ассеты + BalanceConfig + balance.json.
    ///
    /// Меню:
    ///  - Tools/Balance/Pull From Google Sheets + Import — скачать все вкладки и импортировать
    ///  - Tools/Balance/Import (Local CSV)               — импорт из Config/*.csv без сети
    ///  - Tools/Balance/Validate Only                    — только проверка, ничего не пишет
    ///
    /// При ЛЮБОЙ ошибке валидации не меняется ни один файл; все проблемы выводятся разом.
    /// </summary>
    public static class BalanceImporter
    {
        // ID таблицы "config ETS" на Google Drive (из URL между /d/ и /edit).
        private const string SheetId = "1zh7YG5qijXaPp5zZvD2f7_kdI97QPTaOIKes9slpVZI";

        private static string ConfigDir => Path.Combine(Directory.GetCurrentDirectory(), "Config");

        // ---------------- меню ----------------

        [MenuItem("Tools/Balance/Pull From Google Sheets + Import")]
        public static void PullAndImport()
        {
            var downloaded = new Dictionary<string, string>();

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

                for (int i = 0; i < BalanceSheets.TabNames.Length; i++)
                {
                    string tab = BalanceSheets.TabNames[i];
                    EditorUtility.DisplayProgressBar("Balance", $"Скачивание вкладки {tab}…",
                        (float)i / BalanceSheets.TabNames.Length);

                    // gviz-экспорт адресует вкладку ПО ИМЕНИ — gid не нужен.
                    // Требуется доступ «читатель по ссылке» на таблицу.
                    string url = $"https://docs.google.com/spreadsheets/d/{SheetId}/gviz/tq?tqx=out:csv&sheet={Uri.EscapeDataString(tab)}";
                    string csv = client.GetStringAsync(url).GetAwaiter().GetResult();

                    // Если доступ не открыт, Google отдаёт HTML-страницу логина.
                    if (csv.TrimStart().StartsWith("<"))
                        throw new Exception($"вкладка {tab}: вместо CSV пришёл HTML — таблица не расшарена. Открой доступ «читатель по ссылке».");

                    EnsureRequestedTab(tab, csv);
                    downloaded[tab] = csv;
                }
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[Balance] Скачивание не удалось: {e.Message}\nConfig/*.csv не тронуты — можно импортировать локальные: Tools → Balance → Import (Local CSV).");
                return;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            // Пишем на диск только когда скачались ВСЕ вкладки: иначе в Config/
            // осталась бы смесь новых и старых файлов.
            Directory.CreateDirectory(ConfigDir);
            foreach (var (tab, csv) in downloaded)
                File.WriteAllText(Path.Combine(ConfigDir, tab + ".csv"), csv, new UTF8Encoding(false));

            Import(writeChanges: true);
        }

        /// <summary>
        /// Google на НЕСУЩЕСТВУЮЩЕЕ имя вкладки отвечает 200 OK и отдаёт первую
        /// вкладку таблицы вместо ошибки. Без этой проверки переименованная в
        /// таблице вкладка молча затирала бы свой Config/*.csv чужими данными.
        /// Опознаём вкладку по шапке: в ней обязаны быть её колонки.
        /// </summary>
        private static void EnsureRequestedTab(string tab, string csv)
        {
            var required = BalanceValidator.RequiredColumns(tab);
            if (required.Length == 0) return;

            var header = BalanceSheets.ParseCsv(tab, csv).Columns;
            var missing = required.Where(c => !header.Contains(c)).ToList();
            if (missing.Count == 0) return;

            string got = string.Join(", ", header.Where(c => !string.IsNullOrWhiteSpace(c)).Take(6));
            throw new Exception(
                $"вкладка '{tab}': пришли не те колонки (нет {string.Join(", ", missing.Take(5))}" +
                (missing.Count > 5 ? $" и ещё {missing.Count - 5}" : "") + $"; в шапке: {got}…). " +
                "Google отдаёт ПЕРВУЮ вкладку, когда вкладки с таким именем нет — " +
                $"проверь, что в таблице есть вкладка ровно с именем '{tab}'.");
        }

        [MenuItem("Tools/Balance/Import (Local CSV)")]
        public static void ImportLocal() => Import(writeChanges: true);

        [MenuItem("Tools/Balance/Validate Only")]
        public static void ValidateOnly() => Import(writeChanges: false);

        // ---------------- импорт ----------------

        private static void Import(bool writeChanges)
        {
            BalanceAssetIndex.Invalidate();

            var issues = new List<Issue>();
            var sheets = BalanceSheets.LoadFromDirectory(ConfigDir, issues);

            issues.AddRange(BalanceValidator.Validate(sheets));

            var errors = issues.Where(i => i.Severity == IssueSeverity.Error).ToList();
            var warnings = issues.Where(i => i.Severity == IssueSeverity.Warning).ToList();

            foreach (var issue in issues.OrderBy(i => i.Severity == IssueSeverity.Warning))
            {
                if (issue.Severity == IssueSeverity.Error) Debug.LogError("[Balance] " + issue);
                else Debug.LogWarning("[Balance] " + issue);
            }

            if (errors.Count > 0)
            {
                EditorUtility.DisplayDialog("Balance: импорт отменён",
                    $"Ошибок: {errors.Count}, warnings: {warnings.Count}.\n\n" +
                    "Ни один файл не изменён. Полный список — в Console.\n\n" +
                    string.Join("\n", errors.Take(12).Select(x => "• " + x)) +
                    (errors.Count > 12 ? $"\n…и ещё {errors.Count - 12}" : ""),
                    "Ок");
                return;
            }

            if (!writeChanges)
            {
                // Успех — только в Console: модальное окно блокирует главный поток
                // (и заодно MCP/CI), а подтверждать тут нечего.
                Debug.Log($"[Balance] Валидация пройдена: ошибок 0, warnings {warnings.Count}. Файлы не менялись (Validate Only).");
                return;
            }

            // ---- запись (только после чистой валидации) ----

            var writeIssues = new List<Issue>();

            int weaponsStamped = BalanceAssetWriter.StampWeapons(sheets.Weapon);
            int upgradesStamped = BalanceAssetWriter.StampUpgrades(sheets.Upgrades, writeIssues);
            int enemiesStamped = BalanceAssetWriter.StampEnemies(sheets.Enemy, writeIssues);
            bool bossStamped = BalanceAssetWriter.StampBoss(sheets.Boss, writeIssues);
            BalanceAssetWriter.StampShopUnlocks(sheets.Shop, writeIssues);
            var balanceConfig = BalanceAssetWriter.WriteBalanceConfig(sheets);
            BalanceJsonWriter.Write(sheets);

            // Часть баланса живёт на компонентах прямо на сцене (EnemySpawner, BossManager
            // и т.п.) — их Start()/Awake() перекрывает инспектор конфигом только в Play-режиме.
            // Проставляем те же значения и в открытую сцену, чтобы инспектор совпадал сразу.
            BalanceSceneStamper.StampSceneObjects(balanceConfig, writeIssues);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            foreach (var issue in writeIssues)
            {
                if (issue.Severity == IssueSeverity.Error) Debug.LogError("[Balance] " + issue);
                else Debug.LogWarning("[Balance] " + issue);
            }

            string summary =
                $"Оружий: {weaponsStamped}, улучшений: {upgradesStamped}, врагов: {enemiesStamped}, босс: {(bossStamped ? "да" : "НЕТ")}.\n" +
                $"BalanceConfig: {BalanceAssetWriter.BalanceConfigPath}\n" +
                $"JSON: {BalanceJsonWriter.JsonPath}\n" +
                $"Warnings: {warnings.Count}" +
                (writeIssues.Count > 0 ? $"\n⚠ Проблем при записи: {writeIssues.Count} (см. Console)" : "");

            Debug.Log("[Balance] Импорт завершён.\n" + summary);
        }
    }
}
