using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Tick Upgrade/Regen Per Tick Scaling")]
public class RegenPerTickUpgrade : UpgradeBaseSO
{
    public float interval;
    public float valueFlat;

    public override void Apply(UpgradeContextSO context)
    {
        UpgradePerTick.Instance.AddRegenPerTick(valueFlat, interval);
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valueFlat, interval };
    }
}
