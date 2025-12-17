using UnityEngine;
using System.Collections.Generic;


[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class WaveBullet : MonoBehaviour
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

    private void Awake()
    {
        // Базовые настройки физики для триггера
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;

        Rigidbody rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;
        if (healPlayerOnHit)
{
    GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
    if (playerObj != null)
        playerHealth = playerObj.GetComponent<PlayerHealth>();
}
    }

    /// <summary>
    /// tower      — трансформ башни
    /// target     — враг, в сторону которого летим
    /// forwardOff — насколько вынести вперёд от башни (по XZ)
    /// heightOff  — дополнительное смещение по высоте относительно врага
    /// </summary>
    public void Init(Transform tower, Transform target, float forwardOff, float heightOff)
    {
        ownerTransform = tower;
        if (tower == null)
            tower = transform;

        Vector3 towerPos = tower.position;

        // Базовая точка для высоты — по врагу (если есть), иначе по башне
        Vector3 basePos = target != null ? target.position : tower.position;
        float planeY = basePos.y + heightOff;

        // Выравниваем башню и цель по одной высоте для расчёта направления
        towerPos.y = planeY;
        Vector3 targetPos;

        if (target != null)
        {
            targetPos = target.position;
            targetPos.y = planeY;
        }
        else
        {
            targetPos = towerPos + tower.forward; // какой-то вперёд, если цели нет
        }

        Vector3 dir = (targetPos - towerPos);
        dir.y = 0f;                     // движение только в плоскости XZ

        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector3.forward;      // защита от нулевого вектора

        moveDir = dir.normalized;

        // Позиция спавна: от башни вперёд по XZ, на высоте planeY
        Vector3 spawnPos = towerPos + moveDir * forwardOff;
        spawnPos.y = planeY;

        transform.position = spawnPos;
        fixedY = planeY;

        // Повернуть визуал по направлению движения
        transform.rotation = Quaternion.LookRotation(moveDir, Vector3.up);

        timer = lifeTime;
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
            Destroy(gameObject);
        }
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

    // нока-бек, если включён
    if (applyKnockback && ownerTransform != null)
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
