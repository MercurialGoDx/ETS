using UnityEngine;
using System;

public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    public GameState CurrentState { get; private set; }
    public GameState PreviousState { get; private set; }

    public event Action<GameState> OnStateChanged;

    private void Awake()
    {
        Instance = this;
        SetState(GameState.Menu);
    }

    public void SetState(GameState newState)
    {
        if (CurrentState == newState)
            return;

        PreviousState = CurrentState;
        CurrentState = newState;

        ApplyTimeScale(newState);

        OnStateChanged?.Invoke(newState);
    }

    private void ApplyTimeScale(GameState state)
    {
        switch (state)
        {
            case GameState.Menu:
            case GameState.Preparing:
            case GameState.GameOver:
                Time.timeScale = 0f;
                break;

            case GameState.Playing:
                Time.timeScale = GameSpeedController.Instance.CurrentSpeed;
                break;

            case GameState.Paused:
                Time.timeScale = 0f;
                break;
        }
    }

    public bool Is(GameState state) => CurrentState == state;
}