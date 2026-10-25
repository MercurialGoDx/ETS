# -*- coding: utf-8 -*-
"""
Генератор docs/MobilePlan.html — визуальный трекер плана мобильного порта из docs/MobilePlan.md.

Источник правды — MobilePlan.md.
Прогресс считается по чекбоксам `- [x]` / `- [ ]`, текущая фаза — первая незавершённая.
После любой правки MobilePlan.md (отметили пункт, добавили фазу) просто перезапустить:

    python3 tools/gen_plan_html.py

и обновить страницу в браузере. Ничего, кроме стандартной библиотеки Python, не нужно.
"""
import datetime
import html
import os
import re

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(REPO, "docs", "MobilePlan.md")
OUT = os.path.join(REPO, "docs", "MobilePlan.html")
ARCHIVE_MARK = "# Архив предыдущих задач"

PHASE_RE = re.compile(r"^Фаза\s+(\d+)\s+—\s+(.*)$")
CHECK_RE = re.compile(r"^- \[([ xX])\]\s+(.*)$")


# ---------- парсинг ----------

def parse(text):
    """Возвращает (title, intro_notes, sections). Секция: {title, items}; item: {kind, text, checked}."""
    text = text.split(ARCHIVE_MARK)[0]
    title, intro, sections, cur = "", [], [], None
    for raw in text.splitlines():
        line = raw.rstrip()
        if not line.strip() or line.strip() == "---":
            continue
        if line.startswith("# "):
            title = line[2:].strip()
        elif line.startswith("## "):
            cur = {"title": line[3:].strip(), "items": []}
            sections.append(cur)
        elif line.startswith("### ") and cur is not None:
            cur["items"].append({"kind": "sub", "text": line[4:].strip()})
        elif line.startswith(">"):
            note = line.lstrip(">").strip()
            target = cur["items"] if cur is not None else intro
            labeled = re.match(r"^[А-ЯЁA-Z][^:]{0,30}:", note)  # «Статус:», «Стратегия:» — новая заметка
            if target and target[-1].get("kind") == "note" and not labeled:
                target[-1]["text"] += " " + note
            else:
                target.append({"kind": "note", "text": note})
        elif CHECK_RE.match(line) and cur is not None:
            m = CHECK_RE.match(line)
            cur["items"].append({"kind": "check", "checked": m.group(1) != " ", "text": m.group(2)})
        elif line.startswith("- ") and cur is not None:
            cur["items"].append({"kind": "bullet", "text": line[2:]})
        elif raw[:1] == " " and cur is not None and cur["items"]:
            cur["items"][-1]["text"] += " " + line.strip()  # перенос строки внутри пункта
        elif cur is not None:
            cur["items"].append({"kind": "text", "text": line.strip()})
    return title, intro, sections


def inline(s):
    """Минимальный Markdown → HTML: `code`, **bold**, _italic_."""
    out = []
    for i, part in enumerate(re.split(r"(`[^`]*`)", s)):
        if i % 2:
            out.append("<code>%s</code>" % html.escape(part[1:-1]))
            continue
        part = html.escape(part)
        part = re.sub(r"\*\*(.+?)\*\*", r"<strong>\1</strong>", part)
        part = re.sub(r"(?<!\w)_(.+?)_(?!\w)", r"<em>\1</em>", part)
        out.append(part)
    return "".join(out)


# ---------- рендер ----------

def checks(items):
    c = [i for i in items if i["kind"] == "check"]
    return sum(i["checked"] for i in c), len(c)


