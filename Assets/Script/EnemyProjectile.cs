using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 10f;

    [Header("Hit")]
    public float damage;
    public bool destroyOnHit = true;

    private Vector3 target;

    private Enemy enemy;

    public void Init(Vector3 target, float damage, Enemy enemy)
    {
        this.target = target;
        this.damage = damage;
        this.enemy = enemy;
    }

    private void Update()
    {
        if (target == null)
        {
            Destroy(gameObject);
            return;
        }

        Move();
    }

    private void Move()
    {
        Vector3 dir = (target - transform.position).normalized;
        transform.position += dir * speed * Time.deltaTime;

        transform.forward = dir;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(damage);
        }

        // Ўипы
        if (playerHealth.SpikesDamage > 0f)
        {
            enemy.TakeDamage(playerHealth.SpikesDamage, true);
        }

        if (destroyOnHit)
            Destroy(gameObject);
    }
}
