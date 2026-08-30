using UnityEngine;

/// <summary>
/// Моментальная атака без времени полёта. Объект projectile используется только как
/// переиспользуемый контроллер: наносит урон в InitAttack, запускает отдельный hit-VFX
/// и сразу возвращается в пул.
/// </summary>
[RequireComponent(typeof(PooledObject))]
public sealed class InstantHitBullet : MonoBehaviour, IAttackBehaviour
{
    private PooledObject pooledObject;
    private OnHitVFX hitVfx;

    private void Awake()
    {
        pooledObject = GetComponent<PooledObject>();
        hitVfx = GetComponent<OnHitVFX>();
    }

    public void InitAttack(AttackContext context)
    {
        if (pooledObject == null)
            pooledObject = GetComponent<PooledObject>();
        if (hitVfx == null)
            hitVfx = GetComponent<OnHitVFX>();

        Enemy enemy = context.target != null
            ? context.target.GetComponent<Enemy>()
            : null;

        if (enemy == null || enemy.isDead || !enemy.gameObject.activeInHierarchy)
        {
            pooledObject.Release();
            return;
        }

        // Сохраняем позицию до нанесения урона: смертельный удар может сразу вернуть
        // врага в пул и выключить его GameObject.
        Vector3 hitPosition = enemy.GetCenterPosition();
        transform.position = hitPosition;

        if (context.damage > 0f)
        {
            enemy.TakeWeaponDamage(context.damage, context.weapon);
            DamageStatsManager.Instance?.RegisterDamage(context.weapon, context.damage);
        }

        // VFX живёт отдельно от projectile-контроллера, поэтому контроллер можно сразу
        // вернуть в пул, не обрывая проигрывание эффекта.
        hitVfx?.PlayAtPosition(hitPosition, enemy);
        pooledObject.Release();
    }
}
