using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Utility/Duplicator")]
public class DuplicatorUpgrade : UpgradeBaseSO
{
    [Header("Next Purchase")]
    public ItemTier targetTier = ItemTier.Tier2;

    [Min(1)]
    public int duplicateCopies = 1;

    public override void Apply(UpgradeContextSO context)
    {
        if (context?.runtime == null)
        {
            Debug.LogWarning($"{name}: runtime data is not available.");
            return;
        }

        if (!context.runtime.TryArmDuplicator(targetTier, duplicateCopies))
        {
            Debug.Log("[Duplicator] Purchase ignored because a duplicator charge is already active.");
            return;
        }

        Debug.Log($"[Duplicator] Armed for {targetTier}; free copies={duplicateCopies}.");
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { (int)targetTier, duplicateCopies };
    }
}
