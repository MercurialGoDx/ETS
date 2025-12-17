using UnityEngine;
using UnityEngine.UI;

public class GameSpeedButtonUI : MonoBehaviour
{
    [Header("Кнопки")]
    public Button btnX1;
    public Button btnX2;
    public Button btnX3;

    [Header("Визуал (опционально)")]
    public Color normalColor = Color.white;
    public Color activeColor = Color.green;

    private void Start()
    {
        // Привязка кликов
        btnX1.onClick.AddListener(() => GameSpeedController.Instance.SetX1());
        btnX2.onClick.AddListener(() => GameSpeedController.Instance.SetX2());
        btnX3.onClick.AddListener(() => GameSpeedController.Instance.SetX3());

        // Подписка на изменения скорости
        GameSpeedController.Instance.OnSpeedChanged += UpdateVisual;

        // Обновим визуал сразу
        UpdateVisual(GameSpeedController.Instance.CurrentSpeed);
    }

    private void OnDestroy()
    {
        if (GameSpeedController.Instance != null)
            GameSpeedController.Instance.OnSpeedChanged -= UpdateVisual;
    }

    private void UpdateVisual(float speed)
    {
        SetButtonColor(btnX1, Approximately(speed, GameSpeedController.Instance.speedX1));
        SetButtonColor(btnX2, Approximately(speed, GameSpeedController.Instance.speedX2));
        SetButtonColor(btnX3, Approximately(speed, GameSpeedController.Instance.speedX3));
    }

    private void SetButtonColor(Button button, bool active)
    {
        if (button == null) return;
        var colors = button.colors;
        colors.normalColor = active ? activeColor : normalColor;
        colors.selectedColor = colors.normalColor;
        button.colors = colors;
    }

    private bool Approximately(float a, float b) => Mathf.Abs(a - b) < 0.01f;
}
