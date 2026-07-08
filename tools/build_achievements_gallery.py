#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Собирает HTML-галерею достижений с встроенными (base64) иконками и локализацией на все языки игры
(en, ru, de, es, fr, pt) — визуальная витрина + шпаргалка для Steamworks partner-сайта. Дополнительно
пишет docs/achievements/localization.csv."""
import os, base64, html, csv

ICONS = os.path.join(os.path.dirname(__file__), "..", "docs", "achievements")
OUT = os.environ.get("GALLERY_OUT", "/tmp/gallery.html")
CSV_OUT = os.path.join(ICONS, "localization.csv")

LANGS = [("en", "English"), ("ru", "Русский"), ("de", "Deutsch"),
         ("es", "Español"), ("fr", "Français"), ("pt", "Português")]

CATEGORY = {"survive": "Выживание", "collect": "Коллекция", "challenge": "Испытания"}

# Названия по языкам для каждого достижения
NAMES = {
    "SURVIVE_10": {"en": "First Stand",      "ru": "Первый рубеж",     "de": "Erster Widerstand", "es": "Primer bastión",       "fr": "Premier rempart",         "pt": "Primeira linha"},
    "SURVIVE_20": {"en": "Unwavering",       "ru": "Стойкость",        "de": "Standhaft",         "es": "Inquebrantable",       "fr": "Inébranlable",            "pt": "Inabalável"},
    "SURVIVE_30": {"en": "Half-Hour Hell",   "ru": "Полчаса ада",      "de": "Halbe Stunde Hölle","es": "Media hora de infierno","fr": "Trente minutes d'enfer",  "pt": "Meia hora de inferno"},
    "SURVIVE_40": {"en": "Unbreakable",      "ru": "Несокрушимый",     "de": "Unzerbrechlich",    "es": "Indestructible",       "fr": "Indestructible",          "pt": "Inquebrável"},
    "SURVIVE_50": {"en": "Living Legend",    "ru": "Живая легенда",    "de": "Lebende Legende",   "es": "Leyenda viviente",     "fr": "Légende vivante",         "pt": "Lenda viva"},
    "SURVIVE_60": {"en": "Eternal Guardian", "ru": "Вечный страж",     "de": "Ewiger Wächter",    "es": "Guardián eterno",      "fr": "Gardien éternel",         "pt": "Guardião eterno"},
    "ALL_WEAPONS":  {"en": "Armory Baron",   "ru": "Оружейный барон",  "de": "Waffenbaron",       "es": "Barón del arsenal",    "fr": "Baron de l'armurerie",    "pt": "Barão do arsenal"},
    "ALL_UPGRADES": {"en": "Upgrade Master", "ru": "Мастер улучшений", "de": "Upgrade-Meister",   "es": "Maestro de mejoras",   "fr": "Maître des améliorations","pt": "Mestre das melhorias"},
    "BOSS_SLAYER":  {"en": "Boss Slayer",    "ru": "Убийца боссов",    "de": "Bossbezwinger",     "es": "Cazador de jefes",     "fr": "Tueur de boss",           "pt": "Matador de chefes"},
    "SHOPAHOLIC":   {"en": "Shopaholic",     "ru": "Шопоголик",        "de": "Kaufrausch",        "es": "Comprador compulsivo", "fr": "Accro du shopping",       "pt": "Comprador compulsivo"},
}

# Шаблоны описаний
SURVIVE_DESC = {
    "en": "Survive {n} minutes in a single run",
    "ru": "Продержаться {n} минут в одном забеге",
    "de": "Überlebe {n} Minuten in einem Durchlauf",
    "es": "Sobrevive {n} minutos en una sola partida",
    "fr": "Survivre {n} minutes en une seule partie",
    "pt": "Sobreviva {n} minutos em uma única partida",
}
DESC = {
    "ALL_WEAPONS": {"en": "Buy every type of weapon", "ru": "Купить все виды оружия", "de": "Kaufe alle Waffentypen",
                    "es": "Compra todos los tipos de armas", "fr": "Acheter tous les types d'armes", "pt": "Compre todos os tipos de armas"},
    "ALL_UPGRADES": {"en": "Buy every upgrade", "ru": "Купить все улучшения", "de": "Kaufe alle Verbesserungen",
                     "es": "Compra todas las mejoras", "fr": "Acheter toutes les améliorations", "pt": "Compre todas as melhorias"},
    "BOSS_SLAYER": {"en": "Defeat a boss", "ru": "Победить босса", "de": "Besiege einen Boss",
                    "es": "Derrota a un jefe", "fr": "Vaincre un boss", "pt": "Derrote um chefe"},
    "SHOPAHOLIC": {"en": "Make 30 purchases in a single run", "ru": "Совершить 30 покупок за один забег", "de": "Tätige 30 Käufe in einem Durchlauf",
                   "es": "Realiza 30 compras en una sola partida", "fr": "Effectuer 30 achats en une seule partie", "pt": "Faça 30 compras em uma única partida"},
}
for m in (10, 20, 30, 40, 50, 60):
    DESC["SURVIVE_%d" % m] = {lang: tpl.format(n=m) for lang, tpl in SURVIVE_DESC.items()}

ORDER = ["SURVIVE_10", "SURVIVE_20", "SURVIVE_30", "SURVIVE_40", "SURVIVE_50", "SURVIVE_60",
         "ALL_WEAPONS", "ALL_UPGRADES", "BOSS_SLAYER", "SHOPAHOLIC"]
CAT_OF = {a: ("survive" if a.startswith("SURVIVE") else "collect" if a.startswith("ALL_") else "challenge") for a in ORDER}


def data_uri(path):
    with open(path, "rb") as f:
        return "data:image/png;base64," + base64.b64encode(f.read()).decode()


def loc_rows(api):
    rows = ""
    for code, label in LANGS:
        rows += ("<tr><th>%s</th><td class=\"nm\">%s</td><td>%s</td></tr>"
                 % (html.escape(label), html.escape(NAMES[api][code]), html.escape(DESC[api][code])))
    return rows


def card(api):
    ach = data_uri(os.path.join(ICONS, api + ".png"))
    lock = data_uri(os.path.join(ICONS, api + "_locked.png"))
    return """      <article class="card">
        <header class="chead">
          <div class="icons">
            <img class="ach" src="%s" alt="%s — получено" width="256" height="256" />
            <img class="lock" src="%s" alt="заблокировано" title="locked" width="256" height="256" />
          </div>
          <div class="titles">
            <h3>%s</h3>
            <code class="api">%s</code>
          </div>
        </header>
        <div class="tablewrap">
          <table class="loc">
            <thead><tr><th>Язык</th><th>Название</th><th>Описание</th></tr></thead>
            <tbody>%s</tbody>
          </table>
        </div>
      </article>""" % (ach, html.escape(NAMES[api]["ru"]), lock, html.escape(NAMES[api]["ru"]), api, loc_rows(api))


def section(cat_key):
    apis = [a for a in ORDER if CAT_OF[a] == cat_key]
    cards = "\n".join(card(a) for a in apis)
    return """    <section class="group">
      <h2>%s</h2>
      <div class="grid">
