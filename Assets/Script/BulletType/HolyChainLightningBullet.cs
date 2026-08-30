using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(PooledObject))]
public sealed class HolyChainLightningBullet : MonoBehaviour, IAttackBehaviour
{
    [Header("Chain")]
    [SerializeField, Min(1)] private int maxTargets = 4;
    [SerializeField, Min(0.1f)] private float chainRadius = 8f;
    [SerializeField, Min(0f)] private float jumpDelay = 0.09f;
    [SerializeField, Min(0f)] private float damageMultiplierPerJump = 1f;

    [Header("Lifetime")]
    [SerializeField, Min(0f)] private float holdDuration = 0.9f;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.22f;

    [Header("Animated Shape")]
    [SerializeField, Min(5)] private int segments = 18;
    [SerializeField, Min(1f)] private float pathRefreshRate = 26f;
    [SerializeField, Min(0f)] private float morphSpeed = 48f;
    [SerializeField, Min(0f)] private float jitterAmplitude = 0.24f;
    [SerializeField, Min(0f)] private float sideArcAmplitude = 0.34f;

    [Header("Size Per Jump")]
    [SerializeField, Min(0.01f), Tooltip("Visual size of every next lightning link. 1 = same size, 0.9 = 10% smaller per jump, 1.1 = 10% larger per jump.")]
    private float sizeMultiplierPerJump = 1f;
    [SerializeField, Range(0f, 1f), Tooltip("How strongly short links reduce the lightning shape. 0 = distance has no effect, 1 = full distance scaling.")]
    private float distanceShapeInfluence = 0f;

    [Header("Holy Bolt Layers")]
    [SerializeField] private Material lineMaterial;
    [SerializeField, Min(0.001f)] private float glowWidth = 0.32f;
    [SerializeField, Min(0.001f)] private float boltWidth = 0.14f;
    [SerializeField, Min(0.001f)] private float coreWidth = 0.045f;
    [SerializeField, Min(0.001f)] private float sideArcWidth = 0.038f;
    [SerializeField, ColorUsage(true, true)] private Color glowColor = new Color(3.8f, 1.15f, 0.05f, 0.24f);
    [SerializeField, ColorUsage(true, true)] private Color boltColor = new Color(5f, 3.25f, 0.18f, 0.92f);
    [SerializeField, ColorUsage(true, true)] private Color coreColor = new Color(7f, 7f, 4.2f, 1f);
    [SerializeField, ColorUsage(true, true)] private Color sideArcColor = new Color(5f, 1.8f, 0.08f, 0.78f);

    [Header("Moving Energy")]
    [SerializeField, Min(0f)] private float pulseSpeed = 2.6f;
    [SerializeField, Min(0.001f)] private float pulseWidth = 0.2f;
    [SerializeField, Min(0f)] private float coronaRadius = 0.38f;
    [SerializeField, Min(8)] private int coronaSegments = 28;
    [SerializeField, Min(0f)] private float coronaSpinSpeed = 145f;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private sealed class LinkVisual
    {
        public LineRenderer glow;
        public LineRenderer bolt;
        public LineRenderer core;
        public LineRenderer sideA;
        public LineRenderer sideB;
        public LineRenderer pulse;
        public Vector3[] current;
        public Vector3[] target;
        public Vector3[] sideCurrentA;
        public Vector3[] sideTargetA;
        public Vector3[] sideCurrentB;
        public Vector3[] sideTargetB;
        public float nextRefresh;
        public int refreshIndex;
        public bool initialized;
    }

    private sealed class CoronaVisual
    {
        public LineRenderer outer;
        public LineRenderer inner;
    }

    private readonly List<Enemy> targets = new List<Enemy>(4);
    private readonly List<Enemy> searchBuffer = new List<Enemy>(32);
    private readonly HashSet<Enemy> selectedTargets = new HashSet<Enemy>();
    private readonly List<LinkVisual> links = new List<LinkVisual>(4);
    private readonly List<CoronaVisual> coronas = new List<CoronaVisual>(4);
    private Vector3[] cachedTargetPositions;

    private PooledObject pooledObject;
    private Transform source;
    private WeaponDefinition sourceWeapon;
    private float baseDamage;
    private float elapsed;
    private float visualAlpha;
    private int revealedLinks;
    private bool isPlaying;
    private bool debugDamage;

    private void Awake()
    {
        pooledObject = GetComponent<PooledObject>();
        EnsureVisuals();
    }

    private void OnEnable()
    {
        ResetPlayback();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        ResetPlayback();
    }

