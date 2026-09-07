using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A long travelling energy projectile. Its head deals damage as soon as it reaches the
/// target, while the visible tail continues along the travelled path before the pooled
/// object is released.
/// </summary>
public sealed class ProjectileLanceBullet : MonoBehaviour, IAttackBehaviour
{
    private enum FlightPhase
    {
        Inactive,
        Flying,
        FinishingTail
    }

    [Header("Flight")]
    [SerializeField, Min(0.1f)] private float projectileLength = 7.5f;
    [SerializeField, Min(0.1f)] private float tailCatchUpSpeedMultiplier = 1.15f;
    [SerializeField, Min(0.1f)] private float maxLifeTime = 10f;
    [SerializeField] private bool homing = true;
    [SerializeField, Min(0f)] private float bossDamageMultiplier = 1f;

    [Header("Instant kill")]
    [SerializeField, Range(0f, 100f)]
    [Tooltip("Chance in percent to instantly kill a regular enemy on hit.")]
    private float regularEnemyInstantKillChance = 5f;
    [SerializeField, Range(0f, 100f)]
    [Tooltip("Separate chance in percent to instantly kill a boss on hit.")]
    private float bossInstantKillChance = 1f;

    [Header("Beam shape")]
    [SerializeField] private Material lineMaterial;
    [SerializeField, Range(8, 48)] private int lineSegments = 24;
    [SerializeField, Min(0.01f)] private float glowWidth = 0.62f;
    [SerializeField, Min(0.01f)] private float bodyWidth = 0.28f;
    [SerializeField, Min(0.005f)] private float coreWidth = 0.085f;
    [SerializeField, Min(0.001f)] private float ribbonWidth = 0.045f;
    [SerializeField, Min(0f)] private float spiralRadius = 0.22f;
    [SerializeField, Min(0f)] private float spiralTurnsPerUnit = 0.42f;
    [SerializeField] private float spiralDegreesPerSecond = 520f;
    [SerializeField, Min(0f)] private float pulseSpeed = 11f;

    [Header("Beam colors (HDR)")]
    [SerializeField, ColorUsage(true, true)] private Color glowColor = new(2.8f, 0.12f, 1.15f, 0.28f);
    [SerializeField, ColorUsage(true, true)] private Color bodyColor = new(4.5f, 0.55f, 1.35f, 0.92f);
    [SerializeField, ColorUsage(true, true)] private Color coreColor = new(6f, 5.5f, 6f, 1f);
    [SerializeField, ColorUsage(true, true)] private Color ribbonColor = new(0.45f, 3.4f, 5.2f, 0.82f);

    [Header("Impact")]
    [SerializeField, Min(0.05f)] private float impactDuration = 0.72f;
    [SerializeField, Min(0.1f)] private float impactRadius = 1.65f;
    [SerializeField, Min(0.1f)] private float impactScaleMultiplier = 1f;
    [SerializeField] private bool scaleImpactToTargetCollider = true;
    [SerializeField, Range(4, 24)] private int sparkCount = 12;
    [SerializeField, ColorUsage(true, true)] private Color impactColor = new(5.5f, 0.35f, 1.5f, 1f);
    [SerializeField, ColorUsage(true, true)] private Color impactAccentColor = new(0.7f, 4.2f, 6f, 1f);

    private readonly List<Vector3> pathPositions = new(48);
    private readonly List<float> pathDistances = new(48);

    private PooledObject pooledObject;
    private LineRenderer glowLine;
    private LineRenderer bodyLine;
    private LineRenderer coreLine;
    private LineRenderer[] ribbons;
    private LineRenderer[] headFlares;
    private LineRenderer[] impactRings;
    private LineRenderer[] impactSparks;
    private Light impactLight;
    private Material fallbackMaterial;

    private FlightPhase phase;
    private Transform targetTransform;
    private Enemy targetEnemy;
    private uint targetActivationVersion;
    private WeaponDefinition sourceWeapon;
    private float damage;
    private float speed;
    private float lifeTimer;
    private float headDistance;
    private float tailDistance;
    private Vector3 headPosition;
    private Vector3 moveDirection;

