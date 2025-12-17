using UnityEngine;
using UnityEngine.EventSystems;

public class RerollButtonTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [TextArea(1, 2)]
    public string title = "Реролл магазина";

    [TextArea(2, 4)]
    public string description = "Обновляет все 5 слотов оружия и 5 слотов улучшений за плату.";

    [Header("Ссылки")]
    [Tooltip("ShopManager для получения текущей стоимости реролла. Если не задан, будет найден автоматически.")]
    public ShopManager shopManager;

    private void Start()
    {
        // Если ShopManager не задан в инспекторе, пытаемся найти его автоматически
        if (shopManager == null)
        {
            shopManager = FindObjectOfType<ShopManager>();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (WeaponTooltip.Instance == null)
            return;

        // Формируем описание с текущей стоимостью реролла
        string finalDescription = description;
        
        if (shopManager != null)
        {
            int currentPrice = shopManager.CurrentRerollPrice;
            finalDescription = $"{description}\n\n<color=#ffd700>Стоимость: {currentPrice} золота</color>";
        }

        WeaponTooltip.Instance.Show(title, finalDescription, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (WeaponTooltip.Instance != null)
            WeaponTooltip.Instance.Hide();
    }

    /// <summary>
    /// Обновляет тултип с актуальной стоимостью реролла.
    /// Вызывается из ShopManager после изменения стоимости.
    /// </summary>
    public void UpdateTooltip()
    {
        // Если тултип не активен, ничего не делаем
        if (WeaponTooltip.Instance == null || !WeaponTooltip.Instance.gameObject.activeSelf)
            return;

        // Получаем текущую позицию курсора для обновления тултипа
        Vector3 mousePosition = Input.mousePosition;
        
        // Формируем описание с текущей стоимостью реролла
        string finalDescription = description;
        
        if (shopManager != null)
        {
            int currentPrice = shopManager.CurrentRerollPrice;
            finalDescription = $"{description}\n\n<color=#ffd700>Стоимость: {currentPrice} золота</color>";
        }

        WeaponTooltip.Instance.Show(title, finalDescription, mousePosition);
    }
}
