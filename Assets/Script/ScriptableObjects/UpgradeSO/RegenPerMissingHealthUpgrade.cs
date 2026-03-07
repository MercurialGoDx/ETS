using UnityEngine;


[CreateAssetMenu(menuName = "Upgrades/Health/Regen Per Missing Health")]
public class RegenPerMissingHealthUpgrade : UpgradeBaseSO
{
    public float valuePercent;

    public override void Apply(UpgradeContextSO context)
    {
        context.playerHealth.AddRegenPer100MissingHealth(valuePercent);
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valuePercent };
    }
}
