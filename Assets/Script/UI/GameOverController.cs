using UnityEngine;

public class GameOverController : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private GameObject gameOverPanel;

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

        var stats = DamageStatsManager.Instance.GetDamageSorted();

        Debug.Log("Weapon stats:");

        foreach (var stat in stats)
        {
            Debug.Log($"{stat.weapon.GetLocalizedName()} → {stat.damage:F1}");
        }

        Time.timeScale = 0f; // стопаем игру
    }

    public void HideGameOver()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        Time.timeScale = 1f;
    }
}
