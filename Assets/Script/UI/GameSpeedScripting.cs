using ETS.Multiplayer;
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

    /// <summary>Темп зафиксирован и не переключается. Нужно дуэли: разная скорость
    /// у игроков сделала бы сравнение результатов бессмысленным.</summary>
    public bool IsLocked { get; private set; }

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

    private void Update()
    {
        if (GameStateManager.Instance == null || !GameStateManager.Instance.Is(GameState.Playing))
            return;

        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            SetX1();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            SetX2();
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            SetX3();
        }
    }

    public void SetX1() => ApplySpeed(speedX1);
    public void SetX2() => ApplySpeed(speedX2);
    public void SetX3() => ApplySpeed(speedX3);

    /// <summary>
    /// Ставит темп и запрещает дальнейшее переключение — и с клавиш, и с экранных кнопок.
    /// Единственный способ сменить скорость после этого — <see cref="UnlockSpeed"/>.
    /// </summary>
    public void LockSpeed(float value)
    {
        IsLocked = false;
        ApplySpeed(value);
        IsLocked = true;
    }

    public void UnlockSpeed() => IsLocked = false;

    public void ApplySpeed(float value)
    {
        // Одна точка отсечения: SetX1/X2/X3 публичные и висят на кнопках интерфейса,
        // защищать только ввод с клавиатуры было недостаточно.
        if (IsLocked)
            return;

        if (GameStateManager.Instance.Is(GameState.Playing))
        {
            CurrentSpeed = value;
            Time.timeScale = CurrentSpeed;
            OnSpeedChanged?.Invoke(CurrentSpeed);
        }
    }
}
