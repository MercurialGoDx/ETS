using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ETS.BalanceImport
{
    /// <summary>Одна вкладка таблицы: заголовки + строки. Всё строками, типизация — в валидаторе.</summary>
    public class SheetTable
    {
        public string Name;
        public List<string> Columns = new List<string>();
        public List<Dictionary<string, string>> Rows = new List<Dictionary<string, string>>();

        public bool HasColumn(string col) => Columns.Contains(col);
    }

    public class BalanceSheets
    {
        // Имена вкладок = имена CSV-файлов в Config/.
        public static readonly string[] TabNames = { "weapon", "enemy", "boss", "shop", "player_constant", "upgrades" };

        public Dictionary<string, SheetTable> Tabs = new Dictionary<string, SheetTable>();

        public SheetTable Weapon => Tabs.GetValueOrDefault("weapon");
        public SheetTable Enemy => Tabs.GetValueOrDefault("enemy");
        public SheetTable Boss => Tabs.GetValueOrDefault("boss");
        public SheetTable Shop => Tabs.GetValueOrDefault("shop");
        public SheetTable Player => Tabs.GetValueOrDefault("player_constant");
        public SheetTable Upgrades => Tabs.GetValueOrDefault("upgrades");

        public static BalanceSheets LoadFromDirectory(string dir, List<Issue> issues)
        {
            var sheets = new BalanceSheets();

            foreach (string tab in TabNames)
            {
                string path = Path.Combine(dir, tab + ".csv");
                if (!File.Exists(path))
                {
                    issues.Add(Issue.Error(tab, $"файл не найден: {path}"));
                    continue;
                }

                try
                {
                    sheets.Tabs[tab] = ParseCsv(tab, File.ReadAllText(path, Encoding.UTF8));
                }
                catch (Exception e)
                {
                    issues.Add(Issue.Error(tab, $"не удалось разобрать CSV: {e.Message}"));
                }
            }

            return sheets;
        }

        /// <summary>Разбор CSV: кавычки, запятые внутри кавычек, CRLF/LF.</summary>
        public static SheetTable ParseCsv(string name, string text)
        {
            var table = new SheetTable { Name = name };
            var records = SplitRecords(text);

            if (records.Count == 0)
                return table;

            // Шапку тримим: лишний пробел в ячейке заголовка иначе читался бы
            // как «нет обязательной колонки».
            table.Columns = records[0].Select(c => c.Trim()).ToList();

            for (int r = 1; r < records.Count; r++)
            {
                var rec = records[r];

                // полностью пустые строки пропускаем
                bool empty = true;
                foreach (var cell in rec)
                    if (!string.IsNullOrWhiteSpace(cell)) { empty = false; break; }
                if (empty) continue;

                var row = new Dictionary<string, string>();
                for (int c = 0; c < table.Columns.Count; c++)
                    row[table.Columns[c]] = c < rec.Count ? rec[c].Trim() : "";
                table.Rows.Add(row);
            }

            return table;
        }

        private static List<List<string>> SplitRecords(string text)
        {
            var result = new List<List<string>>();
            var record = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];

                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else field.Append(ch);
                }
                else if (ch == '"') inQuotes = true;
                else if (ch == ',') { record.Add(field.ToString()); field.Clear(); }
                else if (ch == '\r') { /* skip */ }
                else if (ch == '\n')
                {
                    record.Add(field.ToString()); field.Clear();
                    result.Add(record); record = new List<string>();
                }
                else field.Append(ch);
            }

            if (field.Length > 0 || record.Count > 0)
            {
                record.Add(field.ToString());
                result.Add(record);
            }

            return result;
        }
    }

    public enum IssueSeverity { Error, Warning }

    public class Issue
    {
        public IssueSeverity Severity;
        public string Where;   // "вкладка" или "вкладка!строка/колонка"
        public string Message;

        public static Issue Error(string where, string msg) => new Issue { Severity = IssueSeverity.Error, Where = where, Message = msg };
        public static Issue Warning(string where, string msg) => new Issue { Severity = IssueSeverity.Warning, Where = where, Message = msg };

        public override string ToString() =>
            (Severity == IssueSeverity.Error ? "ОШИБКА  " : "warning ") + $"[{Where}] {Message}";
    }

    /// <summary>Типизированное чтение ячеек с накоплением ошибок.</summary>
    public static class Cell
    {
        public static float Float(Dictionary<string, string> row, string col, string where, List<Issue> issues, float fallback = 0f)
        {
            if (!row.TryGetValue(col, out var s) || string.IsNullOrWhiteSpace(s))
            {
                issues.Add(Issue.Error(where, $"пустая ячейка '{col}' (отсутствие значения — ошибка, а не 0)"));
                return fallback;
            }
            // дизайнер может ввести и с запятой — примем оба разделителя
            s = s.Replace(',', '.');
            if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            {
                issues.Add(Issue.Error(where, $"'{col}' = '{s}' — не число"));
                return fallback;
            }
            return v;
        }

        public static int Int(Dictionary<string, string> row, string col, string where, List<Issue> issues, int fallback = 0)
        {
            float f = Float(row, col, where, issues, fallback);
            int i = (int)Math.Round(f);
            if (Math.Abs(f - i) > 0.0001f)
                issues.Add(Issue.Error(where, $"'{col}' = {f.ToString(CultureInfo.InvariantCulture)} — ожидается целое"));
            return i;
        }

        public static string Text(Dictionary<string, string> row, string col)
        {
            return row.TryGetValue(col, out var s) ? s.Trim() : "";
        }
    }
}
