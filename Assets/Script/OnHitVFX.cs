using UnityEngine;

public enum HitVfxScaleMode
{
    Original = 0,
    BossMultiplier = 1
}

public class OnHitVFX : MonoBehaviour
{
    [Header("VFX настройки")]
    public GameObject vfxPrefab;
    public float lifeTime = 2f;

    [Tooltip("С какой секунды начинать анимацию VFX (перемотка)")]
    public float startTimeOffset = 0f;

    [Tooltip("Размещать VFX в центре коллайдера врага, а не у его ног. Для старых VFX по умолчанию выключено.")]
    public bool useTargetCenter = false;

    [Header("Масштаб относительно цели")]
    [Tooltip("Original сохраняет исходный размер VFX. Boss Multiplier изменяет размер только при попадании по боссу.")]
    public HitVfxScaleMode scaleMode = HitVfxScaleMode.Original;

    [Min(0.01f)]
    [Tooltip("Множитель размера VFX на боссе. На обычных врагов не влияет.")]
    public float bossScaleMultiplier = 1f;

    /// <summary>
    /// Создаёт VFX в указанной позиции, с возможностью перемотки на нужную секунду.
    /// </summary>
    public void PlayAtPosition(Vector3 position)
    {
        PlayAtPosition(position, null);
    }

    /// <summary>
    /// Создаёт VFX в уже вычисленной позиции и позволяет применить настройки,
    /// зависящие от типа поражённого врага. Позиция передаётся отдельно, потому
    /// что смертельный удар может выключить врага и его коллайдер в тот же кадр.
    /// </summary>
    public void PlayAtPosition(Vector3 position, Enemy targetEnemy)
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

        float targetScaleMultiplier = GetTargetScaleMultiplier(targetEnemy);
        vfxInstance.transform.localScale *= targetScaleMultiplier;

        // Явно запускаем все ParticleSystem: так instant-VFX корректно проигрывается
        // даже у префаба с выключенным Play On Awake.
        ParticleSystem[] allPS = vfxInstance.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in allPS)
        {
            if (startTimeOffset > 0f)
                ps.Simulate(startTimeOffset, true, true); 

            ps.Play(true);
        }

        // Даже зацикленный VFX гарантированно удаляется и не накапливается в сцене.
        Destroy(root, Mathf.Max(0.05f, lifeTime));
    }

    private float GetTargetScaleMultiplier(Enemy targetEnemy)
    {
        if (scaleMode != HitVfxScaleMode.BossMultiplier || targetEnemy == null)
            return 1f;

        bool isBoss = targetEnemy.TryGetComponent(out EnemyStatusEffectController statusEffects) &&
                      statusEffects.IsBoss;

        return isBoss ? Mathf.Max(0.01f, bossScaleMultiplier) : 1f;
    }

    /// <summary>
    /// Старый метод, для совместимости. Использует позицию объекта.
    /// </summary>
    public void Play(Transform target)
    {
        Vector3 pos = transform.position;
        if (target != null)
        {
            Enemy enemy = useTargetCenter ? target.GetComponent<Enemy>() : null;
            pos = enemy != null ? enemy.GetCenterPosition() : target.position;
        }

        PlayAtPosition(pos, target != null ? target.GetComponent<Enemy>() : null);
    }
}
