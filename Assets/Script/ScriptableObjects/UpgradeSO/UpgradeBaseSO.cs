using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

public abstract class UpgradeBaseSO : ScriptableObject
{   
    public Sprite icon;
    public int price;
    public int weight;

    [Header("Название с локализацией")]
    public LocalizedStringTable localizedStringTable;
    public string nameKey;
    public string descriptionKey;


    public abstract void Apply(UpgradeContextSO context);

    public virtual string GetLocalizedName()
    {
        // Получаем таблицу для текущей локали
        var stringTable = localizedStringTable.GetTable();
        if (stringTable == null)
        {
            Debug.LogError($"Localization table not found for upgrade: {name}");
            return nameKey;
        }

        // Получаем строку по ключу
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
        // Получаем специфические аргументы от дочернего класса
        object[] specificArgs = GetSpecificDescriptionArgs();

        // Создаем общий массив: специфические аргументы + цена
        object[] allArgs = new object[specificArgs.Length + 1];
        System.Array.Copy(specificArgs, 0, allArgs, 0, specificArgs.Length);
        allArgs[specificArgs.Length] = price;

        return allArgs;
    }

    // Абстрактный метод, который должны реализовать дочерние классы
    protected abstract object[] GetSpecificDescriptionArgs();
}
