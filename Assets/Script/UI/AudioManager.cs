using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Music Source")]
    public AudioSource musicSource;
    public AudioLowPassFilter lowPassFilter;

    [Header("Music Clips")]
    public AudioClip menuMusic;
    public AudioClip gameMusic;

    [Header("Volume Settings")]
    [Range(0f, 1f)] public float masterVolume = 1f;      // громкость игрока (через слайдер)
    [Range(0f, 1f)] public float pauseMultiplier = 0.5f; // во сколько раз тише в паузе

    [Header("LowPass Settings")]
    public float normalCutoff = 22000f;   // обычный звук
    public float pausedCutoff = 6000f;    // глухой эффект в паузе

    private float _currentBaseVolume = 1f;
    private bool _isPausedFx = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (lowPassFilter != null)
        {
            lowPassFilter.enabled = false;
            lowPassFilter.cutoffFrequency = normalCutoff;
        }
    }

    private void Start()
    {
        PlayMenuMusic();
    }

    // ========= ГРОМКОСТЬ (добавили) =========
    public void SetMasterVolume(float value)
    {
        masterVolume = Mathf.Clamp01(value);
        _currentBaseVolume = masterVolume;

        if (musicSource != null)
        {
            // если сейчас пауза — учитываем множитель
            if (_isPausedFx)
                musicSource.volume = _currentBaseVolume * pauseMultiplier;
            else
                musicSource.volume = _currentBaseVolume;
        }
    }

    public float GetMasterVolume()
    {
        return masterVolume;
    }
    // ========================================

    public void PlayMenuMusic()
    {
        PlayMusic(menuMusic);
    }

    public void PlayGameMusic()
    {
        PlayMusic(gameMusic);
    }

    private void PlayMusic(AudioClip clip)
    {
        if (clip == null || musicSource == null) return;

        _isPausedFx = false;

        _currentBaseVolume = masterVolume;

        musicSource.clip = clip;
        musicSource.volume = _currentBaseVolume;
        musicSource.loop = true;
        musicSource.Play();

        if (lowPassFilter != null)
        {
            lowPassFilter.enabled = false;
            lowPassFilter.cutoffFrequency = normalCutoff;
        }
    }

    // === Пауза ===
    public void ApplyPauseFx()
    {
        if (_isPausedFx || musicSource == null) return;

        _isPausedFx = true;

        // Уменьшаем громкость
        musicSource.volume = _currentBaseVolume * pauseMultiplier;

        // Включаем лёгкий эффект глушения
        if (lowPassFilter != null)
        {
            lowPassFilter.enabled = true;
            lowPassFilter.cutoffFrequency = pausedCutoff;
        }
    }

    // === Возобновление ===
    public void ResetPauseFx()
    {
        if (!_isPausedFx || musicSource == null) return;

        _isPausedFx = false;

        // Возвращаем громкость
        musicSource.volume = _currentBaseVolume;

        // Отключаем LowPass
        if (lowPassFilter != null)
        {
            lowPassFilter.enabled = false;
            lowPassFilter.cutoffFrequency = normalCutoff;
        }
    }
}
