using TMPro;
using UnityEngine;

/// <summary>
/// Одна строка панели характеристик: подпись слева, значение справа.
/// Раньше подписи и значения были двумя отдельными TMP-колонками, и стоило значению
/// перенестись («22 190/22 190»), как вся колонка значений съезжала на строку вниз
/// и переставала соответствовать подписям. Здесь строка — один объект, поэтому
/// рассинхрон невозможен по построению.
/// </summary>
public class StatRow : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private TMP_Text value;

    private void Awake()
    {
        ApplyTextModes();
    }

    /// <summary>Обычная строка: подпись и значение.</summary>
    public void Set(string labelText, string valueText)
    {
        ApplyTextModes();

        if (label != null) label.text = labelText;
        if (value != null)
        {
            value.gameObject.SetActive(true);
            value.text = valueText;
        }
    }

    /// <summary>
    /// Ни подпись, ни значение не переносим: длинный текст должен вылезать за пределы
    /// поля, а не увеличивать высоту строки — иначе вернётся та же рассинхронизация.
    ///
    /// Режим строго Overflow. С Ellipsis внутри HorizontalLayoutGroup TMP вычисляет
    /// ширину до того, как её выставит лэйаут, считает, что не влезает ничего, и
    /// отрисовывает ноль символов — подпись просто исчезает. Выставляем из кода,
    /// чтобы это нельзя было случайно вернуть настройкой префаба.
    /// </summary>
    private void ApplyTextModes()
    {
        if (label != null)
        {
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
        }

        if (value != null)
        {
            value.textWrappingMode = TextWrappingModes.NoWrap;
            value.overflowMode = TextOverflowModes.Overflow;
        }
    }
}
