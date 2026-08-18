using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HUD-панель под здоровьем/щитом: по одной ячейке (иконка + число) на каждое оружие
/// со стаками от механики "килл -> бонус урона" (TowerAttack.AddWeaponBaseDamage).
/// Обновляется по событию, а не каждый кадр — стаки меняются только по киллу.
/// Ячейки переиспользуются (не создаются/уничтожаются заново на каждое обновление).
/// </summary>
public class WeaponKillStacksPanelUI : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private TowerAttack tower;
    [SerializeField] private WeaponKillStackSlot slotPrefab;
    [SerializeField] private Transform container;

    private readonly List<WeaponKillStackSlot> slotPool = new List<WeaponKillStackSlot>();

    private void OnEnable()
    {
        if (tower != null)
            tower.OnWeaponKillStacksChanged += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        if (tower != null)
            tower.OnWeaponKillStacksChanged -= Refresh;
    }

    private void Refresh()
    {
        if (tower == null || slotPrefab == null || container == null)
            return;

        List<TowerAttack.WeaponKillStack> stacks = tower.GetWeaponKillStacks();

        while (slotPool.Count < stacks.Count)
            slotPool.Add(Instantiate(slotPrefab, container));

        for (int i = 0; i < slotPool.Count; i++)
        {
            bool active = i < stacks.Count;
            slotPool[i].gameObject.SetActive(active);

            if (active)
                slotPool[i].SetValue(stacks[i].Def, stacks[i].Stacks);
        }
    }
}
