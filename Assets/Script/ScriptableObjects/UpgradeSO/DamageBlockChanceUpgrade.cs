using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(menuName = "Upgrades/Health/Damage Block Chance")]
public class DamageBlockChanceUpgrade : UpgradeBaseSO
{
    [FormerlySerializedAs("valueFlat")]
    [Range(0f, 100f)]
    public float valuePercent = 10f;

    public override void Apply(UpgradeContextSO context)
    {
        if (context?.playerHealth == null)
        {
            Debug.LogWarning($"{name}: PlayerHealth is not bound in UpgradeContextSO.");
            return;
        }

        context.playerHealth.AddBlockChanceDiminishing(valuePercent);
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valuePercent };
    }
}
