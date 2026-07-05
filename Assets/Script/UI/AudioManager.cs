using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Music Source")]
    public AudioSource musicSource;
    public AudioLowPassFilter lowPassFilter;

    [Header("Menu Music Pool")]
    public AudioClip[] menuMusicPool;

    [Header("Game Music Pools (by stage)")]
    public AudioClip[] earlyGamePool; // 0-10 min
    public AudioClip[] midGamePool;   // 10-20 min
    public AudioClip[] lateGamePool;  // 20+ min

    [Header("Stage thresholds (seconds)")]
    public float midGameStartSeconds = 10f * 60f;  // 10 minutes
    public float lateGameStartSeconds = 20f * 60f; // 20 minutes

    [Header("Volume Settings")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float pauseMultiplier = 0.5f;

    [Header("LowPass Settings")]
    public float normalCutoff = 22000f;
    public float pausedCutoff = 6000f;

    private float _currentBaseVolume = 1f;
    private bool _isPausedFx = false;

    private enum MusicMode { Menu, Game }
    private MusicMode _mode = MusicMode.Menu;

    private float _gameTimeSeconds = 0f;   // прогрессия (влияет на выбор пула)
    private AudioClip _lastPlayedClip = null;

    // Последняя известная позиция воспроизведения (сек), пока окно в фокусе. Нужна, чтобы отличить
    // "трек честно доиграл до конца" от "источник прервали сворачиванием" и продолжить с той же секунды.
    private float _lastPlaybackTime = 0f;

    // Порог у конца клипа: если источник остановился в пределах этого отрезка от конца — считаем,
    // что трек доиграл естественно и пора переключаться на следующий.
    private const float TrackEndThreshold = 0.5f;

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

    private void Update()
    {
        // Пока окно приложения без фокуса (свёрнуто / открыто другое приложение) — не трогаем музыку и
        // прогрессию. При потере фокуса Unity сам приглушает/останавливает источник, из-за чего isPlaying
        // становится false; если бы мы это трактовали как "трек закончился", запустился бы новый трек.
        // Application.isFocused — надёжный признак фокуса, не зависящий от порядка вызова колбэков.
        if (!Application.isFocused)
            return;

        // В игре таймер прогрессии идёт по Time.deltaTime (пауза через timeScale остановит прогрессию — обычно это то, что надо)
        if (_mode == MusicMode.Game)
            _gameTimeSeconds += Time.deltaTime;

        if (musicSource == null || musicSource.clip == null)
            return;

        if (musicSource.isPlaying)
        {
            // Запоминаем позицию, пока играем — понадобится, чтобы отличить конец трека от прерывания.
            _lastPlaybackTime = musicSource.time;
            return;
        }

        // Источник не играет, хотя клип назначен. Два случая:
        // 1) трек доиграл до конца (последняя позиция была у конца клипа) -> следующий трек;
        // 2) трек прервали (свернули/потеряли фокус на прошлых кадрах) -> продолжаем с сохранённой позиции.
        bool reachedEnd = _lastPlaybackTime >= musicSource.clip.length - TrackEndThreshold;
        if (reachedEnd)
        {
            PlayNextTrack();
        }
        else
        {
            musicSource.time = Mathf.Clamp(_lastPlaybackTime, 0f, Mathf.Max(0f, musicSource.clip.length - 0.05f));
            musicSource.Play();
        }
    }

    // ========= ГРОМКОСТЬ =========
    public void SetMasterVolume(float value)
    {
        masterVolume = Mathf.Clamp01(value);
        _currentBaseVolume = masterVolume;

        if (musicSource != null)
        {
            musicSource.volume = _isPausedFx ? _currentBaseVolume * pauseMultiplier : _currentBaseVolume;
        }
    }

    public float GetMasterVolume() => masterVolume;
    // =============================

    // ====== Публичные методы для меню/игры ======
    public void PlayMenuMusic()
    {
        _mode = MusicMode.Menu;
        _gameTimeSeconds = 0f;
        PlayRandomFromPool(menuMusicPool);
    }

    public void PlayGameMusic()
    {
        _mode = MusicMode.Game;
        _gameTimeSeconds = 0f;
        PlayNextTrack(); // выберет early pool
    }

    // Если хочешь вручную “перескочить” трек (например кнопка Next)
    public void SkipTrack()
    {
        if (musicSource == null) return;
        musicSource.Stop();
        PlayNextTrack();
    }

    // Если у тебя таймер игры считается где-то ещё — можно синхронизировать отсюда
    public void SetGameTimeSeconds(float seconds)
    {
        _gameTimeSeconds = Mathf.Max(0f, seconds);
    }
    // ===========================================

    private void PlayNextTrack()
    {
        AudioClip[] pool = GetCurrentPool();
        PlayRandomFromPool(pool);
    }

    private AudioClip[] GetCurrentPool()
    {
        if (_mode == MusicMode.Menu)
            return menuMusicPool;

        // Game mode
        if (_gameTimeSeconds >= lateGameStartSeconds)
            return lateGamePool;

        if (_gameTimeSeconds >= midGameStartSeconds)
            return midGamePool;

        return earlyGamePool;
    }

    private void PlayRandomFromPool(AudioClip[] pool)
    {
        if (musicSource == null) return;
        if (pool == null || pool.Length == 0)
        {
            Debug.LogWarning($"AudioManager: pool is empty for mode={_mode}.");
            return;
        }

        _isPausedFx = false;
        _currentBaseVolume = masterVolume;

        // Выбор случайного трека, стараемся не повторять тот же самый подряд
        AudioClip chosen = ChooseRandomAvoidRepeat(pool, _lastPlayedClip);

        _lastPlayedClip = chosen;
        _lastPlaybackTime = 0f; // новый трек — сбрасываем позицию, чтобы детект конца не сработал по старой

        musicSource.clip = chosen;
        musicSource.volume = _currentBaseVolume;
        musicSource.loop = false; // важно: иначе трек будет зациклен и не переключится
        musicSource.Play();

        if (lowPassFilter != null)
        {
            lowPassFilter.enabled = false;
            lowPassFilter.cutoffFrequency = normalCutoff;
        }
    }

    private AudioClip ChooseRandomAvoidRepeat(AudioClip[] pool, AudioClip avoid)
    {
        if (pool.Length == 1) return pool[0];

        // до 6 попыток выбрать не тот же клип
        for (int i = 0; i < 6; i++)
        {
            var c = pool[Random.Range(0, pool.Length)];
            if (c != null && c != avoid) return c;
        }

        // если всё равно не вышло — возвращаем любой
        return pool[Random.Range(0, pool.Length)];
    }

    // === Пауза FX ===
    public void ApplyPauseFx()
    {
        if (_isPausedFx || musicSource == null) return;

        _isPausedFx = true;
        musicSource.volume = _currentBaseVolume * pauseMultiplier;

        if (lowPassFilter != null)
        {
            lowPassFilter.enabled = true;
            lowPassFilter.cutoffFrequency = pausedCutoff;
        }
    }

    public void ResetPauseFx()
    {
        if (!_isPausedFx || musicSource == null) return;

        _isPausedFx = false;
        musicSource.volume = _currentBaseVolume;

        if (lowPassFilter != null)
        {
            lowPassFilter.enabled = false;
            lowPassFilter.cutoffFrequency = normalCutoff;
        }
    }
}
