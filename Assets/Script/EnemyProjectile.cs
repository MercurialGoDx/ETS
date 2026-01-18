using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 10f;

    [Header("Hit")]
    public float damage;
    public bool destroyOnHit = true;

    private Vector3 target;

    public void Init(Vector3 target, float damage)
    {
        this.target = target;
        this.damage = damage;
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

        if (destroyOnHit)
            Destroy(gameObject);
    }
}
