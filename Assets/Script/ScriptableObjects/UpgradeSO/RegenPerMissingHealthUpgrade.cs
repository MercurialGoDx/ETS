using UnityEngine;


[CreateAssetMenu(menuName = "Upgrades/Health/Regen Per Missing Health")]
public class RegenPerMissingHealthUpgrade : UpgradeBaseSO
{
    public float valueFlat;

    public override void Apply(UpgradeContextSO context)
    {
        context.playerHealth.AddRegenPer100MissingHealth(valueFlat);
    }

    protected override object[] GetDescriptionArgs()
    {
        return new object[] { valueFlat };
    }
}
