using UnityEngine;

[DisallowMultipleComponent]
public sealed class UltimateLaserBeamVisual : MonoBehaviour
{
    [Header("Main Beam")]
    [SerializeField] private LineRenderer coreLine;
    [SerializeField] private LineRenderer glowLine;
    [SerializeField] private LineRenderer[] twinBeams;
    [SerializeField] private LineRenderer[] plasmaRibbons;
    [SerializeField] private LineRenderer[] lightningFilaments;

    [Header("Moving Energy")]
    [SerializeField] private LineRenderer[] energyBolts;
    [SerializeField] private LineRenderer[] shockRings;
    [SerializeField] private LineRenderer[] animeBrushStreaks;
    [SerializeField] private LineRenderer[] sourceRings;
    [SerializeField] private LineRenderer[] orbitalSpirals;

    [Header("Endpoints")]
    [SerializeField] private Transform targetAnchor;
    [SerializeField] private Transform[] energyCones;
    [SerializeField] private ParticleSystem beamEmbers;
    [SerializeField] private ParticleSystem blueBeamSparks;
    [SerializeField] private ParticleSystem[] spiralParticles;
    [SerializeField] private ParticleSystem[] loopingParticles;

    [Header("Animation")]
    [SerializeField, Range(12, 64)] private int beamSegments = 36;
    [SerializeField, Min(0f)] private float coreTurbulence = 0.025f;
    [SerializeField, Min(0f)] private float plasmaRadius = 0.16f;
    [SerializeField, Min(0f)] private float plasmaTurns = 3.4f;
    [SerializeField, Min(0f)] private float plasmaSpeed = 5.6f;
    [SerializeField, Min(0.01f)] private float boltSpeed = 2.15f;
    [SerializeField, Min(0.01f)] private float boltWorldLength = 0.95f;
    [SerializeField, Min(0.01f)] private float ringSpeed = 1.35f;
    [SerializeField, Min(0.01f)] private float coneSpeed = 0.82f;

    private const int BoltPointCount = 7;
    private const int RingPointCount = 28;

    private float animationTime;
    private float coreBaseWidth;
    private float glowBaseWidth;
    private bool widthsCached;

    private void Awake()
    {
        CacheWidths();
    }

    private void OnEnable()
    {
        CacheWidths();
        animationTime = 0f;
        ClearLines();
        ResetCones();

        if (!Application.isPlaying || loopingParticles == null)
            return;

        foreach (ParticleSystem particles in loopingParticles)
        {
            if (particles == null)
                continue;

            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Play(true);
        }
    }

    private void OnDisable()
    {
        ClearLines();
        ResetCones();

        if (targetAnchor != null)
            targetAnchor.localPosition = Vector3.zero;

        if (loopingParticles == null)
            return;

        foreach (ParticleSystem particles in loopingParticles)
        {
            if (particles != null)
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void LateUpdate()
    {
        animationTime += Time.deltaTime;

        float beamLength = Mathf.Abs(transform.localScale.z);
        if (beamLength <= 0.001f)
        {
            ClearLines();
            return;
        }

        if (targetAnchor != null)
            targetAnchor.localPosition = Vector3.forward * beamLength;

        UpdateWidths();
        UpdateMainBeam();
        UpdateTwinBeams();
        UpdatePlasmaRibbons();
        UpdateLightningFilaments();
        UpdateEnergyBolts(beamLength);
        UpdateShockRings();
        UpdateAnimeBrushStreaks(beamLength);
        UpdateSourceRings();
        UpdateOrbitalSpirals();
        UpdateEnergyCones(beamLength);
        UpdateBeamEmbers(beamLength);
    }

    private void CacheWidths()
    {
        if (widthsCached || coreLine == null || glowLine == null)
            return;

        coreBaseWidth = coreLine.widthMultiplier;
        glowBaseWidth = glowLine.widthMultiplier;
        widthsCached = true;
    }

    private void UpdateWidths()
    {
        CacheWidths();
        if (!widthsCached)
            return;

        coreLine.widthMultiplier = coreBaseWidth *
            (0.92f + 0.13f * Mathf.Sin(animationTime * 11.5f));
        glowLine.widthMultiplier = glowBaseWidth *
            (0.78f + 0.26f * Mathf.Sin(animationTime * 4.8f + 0.7f));
    }

    private void UpdateMainBeam()
    {
        int count = Mathf.Max(12, beamSegments);
        PopulateMainLine(coreLine, count, coreTurbulence, 0f);
        PopulateMainLine(glowLine, count, coreTurbulence * 0.45f, 1.9f);
    }

    private void PopulateMainLine(
        LineRenderer line,
        int count,
        float amplitude,
        float phaseOffset)
    {
        if (line == null)
            return;

        line.enabled = true;
        line.positionCount = count;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)(count - 1);
            float endpointFade = Mathf.Sin(Mathf.PI * t);
            float x = Mathf.Sin(t * 31f + animationTime * 8.3f + phaseOffset) +
                Mathf.Sin(t * 73f - animationTime * 4.7f) * 0.35f;
            float y = Mathf.Cos(t * 27f - animationTime * 7.1f + phaseOffset) +
                Mathf.Sin(t * 61f + animationTime * 5.2f) * 0.32f;

            line.SetPosition(i, new Vector3(
                x * amplitude * endpointFade,
                y * amplitude * endpointFade,
                t));
        }
    }

