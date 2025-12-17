using UnityEngine;

public class RandomSpawnPortal : MonoBehaviour
{
    [Header("Позиция")]
    public float spawnRadius = 20f;

    [Header("Параметры урона")]
    public float damage = 5f;
    public float interval = 1f;        // базовый интервал между тиками
    public float damageRadius = 3f;
    public float lifeTime = 5f;

    [Header("VFX")]
    public GameObject impactVfx;

    private float timer = 0f;
    private float aliveTimer = 0f;

    private Transform tower;

    private void Start()
    {
        GameObject towerObj = GameObject.FindGameObjectWithTag("Player");
        if (towerObj != null)
        {
            tower = towerObj.transform;

            // 👉 рандомная позиция вокруг башни
            Vector2 circle = Random.insideUnitCircle * spawnRadius;
            Vector3 pos = new Vector3(
                tower.position.x + circle.x,
                tower.position.y,                 // подстрой при необходимости
                tower.position.z + circle.y
            );
            transform.position = pos;
        }
        else
        {
            Debug.LogError("RandomSpawnPortal: Tower with tag 'Tower' not found!");
        }
    }

    private void Update()
    {
        timer += Time.deltaTime;
        aliveTimer += Time.deltaTime;

        if (timer >= interval)
        {
            timer = 0f;
            DoDamage();
        }

        if (aliveTimer >= lifeTime)
            Destroy(gameObject);
    }

    private void DoDamage()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, damageRadius);

        foreach (Collider col in hits)
        {
            Enemy enemy = col.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage);
            }
        }

        if (impactVfx != null)
            Instantiate(impactVfx, transform.position, Quaternion.identity);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, damageRadius);
    }
}
