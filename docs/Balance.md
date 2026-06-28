# Баланс: оружия и улучшения

Конспект по тирам с фактическими параметрами из ассетов (`Assets/Prefab/Weapon`,
`Assets/Prefab/Upgrade`). DPS = `damagePerProjectile × fireRate` (подтверждено в
`TowerAttack`: `cooldown = 1 / fireRate`, т.е. `fireRate` = выстрелов/сек). Для AoE и
лучевых/цепных оружий «одиночный DPS» условен — реальный урон выше за счёт попадания по
нескольким целям.

Типы урона (`WeaponDamageType`): `Magic=0, Piercing=1, Normal=2, Projectile=3, Heavy=4, Chaos=5`.
Тип-улучшения и `t4`-ультимейты множат урон **по типу** — это основа build-стратегий.

---

## Внесённые правки баланса (changelog)

| Объект | Было | Стало | Причина |
|---|---|---|---|
| `ChainLighting` price | 10 | 400 | отладочное значение, не соответствовало тиру 2 |
| `ChainLighting` weight | 1 111 111 | 8 | отладочное значение (выпадал всегда) |
| `ChainLighting` dmg | 5 | 10 | 2.5 DPS — нежизнеспособно для тира 2 |
| `Balista` weight | 40 | 4 | унификация веса тира 3 |
| `ChaosWave` weight | 40 | 4 | унификация веса тира 3 |
| `laser` weight | 9999 | 4 | отладочное значение (выпадал всегда) |
| t4 ультимейты урона `valuePercent` ×6 | 0.3 | 30 | **баг шкалы**: скрипт делит на 100 → давал +0,3% вместо +30%, ломая моно-тип билды |
| `HeavyUltimate` `damageType` | 3 (Projectile) | 4 (Heavy) | ультимейт Heavy ошибочно бил по Projectile |
| `DegenAura` `valueFlat` | 10000 | 5 | множитель ауры (урон = реген × множ.); по тултипу «2 = 2× регена», 10000 — отладка |

Вес по тирам теперь убывает с ростом тира (реже на высоких тирах):
**T1 = 20, T2 = 8, T3 = 4, T4 = 2** (оружия); у улучшений вес внутри тира варьируется намеренно.

---

## ОРУЖИЯ

### Tier 1 — цена 100, вес 20 (стартовый ассортимент, ~5 DPS)

| Оружие | Тип | dmg | fireRate | DPS | Снаряд | Заметка |
|---|---|---|---|---|---|---|
| Spear | Piercing | 10 | 0.5 | 5.0 | Bullet | пробивает по линии |
| Stone | Heavy | 10 | 0.5 | 5.0 | Bullet | одиночный, тяжёлый |
| MagicOrb | Magic | 5 | 1.0 | 5.0 | Bullet | одиночный |
| ChaosOrb | Chaos | 5 | 1.0 | 5.0 | Bullet | одиночный |
| Axe | Normal | 7 | 0.7 | 4.9 | Bullet | одиночный |
| Bow | Projectile | 4 | 1.5 | 6.0 | Bullet | быстрый, мало урона |

Баланс ровный (4.9–6.0). Каждый тип урона представлен ровно одним стартовым оружием → честный
выбор «специализации» с первого слота.

### Tier 2 — цена 400, вес 8

| Оружие | Тип | dmg | fireRate | DPS | Снаряд | Архетип |
|---|---|---|---|---|---|---|
| Mortar | Heavy | 80 | 0.3 | 24.0 | ArcBullet | AoE, медленный навес |
| Missles | Piercing | 18 | 1.2 | 21.6 | Missles | несколько ракет |
| ShockWave | Normal | 30 | 0.6 | 18.0 | Wave | AoE-волна |
| LifeLaser | Chaos | 6 | 2.0 | 12.0 | LaserBeam | луч + вампиризм |
| IceBullet | Magic | 15 | 0.7 | 10.5 | IceBullet | одиночный + замедление |
| Bounce | Projectile | 9 | 1.0 | 9.0 | BounceBullet | отскоки по целям |
| ChainLighting | Magic | 10 | 0.5 | 5.0/цель | LightingChain | цепь по N целям *(пофикшено)* |

