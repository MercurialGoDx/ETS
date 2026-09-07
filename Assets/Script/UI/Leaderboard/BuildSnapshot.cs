using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Фиксированный порядок статов в снимке билда — тот же порядок строк, что и в
/// InventoryUI.RefreshStats(). Значения хранятся как float в BuildSnapshot.stats,
/// индексируются этим enum-ом и с этим же порядком выводятся в BuildViewerUI.
/// </summary>
public enum BuildStat
{
    Health,
    HealthRegen,
    HealOnKill,
    HealOnHit,
    HealAmp,
    Shield,
    ShieldOnKill,
    BlockChance,
    DamageReduction,
    SpikesDamage,
    AttackSpeed,
    DamageMultiplier,
    MoreEnemies,
    Income,
    GoldBonus,
    HuntChance,
    DmgMagic,
    DmgPiercing,
    DmgNormal,
    DmgProjectile,
    DmgHeavy,
    DmgChaos,
    EnemyHealth,
    EnemyDamage,
    // Добавлен в конец, чтобы не сдвигать индексы статов в старых Steam-снимках.
    DmgHoly,
    HealingPercentFromMaxHealth,

    Count
}

/// <summary>
/// Снимок билда игрока на момент смерти — то же самое, что видно в InventoryPanel:
/// список купленного оружия/улучшений (по индексу в каталоге, см. BuildCatalog) и
/// посчитанные статы. Кодируется в int[] и уходит в Steam как "детали" записи
/// лидерборда (SteamUserStats.UploadLeaderboardScore, до 64 int32 на запись) —
/// так его может скачать и показать себе любой другой игрок, открывший таблицу лидеров.
/// </summary>
public class BuildSnapshot
{
    public struct Item
    {
        /// <summary>Индекс в объединённом каталоге — см. BuildCatalog.</summary>
        public int id;
        public int count;

        public Item(int id, int count)
        {
            this.id = id;
            this.count = count;
        }
    }

    /// <summary>k_cLeaderboardDetailsMax в Steamworks — больше в одну запись не влезет.</summary>
    public const int MaxDetails = 64;

    private const int StatCount = (int)BuildStat.Count;
    private const int LegacyStatCount = (int)BuildStat.DmgHoly;
    private const int MaxItemCount = 9999;
    private const int MaxItems = MaxDetails - 1 - StatCount; // = 39 при 24 статах

    public readonly List<Item> items = new List<Item>();
    public readonly float[] stats = new float[StatCount];

    public float GetStat(BuildStat stat) => stats[(int)stat];
    public void SetStat(BuildStat stat, float value) => stats[(int)stat] = value;

    public static BuildStat GetDamageTypeStat(WeaponDamageType damageType)
    {
        return damageType == WeaponDamageType.Holy
            ? BuildStat.DmgHoly
            : BuildStat.DmgMagic + (int)damageType;
    }

    /// <summary>Кодирует снимок в int[] для SteamUserStats.UploadLeaderboardScore.</summary>
    public int[] Encode()
    {
        List<Item> ordered = items;

        // Если предметов больше, чем влезает (игрок скупил почти весь каталог за забег),
        // оставляем самые "весомые" по количеству — остальные обрежутся.
        if (ordered.Count > MaxItems)
        {
            ordered = new List<Item>(items);
            ordered.Sort((a, b) => b.count.CompareTo(a.count));
            ordered.RemoveRange(MaxItems, ordered.Count - MaxItems);
        }

        var result = new int[1 + ordered.Count + StatCount];
        result[0] = ordered.Count;

        for (int i = 0; i < ordered.Count; i++)
        {
            int count = Mathf.Clamp(ordered[i].count, 0, MaxItemCount);
            result[1 + i] = ordered[i].id * (MaxItemCount + 1) + count;
        }

        int statsOffset = 1 + ordered.Count;
        for (int i = 0; i < StatCount; i++)
            result[statsOffset + i] = Mathf.RoundToInt(stats[i] * 100f);

        return result;
    }

    /// <summary>Пытается разобрать детали записи лидерборда. null — данных нет (старая запись/заглушка без Steam).</summary>
    public static BuildSnapshot Decode(int[] details)
    {
        if (details == null || details.Length < 1)
            return null;

        int count = details[0];
        if (count < 0 || 1 + count + LegacyStatCount > details.Length)
            return null;

        var snapshot = new BuildSnapshot();

        for (int i = 0; i < count; i++)
        {
            int packed = details[1 + i];
            int id = packed / (MaxItemCount + 1);
            int itemCount = packed % (MaxItemCount + 1);
            snapshot.items.Add(new Item(id, itemCount));
        }

        int statsOffset = 1 + count;
        int availableStatCount = Mathf.Min(StatCount, details.Length - statsOffset);
        for (int i = 0; i < availableStatCount; i++)
            snapshot.stats[i] = details[statsOffset + i] / 100f;

        return snapshot;
    }
}
