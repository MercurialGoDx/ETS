using TMPro;
using UnityEngine;
using UnityEngine.Localization;

/// <summary>
/// Простая привязка строки локализации к TMP_Text: сама обновляет текст
/// при смене языка (LocalizedString.StringChanged). Вешается на объект с TMP_Text,
/// в инспекторе выбирается таблица + ключ. Используется для статических подписей UI
/// (кнопки Журнал/Оружия/Улучшения, заголовки панелей и т.п.).
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class LocalizedTMP : MonoBehaviour
{
    [Tooltip("Таблица + ключ строки локализации")]
    public LocalizedString localizedString = new LocalizedString();

    private TMP_Text label;

    private void Awake()
    {
        label = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        // StringChanged вызывается сразу при подписке и повторно при каждой смене локали.
        localizedString.StringChanged += Apply;
    }

    private void OnDisable()
    {
        localizedString.StringChanged -= Apply;
    }

    private void Apply(string value)
    {
        if (label != null)
            label.text = value;
    }
}
