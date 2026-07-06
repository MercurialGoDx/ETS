using TMPro;
using UnityEngine;

/// <summary>
/// Держит одинаковый размер шрифта у группы TMP-текстов (кнопки одной панели).
/// Каждому тексту через авторазмер вычисляется его оптимальный размер (в границах
/// fontSizeMin..fontSizeMax самого текста), затем всем присваивается МИНИМАЛЬНЫЙ из них —
/// самая длинная надпись задаёт размер всем. Пересчёт происходит автоматически, когда
/// у любого участника меняется текст (в т.ч. при смене локали LocalizedTMP обновляет
/// строки асинхронно — сравнение в LateUpdate ловит это без подписок на события).
/// </summary>
public class UniformTextSizeGroup : MonoBehaviour
{
    [Tooltip("Тексты одной панели, у которых размер шрифта должен совпадать")]
    [SerializeField] private TMP_Text[] labels;

    private string[] lastTexts;

    private void OnEnable()
    {
        lastTexts = new string[labels != null ? labels.Length : 0];
        // форсируем первый пересчёт
        for (int i = 0; i < lastTexts.Length; i++)
            lastTexts[i] = null;
    }

    private void LateUpdate()
    {
        if (labels == null || labels.Length == 0) return;

        bool changed = false;
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] == null || !labels[i].gameObject.activeInHierarchy) continue;
            if (labels[i].text != lastTexts[i])
            {
                lastTexts[i] = labels[i].text;
                changed = true;
            }
        }

        if (changed)
            Sync();
    }

    private void Sync()
    {
        float best = float.MaxValue;

        // 1) даём каждому тексту найти свой оптимальный размер
        foreach (var label in labels)
        {
            if (label == null || !label.gameObject.activeInHierarchy) continue;
            label.enableAutoSizing = true;
            label.ForceMeshUpdate();
            if (label.fontSize < best)
                best = label.fontSize;
        }

        if (best >= float.MaxValue) return;

        // 2) всем — одинаковый (минимальный найденный)
        foreach (var label in labels)
        {
            if (label == null || !label.gameObject.activeInHierarchy) continue;
            label.enableAutoSizing = false;
            label.fontSize = best;
        }
    }
}
