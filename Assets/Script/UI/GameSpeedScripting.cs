using UnityEngine;

public class GameSpeedController : MonoBehaviour
{
    public static GameSpeedController Instance { get; private set; }

    [Header("Доступные скорости")]
    public float speedX1 = 1f;
    public float speedX2 = 2f;
    public float speedX3 = 3f;

    [Header("Сохранение")]
    public bool saveSpeed = true;
    private const string PrefKey = "GameSpeed";

    private float _baseFixedDeltaTime;

    public float CurrentSpeed { get; private set; } = 1f;

    public System.Action<float> OnSpeedChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Баланс из таблицы (если импортирован) перекрывает инспектор.
        var cfg = BalanceService.Config;
        if (cfg != null)
        {
            speedX1 = cfg.player.gameSpeed1;
            speedX2 = cfg.player.gameSpeed2;
            speedX3 = cfg.player.gameSpeed3;
        }

        _baseFixedDeltaTime = Time.fixedDeltaTime;
    }

    private void Start()
    {
        float startSpeed = 1f;
        if (saveSpeed) startSpeed = PlayerPrefs.GetFloat(PrefKey, 1f);
    }

    public void SetX1() => ApplySpeed(speedX1);
    public void SetX2() => ApplySpeed(speedX2);
    public void SetX3() => ApplySpeed(speedX3);

    public void ApplySpeed(float value)
    {
        if (GameStateManager.Instance.Is(GameState.Playing))
        {
            CurrentSpeed = value;
            Time.timeScale = CurrentSpeed;
            OnSpeedChanged?.Invoke(CurrentSpeed);
        }
    }
}
