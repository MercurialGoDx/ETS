using System;
using System.Text;

namespace ETS.Multiplayer
{
    /// <summary>
    /// Короткий код лобби, который игрок диктует голосом или пересылает в чат.
    ///
    /// Алфавит без визуально спорных символов: выброшены O и 0, I и L и 1, S и 5, B и 8,
    /// Z и 2. Код, который нельзя продиктовать вслух без переспрашивания, свою задачу
    /// не выполняет.
    ///
    /// Символы вне алфавита не угадываются, а отклоняются: молча подменить чужой код
    /// и увести игрока не в то лобби хуже, чем показать понятную ошибку ввода.
    /// </summary>
    public static class LobbyCode
    {
        /// <summary>25 символов: латиница и цифры без визуально спорных.</summary>
        public const string Alphabet = "ACDEFGHJKMNPQRTUVWXY34679";

        public const int Length = 6;

        /// <summary>
        /// Код из внешнего источника случайности: <paramref name="nextInRange"/> получает
        /// размер алфавита и возвращает индекс. Источник передаётся снаружи, чтобы класс
        /// оставался чистым и тестируемым.
        /// </summary>
        public static string Generate(Func<int, int> nextInRange)
        {
            if (nextInRange == null)
                throw new ArgumentNullException(nameof(nextInRange));

            var builder = new StringBuilder(Length);
            for (int i = 0; i < Length; i++)
            {
                int index = nextInRange(Alphabet.Length);
                if (index < 0)
                    index = 0;
                else if (index >= Alphabet.Length)
                    index = Alphabet.Length - 1;

                builder.Append(Alphabet[index]);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Приводит введённое игроком к каноническому виду. Возвращает null, если после
        /// очистки код не является валидным — вызывающий код обязан это проверить, а не
        /// слать мусор в фильтр Steam.
        /// </summary>
        public static string Normalize(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return null;

            var builder = new StringBuilder(Length);

            for (int i = 0; i < raw.Length; i++)
            {
                char c = char.ToUpperInvariant(raw[i]);

                // Разделители, которые игроки вставляют сами.
                if (c == ' ' || c == '-' || c == '_')
                    continue;

                if (Alphabet.IndexOf(c) < 0)
                    return null;

                if (builder.Length == Length)
                    return null;

                builder.Append(c);
            }

            return builder.Length == Length ? builder.ToString() : null;
        }

        public static bool IsValid(string code)
        {
            return Normalize(code) != null;
        }
    }
}
