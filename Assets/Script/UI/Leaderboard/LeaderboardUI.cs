using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Окно таблицы лидеров в главном меню. По клику на кнопку «Таблица лидеров» (<see cref="Open"/>)
/// прячет кнопки главного меню и показывает окно с топ-результатами; крестик закрытия (<see cref="Close"/>)
/// возвращает кнопки меню. Оформление — как окно статистики урона (<see cref="DamageStatisticsUI"/>):
/// тёмная подложка, заголовок, вертикальный скролл-список строк.
///
/// Данные берутся через <see cref="ILeaderboardService"/>. Сейчас это заглушка
/// (<see cref="StubLeaderboardService"/>); подмена на Steam-реализацию не затронет этот класс.
/// </summary>
public class LeaderboardUI : MonoBehaviour
{
    [Header("Список")]
    [Tooltip("Content у ScrollView (VerticalLayoutGroup + ContentSizeFitter).")]
    [SerializeField] private Transform content;

    [Tooltip("Префаб строки таблицы (LeaderboardEntryUI).")]
    [SerializeField] private LeaderboardEntryUI rowPrefab;

    [Header("Сколько строк показывать")]
    [SerializeField] private int topCount = 10;

    [Header("Кнопки меню, которые прячутся на время показа таблицы")]
    [SerializeField] private GameObject[] menuButtonsToHide;

    [Header("Steam")]
    [Tooltip("Имя лидерборда в Steamworks (FindOrCreateLeaderboard). Должно совпадать с именем на partner-сайте.")]
    [SerializeField] private string steamLeaderboardName = "SurvivalTime";

    private readonly List<GameObject> spawned = new List<GameObject>();

    // Окно авторится выключенным в сцене (как StatisticWindow) и включается только из Open().

    /// <summary>Открыть таблицу: спрятать кнопки меню, загрузить и показать топ-результаты.</summary>
    public void Open()
    {
        SetMenuButtonsVisible(false);
        gameObject.SetActive(true);

        Leaderboards.Configure(steamLeaderboardName);
        Leaderboards.Service.GetTop(topCount, OnTopLoaded);
    }

    /// <summary>Закрыть таблицу: скрыть окно и вернуть кнопки меню.</summary>
    public void Close()
    {
        gameObject.SetActive(false);
        SetMenuButtonsVisible(true);
    }

    private void OnTopLoaded(List<LeaderboardEntry> entries)
    {
        // Окно могли успеть закрыть, пока грузились данные (актуально для асинхронного Steam).
        if (!gameObject.activeInHierarchy)
            return;

        Rebuild(entries);
    }

    private void Rebuild(List<LeaderboardEntry> entries)
    {
        Clear();

        if (content == null || rowPrefab == null || entries == null)
            return;

        foreach (var entry in entries)
        {
            LeaderboardEntryUI row = Instantiate(rowPrefab, content);
            row.Setup(entry);
            spawned.Add(row.gameObject);
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

    private void SetMenuButtonsVisible(bool visible)
    {
        if (menuButtonsToHide == null)
            return;

        foreach (var go in menuButtonsToHide)
        {
            if (go != null)
                go.SetActive(visible);
        }
    }
}
