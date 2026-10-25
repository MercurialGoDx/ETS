# Задача: порт на Android (одиночная игра + онлайн-лидерборд) + техстек (VContainer, UniTask, Addressables, пулинг, DOTween, Firebase)

> Статус: **план, реализация не начата** (2026-09-29). Перед стартом каждой фазы — сверка с пользователем.
> Визуальный трекер: `docs/MobilePlan.html` (пересобрать после правок: `python3 tools/gen_plan_html.py`).
> Стратегия: **рефакторинг текущего проекта**, не новый проект. Один проект — два таргета
> (Windows/Steam и Mobile), платформенное — через Build Profiles + scripting defines + platform overrides.

## Исходное состояние (аудит 2026-09-29)
- Unity 6000.0.80f1, URP 17. `Mobile_RPAsset`/`Mobile_Renderer` + Quality `Mobile` уже есть. Сцена одна — `MainScene`.
- 226 скриптов (~23.7k строк), всё в `Assembly-CSharp`, **нет asmdef и тестов**.
- **17 синглтонов** `static Instance` (GameStateManager, GoldManager, EnemyManager, PoolManager, UpgradeManager,
  SpeedManager, AudioManager, PlayerHealth, DamageStatsManager, ShopUnlockService, AchievementManager,
  SteamManager, …), **~321 обращение** `.Instance`.
- Корутины: ~13 `IEnumerator`; `timeScale` используется в ~25 местах (скорость x1/x2/x3 + пауза).
- Анимации UI вручную (`Lerp/SmoothDamp` ~29 мест, `PressOffsetUI`, `ButtonState`).
- Пул: `PoolObject/` (свой `ObjectPool<T>` + `PoolManager`, ключ — prefab), но VFX/снаряды в горячих путях
  идут через `Instantiate`: `OnHitVFX`, `LightingChainBullet`, `AuraDamageZone`, `RandomSpawnPortal`,
  `EnemyRangedAttack`, `EnemyStatusEffectController`, `EnemyEffectManager`, `RegenAuraDamage`.
- Addressables 2.9.1 уже в проекте транзитивно (через Localization). В группах остались мёртвые
  таблицы zh/fr/ja/ko. `Resources.Load<BalanceConfig>` в `BalanceService`.
- Ввод: legacy `Input.*` в 7 рабочих файлах (`CameraZoom`, `ShopManager`, `InventoryUI`, `GameSpeedScripting`,
  `PauseMenu`, `WeaponTooltip`, `BuildViewerUI`, `ButtonState`), `activeInputHandler: 2` (Both).
- Hover-тултипы (`IPointerEnterHandler`) в ~10 UI-скриптах. Canvas 1920×1080/2560×1440 (+ опечатка 2580×1440).
- Steam уже изолирован `#if !DISABLESTEAMWORKS`, лидерборд — за `ILeaderboardService`.
- Сохранения — `PlayerPrefs`; свой `CrashReporter` (файлы в persistentDataPath).
- Ассеты: AssetStore 615 МБ, Sound 361 МБ, Texture 184 МБ.

## Решения
- [x] **DI: VContainer** (утверждено 2026-09-29)
- [x] **Платформа: только Android** (2026-10-25: iOS исключён — платная подписка Apple Developer
      Program $99/год). Код остаётся без Android-специфики за пределами платформенных сервисов,
      так что к iOS можно вернуться позже без переделок.
- [x] **Ориентация: только landscape** (Landscape Left/Right, autorotate между ними)
- [x] **Без монетизации** (ни рекламы, ни IAP; UMP-согласие нужно только под аналитику)
- [x] **Только одиночная игра, без лобби/мультиплеера.** Аудит: сетевого кода в проекте нет
      (Netcode/Lobby/Matchmaking отсутствуют), есть лишь пакет `com.unity.multiplayer.center` — удалить.
      Единственная онлайн-функция — **таблица лидеров** (асинхронная: отправка рекорда + чтение топа).
