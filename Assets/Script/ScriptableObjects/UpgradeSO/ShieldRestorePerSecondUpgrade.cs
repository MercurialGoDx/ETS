using UnityEngine;

[CreateAssetMenu(
    menuName = "Upgrades/Shield/Shield Restore Per Second",
    fileName = "Shield Restore Per Second")]
public sealed class ShieldRestorePerSecondUpgrade : UpgradeBaseSO
{
    [Header("Active Shield Restore")]
    [Tooltip("Какой процент от Max Shield восстанавлиается за секунду за одну покупку.")]
    [Min(0f)] public float valuePercent = 1f;

    public override void Apply(UpgradeContextSO context)
    {
        if (context?.playerShield == null)
        {
            Debug.LogWarning($"{name}: PlayerShield is not available.");
            return;
        }

        context.playerShield.AddShieldRestorePerSecondPercent(valuePercent);
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valuePercent };
    }
}
