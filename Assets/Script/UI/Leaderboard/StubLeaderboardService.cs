using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Заглушка таблицы лидеров: отдаёт фиксированный набор из 10 «результатов» с плейсхолдер-аватарками
/// (сплошной цвет), пока не подключён реальный Steam-бэкенд. Специально содержит повторяющиеся ники —
/// демонстрирует, что один игрок может занимать несколько позиций. Данные отсортированы по убыванию
/// времени, rank проставлен. Реализация синхронная, но колбэк вызывается как обычно, чтобы UI-код
/// не отличал заглушку от асинхронного Steam.
/// </summary>
public class StubLeaderboardService : ILeaderboardService
{
    // Ник + время (сек). "Aurelia" и "Kael" встречаются дважды — один игрок, несколько позиций.
    private static readonly (string name, float time)[] Data =
    {
        ("Aurelia",  1523f),
        ("Kael",     1487f),
        ("Aurelia",  1402f),
        ("Nyx",      1350f),
        ("Borin",    1298f),
        ("Kael",     1244f),
        ("Seraphine",1180f),
        ("Draven",   1105f),
        ("Mira",      998f),
        ("Borin",     930f),
    };

    // Палитра для плейсхолдер-аватарок (по индексу строки), чтобы строки визуально различались.
    private static readonly Color[] Palette =
    {
        new Color(0.85f, 0.35f, 0.35f), new Color(0.40f, 0.60f, 0.85f),
        new Color(0.50f, 0.75f, 0.45f), new Color(0.80f, 0.65f, 0.30f),
        new Color(0.65f, 0.45f, 0.80f), new Color(0.40f, 0.75f, 0.75f),
        new Color(0.85f, 0.55f, 0.40f), new Color(0.55f, 0.55f, 0.60f),
        new Color(0.80f, 0.45f, 0.65f), new Color(0.45f, 0.70f, 0.55f),
    };

    private List<LeaderboardEntry> cached;

    public void GetTop(int count, Action<List<LeaderboardEntry>> onDone)
    {
        if (cached == null)
            cached = Build();

        var result = new List<LeaderboardEntry>();
        for (int i = 0; i < cached.Count && i < count; i++)
            result.Add(cached[i]);

        if (onDone != null)
            onDone(result);
    }

    public void SubmitTime(float timeSeconds, int[] buildDetails = null, Action onDone = null)
    {
        // Заглушка ничего не сохраняет. Реальную отправку сделает SteamLeaderboardService.
        Debug.Log("[StubLeaderboardService] SubmitTime(" + timeSeconds + ") — заглушка, ничего не сохраняет.");
        if (onDone != null)
            onDone();
    }

    private List<LeaderboardEntry> Build()
    {
        var list = new List<LeaderboardEntry>();
        for (int i = 0; i < Data.Length; i++)
        {
            Sprite avatar = MakeSolidSprite(Palette[i % Palette.Length]);
            // Половине строк даём фейковый снимок билда — чтобы BuildViewerUI можно было
            // проверить в редакторе без Steam. Значения не осмысленные, чисто для теста рендера.
            int[] details = i % 2 == 0 ? MakeFakeSnapshot(i) : null;
            list.Add(new LeaderboardEntry(i + 1, Data[i].name, avatar, Data[i].time, details));
        }
        return list;
    }

    private static int[] MakeFakeSnapshot(int seed)
    {
        var snapshot = new BuildSnapshot();
        snapshot.items.Add(new BuildSnapshot.Item(0, 1 + seed));
        snapshot.items.Add(new BuildSnapshot.Item(1, 2));
        snapshot.SetStat(BuildStat.Health, 500f + seed * 50f);
        snapshot.SetStat(BuildStat.AttackSpeed, 1.2f);
        snapshot.SetStat(BuildStat.DamageMultiplier, 1.5f + seed * 0.1f);
        return snapshot.Encode();
    }

    // Маленькая одноцветная текстура-аватар (плейсхолдер вместо картинки из Steam).
    private static Sprite MakeSolidSprite(Color color)
    {
        const int size = 8;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = color;
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
