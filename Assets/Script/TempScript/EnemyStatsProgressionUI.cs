using TMPro;
using UnityEngine;

public class EnemyStatsProgressionUI : MonoBehaviour
{
    [Header("Links")]
    [SerializeField] private EnemySpawner spawner;

    [Header("UI")]
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text dmgText;
    [SerializeField] private TMP_Text waveText; // можно оставить пустым

    [Header("Base enemy stats for UI")]
    [SerializeField] private float baseHp = 15f;
    [SerializeField] private float baseDmg = 2f;

    private bool isStarted = false;

    /// <summary>
    /// Вызывается из GameStartController по кнопке Ready.
    /// Теперь не запускает корутину, а включает авто-обновление от EnemySpawner.
    /// </summary>
    public void StartProgression()
    {
        isStarted = true;

        if (spawner == null)
        {
            Debug.LogWarning("[EnemyStatsProgressionUI] Spawner is not assigned!");
            return;
        }

        // Подписка (на всякий случай без дублей)
        spawner.OnWaveSpawned -= HandleWaveSpawned;
        spawner.OnWaveSpawned += HandleWaveSpawned;

        // Сразу покажем актуальные значения (до первой волны / для текущей волны)
        UpdateFromSpawner();
    }

    private void OnDisable()
    {
        if (spawner != null)
            spawner.OnWaveSpawned -= HandleWaveSpawned;
    }

    private void HandleWaveSpawned(int waveNumber, float mult, float flatHp, float flatDmg)
    {
        if (!isStarted) return; // пока не Ready — не обновляем

        UpdateUI(waveNumber, mult, flatHp, flatDmg);
    }

    public void ResetUI()
    {
        isStarted = false;
        if (waveText != null) waveText.text = "Волна: -";
        if (hpText != null) hpText.text = "Здоровье: -";
        if (dmgText != null) dmgText.text = "Урон: -";

        if (spawner != null)
            spawner.OnWaveSpawned -= HandleWaveSpawned;
    }

    public void UpdateFromSpawner()
    {
        if (spawner == null) return;

        UpdateUI(
            spawner.CurrentWaveNumber,
            spawner.CurrentMultiplier,
            spawner.CurrentFlatHealthBonus,
            spawner.CurrentFlatDamageBonus
        );
    }

    private void UpdateUI(int waveNumber, float mult, float flatHp, float flatDmg)
    {
        float hp = (baseHp * mult) + flatHp;
        float dmg = (baseDmg * mult) + flatDmg;

        if (waveText != null) waveText.text = $"Волна: {waveNumber}";
        if (hpText != null) hpText.text = $"Здоровье: {Mathf.RoundToInt(hp)}";
        if (dmgText != null) dmgText.text = $"Урон: {Mathf.RoundToInt(dmg)}";
    }
}
