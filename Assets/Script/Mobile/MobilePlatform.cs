using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>
/// Единая точка «мы на телефоне?» для игрового кода.
///
/// Флаг PLATFORM_MOBILE задаётся в Android-defines (см. AndroidBuildTools), поэтому
/// в редакторе с активным Android-таргетом мобильное поведение тоже включено и его
/// можно проверить без устройства. Application.isMobilePlatform страхует сборки,
/// собранные без нашего флага.
/// </summary>
public static class MobilePlatform
{
    public static bool IsMobile
    {
        get
        {
#if PLATFORM_MOBILE
            return true;
#else
            return Application.isMobilePlatform;
#endif
        }
    }

    private const string TargetFpsPrefKey = "mobile_target_fps";
    public const int HighFps = 60;
    public const int BatterySaverFps = 30;

    /// <summary>Лимит кадров, выбранный игроком: 60 по умолчанию, 30 — экономия батареи.</summary>
    public static int TargetFps
    {
        get => PlayerPrefs.GetInt(TargetFpsPrefKey, HighFps) == BatterySaverFps ? BatterySaverFps : HighFps;
        set
        {
            int fps = value == BatterySaverFps ? BatterySaverFps : HighFps;
            PlayerPrefs.SetInt(TargetFpsPrefKey, fps);
            PlayerPrefs.Save();
            ApplyFrameRate();
        }
    }

    /// <summary>
    /// Android по умолчанию держит 30 FPS, а vSync на мобильных игнорирует targetFrameRate.
    /// </summary>
    public static void ApplyFrameRate()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFps;
    }

    /// <summary>
    /// Пришло ли UI-событие от пальца. InputSystemUIInputModule помечает тип указателя явно;
    /// у старого StandaloneInputModule касания имеют неотрицательный pointerId, мышь — отрицательный.
    /// </summary>
    public static bool IsTouch(PointerEventData eventData)
    {
        if (eventData == null)
            return false;

        if (eventData is ExtendedPointerEventData extended)
            return extended.pointerType == UIPointerType.Touch;

        return eventData.pointerId >= 0;
    }
}
