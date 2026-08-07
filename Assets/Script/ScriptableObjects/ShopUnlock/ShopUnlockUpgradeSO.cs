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

    // Таблица локализации может быть не назначена (ассеты заводятся раньше, чем переводы) —
    // тогда GetTable() кидает, а не возвращает null. Отдаём ключ, вызывающий сам решит,
    // что показать.
    public string GetLocalizedName() => Localize(nameKey, false);

    public string GetLocalizedDescription() => Localize(descriptionKey, true);

    private string Localize(string key, bool withPrice)
    {
        if (string.IsNullOrEmpty(key))
            return key;

        try
        {
            var table = localizedStringTable.GetTable();
            if (table == null)
                return key;

            var entry = table.GetEntry(key);
            if (entry == null)
                return key;

            return withPrice ? entry.GetLocalizedString(price) : entry.GetLocalizedString();
        }
        catch
        {
            return key;
        }
    }
}