- [x] **Лидерборд — Firebase Auth (анонимный) + Firestore** (утверждено 2026-10-25).
      Почему не Google Play Games: к записи GPGS можно приложить лишь `scoreTag` ≤ 64 символа, а наш
      `BuildSnapshot` — до 64 int → просмотр билда игрока (`BuildViewerUI`) потерялся бы. Firestore хранит
      снимок целиком, существующий UI лидерборда переиспользуется, Firebase всё равно в стеке.
- [x] **Firebase-модули: Analytics + Crashlytics + Remote Config + Auth + Firestore.** Облачных сейвов нет.
- [x] **Addressables — только локальные группы** (всё внутри билда). Удалённый каталог требует хостинга
      (CDN), версионирования каталога и даёт выигрыш только при частых контент-апдейтах без релиза — для
      нас не нужен. Ценность Addressables здесь — загрузка/выгрузка по требованию (память на слабых телефонах).
      Переход на удалённый каталог позже возможен без переделки кода (меняется только профиль групп).
- [x] **Достижений на мобилках нет.** `AchievementManager` (Steam) живёт только в PC-сборке;
      на мобилках — `NullAchievementService`.
- [x] **Ветки (2026-10-25):** `mobile` — основная ветка мобильной разработки. Фича-ветки создаются от
      `mobile` и мержатся в `mobile`. `main` заморожена на последнем PC/Steam-состоянии и больше не
      трогается (никаких merge в обе стороны). Подробно — `docs/Git.md`.
- [x] **Режим работы — обучение (2026-10-25):** VContainer, UniTask, пулинг и профилирование, Addressables,
      Firebase пользователь внедряет **сам**. Claude объясняет: зачем это делаем, какие преимущества даёт,
      как сделать по шагам, на что смотреть при проверке — и формулирует, как это объяснить на собеседовании.
      Остальное (UI, ввод, ассеты, сборка) Claude может делать сам по согласованию.

## Принципы миграции
- Постепенно (strangler): каждая фаза оставляет игру **играбельной на ПК**; между фазами — Play-проверка.
- Сначала инфраструктура и архитектура, потом мобильный UX — иначе новый UI придётся переписывать под DI.
- Горячие объекты (враги, пули, VFX) **не резолвим через DI поштучно** — получают контекст через
  фабрику/пул (`Init(...)`), чтобы не платить за инъекцию на каждый спавн.
- Всё платформенное — за интерфейсами с реализациями под PC/Mobile (+ Null-реализации для редактора/тестов).
- Каждая фаза: `refresh_unity(compile)` + `read_console(error)` чисто, Play-тест, коммит.
- Каждая фаза — своя ветка от `mobile` (`feature/…`, `refactor/…`), после проверки мерж в `mobile`.
- Фазы с пометкой **«Делаешь сам»** — учебные: сначала объяснение (зачем / преимущества / как / вопросы
  с собеседований), потом реализация руками пользователя, Claude — ревью и помощь при ошибках.

---

## Фаза 0 — Подготовка и замер базовой линии
> **Делаешь сам — профилирование:** baseline-замеры (Profiler, Memory Profiler, Frame Debugger на устройстве). Сборку и чистку Claude может сделать сам.
- [x] Ветка `mobile` от `main` (2026-10-25); `main` заморожена, дальше всё мержится в `mobile`
- [ ] Удалить мёртвое: `Assets/Scenes/SampleScene`, `Script/TempScript` (проверить ссылки), Addressables-группы zh/fr/ja/ko
- [ ] Build Profile **Android**: IL2CPP, ARM64, min API 24+, target API — актуальный для Google Play, AAB,
      Mobile URP asset + Quality `Mobile`, define `DISABLESTEAMWORKS;PLATFORM_MOBILE`
- [ ] Первая сборка «как есть» на реальный телефон (слабый/средний Android) — убедиться, что запускается
- [ ] **Baseline профилинг** (PC + телефон), записать цифры сюда: FPS/frametime на 1/3 скорости в поздней волне,
      GC Alloc/кадр, количество Instantiate/Destroy в секунду, draw calls/SetPass, память, размер AAB
