using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Журнал всех оружий и улучшений. По открытию ставит игру на паузу и показывает окно выбора
/// (Оружия / Улучшения). Каждый список группируется по тиру (Tier 1..4). Источник данных —
/// пул магазина (<see cref="ShopManager.availableWeapons"/> / <see cref="ShopManager.availableUpgrades"/>).
/// Записи показывают иконку + имя, описание — по наведению через <see cref="WeaponTooltip"/>.
/// </summary>
public class JournalManager : MonoBehaviour
{
    [Header("Источник данных")]
    [SerializeField] private ShopManager source;

    [Header("Окно журнала")]
    [Tooltip("Корень окна (весь оверлей) — включается/выключается при открытии/закрытии.")]
    [SerializeField] private GameObject journalRoot;
    [SerializeField] private GameObject selectionPanel;
    [SerializeField] private GameObject weaponsPanel;
    [SerializeField] private GameObject upgradesPanel;

    [Header("Контейнеры списков (Content у ScrollView)")]
    [SerializeField] private Transform weaponsContent;
    [SerializeField] private Transform upgradesContent;

    [Header("Префабы элементов")]
    [SerializeField] private JournalEntry entryPrefab;
    [SerializeField] private TMP_Text tierHeaderPrefab;

    private readonly List<GameObject> spawnedWeaponItems = new List<GameObject>();
    private readonly List<GameObject> spawnedUpgradeItems = new List<GameObject>();

    private GameState stateBeforeOpen = GameState.Playing;
    private bool weaponsBuilt = false;
    private bool upgradesBuilt = false;

    private static readonly ItemTier[] TierOrder =
    {
        ItemTier.Tier1, ItemTier.Tier2, ItemTier.Tier3, ItemTier.Tier4
    };

    private void Awake()
    {
        // На старте окно скрыто.
        if (journalRoot != null)
            journalRoot.SetActive(false);
    }

    // ===================== ОТКРЫТИЕ / ЗАКРЫТИЕ =====================

    public void Open()
    {
        if (GameStateManager.Instance != null)
        {
            stateBeforeOpen = GameStateManager.Instance.CurrentState;
            GameStateManager.Instance.SetState(GameState.Paused);
        }

        if (journalRoot != null)
            journalRoot.SetActive(true);

        ShowSelection();
    }

    public void Close()
    {
        if (WeaponTooltip.Instance != null)
            WeaponTooltip.Instance.Hide();

        if (journalRoot != null)
            journalRoot.SetActive(false);

        if (GameStateManager.Instance != null)
            GameStateManager.Instance.SetState(stateBeforeOpen);
    }

    // ===================== ПЕРЕКЛЮЧЕНИЕ ПАНЕЛЕЙ =====================

    public void ShowSelection()
    {
        SetPanels(selection: true, weapons: false, upgrades: false);
    }

    public void ShowWeapons()
    {
        if (!weaponsBuilt)
        {
            BuildWeapons();
            weaponsBuilt = true;
        }
        SetPanels(selection: false, weapons: true, upgrades: false);
    }

    public void ShowUpgrades()
    {
        if (!upgradesBuilt)
        {
            BuildUpgrades();
            upgradesBuilt = true;
        }
        SetPanels(selection: false, weapons: false, upgrades: true);
    }

    public void BackToSelection()
    {
        if (WeaponTooltip.Instance != null)
            WeaponTooltip.Instance.Hide();

        ShowSelection();
    }

    private void SetPanels(bool selection, bool weapons, bool upgrades)
    {
        if (selectionPanel != null) selectionPanel.SetActive(selection);
        if (weaponsPanel != null) weaponsPanel.SetActive(weapons);
        if (upgradesPanel != null) upgradesPanel.SetActive(upgrades);
    }

    // ===================== ПОСТРОЕНИЕ СПИСКОВ =====================

    private void BuildWeapons()
    {
        ClearSpawned(spawnedWeaponItems);
        if (source == null || source.availableWeapons == null || weaponsContent == null) return;

        foreach (ItemTier tier in TierOrder)
        {
            bool headerSpawned = false;
            foreach (WeaponDefinition weapon in source.availableWeapons)
            {
                if (weapon == null || weapon.itemTier != tier) continue;

                if (!headerSpawned)
                {
                    SpawnHeader(tier, weaponsContent, spawnedWeaponItems);
                    headerSpawned = true;
                }

                JournalEntry entry = Instantiate(entryPrefab, weaponsContent);
                entry.SetupWeapon(weapon);
                spawnedWeaponItems.Add(entry.gameObject);
            }
        }
    }

    private void BuildUpgrades()
    {
        ClearSpawned(spawnedUpgradeItems);
        if (source == null || source.availableUpgrades == null || upgradesContent == null) return;

        foreach (ItemTier tier in TierOrder)
        {
            bool headerSpawned = false;
            foreach (UpgradeBaseSO upgrade in source.availableUpgrades)
            {
                if (upgrade == null || upgrade.itemTier != tier) continue;

                if (!headerSpawned)
                {
                    SpawnHeader(tier, upgradesContent, spawnedUpgradeItems);
                    headerSpawned = true;
                }

                JournalEntry entry = Instantiate(entryPrefab, upgradesContent);
                entry.SetupUpgrade(upgrade);
                spawnedUpgradeItems.Add(entry.gameObject);
            }
        }
    }

    private void SpawnHeader(ItemTier tier, Transform parent, List<GameObject> bucket)
    {
        if (tierHeaderPrefab == null) return;

        TMP_Text header = Instantiate(tierHeaderPrefab, parent);
        header.text = "Tier " + ((int)tier);
        bucket.Add(header.gameObject);
    }

    private void ClearSpawned(List<GameObject> bucket)
    {
        for (int i = 0; i < bucket.Count; i++)
        {
            if (bucket[i] != null)
                Destroy(bucket[i]);
        }
        bucket.Clear();
    }
}
