using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WeaponStatItemUI : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text weaponName;
    [SerializeField] private TMP_Text damageText;

    public void Setup(WeaponDefinition weapon, float damage)
    {
        icon.sprite = weapon.icon;
        weaponName.text = weapon.GetLocalizedName();
        damageText.text = damage.ToString("F0");
    }
}