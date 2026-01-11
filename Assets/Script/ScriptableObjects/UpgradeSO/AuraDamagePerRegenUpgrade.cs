using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Weapon/Damage Aura Per Regen")]
public class AuraDamagePerRegenUpgrade : UpgradeBaseSO
{
    public float valueFlat;

    public override void Apply(UpgradeContextSO context)
    {
        context.regenAuraDamage.regenAuraEnabled = true;
        context.regenAuraDamage.regenAuraMultiplier += valueFlat;
    }
}
