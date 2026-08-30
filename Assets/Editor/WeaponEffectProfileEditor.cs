#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WeaponEffectProfile))]
public sealed class WeaponEffectProfileEditor : Editor
{
    private SerializedProperty effectsProperty;
    private readonly Dictionary<Object, Editor> effectEditors = new();

    private void OnEnable()
    {
        effectsProperty = serializedObject.FindProperty("effects");
    }

    private void OnDisable()
    {
        foreach (Editor effectEditor in effectEditors.Values)
        {
            if (effectEditor != null)
                DestroyImmediate(effectEditor);
        }
        effectEditors.Clear();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        int removeIndex = -1;
        int moveFrom = -1;
        int moveTo = -1;

        for (int i = 0; i < effectsProperty.arraySize; i++)
        {
            SerializedProperty entry = effectsProperty.GetArrayElementAtIndex(i);
            WeaponOnHitEffect effect = entry.objectReferenceValue as WeaponOnHitEffect;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                effect != null ? GetDisplayName(effect) : "Missing Effect",
                EditorStyles.boldLabel);

            GUI.enabled = i > 0;
            if (GUILayout.Button("▲", GUILayout.Width(28f)))
            {
                moveFrom = i;
                moveTo = i - 1;
            }

            GUI.enabled = i < effectsProperty.arraySize - 1;
            if (GUILayout.Button("▼", GUILayout.Width(28f)))
            {
                moveFrom = i;
                moveTo = i + 1;
            }

            GUI.enabled = true;
            if (GUILayout.Button("Remove", GUILayout.Width(64f)))
                removeIndex = i;
            EditorGUILayout.EndHorizontal();

            if (effect == null)
            {
                EditorGUILayout.PropertyField(entry, GUIContent.none);
            }
            else
            {
                Editor effectEditor = GetOrCreateEditor(effect);
                effectEditor.OnInspectorGUI();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(3f);
        }

        if (moveFrom >= 0)
            effectsProperty.MoveArrayElement(moveFrom, moveTo);

        serializedObject.ApplyModifiedProperties();

        if (removeIndex >= 0)
            RemoveEffect(removeIndex);

        if (GUILayout.Button("+ Add Effect", GUILayout.Height(26f)))
            ShowAddEffectMenu();

        EditorGUILayout.Space(3f);
        WeaponOnHitEffect existing = EditorGUILayout.ObjectField(
            "Add Existing Effect",
            null,
            typeof(WeaponOnHitEffect),
            false) as WeaponOnHitEffect;
        if (existing != null)
            AddExistingEffect(existing);
    }

    private Editor GetOrCreateEditor(WeaponOnHitEffect effect)
    {
        if (!effectEditors.TryGetValue(effect, out Editor effectEditor) || effectEditor == null)
        {
            effectEditor = CreateEditor(effect);
            effectEditors[effect] = effectEditor;
        }
        return effectEditor;
    }

    private void ShowAddEffectMenu()
    {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("Stun"), false, () => AddNewEffect<StunWeaponEffect>("Stun"));
        menu.AddItem(new GUIContent("Freeze"), false, () => AddNewEffect<FreezeWeaponEffect>("Freeze"));
        menu.AddItem(new GUIContent("Weakness"), false, () => AddNewEffect<WeaknessWeaponEffect>("Weakness"));
        menu.AddItem(new GUIContent("Healing"), false, () => AddNewEffect<HealingWeaponEffect>("Healing"));
        menu.AddItem(new GUIContent("Burning"), false, () => AddNewEffect<BurningWeaponEffect>("Burning"));
        menu.ShowAsContext();
    }

    private void AddNewEffect<T>(string displayName) where T : WeaponOnHitEffect
    {
        WeaponEffectProfile profile = (WeaponEffectProfile)target;
        T effect = CreateInstance<T>();
        effect.name = displayName;

        Undo.RegisterCreatedObjectUndo(effect, "Add weapon effect");
        AssetDatabase.AddObjectToAsset(effect, profile);
        AddExistingEffect(effect);
        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(profile));
        AssetDatabase.SaveAssets();
    }

    private void AddExistingEffect(WeaponOnHitEffect effect)
    {
        serializedObject.Update();
        int index = effectsProperty.arraySize;
        effectsProperty.InsertArrayElementAtIndex(index);
        effectsProperty.GetArrayElementAtIndex(index).objectReferenceValue = effect;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    private void RemoveEffect(int index)
    {
        serializedObject.Update();
        SerializedProperty entry = effectsProperty.GetArrayElementAtIndex(index);
        WeaponOnHitEffect effect = entry.objectReferenceValue as WeaponOnHitEffect;

        entry.objectReferenceValue = null;
        effectsProperty.DeleteArrayElementAtIndex(index);
        serializedObject.ApplyModifiedProperties();

        if (effect != null &&
            AssetDatabase.GetAssetPath(effect) == AssetDatabase.GetAssetPath(target))
        {
            if (effectEditors.TryGetValue(effect, out Editor effectEditor) && effectEditor != null)
                DestroyImmediate(effectEditor);
            effectEditors.Remove(effect);
            Undo.DestroyObjectImmediate(effect);
        }

        EditorUtility.SetDirty(target);
        AssetDatabase.SaveAssets();
    }

    private static string GetDisplayName(WeaponOnHitEffect effect)
    {
        string typeName = effect.GetType().Name
            .Replace("WeaponEffect", string.Empty)
            .Replace("Effect", string.Empty);
        return ObjectNames.NicifyVariableName(typeName);
    }
}

[CustomEditor(typeof(WeaponOnHitEffect), true)]
public sealed class WeaponOnHitEffectEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script");
        serializedObject.ApplyModifiedProperties();
    }
}
#endif
