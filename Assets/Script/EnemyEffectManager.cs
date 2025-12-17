using UnityEngine;
using System.Collections.Generic;

public class EnemyEffectManager : MonoBehaviour
{
    public static EnemyEffectManager Instance { get; private set; }

    public interface IEffectEntry
    {
        float CurrentChance { get; }
    }

    public interface IEffectEntry1
    {
        float CurrentChance { get; }

        void UpdatePreview();
    }

    [System.Serializable]
    public class EffectEntry : IEffectEntry, IEffectEntry1
    {
        public EnemyEffectDefinition effect;

        [Header("База")]
        [Tooltip("Базовый шанс появления (0..1). Обычно 0.")]
        public float baseChance = 0f;

        [Header("Рост шанса с убывающей полезностью")]
        [Tooltip("Прирост шанса за ПЕРВЫЙ апгрейд (0.05 = 5%)")]
        public float baseIncrease = 0.05f;

        [Tooltip("Во сколько раз слабее каждый следующий прирост (0.8 = 80% от предыдущего)")]
        public float diminishingFactor = 0.8f;

        [HideInInspector]
        public int stacks = 0;  // сколько раз купили апгрейд

        [Header("Итоговый шанс (только чтение)")]
        [SerializeField, Range(0f, 1f)]
        private float previewChance = 0f;  // отображается в инспекторе

        /// <summary>
        /// Итоговый шанс (0..1) с убывающей полезностью.
        /// </summary>
        public float CurrentChance
        {
            get
            {
                float total = baseChance;

                float inc = baseIncrease;
                for (int i = 0; i < stacks; i++)
                {
                    total += inc;
                    inc *= diminishingFactor;
                }

                return Mathf.Clamp01(total);
            }
        }

        // Обновляет previewChance автоматически в инспекторе
        public void UpdatePreview()
        {
            previewChance = CurrentChance;
        }
    }

    [Header("Эффекты врагов")]
    public List<EffectEntry> effects = new List<EffectEntry>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // 👉 вызываем из UpgradesManager при покупке улучшения
    public void AddUpgradeForEffect(EnemyEffectDefinition def)
    {
        if (def == null) return;

        foreach (var e in effects)
        {
            if (e.effect == def)
            {
                e.stacks++;
                e.UpdatePreview(); // ← ДОБАВИЛИ: пересчёт итогового шанса

                Debug.Log($"[EnemyEffectManager] Upgrade for {def.name}, stacks = {e.stacks}, chance = {e.CurrentChance * 100f:0.0}%");
                return;
            }
        }

        Debug.LogWarning($"[EnemyEffectManager] Effect {def.name} не найден в списке effects");
    }

    // 👉 вызываем из EnemySpawner при спавне врага
    public void ApplyEffectsToEnemy(Enemy enemy)
    {
        if (enemy == null) return;

        foreach (var entry in effects)
        {
            if (entry.effect == null) continue;

            float chance = entry.CurrentChance;
            if (chance <= 0f) continue;

            if (Random.value <= chance)
            {
                // визуал — дочерний объект
                if (entry.effect.visualPrefab != null)
                {
                    var vis = Instantiate(entry.effect.visualPrefab, enemy.transform);
                    vis.transform.localPosition = Vector3.zero;
                }

                // бонус к золоту
                enemy.bonusGold += entry.effect.extraGold;
            }
        }
    }
    private void OnValidate()
    {
        // Чтобы красиво обновлялось в инспекторе при изменении значений
        foreach (var entry in effects)
        {
            if (entry != null)
                entry.UpdatePreview();
        }
    }

}
