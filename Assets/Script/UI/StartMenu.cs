using UnityEngine;

public class StartMenu : MonoBehaviour
{
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

        // НЕ запускаем игру — только подготовка
        Time.timeScale = 0f;

        if (gameStartController != null)
            gameStartController.Prepare(); // покажет магазин/кнопку "Готов", отключит спавнеры

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayGameMusic();

        Debug.Log("StartGame pressed (prepare)");
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
        Debug.Log("ExitGame pressed");
    }
}
