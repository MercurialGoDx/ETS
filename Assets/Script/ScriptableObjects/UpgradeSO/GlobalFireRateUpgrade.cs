using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Weapon/Global Fire Rate")]
public class GlobalFireRateUpgrade : UpgradeBaseSO
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
