using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Weapon/Fire Rate")]
public class FireRate : UpgradeBaseSO
{
    public float valuePercent;

    public override void Apply(UpgradeContextSO context)
    {
        context.towerAttack.fireRateMultiplier += valuePercent / 100f;
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valuePercent };
    }
}
