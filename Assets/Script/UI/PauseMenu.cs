using UnityEngine;
using UnityEngine.SceneManagement;
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
    // Пауза запрещена до старта забега: GameStartController.OnReadyClicked() включит её
    // через SetCanPause(true). Так Esc не ставит игру на паузу в главном меню/подготовке.
    private bool canPause = false;

    // Останавливали ли мы время сами. В дуэли не останавливаем — там меню открывается
    // поверх идущего забега, — и тогда возвращать состояние на выходе тоже нечего.
    private bool didFreezeTime;

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
            {
                Pause();
            }
            else
            {
                ResumeGame();
            }
        }
    }

    private void Pause()
    {
        isPaused = true;

        // В дуэли время не останавливаем: пока один сидит в меню паузы, второй играет,
        // и остановка отдала бы ему фору.
        didFreezeTime = !DuelSession.IsSeeded;
        if (didFreezeTime)
            GameStateManager.Instance.SetState(GameState.Paused);

        pauseMenuCanvas.gameObject.SetActive(true);
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

        if (didFreezeTime)
        {
            GameStateManager.Instance.SetState(GameStateManager.Instance.PreviousState);
            didFreezeTime = false;
        }

        pauseMenuCanvas.gameObject.SetActive(false);
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
        SceneManager.LoadScene("MainScene");

        //if (pauseMenuCanvas != null)
        //{
        //    pauseMenuCanvas.enabled = false;
        //    pauseMenuCanvas.gameObject.SetActive(false);
        //}

        //if (gameUICanvas != null)
        //{
        //    gameUICanvas.enabled = false;
        //    gameUICanvas.gameObject.SetActive(false);
        //}

        //if (startMenuCanvas != null)
        //{
        //    startMenuCanvas.gameObject.SetActive(true);
        //    startMenuCanvas.enabled = true;
        //}

        //GameStateManager.Instance.SetState(GameState.Menu);
        
        //isPaused = false;

        //if (AudioManager.Instance != null)
        //{
        //    AudioManager.Instance.ResetPauseFx();
        //    AudioManager.Instance.PlayMenuMusic();
        //}

        //Cursor.visible = true;
        //Cursor.lockState = CursorLockMode.None;
    }
}
