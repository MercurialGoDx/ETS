using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ETS.BalanceImport
{
    /// <summary>
    /// Генерация Assets/GameData/balance.json — читаемого снапшота баланса.
    /// Файл нужен для ревью (diff в PR) и как машиночитаемый срез; игра его
    /// не читает — рантайм ходит в BalanceConfig и ассеты.
    /// </summary>
    public static class BalanceJsonWriter
    {
        public const string JsonPath = "Assets/GameData/balance.json";

        [Serializable] public class Root
        {
            public int version = 1;
            public ItemJson[] items;
            public UpgradeJson[] upgrades;
            public EnemiesJson enemies = new EnemiesJson();
            public BossJson boss = new BossJson();
            public WavesJson waves = new WavesJson();
            public EconomyJson economy = new EconomyJson();
            public PlayerJson player = new PlayerJson();
        }

        [Serializable] public class ItemJson
        {
            public string name; public int id; public float attack_speed; public float damage;
            public int tier; public float weight; public int cost;
            public string damage_type; public float projectile_speed; public string asset;
        }

        [Serializable] public class UpgradeJson
        {
            public string name; public int id; public int tier; public float weight; public int cost;
            public string effect_1; public float value_1;
            public string effect_2; public float value_2;
            public string effect_3; public float value_3;
            public float interval; public string asset;
        }

        [Serializable] public class EnemyStatJson { public float damage; public float hp; public int gold; public float speed; }

        [Serializable] public class EnemiesJson
        {
            public EnemyStatJson melee = new EnemyStatJson();
            public EnemyStatJson mid = new EnemyStatJson();
            public EnemyStatJson range = new EnemyStatJson();
            public float attack_interval;
            public int damage_growth_start_after_attacks;
            public float damage_growth_percent_per_attack;
        }

        [Serializable] public class BossJson
        {
            public float additional_damage_boss;
            public float damage_boss;
            public float hp_boss;
            public float hp_multiplier;
            public int gold;
            public float speed;
            public float attack_interval; public float attack_range;
            public float spawn_interval;
            public string reference_enemy;
        }

        [Serializable] public class WavesJson
        {
            public float delay_per_wave; public int enemy_per_wave;
            public float health_difficult_start; public float damage_difficult_start;
            public float growth_stage1_multiplier_health; public float growth_stage1_multiplier_damage;
            public float growth_stage2_multiplier_health; public float growth_stage2_multiplier_damage;
            public float growth_stage3_multiplier_health; public float growth_stage3_multiplier_damage;
            public float time_difficult_stage1; public float time_difficult_stage2; public float time_difficult_stage3;
            public float hp_add_per_wave; public float damage_add_per_wave;
        }

        [Serializable] public class EconomyJson
        {
            public int first_upgrade_cost; public int second_upgrade_cost;
            public int reroll_base_cost; public int reroll_cost_increase;
            public float auto_reroll_interval; public int start_gold;
            public int passive_gold_per_tick; public float passive_income_interval;
        }

        [Serializable] public class PlayerJson
        {
            public float hp; public float shield; public float block_cap;
            public float shield_recharge_time; public float shield_full_restore_after_no_damage;
            public float tower_range; public float volley_window_percent;
            public float game_speed_1; public float game_speed_2; public float game_speed_3;
        }

        public static void Write(BalanceSheets sheets)
        {
            var root = new Root();

            root.items = sheets.Weapon.Rows.Select(r => new ItemJson
            {
                name = Cell.Text(r, "name"),
                id = I(r["id"]),
                attack_speed = F(r["attack_speed"]),
                damage = F(r["damage"]),
                tier = I(r["tier"]),
                weight = F(r["weight"]),
                cost = I(r["cost"]),
                damage_type = Cell.Text(r, "damage_type"),
                projectile_speed = F(r["projectile_speed"]),
                asset = Cell.Text(r, "asset_name"),
            }).OrderBy(x => x.id).ToArray();

            root.upgrades = sheets.Upgrades.Rows.Select(r => new UpgradeJson
            {
                name = Cell.Text(r, "name"),
                id = I(r["id"]),
                tier = I(r["tier"]),
                weight = F(r["weight"]),
                cost = I(r["cost"]),
                effect_1 = Cell.Text(r, "effect_1"),
                value_1 = FOpt(r, "value_1"),
                effect_2 = Cell.Text(r, "effect_2"),
                value_2 = FOpt(r, "value_2"),
                effect_3 = Cell.Text(r, "effect_3"),
                value_3 = FOpt(r, "value_3"),
                interval = FOpt(r, "interval"),
                asset = Cell.Text(r, "asset_name"),
            }).OrderBy(x => x.id).ToArray();

            var e = sheets.Enemy.Rows[0];
            root.enemies.melee = Stat(e, "damage_melee", "hp_melee", "speed_melee", "gold_for_enemy");
            root.enemies.mid = Stat(e, "damage_mid", "hp_mid", "speed_mid", "gold_for_enemy");
            root.enemies.range = Stat(e, "damage_range", "hp_range", "speed_range", "gold_for_enemy");
            root.enemies.attack_interval = F(e["attack_interval"]);
            root.enemies.damage_growth_start_after_attacks = I(e["damage_growth_start_after_attacks"]);
            root.enemies.damage_growth_percent_per_attack = F(e["damage_growth_percent_per_attack"]);

            var b = sheets.Boss.Rows[0];
            root.boss.additional_damage_boss = F(b["additional_damage_boss"]);
            root.boss.damage_boss = F(b["damage_boss"]);
            root.boss.hp_boss = F(b["hp_boss"]);
            root.boss.hp_multiplier = F(b["hp_multiplier"]);
            root.boss.gold = I(b["gold_for_boss"]);
            root.boss.speed = F(b["speed_boss"]);
            root.boss.attack_interval = F(b["attack_interval"]);
            root.boss.attack_range = F(b["attack_range"]);
            root.boss.spawn_interval = F(b["spawn_interval"]);
            root.boss.reference_enemy = b["reference_enemy"];

            root.waves.delay_per_wave = F(e["delay_per_wave"]);
            root.waves.enemy_per_wave = I(e["enemy_per_wave"]);
            root.waves.health_difficult_start = F(e["health_difficult_start"]);
            root.waves.damage_difficult_start = F(e["damage_difficult_start"]);
            root.waves.growth_stage1_multiplier_health = F(e["growth_stage1_multiplier_health"]);
            root.waves.growth_stage1_multiplier_damage = F(e["growth_stage1_multiplier_damage"]);
            root.waves.growth_stage2_multiplier_health = F(e["growth_stage2_multiplier_health"]);
            root.waves.growth_stage2_multiplier_damage = F(e["growth_stage2_multiplier_damage"]);
            root.waves.growth_stage3_multiplier_health = F(e["growth_stage3_multiplier_health"]);
            root.waves.growth_stage3_multiplier_damage = F(e["growth_stage3_multiplier_damage"]);
            root.waves.time_difficult_stage1 = F(e["time_difficult_stage1"]);
            root.waves.time_difficult_stage2 = F(e["time_difficult_stage2"]);
            root.waves.time_difficult_stage3 = F(e["time_difficult_stage3"]);
            root.waves.hp_add_per_wave = F(e["hp_add_per_wave"]);
            root.waves.damage_add_per_wave = F(e["damage_add_per_wave"]);

            var s = sheets.Shop.Rows[0];
            root.economy.first_upgrade_cost = I(s["first_upgrade_cost"]);
            root.economy.second_upgrade_cost = I(s["second_upgrade_cost"]);
            root.economy.reroll_base_cost = I(s["reroll_base_cost"]);
            root.economy.reroll_cost_increase = I(s["reroll_cost_increase"]);
            root.economy.auto_reroll_interval = F(s["auto_reroll_interval"]);
            root.economy.start_gold = I(s["start_gold"]);
            root.economy.passive_gold_per_tick = I(s["passive_gold_per_tick"]);
            root.economy.passive_income_interval = F(s["passive_income_interval"]);

            var p = sheets.Player.Rows[0];
            root.player.hp = F(p["player_hp"]);
            root.player.shield = F(p["player_shield"]);
            root.player.block_cap = F(p["block_cap"]);
            root.player.shield_recharge_time = F(p["shield_recharge_time"]);
            root.player.shield_full_restore_after_no_damage = F(p["shield_full_restore_after_no_damage"]);
            root.player.tower_range = F(p["tower_range"]);
            root.player.volley_window_percent = F(p["volley_window_percent"]);
            root.player.game_speed_1 = F(p["game_speed_1"]);
            root.player.game_speed_2 = F(p["game_speed_2"]);
            root.player.game_speed_3 = F(p["game_speed_3"]);

            Directory.CreateDirectory(Path.GetDirectoryName(JsonPath)!);
            File.WriteAllText(JsonPath, JsonUtility.ToJson(root, true), new UTF8Encoding(false));
        }

        private static EnemyStatJson Stat(System.Collections.Generic.Dictionary<string, string> row,
            string dmg, string hp, string speed, string gold) => new EnemyStatJson
        {
            damage = F(row[dmg]),
            hp = F(row[hp]),
            speed = F(row[speed]),
            gold = I(row[gold]),
        };

        private static float F(string s) => float.Parse(s.Replace(',', '.'), CultureInfo.InvariantCulture);
        private static int I(string s) => (int)Math.Round(F(s));

        private static float FOpt(System.Collections.Generic.Dictionary<string, string> row, string col)
        {
            string s = Cell.Text(row, col);
            return string.IsNullOrEmpty(s) ? 0f : F(s);
        }
    }
}
