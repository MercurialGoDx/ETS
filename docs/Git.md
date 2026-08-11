# Git в проекте

## Ветки

`main` — транк. Ветвимся от него, в него же и вливаемся.

`master` заморожен на состоянии 19.12.2025 и **не мержится**: его `SampleScene.unity`
разошлась с рабочей линией примерно на 112 тысяч строк, и трёхстороннее слияние
такого YAML ломает сцену. Единственная уникальная работа на нём — коммит
`9ab333b New Weapons` (ChaosBounce, MagicBounce, Resonance), сохранён тегом
`archive/master-new-weapons`. Восстанавливать эти три оружия нужно отдельной
задачей, а не мержем.

Правила:

- ветка всегда от `main`, никогда от чужой фичи;
- живёт 2–3 дня, ежедневно `git pull --rebase origin main`;
- влилась — удаляем локально и на origin;
- `SampleScene.unity` правит один человек за раз, по договорённости в чате;
- перед переключением веток закрываем Unity, иначе ломается `Library/`.

Префиксы: `feature/`, `fix/`, `chore/`.

## UnityYAMLMerge — настроить один раз

`.gitattributes` помечает сцены, префабы и ассеты как `merge=unityyamlmerge`,
но сам драйвер в репозиторий не закоммитишь: путь до него у каждого свой.
Пока драйвер не зарегистрирован, Git молча мержит YAML построчно — это работает,
но именно так сцены и разваливаются.

Windows, Unity 6000.0.80f1 (путь подставить свой, если версия другая):

```bash
git config merge.unityyamlmerge.name "Unity SmartMerge"
git config merge.unityyamlmerge.driver '"C:/Program Files/Unity/Hub/Editor/6000.0.80f1/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p --force --fallback none %O %A %B %A'
git config merge.unityyamlmerge.recursive binary
```

Проверить, что подхватилось:

```bash
git config --get merge.unityyamlmerge.driver
```

`--fallback none` означает, что при неразрешимом конфликте UnityYAMLMerge
не станет молча склеивать файл, а оставит конфликт видимым — разбирать его
нужно в редакторе Unity, не руками в YAML.

## Переносы строк

`.gitattributes` намеренно не задаёт `eol` и `text=auto`. В индексе всё хранится
в LF, а рабочее дерево раскладывается через `core.autocrlf=true`. Если добавить
`eol=lf`, половина проекта разом покажется изменённой и утопит реальные диффы.
