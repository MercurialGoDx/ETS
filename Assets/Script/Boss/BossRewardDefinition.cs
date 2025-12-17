using UnityEngine;

public enum BossRewardRarity
{
    Common,
    Rare,
    Legendary
}

[CreateAssetMenu(menuName = "TD/Boss Reward", fileName = "BossReward_")]
public class BossRewardDefinition : ScriptableObject
{
    [Header("What player gets")]
    public UpgradeDefinition upgrade;   // НИЧЕГО не меняем в UpgradeDefinition, просто ссылка

    [Header("UI")]
    public string title;
    [TextArea(2, 6)] public string description;

    [Header("Drop settings")]
    public BossRewardRarity rarity = BossRewardRarity.Common;
    [Min(1)] public int weight = 10;

    [Header("Optional")]
    public bool enabledInPool = true; // чтобы временно выключать из выпадения
}
