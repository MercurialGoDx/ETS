using System.Globalization;
using UnityEngine;
using UnityEngine.Localization.Settings;

/// <summary>
/// Общее форматирование чисел для панелей статов (InventoryUI — своя живая панель,
/// BuildViewerUI — read-only просмотр чужого билда с лидерборда). Вынесено, чтобы
/// обе панели показывали одинаковые строки одинаково.
/// </summary>
public static class StatFormat
{
    private const float Eps = 0.0001f;
    private const string LocTable = "Game Labels";

    // Разделитель тысяч — неразрывный пробел: выглядит как пробел, но не даёт
    // разорвать число, даже если где-то включится перенос.
    private static readonly NumberFormatInfo NumFormat = new NumberFormatInfo
    {
        NumberGroupSeparator = " ",
        NumberDecimalSeparator = ".",
    };

    /// <summary>Синхронный лукап локализованной строки из таблицы "Game Labels".</summary>
    public static string L(string key)
    {
        return LocalizationSettings.StringDatabase.GetLocalizedString(LocTable, key);
    }

    /// <summary>Прочерк вместо +0%: видно, куда ещё можно вложиться.</summary>
    public static string Percent(float fraction)
    {
        return fraction > Eps ? "+" + Mathf.RoundToInt(fraction * 100f) + "%" : "—";
    }

    /// <summary>Множитель в виде ×1.00 — всегда два знака, чтобы колонка не «дышала».</summary>
    public static string Multiplier(float value)
    {
        return "×" + value.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>Целое с разделителем тысяч: 22 190.</summary>
    public static string Int(float value)
    {
        return Mathf.RoundToInt(value).ToString("#,0", NumFormat);
    }

    /// <summary>Пара «текущее/максимум» без пробелов вокруг слэша. Нулевой максимум — прочерк.</summary>
    public static string Pair(float current, float max)
    {
        return max > Eps ? Int(current) + "/" + Int(max) : "—";
    }

    // Инвариантная культура: на русской локали ОС "0.#" дал бы запятую.
    public static string Num(float value)
    {
        return value > Eps ? value.ToString("0.#", CultureInfo.InvariantCulture) : "—";
    }

    /// <summary>Значение в секунду. Ноль — прочерк, а не «0/с».</summary>
    public static string Rate(float value)
    {
        return value > Eps ? Num(value) + L("inv.per_second") : "—";
    }
}
