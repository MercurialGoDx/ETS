using UnityEngine;
using UnityEngine.Localization.Settings;
using TMPro;

public class GameOverController : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private GameObject gameOverPanel;
    [Tooltip("Источник снимка билда (оружие/улучшения/статы) для лидерборда.")]
    [SerializeField] private InventoryUI inventoryUI;

    [Header("Время забега")]
    [SerializeField] private GameTimeUI gameTimeUI;
    [SerializeField] private TMP_Text playTimeText;
    // Ключ локализации "Your time: {0}" (Game Labels/ui.your_time). {0} — отформатированное время.
    [SerializeField] private string playTimeTable = "Game Labels";
    [SerializeField] private string playTimeKey = "ui.your_time";

    private void Awake()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    private void OnEnable()
    {
        if (playerHealth != null)
            playerHealth.OnDied += OnPlayerDied;
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnDied -= OnPlayerDied;
    }

    private void OnPlayerDied()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        // Показываем, сколько игрок продержался (таймер замирает при GameOver вместе с timeScale).
        // Текст берём из локализации в текущем языке: "Ваше время: {0}" / "Your time: {0}" ...
        if (playTimeText != null && gameTimeUI != null)
        {
            string formatted = gameTimeUI.GetFormattedTime();
            playTimeText.text = LocalizationSettings.StringDatabase.GetLocalizedString(
                playTimeTable, playTimeKey, new object[] { formatted });
        }

        // Отправляем результат забега в таблицу лидеров (Steam, либо заглушка вне Steam) —
        // вместе со снимком билда, чтобы другие игроки могли открыть его со строки лидерборда.
        if (gameTimeUI != null)
        {
            int[] buildDetails = inventoryUI != null ? inventoryUI.CaptureSnapshot().Encode() : null;
            Leaderboards.Service.SubmitTime(gameTimeUI.ElapsedTime, buildDetails);
        }

        var stats = DamageStatsManager.Instance.GetAllDamageSorted();

        Debug.Log("Damage stats:");

        foreach (var stat in stats)
        {
            Debug.Log($"{stat.GetLocalizedName()} → {stat.Damage:F1}");
        }

        GameStateManager.Instance.SetState(GameState.GameOver);
    }

    public void HideGameOver()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        Time.timeScale = 1f;
    }
}
