using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HUD-панель под здоровьем/щитом: по одной ячейке (иконка + число) на каждое оружие
/// со стаками от механики "килл -> бонус урона" (TowerAttack.AddWeaponBaseDamage),
/// плюс отдельная ячейка-индикатор заряженного дубликатора (без числа) — появляется
/// в момент покупки Duplicator и пропадает, как только он сработает на следующей покупке,
/// плюс ячейка динамического бонуса монетки (Coin) — показывает текущий бонус к базовому
/// урону от золота (CoinBullet.GetGoldDamageBonus), обновляется по мере трат/накопления золота.
/// Килл-стаки и дубликатор обновляются по событию, монетка — опросом раз в кадр (её бонус
/// зависит от текущего золота, а не от разовых событий). Ячейки переиспользуются.
/// </summary>
public class WeaponKillStacksPanelUI : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private TowerAttack tower;
    [SerializeField] private WeaponKillStackSlot slotPrefab;
    [SerializeField] private Transform container;

    [Header("Дубликатор")]
    [Tooltip("Откуда брать runtime-данные (IsDuplicatorArmed) и событие изменения заряда.")]
    [SerializeField] private UpgradeContextSO upgradeContext;
    [Tooltip("Апгрейд Duplicator — берём отсюда иконку и текст тултипа для индикатора.")]
    [SerializeField] private UpgradeBaseSO duplicatorUpgrade;

    [Header("Монета (динамический бонус от золота)")]
    [Tooltip("Оружие Coin — если куплено, показываем отдельной ячейкой текущий бонус к базовому урону от золота.")]
    [SerializeField] private WeaponDefinition coinWeapon;

    [Header("Раскладка")]
    [Tooltip("Ширина одной ячейки + отступ между ячейками (px), используется для ручной расстановки в ряд.")]
    [SerializeField] private float slotStep = 108f;

    private readonly List<WeaponKillStackSlot> slotPool = new List<WeaponKillStackSlot>();
    private UpgradesRuntimeData subscribedRuntime;

    private CoinBullet cachedCoinBullet;
    private bool coinOwned;
    private float coinBonus;

    private void OnEnable()
    {
        if (tower != null)
            tower.OnWeaponKillStacksChanged += Refresh;

        subscribedRuntime = upgradeContext != null ? upgradeContext.runtime : null;
        if (subscribedRuntime != null)
            subscribedRuntime.OnDuplicatorStateChanged += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        if (tower != null)
            tower.OnWeaponKillStacksChanged -= Refresh;

        if (subscribedRuntime != null)
            subscribedRuntime.OnDuplicatorStateChanged -= Refresh;

        subscribedRuntime = null;
    }

    private void Update()
    {
        RefreshCoinBonus();
    }

    /// <summary>Бонус монетки зависит от текущего золота — опрашиваем, а не ждём событие.</summary>
    private void RefreshCoinBonus()
    {
        if (coinWeapon == null || tower == null)
            return;

        if (cachedCoinBullet == null)
        {
            if (coinWeapon.bulletPrefab == null)
                return;

            cachedCoinBullet = coinWeapon.bulletPrefab.GetComponent<CoinBullet>();
            if (cachedCoinBullet == null)
                return;
        }

        bool owned = IsWeaponOwned(coinWeapon);
        float bonus = owned ? cachedCoinBullet.GetGoldDamageBonus() : 0f;

        if (owned == coinOwned && Mathf.Approximately(bonus, coinBonus))
            return;

        coinOwned = owned;
        coinBonus = bonus;
        Refresh();
    }

    private bool IsWeaponOwned(WeaponDefinition def)
    {
        foreach (var owned in tower.GetOwnedWeapons())
        {
            if (owned.Def == def)
                return owned.Stacks > 0;
        }

        return false;
    }

    private void Refresh()
    {
        if (tower == null || slotPrefab == null || container == null)
            return;

        List<TowerAttack.WeaponKillStack> stacks = tower.GetWeaponKillStacks();
        bool duplicatorArmed = subscribedRuntime != null && subscribedRuntime.IsDuplicatorArmed;
        bool showCoin = coinOwned;
        int totalSlots = stacks.Count + (duplicatorArmed ? 1 : 0) + (showCoin ? 1 : 0);

        while (slotPool.Count < totalSlots)
            slotPool.Add(Instantiate(slotPrefab, container));

        int index = 0;

        for (int i = 0; i < slotPool.Count; i++)
            slotPool[i].gameObject.SetActive(i < totalSlots);

        if (duplicatorArmed)
        {
            PlaceSlot(slotPool[index], index);
            slotPool[index].SetDuplicator(duplicatorUpgrade);
            index++;
        }

        if (showCoin)
        {
            PlaceSlot(slotPool[index], index);
            slotPool[index].SetValue(coinWeapon, Mathf.RoundToInt(coinBonus));
            index++;
        }

        for (int i = 0; i < stacks.Count; i++, index++)
        {
            PlaceSlot(slotPool[index], index);
            slotPool[index].SetValue(stacks[i].Def, stacks[i].Stacks);
        }
    }

    /// <summary>
    /// Расставляем ячейки в ряд вручную (без Layout Group — префаб ячейки несёт
    /// свою собственную "запечённую" позицию, Instantiate(prefab, parent) берёт её
    /// как локальную относительно нового родителя, поэтому без явного сброса
    /// все ячейки оказываются друг под другом/за экраном).
    /// </summary>
    private void PlaceSlot(WeaponKillStackSlot slot, int index)
    {
        if (slot == null)
            return;

        if (slot.transform is not RectTransform rt)
            return;

        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(index * slotStep, 0f);
    }
}
