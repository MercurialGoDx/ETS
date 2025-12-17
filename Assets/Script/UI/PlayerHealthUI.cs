using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHealthUI : MonoBehaviour
{
    public enum ValueType
    {
        Health,
        Shield
    }

    [Header("Настройки")]
    public PlayerHealth playerHealth;
    public ValueType valueType = ValueType.Health;

    [Header("UI")]
    public Text legacyText;             // если используешь обычный Text
    public TextMeshProUGUI tmpText;     // если используешь TMP
    public Image fillBar;               // необязательно, если полосой управляет другой скрипт

    private void Update()
    {
        if (playerHealth == null)
            return;

        float current = 0f;
        float max = 0f;

        //
        // 1. Определяем текущие значения
        //
        switch (valueType)
        {
            case ValueType.Health:
                current = Mathf.Max(playerHealth.CurrentHealth, 0f);
                max = Mathf.Max(playerHealth.maxHealth, 1f);
                break;

            case ValueType.Shield:
                current = Mathf.Max(playerHealth.CurrentShield, 0f);
                max = Mathf.Max(playerHealth.MaxShield, 0f);
                break;
        }

        //
        // 2. Подготавливаем текст
        //
        int curInt = Mathf.CeilToInt(current);
        int maxInt = Mathf.CeilToInt(max);

        string text;

        // Если щита нет — показываем 0/0
        if (valueType == ValueType.Shield && maxInt <= 0)
        {
            text = "0 / 0";
        }
        else
        {
            text = $"{curInt} / {maxInt}";
        }

        //
        // 3. Обновляем текст
        //
        if (legacyText != null)
            legacyText.text = text;

        if (tmpText != null)
            tmpText.text = text;

        //
        // 4. Обновляем полоску (если есть)
        //
        if (fillBar != null)
        {
            if (max > 0)
                fillBar.fillAmount = Mathf.Clamp01(current / max);
            else
                fillBar.fillAmount = 0f; // если щита нет — полоса пустая
        }
    }
}
