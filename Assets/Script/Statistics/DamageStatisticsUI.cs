using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Полноэкранное окно статистики урона по оружиям (открывается с экрана поражения). Оформлено в
/// стиле журнала: тёмная подложка, заголовок сверху, крестик и вертикальный скролл-список строк
/// «иконка оружия + название + суммарный урон», отсортированных по убыванию урона.
/// </summary>
public class DamageStatisticsUI : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Content у ScrollView (VerticalLayoutGroup + ContentSizeFitter).")]
    [SerializeField] private Transform content;

    [Tooltip("Префаб строки статистики (WeaponStatItemUI).")]
    [SerializeField] private WeaponStatItemUI itemPrefab;

    private readonly List<GameObject> spawned = new List<GameObject>();

    public void Show()
    {
        Rebuild();
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (WeaponTooltip.Instance != null)
            WeaponTooltip.Instance.Hide();

        gameObject.SetActive(false);
    }

    private void Rebuild()
    {
        Clear();

        if (DamageStatsManager.Instance == null || content == null || itemPrefab == null)
            return;

        foreach (var stat in DamageStatsManager.Instance.GetDamageSorted())
        {
            if (stat.weapon == null)
                continue;

            WeaponStatItemUI item = Instantiate(itemPrefab, content);
            item.Setup(stat.weapon, stat.damage);
            spawned.Add(item.gameObject);
        }
    }

    private void Clear()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null)
                Destroy(spawned[i]);
        }
        spawned.Clear();
    }
}
