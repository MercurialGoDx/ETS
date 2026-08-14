using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/GlobalUpgrade/Adaptive Damage While Shield")]
public class AdaptiveDamageWhileShield : UpgradeBaseSO
{
    public float valuePercent;

    public override void Apply(UpgradeContextSO context)
    {
        context.runtime.adaptiveDamageWhileShieldPercent += valuePercent / 100f;
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valuePercent };
    }
}
