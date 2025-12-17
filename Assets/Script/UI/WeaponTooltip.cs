using UnityEngine;
using TMPro;

public class WeaponTooltip : MonoBehaviour
{
    public static WeaponTooltip Instance;

    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private Vector2 offset = new Vector2(10f, -10f);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        Hide();
    }

    // Старый метод — для оружия
    public void Show(WeaponDefinition weapon, Vector2 screenPos)
    {
        if (weapon == null) return;
        ShowInternal(weapon.weaponName, weapon.description, screenPos);
    }

    // Новый универсальный метод — можно вызывать для апгрейдов
    public void Show(string title, string description, Vector2 screenPos)
    {
        ShowInternal(title, description, screenPos);
    }

    private void ShowInternal(string title, string description, Vector2 screenPos)
    {
        gameObject.SetActive(true);

        if (titleText != null)
            titleText.text = title;

        if (descriptionText != null)
            descriptionText.text = description;

        // Панель можно оставить фиксированной; screenPos игнорируем,
        // либо позже сделаем позиционирование по мышке
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}