- [ ] Тестовый сценарий для замеров: фиксированный seed/чит-сейв «поздняя волна + босс» (через `DebugCheats`)
- [ ] **Замер нагрузки симуляции** (на телефоне, поздняя волна, x1 и x3): максимум одновременно живых врагов
      и снарядов; мс/кадр по категориям — скрипты врагов (`Enemy.Update`), Animator, физика (`FixedUpdate`
      на x3 вызывается втрое чаще), рендер (скиннинг, частицы, overdraw). Это вход для решения в Фазе 5

## Фаза 1 — Пакеты и структура сборок
- [ ] Пакеты: **UniTask** (git/OpenUPM), **VContainer** (git/OpenUPM), **DOTween** (Asset Store / OpenUPM
      `com.demigiant.dotween`) + DOTween Setup (модули UI/TMP), **Addressables** — явно в manifest
- [ ] Defines: `VCONTAINER_UNITASK_INTEGRATION`, `UNITASK_DOTWEEN_SUPPORT`
- [ ] asmdef-разбиение (осторожно: из asmdef нельзя ссылаться на `Assembly-CSharp`, проверить ассеты AssetStore
      со скриптами): `ETS.Core` (сервисы/интерфейсы/данные), `ETS.Gameplay`, `ETS.UI`, `ETS.Platform.Steam`,
      `ETS.Platform.Mobile`, `ETS.Editor`, `ETS.Tests`
- [ ] Минимальный EditMode-тест-проект (баланс-формулы, экономика) — страховка для рефакторинга
- [ ] `link.xml` для IL2CPP stripping (VContainer/Firebase/Localization)
- [ ] Проверка: компиляция чистая, игра на ПК без изменений поведения

## Фаза 2 — DI на VContainer (замена синглтонов)
> **Делаешь сам — VContainer.** Перед стартом: зачем DI вместо синглтонов, Composition Root, lifetime-ы, тестируемость.
- [ ] `RootLifetimeScope` (проект, DontDestroyOnLoad): конфиги, Save, Audio, Analytics, Platform-сервисы
- [ ] `GameLifetimeScope` в `MainScene`: GameState, Gold, Enemy, Upgrade, Speed, Pool, DamageStats, Shop…
- [ ] Выделить интерфейсы ключевых сервисов (`IGoldService`, `IGameStateService`, `ISpeedService`, `IPoolService`,
      `IAudioService`, `ISaveService`, `ILeaderboardService` — уже есть, `IAchievementService`, `IInputService`)
- [ ] Миграция по одному синглтону: регистрация (`RegisterComponentInHierarchy` / `Register<T>`), замена
      `.Instance` на `[Inject]`/конструктор; временный `Instance` как `[Obsolete]`-фасад до полного перевода
- [ ] Порядок: листовые сервисы (Speed, Gold, Audio, DamageStats) → Enemy/Pool → GameState/Upgrade/PlayerHealth
- [ ] Спавн через фабрики (`IObjectResolver.Instantiate` только для UI/редких объектов; враги/пули — через пул, Фаза 4)
- [ ] ScriptableObject-апгрейды (`UpgradeSO`): зависимости передаются через `context`, не через инъекцию в SO
- [ ] Заменить `FindObjectOfType`/`GameObject.Find` (~20 мест) на инъекцию
- [ ] Цель: 0 `static Instance` в игровом коде (кроме `CrashReporter` и bootstrap)
- [ ] Проверка: полный забег на ПК до босса без ошибок; тесты зелёные

## Фаза 3 — UniTask (асинхронность)
> **Делаешь сам — UniTask.** Перед стартом: чем async лучше корутин, отмена через CancellationToken, PlayerLoop, отсутствие аллокаций.
- [ ] Перевести ~13 корутин на `async UniTaskVoid/UniTask` с `destroyCancellationToken`
- [ ] Учесть скорость игры/паузу: игровые задержки — `DelayType.DeltaTime` (зависят от timeScale),
      UI/меню — `ignoreTimeScale: true`/`DelayType.UnscaledDeltaTime`
