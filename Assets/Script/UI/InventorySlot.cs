using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Ячейка панели инвентаря: иконка предмета, счётчик стеков и оформление по тиру
/// (цвет рамки + подложка того же оттенка). Только чтение — клик не переопределяется,
/// тултип по наведению достаётся от <see cref="ItemSlotBase"/>.
/// </summary>
public class InventorySlot : ItemSlotBase
{
    [Tooltip("Счётчик стеков в углу ячейки. При одном стеке скрывается.")]
    [SerializeField] private TMP_Text countText;

    [Tooltip("Рамка ячейки — Image со sliced-спрайтом, красится по тиру.")]
    [SerializeField] private Image frameImage;

    [Tooltip("Подложка под иконкой — красится тем же цветом тира, но полупрозрачно.")]
    [SerializeField] private Image backgroundImage;

    [Tooltip("Префикс перед числом стеков. У оружия «×», у улучшений пусто — как в мокапе.")]
    [SerializeField] private string stackPrefix = "×";

    // Базовая толщина sliced-рамки: pixelsPerUnitMultiplier из инспектора.
    // Толщина обратно пропорциональна множителю, поэтому «в 2 раза толще» = множитель / 2.
    private float baseFramePpuMultiplier = -1f;

    public void SetupWeapon(WeaponDefinition weapon, int stacks)
    {
        currentWeapon = weapon;
        currentUpgrade = null;

        SetIcon(weapon != null ? weapon.icon : null);
        SetStacks(stacks);
    }

    public void SetupUpgrade(UpgradeBaseSO upgrade, int stacks)
    {
        currentWeapon = null;
        currentUpgrade = upgrade;

        SetIcon(upgrade != null ? upgrade.icon : null);
        SetStacks(stacks);
    }

    /// <summary>Оформление по тиру: цвет рамки, подложка того же тона и относительная толщина рамки.</summary>
    public void SetTierFrame(Color color, float thicknessMultiplier, float backgroundAlpha)
    {
        if (frameImage != null)
        {
            frameImage.color = color;

            if (baseFramePpuMultiplier < 0f)
                baseFramePpuMultiplier = frameImage.pixelsPerUnitMultiplier;

            if (thicknessMultiplier > 0f)
                frameImage.pixelsPerUnitMultiplier = baseFramePpuMultiplier / thicknessMultiplier;
        }

        if (backgroundImage != null)
            backgroundImage.color = new Color(color.r, color.g, color.b, backgroundAlpha);
    }

    private void SetStacks(int stacks)
    {
        if (countText == null)
            return;

        // Единичный стек не показываем — визуальный шум.
        bool visible = stacks > 1;
        countText.gameObject.SetActive(visible);
        if (visible)
            countText.text = stackPrefix + stacks;
    }
}