    private void OnValidate()
    {
        maxTargets = Mathf.Max(1, maxTargets);
        segments = Mathf.Max(5, segments);
        coronaSegments = Mathf.Max(8, coronaSegments);
        pathRefreshRate = Mathf.Max(1f, pathRefreshRate);
        fadeDuration = Mathf.Max(0.01f, fadeDuration);
        sizeMultiplierPerJump = Mathf.Max(0.01f, sizeMultiplierPerJump);
    }

    public void InitAttack(AttackContext context)
    {
        StopAllCoroutines();
        ResetPlayback();
        EnsureVisuals();

        source = context.firePoint != null ? context.firePoint : context.owner;
        Enemy firstTarget = context.target != null ? context.target.GetComponent<Enemy>() : null;

        if (source == null || !IsValidEnemy(firstTarget))
        {
            ReleaseToPool();
            return;
        }

        sourceWeapon = context.weapon;
        baseDamage = context.damage;
        debugDamage = context.ownerTower != null && context.ownerTower.DebugDamageEnabled;
        BuildTargetChain(firstTarget);

        if (targets.Count == 0)
        {
            ReleaseToPool();
            return;
        }

        elapsed = 0f;
        visualAlpha = 1f;
        revealedLinks = 0;
        isPlaying = true;
        StartCoroutine(PlayChain());
    }

    private void Update()
    {
        if (!isPlaying)
            return;

        elapsed += Time.deltaTime;
        UpdateCachedPositions();

        for (int i = 0; i < revealedLinks; i++)
        {
            Vector3 start = GetLinkStart(i);
            Vector3 end = cachedTargetPositions[i];
            UpdateLink(links[i], i, start, end);
            UpdateCorona(coronas[i], i, end);
        }
    }

    private IEnumerator PlayChain()
    {
        float currentDamage = baseDamage;

        for (int i = 0; i < targets.Count; i++)
        {
            revealedLinks = i + 1;
            SetLinkVisible(links[i], true);
            SetCoronaVisible(coronas[i], true);
            InitializeLinkImmediately(i);

            Enemy target = targets[i];
            if (IsValidEnemy(target))
            {
                cachedTargetPositions[i] = target.GetCenterPosition();
                target.TakeWeaponDamage(currentDamage, sourceWeapon);
                DamageStatsManager.Instance?.RegisterDamage(sourceWeapon, currentDamage);

                if (debugDamage)
                {
                    string weaponName = sourceWeapon != null ? sourceWeapon.name : name;
                    Debug.Log($"[DamageDebug] {weaponName} holy chain hit #{i + 1}: damage={currentDamage:0.###}.");
                }
            }

            currentDamage *= damageMultiplierPerJump;

            if (jumpDelay > 0f && i < targets.Count - 1)
                yield return new WaitForSeconds(jumpDelay);
        }

        if (holdDuration > 0f)
            yield return new WaitForSeconds(holdDuration);

        float fadeElapsed = 0f;
        while (fadeElapsed < fadeDuration)
        {
            fadeElapsed += Time.deltaTime;
            visualAlpha = 1f - Mathf.Clamp01(fadeElapsed / fadeDuration);
            yield return null;
        }

        isPlaying = false;
        ReleaseToPool();
    }

    private void BuildTargetChain(Enemy firstTarget)
    {
        targets.Clear();
        selectedTargets.Clear();
        targets.Add(firstTarget);
        selectedTargets.Add(firstTarget);

        while (targets.Count < maxTargets)
        {
            Enemy previous = targets[targets.Count - 1];
            Enemy next = FindNearestUnselected(previous.GetCenterPosition());
            if (next == null)
                break;

            targets.Add(next);
            selectedTargets.Add(next);
        }

        if (cachedTargetPositions == null || cachedTargetPositions.Length != maxTargets)
            cachedTargetPositions = new Vector3[maxTargets];

        for (int i = 0; i < targets.Count; i++)
            cachedTargetPositions[i] = targets[i].GetCenterPosition();
    }

    private Enemy FindNearestUnselected(Vector3 origin)
    {
        if (EnemyManager.Instance == null)
            return null;

        EnemyManager.Instance.GetEnemiesInRange(origin, chainRadius, searchBuffer);
        Enemy nearest = null;
        float nearestSqrDistance = float.PositiveInfinity;

        for (int i = 0; i < searchBuffer.Count; i++)
        {
            Enemy candidate = searchBuffer[i];
            if (!IsValidEnemy(candidate) || selectedTargets.Contains(candidate))
                continue;

            float sqrDistance = (candidate.GetCenterPosition() - origin).sqrMagnitude;
            if (sqrDistance >= nearestSqrDistance)
                continue;

            nearestSqrDistance = sqrDistance;
            nearest = candidate;
        }

        return nearest;
    }

