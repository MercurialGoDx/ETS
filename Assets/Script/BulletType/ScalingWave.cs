using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class ScalingWave : MonoBehaviour
{
    [Header("Масштабирование")]
    public Vector3 startScale = Vector3.zero;
    public Vector3 endScale = new Vector3(3f, 3f, 3f);
    public float duration = 0.5f;

    [Header("Урон")]
    public bool applyDamage = true;
    public float damage = 5f;

    [Header("Поведение")]
    public bool playOnStart = true;
    public bool destroyAfterFinish = false; // если нужно удалить объект после расширения

    private float timer = 0f;
    private bool isPlaying = false;

    // Каждого врага бьём максимум один раз за одно расширение волны.
    private readonly HashSet<Enemy> hitEnemies = new HashSet<Enemy>();
    private readonly List<Enemy> overlapBuffer = new List<Enemy>();

    private SphereCollider sphere;
    private PooledObject pooledObject;
    private WeaponDefinition sourceWeapon;

    private void Awake()
    {
        // Триггер оставляем (не мешает), но урон считаем по позиции — см. DamageEnemiesInRadius.
        var col = GetComponent<Collider>();
        col.isTrigger = true;
        sphere = col as SphereCollider;

        // PooledObject живёт на корне снаряда — у самой волны его нет, берём из родителя.
        pooledObject = GetComponentInParent<PooledObject>();

        transform.localScale = startScale;
    }

    private void Start()
    {
        if (playOnStart && !isPlaying)
            Play();
    }

    /// <summary>
    /// Задать урон/источник от оружия и запустить волну. Вызывается из <see cref="SpawnBulletOffset"/>,
    /// который получает <see cref="AttackContext"/> на корне снаряда.
    /// </summary>
    public void Configure(float damageValue, WeaponDefinition weapon)
    {
        damage = damageValue;
        sourceWeapon = weapon;
        Play();
    }

    public void Play()
    {
        timer = 0f;
        isPlaying = true;
        transform.localScale = startScale;
        hitEnemies.Clear();
    }

    private void Update()
    {
        if (!isPlaying)
            return;

        timer += Time.deltaTime;

        float t = Mathf.Clamp01(timer / duration);
        transform.localScale = Vector3.Lerp(startScale, endScale, t);

        if (applyDamage)
            DamageEnemiesInRadius();

        if (t >= 1f)
        {
            isPlaying = false;

            if (destroyAfterFinish && pooledObject != null)
                pooledObject.Release();
        }
    }

    // Урон по позиции (без зависимости от Rigidbody/триггеров): растущая зона бьёт
    // каждого врага в текущем радиусе один раз. Это чинит «волна идёт, а урона нет»:
    // раньше урон висел на OnTriggerEnter растущего триггера без Rigidbody — событие не срабатывало.
    private void DamageEnemiesInRadius()
    {
        if (EnemyManager.Instance == null)
            return;

        float worldRadius = CurrentWorldRadius();
        if (worldRadius <= 0f)
            return;

        EnemyManager.Instance.GetEnemiesInRange(transform.position, worldRadius, overlapBuffer);
        for (int i = 0; i < overlapBuffer.Count; i++)
        {
            Enemy enemy = overlapBuffer[i];
            if (enemy == null || hitEnemies.Contains(enemy))
                continue;

            hitEnemies.Add(enemy);
            enemy.TakeDamage(damage);
            DamageStatsManager.Instance?.RegisterDamage(sourceWeapon, damage);
        }
    }

    private float CurrentWorldRadius()
    {
        float baseRadius = sphere != null ? sphere.radius : 0.5f;
        Vector3 s = transform.lossyScale;
        float scale = Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.z));
        return baseRadius * scale;
    }
}
