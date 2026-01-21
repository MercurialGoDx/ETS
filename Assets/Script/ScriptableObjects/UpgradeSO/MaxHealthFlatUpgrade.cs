using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Health/Max Health Flat")]
public class MaxHealthFlatUpgrade : UpgradeBaseSO
{
    public float valueFlat;

    public override void Apply(UpgradeContextSO context)
    {
        context.playerHealth.AddFlatMaxHealthAndHeal(valueFlat);
    }

    protected override object[] GetDescriptionArgs()
    {
        return new object[] { valueFlat };
    }
}