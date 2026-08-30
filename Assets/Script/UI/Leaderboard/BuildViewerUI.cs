using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Read-only копия InventoryPanel для просмотра ЧУЖОГО билда — открывается по клику на
/// строку в таблице лидеров (см. LeaderboardEntryUI, LeaderboardUI). Живёт на
/// MainMenuCanvas, а не на GameCanvas, где обычный InventoryPanel/InventoryUI: таблица
/// лидеров доступна только из главного меню, когда GameCanvas выключен целиком (см.
/// UIFlowManager) — переиспользовать сам InventoryPanel напрямую нельзя, он неактивен.
///
/// Данные — не живые ссылки на игрока, а декодированный BuildSnapshot с чужой записи
/// лидерборда (см. BuildSnapshot, ILeaderboardService, SteamLeaderboardService). Иконки
/// оружия/улучшений достаются локально по id через BuildCatalog — тем же каталогом
/// (ShopManager.availableWeapons/availableUpgrades), что и у своей панели.
/// </summary>
public class BuildViewerUI : MonoBehaviour
{
    [Header("Окно")]
    [Tooltip("Корень панели — включается/выключается при открытии/закрытии.")]
    [SerializeField] private GameObject panelRoot;
    [Tooltip("Ник владельца билда (необязательно).")]
    [SerializeField] private TMP_Text playerNameText;
    [Tooltip("Показывается вместо сеток/статов, если у записи лидерборда нет снимка билда.")]
    [SerializeField] private GameObject noDataLabel;

    [Header("Сетки")]
    [SerializeField] private Transform weaponsContent;
    [SerializeField] private Transform upgradesContent;
    [SerializeField] private InventorySlot weaponSlotPrefab;
    [SerializeField] private InventorySlot upgradeSlotPrefab;

    [Header("Статы")]
    [SerializeField] private Transform statsContent;
    [SerializeField] private Transform damageTypesContent;
    [SerializeField] private GameObject damageTypesHeader;
    [SerializeField] private Transform enemyStatsContent;
    [SerializeField] private GameObject enemyStatsHeader;
    [SerializeField] private StatRow statRowPrefab;

    [Header("Оформление тиров (индекс 0 = Tier 1)")]
    [SerializeField] private Color[] tierColors =
    {
        new Color(0.53f, 0.53f, 0.50f), // Tier 1 — серый
        new Color(0.11f, 0.62f, 0.46f), // Tier 2 — бирюзовый
        new Color(0.50f, 0.47f, 0.87f), // Tier 3 — фиолетовый
        new Color(0.94f, 0.62f, 0.15f), // Tier 4 — золотой
    };
    [SerializeField] private float tier4FrameThickness = 2f;
    [Range(0f, 1f)]
    [SerializeField] private float tierBackgroundAlpha = 0.18f;

    [Header("Каталог")]
    [Tooltip("Для перевода id из снимка обратно в оружие/улучшение (иконка, тултип).")]
    [SerializeField] private ShopManager shopManager;

    // Тот же порядок ключей, что и в enum WeaponDamageType.
    private static readonly string[] DamageTypeKeys =
    {
        "inv.dmg_magic", "inv.dmg_piercing", "inv.dmg_normal",
        "inv.dmg_projectile", "inv.dmg_heavy", "inv.dmg_chaos", "inv.dmg_holy"
    };

    private readonly List<GameObject> spawnedSlots = new List<GameObject>();
    private readonly List<StatRow> statRowPool = new List<StatRow>();
    private readonly List<StatRow> typeRowPool = new List<StatRow>();
    private readonly List<StatRow> enemyRowPool = new List<StatRow>();
    private int usedRows, usedTypeRows, usedEnemyRows;

    private bool isOpen;

