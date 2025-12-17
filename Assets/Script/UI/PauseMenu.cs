using UnityEngine;
using UnityEngine.UI;

public class PauseManager : MonoBehaviour
{
    [Header("Canvases")]
    public Canvas pauseMenuCanvas;
    public Canvas startMenuCanvas;
    public Canvas gameUICanvas;

    [Header("Buttons")]
    public Button resumeButton;
    public Button mainMenuButton;

    private bool isPaused = false;
    private bool canPause = true;

    private void Start()
    {
        if (pauseMenuCanvas != null)
            pauseMenuCanvas.enabled = false;

        if (resumeButton != null)
            resumeButton.onClick.AddListener(ResumeGame);

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(GoToMainMenu);
    }

    public void SetCanPause(bool value)
    {
        canPause = value;
    }
    private void Update()
    {
        if (!canPause) return;
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (!isPaused)
                Pause();
            else
                ResumeGame();
        }
    }

    private void Pause()
    {
        isPaused = true;
        Time.timeScale = 0f;

        if (pauseMenuCanvas != null)
            pauseMenuCanvas.enabled = true;

        if (gameUICanvas != null)
            gameUICanvas.enabled = false;

        if (AudioManager.Instance != null)
            AudioManager.Instance.ApplyPauseFx();

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;

        if (pauseMenuCanvas != null)
            pauseMenuCanvas.enabled = false;

        if (gameUICanvas != null)
            gameUICanvas.enabled = true;

        if (AudioManager.Instance != null)
            AudioManager.Instance.ResetPauseFx();

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void GoToMainMenu()
    {
        if (pauseMenuCanvas != null)
        {
            pauseMenuCanvas.enabled = false;
            pauseMenuCanvas.gameObject.SetActive(false);
        }

        if (gameUICanvas != null)
        {
            gameUICanvas.enabled = false;
            gameUICanvas.gameObject.SetActive(false);
        }

        if (startMenuCanvas != null)
        {
            startMenuCanvas.gameObject.SetActive(true);
            startMenuCanvas.enabled = true;
        }

        Time.timeScale = 0f;
        isPaused = false;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.ResetPauseFx();
            AudioManager.Instance.PlayMenuMusic();
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}
