using UnityEngine;
using UnityEngine.EventSystems;

public class RerollButtonTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [TextArea(1, 2)]
    public string title = "Реролл магазина";

    [TextArea(2, 4)]
    public string description = "Обновляет все 5 слотов оружия и 5 слотов улучшений за плату.";

    [Header("Ссылки")]
    public ShopManager shopManager;

    private bool isHovered;

    private void Start()
    {
        if (shopManager == null)
            shopManager = FindObjectOfType<ShopManager>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        ShowTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;

        if (WeaponTooltip.Instance != null)
            WeaponTooltip.Instance.Hide();
    }

    /// <summary>
    /// Вызывай из ShopManager после изменения стоимости реролла.
    /// </summary>
    public void UpdateTooltip()
    {
        if (!isHovered) return;                 // курсор не на кнопке — не трогаем
        if (WeaponTooltip.Instance == null) return;

        ShowTooltip();                          // обновит текст, позиция сама догонится тултипом
    }

    private void ShowTooltip()
    {
        if (WeaponTooltip.Instance == null) return;

        string finalDescription = description;

        if (shopManager != null)
        {
            int currentPrice = shopManager.CurrentRerollPrice;
            finalDescription =
                $"{description}\n\n<color=#ffd700>Стоимость: {currentPrice} золота</color>";
        }

        WeaponTooltip.Instance.Show(title, finalDescription);
    }
}
