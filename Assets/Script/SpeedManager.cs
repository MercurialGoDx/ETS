using UnityEngine;

public class SpeedManager : MonoBehaviour
{
    public static SpeedManager Instance { get; private set; }

    public float CurrentSpeed { get; private set; } = 1f;

    private void Awake()
    {
        Instance = this;
    }

    public void SetSpeed(float speed)
    {
        CurrentSpeed = speed;

        if (GameStateManager.Instance.Is(GameState.Playing))
        {
            Time.timeScale = speed;
        }
    }
}