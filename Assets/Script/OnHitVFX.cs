using UnityEngine;

public class OnHitVFX : MonoBehaviour
{
    [Header("VFX настройки")]
    public GameObject vfxPrefab;
    public float lifeTime = 2f;

    [Tooltip("С какой секунды начинать анимацию VFX (перемотка)")]
    public float startTimeOffset = 0f;

    /// <summary>
    /// Создаёт VFX в указанной позиции, с возможностью перемотки на нужную секунду.
    /// </summary>
    public void PlayAtPosition(Vector3 position)
    {
        if (vfxPrefab == null)
        {
            Debug.LogWarning($"[OnHitVFX] Нет vfxPrefab на объекте '{name}'");
            return;
        }

        // Корневой объект
        GameObject root = new GameObject("HitVFXRoot");
        root.transform.position = position;

        // Создаём VFX внутри root
        GameObject vfxInstance = Instantiate(vfxPrefab, root.transform);
        vfxInstance.transform.localPosition = Vector3.zero;

        // Перемотка всех particle systems на нужное время
        if (startTimeOffset > 0f)
        {
            ParticleSystem[] allPS = vfxInstance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in allPS)
            {
                ps.Simulate(startTimeOffset, true, true); 
                ps.Play();
            }
        }

        // Уничтожение
        Destroy(root, lifeTime);
    }

    /// <summary>
    /// Старый метод, для совместимости. Использует позицию объекта.
    /// </summary>
    public void Play(Transform target)
    {
        Vector3 pos = target != null ? target.position : transform.position;
        PlayAtPosition(pos);
    }
}
