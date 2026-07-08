using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Строка таблицы лидеров: позиция + аватар игрока + ник + время забега. Стилистика повторяет
/// строку статистики урона (<see cref="WeaponStatItemUI"/>): плашка-подложка, картинка слева,
/// текст по центру, значение справа.
/// </summary>
public class LeaderboardEntryUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private Image avatarImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text timeText;

    public void Setup(LeaderboardEntry entry)
    {
        if (rankText != null)
            rankText.text = entry.rank.ToString();

        if (avatarImage != null)
        {
            avatarImage.sprite = entry.avatar;
            avatarImage.enabled = entry.avatar != null;
        }

        if (nameText != null)
            nameText.text = entry.playerName;

        if (timeText != null)
            timeText.text = FormatTime(entry.timeSeconds);
    }

    /// <summary>Время забега в формате 00:00 (или 01:23:45, если есть часы) — как в <see cref="GameTimeUI"/>.</summary>
    public static string FormatTime(float seconds)
    {
        int totalSeconds = Mathf.FloorToInt(seconds);
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int secs = totalSeconds % 60;

        if (hours > 0)
            return string.Format("{0:00}:{1:00}:{2:00}", hours, minutes, secs);
        return string.Format("{0:00}:{1:00}", minutes, secs);
    }
}
