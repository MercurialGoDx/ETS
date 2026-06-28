using UnityEngine;
using TMPro;        // если используешь TextMeshPro

public class GameTimeUI : MonoBehaviour
{
    public TextMeshProUGUI timeText;

    private float elapsedTime = 0f;

    /// <summary>Сколько секунд длится текущий забег.</summary>
    public float ElapsedTime { get { return elapsedTime; } }

    private void Update()
    {
        elapsedTime += Time.deltaTime;
        timeText.text = GetFormattedTime();
    }

    /// <summary>Текущее время забега в формате 00:00 (или 01:23:45, если есть часы).</summary>
    public string GetFormattedTime()
    {
        int totalSeconds = Mathf.FloorToInt(elapsedTime);
        int hours   = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;

        // формат 00:00 или 01:23:45
        if (hours > 0)
            return $"{hours:00}:{minutes:00}:{seconds:00}";
        return $"{minutes:00}:{seconds:00}";
    }

    public void ResetTimer()
    {
        elapsedTime = 0f;
    }
}