def render_items(items):
    parts, open_list = [], False
    for it in items:
        k = it["kind"]
        if k in ("check", "bullet"):
            if not open_list:
                parts.append('<ul class="list">')
                open_list = True
            if k == "check":
                verify = it["text"].startswith("Проверка")
                cls = "item done" if it["checked"] else "item"
                if verify:
                    cls += " verify"
                mark = '<span class="box" aria-hidden="true"></span>'
                state = "выполнено" if it["checked"] else "не выполнено"
                parts.append('<li class="%s"><span class="sr">%s: </span>%s<span class="txt">%s</span></li>'
                             % (cls, state, mark, inline(it["text"])))
            else:
                parts.append('<li class="item plain"><span class="dot" aria-hidden="true"></span>'
                             '<span class="txt">%s</span></li>' % inline(it["text"]))
            continue
        if open_list:
            parts.append("</ul>")
            open_list = False
        if k == "sub":
            parts.append('<h4 class="sub">%s</h4>' % inline(it["text"]))
        elif k == "note":
            parts.append('<p class="note">%s</p>' % inline(it["text"]))
        else:
            parts.append('<p class="para">%s</p>' % inline(it["text"]))
    if open_list:
        parts.append("</ul>")
    return "\n".join(parts)


def build(title, intro, sections):
    phases, others = [], {}
    for s in sections:
        m = PHASE_RE.match(s["title"])
        if m:
            done, total = checks(s["items"])
            manual = any(i["kind"] == "note" and "Делаешь сам" in i["text"] for i in s["items"])
            phases.append({"n": int(m.group(1)), "name": m.group(2), "items": s["items"],
                           "done": done, "total": total, "manual": manual})
        else:
            others[s["title"].split(" (")[0]] = s

    current = next((p["n"] for p in phases if p["done"] < p["total"]), None)
    for p in phases:
        if p["total"] and p["done"] == p["total"]:
            p["state"], p["label"] = "done", "Готово"
        elif p["n"] == current:
            p["state"], p["label"] = "current", "В работе" if p["done"] else "Следующая"
        else:
            p["state"], p["label"] = "todo", "Впереди"

    all_done = sum(p["done"] for p in phases)
    all_total = sum(p["total"] for p in phases)
    pct = round(100 * all_done / all_total) if all_total else 0
    phases_done = sum(p["state"] == "done" for p in phases)
    cur = next((p for p in phases if p["n"] == current), None)
    dec = others.get("Решения")
    dec_done, dec_total = checks(dec["items"]) if dec else (0, 0)
    status = next((n["text"] for n in intro if n["text"].startswith("Статус")), "")
    strategy = next((n["text"] for n in intro if n["text"].startswith("Стратегия")), "")
    now = datetime.datetime.now().strftime("%d.%m.%Y %H:%M")

    # дорожка фаз
    track = []
    for p in phases:
        w = 100 * p["done"] / p["total"] if p["total"] else 0
        track.append(
            '<a class="stop %s" href="#phase-%d" title="Фаза %d — %s: %d/%d">'
            '<span class="stop-n">%d%s</span><span class="stop-name">%s</span>'
            '<span class="stop-bar"><span style="width:%.0f%%"></span></span></a>'
            % (p["state"], p["n"], p["n"], html.escape(p["name"]), p["done"], p["total"],
               p["n"], '<i class="self-dot" title="Делаешь сам"></i>' if p["manual"] else "",
               inline(re.sub(r"\s*\(.*?\)", "", p["name"])), w))

    # карточки фаз
    cards = []
    for p in phases:
        w = 100 * p["done"] / p["total"] if p["total"] else 0
        cards.append(
            '<details class="phase %s" id="phase-%d"%s>'
            '<summary><span class="ph-n">%02d</span>'
            '<span class="ph-title">%s%s</span>'
            '<span class="ph-meta"><span class="chip %s">%s</span>'
            '<span class="ph-count">%d/%d</span>'
            '<span class="ph-bar"><span style="width:%.0f%%"></span></span></span></summary>'
            '<div class="ph-body">%s</div></details>'
            % (p["state"], p["n"], " open" if p["state"] == "current" else "", p["n"],
               inline(p["name"]), ' <span class="self">делаешь сам</span>' if p["manual"] else "",
               p["state"], p["label"], p["done"], p["total"], w,
               render_items(p["items"])))

    def side(key, extra=""):
        s = others.get(key)
        if not s:
            return ""
        return ('<section class="panel %s"><h2>%s</h2>%s</section>'
                % (extra, inline(s["title"]), render_items(s["items"])))

    cur_html = ("Фаза %d — %s" % (cur["n"], inline(cur["name"]))) if cur else "Все фазы завершены"

    return TEMPLATE.format(
        page_title="ETS Android-порт",
        title=inline(re.sub(r"^Задача:\s*", "", title)[:1].upper() + re.sub(r"^Задача:\s*", "", title)[1:]),
        status=inline(status.replace("Статус:", "").strip()),
        strategy=inline(strategy),
        pct=pct, all_done=all_done, all_total=all_total,
        phases_done=phases_done, phases_total=len(phases),
        current=cur_html,
        dec_done=dec_done, dec_total=dec_total,
        track="\n".join(track),
        cards="\n".join(cards),
        decisions=side("Решения", "decisions"),
        principles=side("Принципы миграции"),
        risks=side("Риски", "risks"),
        audit=side("Исходное состояние"),
        review=side("Review"),
        generated=now,
    )


