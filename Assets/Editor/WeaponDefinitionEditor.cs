#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WeaponDefinition))]
public sealed class WeaponDefinitionEditor : Editor
{
    private SerializedProperty effectProfileProperty;
    private Editor profileEditor;

    private void OnEnable()
    {
        effectProfileProperty = serializedObject.FindProperty("effectProfile");
    }

    private void OnDisable()
    {
        if (profileEditor != null)
            DestroyImmediate(profileEditor);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "effectProfile");

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Эффекты при попадании", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(effectProfileProperty, new GUIContent("Effect Profile"));

        if (effectProfileProperty.objectReferenceValue == null)
        {
            EditorGUILayout.HelpBox(
                "У оружия нет эффектов. Создайте профиль, затем добавьте нужные эффекты кнопкой + Add Effect.",
                MessageType.Info);

            if (GUILayout.Button("Create Effect Profile"))
                CreateAndAssignProfile();
        }

        serializedObject.ApplyModifiedProperties();

        WeaponEffectProfile profile = effectProfileProperty.objectReferenceValue as WeaponEffectProfile;
        if (profile == null)
            return;

        EditorGUILayout.Space(5f);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        Editor.CreateCachedEditor(profile, null, ref profileEditor);
        profileEditor.OnInspectorGUI();
        EditorGUILayout.EndVertical();
    }

    private void CreateAndAssignProfile()
    {
        string weaponPath = AssetDatabase.GetAssetPath(target);
        string directory = Path.GetDirectoryName(weaponPath)?.Replace('\\', '/');
        string weaponName = Path.GetFileNameWithoutExtension(weaponPath);
        string profilePath = AssetDatabase.GenerateUniqueAssetPath(
            $"{directory}/{weaponName}_Effects.asset");

        WeaponEffectProfile profile = CreateInstance<WeaponEffectProfile>();
        profile.name = weaponName + "_Effects";
        AssetDatabase.CreateAsset(profile, profilePath);

        serializedObject.Update();
        effectProfileProperty.objectReferenceValue = profile;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
        AssetDatabase.SaveAssets();
        Selection.activeObject = target;
    }
}
#endif