    private void Awake()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void Update()
    {
        if (!isOpen)
            return;

        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Escape))
            Close();
    }

    /// <summary>Открыть с чужим снимком. snapshot == null — запись без данных билда (старая/не-Steam).</summary>
    public void Show(BuildSnapshot snapshot, string playerName)
    {
        isOpen = true;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (playerNameText != null)
            playerNameText.text = playerName ?? string.Empty;

        bool hasData = snapshot != null;

        if (noDataLabel != null)
            noDataLabel.SetActive(!hasData);
        if (weaponsContent != null)
            weaponsContent.gameObject.SetActive(hasData);
        if (upgradesContent != null)
            upgradesContent.gameObject.SetActive(hasData);
        if (statsContent != null)
            statsContent.gameObject.SetActive(hasData);

        ClearSlots();
        ClearRows();

        if (hasData)
        {
            BuildGrids(snapshot);
            BuildStats(snapshot);
        }
        else
        {
            if (damageTypesHeader != null) damageTypesHeader.SetActive(false);
            if (enemyStatsHeader != null) enemyStatsHeader.SetActive(false);
        }
    }

    public void Close()
    {
        isOpen = false;

        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (WeaponTooltip.Instance != null)
            WeaponTooltip.Instance.Hide();
    }

    // ===================== СЕТКИ =====================

    private void BuildGrids(BuildSnapshot snapshot)
    {
        foreach (var item in snapshot.items)
        {
            if (!BuildCatalog.TryResolve(shopManager, item.id, out WeaponDefinition weapon, out UpgradeBaseSO upgrade))
                continue;

            if (weapon != null && weaponsContent != null && weaponSlotPrefab != null)
            {
                InventorySlot slot = Instantiate(weaponSlotPrefab, weaponsContent);
                slot.SetupWeapon(weapon, item.count);
                ApplyTierLook(slot, weapon.itemTier);
                spawnedSlots.Add(slot.gameObject);
            }
            else if (upgrade != null && upgradesContent != null && upgradeSlotPrefab != null)
            {
                InventorySlot slot = Instantiate(upgradeSlotPrefab, upgradesContent);
                slot.SetupUpgrade(upgrade, item.count);
                ApplyTierLook(slot, upgrade.itemTier);
                spawnedSlots.Add(slot.gameObject);
            }
        }
    }

    private void ApplyTierLook(InventorySlot slot, ItemTier tier)
    {
        int index = Mathf.Clamp((int)tier - 1, 0, tierColors.Length - 1);
        float thickness = tier == ItemTier.Tier4 ? tier4FrameThickness : 1f;
        slot.SetTierFrame(tierColors[index], thickness, tierBackgroundAlpha);
    }

    private void ClearSlots()
    {
        for (int i = 0; i < spawnedSlots.Count; i++)
        {
            if (spawnedSlots[i] != null)
                Destroy(spawnedSlots[i]);
        }
        spawnedSlots.Clear();
    }

    // ===================== СТАТЫ =====================

    private void BuildStats(BuildSnapshot snapshot)
    {
        if (statsContent == null || statRowPrefab == null)
            return;

        usedRows = 0;
        usedTypeRows = 0;
        usedEnemyRows = 0;

        Row(StatFormat.L("inv.health"), StatFormat.Int(snapshot.GetStat(BuildStat.Health)));
        Row(StatFormat.L("inv.health_regen"), StatFormat.Rate(snapshot.GetStat(BuildStat.HealthRegen)));
        Row(StatFormat.L("inv.heal_on_kill"), StatFormat.Num(snapshot.GetStat(BuildStat.HealOnKill)));
        Row(StatFormat.L("inv.heal_on_hit"), StatFormat.Num(snapshot.GetStat(BuildStat.HealOnHit)));
        Row(StatFormat.L("inv.heal_amp"), StatFormat.Percent(snapshot.GetStat(BuildStat.HealAmp)));

        float shield = snapshot.GetStat(BuildStat.Shield);
        Row(StatFormat.L("inv.shield"), shield > 0.0001f ? StatFormat.Int(shield) : "—");
        Row(StatFormat.L("inv.shield_on_kill"), StatFormat.Num(snapshot.GetStat(BuildStat.ShieldOnKill)));

        Row(StatFormat.L("inv.block_chance"), StatFormat.Percent(snapshot.GetStat(BuildStat.BlockChance)));
        Row(StatFormat.L("inv.damage_reduction"), StatFormat.Percent(snapshot.GetStat(BuildStat.DamageReduction)));
        Row(StatFormat.L("inv.spikes_damage"), StatFormat.Num(snapshot.GetStat(BuildStat.SpikesDamage)));

        Row(StatFormat.L("inv.attack_speed"), StatFormat.Multiplier(snapshot.GetStat(BuildStat.AttackSpeed)));
        Row(StatFormat.L("inv.damage_multiplier"), StatFormat.Multiplier(snapshot.GetStat(BuildStat.DamageMultiplier)));

        Row(StatFormat.L("inv.more_enemies"), StatFormat.Percent(snapshot.GetStat(BuildStat.MoreEnemies)));
        Row(StatFormat.L("inv.income"), StatFormat.Int(snapshot.GetStat(BuildStat.Income)) + StatFormat.L("inv.per_second"));
        Row(StatFormat.L("inv.gold_bonus"), StatFormat.Percent(snapshot.GetStat(BuildStat.GoldBonus)));
        Row(StatFormat.L("inv.hunt_chance"), StatFormat.Percent(snapshot.GetStat(BuildStat.HuntChance)));

        if (damageTypesHeader != null)
            damageTypesHeader.SetActive(true);

        for (int i = 0; i < DamageTypeKeys.Length; i++)
        {
            var damageType = (WeaponDamageType)i;
            float total = snapshot.GetStat(BuildSnapshot.GetDamageTypeStat(damageType));
            TypeRow(StatFormat.L(DamageTypeKeys[i]), StatFormat.Percent(total));
        }

        if (enemyStatsHeader != null)
            enemyStatsHeader.SetActive(true);

        EnemyRow(StatFormat.L("inv.enemy_health"), StatFormat.Int(snapshot.GetStat(BuildStat.EnemyHealth)));
        EnemyRow(StatFormat.L("inv.enemy_damage"), StatFormat.Int(snapshot.GetStat(BuildStat.EnemyDamage)));

        for (int i = usedRows; i < statRowPool.Count; i++)
            statRowPool[i].gameObject.SetActive(false);
        for (int i = usedTypeRows; i < typeRowPool.Count; i++)
            typeRowPool[i].gameObject.SetActive(false);
        for (int i = usedEnemyRows; i < enemyRowPool.Count; i++)
            enemyRowPool[i].gameObject.SetActive(false);
    }

    private void ClearRows()
    {
        // Пул не уничтожаем — только прячем, как в InventoryUI: переиспользуется между показами.
        for (int i = 0; i < statRowPool.Count; i++) statRowPool[i].gameObject.SetActive(false);
        for (int i = 0; i < typeRowPool.Count; i++) typeRowPool[i].gameObject.SetActive(false);
        for (int i = 0; i < enemyRowPool.Count; i++) enemyRowPool[i].gameObject.SetActive(false);
        usedRows = 0;
        usedTypeRows = 0;
        usedEnemyRows = 0;
    }

    private void Row(string label, string value)
    {
        Take(statRowPool, ref usedRows, statsContent).Set(label, value);
    }

    private void TypeRow(string label, string value)
    {
        Transform parent = damageTypesContent != null ? damageTypesContent : statsContent;
        Take(typeRowPool, ref usedTypeRows, parent).Set(label, value);
    }

    private void EnemyRow(string label, string value)
    {
        Transform parent = enemyStatsContent != null ? enemyStatsContent : statsContent;
        Take(enemyRowPool, ref usedEnemyRows, parent).Set(label, value);
    }

    private StatRow Take(List<StatRow> pool, ref int used, Transform parent)
    {
        StatRow row;

        if (used < pool.Count)
        {
            row = pool[used];
        }
        else
        {
            row = Instantiate(statRowPrefab, parent);
            pool.Add(row);
        }

        row.gameObject.SetActive(true);
        row.transform.SetSiblingIndex(used);
        used++;
        return row;
    }
}
