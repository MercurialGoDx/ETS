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
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

                foreach (var tab in BalanceSheets.TabNames)
                {
                    EditorUtility.DisplayProgressBar("Balance", $"Скачивание вкладки {tab}…", 0.5f);

                    // gviz-экспорт адресует вкладку ПО ИМЕНИ — gid не нужен.
                    // Требуется доступ «читатель по ссылке» на таблицу.
                    string url = $"https://docs.google.com/spreadsheets/d/{SheetId}/gviz/tq?tqx=out:csv&sheet={Uri.EscapeDataString(tab)}";
                    string csv = client.GetStringAsync(url).GetAwaiter().GetResult();

                    // Если доступ не открыт, Google отдаёт HTML-страницу логина.
                    if (csv.TrimStart().StartsWith("<"))
                        throw new Exception($"вкладка {tab}: вместо CSV пришёл HTML — таблица не расшарена. Открой доступ «читатель по ссылке».");

                    // Несуществующее имя вкладки gviz отдаёт как ответ с ошибкой в JS-обёртке.
                    if (csv.Contains("google.visualization.Query.setResponse"))
                        throw new Exception($"вкладка '{tab}' не найдена в таблице — проверь имена вкладок (нужны: {string.Join(", ", BalanceSheets.TabNames)}).");

                    Directory.CreateDirectory(ConfigDir);
                    File.WriteAllText(Path.Combine(ConfigDir, tab + ".csv"), csv, new UTF8Encoding(false));
                }
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[Balance] Скачивание не удалось: {e.Message}. Можно импортировать локальные CSV: Tools → Balance → Import (Local CSV).");
                return;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Import(writeChanges: true);
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
            BalanceAssetWriter.WriteBalanceConfig(sheets);
            BalanceJsonWriter.Write(sheets);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            foreach (var issue in writeIssues)
                Debug.LogError("[Balance] " + issue);

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
