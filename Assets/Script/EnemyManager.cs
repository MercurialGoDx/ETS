using System.Collections.Generic;
using UnityEngine;

public class EnemyManager : MonoBehaviour
{
    public static EnemyManager Instance;

    private HashSet<Enemy> enemies = new HashSet<Enemy>();

    private void Awake() => Instance = this;

    public void RegisterEnemy(Enemy e) => enemies.Add(e);
    public void UnregisterEnemy(Enemy e) => enemies.Remove(e);

    public List<Enemy> GetEnemiesInRange(Vector3 position, float range)
    {
        List<Enemy> result = new List<Enemy>();
        foreach (var e in enemies)
        {
            if (e == null || e.isDead) continue;
            if ((e.transform.position - position).sqrMagnitude <= range * range)
                result.Add(e);
        }
        return result;
    }
}
