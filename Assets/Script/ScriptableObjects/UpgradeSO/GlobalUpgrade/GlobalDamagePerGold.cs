using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/GlobalUpgrade/Global Damage Per 100 Gold")]
public class GlobalDamagePerGold : UpgradeBaseSO
{
    [Tooltip("Percent damage per 100 unspent gold. 0.5 means +0.5%.")]
    public float valuePercent;

    public override void Apply(UpgradeContextSO context)
    {
        context.runtime.globalDamagePer100GoldPercent += valuePercent / 100f;
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valuePercent };
    }
}
