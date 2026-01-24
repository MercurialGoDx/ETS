using System.Collections.Generic;
using System.Text;
using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Composite")]
public class CompositeUpgrade : UpgradeBaseSO
{
    public List<UpgradeBaseSO> upgrades;

    public override void Apply(UpgradeContextSO context)
    {
        foreach (var upgrade in upgrades)
        {
            try
            {
                if (upgrade != null)
                    upgrade.Apply(context);
            }
            catch (System.NullReferenceException e)
            {
                Debug.LogWarning($"Ошибка в апгрейде {upgrade?.name}: {e.Message}");
                // Продолжаем выполнение остальных апгрейдов
            }
        }
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        return new object[0];
    }
}