    private bool impactActive;
    private float impactElapsed;
    private float impactScale = 1f;
    private Vector3 impactPosition;
    private Vector3 impactDirection;
    private bool tailFinished;

    private void Awake()
    {
        pooledObject = GetComponent<PooledObject>();
        BuildVisuals();
        HideAllVisuals();
    }

    private void OnDisable()
    {
        phase = FlightPhase.Inactive;
        impactActive = false;
        targetTransform = null;
        targetEnemy = null;
        sourceWeapon = null;
        pathPositions.Clear();
        pathDistances.Clear();
        HideAllVisuals();
    }

    private void OnDestroy()
    {
        if (fallbackMaterial != null)
            Destroy(fallbackMaterial);
    }

    public void InitAttack(AttackContext context)
    {
        BuildVisuals();
        HideAllVisuals();

        sourceWeapon = context.weapon;
        damage = context.damage;
        speed = Mathf.Max(0.1f, context.projectileSpeed);
        targetTransform = context.target;
        targetEnemy = targetTransform != null ? targetTransform.GetComponent<Enemy>() : null;
        targetActivationVersion = targetEnemy != null ? targetEnemy.ActivationVersion : 0;

        Vector3 origin = context.firePoint != null ? context.firePoint.position : transform.position;
        transform.position = origin;
        headPosition = origin;
        headDistance = 0f;
        tailDistance = 0f;
        lifeTimer = 0f;
        impactElapsed = 0f;
        impactActive = false;
        tailFinished = false;

        pathPositions.Clear();
        pathDistances.Clear();
        pathPositions.Add(origin);
        pathDistances.Add(0f);

        Vector3 targetPosition = GetCurrentTargetPosition();
        moveDirection = (targetPosition - origin).normalized;
        if (moveDirection.sqrMagnitude < 0.0001f)
            moveDirection = context.firePoint != null ? context.firePoint.forward : transform.forward;
        if (moveDirection.sqrMagnitude < 0.0001f)
            moveDirection = Vector3.forward;

        phase = FlightPhase.Flying;
    }

    private void Update()
    {
        if (phase == FlightPhase.Inactive)
            return;

        float deltaTime = Time.deltaTime;
        lifeTimer += deltaTime;
        if (lifeTimer >= maxLifeTime)
        {
            Release();
            return;
        }

        if (phase == FlightPhase.Flying)
            UpdateFlight(deltaTime);
        else
            UpdateFinishingTail(deltaTime);

        RenderProjectile();
        UpdateImpact(deltaTime);

        if (phase == FlightPhase.FinishingTail && tailFinished && !impactActive)
            Release();
    }

    private void UpdateFlight(float deltaTime)
    {
        if (!IsTargetValid())
        {
            phase = FlightPhase.FinishingTail;
            return;
        }

        Vector3 targetPosition = GetCurrentTargetPosition();
        Vector3 toTarget = targetPosition - headPosition;
        float distanceToTarget = toTarget.magnitude;
        float moveDistance = speed * deltaTime;

        if (homing && distanceToTarget > 0.0001f)
            moveDirection = toTarget / distanceToTarget;

        if (distanceToTarget <= moveDistance)
        {
            AppendHeadPosition(targetPosition);
            tailDistance = Mathf.Max(0f, headDistance - projectileLength);
            HitTarget();
            return;
        }

        AppendHeadPosition(headPosition + moveDirection * moveDistance);
        tailDistance = Mathf.Max(0f, headDistance - projectileLength);
    }

    private void UpdateFinishingTail(float deltaTime)
    {
        tailDistance = Mathf.MoveTowards(
            tailDistance,
            headDistance,
            speed * tailCatchUpSpeedMultiplier * deltaTime);
        tailFinished = headDistance - tailDistance <= 0.015f;
    }