- [ ] Bootstrap-загрузка (конфиги, Addressables, Firebase init, локализация) — последовательный async pipeline
      с экраном загрузки (VContainer `IAsyncStartable`)
- [ ] Правило: без `async void`; все подписки/таски отменяются при выгрузке сцены
- [ ] Проверка: пауза/x3/рестарт забега не ломают таймеры; нет утечек тасков (UniTask Tracker window)

## Фаза 4 — Пулинг с профилированием
> **Делаешь сам — пулинг и профилирование.** Перед стартом: откуда берутся GC-спайки, как пул их убирает, ProfilerMarker/ProfilerCounter.
- [ ] Единый `IPoolService` (на базе `UnityEngine.Pool.ObjectPool<T>` или доработанного своего), ключ — prefab /
      `AssetReference`; `IPoolable { OnSpawn(); OnDespawn(); }` для сброса состояния
- [ ] Перевести на пул все горячие `Instantiate/Destroy`: VFX попаданий (`OnHitVFX`, `LightingChainBullet`,
      `AuraDamageZone`, `RandomSpawnPortal`), `EnemyRangedAttack`-снаряды, визуалы статусов
      (`EnemyStatusEffectController`, `EnemyEffectManager`, `RegenAuraDamage`), враги, золото/текст урона
- [ ] Авто-возврат для ParticleSystem (`stopAction = Callback`) вместо `Destroy(t)`
- [ ] Prewarm по волне/боссу (данные из конфига), лимиты размера пула, политика при переполнении
- [ ] **Профилирование**: `ProfilerMarker` на Spawn/Despawn, `ProfilerCounter`-ы (active/inactive/created/misses
      по каждому пулу) — видны в Profiler и через `ProfilerRecorder` в релизной сборке
- [ ] Debug-оверлей (dev-build/чит): таблица пулов, GC Alloc/кадр, FPS
- [ ] Убрать аллокации в гейм-цикле: `OverlapSphere` → `NonAlloc`, `GetComponent` в Update → кеш, LINQ/boxing
      в таргетинге `TowerAttack`, строковые форматирования в UI каждый кадр
- [ ] Цель: **0 B GC Alloc/кадр** в бою (кроме UI-событий), 0 Instantiate в steady-state
- [ ] Проверка: сравнение с baseline Фазы 0 (записать цифры до/после)

## Фаза 5 — CPU-оптимизация симуляции (Jobs/Burst — по замерам)
> **Делаешь сам — профилирование и Jobs/Burst.** Шаги идут по возрастанию цены. Каждый следующий — только если профайлер на целевом телефоне после
> предыдущего всё ещё показывает узкое место. Полный DOTS/ECS — **не делаем** (переписывание врагов,
> оружий и эффектов статусов = новый проект).
### Шаг 1 — дёшево, делаем всегда
- [ ] **Update-менеджер**: один `ISimulationTick` (из DI) обходит массивы активных врагов/снарядов вместо
      сотен `Update()` (убирает накладные расходы native→managed вызовов). Регистрация — через пул (Фаза 4)
- [ ] **Высота рельефа без луча**: `SnapToGround` (`Enemy.cs`) — `Terrain.SampleHeight` или запечённая карта
      высот вместо `Physics.Raycast` на каждого врага каждый кадр
- [ ] Враги как кинематические тела, которые двигаются через transform: проверить цену синхронизации
      трансформов с физикой (`autoSyncTransforms`, коллайдеры) — особенно на x3
- [ ] Проверка: мс/кадр скриптов врагов до/после в тестовом сценарии
### Шаг 2 — анимация врагов (если узкое место — Animator/скиннинг)
- [ ] Массовые враги без Animator: анимации запекаются в текстуру (VAT), проигрываются шейдером +
      GPU instancing; Animator остаётся только у боссов
