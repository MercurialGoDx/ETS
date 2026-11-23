using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// Таблица лидеров без сети: лучшие забеги этого устройства. Используется на телефоне,
/// пока нет онлайн-таблицы на Firestore (docs/MobilePlan.md, Фаза 12): это честнее, чем
/// заглушка с выдуманными игроками. Снимок билда хранится вместе с результатом, поэтому
/// <see cref="BuildViewerUI"/> работает и здесь.
///
/// Хранилище — JSON в persistentDataPath, запись через временный файл: если процесс
/// убьют посреди записи, старая таблица останется целой.
/// </summary>
public class LocalLeaderboardService : ILeaderboardService
{
    private const int MaxStoredRuns = 50;
    private const string FileName = "local_leaderboard.json";

    [Serializable]
    private class Run
    {
        public float timeSeconds;
        public long finishedUtcTicks;
        public int[] details;
    }

    [Serializable]
    private class Storage
    {
        public int version = 1;
        public List<Run> runs = new List<Run>();
    }

    private readonly string filePath;
    private Storage storage;

    public LocalLeaderboardService()
        : this(Path.Combine(Application.persistentDataPath, FileName))
    {
    }

    /// <summary>Путь задаётся явно в тестах, чтобы не трогать настоящие рекорды игрока.</summary>
    public LocalLeaderboardService(string filePath)
    {
        this.filePath = filePath;
    }

    public void GetTop(int count, Action<List<LeaderboardEntry>> onDone)
    {
        Storage data = Load();
        var result = new List<LeaderboardEntry>();

        for (int i = 0; i < data.runs.Count && i < count; i++)
        {
            Run run = data.runs[i];
            // Ника на устройстве нет — строку подписываем датой забега.
            string label = new DateTime(run.finishedUtcTicks, DateTimeKind.Utc).ToLocalTime()
                .ToString("d", CultureInfo.CurrentCulture);
            result.Add(new LeaderboardEntry(i + 1, label, null, run.timeSeconds, run.details));
        }

        onDone?.Invoke(result);
    }

    public void SubmitTime(float timeSeconds, int[] buildDetails = null, Action onDone = null)
    {
        Storage data = Load();
        data.runs.Add(new Run
        {
            timeSeconds = timeSeconds,
            finishedUtcTicks = DateTime.UtcNow.Ticks,
            details = buildDetails,
        });

        data.runs.Sort((a, b) => b.timeSeconds.CompareTo(a.timeSeconds));
        if (data.runs.Count > MaxStoredRuns)
            data.runs.RemoveRange(MaxStoredRuns, data.runs.Count - MaxStoredRuns);

        Save(data);
        onDone?.Invoke();
    }

    private Storage Load()
    {
        if (storage != null)
            return storage;

        storage = new Storage();
        try
        {
            if (File.Exists(filePath))
            {
                var loaded = JsonUtility.FromJson<Storage>(File.ReadAllText(filePath));
                if (loaded != null && loaded.runs != null)
                    storage = loaded;
            }
        }
        catch (Exception e)
        {
            // Повреждённый файл не должен ломать экран рекордов — начинаем заново.
            Debug.LogWarning("[LocalLeaderboard] Не удалось прочитать " + filePath + ": " + e.Message);
        }

        return storage;
    }

    private void Save(Storage data)
    {
        string tempPath = filePath + ".tmp";
        try
        {
            File.WriteAllText(tempPath, JsonUtility.ToJson(data));
            if (File.Exists(filePath))
                File.Replace(tempPath, filePath, null);
            else
                File.Move(tempPath, filePath);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[LocalLeaderboard] Не удалось сохранить рекорд: " + e.Message);
        }
    }
}
