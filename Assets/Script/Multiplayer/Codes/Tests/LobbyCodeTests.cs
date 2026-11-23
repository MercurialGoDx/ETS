using System;
using NUnit.Framework;

namespace ETS.Multiplayer.Tests
{
    public class LobbyCodeTests
    {
        private static Func<int, int> Sequence(params int[] values)
        {
            int cursor = 0;
            return bound => values[cursor++ % values.Length];
        }

        [Test]
        public void Alphabet_HasNoConfusableCharacters()
        {
            foreach (char c in "OIL01258SBZ")
                Assert.AreEqual(-1, LobbyCode.Alphabet.IndexOf(c), $"символ '{c}' легко спутать, ему не место в алфавите");
        }

        [Test]
        public void Alphabet_HasNoDuplicates()
        {
            for (int i = 0; i < LobbyCode.Alphabet.Length; i++)
            {
                Assert.AreEqual(i, LobbyCode.Alphabet.IndexOf(LobbyCode.Alphabet[i]),
                    $"символ '{LobbyCode.Alphabet[i]}' встречается дважды");
            }
        }

        [Test]
        public void Generate_ProducesValidCodeOfRightLength()
        {
            string code = LobbyCode.Generate(Sequence(0, 1, 2, 3, 4, 5));

            Assert.AreEqual(LobbyCode.Length, code.Length);
            Assert.IsTrue(LobbyCode.IsValid(code), $"сгенерированный код '{code}' не проходит собственную проверку");
        }

        [Test]
        public void Generate_IsAlwaysValidForAnyIndex()
        {
            var random = new Random(7);
            for (int i = 0; i < 500; i++)
            {
                string code = LobbyCode.Generate(bound => random.Next(bound));
                Assert.IsTrue(LobbyCode.IsValid(code), $"код '{code}' невалиден");
            }
        }

        [Test]
        public void Generate_ClampsOutOfRangeIndex()
        {
            // Чужой источник случайности может вернуть что угодно — код всё равно обязан быть валидным.
            Assert.IsTrue(LobbyCode.IsValid(LobbyCode.Generate(Sequence(-100))));
            Assert.IsTrue(LobbyCode.IsValid(LobbyCode.Generate(Sequence(int.MaxValue))));
        }

        [Test]
        public void Generate_RejectsNullSource()
        {
            Assert.Throws<ArgumentNullException>(() => LobbyCode.Generate(null));
        }

        [Test]
        public void Normalize_IgnoresCaseAndSeparators()
        {
            string expected = LobbyCode.Alphabet.Substring(0, 6);

            Assert.AreEqual(expected, LobbyCode.Normalize(expected.ToLowerInvariant()));
            Assert.AreEqual(expected, LobbyCode.Normalize($"{expected.Substring(0, 3)}-{expected.Substring(3)}"));
            Assert.AreEqual(expected, LobbyCode.Normalize($" {expected} "));
            Assert.AreEqual(expected, LobbyCode.Normalize($"{expected.Substring(0, 2)}_{expected.Substring(2)}"));
        }

        [Test]
        public void Normalize_RejectsBadInput()
        {
            Assert.IsNull(LobbyCode.Normalize(null), "null");
            Assert.IsNull(LobbyCode.Normalize(""), "пустая строка");
            Assert.IsNull(LobbyCode.Normalize("ACDEF"), "короткий код");
            Assert.IsNull(LobbyCode.Normalize("ACDEFGH"), "длинный код");
            Assert.IsNull(LobbyCode.Normalize("ACDEF0"), "ноль вне алфавита");
            Assert.IsNull(LobbyCode.Normalize("ACDEFO"), "буква O вне алфавита");
            Assert.IsNull(LobbyCode.Normalize("ACDEF!"), "спецсимвол");
            Assert.IsNull(LobbyCode.Normalize("АCDEFG"), "кириллическая А вместо латинской");
        }

        [Test]
        public void Normalize_IsIdempotent()
        {
            var random = new Random(11);
            for (int i = 0; i < 200; i++)
            {
                string code = LobbyCode.Generate(bound => random.Next(bound));
                Assert.AreEqual(code, LobbyCode.Normalize(code));
                Assert.AreEqual(code, LobbyCode.Normalize(LobbyCode.Normalize(code)));
            }
        }

        [Test]
        public void CodeSpace_IsLargeEnoughForConcurrentLobbies()
        {
            double space = Math.Pow(LobbyCode.Alphabet.Length, LobbyCode.Length);
            Assert.Greater(space, 1e8, $"пространство кодов {space:N0} мало — коллизии станут заметны");
        }
    }
}
