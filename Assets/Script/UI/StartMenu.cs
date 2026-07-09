using UnityEngine;
using UnityEngine.SceneManagement;


public class StartMenu : MonoBehaviour
{
    [SerializeField] private GameObject defeatWindow;
    [SerializeField] private DamageStatisticsUI statisticsWindow;

    [Header("UI Roots")]
    public GameObject startMenuCanvas;
    public GameObject gameUICanvas;

    [Header("Start Controller")]
    public GameStartController gameStartController; // добавим

    private void Start()
    {
        Time.timeScale = 0f;

        if (startMenuCanvas != null) startMenuCanvas.SetActive(true);
        if (gameUICanvas != null) gameUICanvas.SetActive(false);
    }

    // КНОПКА "НАЧАТЬ"
    public void StartGame()
    {
        if (startMenuCanvas != null) startMenuCanvas.SetActive(false);
        if (gameUICanvas != null) gameUICanvas.SetActive(true);

        Debug.Log("StartGameWasPressed!!!");

        GameStateManager.Instance.SetState(GameState.Preparing);
        Debug.Log($"Current state is {GameStateManager.Instance.CurrentState}");

        if (gameStartController != null)
            gameStartController.Prepare(); // покажет магазин/кнопку "Готов", отключит спавнеры

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayGameMusic();

        Debug.Log("StartGame pressed (prepare)");
    }

    public void ExitGame()
    {
        Debug.Log("ExitGame pressed");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OpenStatistics()
    {
        defeatWindow.SetActive(false);
        // Прячем список покупок, чтобы он не наслаивался на окно статистики.
        if (PurchaseHistoryManager.Instance != null)
            PurchaseHistoryManager.Instance.SetVisible(false);
        statisticsWindow.Show();
    }

    public void CloseStatistics()
    {
        statisticsWindow.Hide();
        defeatWindow.SetActive(true);
        // Возвращаемся к окну поражения — снова показываем список покупок.
        if (PurchaseHistoryManager.Instance != null)
            PurchaseHistoryManager.Instance.SetVisible(true);
    }
}