    private void UpdateCachedPositions()
    {
        for (int i = 0; i < targets.Count; i++)
        {
            Enemy target = targets[i];
            if (target != null && target.gameObject.activeInHierarchy)
                cachedTargetPositions[i] = target.GetCenterPosition();
        }
    }

    private Vector3 GetLinkStart(int linkIndex)
    {
        if (linkIndex > 0)
            return cachedTargetPositions[linkIndex - 1];

        return source != null && source.gameObject.activeInHierarchy
            ? source.position
            : transform.position;
    }

    private void InitializeLinkImmediately(int linkIndex)
    {
        LinkVisual link = links[linkIndex];
        link.initialized = false;
        link.nextRefresh = 0f;
        UpdateLink(link, linkIndex, GetLinkStart(linkIndex), cachedTargetPositions[linkIndex]);
        UpdateCorona(coronas[linkIndex], linkIndex, cachedTargetPositions[linkIndex]);
    }

    private void UpdateLink(LinkVisual link, int linkIndex, Vector3 start, Vector3 end)
    {
        float jumpVisualScale = Mathf.Pow(sizeMultiplierPerJump, linkIndex);

        if (!link.initialized || elapsed >= link.nextRefresh)
        {
            BuildPath(link.target, start, end, linkIndex, link.refreshIndex, 0f, 1f, jumpVisualScale);
            BuildPath(link.sideTargetA, start, end, linkIndex, link.refreshIndex, 1f, 0.7f, jumpVisualScale);
            BuildPath(link.sideTargetB, start, end, linkIndex, link.refreshIndex, -1f, 0.7f, jumpVisualScale);
            link.refreshIndex++;
            link.nextRefresh = elapsed + 1f / pathRefreshRate;

            if (!link.initialized)
            {
                CopyPoints(link.target, link.current);
                CopyPoints(link.sideTargetA, link.sideCurrentA);
                CopyPoints(link.sideTargetB, link.sideCurrentB);
                link.initialized = true;
            }
        }

        float blend = morphSpeed <= 0f
            ? 1f
            : 1f - Mathf.Exp(-morphSpeed * Mathf.Max(0f, Time.deltaTime));
        MorphPoints(link.current, link.target, start, end, blend);
        MorphPoints(link.sideCurrentA, link.sideTargetA, start, end, blend);
        MorphPoints(link.sideCurrentB, link.sideTargetB, start, end, blend);

        SetPositions(link.glow, link.current);
        SetPositions(link.bolt, link.current);
        SetPositions(link.core, link.current);
        SetPositions(link.sideA, link.sideCurrentA);
        SetPositions(link.sideB, link.sideCurrentB);
        UpdatePulse(link, linkIndex);

        float flicker = Mathf.Lerp(0.82f, 1.2f, Mathf.PerlinNoise(linkIndex * 7.13f, elapsed * 31f));
        ApplyWidthAndAlpha(link.glow, glowWidth * flicker * jumpVisualScale, visualAlpha);
        ApplyWidthAndAlpha(link.bolt, boltWidth * Mathf.Lerp(0.94f, 1.08f, flicker) * jumpVisualScale, visualAlpha);
        ApplyWidthAndAlpha(link.core, coreWidth * jumpVisualScale, visualAlpha);
        ApplyWidthAndAlpha(link.sideA, sideArcWidth * jumpVisualScale, visualAlpha * 0.9f);
        ApplyWidthAndAlpha(link.sideB, sideArcWidth * 0.8f * jumpVisualScale, visualAlpha * 0.72f);
        ApplyWidthAndAlpha(link.pulse, pulseWidth * jumpVisualScale, visualAlpha);
    }

