using UnityEngine;
using System.Collections.Generic;


[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class WaveBullet : MonoBehaviour, IAttackBehaviour
{
    [Header("Характеристики волны")]
    public float speed = 10f;
    public float damage = 5f;
    public float lifeTime = 2f;   // через сколько секунд исчезнет

    [Header("Knockback")]
    public bool applyKnockback = false;      // будет ли эта волна отталкивать
    public float knockbackDistance = 2f;     // на сколько юнитов оттолкнёт
    public float knockbackDuration = 0.15f;  // за сколько секунд 
    private Transform ownerTransform;    // сюда запомним башню

    private Vector3 moveDir;
    private float timer;
    private float fixedY;
    private HashSet<Enemy> hitEnemies = new HashSet<Enemy>();
    [Header("Лечение игрока")]
    [Tooltip("Если включено — при попадании по врагу волна будет лечить игрока.")]
    public bool healPlayerOnHit = false;

    [Tooltip("Сколько здоровья восстановить за КАЖДОГО поражённого врага.")]
    public float healAmountPerEnemy = 5f;

    private PlayerHealth playerHealth;       // закешируем, если нужно лечить

    private WeaponDefinition sourceWeapon;

    private PooledObject pooledObject;

    private void Awake()
    {
        // Базовые настройки физики для триггера
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;

        Rigidbody rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        pooledObject = GetComponent<PooledObject>();
    }

    public void InitAttack(AttackContext context)
    {
        damage = context.damage;
        ownerTransform = context.owner;

        hitEnemies.Clear();
        timer = lifeTime;

        Transform tower = context.firePoint != null
            ? context.firePoint
            : context.owner;

        if (tower == null)
        {
            pooledObject.Release();
            return;
        }

        Transform target = context.target;

        Vector3 towerPos = tower.position;

        // Базовая точка для высоты
        Vector3 basePos = target != null ? target.position : tower.position;
        float planeY = basePos.y + context.heightOffset;

        towerPos.y = planeY;

        Vector3 targetPos;
        if (target != null)
        {
            targetPos = target.position;
            targetPos.y = planeY;
        }
        else
        {
            targetPos = towerPos + tower.forward;
        }

        Vector3 dir = targetPos - towerPos;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f)
            dir = tower.forward;

        moveDir = dir.normalized;

        Vector3 spawnPos = towerPos + moveDir * context.forwardOffset;
        spawnPos.y = planeY;

        transform.position = spawnPos;
        fixedY = planeY;

        transform.rotation = Quaternion.LookRotation(moveDir, Vector3.up);

        sourceWeapon = context.weapon;
    }


    private void Update()
    {
        // Движение строго по XZ
        Vector3 pos = transform.position + moveDir * speed * Time.deltaTime;
        pos.y = fixedY;
        transform.position = pos;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            pooledObject.Release();
        }
    }

    private void FindPlayerHealth()
    {
        if (playerHealth != null)
            return;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            playerHealth = playerObj.GetComponent<PlayerHealth>();
    }

    private void OnTriggerEnter(Collider other)
    {
        Enemy enemy = other.GetComponent<Enemy>();
        if (enemy == null)
            return;

        // если уже били этого врага этой волной — выходим
        if (hitEnemies.Contains(enemy))
            return;

        // помечаем как уже поражённого
        hitEnemies.Add(enemy);

        // наносим урон
        enemy.TakeDamage(damage);
        DamageStatsManager.Instance?.RegisterDamage(sourceWeapon, damage);


        // нока-бек, если включён
        if (applyKnockback)
        {
            enemy.ApplyKnockback(
                ownerTransform.position,
                knockbackDistance,
                knockbackDuration
            );
        }
        if (healPlayerOnHit && healAmountPerEnemy > 0f && playerHealth != null)
        {
            playerHealth.Heal(healAmountPerEnemy);
        }
    } 
}