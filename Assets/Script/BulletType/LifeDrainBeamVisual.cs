using UnityEngine;

[DisallowMultipleComponent]
public sealed class LifeDrainBeamVisual : MonoBehaviour
{
    [Header("Beam Ribbons")]
    [SerializeField] private LineRenderer coreLine;
    [SerializeField] private LineRenderer glowLine;
    [SerializeField] private LineRenderer[] spiralLines;
    [SerializeField] private LineRenderer[] flowStreaks;

    [Header("Endpoint Effects")]
    [SerializeField] private Transform targetAnchor;
    [SerializeField] private ParticleSystem[] loopingParticles;

    [Header("Animation")]
    [SerializeField, Range(8, 64)] private int beamSegments = 28;
    [SerializeField, Min(0f)] private float coreWaveAmplitude = 0.018f;
    [SerializeField, Min(0f)] private float spiralRadius = 0.105f;
    [SerializeField, Min(0f)] private float spiralTurns = 2.6f;
    [SerializeField, Min(0f)] private float spiralSpeed = 4.2f;
    [SerializeField, Min(0.01f)] private float flowSpeed = 1.25f;
    [SerializeField, Min(0.01f)] private float streakWorldLength = 0.72f;

    private const int StreakPointCount = 6;
    private float animationTime;

    private void OnEnable()
    {
        animationTime = 0f;
        ClearLines();

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

        UpdateRibbon(coreLine, coreWaveAmplitude, 1.35f, 0f, 1.25f);
        UpdateRibbon(glowLine, coreWaveAmplitude * 0.6f, 0.9f, 1.7f, 0.85f);
        UpdateSpirals();
        UpdateFlowStreaks(beamLength);
    }

    private void UpdateRibbon(
        LineRenderer line,
        float amplitude,
        float speed,
        float phaseOffset,
        float turns)
    {
        if (line == null)
            return;

        int count = Mathf.Max(8, beamSegments);
        line.enabled = true;
        line.positionCount = count;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)(count - 1);
            float endpointFade = Mathf.Sin(Mathf.PI * t);
            float angle = t * turns * Mathf.PI * 2f + animationTime * speed + phaseOffset;
            float pulse = 0.72f + 0.28f * Mathf.Sin(animationTime * 5.4f - t * 8f);
            float radius = amplitude * endpointFade * pulse;

            line.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                t));
        }
    }

    private void UpdateSpirals()
    {
        if (spiralLines == null || spiralLines.Length == 0)
            return;

        for (int lineIndex = 0; lineIndex < spiralLines.Length; lineIndex++)
        {
            LineRenderer line = spiralLines[lineIndex];
            if (line == null)
                continue;

            int count = Mathf.Max(8, beamSegments);
            float linePhase = lineIndex * (Mathf.PI * 2f / spiralLines.Length);
            line.enabled = true;
            line.positionCount = count;

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                float endpointFade = Mathf.Sin(Mathf.PI * t);
                float breathing = 0.78f + 0.22f * Mathf.Sin(animationTime * 3.1f + t * 10f);
                float angle = t * spiralTurns * Mathf.PI * 2f +
                    animationTime * spiralSpeed + linePhase;
                float radius = spiralRadius * endpointFade * breathing;

                line.SetPosition(i, new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    t));
            }
        }
    }

    private void UpdateFlowStreaks(float beamLength)
    {
        if (flowStreaks == null || flowStreaks.Length == 0)
            return;

        float normalizedLength = Mathf.Clamp(streakWorldLength / beamLength, 0.045f, 0.3f);

        for (int lineIndex = 0; lineIndex < flowStreaks.Length; lineIndex++)
        {
            LineRenderer line = flowStreaks[lineIndex];
            if (line == null)
                continue;

            // Progress travels from the enemy (1) back to the tower (0): the visual
            // direction therefore reads as life being pulled out of the target.
            float offset = lineIndex / (float)flowStreaks.Length;
            float head = Mathf.Repeat(1f - animationTime * flowSpeed - offset, 1f);
            float orbitPhase = lineIndex * 1.91f + animationTime * spiralSpeed;

            line.enabled = true;
            line.positionCount = StreakPointCount;

            for (int i = 0; i < StreakPointCount; i++)
            {
                float fraction = i / (float)(StreakPointCount - 1);
                float t = Mathf.Clamp01(head + normalizedLength * fraction);
                float endpointFade = Mathf.Sin(Mathf.PI * t);
                float angle = orbitPhase + t * Mathf.PI * 3.2f;
                float radius = spiralRadius * 0.58f * endpointFade;

                line.SetPosition(i, new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    t));
            }
        }
    }

    private void ClearLines()
    {
        ClearLine(coreLine);
        ClearLine(glowLine);
        ClearLineArray(spiralLines);
        ClearLineArray(flowStreaks);
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
        LateUpdate();
    }
#endif
}