    private void BuildPath(
        Vector3[] points,
        Vector3 start,
        Vector3 end,
        int linkIndex,
        int refreshIndex,
        float curveDirection,
        float jitterScale,
        float jumpVisualScale)
    {
        Vector3 direction = end - start;
        float distance = direction.magnitude;
        Vector3 forward = distance > 0.0001f ? direction / distance : Vector3.forward;
        Vector3 side = Vector3.Cross(forward, Vector3.up);

        if (side.sqrMagnitude < 0.001f)
            side = Vector3.Cross(forward, Vector3.right);

        side.Normalize();
        Vector3 normal = Vector3.Cross(side, forward).normalized;
        float distanceBasedScale = Mathf.Clamp(distance * 0.09f, 0.28f, 1.25f);
        float distanceScale = Mathf.Lerp(1f, distanceBasedScale, distanceShapeInfluence) * jumpVisualScale;

        for (int i = 0; i < points.Length; i++)
        {
            float t = i / (points.Length - 1f);
            float envelope = Mathf.Sin(t * Mathf.PI);
            Vector3 point = Vector3.Lerp(start, end, t);

            if (i > 0 && i < points.Length - 1)
            {
                float seed = linkIndex * 17.73f + refreshIndex * 3.19f + i * 1.37f;
                float sideNoise = (Mathf.PerlinNoise(seed, 0.31f) * 2f - 1f) * jitterAmplitude;
                float normalNoise = (Mathf.PerlinNoise(seed, 8.71f) * 2f - 1f) * jitterAmplitude * 0.62f;
                float wave = Mathf.Sin(t * Mathf.PI * 2f + refreshIndex * 0.7f + linkIndex) * 0.35f;
                point += side * (sideNoise * jitterScale + curveDirection * sideArcAmplitude + wave * sideArcAmplitude) * envelope * distanceScale;
                point += normal * normalNoise * jitterScale * envelope * distanceScale;
            }

            points[i] = point;
        }

        points[0] = start;
        points[points.Length - 1] = end;
    }

    private void UpdatePulse(LinkVisual link, int linkIndex)
    {
        float headT = Mathf.Repeat(elapsed * pulseSpeed + linkIndex * 0.21f, 1f);
        float tailT = Mathf.Max(0f, headT - 0.085f);
        link.pulse.positionCount = 2;
        link.pulse.SetPosition(0, SamplePath(link.current, tailT));
        link.pulse.SetPosition(1, SamplePath(link.current, headT));
    }

    private void UpdateCorona(CoronaVisual corona, int index, Vector3 center)
    {
        float jumpVisualScale = Mathf.Pow(sizeMultiplierPerJump, index);
        float pulse = 1f + Mathf.Sin(elapsed * 10f + index * 1.9f) * 0.12f;
        float radius = coronaRadius * pulse * jumpVisualScale;
        float rotation = elapsed * coronaSpinSpeed + index * 41f;
        BuildCoronaRing(corona.outer, center, radius, rotation, 0.28f);
        BuildCoronaRing(corona.inner, center, radius * 0.68f, -rotation * 1.28f, -0.42f);
        ApplyWidthAndAlpha(corona.outer, sideArcWidth * 1.25f * jumpVisualScale, visualAlpha * 0.9f);
        ApplyWidthAndAlpha(corona.inner, sideArcWidth * 0.72f * jumpVisualScale, visualAlpha);
    }

