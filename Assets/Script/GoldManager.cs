using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GoldManager : MonoBehaviour
{
    public static GoldManager Instance;

    [Header("Параметры золота")]
    public int startGold = 0;
    public int currentGold;

    [Header("Пассивный доход")]
    [Tooltip("Включить/выключить пассивный доход золота по времени (для дебага)")]
    public bool enablePassiveIncome = true;

    [Tooltip("Сколько золота выдавать за один тик (обычно = золото в секунду)")]
    public int goldPerTick = 5;

    [Tooltip("Интервал между тиками дохода (в секундах)")]
    public float incomeInterval = 1f;

    [Header("UI (необязательно)")]
    public TextMeshProUGUI goldText;  // Можно оставить пустым, если UI не нужен

    [SerializeField] private float goldGainBonus = 0f;
    private Coroutine passiveIncomeCoroutine;
    public event System.Action<int, GoldSource, UnityEngine.Vector3?> OnGoldGained;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        currentGold = startGold;
        UpdateUI();

        TryStartPassiveIncome();
    }

    // ================== ПАССИВНЫЙ ДОХОД ==================

    /// <summary>
    /// Увеличивает пассивный доход золота (обычно вызывается из апгрейдов).
    /// Например, +1 золото/сек за покупку.
    /// </summary>
    public void AddGoldGainPercent(float percent)
    {
        // percent приходит как 10 (=10%), 25 (=25%) и т.д.
        goldGainBonus += percent / 100f;
        if (goldGainBonus < 0f) goldGainBonus = 0f;
    }
    public void AddPassiveIncome(int amountPerSecond)
    {
        if (amountPerSecond == 0) return;

        // Автоматически включаем пассивный доход, если апгрейд куплен
        enablePassiveIncome = true;

        goldPerTick += amountPerSecond;
        if (goldPerTick < 0) goldPerTick = 0;

        TryStartPassiveIncome();
    }

    private void TryStartPassiveIncome()
    {
        // Ничего не делаем, если выключено или интервал некорректен
        if (!enablePassiveIncome || incomeInterval <= 0f)
            return;

        // Если корутина уже запущена — не дублируем
        if (passiveIncomeCoroutine != null)
            return;

        // Даже если goldPerTick = 0, корутина может уже работать,
        // но золото просто не будет прибавляться до первого апгрейда.
        passiveIncomeCoroutine = StartCoroutine(PassiveIncomeRoutine());
    }

    private IEnumerator PassiveIncomeRoutine()
    {
        var wait = new WaitForSeconds(incomeInterval);

        while (true)
        {
            yield return wait;

            if (enablePassiveIncome && goldPerTick > 0)
            {
                AddGold(goldPerTick, GoldSource.PassiveTick);
            }
        }
    }

    // ================== ПУБЛИЧНЫЕ МЕТОДЫ ==================

    // Старый метод оставляем для совместимости.
// Все старые места в проекте продолжат работать.
public void AddGold(int amount)
{
    AddGold(amount, GoldSource.Other, null);
}

// Новый главный метод: тут ВСЯ логика начисления ивентов и % бонуса
public void AddGold(int amount, GoldSource source, Vector3? worldPos = null)
{
    if (amount == 0) return;

    int finalAmount = amount;

    // Бонус только на получение (не на траты)
    if (amount > 0 && goldGainBonus > 0f)
    {
        finalAmount = Mathf.RoundToInt(amount * (1f + goldGainBonus));
        if (finalAmount < 1) finalAmount = 1;
    }

    currentGold += finalAmount;
    if (currentGold < 0) currentGold = 0;

    UpdateUI();

    // Событие отправляем только если реально получили золото
    if (finalAmount > 0)
        OnGoldGained?.Invoke(finalAmount, source, worldPos);
}


    public bool HasEnoughGold(int price)
    {
        return currentGold >= price;
    }

    public bool SpendGold(int price)
    {
        if (!HasEnoughGold(price))
            return false;

        currentGold -= price;
        if (currentGold < 0) currentGold = 0;

        UpdateUI();
        return true;
    }

    private void UpdateUI()
    {
        if (goldText != null)
        {
            goldText.text = $"Золото: {currentGold}";
        }
    }
}
