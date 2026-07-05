using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using TMPro;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// Динамический выбор языка. Кнопка показывает ТЕКУЩИЙ язык, а раскрывающийся список —
/// только ОСТАЛЬНЫЕ доступные языки (текущий из списка исключён). После выбора список
/// перестраивается, поэтому любой язык всегда остаётся достижимым.
/// </summary>
public class LanguageDropdown : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown dropdown; // Для TextMeshPro Dropdown

    [Header("Размер раскрытого списка")]
    [Tooltip("Высота одной строки списка (px). Должна быть соразмерна большой кнопке.")]
    [SerializeField] private float itemHeight = 100f;
    [Tooltip("Максимум строк без прокрутки")]
    [SerializeField] private int maxVisibleItems = 6;
    [Tooltip("Мин/макс размер шрифта строки (авторазмер, чтобы длинные названия влезали в одну строку)")]
    [SerializeField] private float itemFontMin = 20f;
    [SerializeField] private float itemFontMax = 52f;
    [Tooltip("Отступ фона списка сверху и снизу (px) — одинаковый с обеих сторон для красоты")]
    [SerializeField] private float itemPadding = 16f;

    private List<Locale> availableLocales;
    // Языки, доступные для выбора СЕЙЧАС (все, кроме текущего) — индексы совпадают с опциями дропдауна.
    private readonly List<Locale> selectableLocales = new List<Locale>();
    private bool isInitialized = false;

    private void Start()
    {
        // Если локализация уже инициализирована — настраиваем сразу, иначе ждём завершения.
        var op = LocalizationSettings.InitializationOperation;
        if (op.IsDone)
            OnLocalizationInitialized(op);
        else
            op.Completed += OnLocalizationInitialized;
    }

    private void OnLocalizationInitialized(UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<LocalizationSettings> handle)
    {
        availableLocales = LocalizationSettings.AvailableLocales.Locales;

        if (dropdown != null)
            dropdown.onValueChanged.AddListener(OnDropdownValueChanged);

        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;

        RefreshOptions();
        isInitialized = true;
    }

    /// <summary>
    /// Перестраивает список опций: все языки, КРОМЕ текущего. Кнопка показывает текущий язык.
    /// </summary>
    private void RefreshOptions()
    {
        if (dropdown == null || availableLocales == null) return;

        Locale current = LocalizationSettings.SelectedLocale;

        selectableLocales.Clear();
        var options = new List<TMP_Dropdown.OptionData>();
        foreach (var locale in availableLocales)
        {
            if (locale == current) continue; // текущий язык в список не попадает
            selectableLocales.Add(locale);
            options.Add(new TMP_Dropdown.OptionData(locale.LocaleName));
        }

        // Пересобираем опции без побочного вызова обработчика.
        dropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
        dropdown.ClearOptions();
        dropdown.AddOptions(options);
        ApplyDropdownSizing();

        // Кнопка (caption) показывает текущий язык, которого нет в списке.
        if (dropdown.captionText != null && current != null)
            dropdown.captionText.text = current.LocaleName;

        // Сбрасываем внутреннее значение в -1, чтобы выбор ЛЮБОГО пункта (включая первый) срабатывал
        // (иначе клик по пункту с текущим index не вызвал бы onValueChanged).
        SetInternalValue(-1);

        dropdown.onValueChanged.AddListener(OnDropdownValueChanged);
    }

    // Приводит раскрывающийся список к удобному размеру: высота строки, шрифт, симметричные отступы.
    // TMP строит фон списка как: высота = itemHeight*count + (высота Content шаблона − высота Item).
    // Поэтому задаём Content = itemHeight + 2*padding и центрируем Item → сверху и снизу ровно padding,
    // а фон вмещает все пункты (TMP не обрезает список по экрану, только флипает вверх/вниз).
    //
    // ВАЖНО: объект дропдауна может иметь масштаб != 1 (кнопка Languages уменьшена localScale=0.28),
    // а список — его потомок и наследует масштаб. Чтобы размеры (itemHeight/padding/шрифт) читались как
    // размеры В ЕДИНИЦАХ CANVAS (интуитивно, независимо от масштаба кнопки), переводим их в локальные,
    // деля на относительный масштаб дропдауна к канвасу.
    private void ApplyDropdownSizing()
    {
        if (dropdown == null || dropdown.template == null || dropdown.itemText == null) return;

        RectTransform itemRt = dropdown.itemText.rectTransform.parent as RectTransform; // "Item"
        if (itemRt == null) return;
        RectTransform contentRt = itemRt.parent as RectTransform;                        // "Content"

        // Относительный масштаб «локальная единица списка → единица canvas».
        float rel = 1f;
        var canvas = dropdown.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            float cs = canvas.transform.lossyScale.y;
            float ds = dropdown.transform.lossyScale.y;
            if (cs > 0.0001f) rel = ds / cs;
        }
        if (rel < 0.0001f) rel = 1f;

        float localItem = itemHeight / rel;
        float localPad = itemPadding / rel;

        // Высота строки + авторазмер шрифта (тоже в локальных единицах текста).
        // Перенос выключаем: иначе длинные названия («Portuguese (pt)») ломаются по слогам;
        // без переноса авторазмер сам ужимает текст в одну строку.
        itemRt.sizeDelta = new Vector2(itemRt.sizeDelta.x, localItem);
        dropdown.itemText.enableAutoSizing = true;
        dropdown.itemText.enableWordWrapping = false;
        dropdown.itemText.fontSizeMin = itemFontMin / rel;
        dropdown.itemText.fontSizeMax = itemFontMax / rel;

        // Центрируем шаблонный Item в Content: отступ padding сверху и снизу.
        itemRt.anchorMin = new Vector2(itemRt.anchorMin.x, 1f);
        itemRt.anchorMax = new Vector2(itemRt.anchorMax.x, 1f);
        itemRt.pivot = new Vector2(itemRt.pivot.x, 1f);
        itemRt.anchoredPosition = new Vector2(itemRt.anchoredPosition.x, -localPad);

        if (contentRt != null)
        {
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = new Vector2(contentRt.sizeDelta.x, localItem + 2f * localPad);
        }

        // Высота фона/скролл-области под все пункты + отступы (TMP при показе ужимает лишнее до Content).
        int count = Mathf.Max(1, dropdown.options.Count);
        int visible = Mathf.Min(count, Mathf.Max(1, maxVisibleItems));
        float bg = visible * localItem + 2f * localPad;
        dropdown.template.sizeDelta = new Vector2(dropdown.template.sizeDelta.x, bg);
    }

    // Задаёт внутреннее value дропдауна в обход клампа (public-сеттер не пускает -1).
    private void SetInternalValue(int value)
    {
        var f = typeof(TMP_Dropdown).GetField("m_Value", BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null) f.SetValue(dropdown, value);
    }

    private void OnDropdownValueChanged(int index)
    {
        if (index < 0 || index >= selectableLocales.Count) return;

        LocalizationSettings.SelectedLocale = selectableLocales[index];

        // Перестраиваем список сразу (не ждём асинхронного SelectedLocaleChanged), чтобы
        // выбранный язык тут же исчез из списка, а прежний — вернулся.
        RefreshOptions();
    }

    private void OnLocaleChanged(Locale newLocale)
    {
        if (isInitialized || availableLocales != null)
            RefreshOptions();
    }

    private void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        if (dropdown != null)
            dropdown.onValueChanged.RemoveListener(OnDropdownValueChanged);
    }
}
