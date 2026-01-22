using UnityEngine;

public class SpawnBulletOffset : MonoBehaviour, IAttackBehaviour
{
    [Header("Появление")]
    [Tooltip("Насколько опустить пулю по Y относительно точки спавна")]
    public float lowerOffsetY = 0.5f;

    [Header("Жизнь")]
    public float lifeTime = 1.5f;

    private float timer;
    private bool initialized = false;

    /// <summary>
    /// Инициализация из TowerAttack
    /// </summary>
    public void InitAttack(AttackContext context)
    {
        Vector3 spawnPos = context.firePoint != null
            ? context.firePoint.position
            : context.owner.position;

        spawnPos.y -= lowerOffsetY;
        transform.position = spawnPos;

        timer = lifeTime;
        initialized = true;
    }

    private void Update()
    {
        if (!initialized)
            return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            Destroy(gameObject);
        }
    }
}
