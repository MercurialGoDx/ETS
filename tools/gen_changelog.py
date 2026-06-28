# -*- coding: utf-8 -*-
"""
Генератор docs/История_изменений.docx из истории git.

Перестраивает Word-файл из коммитов диапазона BASE..HEAD (по одному блоку на коммит:
тип, заголовок, хэш, дата, описание из тела коммита, затронутые файлы). У всех уже
сделанных коммитов хэши верные. Для коммита, который ещё только готовится (HEAD не
сдвинут), можно добавить временный блок:

    python3 tools/gen_changelog.py --pending "feat: краткое описание"

В этом случае последним идёт блок «(готовится к коммиту)» со списком файлов из индекса
(git diff --cached). После реального коммита повторный запуск превратит его в обычный
блок с настоящим хэшем (самовосстановление).

Workflow при коммите (вручную, без git-хука):
  1) git add <изменения>
  2) python3 tools/gen_changelog.py --pending "<subject коммита>"
  3) git add docs/История_изменений.docx
  4) git commit -m "<subject>"
"""
import argparse
import os
import subprocess
import zipfile
from xml.sax.saxutils import escape

BASE = "d839316"  # коммит «tooling: add Unity MCP setup for Claude Code» (точка отсчёта)
REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(REPO, "docs", "История_изменений.docx")

TYPE_COLOR = {
    "feat": "1F7A1F", "fix": "B22222", "perf": "1F5FB2",
    "balance": "8A5A00", "docs": "555555", "refactor": "7A1F7A",
    "chore": "555555", "tooling": "1F5FB2", "style": "555555", "test": "1F5FB2",
}

INTRO = (
    "Документ описывает все коммиты ветки feature/UpgradeSystem, начиная сразу после "
    "коммита «tooling: add Unity MCP setup for Claude Code» (d839316) и до текущего HEAD. "
    "Для каждого коммита: тип, заголовок, хэш, дата, описание (из сообщения коммита) и "
    "затронутые файлы. Коммиты идут в хронологическом порядке. Файл генерируется скриптом "
    "tools/gen_changelog.py."
)


def git(*args):
    return subprocess.check_output(["git", "-C", REPO, *args], text=True, encoding="utf-8")


def collect_commits():
    out = git("log", "--reverse", "--pretty=format:%h\x1f%ad\x1f%s\x1f%b\x1e",
              "--date=short", f"{BASE}..HEAD")
    commits = []
    for rec in out.split("\x1e"):
        rec = rec.strip("\n")
        if not rec.strip():
            continue
        parts = rec.split("\x1f")
        if len(parts) < 4:
            continue
        h, date, subject, body = parts[0], parts[1], parts[2], parts[3]
        files = git("show", "--pretty=format:", "--name-only", h).splitlines()
        commits.append(make_block(h, date, subject, body, files))
    return commits


def split_subject(subject):
    if ": " in subject:
        prefix, title = subject.split(": ", 1)
        prefix = prefix.split("(")[0].strip().lower()  # feat(scope) -> feat
        if prefix in TYPE_COLOR:
            return prefix, title
    return "", subject


def body_bullets(body):
    """Тело коммита -> список пунктов. Строки с '- ' = маркеры, переносы склеиваются."""
    bullets = []
    cur = None
    for raw in body.splitlines():
        line = raw.rstrip()
        s = line.strip()
        if not s:
            continue
        if s.startswith("- ") or s.startswith("* "):
            if cur:
                bullets.append(cur)
            cur = s[2:].strip()
        else:
            if cur is None:
                cur = s
            else:
                cur += " " + s
    if cur:
        bullets.append(cur)
    return bullets


def clean_files(files):
    """Убираем .meta, у которых есть «родитель» в списке; оставляем уникальные пути."""
    base_set = set(f for f in files if not f.endswith(".meta"))
    out, seen = [], set()
    for f in files:
        if f.endswith(".meta") and f[:-5] in base_set:
            continue
        if f in seen:
            continue
        seen.add(f)
        out.append(f)
    return out


def make_block(h, date, subject, body, files):
    typ, title = split_subject(subject)
    return {
        "type": typ, "title": title, "hash": h, "date": date,
        "desc": body_bullets(body), "files": clean_files(files), "pending": False,
    }


def pending_block(subject):
    typ, title = split_subject(subject)
    staged = git("diff", "--cached", "--name-only").splitlines()
    return {
        "type": typ, "title": title, "hash": "(готовится)", "date": "—",
        "desc": [], "files": clean_files(staged), "pending": True,
    }


# ---------- сборка docx ----------
NS = ('xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" '
      'xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"')


def run(text, bold=False, italic=False, color=None, size=None):
    props = ""
    if bold: props += '<w:b/>'
    if italic: props += '<w:i/>'
    if color: props += '<w:color w:val="%s"/>' % color
    if size: props += '<w:sz w:val="%d"/>' % size
    rpr = '<w:rPr>%s</w:rPr>' % props if props else ""
    return '<w:r>%s<w:t xml:space="preserve">%s</w:t></w:r>' % (rpr, escape(text))


