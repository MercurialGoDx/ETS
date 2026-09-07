using UnityEngine;

[CreateAssetMenu(
    fileName = "Percent regen from Max HP",
    menuName = "Upgrades/Health/Percent regen from Max HP")]
public sealed class PercentRegenFromMaxHPUpgrade : UpgradeBaseSO
{
    [Min(0f)]
    [Tooltip("Процент максимального здоровья, восстанавливаемый каждую секунду.")]
    public float valuePercent = 1f;

    public override void Apply(UpgradeContextSO context)
    {
        if (context?.playerHealth == null)
        {
            Debug.LogWarning($"{name}: PlayerHealth is not available.");
            return;
        }

        context.playerHealth.AddRegenPercentFromMaxHealth(valuePercent);
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valuePercent };
    }
}