TEMPLATE = """<!doctype html>
<html lang="ru">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, viewport-fit=cover">
<title>{page_title}</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Golos+Text:wght@400;500;600&family=Unbounded:wght@500;700&family=JetBrains+Mono:wght@400;500&display=swap">
<style>
/* Layout: шапка со сводкой → дорожка фаз 0–12 (как прогресс сборки) → фазы-аккордеоны слева, решения/риски справа */
:root {{
  --bg: #eef1f4; --surface: #ffffff; --surface-2: #f6f8fa; --fg: #16202b; --muted: #5a6877;
  --line: #d8dfe6; --accent: #0d7480; --accent-soft: #d5eef0; --done: #2e8a4f; --done-soft: #dcf0e2;
  --verify: #8a5a00; --code-bg: #e8edf2;
  --f-display: "Unbounded", "Golos Text", system-ui, sans-serif;
  --f-body: "Golos Text", system-ui, -apple-system, "Segoe UI", Roboto, sans-serif;
  --f-mono: "JetBrains Mono", ui-monospace, "Cascadia Code", Consolas, monospace;
}}
@media (prefers-color-scheme: dark) {{ :root:not([data-theme="light"]) {{
  --bg: #0f151b; --surface: #17202a; --surface-2: #1c2632; --fg: #e4eaf0; --muted: #93a2b2;
  --line: #2a3644; --accent: #4cc2cc; --accent-soft: #143a40; --done: #5cc483; --done-soft: #17362a;
  --verify: #e0b45a; --code-bg: #243142; color-scheme: dark; }} }}
:root[data-theme="dark"] {{
  --bg: #0f151b; --surface: #17202a; --surface-2: #1c2632; --fg: #e4eaf0; --muted: #93a2b2;
  --line: #2a3644; --accent: #4cc2cc; --accent-soft: #143a40; --done: #5cc483; --done-soft: #17362a;
  --verify: #e0b45a; --code-bg: #243142; color-scheme: dark; }}
* {{ box-sizing: border-box; }}
body {{ margin: 0; background: var(--bg); color: var(--fg); font: 15px/1.55 var(--f-body);
  padding-left: max(16px, env(safe-area-inset-left, 0px)); padding-right: max(16px, env(safe-area-inset-right, 0px));
  padding-block: 28px 48px; }}
.wrap {{ max-width: 1240px; margin: 0 auto; display: grid; gap: 22px; }}
h1, h2 {{ font-family: var(--f-display); text-wrap: balance; margin: 0; }}
h1 {{ overflow-wrap: anywhere; hyphens: auto; font-size: clamp(20px, 2.4vw, 26px); font-weight: 700; line-height: 1.2; }}
h2 {{ font-size: 15px; font-weight: 500; letter-spacing: .02em; }}
code {{ font: 0.86em var(--f-mono); background: var(--code-bg); padding: 1px 5px; border-radius: 4px; overflow-wrap: anywhere; }}
.eyebrow {{ font: 500 11px var(--f-mono); letter-spacing: .12em; text-transform: uppercase; color: var(--muted); }}
.sr {{ position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); }}

/* шапка */
header {{ display: grid; gap: 14px; }}
.status {{ color: var(--muted); max-width: 80ch; margin: 0; }}
.stats {{ display: grid; grid-template-columns: repeat(auto-fit, minmax(min(165px, 100%), 1fr)); gap: 12px; }}
.stat {{ background: var(--surface); border: 1px solid var(--line); border-radius: 10px; padding: 14px 16px; display: grid; gap: 4px; min-width: 0; }}
.stat b {{ overflow-wrap: anywhere; hyphens: auto;  font: 600 22px var(--f-body); font-variant-numeric: tabular-nums; }}
.stat .big {{ font-family: var(--f-display); font-size: 26px; color: var(--accent); }}
.stat span:last-child {{ color: var(--muted); font-size: 13px; }}
.meter {{ height: 6px; background: var(--line); border-radius: 3px; overflow: hidden; }}
.meter > span {{ display: block; height: 100%; background: var(--accent); }}

/* дорожка фаз */
.track-wrap {{ overflow-x: auto; padding-bottom: 4px; }}
.track {{ display: grid; grid-template-columns: repeat(auto-fill, minmax(118px, 1fr)); gap: 6px; }}
.stop {{ min-width: 0; display: grid; grid-template-columns: minmax(0, 1fr); gap: 6px; padding: 10px; border-radius: 8px; text-decoration: none; color: var(--fg);
  background: var(--surface); border: 1px solid var(--line); align-content: start; }}
.stop:hover, .stop:focus-visible {{ border-color: var(--accent); outline: none; }}
.stop-n {{ font: 700 18px var(--f-display); color: var(--muted); }}
.stop-name {{ font-size: 11.5px; line-height: 1.3; color: var(--muted); hyphens: auto; overflow-wrap: break-word; }}
.stop-name code {{ font-size: 11px; padding: 0 3px; }}
.stop-bar, .ph-bar {{ height: 4px; background: var(--line); border-radius: 2px; overflow: hidden; }}
.stop-bar > span, .ph-bar > span {{ display: block; height: 100%; background: var(--done); }}
.stop.done {{ background: var(--done-soft); border-color: transparent; }}
.stop.done .stop-n {{ color: var(--done); }}
.stop.current {{ border: 2px solid var(--accent); background: var(--accent-soft); }}
.stop.current .stop-n, .stop.current .stop-name {{ color: var(--fg); }}

/* основная сетка */
.main {{ display: grid; grid-template-columns: minmax(0, 1fr) 380px; gap: 22px; align-items: start; }}
@media (max-width: 980px) {{ .main {{ grid-template-columns: minmax(0, 1fr); }} }}
.col {{ display: grid; gap: 10px; min-width: 0; }}
.toolbar {{ display: flex; flex-wrap: wrap; gap: 8px; align-items: center; justify-content: space-between; }}
.toolbar .btns {{ display: flex; gap: 8px; flex-wrap: wrap; }}
button {{ font: 500 13px var(--f-body); color: var(--fg); background: var(--surface); border: 1px solid var(--line);
  border-radius: 6px; padding: 6px 12px; cursor: pointer; }}
button:hover {{ border-color: var(--accent); }}
button:focus-visible, summary:focus-visible {{ outline: 2px solid var(--accent); outline-offset: 2px; }}
button[aria-pressed="true"] {{ background: var(--accent-soft); border-color: var(--accent); }}

/* фазы */
.phase {{ background: var(--surface); border: 1px solid var(--line); border-radius: 10px; scroll-margin-top: 16px; }}
.phase.current {{ border: 2px solid var(--accent); }}
.phase summary {{ list-style: none; cursor: pointer; display: grid; grid-template-columns: auto minmax(0, 1fr) auto;
  gap: 14px; align-items: center; padding: 14px 16px; }}
.phase summary::-webkit-details-marker {{ display: none; }}
.ph-n {{ font: 700 20px var(--f-display); color: var(--muted); font-variant-numeric: tabular-nums; }}
.phase.current .ph-n {{ color: var(--accent); }}
.phase.done .ph-n {{ color: var(--done); }}
.ph-title {{ font-weight: 600; font-size: 16px; min-width: 0; }}
.ph-meta {{ display: grid; grid-template-columns: auto auto 80px; gap: 10px; align-items: center; }}
.ph-count {{ font: 500 13px var(--f-mono); color: var(--muted); font-variant-numeric: tabular-nums; min-width: 42px; text-align: right; }}
@media (max-width: 560px) {{ .phase summary {{ grid-template-columns: auto minmax(0, 1fr); }}
  .ph-meta {{ grid-column: 1 / -1; grid-template-columns: auto auto minmax(60px, 1fr); }} }}
.chip {{ font: 500 11px var(--f-mono); letter-spacing: .06em; text-transform: uppercase; padding: 3px 8px; border-radius: 999px;
  background: var(--surface-2); color: var(--muted); border: 1px solid var(--line); white-space: nowrap; }}
.chip.current {{ background: var(--accent); color: var(--surface); border-color: var(--accent); }}
.chip.done {{ background: var(--done-soft); color: var(--done); border-color: transparent; }}
.ph-body {{ padding: 0 16px 16px 16px; border-top: 1px solid var(--line); display: grid; gap: 8px; padding-top: 12px; }}
.list {{ list-style: none; margin: 0; padding: 0; display: grid; gap: 7px; }}
.item {{ display: grid; grid-template-columns: 18px minmax(0, 1fr); gap: 10px; align-items: start; }}
.box {{ width: 16px; height: 16px; margin-top: 3px; border: 1.5px solid var(--muted); border-radius: 4px; position: relative; }}
.item.done .box {{ background: var(--done); border-color: var(--done); }}
.item.done .box::after {{ content: ""; position: absolute; left: 4.5px; top: 1px; width: 4px; height: 8px;
  border: solid var(--surface); border-width: 0 2px 2px 0; transform: rotate(45deg); }}
.item.done .txt {{ color: var(--muted); text-decoration: line-through; text-decoration-color: var(--line); }}
.item.verify .txt {{ color: var(--verify); font-weight: 500; }}
.item.verify .box {{ border-color: var(--verify); border-style: dashed; }}
.self {{ font: 500 10px var(--f-mono); letter-spacing: .08em; text-transform: uppercase; color: var(--verify);
  border: 1px solid currentColor; border-radius: 4px; padding: 1px 6px; margin-left: 6px; vertical-align: 2px; white-space: nowrap; }}
.self-dot {{ display: inline-block; width: 7px; height: 7px; border-radius: 50%; background: var(--verify); margin-left: 6px; vertical-align: 4px; }}
.dot {{ width: 6px; height: 6px; border-radius: 50%; background: var(--muted); margin: 9px 0 0 5px; }}
.txt {{ min-width: 0; overflow-wrap: anywhere; }}
.sub {{ margin: 8px 0 0; font: 500 11px var(--f-mono); letter-spacing: .1em; text-transform: uppercase; color: var(--muted); }}
.note {{ margin: 0; padding: 8px 12px; background: var(--surface-2); border-radius: 6px; color: var(--muted); font-size: 14px; }}
.para {{ margin: 0; color: var(--muted); font-size: 14px; }}
body.hide-done .phase.done {{ display: none; }}
.decisions .item.done .txt {{ color: var(--fg); text-decoration: none; }}

/* правая колонка */
.panel {{ background: var(--surface); border: 1px solid var(--line); border-radius: 10px; padding: 16px; display: grid; gap: 12px; min-width: 0; font-size: 14px; }}
.panel.risks .dot {{ background: var(--verify); }}
details.more > summary {{ cursor: pointer; color: var(--muted); font-size: 13px; padding: 4px 0; }}
footer {{ color: var(--muted); font-size: 12px; }}
@media (prefers-reduced-motion: no-preference) {{ .stop, button {{ transition: border-color .15s; }} }}
</style>
</head>
<body>
<div class="wrap">
  <header>
    <span class="eyebrow">Endless Tower Survivors · план разработки</span>
    <h1>{title}</h1>
    <p class="status">{status}</p>
    <p class="status">{strategy}</p>
    <div class="stats">
      <div class="stat"><span class="eyebrow">Общий прогресс</span><b class="big">{pct}%</b>
        <div class="meter"><span style="width:{pct}%"></span></div><span>{all_done} из {all_total} пунктов</span></div>
      <div class="stat"><span class="eyebrow">Текущий этап</span><b>{current}</b><span>первая незавершённая фаза</span></div>
      <div class="stat"><span class="eyebrow">Фазы</span><b>{phases_done} / {phases_total}</b><span>завершено</span></div>
      <div class="stat"><span class="eyebrow">Решения</span><b>{dec_done} / {dec_total}</b><span>принято</span></div>
    </div>
  </header>

  <nav class="track-wrap" aria-label="Фазы">
    <div class="track">
{track}
    </div>
  </nav>

  <div class="main">
    <div class="col">
      <div class="toolbar">
        <h2>Фазы</h2>
        <div class="btns">
          <button type="button" id="btn-expand">Развернуть все</button>
          <button type="button" id="btn-collapse">Свернуть все</button>
          <button type="button" id="btn-hide" aria-pressed="false">Скрыть завершённые</button>
        </div>
      </div>
{cards}
    </div>
    <aside class="col">
{decisions}
{risks}
{principles}
      <details class="more"><summary>Исходный аудит проекта</summary>
{audit}
      </details>
{review}
    </aside>
  </div>
  <footer>Сгенерировано из <code>docs/MobilePlan.md</code> · {generated} · обновить: <code>python3 tools/gen_plan_html.py</code></footer>
</div>
<script>
(function () {{
  var phases = document.querySelectorAll("details.phase");
  document.getElementById("btn-expand").onclick = function () {{ phases.forEach(function (d) {{ d.open = true; }}); }};
  document.getElementById("btn-collapse").onclick = function () {{ phases.forEach(function (d) {{ d.open = false; }}); }};
  var hide = document.getElementById("btn-hide");
  function setHide(v) {{ document.body.classList.toggle("hide-done", v); hide.setAttribute("aria-pressed", v); }}
  try {{ setHide(localStorage.getItem("ets-plan-hide-done") === "1"); }} catch (e) {{}}
  hide.onclick = function () {{
    var v = hide.getAttribute("aria-pressed") !== "true"; setHide(v);
    try {{ localStorage.setItem("ets-plan-hide-done", v ? "1" : "0"); }} catch (e) {{}}
  }};
  document.querySelectorAll(".stop").forEach(function (a) {{
    a.addEventListener("click", function () {{
      var d = document.querySelector(a.getAttribute("href")); if (d) d.open = true;
    }});
  }});
}})();
</script>
</body>
</html>
"""


def main():
    with open(SRC, encoding="utf-8") as f:
        title, intro, sections = parse(f.read())
    page = build(title, intro, sections)
    with open(OUT, "w", encoding="utf-8") as f:
        f.write(page)
    print("OK:", os.path.relpath(OUT, REPO))


if __name__ == "__main__":
    main()
