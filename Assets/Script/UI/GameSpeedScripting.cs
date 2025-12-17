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

        _baseFixedDeltaTime = Time.fixedDeltaTime;
    }

    private void Start()
    {
        float startSpeed = 1f;
        if (saveSpeed) startSpeed = PlayerPrefs.GetFloat(PrefKey, 1f);
        ApplySpeed(startSpeed);
    }

    public void SetX1() => ApplySpeed(speedX1);
    public void SetX2() => ApplySpeed(speedX2);
    public void SetX3() => ApplySpeed(speedX3);

    public void ApplySpeed(float value)
    {
        // защита от нуля/отрицательных значений
        if (value <= 0f) value = 1f;

        CurrentSpeed = value;

        Time.timeScale = CurrentSpeed;
        Time.fixedDeltaTime = _baseFixedDeltaTime * Time.timeScale;

        if (saveSpeed) PlayerPrefs.SetFloat(PrefKey, CurrentSpeed);

        OnSpeedChanged?.Invoke(CurrentSpeed);
    }
}
