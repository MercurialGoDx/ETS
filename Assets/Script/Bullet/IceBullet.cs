using UnityEngine;

public class IceBullet : Bullet
{
    [Header("Эффект замедления")]
    [Range(0f, 1f)]
    public float slowMultiplier = 0.5f;   // 0.5 = в 2 раза медленнее
    public float slowDuration = 2f;       // длительность замедления в секундах

    protected override void OnEnemyHit(Enemy enemy)
    {
        // сначала стандартная логика пули (урон, уничтожение и т.д.)
        base.OnEnemyHit(enemy);

        // потом — наш эффект замедления
        if (enemy != null)
        {
            enemy.ApplySlow(slowMultiplier, slowDuration);
        }
    }
}
