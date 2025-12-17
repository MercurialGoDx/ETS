using UnityEngine;

public class SpawnBulletOffset : MonoBehaviour
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
    public void Init(Vector3 spawnPos)
    {
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
