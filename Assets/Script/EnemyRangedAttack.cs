using MaykerStudio.Demo;
using UnityEngine;

public class EnemyRangedAttack : MonoBehaviour, IEnemyAttack
{
    public EnemyProjectile projectilePrefab;
    public Transform shootPoint;

    public void Attack(Enemy enemy, Transform target)
    {
        if (projectilePrefab == null || shootPoint == null) return;

        EnemyProjectile proj = Instantiate(
            projectilePrefab,
            shootPoint.position,
            Quaternion.identity
        );

        Vector3 finalPoint = target.position + new Vector3(0f, 3f, 0f);

        proj.Init(
            finalPoint,
            enemy.damageToPlayer
        );
    }
}
