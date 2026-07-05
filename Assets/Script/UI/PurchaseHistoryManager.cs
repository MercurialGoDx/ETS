using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Панель истории покупок игрока (слева по центру экрана). При каждой покупке в магазине
/// (<see cref="ShopManager.BuyWeapon"/> / <see cref="ShopManager.BuyUpgrade"/>) добавляет строку
/// «иконка + количество». Повторная покупка того же предмета не создаёт новую строку, а
/// увеличивает счётчик у существующей. Новые строки идут сверху вниз в порядке первой покупки.
/// </summary>
public class PurchaseHistoryManager : MonoBehaviour
{
    public static PurchaseHistoryManager Instance { get; private set; }

    [Header("Ссылки")]
    [Tooltip("Контейнер Content у ScrollView (с VerticalLayoutGroup + ContentSizeFitter).")]
    [SerializeField] private Transform content;

    [Tooltip("Префаб строки истории покупок (PurchaseHistoryEntry).")]
    [SerializeField] private PurchaseHistoryEntry entryPrefab;

    private readonly Dictionary<WeaponDefinition, PurchaseHistoryEntry> weaponEntries = new();
    private readonly Dictionary<WeaponDefinition, int> weaponCounts = new();
    private readonly Dictionary<UpgradeBaseSO, PurchaseHistoryEntry> upgradeEntries = new();
    private readonly Dictionary<UpgradeBaseSO, int> upgradeCounts = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Показать/скрыть панель истории покупок (например, чтобы не наслаивалась на окно статистики).</summary>
    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    public void AddWeapon(WeaponDefinition weapon)
    {
        if (weapon == null || content == null || entryPrefab == null)
            return;

        if (weaponEntries.TryGetValue(weapon, out var entry))
        {
            weaponCounts[weapon]++;
        }
        else
        {
            entry = Instantiate(entryPrefab, content);
            entry.SetupWeapon(weapon);
            weaponEntries[weapon] = entry;
            weaponCounts[weapon] = 1;
        }

        entry.SetCount(weaponCounts[weapon]);
    }

    public void AddUpgrade(UpgradeBaseSO upgrade)
    {
        if (upgrade == null || content == null || entryPrefab == null)
            return;

        if (upgradeEntries.TryGetValue(upgrade, out var entry))
        {
            upgradeCounts[upgrade]++;
        }
        else
        {
            entry = Instantiate(entryPrefab, content);
            entry.SetupUpgrade(upgrade);
            upgradeEntries[upgrade] = entry;
            upgradeCounts[upgrade] = 1;
        }

        entry.SetCount(upgradeCounts[upgrade]);
    }
}
