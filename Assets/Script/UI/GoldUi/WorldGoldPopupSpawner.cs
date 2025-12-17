using UnityEngine;

public class WorldGoldPopupSpawner : MonoBehaviour
{
    public GoldManager goldManager;
    public WorldGoldPopup popupPrefab;
    public Vector3 offset = new Vector3(0f, 0.2f, 0f);

    private void OnEnable()
    {
        if (goldManager != null)
            goldManager.OnGoldGained += OnGoldGained;
    }

    private void OnDisable()
    {
        if (goldManager != null)
            goldManager.OnGoldGained -= OnGoldGained;
    }

    private void OnGoldGained(int amount, GoldSource source, Vector3? worldPos)
    {
        if (source != GoldSource.Kill) return;
        if (amount <= 0) return;
        if (worldPos == null) return;

        WorldGoldPopup p = Instantiate(popupPrefab, worldPos.Value + offset, Quaternion.identity);
        p.Init(amount);
    }
}
