namespace ETS.Multiplayer
{
    /// <summary>
    /// Независимые потоки случайности внутри дуэли. Значения зашиты в сценарий матча —
    /// менять нельзя, можно только дописывать в конец.
    /// </summary>
    public enum DuelStream
    {
        ShopWeapon = 1,
        ShopUpgrade = 2,
        BossPick = 3,
        BossReward = 4,
    }

    /// <summary>
    /// Случайность дуэли: чистая функция от (сид, поток, индекс).
    ///
    /// Состояния нет намеренно. Последовательный генератор пришлось бы расходовать строго в
    /// одном порядке, а порядок зависит от действий игрока — один лишний реролл, и клиенты
    /// разъехались. Здесь каждый бросок адресуется логическим индексом, поэтому «оффер слота 2
    /// на ролле 7» и «награда третьего босса» одинаковы у обоих независимо от того, что
    /// происходило между этими моментами.
    ///
    /// UnityEngine.Random не годится: Unity не гарантирует свой алгоритм между версиями, а
    /// сценарий матча должен пережить обновление движка. Здесь SplitMix64 на целочисленной
    /// арифметике — одинаков на любой платформе.
    /// </summary>
    public static class DuelRandom
    {
        private const ulong Gamma = 0x9E3779B97F4A7C15UL;
        private const ulong MixA = 0xBF58476D1CE4E5B9UL;
        private const ulong MixB = 0x94D049BB133111EBUL;

        public static ulong Bits(int seed, DuelStream stream, int index)
        {
            ulong h = Mix((ulong)(uint)seed);
            h = Mix(h ^ (ulong)(long)stream);
            h = Mix(h ^ (ulong)(uint)index);
            return h;
        }

        /// <summary>Дробное в [0, 1). Берём 24 старших бита — ровно мантисса float.</summary>
        public static float Value01(int seed, DuelStream stream, int index)
        {
            return (uint)(Bits(seed, stream, index) >> 40) * (1f / 16777216f);
        }

        /// <summary>
        /// Целое из [min, maxExclusive). Свёртка умножением, а не остатком от деления:
        /// остаток смещает результат к младшим значениям, а на пулах в три-пять элементов
        /// перекос заметен.
        /// </summary>
        public static int Range(int seed, DuelStream stream, int index, int min, int maxExclusive)
        {
            if (maxExclusive <= min)
                return min;

            ulong span = (ulong)(uint)(maxExclusive - min);
            return min + (int)(((Bits(seed, stream, index) >> 32) * span) >> 32);
        }

        /// <summary>Складывает два индекса в один: слот внутри ролла, карта внутри раздачи.</summary>
        public static int Compose(int outer, int inner)
        {
            unchecked { return outer * 65599 + inner; }
        }

        /// <summary>
        /// Выбор по весам: индекс в списке или -1, если выбирать не из чего.
        /// Веса берутся базовые, без надбавок за прошлые покупки — иначе у двух игроков
        /// с разными покупками разъедутся сами предложения.
        /// </summary>
        public static int WeightedPick(int seed, DuelStream stream, int index, System.Collections.Generic.IReadOnlyList<float> weights)
        {
            if (weights == null || weights.Count == 0)
                return -1;

            float total = 0f;
            for (int i = 0; i < weights.Count; i++)
                if (weights[i] > 0f)
                    total += weights[i];

            if (total <= 0f)
                return -1;

            float roll = Value01(seed, stream, index) * total;
            float acc = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] <= 0f)
                    continue;

                acc += weights[i];
                if (roll < acc)
                    return i;
            }

            // Досюда доводит только накопленная погрешность сложения float.
            for (int i = weights.Count - 1; i >= 0; i--)
                if (weights[i] > 0f)
                    return i;

            return -1;
        }

        private static ulong Mix(ulong value)
        {
            unchecked
            {
                ulong z = value + Gamma;
                z = (z ^ (z >> 30)) * MixA;
                z = (z ^ (z >> 27)) * MixB;
                return z ^ (z >> 31);
            }
        }
    }
}
