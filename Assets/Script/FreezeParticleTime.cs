using UnityEngine;

public class FreezeParticleTime : MonoBehaviour
{
    [Header("На какой секунде заморозить VFX")]
    public float freezeTime = 0.1f;

    [Tooltip("Применить заморозку при старте")]
    public bool applyOnStart = true;

    [Tooltip("Если не указано, возьмём ParticleSystem с этого объекта")]
    public ParticleSystem targetSystem;

    private bool applied = false;

    private void Awake()
    {
        if (targetSystem == null)
            targetSystem = GetComponent<ParticleSystem>();
    }

    private void Start()
    {
        if (applyOnStart)
            Apply();
    }

    public void Apply()
    {
        if (applied || targetSystem == null)
            return;

        applied = true;

        // на всякий случай выключим автозапуск
        var main = targetSystem.main;
        main.playOnAwake = false;

        // один раз симулируем до нужной секунды
        targetSystem.Simulate(freezeTime, withChildren: true, restart: true);
        targetSystem.Pause(); // стоп-кадр
    }
}