def para(runs_xml, after=120):
    return '<w:p><w:pPr><w:spacing w:after="%d"/></w:pPr>%s</w:p>' % (after, runs_xml)


def bullet(runs_xml):
    ppr = ('<w:pPr><w:numPr><w:ilvl w:val="0"/><w:numId w:val="1"/></w:numPr>'
           '<w:spacing w:after="60"/></w:pPr>')
    return '<w:p>%s%s</w:p>' % (ppr, runs_xml)


def build_document(blocks):
    body = []
    body.append(para(run("История изменений проекта", bold=True, size=40), after=80))
    body.append(para(run("Ветка feature/UpgradeSystem · с коммита d839316 (Unity MCP setup) до HEAD",
                         italic=True, color="666666", size=22)))
    body.append(para(run(INTRO, size=22)))
    body.append(para(run("Всего коммитов: %d" % len([b for b in blocks if not b["pending"]]),
                         bold=True, size=22)))

    for i, c in enumerate(blocks, 1):
        col = TYPE_COLOR.get(c["type"], "000000")
        head = run("%d. " % i, bold=True, size=28)
        if c["type"]:
            head += run("[%s] " % c["type"].upper(), bold=True, color=col, size=28)
        head += run(c["title"], bold=True, size=28)
        body.append(para(head, after=40))
        meta = "Коммит %s · %s" % (c["hash"], c["date"])
        if c["pending"]:
            meta += "  (блок обновится настоящим хэшем после коммита)"
        body.append(para(run(meta, italic=True, color="666666", size=20)))

        if c["desc"]:
            body.append(para(run("Описание:", bold=True, size=22), after=40))
            for x in c["desc"]:
                body.append(bullet(run(x, size=22)))

        if c["files"]:
            body.append(para(run("Затронутые файлы:", bold=True, size=22), after=40))
            for x in c["files"]:
                body.append(bullet(run(x, size=20, color="333333")))

        body.append(para(run(" ", size=10)))

    return ('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<w:document %s><w:body>%s'
            '<w:sectPr><w:pgSz w:w="11906" w:h="16838"/>'
            '<w:pgMar w:top="1134" w:bottom="1134" w:left="1134" w:right="1134"/>'
            '</w:sectPr></w:body></w:document>') % (NS, "".join(body))


def write_docx(document):
    styles = ('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
              '<w:styles %s><w:docDefaults><w:rPrDefault><w:rPr>'
              '<w:rFonts w:ascii="Calibri" w:hAnsi="Calibri" w:cs="Calibri"/>'
              '<w:sz w:val="22"/></w:rPr></w:rPrDefault></w:docDefaults>'
              '<w:style w:type="paragraph" w:default="1" w:styleId="Normal">'
              '<w:name w:val="Normal"/></w:style></w:styles>') % NS
    numbering = ('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
                 '<w:numbering %s><w:abstractNum w:abstractNumId="0"><w:lvl w:ilvl="0">'
                 '<w:start w:val="1"/><w:numFmt w:val="bullet"/><w:lvlText w:val="&#8226;"/>'
                 '<w:lvlJc w:val="left"/><w:pPr><w:ind w:left="567" w:hanging="283"/></w:pPr>'
                 '<w:rPr><w:rFonts w:ascii="Symbol" w:hAnsi="Symbol" w:hint="default"/></w:rPr>'
                 '</w:lvl></w:abstractNum><w:num w:numId="1"><w:abstractNumId w:val="0"/></w:num>'
                 '</w:numbering>') % NS
    content_types = ('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
                     '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">'
                     '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>'
                     '<Default Extension="xml" ContentType="application/xml"/>'
                     '<Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>'
                     '<Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/>'
                     '<Override PartName="/word/numbering.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml"/>'
                     '</Types>')
    rels = ('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
            '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
            '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>'
            '</Relationships>')
    doc_rels = ('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
                '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
                '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>'
                '<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/numbering" Target="numbering.xml"/>'
                '</Relationships>')
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with zipfile.ZipFile(OUT, "w", zipfile.ZIP_DEFLATED) as z:
        z.writestr("[Content_Types].xml", content_types)
        z.writestr("_rels/.rels", rels)
        z.writestr("word/document.xml", document)
        z.writestr("word/styles.xml", styles)
        z.writestr("word/numbering.xml", numbering)
        z.writestr("word/_rels/document.xml.rels", doc_rels)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--pending", help="subject готовящегося коммита (добавить временный блок)")
    args = ap.parse_args()

    blocks = collect_commits()
    if args.pending:
        blocks.append(pending_block(args.pending))
    write_docx(build_document(blocks))
    n = len([b for b in blocks if not b["pending"]])
    extra = " + 1 готовящийся" if args.pending else ""
    print("OK -> %s (%d коммитов%s)" % (OUT, n, extra))


if __name__ == "__main__":
    main()
