using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

/// <summary>
/// Выбор языка: кнопка показывает ТЕКУЩИЙ язык, клик открывает список ОСТАЛЬНЫХ доступных
/// языков (текущий исключён). После выбора список перестраивается: выбранный язык исчезает
/// из него, а прежний — возвращается. Строки списка клонируются из itemTemplate
/// (выключенный ребёнок панели).
/// </summary>
public class LanguageSelector : MonoBehaviour
{
    [SerializeField] private Button mainButton;
    [SerializeField] private TMP_Text currentLabel;
    [SerializeField] private GameObject listPanel;
    [SerializeField] private Button itemTemplate;

    private readonly List<GameObject> spawnedItems = new List<GameObject>();
    private bool initialized;

    private void Start()
    {
        if (mainButton != null)
            mainButton.onClick.AddListener(ToggleList);

        if (listPanel != null)
            listPanel.SetActive(false);

        // Локализация инициализируется асинхронно — до готовности список не строим.
        var op = LocalizationSettings.InitializationOperation;
        if (op.IsDone)
            OnLocalizationReady();
        else
            op.Completed += _ => OnLocalizationReady();
    }

    private void OnDestroy()
    {
        if (initialized)
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        if (mainButton != null)
            mainButton.onClick.RemoveListener(ToggleList);
    }

    private void OnLocalizationReady()
    {
        if (this == null) return; // объект мог умереть, пока грузилась локализация

        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        initialized = true;
        Rebuild();
    }

    private void ToggleList()
    {
        if (!initialized || listPanel == null) return;
        listPanel.SetActive(!listPanel.activeSelf);
    }

    // Ловит и внешние смены языка (например, из настроек), чтобы кнопка и список не разошлись.
    private void OnLocaleChanged(Locale _) => Rebuild();

    private void Rebuild()
    {
        Locale current = LocalizationSettings.SelectedLocale;

        if (currentLabel != null && current != null)
            currentLabel.text = current.LocaleName;

        foreach (var item in spawnedItems)
            Destroy(item);
        spawnedItems.Clear();

        if (itemTemplate == null) return;

        foreach (var locale in LocalizationSettings.AvailableLocales.Locales)
        {
            if (locale == current) continue;

            Button item = Instantiate(itemTemplate, itemTemplate.transform.parent);
            item.gameObject.SetActive(true);

            var label = item.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = locale.LocaleName;

            Locale captured = locale;
            item.onClick.AddListener(() => Select(captured));
            spawnedItems.Add(item.gameObject);
        }
    }

    private void Select(Locale locale)
    {
        LocalizationSettings.SelectedLocale = locale;
        if (listPanel != null)
            listPanel.SetActive(false);

        // Перестраиваем сразу, не дожидаясь асинхронного SelectedLocaleChanged:
        // кнопка и список обновляются в тот же кадр.
        Rebuild();
    }
}
