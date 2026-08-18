using UnityEngine;

public class RegenAuraDamage : MonoBehaviour
{
    [Header("Глобальный урон по всем врагам от регена")]
    [Tooltip("Включить/выключить ауру урона от регена (включится при покупке апгрейда).")]
    public bool regenAuraEnabled = false;

    [Tooltip("Множитель урона от суммарного регена (2 = урон в 2 раза больше регена).")]
    public float regenAuraMultiplier = 0f;

    [Tooltip("Интервал между тиками урона по всем врагам (секунды).")]
    public float regenAuraTickInterval = 1f;

    private float regenAuraTimer = 0f;

    private void Update()
    {
        HandleRegenAuraDamage();
    }

    private void HandleRegenAuraDamage()
    {
        if (!regenAuraEnabled)
            return;

        if (regenAuraMultiplier <= 0f)
            return;

        if (UpgradesManager.Instance.playerHealth == null)
            return;

        regenAuraTimer += Time.deltaTime;
        if (regenAuraTimer < regenAuraTickInterval)
            return;

        regenAuraTimer = 0f;
        ApplyRegenAuraDamage();
    }

    private void ApplyRegenAuraDamage()
    {
        var upgrades = UpgradesManager.Instance;
        var playerHealth = upgrades.playerHealth;
        var playerShield = upgrades.playerShield;
        var runtime = upgrades.context != null ? upgrades.context.runtime : null;

        // 1) Use the same final regeneration value as healing and the stats UI.
        float totalRegen = playerHealth.GetTotalRegen();
        if (totalRegen <= 0f)
            return;

        // 2) Урон ауры = реген * множитель ауры, усиленный только adaptive-бонусом
        // (урон при активном щите). Остальные бонусы урона (тип, генератор, глобальные)
        // на эту ауру намеренно не действуют.
        float baseDamagePerEnemy = totalRegen * regenAuraMultiplier;
        float adaptiveBonus = runtime != null && playerShield != null && playerShield.IsShieldActive
            ? runtime.adaptiveDamageWhileShieldPercent
            : 0f;
        float damagePerEnemy = baseDamagePerEnemy * (1f + adaptiveBonus);

        // 3) Наносим урон всем врагам на сцене
        // Хранить всех доступных врагов в одном листе
        Enemy[] enemies = Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None);

        if (enemies.Length == 0)
            return;

        foreach (var e in enemies)
        {
            if (e == null) continue;
            e.TakeDamage(damagePerEnemy);
        }

        Debug.Log(
            $"[RegenAura] Tick: regen={totalRegen:F1}, mult={regenAuraMultiplier:F2}, " +
            $"base={baseDamagePerEnemy:F1}, adaptive=x{1f + adaptiveBonus:F3}, " +
            $"final={damagePerEnemy:F1}, enemies={enemies.Length}");
    }
}

