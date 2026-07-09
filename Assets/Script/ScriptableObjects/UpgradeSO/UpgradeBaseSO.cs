using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

public abstract class UpgradeBaseSO : ScriptableObject
{
    public Sprite icon;
    public int price;
    public int weight;

    [Header("Тир (для журнала/сортировки)")]
    [Tooltip("Тир улучшения — используется для группировки в журнале по Tier 1..4.")]
    public ItemTier itemTier = ItemTier.None;

    [Header("Weight Scaling")]
    [Tooltip("На сколько увеличивается вес при каждой покупке этого апгрейда")]
    public int weightIncreasePerPurchase = 2;
    [Tooltip("Максимальное количество увеличений веса при покупках")]
    public int maxWeightIncreasePurchases = 5;

    [Header("Weight Modifiers")]
    [Tooltip("Список апгрейдов, вес которых уменьшается при покупке этого апгрейда")]
    public List<UpgradeBaseSO> weightDecreaseUpgrades = new List<UpgradeBaseSO>();
    [Tooltip("Процент уменьшения веса для каждого апгрейда из списка выше (5 = -5% к весу)")]
    public float weightDecreasePercent = 0f;

    [Tooltip("Список апгрейдов, вес которых увеличивается при покупке этого апгрейда")]
    public List<UpgradeBaseSO> weightIncreaseUpgrades = new List<UpgradeBaseSO>();
    [Tooltip("Процент увеличения веса для каждого апгрейда из списка выше (7 = +7% к весу)")]
    public float weightIncreasePercent = 0f;

    [Tooltip("Список оружий, вес которых уменьшается при покупке этого апгрейда")]
    public List<WeaponDefinition> weightDecreaseWeapons = new List<WeaponDefinition>();
    [Tooltip("Процент уменьшения веса для каждого оружия из списка выше (5 = -5% к весу)")]
    public float weightDecreaseWeaponPercent = 0f;

    [Tooltip("Список оружий, вес которых увеличивается при покупке этого апгрейда")]
    public List<WeaponDefinition> weightIncreaseWeapons = new List<WeaponDefinition>();
    [Tooltip("Процент увеличения веса для каждого оружия из списка выше (7 = +7% к весу)")]
    public float weightIncreaseWeaponPercent = 0f;

    [Header("Localization")]
    public LocalizedStringTable localizedStringTable;
    public string nameKey;
    public string descriptionKey;


    public abstract void Apply(UpgradeContextSO context);

    /// <summary>
    /// Можно ли сейчас купить/применить это улучшение. По умолчанию — да.
    /// Переопределяется там, где есть условие (например, недостаточно здоровья
    /// для "золото за жизнь"). Магазин проверяет это ДО списания цены.
    /// </summary>
    public virtual bool CanApply(UpgradeContextSO context) => true;

    public virtual string GetLocalizedName()
    {
        var stringTable = localizedStringTable.GetTable();
        if (stringTable == null)
        {
            Debug.LogError($"Localization table not found for upgrade: {name}");
            return nameKey;
        }

        var entry = stringTable.GetEntry(nameKey);
        return entry?.GetLocalizedString() ?? nameKey;
    }

    public virtual string GetLocalizedDescription()
    {
        var stringTable = localizedStringTable.GetTable();
        if (stringTable == null)
        {
            Debug.LogError($"Localization table not found for upgrade: {name}");
            return descriptionKey;
        }

        var entry = stringTable.GetEntry(descriptionKey);
        if (entry == null) return descriptionKey;

        return entry.GetLocalizedString(GetDescriptionArgs());
    }

    protected virtual object[] GetDescriptionArgs()
    {
        object[] specificArgs = GetSpecificDescriptionArgs();

        object[] allArgs = new object[specificArgs.Length + 1];
        System.Array.Copy(specificArgs, 0, allArgs, 0, specificArgs.Length);
        allArgs[specificArgs.Length] = price;

        return allArgs;
    }

    protected abstract object[] GetSpecificDescriptionArgs();

    /// <summary>
    /// Локализованное название типа урона (dtype.&lt;value&gt; из таблицы улучшения). Без этого
    /// {N} в описании рендерился бы английским именем enum независимо от выбранного языка.
    /// </summary>
    protected string GetLocalizedDamageType(WeaponDamageType damageType)
    {
        var stringTable = localizedStringTable.GetTable();
        if (stringTable != null)
        {
            var entry = stringTable.GetEntry("dtype." + damageType.ToString().ToLowerInvariant());
            if (entry != null)
            {
                string localized = entry.GetLocalizedString();
                if (!string.IsNullOrEmpty(localized))
                    return localized;
            }
        }
        return damageType.ToString();
    }
}
