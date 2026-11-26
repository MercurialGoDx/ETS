using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ETS.Mobile.Editor.Tests
{
    /// <summary>
    /// Страховочные EditMode-тесты порта (docs/MobilePlan.md, Фаза 1): то, что порт поменял
    /// или на что опирается, — пул, локальные рекорды и формат снимка билда.
    /// Лежат в Assembly-CSharp-Editor, потому что игровой код без asmdef и из отдельной
    /// тестовой сборки недоступен.
    /// </summary>
    public class ObjectPoolTests
    {
        private GameObject prefabObject;
        private BoxCollider prefab;
        private Transform parent;

        [SetUp]
        public void SetUp()
        {
            prefabObject = new GameObject("PoolTestPrefab");
            prefab = prefabObject.AddComponent<BoxCollider>();
            parent = new GameObject("PoolTestParent").transform;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(parent.gameObject);
            Object.DestroyImmediate(prefabObject);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(4)]
        public void Get_WithoutRelease_ReturnsDistinctActiveObjects(int initialSize)
        {
            var pool = new ObjectPool<BoxCollider>(prefab, initialSize, parent);
            var handedOut = new HashSet<BoxCollider>();

            for (int i = 0; i < initialSize + 3; i++)
            {
                BoxCollider obj = pool.Get();
                Assert.IsTrue(obj.gameObject.activeSelf);
                Assert.IsTrue(handedOut.Add(obj), "Пул выдал один и тот же живой объект дважды.");
            }
        }

        [Test]
        public void Prefill_CreatesExactlyInitialSizeInactiveObjects()
        {
            new ObjectPool<BoxCollider>(prefab, 4, parent);

            Assert.AreEqual(4, parent.childCount);
            foreach (Transform child in parent)
                Assert.IsFalse(child.gameObject.activeSelf);
        }

        [Test]
        public void Release_ThenGet_ReusesObjectWithoutInstantiate()
        {
            var pool = new ObjectPool<BoxCollider>(prefab, 1, parent);
            BoxCollider first = pool.Get();
            pool.Release(first);

            BoxCollider second = pool.Get();

            Assert.AreSame(first, second);
            Assert.AreEqual(1, parent.childCount);
        }

        [Test]
        public void Get_SkipsDestroyedFreeObject()
        {
            var pool = new ObjectPool<BoxCollider>(prefab, 1, parent);
            BoxCollider obj = pool.Get();
            pool.Release(obj);
            Object.DestroyImmediate(obj.gameObject);

            BoxCollider replacement = pool.Get();

            Assert.IsTrue(replacement != null);
            Assert.IsTrue(replacement.gameObject.activeSelf);
        }
    }

    public class LocalLeaderboardServiceTests
    {
        private string path;

        [SetUp]
        public void SetUp()
        {
            path = Path.Combine(Path.GetTempPath(), "ets_local_leaderboard_test_" + System.Guid.NewGuid().ToString("N") + ".json");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
        }

        [Test]
        public void GetTop_ReturnsRunsSortedByTimeWithRanks()
        {
            var service = new LocalLeaderboardService(path);
            service.SubmitTime(120f);
            service.SubmitTime(300f);
            service.SubmitTime(200f);

            List<LeaderboardEntry> top = null;
            service.GetTop(10, entries => top = entries);

            Assert.AreEqual(3, top.Count);
            Assert.AreEqual(300f, top[0].timeSeconds);
            Assert.AreEqual(200f, top[1].timeSeconds);
            Assert.AreEqual(120f, top[2].timeSeconds);
            Assert.AreEqual(1, top[0].rank);
            Assert.AreEqual(3, top[2].rank);
        }

        [Test]
        public void Runs_SurviveRestartWithBuildSnapshot()
        {
            var snapshot = new BuildSnapshot();
            snapshot.items.Add(new BuildSnapshot.Item(3, 2));
            snapshot.SetStat(BuildStat.Health, 750f);
            new LocalLeaderboardService(path).SubmitTime(95.5f, snapshot.Encode());

            List<LeaderboardEntry> top = null;
            new LocalLeaderboardService(path).GetTop(10, entries => top = entries);

            Assert.AreEqual(1, top.Count);
            Assert.AreEqual(95.5f, top[0].timeSeconds, 0.001f);
            BuildSnapshot restored = BuildSnapshot.Decode(top[0].details);
            Assert.IsNotNull(restored);
            Assert.AreEqual(750f, restored.GetStat(BuildStat.Health), 0.01f);
        }

        [Test]
        public void GetTop_RespectsRequestedCount()
        {
            var service = new LocalLeaderboardService(path);
            for (int i = 0; i < 15; i++)
                service.SubmitTime(i);

            List<LeaderboardEntry> top = null;
            service.GetTop(10, entries => top = entries);

            Assert.AreEqual(10, top.Count);
            Assert.AreEqual(14f, top[0].timeSeconds);
        }

        [Test]
        public void CorruptFile_StartsEmptyInsteadOfThrowing()
        {
            File.WriteAllText(path, "{ not json");
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;

            List<LeaderboardEntry> top = null;
            new LocalLeaderboardService(path).GetTop(10, entries => top = entries);

            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            Assert.IsNotNull(top);
            Assert.AreEqual(0, top.Count);
        }
    }

    public class BuildSnapshotTests
    {
        [Test]
        public void EncodeDecode_RoundTripsItemsAndStats()
        {
            var snapshot = new BuildSnapshot();
            snapshot.items.Add(new BuildSnapshot.Item(0, 5));
            snapshot.items.Add(new BuildSnapshot.Item(12, 1));
            snapshot.SetStat(BuildStat.AttackSpeed, 1.25f);
            snapshot.SetStat(BuildStat.DamageMultiplier, 2.5f);

            int[] encoded = snapshot.Encode();
            BuildSnapshot decoded = BuildSnapshot.Decode(encoded);

            Assert.LessOrEqual(encoded.Length, BuildSnapshot.MaxDetails);
            Assert.AreEqual(2, decoded.items.Count);
            Assert.AreEqual(12, decoded.items[1].id);
            Assert.AreEqual(1, decoded.items[1].count);
            Assert.AreEqual(1.25f, decoded.GetStat(BuildStat.AttackSpeed), 0.01f);
            Assert.AreEqual(2.5f, decoded.GetStat(BuildStat.DamageMultiplier), 0.01f);
        }

        [Test]
        public void Encode_TrimsOversizedBuildToSteamLimit()
        {
            var snapshot = new BuildSnapshot();
            for (int i = 0; i < 100; i++)
                snapshot.items.Add(new BuildSnapshot.Item(i, i + 1));

            Assert.LessOrEqual(snapshot.Encode().Length, BuildSnapshot.MaxDetails);
        }

        [Test]
        public void Decode_EmptyDetails_ReturnsNull()
        {
            Assert.IsNull(BuildSnapshot.Decode(null));
            Assert.IsNull(BuildSnapshot.Decode(new int[0]));
        }
    }

    public class NicknameRulesTests
    {
        [TestCase("Vadim", "Vadim")]
        [TestCase("  Вадим  ", "Вадим")]
        [TestCase("Tower  Boss", "Tower Boss")]
        [TestCase("a_b-c.d", "a_b-c.d")]
        public void TryNormalize_AcceptsAndCleansValidNames(string input, string expected)
        {
            Assert.IsTrue(NicknameRules.TryNormalize(input, out string nickname));
            Assert.AreEqual(expected, nickname);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("ab")]
        [TestCase("12345678901234567")]
        [TestCase("bad<name>")]
        [TestCase("emoji😀")]
        public void TryNormalize_RejectsInvalidNames(string input)
        {
            Assert.IsFalse(NicknameRules.TryNormalize(input, out _));
        }
    }
}