    private void HitTarget()
    {
        impactPosition = headPosition;
        impactDirection = GetPathDirection(headDistance);
        if (impactDirection.sqrMagnitude < 0.0001f)
            impactDirection = moveDirection;

        if (IsTargetValid())
        {
            bool isBoss = targetEnemy.StatusEffects != null && targetEnemy.StatusEffects.IsBoss;
            bool instantKill = RollInstantKill(isBoss);
            float hitDamage = instantKill
                ? Mathf.Max(0f, targetEnemy.CurrentHealth)
                : damage;

            if (!instantKill && isBoss)
                hitDamage *= Mathf.Max(0f, bossDamageMultiplier);

            impactScale = CalculateImpactScale(targetEnemy);
            targetEnemy.TakeWeaponDamage(hitDamage, sourceWeapon);
            DamageStatsManager.Instance?.RegisterDamage(sourceWeapon, hitDamage);
            BeginImpact();
        }

        phase = FlightPhase.FinishingTail;
    }

    private bool RollInstantKill(bool isBoss)
    {
        float chancePercent = isBoss
            ? bossInstantKillChance
            : regularEnemyInstantKillChance;

        chancePercent = Mathf.Clamp(chancePercent, 0f, 100f);
        return chancePercent >= 100f ||
               (chancePercent > 0f && Random.value < chancePercent / 100f);
    }

    private bool IsTargetValid()
    {
        return targetTransform != null &&
               targetEnemy != null &&
               targetEnemy.ActivationVersion == targetActivationVersion &&
               !targetEnemy.isDead &&
               targetEnemy.gameObject.activeInHierarchy;
    }

    private Vector3 GetCurrentTargetPosition()
    {
        return targetEnemy != null ? targetEnemy.GetCenterPosition() :
            targetTransform != null ? targetTransform.position : headPosition + moveDirection;
    }

    private float CalculateImpactScale(Enemy enemy)
    {
        float result = Mathf.Max(0.1f, impactScaleMultiplier);
        if (!scaleImpactToTargetCollider || enemy == null)
            return result;

        Collider targetCollider = enemy.GetComponent<Collider>();
        if (targetCollider == null)
            return result;

        float colliderRadius = Mathf.Max(
            targetCollider.bounds.extents.x,
            Mathf.Max(targetCollider.bounds.extents.y, targetCollider.bounds.extents.z));
        return result * Mathf.Clamp(colliderRadius, 0.75f, 2.5f);
    }

    private void AppendHeadPosition(Vector3 newPosition)
    {
        float travelled = Vector3.Distance(headPosition, newPosition);
        if (travelled <= 0.00001f)
            return;

        headDistance += travelled;
        headPosition = newPosition;
        pathPositions.Add(newPosition);
        pathDistances.Add(headDistance);
    }

    private Vector3 SamplePath(float distance)
    {
        if (pathPositions.Count == 0)
            return headPosition;
        if (distance <= 0f)
            return pathPositions[0];
        if (distance >= headDistance)
            return pathPositions[pathPositions.Count - 1];

        int low = 0;
        int high = pathDistances.Count - 1;
        while (low + 1 < high)
        {
            int middle = (low + high) / 2;
            if (pathDistances[middle] <= distance)
                low = middle;
            else
                high = middle;
        }

        float segmentLength = pathDistances[high] - pathDistances[low];
        float t = segmentLength > 0.00001f
            ? (distance - pathDistances[low]) / segmentLength
            : 0f;
        return Vector3.LerpUnclamped(pathPositions[low], pathPositions[high], t);
    }

    private Vector3 GetPathDirection(float distance)
    {
        float offset = Mathf.Max(0.04f, projectileLength / Mathf.Max(8, lineSegments));
        Vector3 from = SamplePath(Mathf.Max(0f, distance - offset));
        Vector3 to = SamplePath(Mathf.Min(headDistance, distance + offset));
        return (to - from).normalized;
    }

