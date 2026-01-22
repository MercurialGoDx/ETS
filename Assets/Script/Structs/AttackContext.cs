using UnityEngine;

public struct AttackContext
{
    public Transform firePoint;
    public Transform target;
    public float damage;
    public float projectileSpeed;

    public TowerAttack ownerTower;
    public float weaponFireRate;

    public float forwardOffset;
    public float heightOffset;
    public Transform owner; // башня
}