%s
      </div>
    </section>""" % (html.escape(CATEGORY[cat_key]), cards)


CSS = """
<style>
  :root{
    --ground:#12141d; --panel:#1b1e2b; --panel-2:#232739;
    --line:rgba(226,181,63,.16); --gold:#e6ba46; --ink:#efe7d4; --muted:#a49c86; --mono:#d7cdb4;
    --shadow:0 10px 30px rgba(0,0,0,.45);
  }
  @media (prefers-color-scheme: light){
    :root{ --ground:#f3efe4; --panel:#fffdf7; --panel-2:#f6f0e1; --line:rgba(150,110,30,.22);
      --gold:#9a6f1c; --ink:#2a2620; --muted:#6f6650; --mono:#5b5237; --shadow:0 8px 22px rgba(120,90,30,.14); }
  }
  :root[data-theme="dark"]{ --ground:#12141d; --panel:#1b1e2b; --panel-2:#232739; --line:rgba(226,181,63,.16);
    --gold:#e6ba46; --ink:#efe7d4; --muted:#a49c86; --mono:#d7cdb4; --shadow:0 10px 30px rgba(0,0,0,.45); }
  :root[data-theme="light"]{ --ground:#f3efe4; --panel:#fffdf7; --panel-2:#f6f0e1; --line:rgba(150,110,30,.22);
    --gold:#9a6f1c; --ink:#2a2620; --muted:#6f6650; --mono:#5b5237; --shadow:0 8px 22px rgba(120,90,30,.14); }
  *{box-sizing:border-box}
  body{margin:0;background:radial-gradient(1200px 600px at 50% -10%, color-mix(in srgb, var(--gold) 8%, transparent), transparent 60%),var(--ground);
    color:var(--ink);font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif;line-height:1.5;}
  .wrap{max-width:1120px;margin:0 auto;padding:56px 24px 80px}
  header.top{text-align:center;margin-bottom:8px}
  .eyebrow{font-size:.72rem;letter-spacing:.24em;text-transform:uppercase;color:var(--gold);font-weight:700}
  h1{font-family:Georgia,"Times New Roman",serif;font-weight:700;font-size:clamp(2rem,4vw,3rem);margin:.3em 0 .1em;text-wrap:balance}
  .sub{color:var(--muted);max-width:64ch;margin:.2em auto 0}
  .callout{display:flex;gap:14px;align-items:flex-start;max-width:820px;margin:28px auto 36px;background:var(--panel);
    border:1px solid var(--line);border-radius:14px;padding:16px 18px;box-shadow:var(--shadow)}
  .callout .dot{flex:0 0 auto;width:10px;height:10px;border-radius:50%;background:var(--gold);margin-top:7px;
    box-shadow:0 0 0 4px color-mix(in srgb,var(--gold) 22%,transparent)}
  .callout p{margin:0;font-size:.94rem}
  .callout b{color:var(--gold)}
  .group{margin:0 0 40px}
  .group>h2{font-family:Georgia,serif;font-size:1.15rem;font-weight:700;margin:0 0 18px;padding-bottom:8px;border-bottom:1px solid var(--line)}
  .grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(420px,1fr));gap:16px}
  .card{background:var(--panel);border:1px solid var(--line);border-radius:16px;padding:16px;box-shadow:var(--shadow)}
  .chead{display:flex;gap:14px;align-items:center;margin-bottom:12px}
  .icons{position:relative;flex:0 0 auto}
  .icons .ach{width:72px;height:72px;display:block;filter:drop-shadow(0 4px 8px rgba(0,0,0,.35))}
  .icons .lock{position:absolute;right:-7px;bottom:-7px;width:32px;height:32px;border-radius:50%;border:2px solid var(--panel);background:var(--panel-2)}
  .titles h3{margin:0;font-size:1.05rem;font-weight:700}
  .api{display:inline-block;margin-top:5px;font-family:ui-monospace,SFMono-Regular,Menlo,monospace;font-size:.78rem;
    color:var(--mono);background:var(--panel-2);border:1px solid var(--line);border-radius:7px;padding:3px 9px;letter-spacing:.02em}
  .tablewrap{overflow-x:auto}
  table.loc{width:100%;border-collapse:collapse;font-size:.84rem}
  table.loc th,table.loc td{text-align:left;padding:6px 10px;border-top:1px solid var(--line);vertical-align:top}
  table.loc thead th{border-top:0;color:var(--muted);font-weight:600;font-size:.72rem;text-transform:uppercase;letter-spacing:.06em}
  table.loc tbody th{color:var(--muted);font-weight:600;white-space:nowrap}
  table.loc td.nm{font-weight:600;color:var(--ink);white-space:nowrap}
  table.loc td{color:var(--ink)}
  footer{color:var(--muted);font-size:.82rem;text-align:center;border-top:1px solid var(--line);padding-top:22px;margin-top:12px;max-width:820px;margin-left:auto;margin-right:auto}
  footer code{font-family:ui-monospace,monospace;color:var(--mono)}
</style>
"""

DOC = """<title>Достижения — Endless Tower Survivors</title>
%s
<div class="wrap">
  <header class="top">
    <div class="eyebrow">Steam · Endless Tower Survivors</div>
    <h1>Достижения — локализация</h1>
    <p class="sub">10 достижений, названия и описания на всех 6 языках игры (EN · RU · DE · ES · FR · PT). Иконки в едином стиле, процедурные — заменяемы на заказную графику.</p>
    <div class="callout">
      <span class="dot"></span>
      <p>На <b>Steamworks partner-сайте</b> создайте достижение с этим <b>API Name</b>, загрузите иконки <b>achieved</b> и <b>locked</b> (<code>docs/achievements/&lt;API&gt;.png</code> / <code>_locked.png</code>) и впишите название+описание для каждого языка из таблицы.</p>
    </div>
  </header>
%s
  <footer>
    Те же данные — в <code>docs/achievements/localization.csv</code>. Разблокировка — <code>AchievementManager</code> по игровым событиям.
  </footer>
</div>
""" % (CSS, "\n".join(section(k) for k in ("survive", "collect", "challenge")))

with open(OUT, "w", encoding="utf-8") as f:
    f.write(DOC)

# CSV: api, category, field(name/desc), lang columns
with open(CSV_OUT, "w", encoding="utf-8", newline="") as f:
    w = csv.writer(f)
    w.writerow(["api", "field"] + [c for c, _ in LANGS])
    for api in ORDER:
        w.writerow([api, "name"] + [NAMES[api][c] for c, _ in LANGS])
        w.writerow([api, "desc"] + [DESC[api][c] for c, _ in LANGS])

print("wrote", OUT, len(DOC), "bytes;", CSV_OUT)
