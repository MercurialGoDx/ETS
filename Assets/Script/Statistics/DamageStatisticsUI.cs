using System.Collections.Generic;
using UnityEngine;

public class DamageStatisticsUI : MonoBehaviour
{
    [SerializeField] private Transform content;
    [SerializeField] private WeaponStatItemUI itemPrefab;

    private readonly List<GameObject> spawnedItems = new();

    public void Show()
    {
        Clear();

        var stats = DamageStatsManager.Instance.GetDamageSorted();

        foreach (var stat in stats)
        {
            var item = Instantiate(itemPrefab, content);

            item.Setup(stat.weapon, stat.damage);

            spawnedItems.Add(item.gameObject);
        }

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void Clear()
    {
        foreach (var item in spawnedItems)
        {
            Destroy(item);
        }

        spawnedItems.Clear();
    }
}