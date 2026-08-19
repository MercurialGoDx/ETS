using UnityEngine;

/// <summary>
/// Монета: за килл даёт игроку золото, а базовый урон монеты зависит от текущего
/// золота у игрока живьём — по стаку за каждые goldPerDamageStack золота, не больше
/// maxGoldDamageStacks. Бонус входит в БАЗУ (см. TowerAttack.GetRuntimeBaseDamage),
/// поэтому множители (тип урона/global/adaptive) действуют и на него тоже — так же,
/// как на damagePerProjectile. Бонус не накапливается — если золото потрачено, при
/// следующем выстреле пересчитается заново от текущего баланса.
/// </summary>
public class CoinBullet : Bullet
{
    [Header("Золото за килл")]
    [Tooltip("Сколько золота получает игрок, если эта монета убивает врага.")]
    public int goldPerKill = 1;

    [Header("Урон от текущего золота (динамический, входит в базу)")]
    [Tooltip("Порог золота на один стак урона (напр. 100 = стак за каждые 100 золота на руках).")]
    public int goldPerDamageStack = 100;

    [Tooltip("Прибавка к БАЗОВОМУ урону монеты за один стак — множители действуют и на неё.")]
    public float damagePerStack = 1f;

    [Tooltip("Максимальное количество стаков от золота — дальше бонус не растёт.")]
    public int maxGoldDamageStacks = 5;

    /// <summary>
    /// Читается из TowerAttack.GetRuntimeBaseDamage прямо с префаба (без инстанса) —
    /// тот же приём, что уже используется для Catapult. Чистая функция от текущего
    /// золота и конфига, без побочных эффектов — безопасно вызывать на префабе.
    /// </summary>
    public float GetGoldDamageBonus()
    {
        if (GoldManager.Instance == null)
            return 0f;

        if (goldPerDamageStack <= 0 || damagePerStack <= 0f || maxGoldDamageStacks <= 0)
            return 0f;

        int stacks = Mathf.Min(maxGoldDamageStacks, GoldManager.Instance.currentGold / goldPerDamageStack);
        return stacks * damagePerStack;
    }

    protected override void OnEnemyHit(Enemy enemy, bool killedByThisHit)
    {
        if (enemy == null || !killedByThisHit)
            return;

        if (goldPerKill > 0 && GoldManager.Instance != null)
            GoldManager.Instance.AddGold(goldPerKill, GoldSource.Kill, enemy.transform.position);
    }
}
