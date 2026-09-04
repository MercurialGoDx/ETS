using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ETS.BalanceImport
{
    /// <summary>
    /// Индекс балансовых ассетов проекта по имени файла (= asset_name в таблице).
    /// Строится лениво и сбрасывается перед каждым импортом.
    /// </summary>
    public static class BalanceAssetIndex
    {
        private static Dictionary<string, WeaponDefinition> weapons;
        private static Dictionary<string, UpgradeBaseSO> upgrades;

        public const string WeaponRoot = "Assets/Prefab/Weapon";
        public const string UpgradeRoot = "Assets/Prefab/Upgrade";

        // Композитные компоненты — «дети» и не должны попадать в список
        // "забытых в таблице": ими управляют родительские строки.
        private static readonly string[] UpgradeExcludeFolders =
        {
            "Assets/Prefab/Upgrade/CompositeComponent",
            "Assets/Prefab/Upgrade/ShopUnlock",
            "Assets/Prefab/Upgrade/Tier3/NatureBlessing",
            "Assets/Prefab/Upgrade/Tier3/GoldMine",
        };

        // Мёртвые ассеты, сознательно не участвующие в балансе.
        private static readonly HashSet<string> UpgradeIgnore = new HashSet<string> { "Hunting_old" };

        public static void Invalidate()
        {
            weapons = null;
            upgrades = null;
        }

        public static WeaponDefinition FindWeapon(string name)
        {
            BuildIfNeeded();
            return weapons.GetValueOrDefault(name);
        }

        public static UpgradeBaseSO FindUpgrade(string name)
        {
            BuildIfNeeded();
            return upgrades.GetValueOrDefault(name);
        }

        public static IEnumerable<string> AllWeaponNames()
        {
            BuildIfNeeded();
            return weapons.Keys;
        }

        public static IEnumerable<string> AllUpgradeNames()
        {
            BuildIfNeeded();
            return upgrades.Keys;
        }

        private static void BuildIfNeeded()
        {
            if (weapons != null && upgrades != null) return;

            weapons = new Dictionary<string, WeaponDefinition>();
            foreach (var guid in AssetDatabase.FindAssets("t:WeaponDefinition", new[] { WeaponRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
                if (asset != null)
                    weapons[asset.name] = asset;
            }

            upgrades = new Dictionary<string, UpgradeBaseSO>();
            foreach (var guid in AssetDatabase.FindAssets("t:UpgradeBaseSO", new[] { UpgradeRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (UpgradeExcludeFolders.Any(f => path.StartsWith(f + "/")))
                    continue;

                var asset = AssetDatabase.LoadAssetAtPath<UpgradeBaseSO>(path);
                if (asset == null || UpgradeIgnore.Contains(asset.name))
                    continue;

                upgrades[asset.name] = asset;
            }
        }
    }

    /// <summary>Куда писать value_1/value_2/interval конкретного улучшения.</summary>
    public class ValueBinding
    {
        public string Column;    // "value_1" | "value_2" | "interval"
        public string ChildType; // null = поле на самом ассете; иначе имя класса дочернего ассета в CompositeUpgrade.upgrades
        public string Field;     // имя сериализованного поля
        public float Scale = 1f; // множитель: значение_в_ассете = значение_в_таблице * Scale

        public static ValueBinding Self(string column, string field) =>
            new ValueBinding { Column = column, Field = field };

        public static ValueBinding Child(string column, string childType, string field, float scale = 1f) =>
            new ValueBinding { Column = column, ChildType = childType, Field = field, Scale = scale };
    }

    /// <summary>
    /// Карта «asset_name → куда писать значения». Заполняется вручную при добавлении
    /// нового улучшения; валидатор ругается ошибкой, если улучшение из таблицы
    /// здесь не описано — чтобы число не потерялось молча.
    /// </summary>
    public static class UpgradeValueMap
    {
        private static readonly Dictionary<string, ValueBinding[]> Map = new Dictionary<string, ValueBinding[]>
        {
            // ---- Tier 1 ----
            ["BasicHP"] = new[] { ValueBinding.Self("value_1", "valueFlat") },
            ["BagicShield"] = new[] { ValueBinding.Self("value_1", "valueFlat") },
            ["BasicRegen"] = new[] { ValueBinding.Self("value_1", "valueFlat") },
            ["BasicSpike"] = new[] { ValueBinding.Self("value_1", "valueFlat") },
            ["PassiveGold_1Tier"] = new[] { ValueBinding.Self("value_1", "goldPerSecond") },
            ["Damage"] = new[] { ValueBinding.Self("value_1", "valuePercent") },
            ["MagicDamage"] = new[] { ValueBinding.Self("value_1", "valuePercent") },
            ["PiercingDamage"] = new[] { ValueBinding.Self("value_1", "valuePercent") },
            ["NormalDamage"] = new[] { ValueBinding.Self("value_1", "valuePercent") },
            ["ProjectileDamage"] = new[] { ValueBinding.Self("value_1", "valuePercent") },
            ["HeavyDamage"] = new[] { ValueBinding.Self("value_1", "valuePercent") },
            ["ChaosDamage"] = new[] { ValueBinding.Self("value_1", "valuePercent") },
            ["HolyDamage"] = new[] { ValueBinding.Self("value_1", "valuePercent") },
            ["Sacrifice"] = new[]
            {
                ValueBinding.Self("value_1", "lifeLoseValue"),
                ValueBinding.Self("value_2", "basicGold"),
            },

            // ---- Tier 2 ----
            ["BasicDamageReduction"] = new[] { ValueBinding.Self("value_1", "valuePercent") },
            ["Generator"] = new[]
            {
                ValueBinding.Self("value_1", "valuePercent"),
                ValueBinding.Self("interval", "interval"),
            },
            ["HealOnKill"] = new[] { ValueBinding.Self("value_1", "valueFlat") },
            ["HpPerTick"] = new[]
            {
                ValueBinding.Self("value_1", "valueFlat"),
                ValueBinding.Self("interval", "interval"),
            },
            ["ShieldPerTick"] = new[]
            {
                ValueBinding.Self("value_1", "valueFlat"),
                ValueBinding.Self("interval", "interval"),
            },
            ["ShieldRestorePerKill"] = new[] { ValueBinding.Self("value_1", "valueFlat") },
            ["DeathMask"] = new[]
            {
                ValueBinding.Child("value_1", "SpikesBaseUpgrade", "valueFlat"),
                ValueBinding.Child("value_2", "HealPerEnemyHitUpgrade", "valueFlat"),
            },
            ["GoldenSkull"] = new[] { ValueBinding.Self("value_1", "valuePercent") },
            ["Heal_ampl"] = new[] { ValueBinding.Self("value_1", "valuePercent") },
            ["SpikeT2"] = new[]
            {
                ValueBinding.Child("value_1", "SpikesBaseUpgrade", "valueFlat"),
                ValueBinding.Child("value_2", "SpikesPercentUpgrade", "valuePercent"),
            },

            // ---- Tier 3 ----
            ["BlessedArmor"] = new[]
            {
                ValueBinding.Self("value_1", "startScorePercent"),
                ValueBinding.Self("value_2", "lossScorePerHitPercent"),
            },
            ["CursedArmor"] = new[] { ValueBinding.Self("value_1", "valuePercent") },
            ["AttackSpeed"] = new[] { ValueBinding.Self("value_1", "valuePercent") },
            ["Block"] = new[] { ValueBinding.Self("value_1", "valuePercent") },
            ["IncreaceHpRegPercent"] = new[] { ValueBinding.Self("value_1", "valuePercent") },
            ["RegenPerTick"] = new[]
            {
                ValueBinding.Self("value_1", "valueFlat"),
                ValueBinding.Self("interval", "interval"),
            },
            ["Shield_Multiply"] = new[] { ValueBinding.Self("value_1", "valuePercent") },
            ["SuperBullet"] = new[] { ValueBinding.Self("value_1", "valuePercent") },
            ["NatureBlessing"] = new[]
            {
                ValueBinding.Child("value_1", "MaxHealthFlatUpgrade", "valueFlat"),
                ValueBinding.Child("value_2", "RegenPerMissingHealthUpgrade", "valuePercent"),
            },
            ["SpikeScaling"] = new[]
            {
                ValueBinding.Child("value_1", "SpikesBaseUpgrade", "valueFlat"),
                ValueBinding.Child("value_2", "SpikesScalingOnKillUpgrade", "valueFlat"),
            },
            ["GoldMine"] = new[]
            {
                ValueBinding.Child("value_1", "GoldPerSecondPercentUpgrade", "valuePercent"),
                ValueBinding.Child("value_2", "GoldPerSecondUpgrade", "goldPerSecond"),
            },
            ["Hunt"] = new ValueBinding[0],
            ["Duplicator"] = new ValueBinding[0],
            ["Boss Reward Reroll"] = new[]
            {
                ValueBinding.Self("value_1", "rerollsPerPurchase"),
                ValueBinding.Self("value_2", "priceIncreasePerPurchase"),
            },

            // ---- Tier 4 ----
            // valuePercentPerWeapon хранится долей: 0.02 в ассете = +2% за оружие → scale 0.01
            ["NormalDamageUltimate"] = Ultimate(),
            ["PiercingDamageUltimate"] = Ultimate(),
            ["ProjectileDamageUltimate"] = Ultimate(),
            ["MagicDamageUltimate"] = Ultimate(),
            ["HeavyDamageUltimate"] = Ultimate(),
            ["ChaosDamageUltimate"] = Ultimate(),
            ["HolyDamageUltimate"] = Ultimate(),
            ["Complex"] = new[] { ValueBinding.Self("value_1", "valuePercent") },
            ["DamageWhileShieldActive"] = new[]
            {
                ValueBinding.Child("value_1", "MaxShieldFlatUpgrade", "valueFlat"),
                ValueBinding.Child("value_2", "AdaptiveDamageWhileShield", "valuePercent"),
            },
            ["DegenAura"] = new[] { ValueBinding.Self("value_1", "valueFlat") },
            ["UltimateHp"] = new[]
            {
                ValueBinding.Child("value_1", "MaxHealthFlatUpgrade", "valueFlat"),
                ValueBinding.Child("value_2", "MaxHealthPercentUpgrade", "percentValue"),
                ValueBinding.Child("value_3", "DamageReductionUpgrade", "valuePercent"),
            },
            ["Spike_t4"] = new[]
            {
                ValueBinding.Child("value_1", "SpikesBaseUpgrade", "valueFlat"),
                ValueBinding.Child("value_2", "SpikeEnemyAttackStacking", "valuePercent"),
            },
            ["SpikeUltimate"] = new[]
            {
                ValueBinding.Child("value_1", "SpikesPercentUpgrade", "valuePercent"),
                ValueBinding.Child("value_2", "BossDamageMultiply", "valuePercent"),
            },
            ["BossContract"] = new ValueBinding[0],
            ["MytrhillMaterial"] = new[]
            {
                ValueBinding.Child("value_1", "MaxHealthPercentUpgrade", "percentValue"),
                ValueBinding.Child("value_2", "DamageFromMaxHealthPercentUpgrade", "valuePercent"),
            },
        };

        private static ValueBinding[] Ultimate() => new[]
        {
            ValueBinding.Child("value_1", "DamageTypePercentUpgrade", "valuePercent"),
            ValueBinding.Child("value_2", "DamagePerWeaponUpgrade", "valuePercentPerWeapon", 0.01f),
        };

        public static ValueBinding[] Get(string assetName) => Map.GetValueOrDefault(assetName);
    }
}
