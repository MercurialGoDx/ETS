using UnityEngine;

public class WorldDamagePopupSpawner : MonoBehaviour
{
    [SerializeField] private WorldDamagePopup popupPrefab;
    [SerializeField] private Vector3 offset;

    private void OnEnable()
    {
        Enemy.WeaponDamageTaken += OnWeaponDamageTaken;
    }

    private void OnDisable()
    {
        Enemy.WeaponDamageTaken -= OnWeaponDamageTaken;
    }

    private void OnWeaponDamageTaken(
        float amount,
        WeaponDamageType damageType,
        Vector3 worldPosition)
    {
        if (popupPrefab == null || PoolManager.Instance == null)
            return;

        GameObject popupObject = PoolManager.Instance.Spawn(
            popupPrefab.gameObject,
            worldPosition + offset,
            Quaternion.identity);
        WorldDamagePopup popup = popupObject != null
            ? popupObject.GetComponent<WorldDamagePopup>()
            : null;
        popup?.Init(amount, damageType);
    }
}
