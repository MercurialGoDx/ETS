# AI Assistant ↔ Unity (MCP) — настройка и эксплуатация

Этот документ описывает, как любой AI-агент (Claude Code, Cursor, Codex, и т.д.)
подключается к **живому Unity Editor** этого проекта через MCP и что он умеет.
Написан как handoff: если сменишь агента — дай ему этот файл.

> TL;DR: используется **MCP for Unity** (CoplayDev, бесплатный OSS). Сервер `mcp-for-unity`
> запускается агентом через **Windows `uvx.exe` из WSL** (interop) и соединяется с
> сокет-мостом Unity на `127.0.0.1:6400`. Сеть WSL↔Windows НЕ задействована.

---

## 1. Окружение (важно — определяет всю схему)

| Параметр | Значение |
|---|---|
| ОС | **Windows 10** (build 19045) — mirrored networking НЕ поддерживается (нужен Win11 22000+) |
| Агент | Claude Code в **WSL2** (сеть = **NAT**), проект на `D:\Unity\Unity_Projects\ETS` |
| Unity | 6000.0.58f2, проект «ETS» |
| Windows-хост из WSL | `192.168.64.1` (для NAT); Windows-дом: `C:\Users\murte` |
| Python/uv | на **Windows**: `C:\Users\murte\.local\bin\uvx.exe` (+ Python 3.12) |
| Unity-пакет | `com.coplaydev.unity-mcp` (git `...unity-mcp.git?path=/MCPForUnity#main`), верс. 9.7.3 |

Ключевое следствие: WSL (NAT) **не достаёт** Windows-овский `127.0.0.1`, а mirrored на Win10
недоступен. Поэтому HTTP-транспорт MCP не годится. Решение — **stdio**: агент сам запускает
сервер как Windows-процесс (через interop), и сервер локально (на Windows) коннектится к Unity.

```
Claude Code (WSL)
   │  stdio (через WSL interop)
   ▼
uvx.exe → mcp-for-unity  (Windows-процесс)
   │  TCP 127.0.0.1:6400  (локально на Windows)
   ▼
Unity Editor  StdioBridgeHost (сокет-мост)
```

---

## 2. Конфигурация клиента (Claude Code) — `.mcp.json`

Лежит в корне проекта `/.mcp.json`:

```json
{
  "mcpServers": {
    "unityMCP": {
      "command": "/mnt/c/Users/murte/.local/bin/uvx.exe",
      "args": ["--from", "mcpforunityserver==9.7.3", "mcp-for-unity", "--default-instance", "ETS"],
      "env": { "MCP_TOOL_TIMEOUT": "720000" }
    }
  }
}
```

Заметки:
- **Сервер = `mcp-for-unity`** (PyPI-дистрибутив `mcpforunityserver`). НЕ путать с
  `coplay-mcp-server` — это другой (облачный) продукт, он использует файловый IPC
  `Temp/Coplay/MCPRequests` и с этим пакетом НЕ работает.
- Версия сервера должна **совпадать с версией Unity-пакета** (`Packages/manifest` → 9.7.3).
  При обновлении пакета — поменяй пин в `--from mcpforunityserver==X.Y.Z`.
- Путь `command` machine-specific (`C:\Users\murte`). На другой машине — поправить.
- Project-scoped сервер требует **approve** в Claude при первом запуске (`/mcp` или диалог на старте).

Другие агенты (Cursor / Codex / VS Code и т.д.): в окне Unity «MCP for Unity» есть
авто-конфигураторы для многих клиентов — проще нажать там «Configure» для нужного агента
(он впишет аналогичную stdio-команду). Под WSL путь к `uvx` указывай через `/mnt/c/...`.

---

## 3. Настройка на стороне Unity (обязательно каждый сеанс)

1. Unity Editor должен быть **ОТКРЫТ** с этим проектом.
2. Окно **Window → MCP for Unity**.
3. Транспорт = **stdio** (НЕ «HTTP Local» и НЕ «HTTP Remote»).
   Только в stdio поднимается сокет-мост на 6400 и пишется файл-регистрация.
4. Нажать **Start**. Статус «No session» — это нормально (сессия появится, когда агент
   подключится).
5. Проверка: должен появиться `C:\Users\murte\.unity-mcp\unity-mcp-status-<hash>.json`
   с `"unity_port":6400`.

После этого в Claude Code инструменты `mcp__unityMCP__*` подхватятся при старте сессии.

---

## 4. Что агент теперь умеет (42 инструмента)

- **Чтение состояния**: `read_console` (логи/ошибки/варнинги), `find_gameobjects`,
  `manage_scene` (иерархия/сцены), `manage_gameobject`, `manage_components`, `get_sha`.
- **Правка проекта**: `create_script` / `manage_script` / `apply_text_edits` /
  `script_apply_edits` / `validate_script`, `manage_asset`, `manage_prefabs`,
  `manage_material`, `manage_shader`, `manage_texture`, `manage_scriptable_object`,
  `manage_ui`, `manage_vfx`, `manage_animation`.
- **Управление редактором**: `execute_menu_item`, `manage_editor` (play/pause/stop,
  теги/слои, undo/redo), `refresh_unity`, `execute_code` (произвольный C# в редакторе),
  `batch_execute`.
- **Тесты и профайлинг**: `run_tests` / `get_test_job` (Unity Test Framework 1.5.1),
  `manage_profiler`, `manage_build`, `manage_physics`, `manage_graphics`, `manage_camera`,
  `manage_probuilder`, `manage_packages`, `unity_docs`, `unity_reflect`.

Практический эффект: агент видит ошибки компиляции/рантайма без copy-paste, может править
скрипты и сразу проверять компиляцию, управлять сценой и гонять тесты — замкнутый цикл.

---

## 5. Траблшутинг (грабли, на которые мы уже наступили)

| Симптом | Причина | Решение |
|---|---|---|
| `No Unity Editor instances found` | Окно Unity в HTTP-режиме (сокет-мост не стартует) | Переключить окно в **stdio** + Start |
| `Requests directory missing: ...Temp\Coplay\MCPRequests` | Запущен НЕ тот сервер (`coplay-mcp-server`) | Использовать `mcp-for-unity` (см. §2) |
| `Unknown tool: get_unity_editor_state` | Имена инструментов от чужого сервера (coplay) | Это `mcp-for-unity`: см. список §4 (`read_console` и т.д.) |
| HTTP `:8080` недоступен из WSL | NAT: WSL ≠ Windows localhost; mirrored нет на Win10 | Не использовать HTTP; только stdio (см. §1) |
| Логи сервера в песочнице Store-Python | `uvx` берёт MS Store Python (виртуализирует `%LOCALAPPDATA%`) | Косметика: discovery в `%USERPROFILE%\.unity-mcp`, не страдает. При желании — `uvx --python-preference only-managed` |

Логи сервера (для диагностики):
`C:\Users\murte\AppData\Local\Packages\PythonSoftwareFoundation.Python.3.12_*\LocalCache\Local\UnityMCP\Logs\unity_mcp_server.log`

Быстрый self-тест без рестарта агента (Python-handshake): см.
`tasks/` историю или повторно собрать probe, который шлёт `initialize` + `tools/call`
`read_console {action:get}` на ту же команду из `.mcp.json`.

---

## 6. Если переезд на Windows 11
Тогда доступен **mirrored networking** (`.wslconfig`: `[wsl2] networkingMode=mirrored`),
и можно перейти на HTTP-транспорт (`http://localhost:8080/mcp`) без stdio-interop. На Win10 — нет.
