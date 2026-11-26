using System.Text;
using UnityEngine;

/// <summary>
/// Ник игрока для онлайн-таблицы: хранение и проверка. Те же ограничения стоят в правилах
/// Firestore (Firebase/firestore.rules), так что клиент лишь не даёт отправить заведомо
/// отклонённую запись.
/// </summary>
public static class NicknameRules
{
    public const int MinLength = 3;
    public const int MaxLength = 16;
    private const string PrefKey = "leaderboard_nickname";

    public static string Saved
    {
        get => PlayerPrefs.GetString(PrefKey, string.Empty);
        set
        {
            PlayerPrefs.SetString(PrefKey, value ?? string.Empty);
            PlayerPrefs.Save();
        }
    }

    public static bool HasSaved => !string.IsNullOrEmpty(Saved);

    /// <summary>
    /// Обрезает пробелы по краям и схлопывает повторные. Разрешены буквы любого алфавита,
    /// цифры, пробел, «_», «-» и «.» — без эмодзи и управляющих символов, которых нет в шрифтах игры.
    /// </summary>
    public static bool TryNormalize(string input, out string nickname)
    {
        nickname = null;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var builder = new StringBuilder(input.Length);
        bool previousSpace = false;
        foreach (char c in input.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                if (!previousSpace)
                    builder.Append(' ');
                previousSpace = true;
                continue;
            }

            if (!char.IsLetterOrDigit(c) && c != '_' && c != '-' && c != '.')
                return false;

            builder.Append(c);
            previousSpace = false;
        }

        string result = builder.ToString();
        if (result.Length < MinLength || result.Length > MaxLength)
            return false;

        nickname = result;
        return true;
    }
}
