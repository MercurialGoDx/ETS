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
        Debug.Log("asd");
        if (GameStateManager.Instance.Is(GameState.Playing))
        {
            CurrentSpeed = speed;
            Time.timeScale = CurrentSpeed;
        }
    }
}