using UnityEngine;
using UnityEngine.UI;

public class VolumeUI : MonoBehaviour
{
    [Header("Слайдер в главном меню")]
    [SerializeField] private Slider menuSlider;

    [Header("Слайдер в паузе")]
    [SerializeField] private Slider pauseSlider;
    [SerializeField] private Slider pauseSoundSlider;

    private bool _isUpdatingMusic = false;

    private const string LegacyVolumeKey = "master_volume";
    private const string MusicVolumeKey = "music_volume";
    private const string SoundVolumeKey = "sound_volume";
    private const float DefaultVolume = 0.5f;

    private void Start()
    {
        float musicVolume = LoadMusicVolume();
        float soundVolume = PlayerPrefs.GetFloat(SoundVolumeKey, DefaultVolume);

        // применяем в аудио сразу
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMusicVolume(musicVolume);
            AudioManager.Instance.SetSoundVolume(soundVolume);
        }

        SetupSlider(menuSlider, musicVolume, OnMenuSliderChanged);
        SetupSlider(pauseSlider, musicVolume, OnPauseSliderChanged);
        SetupSlider(pauseSoundSlider, soundVolume, OnPauseSoundSliderChanged);
    }

    private void SetupSlider(Slider slider, float value, UnityEngine.Events.UnityAction<float> callback)
    {
        if (slider == null) return;

        slider.minValue = 0f;
        slider.maxValue = 1f;

        // чтобы при выставлении значения не дергались события
        slider.SetValueWithoutNotify(value);

        slider.onValueChanged.AddListener(callback);
    }

    private void OnDestroy()
    {
        if (menuSlider != null)
            menuSlider.onValueChanged.RemoveListener(OnMenuSliderChanged);

        if (pauseSlider != null)
            pauseSlider.onValueChanged.RemoveListener(OnPauseSliderChanged);

        if (pauseSoundSlider != null)
            pauseSoundSlider.onValueChanged.RemoveListener(OnPauseSoundSliderChanged);
    }

    private void OnMenuSliderChanged(float value)
    {
        if (_isUpdatingMusic) return;
        _isUpdatingMusic = true;

        // синхронизируем второй без вызова событий
        if (pauseSlider != null)
            pauseSlider.SetValueWithoutNotify(value);

        ApplyMusicAndSave(value);

        _isUpdatingMusic = false;
    }

    private void OnPauseSliderChanged(float value)
    {
        if (_isUpdatingMusic) return;
        _isUpdatingMusic = true;

        // синхронизируем первый без вызова событий
        if (menuSlider != null)
            menuSlider.SetValueWithoutNotify(value);

        ApplyMusicAndSave(value);

        _isUpdatingMusic = false;
    }

    private void OnPauseSoundSliderChanged(float value)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetSoundVolume(value);

        PlayerPrefs.SetFloat(SoundVolumeKey, value);
        PlayerPrefs.Save();
    }

    private void ApplyMusicAndSave(float value)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMusicVolume(value);

        PlayerPrefs.SetFloat(MusicVolumeKey, value);
        PlayerPrefs.Save();
    }

    private float LoadMusicVolume()
    {
        if (PlayerPrefs.HasKey(MusicVolumeKey))
            return PlayerPrefs.GetFloat(MusicVolumeKey, DefaultVolume);

        return PlayerPrefs.GetFloat(LegacyVolumeKey, DefaultVolume);
    }
}
