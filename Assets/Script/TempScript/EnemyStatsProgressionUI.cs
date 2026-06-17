using TMPro;
using UnityEngine;
using UnityEngine.Localization;

public class EnemyStatsProgressionUI : MonoBehaviour
{
    [Header("Links")]
    [SerializeField] private EnemySpawner spawner;

    [Header("UI")]
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text dmgText;
    [SerializeField] private TMP_Text waveText; // можно оставить пустым

    [Header("Localization")]
    [SerializeField] private LocalizedString healthPrefix;  // "Здоровье: " или "Health: "
    [SerializeField] private LocalizedString damagePrefix;  // "Урон: " или "Damage: "

    [Header("Base enemy stats for UI")]
    [SerializeField] private float baseHp = 15f;
    [SerializeField] private float baseDmg = 2f;

    private bool isStarted = false;

    private string _currentHealthPrefix = "Здоровье: ";
    private string _currentDamagePrefix = "Урон: ";

    private int _currentHpValue = 0;
    private int _currentDmgValue = 0;

    private void Awake()
    {
        // Подписываемся на изменение языка
        //if (healthPrefix != null) healthPrefix.StringChanged += UpdateHealthPrefix;
        //if (damagePrefix != null) damagePrefix.StringChanged += UpdateDamagePrefix;
    }

    private void OnDestroy()
    {
        // Отписываемся
        //if (healthPrefix != null) healthPrefix.StringChanged -= UpdateHealthPrefix;
        //if (damagePrefix != null) damagePrefix.StringChanged -= UpdateDamagePrefix;
    }

    private void Start()
    {
        //UpdateFromSpawner();
    }

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
        if (hpText != null) hpText.text = $"{_currentHealthPrefix}-";
        if (dmgText != null) dmgText.text = $"{_currentDamagePrefix}-";

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

        _currentHpValue = Mathf.RoundToInt(hp);
        _currentDmgValue = Mathf.RoundToInt(dmg);

        UpdateUITexts();
    }

    private void UpdateUITexts()
    {
        if (hpText != null)
            //hpText.text = $"{_currentHealthPrefix}{_currentHpValue}";
            hpText.text = $"Health: 12";

        if (dmgText != null)
            //dmgText.text = $"{_currentDamagePrefix}{_currentDmgValue}";
            dmgText.text = $"Damage: 1";
    }

    private void UpdateHealthPrefix(string localizedPrefix)
    {
        _currentHealthPrefix = localizedPrefix;
        UpdateFromSpawner(); // Обновляем UI с новым префиксом
    }

    private void UpdateDamagePrefix(string localizedPrefix)
    {
        _currentDamagePrefix = localizedPrefix;
        UpdateFromSpawner();
    }
}
