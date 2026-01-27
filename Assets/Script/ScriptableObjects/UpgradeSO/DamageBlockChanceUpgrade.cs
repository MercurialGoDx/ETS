using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Health/Damage Block Chance")]
public class DamageBlockChanceUpgrade : UpgradeBaseSO
{
    public int valueFlat = 1;

    public override void Apply(UpgradeContextSO context)
    {
        if (context?.playerHealth == null)
        {
            Debug.LogWarning($"{name}: PlayerHealth is not bound in UpgradeContextSO.");
            return;
        }

        context.playerHealth.AddBlockChanceDiminishing(valueFlat);
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valueFlat };
    }
}
