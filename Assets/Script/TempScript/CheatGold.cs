using UnityEngine;

public class DebugCheatGoldSequence : MonoBehaviour
{
    [Header("Настройки")]
    public int addGoldAmount = 1000;
    public float inputTimeout = 1.5f; // сек между нажатиями

    private readonly KeyCode[] sequence = {
        KeyCode.G,
        KeyCode.O,
        KeyCode.L,
        KeyCode.D
    };

    private int currentIndex = 0;
    private float lastInputTime = 0f;

    private void Update()
    {
        // 🔒 Только для дебага (Editor + Development Build)
        if (!Debug.isDebugBuild)
            return;

        // если долго не нажимали — сбрасываем
        if (currentIndex > 0 && Time.time - lastInputTime > inputTimeout)
        {
            ResetSequence();
        }

        // проверяем ввод
        if (Input.anyKeyDown)
        {
            if (Input.GetKeyDown(sequence[currentIndex]))
            {
                currentIndex++;
                lastInputTime = Time.time;

                // вся последовательность введена
                if (currentIndex >= sequence.Length)
                {
                    GrantGold();
                    ResetSequence();
                }
            }
            else
            {
                // нажали не ту клавишу — сброс
                ResetSequence();
            }
        }
    }

    private void GrantGold()
    {
        if (GoldManager.Instance != null)
        {
            GoldManager.Instance.AddGold(addGoldAmount);
            Debug.Log($"<color=yellow>[CHEAT]</color> GOLD +{addGoldAmount}");
        }
        else
        {
            Debug.LogWarning("[CHEAT] GoldManager.Instance == null");
        }
    }

    private void ResetSequence()
    {
        currentIndex = 0;
        lastInputTime = 0f;
    }
}