- [ ] Проверка: мс Animator + рендер скиннинга до/после, draw calls
### Шаг 3 — Jobs + Burst (если скрипты врагов всё ещё > ~3–4 мс/кадр на целевом телефоне)
- [ ] Пакеты `com.unity.burst`, `com.unity.collections`, `com.unity.mathematics`; проверить Burst в IL2CPP ARM64-сборке
- [ ] Данные врагов (позиция, скорость, множители замедления, состояние) — в `NativeArray`, владелец —
      менеджер врагов; MonoBehaviour остаётся «представлением»
- [ ] Движение и разворот — `IJobParallelForTransform` + `TransformAccessArray`; привязка к земле —
      карта высот в job или пачка `RaycastCommand`
- [ ] Движение массовых снарядов (ракеты, отскоки) — тот же подход, если их сотни
- [ ] (При тысячах врагов) пространственная сетка (spatial hash) в job для поиска целей и AoE вместо перебора
      `EnemyManager.GetEnemiesInRange`
- [ ] Один sync point за кадр (Schedule в начале кадра, Complete перед использованием), без `Complete()` сразу после Schedule
- [ ] Проверка: мс/кадр до/после, загрузка ядер в Profiler (Timeline), отсутствие регрессий геймплея
  (отбрасывание, замедление, оглушение, ледяная тюрьма)

## Фаза 6 — Addressables
> **Делаешь сам — Addressables.** Перед стартом: чем лучше Resources и прямых ссылок, группы, handle/Release, память.
- [ ] Группы: `Core` (локальная, в билде), `Weapons`, `Enemies+Bosses`, `VFX`, `Audio_Music` (streaming),
      `Audio_SFX`, `UI`, `Localization` (почистить)
- [ ] `BalanceConfig`: `Resources.Load` → Addressables (или оставить в Core с прямой ссылкой — решить)
- [ ] Тяжёлое грузить по требованию: музыка, боссы, VFX ультимейтов, иконки журнала/инвентаря
- [ ] `IAssetProvider` поверх Addressables (async на UniTask, учёт handle/Release, кеш) — интеграция с `IPoolService`
      (пул держит handle, пока жив)
- [ ] Analyze rules: нет дублей ассетов между группами/сценой
- [ ] (Опционально) удалённый каталог + контент-апдейты
- [ ] Проверка: Memory Profiler — ассеты выгружаются после забега; размер AAB vs baseline

## Фаза 7 — Ввод (New Input System)
- [ ] `IInputService` с событиями (Zoom, Pause/Back, SpeedChange, ToggleInventory, Reroll, Buy…)
- [ ] Реализация PC (клавиатура/мышь — текущие хоткеи) и Mobile (EnhancedTouch: pinch-zoom, tap, long-press)
- [ ] Перевести 7 файлов с legacy `Input.*`; `CameraZoom` → pinch; Android «Назад» = Pause/закрыть панель
- [ ] `activeInputHandler` → только New Input System; `InputSystemUIInputModule` в EventSystem
- [ ] Проверка: ПК-управление без регрессий; на телефоне зум/пауза/назад работают

## Фаза 8 — Мобильный UI/UX + DOTween
- [ ] Тултипы: hover → tap-to-inspect / long-press (единый `ITooltipTrigger` c PC-hover и Mobile-tap);
      `WeaponTooltip` — якорь к слоту вместо `Input.mousePosition`
- [ ] Покупка в магазине: «тап = выбор + инфо, повторный тап/кнопка = купить» (защита от случайных покупок)
- [ ] `SafeArea`-компонент на корневые панели; CanvasScaler под мобилки (единый reference, фикс 2580×1440)
- [ ] Хоткеи → экранные кнопки: скорость x1/x2/x3, пауза, инвентарь, реролл
- [ ] Player Settings: только Landscape Left/Right. Раскладки под телефон в landscape, минимум 48dp
      для кнопок, размер шрифтов, скролл инвентаря/журнала/статистики
