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
}
