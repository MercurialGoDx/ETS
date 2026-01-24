using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

public abstract class UpgradeBaseSO : ScriptableObject
{   
    public Sprite icon;
    public int price;
    public int weight;

    [Header("Название с локализацией")]
    public StringTable localizationTable;
    public string nameKey;
    public string descriptionKey;


    public abstract void Apply(UpgradeContextSO context);

    public virtual string GetLocalizedName()
    {
        return LocalizationSettings.StringDatabase.GetLocalizedString(
            localizationTable.TableCollectionName,
            nameKey
        );
    }

    public virtual string GetLocalizedDescription()
    {
        return LocalizationSettings.StringDatabase.GetLocalizedString(
            localizationTable.TableCollectionName,
            descriptionKey,
            GetDescriptionArgs()
        );
    }

    protected virtual object[] GetDescriptionArgs()
    {
        return null;
    }
}
