using System.Collections;
using UnityEngine;

/// <summary>
/// Positions a particle-only status indicator relative to the parent enemy.
/// The script is visual-only and does not affect gameplay, colliders or time.
/// </summary>
[DisallowMultipleComponent]
public sealed class StunStarsVisual : MonoBehaviour
{
    private enum AnchorMode
    {
        AboveHead = 0,
        BodyCenter = 1
    }

    [Header("Target")]
    [Tooltip("Optional explicit target. When empty, the parent transform is used.")]
    [SerializeField] private Transform targetOverride;

    [Header("Auto placement")]
    [SerializeField] private Transform visualRoot;
    [Tooltip("Above Head используется для оглушения. Body Center размещает эффект в середине тела цели.")]
    [SerializeField] private AnchorMode anchorMode = AnchorMode.AboveHead;
    [Min(0f)]
    [SerializeField] private float heightOffset = 0.22f;
    [Tooltip("Дополнительное вертикальное смещение от центра тела в режиме Body Center.")]
    [SerializeField] private float bodyCenterOffset;
    [Min(0.01f)]
    [SerializeField] private float widthMultiplier = 1.05f;
    [Min(0.01f)]
    [SerializeField] private float minimumScale = 0.7f;
    [Min(0.01f)]
    [SerializeField] private float maximumScale = 2.5f;
    [Min(0.01f)]
    [Tooltip("Дополнительный множитель размера только для босса. Значение 1 сохраняет обычный размер.")]
    [SerializeField] private float bossScaleMultiplier = 1f;
    [SerializeField] private bool fitOnEnable = true;

    private Coroutine delayedFitRoutine;
    private Coroutine releaseRoutine;

    private void Reset()
    {
        FindVisualRoot();
    }

    private void Awake()
    {
        FindVisualRoot();
    }

    private void OnEnable()
    {
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
        if (isActiveAndEnabled && fitOnEnable)
            ScheduleFit();
    }

    [ContextMenu("Fit To Parent")]
    public void FitToTarget()
    {
        FindVisualRoot();

        Transform target = targetOverride != null ? targetOverride : transform.parent;
        if (target == null || visualRoot == null || !TryGetLocalBounds(target, out Bounds bounds))
            return;

        float targetWidth = Mathf.Max(bounds.size.x, bounds.size.z);
        float scale = Mathf.Clamp(targetWidth * widthMultiplier, minimumScale, maximumScale);

        if (target.TryGetComponent(out EnemyStatusEffectController statusEffects) && statusEffects.IsBoss)
            scale *= Mathf.Max(0.01f, bossScaleMultiplier);

        float anchorY = anchorMode == AnchorMode.BodyCenter
            ? bounds.center.y + bodyCenterOffset * scale
            : bounds.max.y + heightOffset * scale;

        visualRoot.localPosition = new Vector3(bounds.center.x, anchorY, bounds.center.z);
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one * scale;
    }

    /// <summary>
    /// Optional hook for a future status-effect system.
    /// </summary>
    public void Release(float duration = 0.14f)
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
        Vector3 startScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (visualRoot != null)
                visualRoot.localScale = startScale * (1f - t * t);

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
            Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
            foreach (Collider targetCollider in colliders)
            {
                if (targetCollider == null || !targetCollider.enabled || targetCollider.isTrigger || targetCollider.transform.IsChildOf(transform))
                    continue;

                EncapsulateWorldBounds(targetCollider.bounds, ref hasBounds, ref minimum, ref maximum);
            }
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
                    Vector3 localCorner = transform.InverseTransformPoint(
                        center + Vector3.Scale(extents, new Vector3(x, y, z)));

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

    private void FindVisualRoot()
    {
        if (visualRoot == null)
            visualRoot = transform.Find("VisualRoot");
    }
}
