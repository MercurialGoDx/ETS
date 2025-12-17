using UnityEngine;

[CreateAssetMenu(menuName = "TD/Enemy Effect")]
public class EnemyEffectDefinition : ScriptableObject
{
    [Header("Визуал эффекта")]
    public GameObject visualPrefab;   // дочерний объект, который вешаем на врага

    [Header("Награда")]
    public int extraGold = 1;         // сколько доп. золота даёт этот эффект
}
