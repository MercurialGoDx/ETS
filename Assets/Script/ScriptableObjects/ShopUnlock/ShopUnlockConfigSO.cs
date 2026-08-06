using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Стартовое состояние магазина и список апгрейдов разблокировки.
/// Здесь настраивается, сколько слотов открыто в начале забега и какие тиры доступны
/// без покупок — в коде этих чисел нет.
/// </summary>
[CreateAssetMenu(fileName = "ShopUnlockConfig", menuName = "TD/Shop Unlock Config")]
public class ShopUnlockConfigSO : ScriptableObject
{
    [Header("Старт забега")]
    [Tooltip("Индексы колонок (с нуля), открытых на старте. Применяются к обоим рядам магазина.")]
    public List<int> startingUnlockedColumns = new List<int> { 0, 1, 2 };

    [Tooltip("Тиры, доступные без покупки апгрейдов.")]
    public List<ItemTier> startingUnlockedTiers = new List<ItemTier> { ItemTier.Tier1, ItemTier.Tier2 };

    [Header("Апгрейды")]
    [Tooltip("Порядок важен: кнопка магазина показывает первый некупленный апгрейд из этого списка.")]
    public List<ShopUnlockUpgradeSO> upgrades = new List<ShopUnlockUpgradeSO>();
}
