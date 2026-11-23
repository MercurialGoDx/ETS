using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ETS.Mobile.Editor
{
    /// <summary>
    /// Снимки интерфейса в разрешениях телефонов без устройства. Проходит главное меню →
    /// магазин подготовки → бой → инвентарь → пауза и на каждом шаге рендерит кадр
    /// в RenderTexture нужного размера (16:9, 19,5:9, 20:9, 4:3).
    ///
    ///   Unity.exe -batchmode -projectPath . -buildTarget Android -executeMethod ETS.Mobile.Editor.MobileLayoutCapture.RunCli
    ///
    /// Без -quit: редактору нужен живой цикл для Play Mode, выход — сам по завершении.
    /// Overlay-канвасы в текстуру не рисуются, поэтому на время снимка канвас переводится
    /// в Screen Space Camera; CanvasScaler при этом считает размер по текстуре, как по экрану.
    /// Это проверка раскладки, а не точная картинка устройства: DPI и шрифты — редакторные.
    /// </summary>
    [InitializeOnLoad]
    public static class MobileLayoutCapture
    {
        private const string ActiveKey = "ETS.MobileLayoutCapture.Active";
        private const string StepKey = "ETS.MobileLayoutCapture.Step";
        private const string OutputDir = "Logs/AndroidPort/Screens";
        private const string MainScenePath = "Assets/Scenes/MainScene.unity";

        private static readonly Vector2Int[] Resolutions =
        {
            new Vector2Int(1920, 1080), // 16:9
            new Vector2Int(2340, 1080), // 19,5:9
            new Vector2Int(2400, 1080), // 20:9
            new Vector2Int(2048, 1536), // 4:3, планшет
        };

        private struct Step
        {
            public string name;
            public Action action;
            public float waitSeconds;
        }

        private static List<Step> steps;
        private static int stepIndex;
        private static int resolutionIndex;
        private static double nextTime;
        private static bool captureArmed;
        private static int armedFrame;
        private static RenderTexture captureTexture;
        private static readonly List<(Canvas canvas, RenderMode mode, Camera cam, float plane)> switchedCanvases = new();

        static MobileLayoutCapture()
        {
            if (SessionState.GetBool(ActiveKey, false))
                EditorApplication.update += Tick;
        }

        [MenuItem("Tools/Android/Capture Mobile Layout Screenshots")]
        public static void Run()
        {
            Directory.CreateDirectory(OutputDir);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetInt(StepKey, 0);
            EditorSceneManager.OpenScene(MainScenePath);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        public static void RunCli() => Run();

        private static void BuildSteps()
        {
            steps = new List<Step>
            {
                new Step { name = "01_menu", waitSeconds = 3f },
                new Step { name = "02_prepare_shop", waitSeconds = 2f, action = () =>
                {
                    Find<StartMenu>()?.StartGame();
                    Find<TutorialManager>()?.Dismiss();
                }},
                new Step { name = "03_battle", waitSeconds = 6f, action = () =>
                {
                    Find<TutorialManager>()?.Dismiss();
                    Find<GameStartController>()?.OnReadyClicked();
                }},
                new Step { name = "04_inventory", waitSeconds = 1f, action = () => Find<InventoryUI>()?.Toggle() },
                new Step { name = "05_pause", waitSeconds = 1f, action = () =>
                {
                    Find<InventoryUI>()?.Toggle();
                    Find<PauseManager>()?.SetCanPause(true);
                    Find<PauseManager>()?.TogglePause();
                }},
            };
        }

        private static T Find<T>() where T : UnityEngine.Object
            => UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

        private static void Tick()
        {
            if (!EditorApplication.isPlaying)
                return;

            if (steps == null)
            {
                BuildSteps();
                stepIndex = SessionState.GetInt(StepKey, 0);
                resolutionIndex = 0;
                nextTime = EditorApplication.timeSinceStartup + steps[stepIndex].waitSeconds;
            }

            if (EditorApplication.timeSinceStartup < nextTime)
                return;

            try
            {
                if (!captureArmed)
                {
                    ArmCapture(Resolutions[resolutionIndex]);
                    return;
                }

                // CanvasScaler пересчитывает масштаб в своём Update — ждём пару кадров.
                if (Time.frameCount < armedFrame + 2)
                    return;

                SaveCapture(steps[stepIndex].name, Resolutions[resolutionIndex]);
                resolutionIndex++;

                if (resolutionIndex < Resolutions.Length)
                    return;

                resolutionIndex = 0;
                stepIndex++;
                SessionState.SetInt(StepKey, stepIndex);

                if (stepIndex >= steps.Count)
                {
                    Finish(0);
                    return;
                }

                steps[stepIndex].action?.Invoke();
                nextTime = EditorApplication.timeSinceStartup + steps[stepIndex].waitSeconds;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Finish(1);
            }
        }

        private static void ArmCapture(Vector2Int size)
        {
            Camera cam = Camera.main;
            if (cam == null)
                throw new InvalidOperationException("Main Camera не найдена.");

            captureTexture = new RenderTexture(size.x, size.y, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = captureTexture;

            switchedCanvases.Clear();
            foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    continue;

                switchedCanvases.Add((canvas, canvas.renderMode, canvas.worldCamera, canvas.planeDistance));
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                // Чем выше sortingOrder, тем ближе к камере: порядок наложения как у Overlay.
                canvas.planeDistance = Mathf.Max(cam.nearClipPlane + 0.01f, 1f - canvas.sortingOrder * 0.001f);
            }

            captureArmed = true;
            armedFrame = Time.frameCount;
        }

        private static void SaveCapture(string stepName, Vector2Int size)
        {
            Camera cam = Camera.main;
            Canvas.ForceUpdateCanvases();
            cam.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = captureTexture;
            var texture = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;

            string path = Path.Combine(OutputDir, $"{stepName}_{size.x}x{size.y}.png");
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            Debug.Log("[MobileLayoutCapture] " + path);

            foreach (var (canvas, mode, worldCam, plane) in switchedCanvases)
            {
                if (canvas == null) continue;
                canvas.renderMode = mode;
                canvas.worldCamera = worldCam;
                canvas.planeDistance = plane;
            }
            switchedCanvases.Clear();

            cam.targetTexture = null;
            captureTexture.Release();
            UnityEngine.Object.DestroyImmediate(captureTexture);
            captureTexture = null;
            captureArmed = false;
        }

        private static void Finish(int exitCode)
        {
            EditorApplication.update -= Tick;
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseInt(StepKey);
            steps = null;
            Debug.Log("[MobileLayoutCapture] Done, exit code " + exitCode);

            if (Application.isBatchMode)
                EditorApplication.Exit(exitCode);
            else
                EditorApplication.ExitPlaymode();
        }
    }
}
