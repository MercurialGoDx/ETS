using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class BossContractOutline : MonoBehaviour
{
    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
    private static readonly int GlowIntensityId = Shader.PropertyToID("_GlowIntensity");

    private readonly List<GameObject> outlineObjects = new();
    private readonly List<SkinnedMeshRenderer> outlineRenderers = new();
    private Material runtimeMaterial;
    private Material sourceMaterial;
    private bool renderersCreated;

    public void Show(Material materialTemplate, Color color, float width, float glowIntensity)
    {
        if (materialTemplate == null)
        {
            Debug.LogWarning("[BossContractOutline] Outline material is not assigned.");
            return;
        }

        EnsureMaterial(materialTemplate);
        EnsureRenderers();

        if (outlineObjects.Count == 0)
        {
            Debug.LogWarning("[BossContractOutline] No skinned mesh renderers were found.");
            return;
        }

        runtimeMaterial.SetColor(OutlineColorId, color);
        runtimeMaterial.SetFloat(OutlineWidthId, Mathf.Max(0f, width));
        runtimeMaterial.SetFloat(GlowIntensityId, Mathf.Max(0f, glowIntensity));

        for (int i = 0; i < outlineObjects.Count; i++)
            outlineObjects[i].SetActive(true);
    }

    public void Hide()
    {
        for (int i = 0; i < outlineObjects.Count; i++)
            outlineObjects[i].SetActive(false);
    }

    private void OnEnable()
    {
        Hide();
    }

    private void OnDisable()
    {
        Hide();
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);
    }

    private void EnsureMaterial(Material materialTemplate)
    {
        if (runtimeMaterial != null && sourceMaterial == materialTemplate)
            return;

        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);

        sourceMaterial = materialTemplate;
        runtimeMaterial = new Material(materialTemplate)
        {
            name = $"{materialTemplate.name} (Runtime)",
            hideFlags = HideFlags.DontSave
        };

        for (int i = 0; i < outlineRenderers.Count; i++)
            AssignMaterial(outlineRenderers[i]);
    }

    private void EnsureRenderers()
    {
        if (renderersCreated)
            return;

        renderersCreated = true;
        SkinnedMeshRenderer[] sourceRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (SkinnedMeshRenderer source in sourceRenderers)
        {
            if (source.sharedMesh == null)
                continue;

            var outlineObject = new GameObject($"{source.name}_ContractOutline")
            {
                layer = source.gameObject.layer,
                hideFlags = HideFlags.DontSave
            };
            outlineObject.transform.SetParent(source.transform, false);

            SkinnedMeshRenderer outline = outlineObject.AddComponent<SkinnedMeshRenderer>();
            outline.sharedMesh = source.sharedMesh;
            outline.bones = source.bones;
            outline.rootBone = source.rootBone;
            outline.localBounds = source.localBounds;
            outline.quality = source.quality;
            outline.updateWhenOffscreen = source.updateWhenOffscreen;
            outline.shadowCastingMode = ShadowCastingMode.Off;
            outline.receiveShadows = false;
            outline.lightProbeUsage = LightProbeUsage.Off;
            outline.reflectionProbeUsage = ReflectionProbeUsage.Off;
            outline.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            outlineObjects.Add(outlineObject);
            outlineRenderers.Add(outline);
            AssignMaterial(outline);
        }
    }

    private void AssignMaterial(SkinnedMeshRenderer renderer)
    {
        int materialCount = Mathf.Max(1, renderer.sharedMesh.subMeshCount);
        var materials = new Material[materialCount];
        for (int i = 0; i < materials.Length; i++)
            materials[i] = runtimeMaterial;

        renderer.sharedMaterials = materials;
    }
}
