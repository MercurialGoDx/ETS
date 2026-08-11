using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerShield : MonoBehaviour, ITakeDamageModifier
{
    [Header("Щит")]
    [SerializeField] private float maxShield = 0f;
    [SerializeField] private float shieldMultiplier = 1f;
    [SerializeField] private float shieldRechargeTime = 10f;
    [SerializeField] private float shieldRechargeDelay = 0f; // хранение значения, в логике не используем
    [SerializeField] private float damageWhileShieldActivePercent = 0f; // бонус к урону, когда щит АКТИВЕН
    [SerializeField] private float shieldRestorePerEnemyKill = 0f;

    [Header("Полный фулл щита без урона")]
    [Tooltip("Если щит НЕ терял прочность от атак врага в течение этого времени — мгновенно восстанавливаем до максимума.")]
    [SerializeField] private float fullRestoreAfterNoShieldDamageSeconds = 20f;

    [Header("UI")]
    [SerializeField] private Image shieldBarFill;

    [Header("Visual Shield (VFX)")]
    [SerializeField] private GameObject shieldVisual; // VFX объект щита на игроке

    [Header("VFX Fade")]
    [SerializeField] private float vfxFadeInTime = 0.2f;
    [SerializeField] private float vfxFadeOutTime = 0.25f;
    [SerializeField] private bool disableObjectAfterFadeOut = true;
    [SerializeField] private bool fadeAlphaIfPossible = true; // если материалы поддерживают альфу — будет плавнее

    private float currentShield;
    private float shieldRegenTimer = 0f;

    // true = щит активен (не в перезарядке), false = щит в перезарядке/выбит и заряжается
    private bool shieldActive = true;

    // таймер "сколько времени щит не получал урон (не терял прочность)"
    private float noShieldDamageTimer = 0f;

    // чтобы не дергать визуалку каждый кадр
    private bool lastVisualShouldBeVisible = false;

    // VFX кеш
    private ParticleSystem[] cachedParticles;
    private TrailRenderer[] cachedTrails;
    private LineRenderer[] cachedLines;
    private Renderer[] cachedRenderers;

    // исходные emission значения
    private readonly Dictionary<ParticleSystem, float> baseEmissionRates = new();

    // управление корутинами
    private Coroutine fadeRoutine;

    public float MaxShield => maxShield * shieldMultiplier;
    public float CurrentShield => currentShield;

    public float ShieldRestorePerEnemyKill => shieldRestorePerEnemyKill;
    public float ShieldRechargeTime => shieldRechargeTime;
    public float ShieldRechargeDelay => shieldRechargeDelay;

    public bool IsShieldActive => shieldActive;
    public int Priority => 100;

    private void Awake()
    {
        // Баланс из таблицы (если импортирован) перекрывает инспектор.
        var cfg = BalanceService.Config;
        if (cfg != null)
        {
            maxShield = cfg.player.playerShield;
            shieldRechargeTime = cfg.player.shieldRechargeTime;
            fullRestoreAfterNoShieldDamageSeconds = cfg.player.shieldFullRestoreAfterNoDamage;
        }

        currentShield = MaxShield;
        shieldActive = MaxShield > 0f;
        shieldRegenTimer = 0f;
        noShieldDamageTimer = 0f;

        CacheVfxComponents();

        UpdateShieldUI();

        // принудительно применяем стартовое состояние визуалки
        lastVisualShouldBeVisible = false;
        ApplyShieldVisualStateImmediate(GetShouldBeVisible());
        lastVisualShouldBeVisible = GetShouldBeVisible();
    }

    private void Update()
    {
        if (GameStateManager.Instance.CurrentState != GameState.Playing) return;

        HandleNoShieldDamageFullRestore();
        HandleShieldRegen();
    }

    // === МЕХАНИКА: если щит не терял прочность N секунд -> мгновенно фуллим ===
    private void HandleNoShieldDamageFullRestore()
    {
        if (fullRestoreAfterNoShieldDamageSeconds <= 0f) return;

        // если щита как механики нет — не копим таймер
        if (MaxShield <= 0f)
        {
            noShieldDamageTimer = 0f;
            return;
        }

        // если щит не активен (перезарядка) — не фулим "за бездействие", иначе будет ломать твою механику зарядки
        if (!shieldActive)
        {
            noShieldDamageTimer = 0f;
            return;
        }

        // если и так полный — держим таймер на нуле
        if (currentShield >= MaxShield)
        {
            noShieldDamageTimer = 0f;
            return;
        }

        noShieldDamageTimer += Time.deltaTime;

        if (noShieldDamageTimer >= fullRestoreAfterNoShieldDamageSeconds)
        {
            currentShield = MaxShield;   // за 1 кадр
            noShieldDamageTimer = 0f;

            UpdateShieldUI();
            UpdateShieldVisual();
        }
    }

    // === Апгрейды щита ===

    public void AddMaxShield(float amount)
    {
        float oldMax = MaxShield;

        maxShield += amount;

        float newMax = MaxShield;
        float delta = newMax - oldMax;

        if (oldMax <= 0f)
        {
            shieldActive = true;
            currentShield = newMax;
            shieldRegenTimer = 0f;
            noShieldDamageTimer = 0f;
        }
        else
        {
            currentShield = Mathf.Min(currentShield + delta, newMax);
        }

        UpdateShieldUI();
        UpdateShieldVisual();
    }

    // amount = 0.1f → +10%
    public void AddShieldPercent(float amount)
    {
        float oldMax = MaxShield;

        shieldMultiplier += amount;

        float newMax = MaxShield;
        float delta = newMax - oldMax;

        if (oldMax <= 0f)
        {
            shieldActive = true;
            currentShield = newMax;
            shieldRegenTimer = 0f;
            noShieldDamageTimer = 0f;
        }
        else
        {
            currentShield = Mathf.Min(currentShield + delta, newMax);
        }

        UpdateShieldUI();
        UpdateShieldVisual();
    }

    public void SetShieldRechargeTime(float newTime)
    {
        shieldRechargeTime = Mathf.Max(0.1f, newTime);
    }

    public void SetShieldRechargeDelay(float newDelay)
    {
        shieldRechargeDelay = Mathf.Max(0f, newDelay);
    }

    // === Логика регена щита ===
    // Важно: во время регена shieldActive == false -> визуалка выключена, даже если currentShield > 1
    private void HandleShieldRegen()
    {
        // если щит как механика не задан — щита нет
        if (MaxShield <= 0f)
        {
            currentShield = 0f;
            shieldActive = false;
            shieldRegenTimer = 0f;
            noShieldDamageTimer = 0f;

            UpdateShieldUI();
            UpdateShieldVisual();
            return;
        }

        // если щит активен — ничего не делаем
        if (shieldActive)
        {
            if (currentShield > MaxShield)
            {
                currentShield = MaxShield;
                UpdateShieldUI();
            }

            UpdateShieldVisual();
            return;
        }

        // щит в перезарядке
        shieldRegenTimer += Time.deltaTime;

        float duration = Mathf.Max(0.01f, shieldRechargeTime);
        float t = Mathf.Clamp01(shieldRegenTimer / duration);

        currentShield = MaxShield * t;

        UpdateShieldUI();
        UpdateShieldVisual();

        // зарядился полностью — снова активируем
        if (t >= 1f)
        {
            shieldActive = true;
            currentShield = MaxShield;
            shieldRegenTimer = 0f;
            noShieldDamageTimer = 0f;

            UpdateShieldUI();
            UpdateShieldVisual();
        }
    }

    // === UI ===

    private void UpdateShieldUI()
    {
        if (shieldBarFill == null) return;

        float normalized = MaxShield > 0f ? currentShield / MaxShield : 0f;
        shieldBarFill.fillAmount = Mathf.Clamp01(normalized);
    }

    // === Визуалка щита (VFX) ===
    private bool GetShouldBeVisible()
    {
        return MaxShield > 0f && currentShield > 0f && shieldActive;
    }

    private void UpdateShieldVisual()
    {
        if (shieldVisual == null) return;

        bool shouldBeVisible = GetShouldBeVisible();

        if (lastVisualShouldBeVisible == shouldBeVisible)
            return;

        lastVisualShouldBeVisible = shouldBeVisible;
        ApplyShieldVisualStateSmooth(shouldBeVisible);
    }

    private void CacheVfxComponents()
    {
        if (shieldVisual == null) return;

        cachedParticles = shieldVisual.GetComponentsInChildren<ParticleSystem>(true);
        cachedTrails = shieldVisual.GetComponentsInChildren<TrailRenderer>(true);
        cachedLines = shieldVisual.GetComponentsInChildren<LineRenderer>(true);
        cachedRenderers = shieldVisual.GetComponentsInChildren<Renderer>(true);

        baseEmissionRates.Clear();
        foreach (var ps in cachedParticles)
        {
            if (ps == null) continue;
            var em = ps.emission;
            baseEmissionRates[ps] = em.rateOverTimeMultiplier;
        }
    }

    private void ApplyShieldVisualStateImmediate(bool visible)
    {
        if (shieldVisual == null) return;

        if (!visible)
        {
            StopAndClearVfx();
            if (disableObjectAfterFadeOut)
                shieldVisual.SetActive(false);
            else
                SetAlphaAll(0f);

            return;
        }

        shieldVisual.SetActive(true);
        SetAlphaAll(1f);
        SetEmissionMultiplier(1f);
        PlayVfx();
    }

    private void ApplyShieldVisualStateSmooth(bool visible)
    {
        if (shieldVisual == null) return;

        if (cachedParticles == null || cachedParticles.Length == 0)
            CacheVfxComponents();

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeVfxRoutine(visible));
    }

    private IEnumerator FadeVfxRoutine(bool show)
    {
        if (show)
        {
            shieldVisual.SetActive(true);
            PlayVfx();

            float dur = Mathf.Max(0.01f, vfxFadeInTime);
            float t = 0f;

            SetEmissionMultiplier(0f);
            SetAlphaAll(fadeAlphaIfPossible ? 0f : 1f);

            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);

                SetEmissionMultiplier(k);
                if (fadeAlphaIfPossible) SetAlphaAll(k);

                yield return null;
            }

            SetEmissionMultiplier(1f);
            if (fadeAlphaIfPossible) SetAlphaAll(1f);
        }
        else
        {
            float dur = Mathf.Max(0.01f, vfxFadeOutTime);
            float t = 0f;

            while (t < dur)
            {
                t += Time.deltaTime;
                float k = 1f - Mathf.Clamp01(t / dur);

                SetEmissionMultiplier(k);
                if (fadeAlphaIfPossible) SetAlphaAll(k);

                yield return null;
            }

            SetEmissionMultiplier(0f);
            if (fadeAlphaIfPossible) SetAlphaAll(0f);

            StopAndClearVfx();

            if (disableObjectAfterFadeOut)
                shieldVisual.SetActive(false);
        }

        fadeRoutine = null;
    }

    private void PlayVfx()
    {
        if (cachedLines != null)
        {
            foreach (var lr in cachedLines)
                if (lr != null) lr.enabled = true;
        }

        if (cachedTrails != null)
        {
            foreach (var tr in cachedTrails)
                if (tr != null) tr.Clear();
        }

        if (cachedParticles != null)
        {
            foreach (var ps in cachedParticles)
            {
                if (ps == null) continue;
                ps.Clear(true);
                ps.Play(true);
            }
        }
    }

    private void StopAndClearVfx()
    {
        if (cachedParticles != null)
        {
            foreach (var ps in cachedParticles)
            {
                if (ps == null) continue;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Clear(true);
            }
        }

        if (cachedTrails != null)
        {
            foreach (var tr in cachedTrails)
                if (tr != null) tr.Clear();
        }

        if (cachedLines != null)
        {
            foreach (var lr in cachedLines)
                if (lr != null) lr.enabled = false;
        }
    }

    private void SetEmissionMultiplier(float multiplier01)
    {
        if (cachedParticles == null) return;

        float m = Mathf.Clamp01(multiplier01);

        foreach (var ps in cachedParticles)
        {
            if (ps == null) continue;

            var em = ps.emission;
            if (!baseEmissionRates.TryGetValue(ps, out float baseRate))
                baseRate = em.rateOverTimeMultiplier;

            em.rateOverTimeMultiplier = baseRate * m;
        }
    }

    private void SetAlphaAll(float a)
    {
        if (!fadeAlphaIfPossible) return;
        if (cachedRenderers == null) return;

        a = Mathf.Clamp01(a);

        foreach (var r in cachedRenderers)
        {
            if (r == null) continue;
            r.enabled = true;

            var mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);

            if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_BaseColor"))
            {
                Color c = r.sharedMaterial.GetColor("_BaseColor");
                c.a = a;
                mpb.SetColor("_BaseColor", c);
                r.SetPropertyBlock(mpb);
            }
            else if (r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Color"))
            {
                Color c = r.sharedMaterial.GetColor("_Color");
                c.a = a;
                mpb.SetColor("_Color", c);
                r.SetPropertyBlock(mpb);
            }
        }
    }

    // === Получение урона ===
    // "Получать урон" в твоем смысле: щит теряет прочность от атак.
    public float ModifyDamage(float damage)
    {
        float remaining = damage;

        if (shieldActive && MaxShield > 0f && currentShield > 0f)
        {
            float before = currentShield;

            float shieldUsed = Mathf.Min(remaining, currentShield);
            currentShield -= shieldUsed;
            remaining -= shieldUsed;

            // если щит реально потерял прочность — сбрасываем таймер "без урона"
            if (currentShield < before)
            {
                noShieldDamageTimer = 0f;
            }

            if (currentShield <= 0f)
            {
                currentShield = 0f;
                shieldActive = false;   // уходим в перезарядку
                shieldRegenTimer = 0f;
                noShieldDamageTimer = 0f;
            }

            UpdateShieldUI();
            UpdateShieldVisual();
            return remaining;
        }

        return damage;
    }

    // === Бонус к урону при активном щите ===

    public void AddDamageWhileShieldActivePercent(float amount)
    {
        damageWhileShieldActivePercent += amount / 100;
    }

    public float GetShieldDamageBonusMultiplier()
    {
        if (damageWhileShieldActivePercent <= 0f)
            return 1f;

        if (!IsShieldActive)
            return 1f;

        float percent = damageWhileShieldActivePercent / 100f;
        return 1f + percent;
    }

    // === Восстановление ===

    public void AddShieldRestorePerEnemyKill(float amount)
    {
        shieldRestorePerEnemyKill += amount;
    }

    public void RestoreCurrentShield(float amount)
    {
        if (MaxShield <= 0f) return;

        currentShield = Mathf.Clamp(currentShield + amount, 0f, MaxShield);

        // это не "урон", поэтому таймер не сбрасываем
        UpdateShieldUI();
        UpdateShieldVisual();
    }
}
