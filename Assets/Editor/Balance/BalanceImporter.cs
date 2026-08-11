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
        // ID таблицы "ETS Config" на Google Drive.
        private const string SheetId = "1MPre5L9U05qZTA9R96mDZbdJjf7dTObvujPIEaeapYs";

        // gid каждой вкладки — виден в URL при переключении вкладок в браузере
        // (#gid=123456789). После добавления вкладок в таблицу проставить здесь.
        // -1 = неизвестен, Pull откажется качать, пока не заполнено.
        private static readonly Dictionary<string, long> TabGids = new Dictionary<string, long>
        {
            ["weapon"] = 0,
            ["enemy"] = -1,
            ["boss"] = -1,
            ["shop"] = -1,
            ["player_constant"] = -1,
            ["upgrades"] = -1,
        };

        private static string ConfigDir => Path.Combine(Directory.GetCurrentDirectory(), "Config");

        // ---------------- меню ----------------

        [MenuItem("Tools/Balance/Pull From Google Sheets + Import")]
        public static void PullAndImport()
        {
            var missing = TabGids.Where(kv => kv.Value < 0).Select(kv => kv.Key).ToList();
            if (missing.Count > 0)
            {
                EditorUtility.DisplayDialog("Balance",
                    "Не заполнены gid вкладок: " + string.Join(", ", missing) +
                    "\n\nОткрой таблицу в браузере, переключись на вкладку и скопируй число из URL (#gid=...) в TabGids (BalanceImporter.cs).",
                    "Ок");
                return;
            }

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

                foreach (var (tab, gid) in TabGids)
                {
                    EditorUtility.DisplayProgressBar("Balance", $"Скачивание вкладки {tab}…", 0.5f);

                    string url = $"https://docs.google.com/spreadsheets/d/{SheetId}/export?format=csv&gid={gid}";
                    string csv = client.GetStringAsync(url).GetAwaiter().GetResult();

                    // Если доступ не открыт, Google отдаёт HTML-страницу логина.
                    if (csv.TrimStart().StartsWith("<"))
                        throw new Exception($"вкладка {tab}: вместо CSV пришёл HTML — таблица не расшарена. Открой доступ «читатель по ссылке».");

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
