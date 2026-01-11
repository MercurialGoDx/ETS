using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Upgrade Context")]
public class UpgradeContextSO : ScriptableObject
{
    [NonSerialized] public PlayerHealth playerHealth;
    [NonSerialized] public PlayerShield playerShield;
    [NonSerialized] public TowerAttack towerAttack;
    [NonSerialized] public RegenAuraDamage regenAuraDamage;
    [NonSerialized] public GoldManager goldManager;
    [NonSerialized] public UpgradesRuntimeData runtime;
}