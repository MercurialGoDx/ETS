using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Player;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ETS.Mobile.Editor
{
    /// <summary>
    /// Android-настройки и сборка порта. Всё, что раньше делалось руками в Build Profile,
    /// собрано здесь, чтобы одинаково работать из меню и из командной строки:
    ///
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod ETS.Mobile.Editor.AndroidBuildTools.BuildDevelopmentApkCli
    ///
    /// Правка PlayerSettings через API, а не текстом: ProjectSettings.asset — сериализованный
    /// YAML, и ручная правка в нём ломается так же, как в сцене.
    /// </summary>
    public static class AndroidBuildTools
    {
        public const string ApplicationId = "com.havocstudioteam.ets";

        // DISABLESTEAMWORKS вырезает Steamworks.NET и всё, что на нём держится.
        // PLATFORM_MOBILE — наш собственный флаг: мобильное поведение без привязки к UNITY_ANDROID,
        // чтобы его можно было проверить компиляцией и на Windows-таргете.
        public static readonly string[] MobileDefines = { "DISABLESTEAMWORKS", "PLATFORM_MOBILE" };

        private const string MainScenePath = "Assets/Scenes/MainScene.unity";
        private const string BuildRoot = "Builds/Android";
        private const string MenuRoot = "Tools/Android/";

        // ===================== НАСТРОЙКИ =====================

        [MenuItem(MenuRoot + "Apply Android Player Settings")]
        public static void ApplyAndroidPlayerSettings()
        {
            var android = NamedBuildTarget.Android;

            PlayerSettings.SetApplicationIdentifier(android, ApplicationId);
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            // Google Play требует свежий target API; «Auto» берёт самый новый установленный SDK.
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            MergeDefines(android, MobileDefines);

            // Только горизонтальная ориентация, с поворотом между двумя landscape.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            // Рисуем только в безопасной области: вырез камеры и скруглённые углы не
            // перекрывают HUD без правки каждого Canvas. Цена — чёрные поля по бокам
            // на телефонах с вырезом; это заметно лишь в горизонтали и только у края.
            PlayerSettings.Android.renderOutsideSafeArea = false;

            // Ровная подача кадров (Swappy): на экранах 90/120 Гц кадр, чуть не успевший
            // в 16,6 мс, иначе показывается через 25 мс, и 60 FPS превращаются в рваные ~49.
            PlayerSettings.Android.optimizedFramePacing = true;
            PlayerSettings.Android.startInFullscreen = true;

            AssetDatabase.SaveAssets();
            Debug.Log("[AndroidBuild] Android Player Settings applied: " + ApplicationId
                + ", IL2CPP ARM64, API 24+, landscape, defines " + string.Join(";", MobileDefines));
        }

        /// <summary>
        /// Первый шаг на чистой машине: без Android-define Steamworks.NET не компилируется
        /// под Android, и -executeMethod на Android-таргете не запустится. Поэтому настройки
        /// применяются с Windows-таргета (-buildTarget StandaloneWindows64), а сборка — следующим запуском.
        /// </summary>
        public static void ApplyAndroidPlayerSettingsCli()
        {
            ApplyAndroidPlayerSettings();
            EditorApplication.Exit(0);
        }

        private static void MergeDefines(NamedBuildTarget target, string[] required)
        {
            var current = PlayerSettings.GetScriptingDefineSymbols(target)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(d => d.Trim())
                .ToList();

            foreach (var define in required)
            {
                if (!current.Contains(define))
                    current.Add(define);
            }

            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", current));
        }

        // ===================== ПРОВЕРКА КОМПИЛЯЦИИ =====================

        /// <summary>
        /// Компилирует player-скрипты с мобильными define на Windows-таргете. Не требует
        /// Android-модуля, поэтому ловит ошибки ветки DISABLESTEAMWORKS/PLATFORM_MOBILE
        /// на любой машине. Это проверка компиляции, а не замена Android-сборки.
        /// </summary>
        [MenuItem(MenuRoot + "Compile Check (mobile defines, Windows target)")]
        public static void CompileCheckMobileDefines()
        {
            bool ok = CompilePlayerScripts(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, MobileDefines);
            Debug.Log(ok
                ? "[AndroidBuild] Mobile-define compile check PASSED."
                : "[AndroidBuild] Mobile-define compile check FAILED — see errors above.");
        }

        public static void CompileCheckMobileDefinesCli()
        {
            bool ok = CompilePlayerScripts(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, MobileDefines);
            bool pcOk = CompilePlayerScripts(BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone, Array.Empty<string>());
            Debug.Log($"[AndroidBuild] Compile check: mobile defines {(ok ? "PASSED" : "FAILED")}, PC {(pcOk ? "PASSED" : "FAILED")}.");
            EditorApplication.Exit(ok && pcOk ? 0 : 1);
        }

        private static bool CompilePlayerScripts(BuildTarget target, BuildTargetGroup group, string[] extraDefines)
        {
            string output = Path.Combine("Temp", "MobileCompileCheck", target + "_" + string.Join("_", extraDefines));
            Directory.CreateDirectory(output);

            var settings = new ScriptCompilationSettings
            {
                target = target,
                group = group,
                options = ScriptCompilationOptions.None,
                extraScriptingDefines = extraDefines,
            };

            var result = PlayerBuildInterface.CompilePlayerScripts(settings, output);
            // При ошибках компилятор уже вывел их в лог; пустой список сборок — признак провала.
            return result.assemblies != null && result.assemblies.Count > 0;
        }

        // ===================== СБОРКА =====================

        [MenuItem(MenuRoot + "Build Development APK")]
        public static void BuildDevelopmentApk() => Build(development: true, appBundle: false);

        [MenuItem(MenuRoot + "Build Release AAB (Google Play)")]
        public static void BuildReleaseAab() => Build(development: false, appBundle: true);

        public static void BuildDevelopmentApkCli() => ExitWith(Build(development: true, appBundle: false));

        public static void BuildReleaseAabCli() => ExitWith(Build(development: false, appBundle: true));

        /// <summary>
        /// Development APK с дополнительной x86_64-библиотекой для эмулятора на ПК: образы
        /// эмулятора x86_64, а трансляция ARM в них работает не для всех IL2CPP-сборок.
        /// Архитектуры проекта после сборки возвращаются к ARM64 — в Google Play x86_64 не нужен.
        /// </summary>
        [MenuItem(MenuRoot + "Build Emulator APK (arm64 + x86_64)")]
        public static void BuildEmulatorApk() => BuildForEmulator();

        public static void BuildEmulatorApkCli() => ExitWith(BuildForEmulator());

        private static bool BuildForEmulator()
        {
            try
            {
                return Build(development: true, appBundle: false, "ETS-emulator",
                    AndroidArchitecture.ARM64 | AndroidArchitecture.X86_64);
            }
            finally
            {
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                AssetDatabase.SaveAssets();
            }
        }

        private static void ExitWith(bool ok) => EditorApplication.Exit(ok ? 0 : 1);

        private static bool Build(bool development, bool appBundle)
            => Build(development, appBundle, development ? "ETS-dev" : "ETS-release", AndroidArchitecture.ARM64);

        private static bool Build(bool development, bool appBundle, string fileName, AndroidArchitecture architectures)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
            {
                Debug.LogError("[AndroidBuild] Android Build Support не установлен для этой версии Unity.");
                return false;
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            ApplyAndroidPlayerSettings();
            PlayerSettings.Android.targetArchitectures = architectures;
            EditorUserBuildSettings.buildAppBundle = appBundle;

            if (!appBundle)
            {
                // Release AAB без ключа Google Play не примет; ключ хранит владелец и
                // задаёт в Publishing Settings. Для dev-APK хватает debug-подписи.
                PlayerSettings.Android.useCustomKeystore = false;
            }

            string path = Path.Combine(BuildRoot, fileName + (appBundle ? ".aab" : ".apk"));
            Directory.CreateDirectory(BuildRoot);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { MainScenePath },
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = development ? BuildOptions.Development : BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[AndroidBuild] {summary.result}: {path}, errors {summary.totalErrors}, "
                + $"warnings {summary.totalWarnings}, {summary.totalTime.TotalSeconds:F0}s");

            if (summary.result == BuildResult.Succeeded && File.Exists(path))
                Debug.Log($"[AndroidBuild] Size: {new FileInfo(path).Length / (1024f * 1024f):F1} MiB");

            return summary.result == BuildResult.Succeeded;
        }
    }
}
