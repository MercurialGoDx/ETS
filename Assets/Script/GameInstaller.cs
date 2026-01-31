using System.Resources;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class GameInstaller : MonoBehaviour
{
    [SerializeField] private UpgradeContextSO upgradeContext;

    private void Awake()
    {
        upgradeContext.playerHealth = FindObjectOfType<PlayerHealth>();
        upgradeContext.playerShield = FindObjectOfType<PlayerShield>();
        upgradeContext.towerAttack = FindObjectOfType<TowerAttack>();
        upgradeContext.goldManager = FindObjectOfType<GoldManager>();
        upgradeContext.regenAuraDamage = FindObjectOfType<RegenAuraDamage>();
        upgradeContext.runtime = new UpgradesRuntimeData();
    }
}
