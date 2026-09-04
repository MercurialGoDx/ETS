using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class ShopSlot : ItemSlotBase
{
    [SerializeField] private TMP_Text priceText;

    [Tooltip("Оверлей закрытого слота. Пусто — соберётся плейсхолдер (затемнение + иконка замка).")]
    [SerializeField] private GameObject lockedOverlay;

    [Tooltip("Иконка замка для плейсхолдера закрытого слота. Не задана — оверлей будет без иконки (только затемнение).")]
    [SerializeField] private Sprite lockIcon;

    [Header("Данные")]
    private ShopManager shopManager;

    private bool isLocked;

    /// <summary>Слот закрыт: предмет не генерируется, клик не покупает.</summary>
    public bool IsLocked => isLocked;

    /// <summary>Закрытый слот не показывает тултип.</summary>
    protected override bool TooltipAllowed => !isLocked;

    // ==== ЗАКРЫТЫЙ СЛОТ ====

    /// <summary>
    /// Переводит слот в закрытое состояние: пустой, с оверлеем, не реагирует на клик и наведение.
    /// Открывается обратно любым из Setup* или Clear().
    /// </summary>
    public void SetLocked()
    {
        currentWeapon = null;
        currentUpgrade = null;
        shopManager = null;
        isLocked = true;

        SetIcon(null);

        if (priceText != null)
            priceText.text = "";

        ShowLockedOverlay(true);
    }

    private void SetUnlocked()
    {
        isLocked = false;
        ShowLockedOverlay(false);
    }

    private void ShowLockedOverlay(bool visible)
    {
        if (lockedOverlay == null)
        {
            if (!visible)
                return;

            lockedOverlay = BuildPlaceholderOverlay();
        }

        lockedOverlay.SetActive(visible);
    }

    /// <summary>
    /// Временный вид закрытого слота, пока в lockedOverlay не положили готовый объект:
    /// затемнение на всю ячейку (цвет не трогать — так и должно оставаться) плюс иконка
    /// замка по центру, если она задана в lockIcon.
    /// </summary>
    private GameObject BuildPlaceholderOverlay()
    {
        var go = new GameObject("LockedOverlay", typeof(RectTransform), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var bg = go.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.72f);
        bg.raycastTarget = false; // клик остаётся на самом слоте, он и решает, что делать

        var iconGo = new GameObject("LockIcon", typeof(RectTransform), typeof(Image));
        var iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.SetParent(rt, false);
        // Иконка по центру, не на всю ячейку — как и обычные иконки предметов в слоте.
        iconRt.anchorMin = new Vector2(0.2f, 0.2f);
        iconRt.anchorMax = new Vector2(0.8f, 0.8f);
        iconRt.offsetMin = Vector2.zero;
        iconRt.offsetMax = Vector2.zero;

        var iconImage = iconGo.GetComponent<Image>();
        iconImage.sprite = lockIcon;
        iconImage.enabled = lockIcon != null;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        return go;
    }

    // ==== ОРУЖИЕ ====
    public void SetupWeapon(WeaponDefinition weapon, ShopManager manager)
    {
        SetUnlocked();

        currentWeapon = weapon;
        currentUpgrade = null;
        shopManager = manager;

        SetIcon(weapon != null ? weapon.icon : null);

        if (priceText != null)
            priceText.text = weapon != null ? weapon.price.ToString() : "";
    }

    // ==== УЛУЧШЕНИЕ ====
    public void SetupUpgrade(UpgradeBaseSO upgrade, ShopManager manager)
    {
        SetUnlocked();

        currentWeapon = null;
        currentUpgrade = upgrade;
        shopManager = manager;

        SetIcon(upgrade != null ? upgrade.icon : null);

        RefreshPrice();
    }

    /// <summary>Обновляет цену, не меняя выпавший в слоте предмет.</summary>
    public void RefreshPrice()
    {
        if (priceText == null)
            return;

        if (currentWeapon != null)
        {
            priceText.text = currentWeapon.price.ToString();
            return;
        }

        priceText.text = currentUpgrade != null && shopManager != null
            ? shopManager.GetUpgradePrice(currentUpgrade).ToString()
            : "";
    }

    // ==== ОЧИСТКА ====
    public void Clear()
    {
        SetUnlocked();

        currentWeapon = null;
        currentUpgrade = null;
        shopManager = null;

        SetIcon(null);

        if (priceText != null)
            priceText.text = "";

        // на всякий — прячем тултип, если вдруг висел
        if (WeaponTooltip.Instance != null)
            WeaponTooltip.Instance.Hide();
    }

    // ==== КЛИК ====
    public override void OnPointerClick(PointerEventData eventData)
    {
        // Закрытый слот не покупается. Проверка здесь, а не через Button.interactable:
        // StateRestrictedButton перезаписывает interactable при каждой смене GameState
        // и снял бы замок на переходе Preparing -> Playing.
        if (isLocked) return;

        if (shopManager == null) return;
        if (GameStateManager.Instance.CurrentState != GameState.Preparing && GameStateManager.Instance.CurrentState != GameState.Playing) return;

        if (currentWeapon != null)
        {
            shopManager.BuyWeapon(currentWeapon, this);
        }
        else if (currentUpgrade != null)
        {
            shopManager.BuyUpgrade(currentUpgrade, this);
        }
    }
}
