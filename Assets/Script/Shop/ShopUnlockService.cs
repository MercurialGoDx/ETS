using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// В каком состоянии находится апгрейд разблокировки с точки зрения покупки.
/// </summary>
public enum ShopUnlockState
{
    Available,           // можно купить прямо сейчас
    NotEnoughGold,       // требование выполнено, но не хватает золота
    LockedByRequirement, // не куплен предыдущий апгрейд
    Purchased
}

/// <summary>
/// Состояние разблокировок магазина в пределах одного забега: какие колонки слотов
/// открыты и какие тиры попадают в пул.
///
/// Специально MonoBehaviour, а не ScriptableObject: новый забег — это перезагрузка сцены
/// (UIFlowManager.StartButtonClicked), поэтому объект пересоздаётся и состояние обнуляется
/// само. Поля SO пережили бы перезагрузку и протекли между забегами.
/// </summary>
[DefaultExecutionOrder(-50)]
public class ShopUnlockService : MonoBehaviour
{
    public static ShopUnlockService Instance { get; private set; }

    [SerializeField] private ShopUnlockConfigSO config;

    /// <summary>Купили апгрейд — набор открытых колонок и тиров изменился.</summary>
    public event Action OnUnlocksChanged;

    private readonly HashSet<int> unlockedColumns = new HashSet<int>();
    private readonly HashSet<ItemTier> unlockedTiers = new HashSet<ItemTier>();
    private readonly HashSet<ShopUnlockUpgradeSO> purchased = new HashSet<ShopUnlockUpgradeSO>();

    public ShopUnlockConfigSO Config => config;

    private void Awake()
    {
        Instance = this;
        ResetToStartingState();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void ResetToStartingState()
    {
        unlockedColumns.Clear();
        unlockedTiers.Clear();
        purchased.Clear();

        if (config == null)
        {
            Debug.LogError("[ShopUnlockService] Не назначен ShopUnlockConfigSO — магазин останется полностью закрытым.");
            return;
        }

        foreach (var c in config.startingUnlockedColumns)
            unlockedColumns.Add(c);

        foreach (var t in config.startingUnlockedTiers)
            unlockedTiers.Add(t);
    }

    // ======================== ЗАПРОСЫ ========================

    public bool IsColumnUnlocked(int column) => unlockedColumns.Contains(column);

    public bool IsTierUnlocked(ItemTier tier) => unlockedTiers.Contains(tier);

    public bool IsPurchased(ShopUnlockUpgradeSO upgrade) => upgrade != null && purchased.Contains(upgrade);

    /// <summary>
    /// Первый некупленный апгрейд в порядке конфига. Кнопка магазина показывает именно его.
    /// null — куплено всё.
    /// </summary>
    public ShopUnlockUpgradeSO GetNextUpgrade()
    {
        if (config == null)
            return null;

        foreach (var u in config.upgrades)
        {
            if (u != null && !purchased.Contains(u))
                return u;
        }

        return null;
    }

    public ShopUnlockState GetState(ShopUnlockUpgradeSO upgrade)
    {
        if (upgrade == null)
            return ShopUnlockState.Purchased;

        if (purchased.Contains(upgrade))
            return ShopUnlockState.Purchased;

        if (upgrade.requiredUpgrade != null && !purchased.Contains(upgrade.requiredUpgrade))
            return ShopUnlockState.LockedByRequirement;

        if (GoldManager.Instance != null && !GoldManager.Instance.HasEnoughGold(upgrade.price))
            return ShopUnlockState.NotEnoughGold;

        return ShopUnlockState.Available;
    }

    // ======================== ПОКУПКА ========================

    public bool TryPurchase(ShopUnlockUpgradeSO upgrade)
    {
        if (GetState(upgrade) != ShopUnlockState.Available)
            return false;

        // Золото списываем только после того, как все проверки прошли: повторная покупка
        // отсекается выше по Purchased, так что списания без эффекта быть не может.
        if (GoldManager.Instance != null && !GoldManager.Instance.SpendGold(upgrade.price))
            return false;

        purchased.Add(upgrade);

        foreach (var c in upgrade.unlockedColumns)
            unlockedColumns.Add(c);

        if (upgrade.unlockedTier != ItemTier.None)
            unlockedTiers.Add(upgrade.unlockedTier);

        OnUnlocksChanged?.Invoke();
        return true;
    }
}