    private void BuildCoronaRing(LineRenderer line, Vector3 center, float radius, float angle, float tilt)
    {
        line.positionCount = coronaSegments;
        Quaternion spin = Quaternion.AngleAxis(angle, Vector3.up);

        for (int i = 0; i < coronaSegments; i++)
        {
            float t = i / (coronaSegments - 1f);
            float radians = t * Mathf.PI * 2f;
            Vector3 local = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians) * tilt, Mathf.Sin(radians)) * radius;
            line.SetPosition(i, center + spin * local);
        }
    }

    private void EnsureVisuals()
    {
        while (links.Count < maxTargets)
            links.Add(CreateLink(links.Count));

        while (coronas.Count < maxTargets)
            coronas.Add(CreateCorona(coronas.Count));

        for (int i = 0; i < links.Count; i++)
        {
            EnsureBuffers(links[i]);
            ApplyLinkStyles(links[i]);
            SetLinkVisible(links[i], false);
        }

        for (int i = 0; i < coronas.Count; i++)
        {
            ApplyLineStyle(coronas[i].outer, sideArcWidth * 1.25f, boltColor, 4);
            ApplyLineStyle(coronas[i].inner, sideArcWidth * 0.72f, coreColor, 5);
            SetCoronaVisible(coronas[i], false);
        }
    }

    private LinkVisual CreateLink(int index)
    {
        return new LinkVisual
        {
            glow = CreateLine($"Link {index + 1} - Golden Glow", false),
            bolt = CreateLine($"Link {index + 1} - Holy Bolt", false),
            core = CreateLine($"Link {index + 1} - White Core", false),
            sideA = CreateLine($"Link {index + 1} - Arc A", false),
            sideB = CreateLine($"Link {index + 1} - Arc B", false),
            pulse = CreateLine($"Link {index + 1} - Energy Pulse", false)
        };
    }

    private CoronaVisual CreateCorona(int index)
    {
        return new CoronaVisual
        {
            outer = CreateLine($"Target {index + 1} - Corona", true),
            inner = CreateLine($"Target {index + 1} - Inner Corona", true)
        };
    }

    private LineRenderer CreateLine(string lineName, bool loop)
    {
        var lineObject = new GameObject(lineName);
        lineObject.transform.SetParent(transform, false);
        lineObject.hideFlags = HideFlags.HideInHierarchy;

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = loop;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCornerVertices = 3;
        line.numCapVertices = 3;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.lightProbeUsage = LightProbeUsage.Off;
        line.reflectionProbeUsage = ReflectionProbeUsage.Off;
        line.enabled = false;
        return line;
    }

    private void ApplyLinkStyles(LinkVisual link)
    {
        ApplyLineStyle(link.glow, glowWidth, glowColor, 0);
        ApplyLineStyle(link.bolt, boltWidth, boltColor, 1);
        ApplyLineStyle(link.core, coreWidth, coreColor, 2);
        ApplyLineStyle(link.sideA, sideArcWidth, sideArcColor, 1);
        ApplyLineStyle(link.sideB, sideArcWidth * 0.8f, sideArcColor, 1);
        ApplyLineStyle(link.pulse, pulseWidth, coreColor, 3);
    }

    private void ApplyLineStyle(LineRenderer line, float width, Color color, int sortingOrder)
    {
        line.sharedMaterial = lineMaterial;
        line.widthMultiplier = width;
        line.startColor = Color.white;
        line.endColor = Color.white;
        line.sortingOrder = sortingOrder;

        var properties = new MaterialPropertyBlock();
        properties.SetColor(ColorId, color);
        properties.SetColor(BaseColorId, color);
        line.SetPropertyBlock(properties);
    }

    private void EnsureBuffers(LinkVisual link)
    {
        if (link.current != null && link.current.Length == segments)
            return;

        link.current = new Vector3[segments];
        link.target = new Vector3[segments];
        link.sideCurrentA = new Vector3[segments];
        link.sideTargetA = new Vector3[segments];
        link.sideCurrentB = new Vector3[segments];
        link.sideTargetB = new Vector3[segments];
        link.initialized = false;
    }

    private void ResetPlayback()
    {
        isPlaying = false;
        source = null;
        sourceWeapon = null;
        revealedLinks = 0;
        visualAlpha = 0f;
        targets.Clear();
        selectedTargets.Clear();

        for (int i = 0; i < links.Count; i++)
        {
            links[i].initialized = false;
            links[i].refreshIndex = 0;
            links[i].nextRefresh = 0f;
            SetLinkVisible(links[i], false);
        }

        for (int i = 0; i < coronas.Count; i++)
            SetCoronaVisible(coronas[i], false);
    }

    private void ReleaseToPool()
    {
        if (pooledObject != null)
            pooledObject.Release();
        else
            gameObject.SetActive(false);
    }

    private static bool IsValidEnemy(Enemy enemy)
    {
        return enemy != null &&
               enemy.gameObject.activeInHierarchy &&
               !enemy.isDead &&
               enemy.CurrentHealth > 0f;
    }

    private static void CopyPoints(Vector3[] sourcePoints, Vector3[] destinationPoints)
    {
        System.Array.Copy(sourcePoints, destinationPoints, sourcePoints.Length);
    }

    private static void MorphPoints(Vector3[] current, Vector3[] target, Vector3 start, Vector3 end, float blend)
    {
        for (int i = 1; i < current.Length - 1; i++)
            current[i] = Vector3.Lerp(current[i], target[i], blend);

        current[0] = start;
        current[current.Length - 1] = end;
    }

    private static void SetPositions(LineRenderer line, Vector3[] positions)
    {
        line.positionCount = positions.Length;
        line.SetPositions(positions);
    }

    private static Vector3 SamplePath(Vector3[] points, float t)
    {
        float scaled = Mathf.Clamp01(t) * (points.Length - 1);
        int index = Mathf.Min(Mathf.FloorToInt(scaled), points.Length - 2);
        return Vector3.Lerp(points[index], points[index + 1], scaled - index);
    }

    private static void ApplyWidthAndAlpha(LineRenderer line, float width, float alpha)
    {
        line.widthMultiplier = width;
        Color color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
        line.startColor = color;
        line.endColor = color;
    }

    private static void SetLinkVisible(LinkVisual link, bool visible)
    {
        link.glow.enabled = visible;
        link.bolt.enabled = visible;
        link.core.enabled = visible;
        link.sideA.enabled = visible;
        link.sideB.enabled = visible;
        link.pulse.enabled = visible;
    }

    private static void SetCoronaVisible(CoronaVisual corona, bool visible)
    {
        corona.outer.enabled = visible;
        corona.inner.enabled = visible;
    }
}
