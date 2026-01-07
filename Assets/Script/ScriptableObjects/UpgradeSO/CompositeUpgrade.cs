using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Composite")]
public class CompositeUpgrade : UpgradeBaseSO
{
    public List<UpgradeBaseSO> upgrades;

    public override void Apply(UpgradeContextSO context)
    {
        foreach (var upgrade in upgrades)
        {
            upgrade.Apply(context);
        }
    }
}