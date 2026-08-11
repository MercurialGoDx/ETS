using UnityEngine;

/// <summary>
/// Единственная точка доступа к балансу в рантайме. Загружает BalanceConfig
/// из Resources один раз ДО Awake любых объектов сцены (BeforeSceneLoad),
/// поэтому потребители могут читать Config прямо в своих Awake.
/// Если конфига нет (импорт ещё не запускали) — Config == null и все
/// потребители остаются на значениях из инспектора.
/// </summary>
public static class BalanceService
{
    public static BalanceConfig Config { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Load()
    {
        Config = Resources.Load<BalanceConfig>(BalanceConfig.ResourcesPath);

        if (Config == null)
            Debug.LogWarning("[Balance] BalanceConfig не найден в Resources/Balance — игра на значениях из инспектора. Запусти Tools → Balance → Import.");
    }
}
