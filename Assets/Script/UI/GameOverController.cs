using UnityEngine;
using TMPro;

public class GameOverController : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private GameObject gameOverPanel;

    [Header("Время забега")]
    [SerializeField] private GameTimeUI gameTimeUI;
    [SerializeField] private TMP_Text playTimeText;
    [SerializeField] private string playTimePrefix = "Ваше время: ";

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
        if (playTimeText != null && gameTimeUI != null)
            playTimeText.text = playTimePrefix + gameTimeUI.GetFormattedTime();

        var stats = DamageStatsManager.Instance.GetDamageSorted();

        Debug.Log("Weapon stats:");

        foreach (var stat in stats)
        {
            Debug.Log($"{stat.weapon.GetLocalizedName()} → {stat.damage:F1}");
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
