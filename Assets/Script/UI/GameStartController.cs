using UnityEngine;

public class GameStartController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject readyPanel;   // панель/кнопка "Готов"
    [SerializeField] private GameObject shopPanel;    // если магазин отдельной панелью
    [SerializeField] private EnemyStatsProgressionUI enemyStatsProgressionUI;

    [Header("Gameplay Systems (выключить до старта)")]
    [SerializeField] private MonoBehaviour enemySpawner; // EnemySpawner (как компонент)
    [SerializeField] private MonoBehaviour waveTimerUI;  // если есть отдельный таймер-скрипт
    [SerializeField] private GoldManager goldManager;    // если хочешь жестко выключать пассивку (опционально)

    [Header("Pause")]
    [SerializeField] private PauseManager pauseManager;  // чтобы запретить ESC до "Готов"

    private bool isRunStarted = false;

    public void Prepare()
    {
        isRunStarted = false;

        // показываем магазин + кнопку готов
        if (shopPanel != null) shopPanel.SetActive(true);
        if (readyPanel != null) readyPanel.SetActive(true);

        // стопаем время
        Time.timeScale = 0f;

        // выключаем системы, которые не должны жить до Ready
        if (enemySpawner != null) enemySpawner.enabled = false;
        if (waveTimerUI != null) waveTimerUI.enabled = false;

        // опционально: если у тебя пассивка может идти не через timescale
        // то можно на время подготовки её выключить
        // (в твоём GoldManager корутина с WaitForSeconds и так остановится при timeScale=0)
        if (goldManager != null) goldManager.enablePassiveIncome = false;

        if (pauseManager != null) pauseManager.SetCanPause(false);
    }

    // КНОПКА "ГОТОВ"
    public void OnReadyClicked()
    {
    if (isRunStarted) return;
    isRunStarted = true;

    if (readyPanel != null) readyPanel.SetActive(false);

    if (enemySpawner != null) enemySpawner.enabled = true;
    if (waveTimerUI != null) waveTimerUI.enabled = true;

    if (goldManager != null) goldManager.enablePassiveIncome = true;

    Time.timeScale = 1f;

    if (pauseManager != null) pauseManager.SetCanPause(true);

    // ✅ стартуем рост цифр
    if (enemyStatsProgressionUI != null)
        enemyStatsProgressionUI.StartProgression();
    else
        Debug.LogWarning("[GameStartController] enemyStatsProgressionUI is NOT assigned!");
}
}
