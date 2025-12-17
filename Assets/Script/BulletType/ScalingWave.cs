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

    private HashSet<Enemy> hitEnemies = new HashSet<Enemy>();


    private void Awake()
    {
        // Обязательно нужен IsTrigger = true
        var col = GetComponent<Collider>();
        col.isTrigger = true;

        // Начальный масштаб
        transform.localScale = startScale;
    }

    private void Start()
    {
        if (playOnStart)
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

        if (t >= 1f)
        {
            isPlaying = false;

            if (destroyAfterFinish)
                Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!applyDamage)
            return;

        Enemy enemy = other.GetComponent<Enemy>();
        if (enemy == null)
            return;

        if (hitEnemies.Contains(enemy))
            return;

        hitEnemies.Add(enemy);
        enemy.TakeDamage(damage);
    }
}
