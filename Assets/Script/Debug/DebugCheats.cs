// Панель читов для ручного тестирования забега.
// Весь файл собирается только в редакторе и в development-сборке: в релиз он не попадёт
// физически, поэтому читы невозможно случайно оставить включёнными в билде для игроков.
#if UNITY_EDITOR || DEVELOPMENT_BUILD

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Оверлей с читами (по умолчанию F1). Ничего не меняет в игровом коде — дёргает те же
/// публичные методы, что и магазин с наградами за боссов, поэтому поведение совпадает
/// с настоящей покупкой. Списки оружия и улучшений берутся из пула магазина.
///
/// Правило: значения для теста задавать здесь, а НЕ в таблице баланса. Однажды
/// player_hp = 999999 уже уехал из таблицы в ассеты и чуть не попал в коммит.
/// </summary>
public class DebugCheats : MonoBehaviour
{
    [Header("Управление")]
    public KeyCode toggleKey = KeyCode.F1;

    [Tooltip("Источник списков оружия и улучшений. Пусто — найдётся сам.")]
    [SerializeField] private ShopManager shop;

    [Header("Размеры окна")]
    [SerializeField] private float windowWidth = 460f;
    [SerializeField] private float windowHeight = 620f;

    private bool visible;
    private Rect window = new Rect(20f, 20f, 460f, 620f);
    private Vector2 weaponsScroll;
    private Vector2 upgradesScroll;
    private string filter = "";
    private bool godMode;
    private string lobbyCodeInput = "";
    private string lobbyStatus = "";

    // Кэш, чтобы не искать объекты каждый кадр отрисовки GUI.
    private PlayerHealth health;
    private PlayerShield shield;
    private TowerAttack tower;

    private void Awake()
    {
        window.width = windowWidth;
        window.height = windowHeight;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            visible = !visible;
            if (visible) Rebind();
        }

