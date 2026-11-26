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

    /// <summary>
    /// Можно ли сейчас открыть паузу: идёт забег, не открыта другая панель, не выбирается
    /// награда босса (она сама держит Time.timeScale = 0, а выход из паузы вернул бы скорость
    /// под открытым окном) и это не экран поражения.
    /// </summary>
    public bool CanPause => canPause
        && !BossRewardUI.IsSelectionOpen
        && (GameStateManager.Instance == null || !GameStateManager.Instance.Is(GameState.GameOver));

    public bool IsPaused => isPaused;

    /// <summary>То же, что Esc, — для экранной кнопки паузы на телефоне.</summary>
    public void TogglePause()
    {
        if (!isPaused && !CanPause) return;

        if (isPaused)
            ResumeGame();
        else
            Pause();
    }

    // На телефоне игрок может свернуть приложение посреди боя (звонок, шторка, кнопка «Домой»).
    // Забег не должен идти без него: открываем обычное меню паузы, и по возвращении он сам
    // решает, когда продолжить. На ПК сворачивание окна поведение не меняет.
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && MobilePlatform.IsMobile && CanPause && !isPaused)
            Pause();
    }

    private void Update()
    {
        if (!isPaused && !CanPause) return;
        if (isPaused && !canPause) return;
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
