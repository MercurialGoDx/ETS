using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Composite")]
public class CompositeUpgrade : UpgradeBaseSO
{
    public List<UpgradeBaseSO> upgrades;

    // Список композита собирается руками в инспекторе, и туда уже попадала ссылка на
    // сам ассет: Apply уходил в бесконечную рекурсию и ронял процесс StackOverflow'ом,
    // который перехватить нельзя — падает весь редактор. Помним, какие композиты сейчас
    // применяются, и не входим в них повторно. Ловит и косвенные циклы (A → B → A).
    private static readonly HashSet<CompositeUpgrade> applying = new HashSet<CompositeUpgrade>();

    public override void Apply(UpgradeContextSO context)
    {
        if (upgrades == null)
            return;

        if (!applying.Add(this))
        {
            Debug.LogError(
                $"Композит «{name}» ссылается сам на себя (напрямую или через цепочку). " +
                "Вложенное применение пропущено — проверь список upgrades в инспекторе.");
            return;
        }

        try
        {
            foreach (var upgrade in upgrades)
            {
                if (upgrade == null)
                    continue;

                try
                {
                    upgrade.Apply(context);
                }
                catch (System.NullReferenceException e)
                {
                    Debug.LogWarning($"Ошибка в апгрейде {upgrade.name}: {e.Message}");
                    // Продолжаем применять остальные элементы композита.
                }
            }
        }
        finally
        {
            // finally, а не в конце тела: иначе исключение внутри навсегда оставит
            // композит «в процессе применения», и он перестанет работать до перезапуска.
            applying.Remove(this);
        }
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[0];
    }
}
