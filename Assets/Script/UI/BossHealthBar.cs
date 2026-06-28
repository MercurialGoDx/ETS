using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Полоса здоровья босса. Показывается, когда появляется босс (вызывает <see cref="BossManager"/>),
/// заполненность отражает текущее здоровье, скрывается при смерти босса или конце игры.
/// </summary>
public class BossHealthBar : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Корень полоски (BossBarBack) — включается/выключается при показе/скрытии.")]
    [SerializeField] private GameObject barRoot;
    [Tooltip("Image заливки (тип Filled) — fillAmount = текущее/максимальное здоровье босса.")]
    [SerializeField] private Image fillBar;

    private Enemy boss;

    private void Awake()
    {
        Hide();
    }

    /// <summary>Показать полоску для конкретного босса.</summary>
    public void Show(Enemy bossEnemy)
    {
        boss = bossEnemy;
        if (barRoot != null)
            barRoot.SetActive(true);
        UpdateFill();
    }

    /// <summary>Скрыть полоску.</summary>
    public void Hide()
    {
        boss = null;
        if (barRoot != null)
            barRoot.SetActive(false);
    }

    private void Update()
    {
        if (boss == null)
            return;

        // Конец игры (или возврат в меню) — полоску убираем.
        GameStateManager gsm = GameStateManager.Instance;
        if (gsm != null && (gsm.CurrentState == GameState.GameOver || gsm.CurrentState == GameState.Menu))
        {
            Hide();
            return;
        }

        // Босс умер или ушёл в пул — прячем.
        if (boss.isDead || !boss.gameObject.activeInHierarchy)
        {
            Hide();
            return;
        }

        UpdateFill();
    }

    private void UpdateFill()
    {
        if (fillBar == null || boss == null)
            return;

        float max = Mathf.Max(boss.maxHealth, 1f);
        float current = Mathf.Max(boss.CurrentHealth, 0f);
        fillBar.fillAmount = Mathf.Clamp01(current / max);
    }
}
