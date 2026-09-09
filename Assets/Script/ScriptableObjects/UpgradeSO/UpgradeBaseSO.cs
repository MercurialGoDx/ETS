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

    /// <summary>
    /// Цена следующей покупки. По умолчанию постоянная; отдельные улучшения
    /// могут переопределить расчёт, не дублируя логику магазина и тултипа.
    /// </summary>
    public virtual int GetCurrentPrice(UpgradeContextSO context) => Mathf.Max(0, price);

    public virtual string GetLocalizedName()
    {
        var stringTable = ResolveTable();
        if (stringTable == null)
            return nameKey;

        var entry = stringTable.GetEntry(nameKey);
        return entry?.GetLocalizedString() ?? nameKey;
    }

    public virtual string GetLocalizedDescription()
    {
        var stringTable = ResolveTable();
        if (stringTable == null)
            return descriptionKey;

        var entry = stringTable.GetEntry(descriptionKey);
        if (entry == null) return descriptionKey;

        return entry.GetLocalizedString(GetDescriptionArgs());
    }

    /// <summary>
    /// Таблица улучшения или null. Ссылка может быть не назначена — ассеты заводят раньше,
    /// чем переводы, — и тогда GetTable() не возвращает null, а кидает ArgumentException.
    /// Раньше это исключение вылетало наружу и роняло отрисовку слота магазина целиком:
    /// предмет просто не появлялся, а консоль забивалась "Empty Table Reference".
    /// </summary>
    private StringTable ResolveTable()
    {
        if (localizedStringTable == null || localizedStringTable.IsEmpty)
        {
            Debug.LogWarning($"[{name}] Не назначена таблица локализации — показываю ключ.");
            return null;
        }

        var table = localizedStringTable.GetTable();
        if (table == null)
            Debug.LogWarning($"[{name}] Таблица локализации не найдена — показываю ключ.");

        return table;
    }

    protected virtual object[] GetDescriptionArgs()
    {
        object[] specificArgs = GetSpecificDescriptionArgs();

        object[] allArgs = new object[specificArgs.Length + 1];
        System.Array.Copy(specificArgs, 0, allArgs, 0, specificArgs.Length);
        UpgradeContextSO context = UpgradesManager.Instance != null
            ? UpgradesManager.Instance.context
            : null;
        allArgs[specificArgs.Length] = GetCurrentPrice(context);

        return allArgs;
    }

    protected abstract object[] GetSpecificDescriptionArgs();

    /// <summary>
    /// Локализованное название типа урона (dtype.&lt;value&gt; из таблицы улучшения). Без этого
    /// {N} в описании рендерился бы английским именем enum независимо от выбранного языка.
    /// </summary>
    protected string GetLocalizedDamageType(WeaponDamageType damageType)
    {
        var stringTable = ResolveTable();
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