Одиночный эталон ~10–12 (IceBullet/LifeLaser); AoE/мульти имеют выше «число», но размазывают урон.
ChainLighting после фикса — дешёвый цепной контроль толп.

### Tier 3 — цена 1500, вес 4

| Оружие | Тип | dmg | fireRate | DPS | Снаряд | Архетип |
|---|---|---|---|---|---|---|
| laser | Piercing | 45 | 2.0 | 90.0 | LaserBeam | луч-пробой по линии |
| Balista | Projectile | 250 | 0.35 | 87.5 | Bullet | одиночный «снайпер» |
| Log | Heavy | 160 | 0.4 | 64.0 | Wave | AoE-волна |
| Hammer | Normal | 125 | 0.5 | 62.5 | Bullet | одиночный, тяжёлый |
| Tornado | Magic | 85 | 0.65 | 55.3 | Wave | AoE-волна |
| ChaosWave | Chaos | 80 | 0.6 | 48.0 | Wave | AoE-волна |
| Catapult | Heavy | %HP | 0.35 | — | Catapult | **15% HP врага** по площади (`aoeRadius`) |

Single-target (laser/Balista/Hammer) бьёт сильнее по числу, AoE (волны) — по площади.
Catapult масштабируется от HP цели → лучший выбор против боссов/жирных, бесполезен по мелочи.

### Tier 4 — цена 3000, вес 2 (ультимативные)

| Оружие | Тип | dmg | fireRate | DPS | Снаряд | Архетип |
|---|---|---|---|---|---|---|
| SlashingSpin | Normal | 200 | 1.0 | 200.0 | RandomSpawnPortal | вращение AoE ближней зоны (рискованно) |
| Blizzard | Magic | 225 | 0.6 | 135.0 | PortalBullet | зональный AoE |
| Meteor | Chaos | 340 | 0.3 | 102.0 | FalingBullet | падение, AoE-удар |
| ChaosFire | Chaos | 100 | 1.0 | 100.0 | AuraDamageZone | DoT-аура (зона) |
| MagicCircle | Magic | 325 | 0.2 | 65.0 | ScalingWave | растущая AoE (скейл со временем) |

SlashingSpin — топ по урону, но требует подпускать врагов вплотную (позиционный риск).
MagicCircle/ChaosFire — «зоны контроля» с растущим/продолжительным уроном.

---

## УЛУЧШЕНИЯ

Шкала эффектов: `valuePercent` у тип-урона и многих апгрейдов делится на 100 (10 → +10%),
кроме `DamagePerWeaponUpgrade` (`valuePercentPerWeapon` добавляется напрямую: 0.02 → +2%/оружие).

### Tier 1 — цена 100

**Защита/эконо (вес 25 — частые):**
| Улучшение | Эффект |
|---|---|
| BasicHP | +50 к макс. HP |
| BagicShield | +50 к макс. щиту |
| BasicRegen | +5 регена HP |
| BasicSpike | +5 к шипам (урон при касании) |
| PassiveGold_1Tier | +1 золото/сек |

**Урон (вес 8 — реже; тип-специфичные +10%):**
| Улучшение | Эффект |
|---|---|
| NormalDamage / PiercingDamage / ProjectileDamage / MagicDamage / HeavyDamage / ChaosDamage | +10% к урону своего типа |
| Damage (вес 18) | +5% глобального урона |
| Sacrifice (цена 0, вес 18) | −50 жизни → +200 базового золота ×5 (бартер HP→золото) |

### Tier 2 — цена 400, вес 14 (DeathMask — 9)

