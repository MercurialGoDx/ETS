using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

public abstract class UpgradeBaseSO : ScriptableObject
{   
    public Sprite icon;
    public int price;
    public int weight;

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