    private void RenderProjectile()
    {
        float visibleLength = headDistance - tailDistance;
        if (visibleLength <= 0.015f)
        {
            SetProjectileLinesEnabled(false);
            return;
        }

        SetProjectileLinesEnabled(true);
        int count = Mathf.Max(8, lineSegments);
        SetLinePointCount(count);

        float phaseRadians = spiralDegreesPerSecond * Mathf.Deg2Rad * Time.time;
        for (int index = 0; index < count; index++)
        {
            float normalized = index / (float)(count - 1);
            float distance = Mathf.Lerp(tailDistance, headDistance, normalized);
            Vector3 point = SamplePath(distance);
            Vector3 tangent = GetPathDirection(distance);
            BuildPerpendicularBasis(tangent, out Vector3 side, out Vector3 up);

            glowLine.SetPosition(index, point);
            bodyLine.SetPosition(index, point);
            coreLine.SetPosition(index, point);

            float edgeFade = Mathf.Sin(normalized * Mathf.PI);
            float spiralPhase = phaseRadians + distance * spiralTurnsPerUnit * Mathf.PI * 2f;
            Vector3 spiralOffset =
                (side * Mathf.Cos(spiralPhase) + up * Mathf.Sin(spiralPhase)) *
                spiralRadius * edgeFade;
            ribbons[0].SetPosition(index, point + spiralOffset);
            ribbons[1].SetPosition(index, point - spiralOffset);
        }

        float pulse = 0.92f + Mathf.Sin(Time.time * pulseSpeed) * 0.08f;
        glowLine.widthMultiplier = glowWidth * pulse;
        bodyLine.widthMultiplier = bodyWidth * (2f - pulse);
        coreLine.widthMultiplier = coreWidth;
        ribbons[0].widthMultiplier = ribbonWidth * pulse;
        ribbons[1].widthMultiplier = ribbonWidth * pulse;

        float headAlpha = phase == FlightPhase.Flying
            ? 1f
            : 1f - Mathf.Clamp01(impactElapsed / Mathf.Max(0.05f, impactDuration));
        RenderHeadFlare(headPosition, impactDirection.sqrMagnitude > 0.001f ? impactDirection : moveDirection, headAlpha);
    }

    private void SetLinePointCount(int count)
    {
        glowLine.positionCount = count;
        bodyLine.positionCount = count;
        coreLine.positionCount = count;
        for (int i = 0; i < ribbons.Length; i++)
            ribbons[i].positionCount = count;
    }

    private void RenderHeadFlare(Vector3 center, Vector3 direction, float alpha)
    {
        BuildPerpendicularBasis(direction, out Vector3 side, out Vector3 up);
        float pulse = 0.82f + Mathf.Sin(Time.time * pulseSpeed * 1.7f) * 0.18f;
        float radius = (impactActive ? 0.48f * impactScale : 0.28f) * pulse;

        SetTwoPointLine(headFlares[0], center - side * radius, center + side * radius);
        SetTwoPointLine(headFlares[1], center - up * radius, center + up * radius);
        SetLineStyle(headFlares[0], coreColor, coreWidth * 1.6f, alpha);
        SetLineStyle(headFlares[1], ribbonColor, coreWidth * 1.25f, alpha);
    }

    private void BeginImpact()
    {
        impactActive = true;
        impactElapsed = 0f;
        for (int i = 0; i < impactRings.Length; i++)
            impactRings[i].enabled = true;
        for (int i = 0; i < impactSparks.Length; i++)
            impactSparks[i].enabled = true;
        if (impactLight != null)
        {
            impactLight.transform.position = impactPosition;
            impactLight.enabled = true;
        }
    }

