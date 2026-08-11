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
        ApplyNoWrap();
    }

    /// <summary>Обычная строка: подпись и значение.</summary>
    public void Set(string labelText, string valueText)
    {
        ApplyNoWrap();

        if (label != null) label.text = labelText;
        if (value != null)
        {
            value.gameObject.SetActive(true);
            value.text = valueText;
        }
    }

    /// <summary>Заголовок секции: только подпись, значение скрыто.</summary>
    public void SetHeader(string labelText)
    {
        if (label != null) label.text = labelText;
        if (value != null)
        {
            value.text = "";
            value.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Значение не переносим: длинное число должно вылезать за пределы поля, а не
    /// увеличивать высоту строки — иначе вернётся та же рассинхронизация.
    /// </summary>
    private void ApplyNoWrap()
    {
        if (value == null) return;

        value.textWrappingMode = TextWrappingModes.NoWrap;
        value.overflowMode = TextOverflowModes.Overflow;
    }
}
