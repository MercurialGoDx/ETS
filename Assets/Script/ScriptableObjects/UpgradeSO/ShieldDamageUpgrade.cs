using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Weapon/Shield Active Damage")]
public class ShieldDamageUpgrade : UpgradeBaseSO
{
    public float valuePercent;

    public override void Apply(UpgradeContextSO context)
    {
        // Шкала: valuePercent = 50 означает +50%; бонус в калькуляторе — доля.
        // Без /100 давало бы +5000%. Класс сейчас не используется ассетами,
        // но оставлен исправленным, чтобы не был ловушкой.
        context.runtime.damageWhileShieldActivePercent += valuePercent / 100f;
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valuePercent };
    }
}