    private void UpdateImpact(float deltaTime)
    {
        if (!impactActive)
            return;

        impactElapsed += deltaTime;
        float normalized = Mathf.Clamp01(impactElapsed / Mathf.Max(0.05f, impactDuration));
        float eased = 1f - (1f - normalized) * (1f - normalized);
        float alpha = 1f - normalized;
        float radius = impactRadius * impactScale;

        BuildRing(impactRings[0], impactPosition, Vector3.right, Vector3.forward,
            Mathf.Lerp(0.08f, radius, eased));
        BuildPerpendicularBasis(impactDirection, out Vector3 side, out Vector3 up);
        BuildRing(impactRings[1], impactPosition, side, up,
            Mathf.Lerp(0.04f, radius * 0.72f, eased));
        SetLineStyle(impactRings[0], impactColor, 0.12f * impactScale, alpha);
        SetLineStyle(impactRings[1], impactAccentColor, 0.075f * impactScale, alpha * 0.9f);

        for (int i = 0; i < impactSparks.Length; i++)
        {
            Vector3 direction = GetSparkDirection(i, impactDirection);
            float lengthVariation = 0.65f + Repeat01(i * 0.6180339f) * 0.65f;
            float travel = radius * eased * lengthVariation;
            Vector3 start = impactPosition + direction * travel * 0.35f;
            Vector3 end = impactPosition + direction * travel;
            SetTwoPointLine(impactSparks[i], start, end);
            Color color = (i & 1) == 0 ? impactColor : impactAccentColor;
            SetLineStyle(impactSparks[i], color, 0.055f * impactScale, alpha);
        }

        if (impactLight != null)
        {
            impactLight.transform.position = impactPosition;
            impactLight.intensity = 5.5f * impactScale * alpha;
            impactLight.range = radius * 2.4f;
        }

        if (normalized < 1f)
            return;

        impactActive = false;
        for (int i = 0; i < impactRings.Length; i++)
            impactRings[i].enabled = false;
        for (int i = 0; i < impactSparks.Length; i++)
            impactSparks[i].enabled = false;
        if (impactLight != null)
            impactLight.enabled = false;
    }

    private void BuildVisuals()
    {
        if (glowLine != null)
            return;

        Material material = lineMaterial;
        if (material == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                fallbackMaterial = new Material(shader) { name = "ProjectileLance_Fallback" };
                material = fallbackMaterial;
            }
        }

        Transform visualRoot = new GameObject("Runtime Visuals").transform;
        visualRoot.SetParent(transform, false);

        glowLine = CreateLine(visualRoot, "Outer Glow", material, false, 20);
        bodyLine = CreateLine(visualRoot, "Energy Body", material, false, 21);
        coreLine = CreateLine(visualRoot, "White Core", material, false, 22);
        ribbons = new[]
        {
            CreateLine(visualRoot, "Animated Ribbon A", material, false, 23),
            CreateLine(visualRoot, "Animated Ribbon B", material, false, 23)
        };
        headFlares = new[]
        {
            CreateLine(visualRoot, "Head Flare A", material, false, 25),
            CreateLine(visualRoot, "Head Flare B", material, false, 25)
        };
        impactRings = new[]
        {
            CreateLine(visualRoot, "Impact Ring Ground", material, true, 26),
            CreateLine(visualRoot, "Impact Ring Front", material, true, 27)
        };

        int actualSparkCount = Mathf.Max(4, sparkCount);
        impactSparks = new LineRenderer[actualSparkCount];
        for (int i = 0; i < impactSparks.Length; i++)
            impactSparks[i] = CreateLine(visualRoot, $"Impact Spark {i + 1:00}", material, false, 28);

        GameObject lightObject = new("Impact Light");
        lightObject.transform.SetParent(visualRoot, false);
        impactLight = lightObject.AddComponent<Light>();
        impactLight.type = LightType.Point;
        impactLight.color = new Color(1f, 0.18f, 0.62f);
        impactLight.shadows = LightShadows.None;
        impactLight.enabled = false;

