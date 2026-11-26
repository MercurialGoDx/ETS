#if PLATFORM_MOBILE
using System;
using System.Collections.Generic;
using Firebase.Analytics;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;

/// <summary>
/// Онлайн-таблица рекордов на Firestore: одна запись на игрока (документ с его UID),
/// в ней лучший забег, ник и снимок билда для <see cref="BuildViewerUI"/>.
///
/// Каждый забег сначала пишется в <see cref="LocalLeaderboardService"/>; в Firestore уходит
/// лучший локальный результат. Поэтому ничего не теряется без сети или до анонимного входа:
/// как только вход выполнен, догоняем. Пока Firebase недоступен, таблица показывает
/// локальные рекорды. Правила, которые проверяют запись на сервере, — Firebase/firestore.rules.
/// </summary>
public class FirestoreLeaderboardService : ILeaderboardService
{
    private const string CollectionName = "leaderboard";

    private readonly LocalLeaderboardService local = new LocalLeaderboardService();
    private bool uploadPending;

    public FirestoreLeaderboardService()
    {
        FirebaseServices.SignedIn += OnSignedIn;
    }

    private static bool Online => FirebaseServices.IsAvailable && FirebaseServices.LeaderboardEnabled;

    public void GetTop(int count, Action<List<LeaderboardEntry>> onDone)
    {
        if (!Online)
        {
            local.GetTop(count, onDone);
            return;
        }

        FirebaseFirestore.DefaultInstance.Collection(CollectionName)
            .OrderByDescending("timeSeconds")
            .Limit(count)
            .GetSnapshotAsync()
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning("[Leaderboard] Firestore недоступен, показываю локальные рекорды: "
                        + task.Exception?.GetBaseException().Message);
                    local.GetTop(count, onDone);
                    return;
                }

                var entries = new List<LeaderboardEntry>();
                int rank = 1;
                foreach (DocumentSnapshot document in task.Result.Documents)
                {
                    if (TryReadEntry(document, rank, out var entry))
                    {
                        entries.Add(entry);
                        rank++;
                    }
                }
                onDone?.Invoke(entries);
            });
    }

    public void SubmitTime(float timeSeconds, int[] buildDetails = null, Action onDone = null)
    {
        float previousBest = LocalBestTime();
        local.SubmitTime(timeSeconds, buildDetails);
        FirebaseServices.LogEvent("run_end", new Parameter("time_seconds", (double)timeSeconds));
        onDone?.Invoke();

        if (!Online)
        {
            uploadPending = true;
            return;
        }

        if (NicknameRules.HasSaved)
        {
            UploadPersonalBest();
            return;
        }

        // Ник спрашиваем при первом попадании в таблицу. Если игрок отложил ввод,
        // снова спросим только при новом личном рекорде, а не после каждого забега.
        if (timeSeconds > previousBest || previousBest <= 0f)
        {
            NicknamePrompt.Show(nickname =>
            {
                if (nickname == null)
                    return;
                NicknameRules.Saved = nickname;
                UploadPersonalBest();
            });
        }
    }

    private void OnSignedIn()
    {
        if (uploadPending && NicknameRules.HasSaved)
            UploadPersonalBest();
    }

    private float LocalBestTime()
    {
        float best = 0f;
        local.GetTop(1, entries =>
        {
            if (entries.Count > 0)
                best = entries[0].timeSeconds;
        });
        return best;
    }

    private void UploadPersonalBest()
    {
        string userId = FirebaseServices.UserId;
        if (string.IsNullOrEmpty(userId) || !NicknameRules.HasSaved)
        {
            uploadPending = true;
            return;
        }

        LeaderboardEntry best = default;
        bool hasBest = false;
        local.GetTop(1, entries =>
        {
            hasBest = entries.Count > 0;
            if (hasBest)
                best = entries[0];
        });
        if (!hasBest)
            return;

        uploadPending = false;
        DocumentReference document = FirebaseFirestore.DefaultInstance.Collection(CollectionName).Document(userId);
        document.GetSnapshotAsync().ContinueWithOnMainThread(read =>
        {
            if (read.IsFaulted || read.IsCanceled)
            {
                uploadPending = true;
                return;
            }

            DocumentSnapshot existing = read.Result;
            double serverTime = existing.Exists && existing.TryGetValue("timeSeconds", out double stored) ? stored : 0d;
            string serverName = existing.Exists && existing.TryGetValue("name", out string storedName) ? storedName : null;
            string nickname = NicknameRules.Saved;

            if (serverTime >= best.timeSeconds)
            {
                // Результат на сервере не хуже (например, с другого устройства) — его не трогаем,
                // разве что игрок сменил ник.
                if (existing.Exists && serverName != nickname)
                    Write(document.UpdateAsync(new Dictionary<string, object>
                    {
                        { "name", nickname },
                        { "updatedAt", FieldValue.ServerTimestamp },
                    }));
                return;
            }

            Write(document.SetAsync(new Dictionary<string, object>
            {
                { "name", nickname },
                { "timeSeconds", (double)best.timeSeconds },
                { "details", ToList(best.details) },
                { "gameVersion", Application.version },
                { "updatedAt", FieldValue.ServerTimestamp },
            }));
        });
    }

    private void Write(System.Threading.Tasks.Task write)
    {
        // Без сети Firestore держит запись в очереди и отправит её сам при появлении связи.
        write.ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                uploadPending = true;
                Debug.LogWarning("[Leaderboard] Запись отклонена: " + task.Exception?.GetBaseException().Message);
            }
        });
    }

    private static List<object> ToList(int[] details)
    {
        var list = new List<object>();
        if (details != null)
        {
            foreach (int value in details)
                list.Add((long)value);
        }
        return list;
    }

    private static bool TryReadEntry(DocumentSnapshot document, int rank, out LeaderboardEntry entry)
    {
        entry = default;
        if (!document.TryGetValue("name", out string name) || !document.TryGetValue("timeSeconds", out double time))
            return false;

        int[] details = null;
        if (document.TryGetValue("details", out List<object> raw) && raw != null && raw.Count > 0)
        {
            details = new int[raw.Count];
            for (int i = 0; i < raw.Count; i++)
                details[i] = Convert.ToInt32(raw[i]);
        }

        entry = new LeaderboardEntry(rank, name, null, (float)time, details);
        return true;
    }
}
#endif
