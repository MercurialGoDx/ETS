using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerShield : MonoBehaviour, ITakeDamageModifier
{
    [Header("Щит")]
    [SerializeField] private float maxShield = 0f;
    [SerializeField] private float shieldMultiplier = 1f;
    [SerializeField] private float shieldGlobalMultiplier = 1f;
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

    private int shieldStunStacks;
    private float shieldStunDuration;
    private float shieldStunDamageMultiplier;
    private float shieldStunDamageWaveSpeed;
    private UpgradeBaseSO shieldStunDamageSource;
    private GameObject shieldStunTowerVisualPrefab;
    private float shieldStunVisualLifetime = 5f;
    private float shieldStunVisualHeightOffset;
    private readonly List<Enemy> shieldStunTargets = new();

    // исходные emission значения
    private readonly Dictionary<ParticleSystem, float> baseEmissionRates = new();

    // управление корутинами
    private Coroutine fadeRoutine;

    public float MaxShield => maxShield * shieldMultiplier * shieldGlobalMultiplier;
    public float CurrentShield => currentShield;
    public float ShieldGlobalMultiplier => shieldGlobalMultiplier;

    public float ShieldRestorePerEnemyKill => shieldRestorePerEnemyKill;
    public float ShieldRechargeTime => shieldRechargeTime;
    public float ShieldRechargeDelay => shieldRechargeDelay;

    public bool IsShieldActive => shieldActive;
    public int ShieldStunStacks => shieldStunStacks;
    public float ShieldStunDuration => shieldStunDuration;
    public float ShieldStunDamageMultiplier => shieldStunDamageMultiplier;
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

    public void AddShieldGlobalMultiplier(float amount)
    {
        float oldMax = MaxShield;
        shieldGlobalMultiplier += amount;
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
                // Снимаем слепок всех общих множителей до выключения щита.
                // Так усиление «пока щит активен» тоже участвует в импульсе разрушения.
                float shieldBreakDamageMultiplier = GetShieldStunGlobalDamageMultiplier();
                currentShield = 0f;
                shieldActive = false;   // уходим в перезарядку
                shieldRegenTimer = 0f;
                noShieldDamageTimer = 0f;
                TriggerShieldStun(shieldBreakDamageMultiplier);
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

    public float GetShieldStunDurationAfterNextStack(
        float firstStunDuration,
        float additionalDurationPerStack)
    {
        float durationIncrease = shieldStunStacks == 0
            ? Mathf.Max(0f, firstStunDuration)
            : Mathf.Max(0f, additionalDurationPerStack);

        return shieldStunDuration + durationIncrease;
    }

    public float GetShieldStunDamageMultiplierAfterNextStack(
        float damageMultiplierPerStack)
    {
        return shieldStunDamageMultiplier + Mathf.Max(0f, damageMultiplierPerStack);
    }

    public void AddShieldStun(
        float firstStunDuration,
        float additionalDurationPerStack,
        float damageMultiplierPerStack,
        float damageWaveSpeed,
        UpgradeBaseSO damageSource,
        GameObject towerVisualPrefab,
        float visualLifetime,
        float visualHeightOffset)
    {
        shieldStunDuration = GetShieldStunDurationAfterNextStack(
            firstStunDuration,
            additionalDurationPerStack);
        shieldStunDamageMultiplier = GetShieldStunDamageMultiplierAfterNextStack(
            damageMultiplierPerStack);
        shieldStunStacks++;

        shieldStunDamageWaveSpeed = Mathf.Max(0f, damageWaveSpeed);
        shieldStunDamageSource = damageSource;
        shieldStunTowerVisualPrefab = towerVisualPrefab;
        shieldStunVisualLifetime = Mathf.Max(0.01f, visualLifetime);
        shieldStunVisualHeightOffset = visualHeightOffset;
    }

    private float GetShieldStunGlobalDamageMultiplier()
    {
        TowerAttack towerAttack = UpgradesManager.Instance?.towerAttack;
        return towerAttack != null ? towerAttack.GetGlobalDamageMultiplier() : 1f;
    }

    private void TriggerShieldStun(float globalDamageMultiplier)
    {
        if (shieldStunStacks <= 0 || shieldStunDuration <= 0f)
            return;

        SpawnShieldStunTowerVisual();

        EnemyManager enemyManager = EnemyManager.Instance;
        if (enemyManager == null)
            return;

        enemyManager.GetActiveEnemies(shieldStunTargets);
        foreach (Enemy enemy in shieldStunTargets)
        {
            if (enemy == null || enemy.isDead)
                continue;

            enemy.StatusEffects?.ApplyStun(
                shieldStunDuration,
                visualPrefabOverride: null,
                ignoreBossImmunity: true);
        }

        shieldStunTargets.Clear();

        StartShieldStunDamageWave(globalDamageMultiplier);
    }

    private void StartShieldStunDamageWave(float globalDamageMultiplier)
    {
        float baseDamage = MaxShield * shieldStunDamageMultiplier;
        if (baseDamage <= 0f || shieldStunDamageWaveSpeed <= 0f)
            return;

        float finalDamage = baseDamage * globalDamageMultiplier;

        StartCoroutine(DealShieldStunDamageWave(
            transform.position,
            finalDamage,
            shieldStunDamageWaveSpeed,
            shieldStunVisualLifetime,
            shieldStunDamageSource));

        Debug.Log(
            $"[ShieldStun] Damage wave: MaxShield={MaxShield:0.###}, " +
            $"base multiplier=x{shieldStunDamageMultiplier:0.###}, " +
            $"base damage={baseDamage:0.###}, global=x{globalDamageMultiplier:0.###}, " +
            $"final damage={finalDamage:0.###}.");
    }

    private IEnumerator DealShieldStunDamageWave(
        Vector3 center,
        float damage,
        float waveSpeed,
        float duration,
        UpgradeBaseSO damageSource)
    {
        var pendingEnemies = new Dictionary<Enemy, uint>();
        var damagedEnemies = new Dictionary<Enemy, uint>();
        var activeEnemies = new List<Enemy>();
        float elapsed = 0f;

        while (true)
        {
            float radius = waveSpeed * elapsed;
            float radiusSqr = radius * radius;
            EnemyManager enemyManager = EnemyManager.Instance;

            if (enemyManager != null)
            {
                enemyManager.GetActiveEnemies(activeEnemies);
                foreach (Enemy enemy in activeEnemies)
                {
                    if (enemy == null || enemy.isDead)
                        continue;

                    uint activationVersion = enemy.ActivationVersion;
                    if (damagedEnemies.TryGetValue(enemy, out uint damagedVersion))
                    {
                        if (damagedVersion == activationVersion)
                            continue;

                        damagedEnemies.Remove(enemy);
                    }

                    if (pendingEnemies.TryGetValue(enemy, out uint pendingVersion) &&
                        pendingVersion != activationVersion)
                    {
                        pendingEnemies.Remove(enemy);
                    }

                    Vector3 offset = enemy.transform.position - center;
                    offset.y = 0f;
                    float distanceSqr = offset.sqrMagnitude;

                    if (pendingEnemies.ContainsKey(enemy))
                    {
                        if (distanceSqr <= radiusSqr)
                        {
                            pendingEnemies.Remove(enemy);
                            damagedEnemies[enemy] = activationVersion;
                            enemy.TakeWeaponDamage(
                                damage,
                                (WeaponDamageType)(-1));
                            DamageStatsManager.Instance?.RegisterDamage(damageSource, damage);
                        }
                    }
                    else if (distanceSqr >= radiusSqr)
                    {
                        // Враг появился перед фронтом волны. Если он появился уже
                        // позади прошедшего фронта, урон от этой волны не получит.
                        pendingEnemies[enemy] = activationVersion;
                    }
                }
            }

            if (elapsed >= duration)
                yield break;

            yield return null;
            elapsed = Mathf.Min(duration, elapsed + Time.deltaTime);
        }
    }

    private void SpawnShieldStunTowerVisual()
    {
        if (shieldStunTowerVisualPrefab == null)
            return;

        Vector3 localSpawnPosition =
            shieldStunTowerVisualPrefab.transform.localPosition +
            Vector3.up * shieldStunVisualHeightOffset;
        Vector3 worldSpawnPosition = transform.TransformPoint(localSpawnPosition);
        Quaternion worldSpawnRotation =
            transform.rotation * shieldStunTowerVisualPrefab.transform.localRotation;

        GameObject vfxContainer = GameObject.Find("/Managers/VFXContainer");
        Transform visualParent = vfxContainer != null ? vfxContainer.transform : null;
        GameObject visual = Instantiate(
            shieldStunTowerVisualPrefab,
            worldSpawnPosition,
            worldSpawnRotation,
            visualParent);
        visual.name = shieldStunTowerVisualPrefab.name;
        Destroy(visual, shieldStunVisualLifetime);
    }
}
