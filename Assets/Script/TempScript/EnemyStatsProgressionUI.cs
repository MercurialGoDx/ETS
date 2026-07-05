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
    // Smart-строки вида "Health: {0}" / "Damage: {0}". Число передаётся как аргумент {0},
    // поэтому при смене языка (StringChanged) текст перерисовывается ВМЕСТЕ с числом —
    // раньше локаль перезаписывала подпись без числа и значение «пропадало».
    [SerializeField] private LocalizedString healthString;
    [SerializeField] private LocalizedString damageString;

    [Header("Base enemy stats for UI")]
    [SerializeField] private float baseHp = 15f;
    [SerializeField] private float baseDmg = 2f;

    private bool isStarted = false;

    // Аргументы для smart-строк. Хранятся как object, чтобы можно было показать "-" до старта.
    private readonly object[] hpArgs = new object[] { "-" };
    private readonly object[] dmgArgs = new object[] { "-" };

    private void OnEnable()
    {
        healthString.Arguments = hpArgs;
        damageString.Arguments = dmgArgs;
        // Подписка сразу отрисует текущее значение и будет реагировать на смену локали.
        healthString.StringChanged += OnHealthStringChanged;
        damageString.StringChanged += OnDamageStringChanged;
    }

    private void OnDisable()
    {
        healthString.StringChanged -= OnHealthStringChanged;
        damageString.StringChanged -= OnDamageStringChanged;

        if (spawner != null)
            spawner.OnWaveSpawned -= HandleWaveSpawned;
    }

    /// <summary>
    /// Вызывается из GameStartController по кнопке Ready — включает авто-обновление от EnemySpawner.
    /// </summary>
    public void StartProgression()
    {
        isStarted = true;

        if (spawner == null)
        {
            Debug.LogWarning("[EnemyStatsProgressionUI] Spawner is not assigned!");
            return;
        }

        spawner.OnWaveSpawned -= HandleWaveSpawned;
        spawner.OnWaveSpawned += HandleWaveSpawned;

        UpdateFromSpawner();
    }

    private void HandleWaveSpawned(int waveNumber, float mult, float flatHp, float flatDmg)
    {
        if (!isStarted) return;
        UpdateUI(waveNumber, mult, flatHp, flatDmg);
    }

    public void ResetUI()
    {
        isStarted = false;

        hpArgs[0] = "-";
        dmgArgs[0] = "-";
        RefreshStrings();

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

        hpArgs[0] = Mathf.RoundToInt(hp);
        dmgArgs[0] = Mathf.RoundToInt(dmg);

        if (waveText != null)
            waveText.text = waveNumber.ToString();

        RefreshStrings();
    }

    // Перерисовка smart-строк с текущими аргументами (число не теряется).
    private void RefreshStrings()
    {
        healthString.RefreshString();
        damageString.RefreshString();
    }

    private void OnHealthStringChanged(string value)
    {
        if (hpText != null) hpText.text = value;
    }

    private void OnDamageStringChanged(string value)
    {
        if (dmgText != null) dmgText.text = value;
    }
}
