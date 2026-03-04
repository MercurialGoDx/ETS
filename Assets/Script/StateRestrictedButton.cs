using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class StateRestrictedButton : MonoBehaviour
{
    public GameState[] allowedStates;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        GameStateManager.Instance.OnStateChanged += UpdateState;
        UpdateState(GameStateManager.Instance.CurrentState);
    }

    private void OnDisable()
    {
        GameStateManager.Instance.OnStateChanged -= UpdateState;
    }

    private void UpdateState(GameState state)
    {
        bool allowed = false;

        foreach (var s in allowedStates)
        {
            if (s == state)
            {
                allowed = true;
                break;
            }
        }

        button.interactable = allowed;
    }
}