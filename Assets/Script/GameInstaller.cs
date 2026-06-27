using System.Resources;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class GameInstaller : MonoBehaviour
{
    [SerializeField] private UpgradeContextSO upgradeContext;

    private void Awake()
    {
        upgradeContext.playerHealth = FindFirstObjectByType<PlayerHealth>();
        upgradeContext.playerShield = FindFirstObjectByType<PlayerShield>();
        upgradeContext.towerAttack = FindFirstObjectByType<TowerAttack>();
        upgradeContext.goldManager = FindFirstObjectByType<GoldManager>();
        upgradeContext.regenAuraDamage = FindFirstObjectByType<RegenAuraDamage>();
        upgradeContext.runtime = new UpgradesRuntimeData();
        upgradeContext.runtime.enemySpawner = FindFirstObjectByType<EnemySpawner>();
    }
}
