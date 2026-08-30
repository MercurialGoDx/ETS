using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy))]
public sealed class EnemyStatusEffectController : MonoBehaviour
{
    private sealed class BurningBatch
    {
        public WeaponDefinition sourceWeapon;
        public float totalDamage;
        public float duration;
        public float endTime;
        public float lastDamageTime;
    }

    [Header("Иммунитет")]
    [Tooltip("У босса Stun и Freeze полностью игнорируются. Weakness и Burning продолжают работать.")]
    [SerializeField] private bool isBoss;

    [Header("Стандартные визуальные префабы")]
    [SerializeField] private GameObject defaultStunVisualPrefab;
    [SerializeField] private GameObject defaultFreezeVisualPrefab;
    [SerializeField] private GameObject defaultWeaknessVisualPrefab;
    [SerializeField] private GameObject defaultBurningVisualPrefab;

    [Header("Burning")]
    [Tooltip("Как часто Burning наносит часть накопленного урона.")]
    [Min(0.05f)]
    [SerializeField] private float burningTickInterval = 0.5f;

    private Enemy enemy;

    private bool isStunned;
    private float stunEndTime;
    private GameObject stunVisual;

    private bool isFrozen;
    private float freezeEndTime;
    private GameObject freezeVisual;

    private float weaknessPercent;
    private float weaknessEndTime;
    private GameObject weaknessVisual;

    private readonly List<BurningBatch> burningBatches = new();
    private GameObject burningVisual;

