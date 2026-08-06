using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

/// <summary>
/// Покупаемый в магазине апгрейд, который открывает колонки слотов и добавляет тир в пул.
/// Живёт один забег: покупки хранит ShopUnlockService, сюда ничего не пишется.
/// </summary>
[CreateAssetMenu(fileName = "ShopUnlock", menuName = "TD/Shop Unlock Upgrade")]
public class ShopUnlockUpgradeSO : ScriptableObject
{
    [Header("Основное")]
    public Sprite icon;
    public int price = 150;

    [Header("Что открывает")]
    [Tooltip("Индексы колонок (с нуля), которые открываются в ОБОИХ рядах магазина.")]
    public List<int> unlockedColumns = new List<int>();

    [Tooltip("Тир, предметы которого попадают в пул после покупки.")]
    public ItemTier unlockedTier = ItemTier.None;

    [Header("Требование")]
    [Tooltip("Апгрейд, который нужно купить до этого. Пусто — доступен сразу.")]
    public ShopUnlockUpgradeSO requiredUpgrade;

    [Header("Localization")]
    public LocalizedStringTable localizedStringTable;
    public string nameKey;
    public string descriptionKey;

    public string GetLocalizedName()
    {
        var table = localizedStringTable.GetTable();
        if (table == null)
            return nameKey;

        var entry = table.GetEntry(nameKey);
        return entry?.GetLocalizedString() ?? nameKey;
    }

    public string GetLocalizedDescription()
    {
        var table = localizedStringTable.GetTable();
        if (table == null)
            return descriptionKey;

        var entry = table.GetEntry(descriptionKey);
        return entry != null ? entry.GetLocalizedString(price) : descriptionKey;
    }
}