| Улучшение | Эффект |
|---|---|
| BasicDamageReduction | −3.5% входящего урона (с затуханием) |
| Generator | пассивный доход каждые 60 сек (+4%) |
| GoldMine | +10% получаемого золота |
| HealOnKill | +15 HP за убийство |
| HpPerTick | +5 HP каждые 10 сек |
| ShieldPerTick | +5 щита каждые 10 сек |
| ShieldRestorePerKill | +10 щита за убийство |
| DeathMask *(composite)* | шипы +5 и +3 HP за каждого касающегося врага |

### Tier 3 — цена 1500, вес 7 (composite — 4)

| Улучшение | Эффект |
|---|---|
| AttackSpeed | +15% скорости атаки всех оружий |
| Block | +1 к шансу блока (с затуханием) |
| IncreaceHpRegPercent | +25% к регену HP |
| RegenPerTick | +1 реген каждые 10 сек |
| Shield_Multiply | +30% к макс. щиту |
| SuperBullet | +50% урона оружиям **Tier 1** (ставка на дешёвый старт) |
| NatureBlessing *(composite)* | +250 HP и реген 2% от недостающего HP |
| MoreEnemy *(цена 0, composite)* | +15% врагов в волне и +500 золота (риск/награда) |
| SpikeScaling *(composite)* | шипы +20 база и +1 за каждое убийство шипами |
| ~~Hunting_old~~ | устаревший (type 18), вне основного пула |

### Tier 4 — цена 3000

**Ультимейты урона (вес 1, composite):** +30% к типу урона *(пофикшено с 0.3)* и +2% за каждое
оружие этого типа на поле.
| Улучшение | Тип |
|---|---|
| NormalDamageUltimate | Normal |
| PiercingDamageUltimate | Piercing |
| ProjectileDamageUltimate | Projectile |
| MagicDamageUltimate | Magic |
| HeavyDamageUltimate | Heavy *(тип пофикшен с Projectile)* |
| ChaosDamageUltimate | Chaos |

**Прочие (вес 3):**
| Улучшение | Эффект |
|---|---|
| SpikeUltimate | +100% к шипам |
| DamageWhileShieldActive | +50% урона, пока активен щит |
| DegenAura *(composite)* | аура урона = реген × 5/сек по площади *(пофикшено с 10000)* |
| MytrhillMaterial *(composite)* | +25% макс. HP и +5% урона от макс. HP (танк-в-урон) |

---

## Поддерживаемые стратегии (после правок)

1. **Моно-тип урона.** Одно оружие нужного типа + все `+тип%` (T1) + ультимейт типа (T4,
   +30% и +2%/оружие). Теперь окупается на T4 — раньше был сломан шкалой 0.3%.
2. **AoE / контроль толп.** Волновые/зональные: ShockWave → Log/Tornado → Blizzard/Meteor/MagicCircle.
3. **Single-target / боссы.** Balista/laser/Hammer + Catapult (%HP) против жирных целей.
4. **Танк-шипы.** HP + щит + шипы (BasicSpike → SpikeScaling → SpikeUltimate): урон от касания.
5. **Реген / аура.** Стек регена (BasicRegen, RegenPerTick, NatureBlessing) + DegenAura
   (урон = реген × 5) + MytrhillMaterial.
6. **Экономика.** PassiveGold → GoldMine → Generator, Sacrifice, MoreEnemy (больше золота с волн).
7. **Щит-билд.** Стек щита (BagicShield, Shield_Multiply, ShieldPerTick) + DamageWhileShieldActive
   (+50% урона при активном щите).

## Открытые вопросы / на проверку плейтестом
- **DegenAura ×5**: подобрано по тултипу; финальную силу проверить с реальными значениями регена
  поздней игры (урон = суммарный_реген × 5 в секунду по площади).
- **SlashingSpin (200 DPS)** и **Balista/laser (~88–90)** — верхние границы тиров; оставлены как
  «премиальные» варианты с компромиссом (ближняя дистанция / чистый single-target).
- **MagicCircle (65 «номинального» DPS)** — низко по числу, но растёт со временем; оценить в долгих забегах.
