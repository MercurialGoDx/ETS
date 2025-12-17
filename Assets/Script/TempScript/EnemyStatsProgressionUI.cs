using System.Collections;
using TMPro;
using UnityEngine;

public class EnemyStatsProgressionUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text dmgText;

    [Header("Start values")]
    [SerializeField] private float hp = 15f;
    [SerializeField] private float dmg = 2f;

    [Header("Growth")]
    [Tooltip("Интервал повышения (сек)")]
    [SerializeField] private float intervalSeconds = 10f;

    [Tooltip("Процент роста за тик. 0.05 = +5%")]
    [SerializeField] private float growthPercent = 0.05f;

    private Coroutine routine;

    private void OnDisable()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    // Вызывай это из кнопки Ready
    public void StartProgression()
    {
    Debug.Log($"[EnemyStats] StartProgression called. timeScale={Time.timeScale}. active={gameObject.activeInHierarchy}");
    if (routine != null) return;

    UpdateUI();
    routine = StartCoroutine(GrowLoop());
}

    // Если нужно сбрасывать при выходе в меню / новой игре
    public void ResetStats(float startHp = 15f, float startDmg = 2f)
    {
        hp = startHp;
        dmg = startDmg;
        UpdateUI();
    }

    private IEnumerator GrowLoop()
    {
        var wait = new WaitForSeconds(intervalSeconds);

        while (true)
        {
            yield return wait;

            // рост "сам на себя" (компаунд)
            hp *= (1f + growthPercent);
            dmg *= (1f + growthPercent);

            UpdateUI();
        }
    }

    private void UpdateUI()
    {
        if (hpText != null) hpText.text = $"Здоровье: {Mathf.RoundToInt(hp)}";
        if (dmgText != null) dmgText.text = $"Урон: {Mathf.RoundToInt(dmg)}";
    }

    // Если другим скриптам нужно брать эти значения
    public float CurrentHp => hp;
    public float CurrentDmg => dmg;
}
