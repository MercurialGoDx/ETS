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

        // 1) Считаем общий реген игрока в секунду
        float baseRegen = playerHealth.healthRegenPerSecond; // базовый реген из апгрейдов

        float bonusRegen = 0f;
        // если мы делали улучшение "реген за недостающее здоровье"
        if (playerHealth.regenPer100MissingHealth > 0f)
        {
            float missing = playerHealth.MaxHealth - playerHealth.CurrentHealth;
            if (missing > 0f)
            {
                bonusRegen = playerHealth.regenPer100MissingHealth * (missing / 100f);
            }
        }

        float totalRegen = baseRegen + bonusRegen;
        if (totalRegen <= 0f)
            return;

        // 2) Считаем базовый урон от ауры, а потом усиливаем только разрешёнными бонусами.
        float baseDamagePerEnemy = totalRegen * regenAuraMultiplier;
        float globalBonus = runtime != null ? runtime.globalDamagePercent : 0f;
        float generatorBonus = runtime != null ? runtime.totalGeneratorDamagePercent : 0f;
        float shieldBonus = runtime != null && playerShield != null && playerShield.IsShieldActive
            ? runtime.damageWhileShieldActivePercent
            : 0f;
        float totalAllowedBonus = globalBonus + generatorBonus + shieldBonus;
        float damagePerEnemy = baseDamagePerEnemy * (1f + totalAllowedBonus);

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
            $"base={baseDamagePerEnemy:F1}, global={globalBonus * 100f:F1}%, " +
            $"generator={generatorBonus * 100f:F1}%, shield={shieldBonus * 100f:F1}%, " +
            $"final={damagePerEnemy:F1}, enemies={enemies.Length}");
    }
}

