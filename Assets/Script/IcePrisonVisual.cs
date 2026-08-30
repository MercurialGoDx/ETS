using System.Collections;
using UnityEngine;

/// <summary>
/// Purely visual ice shell. Attach the prefab as a child of an enemy and it will
/// fit itself around the parent's visible renderers without adding colliders or
/// changing any gameplay state.
/// </summary>
[DisallowMultipleComponent]
public sealed class IcePrisonVisual : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Optional explicit target. When empty, the parent transform is used.")]
    [SerializeField] private Transform targetOverride;

    [Header("Auto fit")]
    [SerializeField] private Transform fitRoot;
    [SerializeField] private Transform animationRoot;
    [SerializeField] private Vector3 sizeMultiplier = new Vector3(1.18f, 1.08f, 1.18f);
    [SerializeField] private Vector3 absolutePadding = new Vector3(0.12f, 0.08f, 0.12f);
    [SerializeField] private float verticalOffset;
    [SerializeField] private bool fitOnEnable = true;

    [Header("Appearance")]
    [Min(0f)]
    [SerializeField] private float appearDuration = 0.2f;
    [Range(0.1f, 1f)]
    [SerializeField] private float appearFromScale = 0.72f;
    [SerializeField] private AnimationCurve appearCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Min(0f)]
    [SerializeField] private float idlePulseAmount;
    [Min(0f)]
    [SerializeField] private float idlePulseSpeed = 1.8f;

    private float appearTime;
    private float effectScaleMultiplier = 1f;
    private bool hasFitted;
    private Coroutine delayedFitRoutine;
    private Coroutine releaseRoutine;

    private void Reset()
    {
        FindVisualRoots();
    }

    private void Awake()
    {
        FindVisualRoots();
    }

    private void OnEnable()
    {
        appearTime = 0f;
        hasFitted = false;

        if (fitOnEnable)
            ScheduleFit();
    }

    private void OnDisable()
    {
        if (delayedFitRoutine != null)
            StopCoroutine(delayedFitRoutine);

        delayedFitRoutine = null;
        releaseRoutine = null;
    }

    private void OnTransformParentChanged()
    {
        if (isActiveAndEnabled && fitOnEnable && !hasFitted)
            ScheduleFit();
    }

    public void SetScaleMultiplier(float multiplier)
    {
        effectScaleMultiplier = Mathf.Max(0.01f, multiplier);

        if (hasFitted)
            FitToTarget();
    }

    private void Update()
    {
        if (animationRoot == null)
            return;

        appearTime += Time.deltaTime;
        float normalizedTime = appearDuration <= 0f ? 1f : Mathf.Clamp01(appearTime / appearDuration);
        float appeared = Mathf.Lerp(appearFromScale, 1f, appearCurve.Evaluate(normalizedTime));
        float pulse = 1f + Mathf.Sin(Time.time * idlePulseSpeed) * idlePulseAmount;
        animationRoot.localScale = Vector3.one * (appeared * pulse);
    }

    [ContextMenu("Fit To Parent")]
    public void FitToTarget()
    {
        FindVisualRoots();

        Transform target = targetOverride != null ? targetOverride : transform.parent;
        if (target == null || fitRoot == null)
            return;

        if (!TryGetLocalBounds(target, out Bounds localBounds))
            return;

        Vector3 paddedSize =
            (Vector3.Scale(localBounds.size, sizeMultiplier) + absolutePadding) *
            effectScaleMultiplier;
        paddedSize.x = Mathf.Max(0.25f, paddedSize.x);
        paddedSize.y = Mathf.Max(0.25f, paddedSize.y);
        paddedSize.z = Mathf.Max(0.25f, paddedSize.z);

        Vector3 center = localBounds.center;
        center.y += verticalOffset;

        fitRoot.localPosition = center;
        fitRoot.localRotation = Quaternion.identity;
        fitRoot.localScale = paddedSize;
        hasFitted = true;
    }

    /// <summary>
    /// Optional hook for a future freeze system. It gives the shell a short
    /// shrink-out animation and then removes the visual object.
    /// </summary>
    public void Release(float duration = 0.18f)
    {
        if (!isActiveAndEnabled || duration <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        if (releaseRoutine != null)
            StopCoroutine(releaseRoutine);

        releaseRoutine = StartCoroutine(ReleaseRoutine(duration));
    }

    private IEnumerator ReleaseRoutine(float duration)
    {
        Vector3 startScale = animationRoot != null ? animationRoot.localScale : Vector3.one;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - t * t;

            if (animationRoot != null)
                animationRoot.localScale = startScale * eased;

            yield return null;
        }

        Destroy(gameObject);
    }

    private void ScheduleFit()
    {
        if (delayedFitRoutine != null)
            StopCoroutine(delayedFitRoutine);

        delayedFitRoutine = StartCoroutine(FitAfterHierarchySettles());
    }

    private IEnumerator FitAfterHierarchySettles()
    {
        yield return null;
        FitToTarget();
        delayedFitRoutine = null;
    }

    private bool TryGetLocalBounds(Transform target, out Bounds localBounds)
    {
        // Размер ледяной глыбы должен зависеть от постоянного боевого коллайдера
        // врага, а не от временных дочерних VFX. Иначе исчезающая предыдущая
        // глыба может попасть в Renderer.bounds новой и размер начнёт расти.
        Collider targetCollider = target.GetComponent<Collider>();
        if (targetCollider != null && targetCollider.enabled && !targetCollider.isTrigger)
        {
            bool colliderHasBounds = false;
            Vector3 colliderMinimum = Vector3.zero;
            Vector3 colliderMaximum = Vector3.zero;
            EncapsulateWorldBounds(
                targetCollider.bounds,
                ref colliderHasBounds,
                ref colliderMinimum,
                ref colliderMaximum);

            localBounds = new Bounds(
                (colliderMinimum + colliderMaximum) * 0.5f,
                colliderMaximum - colliderMinimum);
            return true;
        }

        bool hasBounds = false;
        Vector3 minimum = Vector3.zero;
        Vector3 maximum = Vector3.zero;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer targetRenderer in renderers)
        {
            if (targetRenderer == null || !targetRenderer.enabled || targetRenderer.transform.IsChildOf(transform))
                continue;

            if (targetRenderer is ParticleSystemRenderer || targetRenderer is TrailRenderer || targetRenderer is LineRenderer)
                continue;

            EncapsulateWorldBounds(targetRenderer.bounds, ref hasBounds, ref minimum, ref maximum);
        }

        if (!hasBounds)
        {
            localBounds = default;
            return false;
        }

        localBounds = new Bounds((minimum + maximum) * 0.5f, maximum - minimum);
        return true;
    }

    private void EncapsulateWorldBounds(Bounds worldBounds, ref bool hasBounds, ref Vector3 minimum, ref Vector3 maximum)
    {
        Vector3 center = worldBounds.center;
        Vector3 extents = worldBounds.extents;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 worldCorner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    Vector3 localCorner = transform.InverseTransformPoint(worldCorner);

                    if (!hasBounds)
                    {
                        minimum = localCorner;
                        maximum = localCorner;
                        hasBounds = true;
                    }
                    else
                    {
                        minimum = Vector3.Min(minimum, localCorner);
                        maximum = Vector3.Max(maximum, localCorner);
                    }
                }
            }
        }
    }

    private void FindVisualRoots()
    {
        if (fitRoot == null)
            fitRoot = transform.Find("FitRoot");

        if (animationRoot == null && fitRoot != null)
            animationRoot = fitRoot.Find("AnimationRoot");
    }
}