- [ ] Убрать `ResolutionPreset` из мобильного меню настроек (добавить: качество графики, FPS 30/60, вибрация)
- [ ] **DOTween**: анимации панелей, кнопок, счётчика золота, попапов урона/награды босса — заменить ручные
      Lerp-анимации UI; UI-твины `SetUpdate(true)` (работают на паузе), `SetLink(gameObject)`/Kill при
      уничтожении, `DOTween.SetTweensCapacity` + `SetRecyclable`; UniTask-await твинов в UI-флоу
- [ ] Не использовать DOTween в горячем геймплей-коде на каждого врага/пулю (аллокации) — только UI/редкие эффекты
- [ ] Проверка: скриншоты на 16:9, 19.5:9, 20:9, планшет 4:3; с вырезом; прогон всех экранов

## Фаза 9 — Мобильная производительность и ассеты
- [ ] Platform overrides текстур: ASTC, max 1024–2048, mipmaps где нужно; атласы UI (SpriteAtlas)
- [ ] Аудио: музыка Vorbis + Streaming, SFX ADPCM/Vorbis Decompress On Load, моно где можно
- [ ] URP Mobile: без лишних post-process, shadow distance/cascades, MSAA, render scale (настраиваемый)
- [ ] Шейдеры (`IcePrison*`, `HolyChainLightning`, `BasicPixelation`, частицы): упростить/LOD для мобилок,
      strip неиспользуемых вариантов
- [ ] Частицы: лимиты max particles, меньше overdraw при x3 скорости
- [ ] `Application.targetFrameRate`, Adaptive Performance (термотроттлинг) — по результатам замеров
- [ ] Проверка: стабильные 60 FPS (или 30 на слабых) в тестовом сценарии, нагрев/батарея за 20 минут

## Фаза 10 — Сохранения и жизненный цикл приложения
- [ ] `ISaveService` (JSON в persistentDataPath, версия схемы, атомарная запись); миграция из `PlayerPrefs`
- [ ] Автосейв забега на `OnApplicationPause(true)`/`OnApplicationFocus(false)` + восстановление при запуске
- [ ] Авто-пауза при сворачивании, звук при потере фокуса
- [ ] Проверка: свернуть/убить приложение посреди волны → продолжить с того же места

## Фаза 11 — Firebase
> **Делаешь сам — Firebase.** Перед стартом: что даёт каждый модуль (Crashlytics, Analytics, Remote Config, Auth), как инициализировать, приватность.
- [ ] Firebase-проект, Android-приложение, `google-services.json` (решить: в git или через CI-секрет), EDM4U resolve
- [ ] Вся Firebase-логика в `ETS.Platform.Mobile` за интерфейсами; для PC/редактора — Null/Steam-реализации
- [ ] Init через async bootstrap (Фаза 3): `CheckAndFixDependenciesAsync` до использования
- [ ] **Crashlytics** — поверх/вместо `CrashReporter` на мобилке (логи + custom keys: волна, билд, скорость)
- [ ] **Analytics** — `IAnalyticsService`: run_start/run_end (время, волна, причина), wave_reached, weapon_bought,
      upgrade_bought, reroll, boss_killed, tutorial_step; словарь событий в `docs/`
- [ ] **Remote Config** — оверрайды баланса (`BalanceConfig`) с дефолтами в билде; A/B-готовность
- [ ] **Auth (анонимный вход)** — стабильный uid игрока без регистрации (основа лидерборда)
- [ ] Согласие на сбор данных (GDPR — Analytics/Crashlytics), Privacy Policy + Data safety form для Google Play
- [ ] Проверка: события в DebugView, тестовый краш в консоли Crashlytics, RC-значение применяется

