using System;
using System.Collections.Generic;

/// <summary>
/// Источник данных таблицы лидеров. UI (<see cref="LeaderboardUI"/>) работает только через этот
/// интерфейс и ничего не знает о том, откуда берутся результаты — это позволяет собрать и проверить
/// UI на заглушке (<see cref="StubLeaderboardService"/>), а затем подменить реализацию на Steam
/// без единого изменения в UI.
///
/// Планируемая боевая реализация — Steam Leaderboards:
///   • GetTop  → SteamUserStats.DownloadLeaderboardEntries(handle, Global, 1, count),
///               по каждому entry берём SteamID → SteamFriends.GetFriendPersonaName (ник) и
///               GetLargeFriendAvatar / GetMediumFriendAvatar (аватар в текстуру → Sprite).
///   • SubmitTime → SteamUserStats.UploadLeaderboardScore(handle, KeepBest/ForceUpdate, score).
///     Время хранится как целое (например, секунды или миллисекунды), сортировка Descending.
/// Оба вызова у Steam асинхронные (колбэки), поэтому интерфейс тоже асинхронный (onDone).
/// </summary>
public interface ILeaderboardService
{
    /// <summary>
    /// Запрашивает топ-<paramref name="count"/> результатов. Результат приходит в <paramref name="onDone"/>
    /// (список уже отсортирован и с проставленными rank). При ошибке возвращается пустой список.
    /// </summary>
    void GetTop(int count, Action<List<LeaderboardEntry>> onDone);

    /// <summary>
    /// Отправляет результат текущего игрока (время забега в секундах), опционально вместе
    /// со снимком билда (см. BuildSnapshot.Encode — не длиннее BuildSnapshot.MaxDetails).
    /// <paramref name="onDone"/> вызывается по завершении (может быть null).
    /// </summary>
    void SubmitTime(float timeSeconds, int[] buildDetails = null, Action onDone = null);
}
