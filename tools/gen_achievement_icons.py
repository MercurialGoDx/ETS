#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Генератор иконок достижений Steam в едином стиле — БЕЗ внешних зависимостей (только stdlib zlib).
Рисует медальон: тень + золотое кольцо + купольный диск (цвет по категории) + символ достижения
(число минут 7-сегментами и часики — для выживания; эмблемы — для остальных). Для каждого достижения
делается "achieved" (цветная) и "locked" (обесцвеченная затемнённая) версия 256x256.

Иконки → docs/achievements/. Их нужно загрузить на Steamworks partner-сайте к достижениям с
соответствующими API-именами.
"""
import os, zlib, struct, math

OUT = os.path.join(os.path.dirname(__file__), "..", "docs", "achievements")
SIZE = 256
SS = 3                      # суперсэмплинг для сглаживания
W = SIZE * SS

# ---------- низкоуровневый холст (RGBA bytearray) ----------

def new_canvas():
    return bytearray(W * W * 4)  # всё прозрачно

def blend(buf, x, y, r, g, b, a):
    if x < 0 or y < 0 or x >= W or y >= W or a <= 0:
        return
    i = (y * W + x) * 4
    ba = buf[i + 3] / 255.0
    sa = a / 255.0
    outa = sa + ba * (1 - sa)
    if outa <= 0:
        return
    for k, sc in enumerate((r, g, b)):
        bc = buf[i + k]
        buf[i + k] = int((sc * sa + bc * ba * (1 - sa)) / outa + 0.5)
    buf[i + 3] = int(outa * 255 + 0.5)

def lerp(a, b, t):
    return a + (b - a) * t

def lerp_c(c1, c2, t):
    return (lerp(c1[0], c2[0], t), lerp(c1[1], c2[1], t), lerp(c1[2], c2[2], t))

# ---------- примитивы (итерация по bbox) ----------

def disc(buf, cx, cy, R, color_center, color_edge, a=255):
    x0, x1 = int(cx - R) - 1, int(cx + R) + 1
    y0, y1 = int(cy - R) - 1, int(cy + R) + 1
    for y in range(y0, y1):
        for x in range(x0, x1):
            d = math.hypot(x - cx, y - cy)
            if d <= R:
                t = d / R
                t = t * t * (3 - 2 * t)        # smoothstep — купол
                c = lerp_c(color_center, color_edge, t)
                blend(buf, x, y, int(c[0]), int(c[1]), int(c[2]), a)

def ring(buf, cx, cy, r_in, r_out, c_light, c_dark):
    x0, x1 = int(cx - r_out) - 1, int(cx + r_out) + 1
    y0, y1 = int(cy - r_out) - 1, int(cy + r_out) + 1
    for y in range(y0, y1):
        for x in range(x0, x1):
            d = math.hypot(x - cx, y - cy)
            if r_in <= d <= r_out:
                # металлический блик: светлее сверху-слева, темнее снизу-справа
                ang = math.atan2(y - cy, x - cx)
                shine = 0.5 + 0.5 * math.cos(ang + 2.3)
                across = (d - r_in) / max(1e-6, (r_out - r_in))
                edge = 1 - abs(across - 0.5) * 2       # темнее у краёв кольца
                t = 0.25 + 0.75 * (1 - shine) * (0.6 + 0.4 * (1 - edge))
                c = lerp_c(c_light, c_dark, t)
                blend(buf, x, y, int(c[0]), int(c[1]), int(c[2]), 255)

def soft_shadow(buf, cx, cy, R):
    x0, x1 = int(cx - R) - 1, int(cx + R) + 1
    y0, y1 = int(cy - R) - 1, int(cy + R) + 1
    for y in range(y0, y1):
        for x in range(x0, x1):
            d = math.hypot(x - cx, y - cy)
            if d <= R:
                a = int(120 * (1 - d / R))
                blend(buf, x, y, 0, 0, 0, a)

def capsule(buf, x1, y1, x2, y2, rad, color, a=255):
    minx, maxx = int(min(x1, x2) - rad) - 1, int(max(x1, x2) + rad) + 1
    miny, maxy = int(min(y1, y2) - rad) - 1, int(max(y1, y2) + rad) + 1
    dx, dy = x2 - x1, y2 - y1
    ll = dx * dx + dy * dy
    for y in range(miny, maxy):
        for x in range(minx, maxx):
            if ll == 0:
                t = 0.0
            else:
                t = max(0.0, min(1.0, ((x - x1) * dx + (y - y1) * dy) / ll))
            px, py = x1 + t * dx, y1 + t * dy
            if math.hypot(x - px, y - py) <= rad:
                blend(buf, x, y, color[0], color[1], color[2], a)

def polygon(buf, pts, color, a=255):
    ys = [p[1] for p in pts]
    y0, y1 = int(min(ys)), int(max(ys)) + 1
    n = len(pts)
    for y in range(y0, y1):
        xs = []
        for i in range(n):
            ax, ay = pts[i]
            bx, by = pts[(i + 1) % n]
            if (ay <= y < by) or (by <= y < ay):
                xs.append(ax + (bx - ax) * (y - ay) / (by - ay))
        xs.sort()
        for j in range(0, len(xs) - 1, 2):
            for x in range(int(xs[j]), int(xs[j + 1]) + 1):
                blend(buf, x, y, color[0], color[1], color[2], a)

def circle_fill(buf, cx, cy, R, color, a=255):
    disc(buf, cx, cy, R, color, color, a)

# ---------- 7-сегментные цифры ----------

SEG = {
    '0': "abcdef", '1': "bc", '2': "abged", '3': "abgcd",
    '4': "fgbc", '5': "afgcd", '6': "afgecd", '7': "abc",
    '8': "abcdefg", '9': "abcdfg",
}

def draw_digit(buf, ch, cx, cy, w, h, t, color):
    x0, x1 = cx - w / 2, cx + w / 2
    yt, ym, yb = cy - h / 2, cy, cy + h / 2
    ins = t * 0.9
    segs = {
        'a': ((x0 + ins, yt), (x1 - ins, yt)),
        'g': ((x0 + ins, ym), (x1 - ins, ym)),
        'd': ((x0 + ins, yb), (x1 - ins, yb)),
        'f': ((x0, yt + ins), (x0, ym - ins)),
        'b': ((x1, yt + ins), (x1, ym - ins)),
        'e': ((x0, ym + ins), (x0, yb - ins)),
        'c': ((x1, ym + ins), (x1, yb - ins)),
    }
    for s in SEG[ch]:
        (ax, ay), (bx, by) = segs[s]
        capsule(buf, ax, ay, bx, by, t / 2, color)

def draw_number(buf, s, cx, cy, digit_w, digit_h, t, color):
    gap = digit_w * 0.55
    total = len(s) * digit_w + (len(s) - 1) * gap
    x = cx - total / 2 + digit_w / 2
    for ch in s:
        draw_digit(buf, ch, x, cy, digit_w, digit_h, t, color)
        x += digit_w + gap

def draw_clock(buf, cx, cy, R, color):
    ring(buf, cx, cy, R * 0.82, R, color, lerp_c(color, (0, 0, 0), 0.4))
    capsule(buf, cx, cy, cx, cy - R * 0.55, R * 0.09, color)          # минутная
    capsule(buf, cx, cy, cx + R * 0.45, cy, R * 0.09, color)          # часовая

# ---------- эмблемы ----------

def emblem_sword(buf, cx, cy, s, color):
    steel = color; dark = lerp_c(color, (0, 0, 0), 0.45)
    # клинок
    polygon(buf, [(cx, cy - s), (cx + s * 0.18, cy - s * 0.75),
                  (cx + s * 0.18, cy + s * 0.35), (cx - s * 0.18, cy + s * 0.35),
                  (cx - s * 0.18, cy - s * 0.75)], steel)
    capsule(buf, cx, cy - s, cx, cy + s * 0.35, 1, dark)             # рёбра (тонкая линия)
    # гарда
    capsule(buf, cx - s * 0.6, cy + s * 0.4, cx + s * 0.6, cy + s * 0.4, s * 0.09, dark)
    # рукоять + навершие
    capsule(buf, cx, cy + s * 0.4, cx, cy + s * 0.85, s * 0.08, dark)
    circle_fill(buf, cx, cy + s * 0.92, s * 0.12, dark)

def emblem_chevrons(buf, cx, cy, s, color):
    dark = lerp_c(color, (0, 0, 0), 0.4)
    for i, oy in enumerate((-0.55, 0.0, 0.55)):
        c = color if i == 0 else lerp_c(color, dark, 0.35 * i)
        y = cy + oy * s
        capsule(buf, cx - s * 0.6, y + s * 0.28, cx, y - s * 0.28, s * 0.13, c)
        capsule(buf, cx, y - s * 0.28, cx + s * 0.6, y + s * 0.28, s * 0.13, c)

def emblem_skull(buf, cx, cy, s, color):
    bone = color; hole = lerp_c(color, (0, 0, 0), 0.72)
    circle_fill(buf, cx, cy - s * 0.15, s * 0.62, bone)              # череп
    polygon(buf, [(cx - s * 0.4, cy + s * 0.25), (cx + s * 0.4, cy + s * 0.25),
                  (cx + s * 0.3, cy + s * 0.7), (cx - s * 0.3, cy + s * 0.7)], bone)  # челюсть
    circle_fill(buf, cx - s * 0.25, cy - s * 0.15, s * 0.17, hole)   # глазницы
    circle_fill(buf, cx + s * 0.25, cy - s * 0.15, s * 0.17, hole)
    polygon(buf, [(cx, cy + s * 0.02), (cx + s * 0.08, cy + s * 0.2),
                  (cx - s * 0.08, cy + s * 0.2)], hole)              # нос
    for dx in (-0.18, 0, 0.18):                                     # зубы
        capsule(buf, cx + dx * s, cy + s * 0.3, cx + dx * s, cy + s * 0.62, s * 0.05, hole)

def emblem_coins(buf, cx, cy, s, color):
    dark = lerp_c(color, (0, 0, 0), 0.4); light = lerp_c(color, (255, 255, 255), 0.4)
    for oy in (0.45, 0.0, -0.45):
        y = cy + oy * s
        # монета как приплюснутый диск (эллипс) — рисуем полигоном-аппроксимацией
        pts = []
        for k in range(24):
            ang = 2 * math.pi * k / 24
            pts.append((cx + math.cos(ang) * s * 0.62, y + math.sin(ang) * s * 0.26))
        polygon(buf, pts, color)
        capsule(buf, cx - s * 0.5, y, cx + s * 0.5, y, s * 0.03, dark)
    circle_fill(buf, cx - s * 0.22, cy - s * 0.5, s * 0.08, light)   # блик

# ---------- сборка бейджа ----------

C = W / 2
R_OUT = W * 0.44
R_IN = W * 0.35
GOLD_L = (247, 226, 138)
GOLD_D = (150, 108, 30)

def build_badge(kind, disc_c, arg):
    buf = new_canvas()
    soft_shadow(buf, C, C + W * 0.03, R_OUT * 1.02)
    ring(buf, C, C, R_IN, R_OUT, GOLD_L, GOLD_D)
    disc(buf, C, C, R_IN, lerp_c(disc_c, (255, 255, 255), 0.28), lerp_c(disc_c, (0, 0, 0), 0.35))
    white = (245, 245, 240)
    if kind == "time":
        draw_clock(buf, C, C - R_IN * 0.42, R_IN * 0.24, white)
        draw_number(buf, str(arg), C, C + R_IN * 0.18, R_IN * 0.42, R_IN * 0.72, R_IN * 0.14, white)
    elif kind == "sword":
        emblem_sword(buf, C, C, R_IN * 0.62, (225, 228, 235))
    elif kind == "chevrons":
        emblem_chevrons(buf, C, C, R_IN * 0.5, white)
    elif kind == "skull":
        emblem_skull(buf, C, C, R_IN * 0.6, (235, 233, 220))
    elif kind == "coins":
        emblem_coins(buf, C, C, R_IN * 0.7, (247, 205, 90))
    return buf

def to_locked(buf):
    out = bytearray(buf)
    for i in range(0, len(out), 4):
        a = out[i + 3]
        if a == 0:
            continue
        g = int(0.299 * out[i] + 0.587 * out[i + 1] + 0.114 * out[i + 2])
        g = int(g * 0.42 + 15)                 # обесцветить + затемнить
        out[i] = out[i + 1] = out[i + 2] = g
    return out

# ---------- даунсэмпл + PNG ----------

def downsample(buf):
    out = bytearray(SIZE * SIZE * 4)
    for y in range(SIZE):
        for x in range(SIZE):
            r = gg = b = a = 0
            for sy in range(SS):
                for sx in range(SS):
                    i = ((y * SS + sy) * W + (x * SS + sx)) * 4
                    r += buf[i]; gg += buf[i + 1]; b += buf[i + 2]; a += buf[i + 3]
            n = SS * SS
            j = (y * SIZE + x) * 4
            out[j] = r // n; out[j + 1] = gg // n; out[j + 2] = b // n; out[j + 3] = a // n
    return out

def write_png(path, rgba):
    def chunk(tag, data):
        return (struct.pack(">I", len(data)) + tag + data +
                struct.pack(">I", zlib.crc32(tag + data) & 0xffffffff))
    raw = bytearray()
    for y in range(SIZE):
        raw.append(0)
        raw += rgba[y * SIZE * 4:(y + 1) * SIZE * 4]
    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", SIZE, SIZE, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(bytes(raw), 9))
    png += chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)

# ---------- достижения ----------

ACHIEVEMENTS = [
    ("SURVIVE_10", "time", 10, (150, 96, 46)),
    ("SURVIVE_20", "time", 20, (176, 112, 48)),
    ("SURVIVE_30", "time", 30, (196, 132, 52)),
    ("SURVIVE_40", "time", 40, (208, 152, 56)),
    ("SURVIVE_50", "time", 50, (220, 178, 62)),
    ("SURVIVE_60", "time", 60, (240, 210, 82)),
    ("ALL_WEAPONS", "sword", None, (128, 40, 52)),
    ("ALL_UPGRADES", "chevrons", None, (34, 120, 80)),
    ("BOSS_SLAYER", "skull", None, (86, 52, 116)),
    ("SHOPAHOLIC", "coins", None, (96, 60, 22)),
]

def main():
    os.makedirs(OUT, exist_ok=True)
    for api, kind, arg, col in ACHIEVEMENTS:
        buf = build_badge(kind, col, arg)
        write_png(os.path.join(OUT, api + ".png"), downsample(buf))
        write_png(os.path.join(OUT, api + "_locked.png"), downsample(to_locked(buf)))
        print("ok", api)
    print("done ->", os.path.normpath(OUT))

if __name__ == "__main__":
    main()