## Фаза 12 — Онлайн-лидерборд (Firestore)
> **Делаешь сам — Firebase Auth + Firestore.** Перед стартом: модель данных, запросы, Security Rules, почему не доверяем клиенту.
> Игра остаётся одиночной: никакого лобби/матчмейкинга/синхронизации. Онлайн — только
> асинхронные «отправить рекорд» и «прочитать топ».
- [ ] Удалить пакет `com.unity.multiplayer.center`
- [ ] Перевести `ILeaderboardService` на UniTask (`UniTask<List<LeaderboardEntry>> GetTopAsync`,
      `UniTask SubmitTimeAsync`) — Steam-реализация адаптируется, UI не меняется
- [ ] `FirestoreLeaderboardService : ILeaderboardService`: коллекция `leaderboard/{uid}` —
      `{ name, timeSeconds, build: int[], appVersion, updatedAt }`; топ = `orderBy(timeSeconds desc).limit(N)`;
      своя позиция — count-запрос `timeSeconds > мой`
- [ ] Отправка только если результат лучше личного рекорда; офлайн — очередь и отправка при появлении сети
- [ ] Ник игрока: экран ввода при первой отправке (длина, фильтр символов/мата), смена в настройках;
      аватар — сгенерированный (инициал/цвет) вместо Steam-аватарки
- [ ] `BuildViewerUI` работает с `build` из Firestore (тот же `BuildSnapshot.Decode`)
- [ ] **Security Rules**: писать только в свой `{uid}`, `timeSeconds` только растёт, валидация типов/размеров
      (`build` ≤ 64, `name` ≤ N символов), `timeSeconds` в правдоподобных пределах
- [ ] Античит (базовый): проверка правдоподобности (время ↔ волна ↔ урон) на клиенте + в rules;
      Cloud Function-валидация / Play Integrity — отложено (требует тарифа Blaze)
- [ ] Регистрация сервиса в VContainer по платформе: PC → `SteamLeaderboardService`, Android → Firestore
- [ ] Выделить `IAchievementService` из `AchievementManager`: PC → Steam, мобилки → `NullAchievementService`
- [ ] **Модерация ников** (UGC — требование политики Google Play): фильтр мата/запрещённых
      слов при вводе, кнопка «пожаловаться» на запись в таблице (коллекция `reports`), возможность скрыть
      запись (флаг `hidden` правится из Firebase Console)
- [ ] Проверка: 2 устройства/эмулятора → оба рекорда в топе, просмотр билда чужого игрока, попытка записать
      в чужой документ / уменьшить время отклоняется rules (Firebase Emulator / Rules Playground)

## Фаза 13 — Релиз Android
- [ ] Keystore, версионирование (versionCode из CI/скрипта), AAB, Play Console: internal testing
- [ ] Иконки/сплэш/скриншоты стора, локализованные описания (en/ru/de/es/pt)
- [ ] Pre-launch report, Android vitals (ANR/краши), тесты на 3–5 устройствах
- [ ] (По желанию) PC-сборка из `mobile` не сломана — Steam-код остаётся за define

## Риски
- Переход на asmdef может сломать ссылки на скрипты из AssetStore-пакетов → делать первым и отдельно.
- DI-миграция затрагивает сотни мест → строго по одному сервису, с Play-проверкой после каждого.
- `Time.timeScale` (x1/x2/x3/пауза) влияет на UniTask/DOTween — явно выбирать scaled/unscaled.
- Firebase + IL2CPP stripping + EDM4U — частый источник проблем сборки → отдельная тестовая сборка сразу после подключения.
- Firebase не для продакшна на Desktop → не включать в PC-сборку.
- Jobs/Burst усложняют код (данные в NativeArray, sync points, отладка) и на телефонах дают меньше, чем на ПК
  (мало мощных ядер) → только по замерам, после Update-менеджера и VAT-анимации.
- Лидерборд без серверной валидации взламываем (клиент пишет свой результат) → rules + проверки
  правдоподобности сейчас, серверная проверка — если читеры станут проблемой.
- Бесплатный тариф Firestore (Spark): 50k чтений/день — кешировать топ (TTL ~1–5 мин), не перечитывать на каждое открытие.

## Review
_(заполняется по мере выполнения фаз: цифры baseline и после, что сделано, отклонения от плана)_
