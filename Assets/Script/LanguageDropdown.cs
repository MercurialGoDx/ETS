using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using TMPro;
using System.Collections.Generic;

public class LanguageDropdown : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown dropdown; // Для TextMeshPro Dropdown
    // [SerializeField] private UnityEngine.UI.Dropdown dropdown; // Для обычного Dropdown

    private List<Locale> availableLocales;
    private bool isInitialized = false;

    private void Start()
    {
        // Ждём инициализации локализации
        LocalizationSettings.InitializationOperation.Completed += OnLocalizationInitialized;
    }

    private void OnLocalizationInitialized(UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<LocalizationSettings> handle)
    {
        // Заполняем список доступных языков
        availableLocales = LocalizationSettings.AvailableLocales.Locales;

        // Настраиваем Dropdown
        SetupDropdown();

        // Подписываемся на смену языка
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;

        isInitialized = true;
    }

    private void SetupDropdown()
    {
        if (dropdown == null) return;

        // Очищаем старые опции
        dropdown.ClearOptions();

        // Создаём список опций для Dropdown
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();

        foreach (var locale in availableLocales)
        {
            // Получаем название языка на его родном языке (например, "English", "Русский")
            string languageName = locale.LocaleName;

            // Или можешь использовать LocalizedString, чтобы показывать названия на текущем языке
            // string languageName = GetLocalizedLanguageName(locale);

            options.Add(new TMP_Dropdown.OptionData(languageName));
        }

        dropdown.AddOptions(options);

        // Устанавливаем текущий выбранный язык
        SetDropdownToCurrentLocale();

        // Подписываемся на выбор в Dropdown
        dropdown.onValueChanged.AddListener(OnDropdownValueChanged);
    }

    private void SetDropdownToCurrentLocale()
    {
        if (dropdown == null) return;

        Locale currentLocale = LocalizationSettings.SelectedLocale;

        for (int i = 0; i < availableLocales.Count; i++)
        {
            if (availableLocales[i] == currentLocale)
            {
                dropdown.SetValueWithoutNotify(i);
                break;
            }
        }
    }

    private void OnDropdownValueChanged(int index)
    {
        if (index < 0 || index >= availableLocales.Count) return;

        Locale selectedLocale = availableLocales[index];
        LocalizationSettings.SelectedLocale = selectedLocale;
    }

    private void OnLocaleChanged(Locale newLocale)
    {
        // Обновляем Dropdown, если язык изменился из другого места
        if (dropdown != null && isInitialized)
        {
            SetDropdownToCurrentLocale();
        }
    }

    private void OnDestroy()
    {
        //if (LocalizationSettings.SelectedLocaleChanged != null)
        //    LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;

        if (dropdown != null && isInitialized)
            dropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
    }
}