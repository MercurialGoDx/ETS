using System;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public sealed class LightningDeathVisual : MonoBehaviour, IAttackBehaviour
{
    [Header("Preview Endpoints")]
    [SerializeField] private Transform previewStart;
    [SerializeField] private Transform previewEnd;
    [SerializeField] private bool animateInEditMode = true;

    [Header("Shape")]
    [SerializeField, Min(3)] private int segments = 15;
    [SerializeField, Min(0f)] private float jitterAmplitude = 0.38f;
    [SerializeField, Min(0f)] private float curveAmplitude = 0.22f;
    [SerializeField, Min(1f)] private float pathRefreshRate = 24f;
    [SerializeField, Min(0f)] private float morphSpeed = 42f;

    [Header("Timing")]
    [SerializeField, Min(0.05f), Tooltip("How long one lightning strike remains visible after dealing damage.")]
    private float strikeDuration = 0.2f;
    [SerializeField, Min(0f)] private float fadeDuration = 0.05f;

    [Header("Kill Scaling")]
    [SerializeField, Min(0f), Tooltip("Base damage gained by every stack of this weapon when its strike kills an enemy.")]
    private float baseDamageGainOnKill = 1f;

    [Header("Layered Bolt")]
    [SerializeField] private Material lineMaterial;
    [SerializeField, Min(0.001f)] private float glowWidth = 0.34f;
    [SerializeField, Min(0.001f)] private float boltWidth = 0.14f;
    [SerializeField, Min(0.001f)] private float coreWidth = 0.045f;
    [SerializeField, ColorUsage(true, true)] private Color glowColor = new Color(0.08f, 1.2f, 4f, 0.2f);
    [SerializeField, ColorUsage(true, true)] private Color boltColor = new Color(0.35f, 1.8f, 5f, 0.9f);
    [SerializeField, ColorUsage(true, true)] private Color coreColor = new Color(5f, 6f, 8f, 1f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private Transform runtimeStart;
    private Transform runtimeEnd;
    private PooledObject pooledObject;
    private LineRenderer glowLine;
    private LineRenderer boltLine;
    private LineRenderer coreLine;
    private MaterialPropertyBlock glowProperties;
    private MaterialPropertyBlock boltProperties;
    private MaterialPropertyBlock coreProperties;
    private Vector3[] currentPoints;
    private Vector3[] targetPoints;
    private System.Random random;
    private float elapsed;
    private float activeDuration;
    private float nextPathRefresh;
    private float randomPhase;
    private bool pathInitialized;
    private bool isPlaying;
    private bool useFixedRuntimeEnd;
    private bool releaseOnComplete;
    private Vector3 fixedRuntimeEnd;
    private double lastEditorTime;

    public void Play(Transform start, Transform end, float duration = -1f)
    {
        runtimeStart = start;
        runtimeEnd = end;
        useFixedRuntimeEnd = false;
        releaseOnComplete = false;
        activeDuration = duration > 0f ? duration : strikeDuration;
        BeginStrike();
    }

    public void InitAttack(AttackContext context)
    {
        Transform start = context.firePoint != null ? context.firePoint : context.owner;
        Enemy enemy = context.target != null
            ? context.target.GetComponent<Enemy>()
            : null;

        if (start == null || enemy == null || enemy.isDead || !enemy.gameObject.activeInHierarchy)
        {
            ReleaseToPool();
            return;
        }

        runtimeStart = start;
        runtimeEnd = null;
        fixedRuntimeEnd = enemy.GetCenterPosition();
        useFixedRuntimeEnd = true;
        releaseOnComplete = true;
        activeDuration = strikeDuration;
        BeginStrike();

        enemy.TakeDamage(context.damage);
        DamageStatsManager.Instance?.RegisterDamage(context.weapon, context.damage);

        if (enemy.isDead && context.ownerTower != null)
            context.ownerTower.AddWeaponBaseDamage(context.weapon, baseDamageGainOnKill);

        if (context.ownerTower != null && context.ownerTower.DebugDamageEnabled)
        {
            string weaponName = context.weapon != null ? context.weapon.name : name;
            Debug.Log($"[DamageDebug] {weaponName}: lightning hit damage={context.damage:0.###}.");
        }
    }

    public void Stop()
    {
        isPlaying = false;
        SetLinesVisible(false);
    }

    private void OnEnable()
    {
        if (pooledObject == null)
            pooledObject = GetComponent<PooledObject>();

        EnsureLines();
        releaseOnComplete = false;
        activeDuration = strikeDuration;

        if (Application.isPlaying)
        {
            isPlaying = false;
            pathInitialized = false;
            SetLinesVisible(false);
            return;
        }

        BeginStrike();
    }

    private void OnDisable()
    {
        SetLinesVisible(false);
        runtimeStart = null;
        runtimeEnd = null;
        useFixedRuntimeEnd = false;
        releaseOnComplete = false;
    }

    private void OnDestroy()
    {
        DestroyLine(ref glowLine);
        DestroyLine(ref boltLine);
        DestroyLine(ref coreLine);
    }

    private void OnValidate()
    {
        segments = Mathf.Max(3, segments);
        pathRefreshRate = Mathf.Max(1f, pathRefreshRate);
        strikeDuration = Mathf.Max(0.05f, strikeDuration);
        fadeDuration = Mathf.Clamp(fadeDuration, 0f, strikeDuration);
        currentPoints = null;
        targetPoints = null;

        if (isActiveAndEnabled)
        {
            EnsureLines();
            ApplyLineStyles();
            pathInitialized = false;
            nextPathRefresh = 0f;
        }
    }

    private void Update()
    {
        if (!Application.isPlaying && !animateInEditMode)
        {
            SetLinesVisible(false);
            return;
        }

        if (!isPlaying)
        {
            if (Application.isPlaying)
                return;

            BeginStrike();
        }

        if (!TryGetEndpoints(out Vector3 start, out Vector3 end))
        {
            if (Application.isPlaying && releaseOnComplete)
                CompleteStrike();
            else
                Stop();

            return;
        }

        float deltaTime = GetDeltaTime();
        elapsed += deltaTime;

        if (Application.isPlaying && elapsed >= activeDuration)
        {
            CompleteStrike();
            return;
        }

        EnsurePointBuffers();

        if (!pathInitialized || elapsed >= nextPathRefresh)
        {
            BuildTargetPath(start, end);
            nextPathRefresh = elapsed + 1f / pathRefreshRate;
        }

        MorphPath(start, end, deltaTime);
        ApplyPath();
        ApplyFlicker();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
#endif
    }

    private void BeginStrike()
    {
        EnsureLines();
        random = new System.Random(GetInstanceID() ^ Environment.TickCount);
        randomPhase = (float)random.NextDouble() * 100f;
        elapsed = 0f;
        nextPathRefresh = 0f;
        pathInitialized = false;
        lastEditorTime = 0d;
        isPlaying = true;

        if (!TryGetEndpoints(out Vector3 start, out Vector3 end))
        {
            SetLinesVisible(false);
            return;
        }

        // A pooled LineRenderer keeps its previous positions, so rebuild them before revealing it.
        EnsurePointBuffers();
        BuildTargetPath(start, end);
        ApplyPath();
        ApplyFlicker();
        SetLinesVisible(true);
    }

    private bool TryGetEndpoints(out Vector3 start, out Vector3 end)
    {
        Transform startTransform = runtimeStart != null ? runtimeStart : previewStart;

        if (startTransform == null)
        {
            start = default;
            end = default;
            return false;
        }

        if (Application.isPlaying && !startTransform.gameObject.activeInHierarchy)
        {
            start = default;
            end = default;
            return false;
        }

        start = startTransform.position;

        if (useFixedRuntimeEnd)
        {
            end = fixedRuntimeEnd;
        }
        else
        {
            Transform endTransform = runtimeEnd != null ? runtimeEnd : previewEnd;
            if (endTransform == null ||
                (Application.isPlaying && !endTransform.gameObject.activeInHierarchy))
            {
                end = default;
                return false;
            }

            end = endTransform.position;
        }

        return (end - start).sqrMagnitude > 0.0001f;
    }

    private void CompleteStrike()
    {
        bool shouldRelease = releaseOnComplete;
        Stop();

        if (shouldRelease)
            ReleaseToPool();
    }

    private void ReleaseToPool()
    {
        releaseOnComplete = false;

        if (pooledObject != null)
            pooledObject.Release();
        else if (Application.isPlaying)
            gameObject.SetActive(false);
    }

    private void EnsurePointBuffers()
    {
        if (currentPoints != null && currentPoints.Length == segments)
            return;

        currentPoints = new Vector3[segments];
        targetPoints = new Vector3[segments];
        pathInitialized = false;
    }

    private void BuildTargetPath(Vector3 start, Vector3 end)
    {
        Vector3 direction = end - start;
        float distance = direction.magnitude;
        Vector3 forward = direction / distance;
        Vector3 side = Vector3.Cross(forward, Vector3.up);

        if (side.sqrMagnitude < 0.001f)
            side = Vector3.Cross(forward, Vector3.right);

        side.Normalize();
        Vector3 normal = Vector3.Cross(side, forward).normalized;
        float distanceScale = Mathf.Clamp(distance * 0.08f, 0.35f, 1.4f);
        float curveDirection = NextSignedFloat();

        for (int i = 0; i < segments; i++)
        {
            float t = i / (segments - 1f);
            float envelope = Mathf.Sin(t * Mathf.PI);
            Vector3 point = Vector3.Lerp(start, end, t);

            if (i > 0 && i < segments - 1)
            {
                float sideNoise = NextSignedFloat() * jitterAmplitude * distanceScale;
                float normalNoise = NextSignedFloat() * jitterAmplitude * 0.65f * distanceScale;
                float curve = curveDirection * curveAmplitude * envelope * distanceScale;
                point += side * (sideNoise + curve) * envelope;
                point += normal * normalNoise * envelope;
            }

            targetPoints[i] = point;
        }

        targetPoints[0] = start;
        targetPoints[segments - 1] = end;

        if (pathInitialized)
            return;

        Array.Copy(targetPoints, currentPoints, segments);
        pathInitialized = true;
    }

    private void MorphPath(Vector3 start, Vector3 end, float deltaTime)
    {
        float blend = morphSpeed <= 0f
            ? 1f
            : 1f - Mathf.Exp(-morphSpeed * Mathf.Max(0f, deltaTime));

        for (int i = 1; i < segments - 1; i++)
            currentPoints[i] = Vector3.Lerp(currentPoints[i], targetPoints[i], blend);

        currentPoints[0] = start;
        currentPoints[segments - 1] = end;
    }

    private void ApplyPath()
    {
        SetLinePositions(glowLine);
        SetLinePositions(boltLine);
        SetLinePositions(coreLine);
    }

    private void SetLinePositions(LineRenderer line)
    {
        if (line == null)
            return;

        line.positionCount = segments;
        line.SetPositions(currentPoints);
    }

    private void ApplyFlicker()
    {
        float noise = Mathf.PerlinNoise(randomPhase, elapsed * 28f);
        float flicker = Mathf.Lerp(0.82f, 1.18f, noise);
        float visibility = 1f;

        if (Application.isPlaying && fadeDuration > 0f)
        {
            float fadeStart = Mathf.Max(0f, activeDuration - fadeDuration);
            if (elapsed > fadeStart)
                visibility = 1f - Mathf.InverseLerp(fadeStart, activeDuration, elapsed);
        }

        glowLine.widthMultiplier = glowWidth * flicker * visibility;
        boltLine.widthMultiplier = boltWidth * Mathf.Lerp(0.92f, 1.08f, noise) * visibility;
        coreLine.widthMultiplier = coreWidth * visibility;
    }

    private void EnsureLines()
    {
        if (glowLine == null)
            glowLine = CreateLine("Outer Glow");
        if (boltLine == null)
            boltLine = CreateLine("Main Bolt");
        if (coreLine == null)
            coreLine = CreateLine("Hot Core");

        glowProperties ??= new MaterialPropertyBlock();
        boltProperties ??= new MaterialPropertyBlock();
        coreProperties ??= new MaterialPropertyBlock();
        ApplyLineStyles();
    }

    private LineRenderer CreateLine(string lineName)
    {
        var lineObject = new GameObject(lineName);
        lineObject.transform.SetParent(transform, false);
        lineObject.hideFlags = Application.isPlaying
            ? HideFlags.HideInHierarchy
            : HideFlags.HideAndDontSave;

        var line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = false;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCornerVertices = 3;
        line.numCapVertices = 3;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.lightProbeUsage = LightProbeUsage.Off;
        line.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return line;
    }

    private void ApplyLineStyles()
    {
        ConfigureLine(glowLine, glowWidth, glowColor, glowProperties, -2);
        ConfigureLine(boltLine, boltWidth, boltColor, boltProperties, -1);
        ConfigureLine(coreLine, coreWidth, coreColor, coreProperties, 0);
    }

    private void ConfigureLine(
        LineRenderer line,
        float width,
        Color color,
        MaterialPropertyBlock properties,
        int sortingOrder)
    {
        if (line == null)
            return;

        line.sharedMaterial = lineMaterial;
        line.widthMultiplier = width;
        line.startColor = Color.white;
        line.endColor = Color.white;
        line.sortingOrder = sortingOrder;

        if (properties == null)
            return;

        properties.Clear();
        properties.SetColor(BaseColorId, color);
        properties.SetColor(ColorId, color);
        line.SetPropertyBlock(properties);
    }

    private void SetLinesVisible(bool visible)
    {
        if (glowLine != null)
            glowLine.enabled = visible;
        if (boltLine != null)
            boltLine.enabled = visible;
        if (coreLine != null)
            coreLine.enabled = visible;
    }

    private float GetDeltaTime()
    {
        if (Application.isPlaying)
            return Time.deltaTime;

#if UNITY_EDITOR
        double currentTime = EditorApplication.timeSinceStartup;
        float deltaTime = lastEditorTime <= 0d
            ? 1f / 60f
            : (float)(currentTime - lastEditorTime);
        lastEditorTime = currentTime;
        return Mathf.Clamp(deltaTime, 0f, 0.1f);
#else
        return 0f;
#endif
    }

    private float NextSignedFloat()
    {
        return (float)(random.NextDouble() * 2d - 1d);
    }

    private static void DestroyLine(ref LineRenderer line)
    {
        if (line == null)
            return;

        GameObject lineObject = line.gameObject;
        line = null;

        if (Application.isPlaying)
            Destroy(lineObject);
        else
            DestroyImmediate(lineObject);
    }
}
