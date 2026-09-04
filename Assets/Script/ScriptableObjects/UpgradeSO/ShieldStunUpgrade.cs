using UnityEngine;

[CreateAssetMenu(menuName = "Upgrades/Shield/Shield Stun")]
public sealed class ShieldStunUpgrade : UpgradeBaseSO
{
    [Header("Global Stun")]
    [Min(0f)]
    [Tooltip("Длительность глобального стана после первой покупки.")]
    public float firstStunDuration = 2f;

    [Min(0f)]
    [Tooltip("Сколько секунд добавляет каждая покупка после первой.")]
    public float additionalDurationPerStack = 1f;

    [Header("Damage Wave")]
    [Min(0f)]
    [Tooltip("Множитель MaxShield, добавляемый за каждую покупку. 5 даёт x5, x10, x15...")]
    public float damageMultiplierPerStack = 5f;

    [Min(0.01f)]
    [Tooltip("Скорость расширения наносящего урон фронта в мировых юнитах за секунду.")]
    public float damageWaveSpeed = 4f;

    [Header("Tower Visual")]
    [Tooltip("Необязательный префаб импульса, создаваемый на башне при разрушении щита.")]
    public GameObject towerVisualPrefab;

    [Min(0.01f)]
    [Tooltip("Через сколько секунд уничтожить визуал и остановить наносящую урон волну.")]
    public float visualLifetime = 5f;

    [Tooltip("Насколько поднять визуал импульса относительно башни.")]
    public float visualHeightOffset = 0.25f;

    public override void Apply(UpgradeContextSO context)
    {
        if (context?.playerShield == null)
        {
            Debug.LogWarning($"{name}: PlayerShield is not available.");
            return;
        }

        context.playerShield.AddShieldStun(
            firstStunDuration,
            additionalDurationPerStack,
            damageMultiplierPerStack,
            damageWaveSpeed,
            this,
            towerVisualPrefab,
            visualLifetime,
            visualHeightOffset);

        Debug.Log(
            $"[ShieldStun] Applied: stacks={context.playerShield.ShieldStunStacks}, " +
            $"duration={context.playerShield.ShieldStunDuration:0.###}s, " +
            $"damage multiplier=x{context.playerShield.ShieldStunDamageMultiplier:0.###}.");
    }

    protected override object[] GetSpecificDescriptionArgs()
    {
        PlayerShield playerShield = UpgradesManager.Instance?.playerShield;
        float nextDuration = playerShield != null
            ? playerShield.GetShieldStunDurationAfterNextStack(
                firstStunDuration,
                additionalDurationPerStack)
            : Mathf.Max(0f, firstStunDuration);
        float nextDamageMultiplier = playerShield != null
            ? playerShield.GetShieldStunDamageMultiplierAfterNextStack(
                damageMultiplierPerStack)
            : Mathf.Max(0f, damageMultiplierPerStack);

        return new object[] { nextDuration, nextDamageMultiplier };
    }
}
