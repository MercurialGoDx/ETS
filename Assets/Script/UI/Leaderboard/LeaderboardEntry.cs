using UnityEngine;

/// <summary>
/// Одна строка таблицы лидеров: позиция, ник игрока (из Steam), аватар (из Steam) и время,
/// которое игрок продержался в забеге. Один игрок может встречаться в списке несколько раз
/// (несколько его лучших результатов), поэтому это просто данные строки, а не «уникальный игрок».
/// </summary>
public struct LeaderboardEntry
{
    /// <summary>Позиция в таблице, начиная с 1.</summary>
    public int rank;

    /// <summary>Ник игрока (Steam persona name).</summary>
    public string playerName;

    /// <summary>Аватар игрока (Steam avatar). Может быть null — тогда строка рисуется без картинки.</summary>
    public Sprite avatar;

    /// <summary>Продолжительность забега в секундах. Форматируется в mm:ss / hh:mm:ss при отображении.</summary>
    public float timeSeconds;

    public LeaderboardEntry(int rank, string playerName, Sprite avatar, float timeSeconds)
    {
        this.rank = rank;
        this.playerName = playerName;
        this.avatar = avatar;
        this.timeSeconds = timeSeconds;
    }
}
