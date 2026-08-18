using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Utility/Duplicator")]
public class DuplicatorUpgrade : UpgradeBaseSO
{
    [Header("Next Purchase — Slot 1")]
    public ItemTier targetTier1 = ItemTier.Tier1;

    [Min(1)]
    public int duplicateCopies1 = 5;

    [Header("Next Purchase — Slot 2")]
    public ItemTier targetTier2 = ItemTier.Tier2;

    [Min(1)]
    public int duplicateCopies2 = 2;

    public override void Apply(UpgradeContextSO context)
    {
        if (context?.runtime == null)
        {
            Debug.LogWarning($"{name}: runtime data is not available.");
            return;
        }

        if (!context.runtime.TryArmDuplicator(targetTier1, duplicateCopies1, targetTier2, duplicateCopies2))
        {
            Debug.Log("[Duplicator] Purchase ignored because a duplicator charge is already active.");
            return;
        }

        Debug.Log($"[Duplicator] Armed for {targetTier1} (x{duplicateCopies1}) and {targetTier2} (x{duplicateCopies2}).");
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { (int)targetTier1, duplicateCopies1, (int)targetTier2, duplicateCopies2 };
    }
}