    private void UpdatePlasmaRibbons()
    {
        if (plasmaRibbons == null || plasmaRibbons.Length == 0)
            return;

        int count = Mathf.Max(12, beamSegments - 4);
        for (int lineIndex = 0; lineIndex < plasmaRibbons.Length; lineIndex++)
        {
            LineRenderer line = plasmaRibbons[lineIndex];
            if (line == null)
                continue;

            float phase = lineIndex * Mathf.PI * 2f / plasmaRibbons.Length;
            line.enabled = true;
            line.positionCount = count;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                float endpointFade = Mathf.Sin(Mathf.PI * t);
                float breathing = 0.8f + 0.2f *
                    Mathf.Sin(animationTime * 6.2f - t * 13f + phase);
                float angle = t * plasmaTurns * Mathf.PI * 2f +
                    animationTime * plasmaSpeed + phase;
                float radius = plasmaRadius * endpointFade * breathing;

                line.SetPosition(i, new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    t));
            }
        }
    }

    private void UpdateTwinBeams()
    {
        if (twinBeams == null || twinBeams.Length == 0)
            return;

        int count = Mathf.Max(12, beamSegments - 2);
        for (int lineIndex = 0; lineIndex < twinBeams.Length; lineIndex++)
        {
            LineRenderer line = twinBeams[lineIndex];
            if (line == null)
                continue;

            float side = lineIndex % 2 == 0 ? -1f : 1f;
            float phase = lineIndex * Mathf.PI;
            line.enabled = true;
            line.positionCount = count;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                float endpointFade = Mathf.Sin(Mathf.PI * t);
                float crossing = Mathf.Sin(t * Mathf.PI * 4.5f +
                    animationTime * 7.4f + phase);
                float vertical = Mathf.Cos(t * Mathf.PI * 3.2f -
                    animationTime * 6.1f + phase);

                line.SetPosition(i, new Vector3(
                    side * 0.055f * endpointFade + crossing * 0.038f * endpointFade,
                    vertical * 0.046f * endpointFade,
                    t));
            }
        }
    }

    private void UpdateLightningFilaments()
    {
        if (lightningFilaments == null || lightningFilaments.Length == 0)
            return;

        const int count = 24;
        float flickerFrame = Mathf.Floor(animationTime * 24f) * 0.17f;

        for (int lineIndex = 0; lineIndex < lightningFilaments.Length; lineIndex++)
        {
            LineRenderer line = lightningFilaments[lineIndex];
            if (line == null)
                continue;

            line.enabled = true;
            line.positionCount = count;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                float endpointFade = Mathf.Sin(Mathf.PI * t);
                float noiseX = Mathf.PerlinNoise(
                    t * 8.4f + lineIndex * 2.37f,
                    flickerFrame) - 0.5f;
                float noiseY = Mathf.PerlinNoise(
                    t * 9.1f + lineIndex * 3.11f,
                    flickerFrame + 17.3f) - 0.5f;
                float biasAngle = lineIndex * Mathf.PI * 2f /
                    lightningFilaments.Length + t * Mathf.PI * 1.5f;
                float biasRadius = plasmaRadius * 0.42f * endpointFade;

                line.SetPosition(i, new Vector3(
                    Mathf.Cos(biasAngle) * biasRadius + noiseX * 0.095f * endpointFade,
                    Mathf.Sin(biasAngle) * biasRadius + noiseY * 0.095f * endpointFade,
                    t));
            }
        }
    }

    private void UpdateEnergyBolts(float beamLength)
    {
        if (energyBolts == null || energyBolts.Length == 0)
            return;

        float normalizedLength = Mathf.Clamp(boltWorldLength / beamLength, 0.055f, 0.32f);

        for (int lineIndex = 0; lineIndex < energyBolts.Length; lineIndex++)
        {
            LineRenderer line = energyBolts[lineIndex];
            if (line == null)
                continue;

            float offset = lineIndex / (float)energyBolts.Length;
            float head = Mathf.Repeat(animationTime * boltSpeed + offset, 1f);
            float orbitPhase = lineIndex * 2.17f - animationTime * plasmaSpeed * 0.55f;

            line.enabled = true;
            line.positionCount = BoltPointCount;

            for (int i = 0; i < BoltPointCount; i++)
            {
                float fraction = i / (float)(BoltPointCount - 1);
                float t = Mathf.Clamp01(head - normalizedLength * (1f - fraction));
                float endpointFade = Mathf.Sin(Mathf.PI * t);
                float angle = orbitPhase + t * Mathf.PI * 4.2f;
                float radius = plasmaRadius * 0.48f * endpointFade;

                line.SetPosition(i, new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    t));
            }
        }
    }

    private void UpdateShockRings()
    {
        if (shockRings == null || shockRings.Length == 0)
            return;

        for (int ringIndex = 0; ringIndex < shockRings.Length; ringIndex++)
        {
            LineRenderer ring = shockRings[ringIndex];
            if (ring == null)
                continue;

            float offset = ringIndex / (float)shockRings.Length;
            float progress = Mathf.Repeat(animationTime * ringSpeed + offset, 1f);
            float envelope = Mathf.Sin(Mathf.PI * progress);
            float radius = plasmaRadius * (0.72f + 0.5f * envelope) *
                (0.9f + 0.1f * Mathf.Sin(animationTime * 9f + ringIndex));

            ring.enabled = true;
            ring.positionCount = RingPointCount;

            for (int i = 0; i < RingPointCount; i++)
            {
                float angle = i / (float)RingPointCount * Mathf.PI * 2f;
                ring.SetPosition(i, new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    progress));
            }
        }
    }

    private void UpdateEnergyCones(float beamLength)
    {
        if (energyCones == null || energyCones.Length == 0)
            return;

        for (int coneIndex = 0; coneIndex < energyCones.Length; coneIndex++)
        {
            Transform cone = energyCones[coneIndex];
            if (cone == null)
                continue;

            float offset = coneIndex / (float)energyCones.Length;
            float progress = Mathf.Repeat(animationTime * coneSpeed + offset, 1f);
            float envelope = Mathf.Sin(Mathf.PI * progress);
            float radius = plasmaRadius * (1.3f + envelope * 1.05f);
            float length = 0.52f + envelope * 0.72f;

            cone.localPosition = Vector3.forward * (progress * beamLength);
            cone.localRotation = Quaternion.Euler(
                0f,
                0f,
                animationTime * (95f + coneIndex * 13f) + coneIndex * 37f);
            cone.localScale = new Vector3(radius, radius, length);
        }
    }

    private void UpdateAnimeBrushStreaks(float beamLength)
    {
        if (animeBrushStreaks == null || animeBrushStreaks.Length == 0)
            return;

        for (int streakIndex = 0; streakIndex < animeBrushStreaks.Length; streakIndex++)
        {
            LineRenderer streak = animeBrushStreaks[streakIndex];
            if (streak == null)
                continue;

            const int points = 9;
            float offset = streakIndex / (float)animeBrushStreaks.Length;
            float head = Mathf.Repeat(animationTime * (1.7f + streakIndex * 0.035f) + offset, 1f);
            float worldLength = 0.75f + (streakIndex % 4) * 0.28f;
            float normalizedLength = Mathf.Clamp(worldLength / beamLength, 0.07f, 0.34f);
            float tail = head - normalizedLength;
            float radialPhase = streakIndex * 2.41f + animationTime * 3.7f;

            streak.enabled = true;
            streak.positionCount = points;

            for (int i = 0; i < points; i++)
            {
                float fraction = i / (float)(points - 1);
                float t = Mathf.Clamp01(Mathf.Lerp(tail, head, fraction));
                float endpointFade = Mathf.Sin(Mathf.PI * t);
                float ragged = Mathf.Sin(fraction * 17f + streakIndex * 1.7f +
                    animationTime * 8f) * 0.028f;
                float radius = (0.035f + (streakIndex % 3) * 0.028f + ragged) *
                    endpointFade;
                float angle = radialPhase + t * Mathf.PI * (2.1f + streakIndex * 0.08f);

                streak.SetPosition(i, new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    t));
            }
        }
    }

    private void UpdateSourceRings()
    {
        if (sourceRings == null || sourceRings.Length == 0)
            return;

        const int points = 36;
        for (int ringIndex = 0; ringIndex < sourceRings.Length; ringIndex++)
        {
            LineRenderer ring = sourceRings[ringIndex];
            if (ring == null)
                continue;

            float phase = animationTime * (2.2f + ringIndex * 0.37f) + ringIndex * 1.4f;
            float radius = 0.25f + ringIndex * 0.085f +
                Mathf.Sin(animationTime * 6f + ringIndex) * 0.035f;
            float tilt = 0.035f + ringIndex * 0.018f;

            ring.enabled = true;
            ring.positionCount = points;

            for (int i = 0; i < points; i++)
            {
                float angle = i / (float)points * Mathf.PI * 2f + phase;
                ring.SetPosition(i, new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    Mathf.Sin(angle * 2f + phase) * tilt + 0.025f * ringIndex));
            }
        }
    }

    private void UpdateOrbitalSpirals()
    {
        if (orbitalSpirals == null || orbitalSpirals.Length == 0)
            return;

        int count = Mathf.Max(20, beamSegments + 4);
        for (int spiralIndex = 0; spiralIndex < orbitalSpirals.Length; spiralIndex++)
        {
            LineRenderer spiral = orbitalSpirals[spiralIndex];
            if (spiral == null)
                continue;

            float direction = spiralIndex % 2 == 0 ? 1f : -1f;
            float phase = spiralIndex * Mathf.PI * 2f / orbitalSpirals.Length;
            float turns = 2.25f + spiralIndex * 0.52f;
            float baseRadius = plasmaRadius * (1.02f + spiralIndex * 0.13f);
            float speed = direction * (2.6f + spiralIndex * 0.38f);

            spiral.enabled = true;
            spiral.positionCount = count;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                float endpointFade = Mathf.Sin(Mathf.PI * t);
                float wave = 0.83f + 0.17f *
                    Mathf.Sin(animationTime * (4.2f + spiralIndex * 0.3f) -
                        t * (11f + spiralIndex));
                float angle = t * turns * Mathf.PI * 2f * direction +
                    animationTime * speed + phase;
                float radius = baseRadius * endpointFade * wave;

                spiral.SetPosition(i, new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    t));
            }
        }
    }

    private void UpdateBeamEmbers(float beamLength)
    {
        if (beamEmbers == null)
        {
            UpdateBeamParticleShape(blueBeamSparks, beamLength, plasmaRadius * 2.15f);
            return;
        }

        UpdateBeamParticleShape(beamEmbers, beamLength, plasmaRadius * 1.8f);
        UpdateBeamParticleShape(blueBeamSparks, beamLength, plasmaRadius * 2.15f);

        if (spiralParticles != null)
        {
            for (int i = 0; i < spiralParticles.Length; i++)
            {
                float thickness = plasmaRadius * (2.3f + i * 0.42f);
                UpdateBeamParticleShape(spiralParticles[i], beamLength, thickness);
            }
        }
    }

    private static void UpdateBeamParticleShape(
        ParticleSystem particles,
        float beamLength,
        float thickness)
    {
        if (particles == null)
            return;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.position = Vector3.forward * (beamLength * 0.5f);
        shape.scale = new Vector3(thickness, thickness, beamLength);
    }

    private void ClearLines()
    {
        ClearLine(coreLine);
        ClearLine(glowLine);
        ClearLineArray(twinBeams);
        ClearLineArray(plasmaRibbons);
        ClearLineArray(lightningFilaments);
        ClearLineArray(energyBolts);
        ClearLineArray(shockRings);
        ClearLineArray(animeBrushStreaks);
        ClearLineArray(sourceRings);
        ClearLineArray(orbitalSpirals);

        if (widthsCached)
        {
            if (coreLine != null)
                coreLine.widthMultiplier = coreBaseWidth;
            if (glowLine != null)
                glowLine.widthMultiplier = glowBaseWidth;
        }
    }

    private void ResetCones()
    {
        if (energyCones == null)
            return;

        foreach (Transform cone in energyCones)
        {
            if (cone == null)
                continue;

            cone.localPosition = Vector3.zero;
            cone.localRotation = Quaternion.identity;
            cone.localScale = Vector3.zero;
        }
    }

    private static void ClearLineArray(LineRenderer[] lines)
    {
        if (lines == null)
            return;

        foreach (LineRenderer line in lines)
            ClearLine(line);
    }

    private static void ClearLine(LineRenderer line)
    {
        if (line == null)
            return;

        line.positionCount = 0;
        line.enabled = false;
    }

#if UNITY_EDITOR
    public void EditorPreview(float beamLength, float previewTime)
    {
        Vector3 scale = transform.localScale;
        scale.z = Mathf.Max(0.01f, beamLength);
        transform.localScale = scale;
        animationTime = Mathf.Max(0f, previewTime);
        CacheWidths();
        LateUpdate();
    }
#endif
}