        // Бессмертие: настоящего флага неуязвимости в PlayerHealth нет, поэтому просто
        // добиваем здоровье до максимума. Урон при этом реально проходит — если нужно
        // ловить баги в расчёте урона, чит лучше выключить.
        if (godMode && health != null)
            health.Heal(health.MaxHealth);
    }

    /// <summary>Пересобирает ссылки: между забегами объекты пересоздаются.</summary>
    private void Rebind()
    {
        if (shop == null) shop = FindFirstObjectByType<ShopManager>();
        health = FindFirstObjectByType<PlayerHealth>();
        shield = FindFirstObjectByType<PlayerShield>();
        tower = FindFirstObjectByType<TowerAttack>();
    }

    private void OnGUI()
    {
        if (!visible) return;
        window = GUILayout.Window(GetInstanceID(), window, DrawWindow, "Читы  ·  " + toggleKey + " — закрыть");
    }

    private void DrawWindow(int id)
    {
        GUILayout.Label(StateLine());

        // ---------- Ресурсы ----------
        GUILayout.Space(4f);
        GUILayout.Label("Золото");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+1 000")) AddGold(1000);
        if (GUILayout.Button("+10 000")) AddGold(10000);
        if (GUILayout.Button("+100 000")) AddGold(100000);
        GUILayout.EndHorizontal();

        // ---------- Выживаемость ----------
        GUILayout.Space(4f);
        GUILayout.Label("Выживаемость");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Полное HP") && health != null) health.Heal(health.MaxHealth);
        if (GUILayout.Button("+500 макс. HP") && health != null) health.IncreaseMaxHealth(500f, true);
        if (GUILayout.Button("+500 щита") && shield != null) shield.AddMaxShield(500f);
        GUILayout.EndHorizontal();
        godMode = GUILayout.Toggle(godMode, "Бессмертие (хил до полного каждый кадр)");

        // ---------- Лобби ----------
        GUILayout.Space(4f);
        GUILayout.Label(LobbyLine());

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Создать")) CreateLobby();
        if (GUILayout.Button("Пригласить")) Lobbies.Service.InviteFriend();
        if (GUILayout.Button("Выйти")) LeaveLobby();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        lobbyCodeInput = GUILayout.TextField(lobbyCodeInput, GUILayout.Width(110f));
        if (GUILayout.Button("Войти по коду")) JoinLobby();
        GUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(lobbyStatus))
            GUILayout.Label(lobbyStatus);

        foreach (var member in Lobbies.Service.Members)
            GUILayout.Label($"  • {member.Name}{(member.IsHost ? " (хост)" : "")}");

        // ---------- Темп забега ----------
        GUILayout.Space(4f);

        // SpeedManager.SetSpeed срабатывает только в состоянии Playing — вне забега
        // кнопки гасим, иначе выглядит как будто чит сломан.
        bool playing = GameStateManager.Instance != null
                       && GameStateManager.Instance.Is(GameState.Playing);
        GUILayout.Label(playing ? "Скорость игры" : "Скорость игры (только во время забега)");

        GUI.enabled = playing;
        GUILayout.BeginHorizontal();
        foreach (float s in new[] { 1f, 2f, 3f, 5f, 10f })
            if (GUILayout.Button("×" + s)) SetSpeed(s);
        GUILayout.EndHorizontal();
        GUI.enabled = true;

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Убить всех врагов")) KillAllEnemies();
        if (GUILayout.Button("Заспавнить босса")) SpawnBoss();
        GUILayout.EndHorizontal();

        // ---------- Контент ----------
        GUILayout.Space(6f);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Фильтр", GUILayout.Width(48f));
        filter = GUILayout.TextField(filter);
        if (GUILayout.Button("×", GUILayout.Width(24f))) filter = "";
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Случайное оружие")) GiveRandomWeapon();
        if (GUILayout.Button("Случайное улучшение")) GiveRandomUpgrade();
        GUILayout.EndHorizontal();

        DrawWeapons();
        DrawUpgrades();

        GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
    }

    private string StateLine()
    {
        string hp = health != null
            ? Mathf.RoundToInt(health.CurrentHealth) + "/" + Mathf.RoundToInt(health.MaxHealth)
            : "—";
        string gold = GoldManager.Instance != null ? GoldManager.Instance.currentGold.ToString() : "—";
        string state = GameStateManager.Instance != null ? GameStateManager.Instance.CurrentState.ToString() : "—";

        var spawner = FindFirstObjectByType<EnemySpawner>();
        string wave = spawner != null ? spawner.CurrentWaveNumber.ToString() : "—";

        return $"Состояние: {state}   HP: {hp}   Золото: {gold}   Волна: {wave}";
    }

    private void DrawWeapons()
    {
        if (shop == null || shop.availableWeapons == null) return;

        GUILayout.Label("Оружие");
        weaponsScroll = GUILayout.BeginScrollView(weaponsScroll, GUILayout.Height(160f));
        foreach (var w in shop.availableWeapons)
        {
            if (w == null || !Matches(w.name)) continue;
            if (GUILayout.Button($"{w.name}   [{w.itemTier}]  {w.damageType}"))
                GiveWeapon(w);
        }
        GUILayout.EndScrollView();
    }

    private void DrawUpgrades()
    {
        if (shop == null || shop.availableUpgrades == null) return;

        GUILayout.Label("Улучшения");
        upgradesScroll = GUILayout.BeginScrollView(upgradesScroll, GUILayout.Height(160f));
        foreach (var u in shop.availableUpgrades)
        {
            if (u == null || !Matches(u.name)) continue;
            if (GUILayout.Button($"{u.name}   [{u.itemTier}]"))
                GiveUpgrade(u);
        }
        GUILayout.EndScrollView();
    }

    private bool Matches(string name)
    {
        return string.IsNullOrEmpty(filter)
            || name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // ===================== ДЕЙСТВИЯ =====================

    // ---------- Лобби ----------

    private static string LobbyLine()
    {
        string backend = Lobbies.IsSteamBacked ? "Steam" : "ЗАГЛУШКА (Steam не поднялся)";
        var service = Lobbies.Service;

        return service.IsInLobby
            ? $"Лобби [{backend}]: код {service.Code}, участников {service.Members.Count}"
            : $"Лобби [{backend}]: не в лобби";
    }

    private void CreateLobby()
    {
        lobbyStatus = "Создаём…";
        Lobbies.Service.Create(result =>
        {
            lobbyStatus = Describe(result);
            if (result == LobbyResult.Ok)
                lobbyCodeInput = Lobbies.Service.Code;
        });
    }

    private void JoinLobby()
    {
        lobbyStatus = "Ищем лобби…";
        Lobbies.Service.JoinByCode(lobbyCodeInput, result => lobbyStatus = Describe(result));
    }

    private void LeaveLobby()
    {
        Lobbies.Service.Leave();
        lobbyStatus = "Вышли из лобби.";
    }

    private static string Describe(LobbyResult result)
    {
        switch (result)
        {
            case LobbyResult.Ok: return "Готово.";
            case LobbyResult.NoSteam: return "Steam недоступен — работает заглушка.";
            case LobbyResult.InvalidCode: return "Код неверного формата: нужно 6 символов из алфавита.";
            case LobbyResult.NotFound: return "Лобби с таким кодом нет (или версия сборки другая).";
            default: return "Не удалось: лобби закрыто, заполнено или отказал Steam.";
        }
    }

    private void AddGold(int amount)
    {
        if (GoldManager.Instance != null)
            GoldManager.Instance.AddGold(amount, GoldSource.Other);
    }

    private void SetSpeed(float speed)
    {
        if (SpeedManager.Instance != null)
            SpeedManager.Instance.SetSpeed(speed);
    }

    /// <summary>Выдаёт оружие тем же путём, что и магазин, — со всеми побочными эффектами.</summary>
    private void GiveWeapon(WeaponDefinition weapon)
    {
        if (tower == null) Rebind();
        if (tower == null || weapon == null) return;

        tower.AddWeapon(weapon);

        if (UpgradesManager.Instance != null)
            UpgradesManager.Instance.RegisterWeaponPurchase(weapon);
        if (PurchaseHistoryManager.Instance != null)
            PurchaseHistoryManager.Instance.AddWeapon(weapon);
    }

    private void GiveUpgrade(UpgradeBaseSO upgrade)
    {
        if (upgrade == null || UpgradesManager.Instance == null) return;

        UpgradesManager.Instance.ApplyUpgrade(upgrade);

        if (PurchaseHistoryManager.Instance != null)
            PurchaseHistoryManager.Instance.AddUpgrade(upgrade);
    }

    private void GiveRandomWeapon()
    {
        var list = shop != null ? shop.availableWeapons : null;
        if (list == null || list.Count == 0) return;
        GiveWeapon(list[Random.Range(0, list.Count)]);
    }

    private void GiveRandomUpgrade()
    {
        var list = shop != null ? shop.availableUpgrades : null;
        if (list == null || list.Count == 0) return;
        GiveUpgrade(list[Random.Range(0, list.Count)]);
    }

    private void KillAllEnemies()
    {
        if (EnemyManager.Instance == null) return;

        // Копия списка: смерть врага дергает UnregisterEnemy и меняет исходную коллекцию.
        List<Enemy> all = EnemyManager.Instance.GetEnemiesInRange(Vector3.zero, 100000f);
        foreach (var e in all)
        {
            if (e != null)
                e.TakeDamage(999999f);
        }
    }

    private void SpawnBoss()
    {
        var boss = FindFirstObjectByType<BossManager>();
        if (boss != null)
            boss.SpawnRandomBoss();
    }
}

#endif