    public bool IsBoss => isBoss;
    public bool IsStunned => isStunned;
    public bool IsFrozen => isFrozen;
    public bool BlocksActions => isStunned || isFrozen;
    public float OutgoingDamageMultiplier => 1f - Mathf.Clamp01(weaknessPercent);
    public int ActiveBurningBatchCount => burningBatches.Count;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
    }

    private void OnEnable()
    {
        if (enemy == null)
            enemy = GetComponent<Enemy>();

        ClearAll(false);
    }

    private void OnDisable()
    {
        ClearAll(false);
    }

    private void Update()
    {
        if (enemy == null || enemy.isDead)
            return;

        if (GameStateManager.Instance != null &&
            GameStateManager.Instance.CurrentState != GameState.Playing)
        {
            return;
        }

        float now = Time.time;

        if (isStunned && now >= stunEndTime)
            EndStun(true);

        if (isFrozen && now >= freezeEndTime)
            EndFreeze(true);

        if (weaknessPercent > 0f && now >= weaknessEndTime)
            EndWeakness(true);

        UpdateBurning(now);
    }

    public bool ApplyStun(float duration, GameObject visualPrefabOverride = null)
    {
        if (isBoss || enemy == null || enemy.isDead || duration <= 0f)
            return false;

        float requestedEndTime = Time.time + duration;
        bool wasAlreadyStunned = isStunned;

        isStunned = true;
        stunEndTime = Mathf.Max(stunEndTime, requestedEndTime);
        enemy.CancelKnockbackForControlEffect();

        if (!wasAlreadyStunned && !isFrozen)
            enemy.PlayStunIdlePose();

        EnsureVisual(ref stunVisual, visualPrefabOverride != null
            ? visualPrefabOverride
            : defaultStunVisualPrefab);
        return true;
    }

    public bool ApplyFreeze(
        float duration,
        GameObject visualPrefabOverride = null,
        float visualScaleMultiplier = 1f)
    {
        if (isBoss || enemy == null || enemy.isDead || duration <= 0f)
            return false;

        float requestedEndTime = Time.time + duration;
        bool wasAlreadyFrozen = isFrozen;

        isFrozen = true;
        freezeEndTime = Mathf.Max(freezeEndTime, requestedEndTime);
        enemy.CancelKnockbackForControlEffect();

        if (!wasAlreadyFrozen)
            enemy.PauseAnimationForFreeze();

        bool visualWasCreated = EnsureVisual(ref freezeVisual, visualPrefabOverride != null
            ? visualPrefabOverride
            : defaultFreezeVisualPrefab);

        // Размер задаётся только при создании. Повторный Freeze обновляет лишь
        // длительность и никогда не масштабирует уже существующую глыбу.
        if (visualWasCreated && freezeVisual.TryGetComponent(out IcePrisonVisual icePrison))
            icePrison.SetScaleMultiplier(visualScaleMultiplier);

        return true;
    }

    public void ApplyWeakness(
        float damageReductionPercent,
        float duration,
        GameObject visualPrefabOverride = null)
    {
        if (enemy == null || enemy.isDead || duration <= 0f || damageReductionPercent <= 0f)
            return;

        weaknessPercent = Mathf.Max(
            weaknessPercent,
            Mathf.Clamp(damageReductionPercent / 100f, 0f, 1f));
        weaknessEndTime = Mathf.Max(weaknessEndTime, Time.time + duration);

        EnsureVisual(ref weaknessVisual, visualPrefabOverride != null
            ? visualPrefabOverride
            : defaultWeaknessVisualPrefab);
    }

    public void AddBurning(
        float directDamage,
        int stacks,
        float damagePercentPerStack,
        float duration,
        WeaponDefinition sourceWeapon,
        GameObject visualPrefabOverride = null)
    {
        if (enemy == null || enemy.isDead || directDamage <= 0f || stacks <= 0 ||
            damagePercentPerStack <= 0f || duration <= 0f || sourceWeapon == null)
        {
            return;
        }

        float now = Time.time;
        burningBatches.Add(new BurningBatch
        {
            sourceWeapon = sourceWeapon,
            totalDamage = directDamage * (damagePercentPerStack / 100f) * stacks,
            duration = duration,
            endTime = now + duration,
            lastDamageTime = now
        });

        EnsureVisual(ref burningVisual, visualPrefabOverride != null
            ? visualPrefabOverride
            : defaultBurningVisualPrefab);
    }

    public void ClearAll(bool animateVisualRemoval)
    {
        bool hadControlEffect = isStunned || isFrozen;

        isStunned = false;
        stunEndTime = 0f;
        isFrozen = false;
        freezeEndTime = 0f;
        weaknessPercent = 0f;
        weaknessEndTime = 0f;
        burningBatches.Clear();

        RemoveVisual(ref stunVisual, animateVisualRemoval);
        RemoveVisual(ref freezeVisual, animateVisualRemoval);
        RemoveVisual(ref weaknessVisual, animateVisualRemoval);
        RemoveVisual(ref burningVisual, animateVisualRemoval);

        if (hadControlEffect && enemy != null)
            enemy.ResumeAfterControlEffect();
    }

    private void EndStun(bool animateVisualRemoval)
    {
        isStunned = false;
        stunEndTime = 0f;
        RemoveVisual(ref stunVisual, animateVisualRemoval);

        if (!isFrozen)
            enemy.ResumeAfterControlEffect();
    }

    private void EndFreeze(bool animateVisualRemoval)
    {
        isFrozen = false;
        freezeEndTime = 0f;
        RemoveVisual(ref freezeVisual, animateVisualRemoval);

        if (isStunned)
            enemy.PlayStunIdlePose();
        else
            enemy.ResumeAfterControlEffect();
    }

    private void EndWeakness(bool animateVisualRemoval)
    {
        weaknessPercent = 0f;
        weaknessEndTime = 0f;
        RemoveVisual(ref weaknessVisual, animateVisualRemoval);
    }

    private void UpdateBurning(float now)
    {
        if (burningBatches.Count == 0)
            return;

        for (int i = burningBatches.Count - 1; i >= 0; i--)
        {
            if (enemy.isDead)
                break;

            BurningBatch batch = burningBatches[i];
            float damageUntil = Mathf.Min(now, batch.endTime);
            float elapsedSinceDamage = damageUntil - batch.lastDamageTime;
            bool isFinalTick = now >= batch.endTime;

            if (elapsedSinceDamage >= burningTickInterval || isFinalTick)
            {
                float damage = batch.totalDamage * (elapsedSinceDamage / batch.duration);
                batch.lastDamageTime = damageUntil;

                if (damage > 0f)
                {
                    enemy.TakeStatusDamage(damage, batch.sourceWeapon);
                    DamageStatsManager.Instance?.RegisterDamage(batch.sourceWeapon, damage);

                    // TakeStatusDamage может убить врага и очистить список через Die().
                    if (enemy.isDead)
                        return;
                }
            }

            if (isFinalTick)
                burningBatches.RemoveAt(i);
        }

        if (burningBatches.Count == 0)
            RemoveVisual(ref burningVisual, true);
    }

    private bool EnsureVisual(ref GameObject activeVisual, GameObject prefab)
    {
        if (activeVisual != null || prefab == null)
            return false;

        activeVisual = Instantiate(prefab, transform);
        activeVisual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        return true;
    }

    private static void RemoveVisual(ref GameObject activeVisual, bool animate)
    {
        if (activeVisual == null)
            return;

        GameObject visual = activeVisual;
        activeVisual = null;

        // Исчезающий VFX больше не является частью иерархии врага. Это не даёт
        // следующему визуальному эффекту случайно учитывать его размеры.
        if (animate)
            visual.transform.SetParent(null, true);

        if (animate && visual.TryGetComponent(out StunStarsVisual stunStars))
            stunStars.Release();
        else if (animate && visual.TryGetComponent(out IcePrisonVisual icePrison))
            icePrison.Release();
        else
            Destroy(visual);
    }
}
