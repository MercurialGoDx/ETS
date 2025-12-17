using UnityEngine;
using UnityEngine.UI;

public class VolumeUIManager : MonoBehaviour
{
    [Header("Слайдер в главном меню")]
    [SerializeField] private Slider menuSlider;

    [Header("Слайдер в паузе")]
    [SerializeField] private Slider pauseSlider;

    private bool _isUpdating = false;

    private void Start()
    {
        float volume = 1f;

        if (AudioManager.Instance != null)
        {
            volume = AudioManager.Instance.GetMasterVolume();
        }

        if (menuSlider != null)
        {
            menuSlider.minValue = 0f;
            menuSlider.maxValue = 1f;
            menuSlider.value = volume;
            menuSlider.onValueChanged.AddListener(OnMenuSliderChanged);
        }

        if (pauseSlider != null)
        {
            pauseSlider.minValue = 0f;
            pauseSlider.maxValue = 1f;
            pauseSlider.value = volume;
            pauseSlider.onValueChanged.AddListener(OnPauseSliderChanged);
        }
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

        // синхронизируем второй
        if (pauseSlider != null)
            pauseSlider.value = value;

        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMasterVolume(value);

        _isUpdating = false;
    }

    private void OnPauseSliderChanged(float value)
    {
        if (_isUpdating) return;
        _isUpdating = true;

        // синхронизируем первый
        if (menuSlider != null)
            menuSlider.value = value;

        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMasterVolume(value);

        _isUpdating = false;
    }
}
