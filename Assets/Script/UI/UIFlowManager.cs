using UnityEngine;
using UnityEngine.SceneManagement;

public class UIFlowManager : MonoBehaviour
{
    [Header("Canvases")]
    [SerializeField] private GameObject gameCanvas;
    [SerializeField] private GameObject mainMenuCanvas;

    [Header("Game Over")]
    [SerializeField] private GameOverController gameOverController;

    // Флаг переживает перезагрузку сцены
    public static bool AutoStartAfterReload = false;

    private void Awake()
    {
        Time.timeScale = 1f;

        if (!enabled) enabled = true;

        if (gameCanvas == null)
        {
            var go = GameObject.Find("GameCanvas");
            if (go != null) gameCanvas = go;
        }

        if (mainMenuCanvas == null)
        {
            var go = GameObject.Find("MainMenuCanvas");
            if (go != null) mainMenuCanvas = go;
        }

        // Авто-старт после перезагрузки сцены
        if (AutoStartAfterReload)
        {
            AutoStartAfterReload = false;
            StartGame();
        }
        else
        {
            ShowMainMenuOnly();
        }
    }

    // === КНОПКА "НАЧАТЬ ИГРУ" (ЕДИНСТВЕННАЯ) ===
    // Привяжи её в OnClick() к StartButtonClicked()
    public void StartButtonClicked()
    {
        Time.timeScale = 1f;
        AutoStartAfterReload = true;

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // === КНОПКА "ГЛАВНОЕ МЕНЮ" НА GAME OVER PANEL ===
    // Возврат в главное меню с полной перезагрузкой сцены — чистый сброс забега
    // (как в меню паузы). AutoStartAfterReload = false → после перезагрузки покажется
    // меню, а не запустится игра.
    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        AutoStartAfterReload = false;

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // === ВКЛЮЧИТЬ ИГРУ (без перезагрузки сцены) ===
    // Этот метод вызывается автоматически после перезагрузки (в Awake),
    // но можешь вызывать вручную, если понадобится.
    public void StartGame()
    {
        if (mainMenuCanvas != null) mainMenuCanvas.SetActive(false);
        if (gameCanvas != null) gameCanvas.SetActive(true);

        Time.timeScale = 1f;
    }

    private void ShowMainMenuOnly()
    {
        if (gameCanvas != null) gameCanvas.SetActive(false);
        if (mainMenuCanvas != null) mainMenuCanvas.SetActive(true);

        Time.timeScale = 1f;
    }
}
