using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Gold/Gold On Purchase")]
public class GoldOnPurchaseUpgrade : UpgradeBaseSO
{
    [Header("Reward")]
    public int basicGold = 200;
    public int multGold = 5;

    public override void Apply(UpgradeContextSO context)
    {
        if (context == null || context.runtime == null) return;

        // Счётчик именно покупок этого типа апгрейда (как у тебя в примере)
        context.runtime.goldUpgradeCount++;

        int reward = basicGold + multGold * context.runtime.goldUpgradeCount;

        // Предпочтительно через контекст, но оставлю fallback на Instance, чтобы не ломать текущую архитектуру
        if (context.goldManager != null)
            context.goldManager.AddGold(reward);
        else if (GoldManager.Instance != null)
            GoldManager.Instance.AddGold(reward);
        else
            Debug.LogWarning($"{name}: GoldManager not found (context.goldManager and GoldManager.Instance are null).");
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        // можно показать, сколько даёт база и множитель (если у тебя описание поддерживает 2 аргумента)
        return new object[] { basicGold, multGold };
    }
}
