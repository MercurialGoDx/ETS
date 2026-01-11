using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Weapon/Damage From Max Health Percent")]
public class DamageFromMaxHealthPercentUpgrade : UpgradeBaseSO
{
    public float valuePercent;

    public override void Apply(UpgradeContextSO context)
    {
        context.runtime.damageFromMaxHealthPercent += context.playerHealth.MaxHealth * (valuePercent / 100);
    }
}
