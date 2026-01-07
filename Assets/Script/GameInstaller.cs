using System.Resources;
using UnityEngine;

public class GameInstaller : MonoBehaviour
{
    [SerializeField] private UpgradeContextSO upgradeContext;

    private void Awake()
    {
        upgradeContext.playerHealth = FindObjectOfType<PlayerHealth>();
        upgradeContext.playerShield = FindObjectOfType<PlayerShield>();
        upgradeContext.towerAttack = FindObjectOfType<TowerAttack>();
        upgradeContext.goldManager = FindObjectOfType<GoldManager>();
        upgradeContext.runtime = new UpgradesRuntimeData();
    }
}
