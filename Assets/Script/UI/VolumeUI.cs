using UnityEngine;
using UnityEngine.UI;

public class VolumeUI : MonoBehaviour
{
    [Header("Слайдер в главном меню")]
    [SerializeField] private Slider menuSlider;

    [Header("Слайдер в паузе")]
    [SerializeField] private Slider pauseSlider;

    private bool _isUpdating = false;

    private const string VolumeKey = "master_volume";
    private const float DefaultVolume = 0.5f;

    private void Start()
    {
        float volume = LoadVolume();

        // применяем в аудио сразу
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMasterVolume(volume);

        SetupSlider(menuSlider, volume, OnMenuSliderChanged);
        SetupSlider(pauseSlider, volume, OnPauseSliderChanged);
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
    }

    private void OnMenuSliderChanged(float value)
    {
        if (_isUpdating) return;
        _isUpdating = true;

        // синхронизируем второй без вызова событий
        if (pauseSlider != null)
            pauseSlider.SetValueWithoutNotify(value);

        ApplyAndSave(value);

        _isUpdating = false;
    }

    private void OnPauseSliderChanged(float value)
    {
        if (_isUpdating) return;
        _isUpdating = true;

        // синхронизируем первый без вызова событий
        if (menuSlider != null)
            menuSlider.SetValueWithoutNotify(value);

        ApplyAndSave(value);

        _isUpdating = false;
    }

    private void ApplyAndSave(float value)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMasterVolume(value);

        PlayerPrefs.SetFloat(VolumeKey, value);
        PlayerPrefs.Save();
    }

    private float LoadVolume()
    {
        // Если пользователь никогда не трогал — будет 0.5
        return PlayerPrefs.GetFloat(VolumeKey, DefaultVolume);
    }
}
