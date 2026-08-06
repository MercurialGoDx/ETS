using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class ShopSlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text priceText;

    [Tooltip("Оверлей закрытого слота. Пусто — соберётся плейсхолдер (затемнение + LOCKED).")]
    [SerializeField] private GameObject lockedOverlay;

    [Header("Данные")]
    private WeaponDefinition currentWeapon;
    private UpgradeBaseSO currentUpgrade;
    private ShopManager shopManager;

    private bool isLocked;

    /// <summary>Слот закрыт: предмет не генерируется, клик не покупает.</summary>
    public bool IsLocked => isLocked;

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

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

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
    /// Временный вид закрытого слота, пока нет иконки замка: затемнение на всю ячейку и подпись.
    /// Как только в lockedOverlay положат готовый объект, этот код перестанет вызываться.
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

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.SetParent(rt, false);
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        var label = labelGo.GetComponent<TextMeshProUGUI>();
        label.text = "LOCKED";
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 10f;
        label.fontSizeMax = 72f;
        label.color = new Color(1f, 1f, 1f, 0.85f);
        label.raycastTarget = false;

        return go;
    }

    // ==== ОРУЖИЕ ====
    public void SetupWeapon(WeaponDefinition weapon, ShopManager manager)
    {
        SetUnlocked();

        currentWeapon = weapon;
        currentUpgrade = null;
        shopManager = manager;

        if (iconImage != null)
        {
            if (weapon != null && weapon.icon != null)
            {
                iconImage.sprite = weapon.icon;
                iconImage.enabled = true;
            }
            else
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }
        }

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

        if (iconImage != null)
        {
            if (upgrade != null && upgrade.icon != null)
            {
                iconImage.sprite = upgrade.icon;
                iconImage.enabled = true;
            }
            else
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }
        }

        if (priceText != null)
            priceText.text = upgrade != null ? upgrade.price.ToString() : "";
    }

    // ==== ОЧИСТКА ====
    public void Clear()
    {
        SetUnlocked();

        currentWeapon = null;
        currentUpgrade = null;
        shopManager = null;

        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        if (priceText != null)
            priceText.text = "";

        // на всякий — прячем тултип, если вдруг висел
        if (WeaponTooltip.Instance != null)
            WeaponTooltip.Instance.Hide();
    }

    // ==== КЛИК ====
    public void OnPointerClick(PointerEventData eventData)
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

    // ==== НАВЕДЕНИЕ ДЛЯ ТУЛТИПА ====
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isLocked) return;

        if (WeaponTooltip.Instance == null)
            return;

        if (currentWeapon != null)
        {
            WeaponTooltip.Instance.Show(currentWeapon);
        }
        else if (currentUpgrade != null)
        {
            WeaponTooltip.Instance.Show(
                currentUpgrade.GetLocalizedName(),
                currentUpgrade.GetLocalizedDescription()
            );
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (WeaponTooltip.Instance != null)
            WeaponTooltip.Instance.Hide();
    }
}
