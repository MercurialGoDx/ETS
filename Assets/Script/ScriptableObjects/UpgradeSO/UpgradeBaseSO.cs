using UnityEngine;

public abstract class UpgradeBaseSO : ScriptableObject
{
    public string upgradeName;
    public Sprite icon;
    public int price;
    public int weight;

    [Header("Описание")]
    [TextArea]
    public string description;

    public abstract void Apply(UpgradeContextSO context);
}
