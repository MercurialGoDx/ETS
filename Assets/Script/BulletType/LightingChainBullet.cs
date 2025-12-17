using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightningChainBullet : MonoBehaviour
{
    [Header("Chain Lightning Settings")]
    [SerializeField] private int maxChains = 5;
    [SerializeField] private float chainRadius = 6f;
    [SerializeField] private float tickDelay = 0.3f;
    [SerializeField] private float damageMultiplierPerJump = 0.8f;

    [Header("Flicker")]
    [SerializeField] private float flickerStrength = 0.15f;
    [SerializeField] private float flickerSpeed = 25f;

    [Header("Lines (Outer = glow, Inner = core)")]
    [SerializeField] private LineRenderer outerLine;
    [SerializeField] private LineRenderer innerLine;

    [Header("Visual")]
    [SerializeField] private float widthMultiplier = 1.4f;
    [SerializeField] private int segments = 8;
    [SerializeField] private float jitterAmplitude = 0.2f;

    [Header("Impact VFX")]
    [SerializeField] private GameObject hitVfxPrefab;
    [SerializeField] private float hitVfxYOffset = 1f;

    [Header("Target Filtering")]
    [SerializeField] private LayerMask enemyMask = ~0;

    private float baseDamage;
    private HashSet<Enemy> hitEnemies = new HashSet<Enemy>();

    public void Init(Transform startPoint, Enemy firstTarget, float damage)
    {
        baseDamage = damage;

        // Если вдруг не назначили outerLine — попробуем взять на корне (но у тебя линии на детях, так что лучше назначать вручную)
        if (outerLine == null) outerLine = GetComponent<LineRenderer>();

        ApplyFlicker(); // зададим стартовую ширину

        StopAllCoroutines();
        StartCoroutine(ChainRoutine(startPoint, firstTarget));
    }

    private IEnumerator ChainRoutine(Transform start, Enemy target)
    {
        Transform currentFrom = start;
        Enemy currentTarget = target;
        float currentDamage = baseDamage;

        for (int i = 0; i < maxChains; i++)
        {
            if (!IsValidEnemy(currentTarget))
                break;

            hitEnemies.Add(currentTarget);

            DrawLightning(currentFrom.position, currentTarget.transform.position);

            yield return new WaitForSeconds(tickDelay);

            if (IsValidEnemy(currentTarget))
            {
                SpawnImpact(currentTarget.transform.position);
                currentTarget.TakeDamage(currentDamage);
            }

            currentDamage *= damageMultiplierPerJump;

            Enemy next = FindNextEnemy(currentTarget.transform.position);

            currentFrom = currentTarget.transform;
            currentTarget = next;
        }

        Destroy(gameObject);
    }

    private void SpawnImpact(Vector3 pos)
    {
        if (hitVfxPrefab == null) return;
        Instantiate(hitVfxPrefab, pos + Vector3.up * hitVfxYOffset, Quaternion.identity);
    }

    private Enemy FindNextEnemy(Vector3 from)
    {
        Collider[] hits = Physics.OverlapSphere(from, chainRadius, enemyMask);

        Enemy closest = null;
        float minDist = float.MaxValue;

        foreach (var h in hits)
        {
            if (!h.TryGetComponent(out Enemy enemy)) continue;
            if (!IsValidEnemy(enemy)) continue;
            if (hitEnemies.Contains(enemy)) continue;

            float dist = Vector3.Distance(from, enemy.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = enemy;
            }
        }

        return closest;
    }

    private bool IsValidEnemy(Enemy enemy)
    {
        if (enemy == null) return false;
        if (!enemy.gameObject.activeInHierarchy) return false;
        if (enemy.CurrentHealth <= 0f) return false;
        return true;
    }

    private void DrawLightning(Vector3 from, Vector3 to)
    {
        DrawLine(outerLine, from, to);
        DrawLine(innerLine, from, to);

        // flicker применяем один раз на отрисовку, а не на каждый сегмент
        ApplyFlicker();
    }

    private void DrawLine(LineRenderer lr, Vector3 from, Vector3 to)
    {
        if (lr == null) return;

        lr.positionCount = segments;

        for (int i = 0; i < segments; i++)
        {
            float t = i / (segments - 1f);
            Vector3 pos = Vector3.Lerp(from, to, t);

            if (i != 0 && i != segments - 1)
                pos += Random.insideUnitSphere * jitterAmplitude;

            lr.SetPosition(i, pos);
        }
    }

    private void ApplyFlicker()
    {
        float flicker = 1f + Mathf.Sin(Time.time * flickerSpeed) * flickerStrength;

        if (outerLine != null) outerLine.widthMultiplier = widthMultiplier * flicker;
        if (innerLine != null) innerLine.widthMultiplier = widthMultiplier * (flicker * 0.8f);
    }
}