        AnimationCurve tapered = new(
            new Keyframe(0f, 0f),
            new Keyframe(0.07f, 1f),
            new Keyframe(0.82f, 1f),
            new Keyframe(1f, 0f));
        glowLine.widthCurve = tapered;
        bodyLine.widthCurve = tapered;
        coreLine.widthCurve = tapered;
        ribbons[0].widthCurve = tapered;
        ribbons[1].widthCurve = tapered;
    }

    private static LineRenderer CreateLine(
        Transform parent,
        string lineName,
        Material material,
        bool loop,
        int sortingOrder)
    {
        GameObject lineObject = new(lineName);
        lineObject.transform.SetParent(parent, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Tile;
        line.numCornerVertices = 3;
        line.numCapVertices = 5;
        line.loop = loop;
        line.positionCount = loop ? 32 : 2;
        line.sharedMaterial = material;
        line.sortingOrder = sortingOrder;
        line.enabled = false;
        return line;
    }

    private static void BuildRing(
        LineRenderer line,
        Vector3 center,
        Vector3 axisA,
        Vector3 axisB,
        float radius)
    {
        int count = Mathf.Max(12, line.positionCount);
        line.positionCount = count;
        for (int i = 0; i < count; i++)
        {
            float angle = i / (float)count * Mathf.PI * 2f;
            line.SetPosition(i, center +
                (axisA * Mathf.Cos(angle) + axisB * Mathf.Sin(angle)) * radius);
        }
    }

    private static void SetTwoPointLine(LineRenderer line, Vector3 start, Vector3 end)
    {
        line.enabled = true;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    private static void SetLineStyle(LineRenderer line, Color color, float width, float alpha)
    {
        Color visibleColor = color;
        visibleColor.a *= Mathf.Clamp01(alpha);
        line.startColor = visibleColor;
        line.endColor = visibleColor;
        line.widthMultiplier = width;
    }

    private static void BuildPerpendicularBasis(Vector3 direction, out Vector3 side, out Vector3 up)
    {
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        side = Vector3.Cross(direction, Vector3.up);
        if (side.sqrMagnitude < 0.001f)
            side = Vector3.Cross(direction, Vector3.right);
        side.Normalize();
        up = Vector3.Cross(side, direction).normalized;
    }

    private static Vector3 GetSparkDirection(int index, Vector3 incomingDirection)
    {
        float angle = index * 2.39996323f;
        float height = Mathf.Lerp(-0.35f, 0.75f, Repeat01(index * 0.7548777f));
        Vector3 radial = new(Mathf.Cos(angle), height, Mathf.Sin(angle));
        Vector3 backwards = incomingDirection.sqrMagnitude > 0.001f
            ? -incomingDirection.normalized * 0.35f
            : Vector3.zero;
        return (radial + backwards).normalized;
    }

    private static float Repeat01(float value)
    {
        return value - Mathf.Floor(value);
    }

    private void SetProjectileLinesEnabled(bool enabled)
    {
        glowLine.enabled = enabled;
        bodyLine.enabled = enabled;
        coreLine.enabled = enabled;
        for (int i = 0; i < ribbons.Length; i++)
            ribbons[i].enabled = enabled;
        for (int i = 0; i < headFlares.Length; i++)
            headFlares[i].enabled = enabled;

        if (enabled)
        {
            SetLineStyle(glowLine, glowColor, glowWidth, 1f);
            SetLineStyle(bodyLine, bodyColor, bodyWidth, 1f);
            SetLineStyle(coreLine, coreColor, coreWidth, 1f);
            SetLineStyle(ribbons[0], ribbonColor, ribbonWidth, 1f);
            SetLineStyle(ribbons[1], ribbonColor, ribbonWidth, 1f);
        }
    }

    private void HideAllVisuals()
    {
        if (glowLine == null)
            return;

        SetProjectileLinesEnabled(false);
        for (int i = 0; i < impactRings.Length; i++)
            impactRings[i].enabled = false;
        for (int i = 0; i < impactSparks.Length; i++)
            impactSparks[i].enabled = false;
        if (impactLight != null)
            impactLight.enabled = false;
    }

    private void Release()
    {
        phase = FlightPhase.Inactive;
        HideAllVisuals();
        if (pooledObject != null)
            pooledObject.Release();
        else
            gameObject.SetActive(false);
    }
}
