using System;
using System.Collections.Generic;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    public static EnemyManager Instance;

    private HashSet<Enemy> enemies = new HashSet<Enemy>();

    [Header("Общий рост урона врагов после атак")]
    [Tooltip("Первые N атак каждого врага проходят без усиления. Рост начинается с атаки N+1.")]
    [Min(0)]
    public int damageGrowthStartAfterAttacks = 3;

    [Tooltip("Процент увеличения урона от его текущего значения на каждой атаке после порога. 5 = +5%.")]
    [Min(0f)]
    public float damageGrowthPercentPerAttack = 0f;

    public int DamageGrowthStartAfterAttacks => Mathf.Max(0, damageGrowthStartAfterAttacks);
    public float DamageGrowthPercentPerAttack => Mathf.Max(0f, damageGrowthPercentPerAttack);

    public event Action<Enemy> EnemyRegistered;
    public event Action<Enemy> EnemyUnregistered;

    private void Awake()
    {
        Instance = this;

        // Значения из BalanceConfig имеют приоритет над инспекторными.
        // Если импорт ещё не выполнялся, BalanceService.Config == null и
        // остаются локальные значения компонента как резервные.
        var cfg = BalanceService.Config;
        if (cfg != null)
        {
            damageGrowthStartAfterAttacks = Mathf.Max(0, cfg.waves.damageGrowthStartAfterAttacks);
            damageGrowthPercentPerAttack = Mathf.Max(0f, cfg.waves.damageGrowthPercentPerAttack);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void RegisterEnemy(Enemy e)
    {
        if (e != null && enemies.Add(e))
            EnemyRegistered?.Invoke(e);
    }

    public void UnregisterEnemy(Enemy e)
    {
        if (e != null && enemies.Remove(e))
            EnemyUnregistered?.Invoke(e);
    }

    public void GetActiveEnemies(List<Enemy> results)
    {
        results.Clear();

        foreach (Enemy enemy in enemies)
        {
            if (enemy != null && enemy.isActiveAndEnabled && !enemy.isDead)
                results.Add(enemy);
        }
    }

    public List<Enemy> GetEnemiesInRange(Vector3 position, float range)
    {
        List<Enemy> result = new List<Enemy>();
        GetEnemiesInRange(position, range, result);
        return result;
    }

    /// <summary>
    /// Без аллокаций: очищает и заполняет переданный буфер. Подходит для синхронных вызовов
    /// (внутри одного кадра), где буфер можно переиспользовать между вызовами.
    /// </summary>
    public void GetEnemiesInRange(Vector3 position, float range, List<Enemy> results)
    {
        results.Clear();
        float sqrRange = range * range;
        foreach (var e in enemies)
        {
            if (e == null || e.isDead) continue;
            if ((e.transform.position - position).sqrMagnitude <= sqrRange)
                results.Add(e);
        }
    }
}
