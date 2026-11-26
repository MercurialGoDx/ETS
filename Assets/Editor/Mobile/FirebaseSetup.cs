using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ETS.Mobile.Editor
{
    /// <summary>
    /// Настройка Firebase Unity SDK под проект, где Firebase нужен только Android:
    /// 1. Плагины Firebase исключаются из ПК-сборки — Steam-версия их не грузит и не несёт.
    /// 2. Включаются Gradle-шаблоны, чтобы External Dependency Manager вписывал Android-зависимости
    ///    в mainTemplate.gradle, а не складывал десятки .aar в Assets.
    /// 3. Запускается разрешение Android-зависимостей.
    ///
    ///   Unity.exe -batchmode -projectPath . -buildTarget Android -executeMethod ETS.Mobile.Editor.FirebaseSetup.ConfigureCli
    ///
    /// SDK в проекте урезан: без iOS/tvOS и десктопных библиотек macOS/Linux (см. docs/MobilePlan.md, Фаза 11).
    /// </summary>
    public static class FirebaseSetup
    {
        private const string FirebasePluginRoot = "Assets/Firebase/Plugins";
        private const string AndroidPluginDir = "Assets/Plugins/Android";

        private static readonly string[] GradleTemplates =
        {
            "mainTemplate.gradle", "settingsTemplate.gradle", "gradleTemplate.properties",
        };

        private static readonly BuildTarget[] DesktopTargets =
        {
            BuildTarget.StandaloneWindows, BuildTarget.StandaloneWindows64,
            BuildTarget.StandaloneOSX, BuildTarget.StandaloneLinux64,
        };

        [MenuItem("Tools/Android/Firebase: Configure and Resolve")]
        public static void Configure()
        {
            ExcludeFirebaseFromDesktopPlayers();
            EnableGradleTemplates();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ResolveAndroidDependencies();
        }

        public static void ConfigureCli()
        {
            try
            {
                Configure();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Управляемые DLL Firebase — редактор и Android; нативные x86_64-библиотеки Windows —
        /// только редактор (Play Mode). В ПК-плеер не попадает ничего.
        /// </summary>
        private static void ExcludeFirebaseFromDesktopPlayers()
        {
            int changed = 0;
            foreach (string guid in AssetDatabase.FindAssets("", new[] { FirebasePluginRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is PluginImporter plugin))
                    continue;

                bool editorOnly = path.Contains("/x86_64/");
                plugin.SetCompatibleWithAnyPlatform(false);
                plugin.SetCompatibleWithEditor(true);
                plugin.SetCompatibleWithPlatform(BuildTarget.Android, !editorOnly);
                foreach (var target in DesktopTargets)
                    plugin.SetCompatibleWithPlatform(target, false);

                plugin.SaveAndReimport();
                changed++;
            }
            Debug.Log($"[FirebaseSetup] Плагинов Firebase настроено (Editor + Android): {changed}.");
        }

        private static void EnableGradleTemplates()
        {
            string source = Path.Combine(BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.Android, BuildOptions.None),
                "Tools", "GradleTemplates");
            Directory.CreateDirectory(AndroidPluginDir);
            foreach (string template in GradleTemplates)
            {
                string destination = Path.Combine(AndroidPluginDir, template);
                if (!File.Exists(destination))
                    File.Copy(Path.Combine(source, template), destination);
            }

            // В публичном API этих флажков нет — те же, что «Custom Main Gradle Template» и т. п.
            // в Player Settings → Publishing Settings.
            var playerSettings = Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings");
            var serialized = new SerializedObject(playerSettings);
            foreach (string property in new[]
                     { "useCustomMainGradleTemplate", "useCustomGradleSettingsTemplate", "useCustomGradlePropertiesTemplate" })
            {
                var flag = serialized.FindProperty(property);
                if (flag != null)
                    flag.boolValue = true;
                else
                    Debug.LogWarning("[FirebaseSetup] Нет свойства PlayerSettings." + property);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[FirebaseSetup] Gradle-шаблоны включены.");
        }

        private static void ResolveAndroidDependencies()
        {
            // EDM4U грузится как отдельная DLL с версией в пути — зовём через рефлексию,
            // чтобы этот файл компилировался и до импорта SDK.
            Type resolver = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                resolver = assembly.GetType("GooglePlayServices.PlayServicesResolver");
                if (resolver != null)
                    break;
            }

            MethodInfo resolveSync = resolver?.GetMethod("ResolveSync", BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(bool) }, null);
            if (resolveSync == null)
            {
                Debug.LogError("[FirebaseSetup] External Dependency Manager не найден — Android-зависимости не разрешены.");
                return;
            }

            bool ok = (bool)resolveSync.Invoke(null, new object[] { true });
            Debug.Log("[FirebaseSetup] Разрешение Android-зависимостей: " + (ok ? "успешно" : "ошибка"));
        }
    }
}
