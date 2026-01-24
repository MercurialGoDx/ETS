using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Health/Heal Per Enemy Kill")]
public class HealOnKillUpgrade : UpgradeBaseSO
{
    public float valueFlat;

    public override void Apply(UpgradeContextSO context)
    {
        context.playerHealth.AddHealOnKill(valueFlat);
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valueFlat };
    }
}
