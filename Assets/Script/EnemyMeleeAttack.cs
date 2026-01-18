using UnityEngine;

public class EnemyMeleeAttack : MonoBehaviour, IEnemyAttack
{
    public void Attack(Enemy enemy, Transform target)
    {
        PlayerHealth playerHealth = target.GetComponent<PlayerHealth>();
        if (playerHealth == null) return;

        playerHealth.TakeDamage(enemy.damageToPlayer);

        // Ўипы
        if (playerHealth.SpikesDamage > 0f)
        {
            enemy.TakeDamage(playerHealth.SpikesDamage, true);
        }
    }
}
