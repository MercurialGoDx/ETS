using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Включает мобильное поведение без правок в MainScene: лимит кадров, растягивание
/// интерфейса под широкие экраны и экранные кнопки (<see cref="MobileHud"/>).
/// На ПК ничего не делает.
/// </summary>
public static class MobileBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (!MobilePlatform.IsMobile)
            return;

        MobilePlatform.ApplyFrameRate();
        Input.multiTouchEnabled = true;

        // Подписка переживает перезагрузку сцены («Главное меню» грузит MainScene заново),
        // поэтому снимаем прежнюю на случай отключённого Domain Reload в редакторе.
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        FitCanvasesToScreen();

        if (Object.FindFirstObjectByType<MobileHud>() == null)
            new GameObject("MobileHud").AddComponent<MobileHud>();
    }

    /// <summary>
    /// Canvas'ы сцены масштабируются «наполовину по ширине, наполовину по высоте» (match 0.5).
    /// На телефоне 20:9 при эталоне 16:9 это обрезает сверху и снизу примерно десятую часть
    /// интерфейса. Expand вписывает эталонный прямоугольник в экран целиком: на широком экране
    /// лишнее место уходит по бокам, на планшете 4:3 — сверху и снизу.
    /// </summary>
    private static void FitCanvasesToScreen()
    {
        var scalers = Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var scaler in scalers)
        {
            if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }
    }
}
