using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Utility/Enemies Per Wave Percent")]
public class EnemiesPerWavePercentUpgrade : UpgradeBaseSO
{
    [Range(0f, 300f)]
    public float valuePercent = 10f;

    public override void Apply(UpgradeContextSO context)
    {
        if (context?.runtime?.enemySpawner == null)
        {
            Debug.LogWarning($"{name}: EnemySpawner is not bound in UpgradeContextSO.runtime.");
            return;
        }

        context.runtime.enemySpawner.AddEnemiesPerWavePercent(valuePercent / 100f);
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[] { valuePercent };
    }
}
