using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Weapon/Damage While Shield Active Percent")]
public class DamageWhileShieldActivePercentUpgrade : UpgradeBaseSO
{
    public float valuePercent;

    public override void Apply(UpgradeContextSO context)
    {
        // Пишем в runtime — его читает ShieldDamageBonus в DamageCalculator.
        // Старый путь (playerShield.AddDamageWhileShieldActivePercent) никем не
        // читался: апгрейд не работал вовсе.
        // Шкала: в ассете 50 (= +50%), бонус в калькуляторе — доля, поэтому /100.
        context.runtime.damageWhileShieldActivePercent += valuePercent / 100f;
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valuePercent };
    }
}
