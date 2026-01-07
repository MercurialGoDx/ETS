using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Weapon/Global Fire Rate")]
public class GlobalFireRateUpgrade : UpgradeBaseSO
{
    public float percentValue;

    public override void Apply(UpgradeContextSO context)
    {
        context.towerAttack.fireRateMultiplier += percentValue / 100f;
    }
}
