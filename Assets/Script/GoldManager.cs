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

    [Tooltip("Базовый прирост золота за один тик (сумма всех плоских бонусов: +4/с, +1/с и т.д.), " +
             "БЕЗ учёта множителя. Может быть дробным (напр. 0.5) — сама выдача на баланс всё равно " +
             "идёт целыми монетами, остаток копится.")]
    public float basePassiveGoldPerTick = 5f;

    [Tooltip("Множитель на базовый прирост (1 = без бонуса, 1.8 = +80%). Копится независимо от базы: " +
             "каждый +% бонус прибавляется сюда, а не умножает уже накопленное — поэтому порядок покупок " +
             "не важен.")]
    public float passiveGoldMultiplier = 1f;

    /// <summary>Итоговое золото за тик = база * множитель. Так его и показываем в UI.</summary>
    public float goldPerTick => basePassiveGoldPerTick * passiveGoldMultiplier;

    [Tooltip("Интервал между тиками дохода (в секундах)")]
    public float incomeInterval = 1f;

    [Header("UI (необязательно)")]
    public TextMeshProUGUI goldText;  // Можно оставить пустым, если UI не нужен

    [SerializeField] private float goldGainBonus = 0f;

    /// <summary>Суммарный бонус к получаемому золоту (0.1 = +10%). Только чтение — для панелей UI.</summary>
    public float GoldGainBonus => goldGainBonus;

    private Coroutine passiveIncomeCoroutine;

    // Дробный остаток от goldPerTick (напр. 0.5/тик — каждый второй тик реально
    // выдаёт 1 золото). currentGold всегда остаётся целым — копится только здесь.
    private float pendingPassiveGold = 0f;
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
        // Баланс из таблицы (если импортирован) перекрывает инспектор.
        var cfg = BalanceService.Config;
        if (cfg != null)
        {
            startGold = cfg.shop.startGold;
            basePassiveGoldPerTick = cfg.shop.passiveGoldPerTick;
            incomeInterval = cfg.shop.passiveIncomeInterval;
        }

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
    public void AddPassiveIncome(float amountPerSecond)
    {
        if (amountPerSecond == 0f) return;

        // Автоматически включаем пассивный доход, если апгрейд куплен
        enablePassiveIncome = true;

        basePassiveGoldPerTick += amountPerSecond;
        if (basePassiveGoldPerTick < 0) basePassiveGoldPerTick = 0;

        TryStartPassiveIncome();
    }

    /// <summary>
    /// Прибавляет к независимому множителю пассивного дохода (в отличие от AddGoldGainPercent —
    /// это множитель именно на пассив, а не на любое получение золота).
    /// Каждый +% прибавляется к множителю, а не умножает уже накопленную базу — итог всегда
    /// равен basePassiveGoldPerTick * passiveGoldMultiplier, независимо от порядка покупок.
    /// Пример: база 15/сек, куплено 2 бонуса +10% -> множитель 1.2 -> итог 18/сек.
    /// </summary>
    public void MultiplyPassiveIncome(float percent)
    {
        if (percent == 0f) return;

        passiveGoldMultiplier += percent / 100f;
        if (passiveGoldMultiplier < 0f) passiveGoldMultiplier = 0f;

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

            if (enablePassiveIncome && goldPerTick > 0f)
            {
                pendingPassiveGold += goldPerTick;

                int wholeGold = Mathf.FloorToInt(pendingPassiveGold);
                if (wholeGold > 0)
                {
                    pendingPassiveGold -= wholeGold;
                    AddGold(wholeGold, GoldSource.PassiveTick);
                }
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

    // Бонус только на получение (не на траты) и НЕ на пассивный доход или фиксированные
    // выдачи (Fixed): пассив всегда равен сумме номиналов апгрейдов/наград ("+4/с" значит
    // ровно +4/с), а Fixed — это ровно заявленное число (напр. Sacrifice: ровно 200 золота),
    // иначе тултипы/описания врут, а экономика уходит в снежный ком.
    if (amount > 0 && goldGainBonus > 0f && source != GoldSource.PassiveTick && source != GoldSource.Fixed)
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
            goldText.text = $"{currentGold}";
        }
    }
}
