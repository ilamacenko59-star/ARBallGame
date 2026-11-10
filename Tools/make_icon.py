"""Рисует иконку AR Ball Game.

Выход (в Assets/ARGame/AppIcon):
  icon_bg.png    — фон адаптивной иконки Android (градиент)
  icon_fg.png    — передний план адаптивной иконки (кубики + шар + рамка AR), прозрачный
  icon_full.png  — обычная квадратная иконка (фон + передний план)
  icon_round.png — круглая иконка
  Tools/icon_preview.png — как иконка выглядит в разных масках (только для проверки)

Запуск:  python Tools/make_icon.py
"""
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFilter

S = 2048          # рисуем в 2× и уменьшаем — так края гладкие
OUT = 1024

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT_DIR = os.path.join(ROOT, "Assets", "ARGame", "AppIcon")


def lerp(a, b, t):
    return a + (b - a) * t


def mix(c1, c2, t):
    return tuple(int(round(lerp(c1[i], c2[i], t))) for i in range(len(c1)))


def shade(c, k):
    """k>0 — светлее, k<0 — темнее."""
    if k >= 0:
        return mix(c, (255, 255, 255), k)
    return mix(c, (0, 0, 0), -k)


# ---------- фон ----------

def background(size):
    small = 256
    img = Image.new("RGB", (small, small))
    top = (40, 30, 110)      # тёмный индиго
    mid = (98, 52, 190)      # фиолетовый
    bot = (22, 120, 190)     # бирюзово-синий
    px = []
    for y in range(small):
        for x in range(small):
            # диагональный градиент + мягкое свечение в центре
            t = (x * 0.35 + y * 0.65) / small
            c = mix(top, mid, min(1, t * 1.6)) if t < 0.62 else mix(mid, bot, (t - 0.62) / 0.38)
            dx, dy = x / small - 0.5, y / small - 0.55
            glow = max(0.0, 1 - math.sqrt(dx * dx + dy * dy) / 0.55)
            c = shade(c, 0.22 * glow * glow)
            px.append(c)
    img.putdata(px)
    return img.resize((size, size), Image.BICUBIC)


# ---------- передний план ----------

CUBE_COLORS = [
    (250, 199, 56),   # жёлтый
    (64, 158, 242),   # синий
    (89, 209, 115),   # зелёный
    (237, 115, 191),  # розовый
    (245, 140, 64),   # оранжевый
    (153, 115, 230),  # фиолетовый
]
OUTLINE = (26, 22, 60)


def iso_cube(draw, cx, cy, a, color, outline_w):
    """Изометрический куб: (cx, cy) — центр верхней грани, a — длина ребра в пикселях."""
    dx = a * math.cos(math.radians(30))
    dy = a * math.sin(math.radians(30))
    top = [(cx, cy - dy), (cx + dx, cy), (cx, cy + dy), (cx - dx, cy)]
    left = [(cx - dx, cy), (cx, cy + dy), (cx, cy + dy + a), (cx - dx, cy + a)]
    right = [(cx + dx, cy), (cx, cy + dy), (cx, cy + dy + a), (cx + dx, cy + a)]
    draw.polygon(left, fill=color)
    draw.polygon(right, fill=shade(color, -0.28))
    draw.polygon(top, fill=shade(color, 0.3))
    # блик на верхней грани
    hl = [(cx, cy - dy * 0.55), (cx + dx * 0.45, cy - dy * 0.1), (cx, cy + dy * 0.35), (cx - dx * 0.45, cy - dy * 0.1)]
    draw.polygon(hl, fill=shade(color, 0.5))
    # контур
    outline = [(cx, cy - dy), (cx + dx, cy), (cx + dx, cy + a), (cx, cy + dy + a), (cx - dx, cy + a), (cx - dx, cy)]
    draw.line(outline + [outline[0]], fill=OUTLINE, width=outline_w, joint="curve")
    draw.line([(cx - dx, cy), (cx, cy + dy), (cx + dx, cy)], fill=OUTLINE, width=outline_w, joint="curve")
    draw.line([(cx, cy + dy), (cx, cy + dy + a)], fill=OUTLINE, width=outline_w)


def ball(size, base=(232, 52, 44)):
    """Глянцевый шар: радиальный градиент + блик. Возвращает RGBA size×size."""
    small = 200
    img = Image.new("RGBA", (small, small), (0, 0, 0, 0))
    px = []
    for y in range(small):
        for x in range(small):
            u, v = (x + 0.5) / small * 2 - 1, (y + 0.5) / small * 2 - 1
            r = math.sqrt(u * u + v * v)
            if r > 1:
                px.append((0, 0, 0, 0))
                continue
            # освещение сверху-слева
            hx, hy = u + 0.38, v + 0.42
            h = math.sqrt(hx * hx + hy * hy)
            k = 0.45 - h * 0.75
            c = shade(base, max(-0.55, min(0.5, k)))
            spec = max(0.0, 1 - math.sqrt((u + 0.35) ** 2 + (v + 0.4) ** 2) / 0.28)
            c = mix(c, (255, 255, 255), spec ** 1.5 * 0.85)
            px.append(c + (255,))
    img.putdata(px)
    img = img.resize((size, size), Image.BICUBIC)
    # гладкий край
    mask = Image.new("L", (size * 2, size * 2), 0)
    ImageDraw.Draw(mask).ellipse((0, 0, size * 2 - 1, size * 2 - 1), fill=255)
    mask = mask.resize((size, size), Image.LANCZOS)
    img.putalpha(mask)
    return img


def ar_brackets(img, P, W):
    """Уголки AR-рамки (рисуются позади сцены)."""
    draw = ImageDraw.Draw(img)
    br_col = (255, 255, 255, 215)
    t = int(W * 0.03)
    L = W * 0.15
    m = -0.04                          # доля размера сцены: рамка чуть шире сцены
    corners = [(m, m, 1, 1), (1 - m, m, -1, 1), (m, 1 - m, 1, -1), (1 - m, 1 - m, -1, -1)]
    for (x, y, sx, sy) in corners:
        px0, py0 = P(x, y)
        draw.line([(px0, py0), (px0 + sx * L, py0)], fill=br_col, width=t)
        draw.line([(px0, py0), (px0, py0 + sy * L)], fill=br_col, width=t)
        r = t / 2
        for (qx, qy) in [(px0, py0), (px0 + sx * L, py0), (px0, py0 + sy * L)]:
            draw.ellipse((qx - r, qy - r, qx + r, qy + r), fill=br_col)


def foreground(size, scale, brackets=False):
    """Сцена на прозрачном фоне. scale — доля размера, которую занимает сцена."""
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    W = size * scale
    ox = (size - W) / 2
    oy = (size - W) / 2

    def P(x, y):
        return ox + x * W, oy + y * W

    if brackets:
        ar_brackets(img, P, W)

    # тень под пирамидой
    shadow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    sd = ImageDraw.Draw(shadow)
    sx, sy = P(0.42, 0.9)
    sd.ellipse((sx - W * 0.42, sy - W * 0.07, sx + W * 0.42, sy + W * 0.07), fill=(10, 5, 40, 130))
    shadow = shadow.filter(ImageFilter.GaussianBlur(W * 0.025))
    img.alpha_composite(shadow)

    draw = ImageDraw.Draw(img)
    a = W * 0.155                     # ребро куба
    dx = a * math.cos(math.radians(30))
    dy = a * 0.5
    ow = max(2, int(W * 0.011))

    # Пирамида из ящиков 3-2-1: в ряду кубы касаются вертикальными рёбрами,
    # куб верхнего ряда стоит на стыке двух нижних.
    px0, py0 = P(0.40, 0.93)          # середина нижней точки нижнего ряда
    cy0 = py0 - a - dy                # центр верхней грани нижнего ряда
    order = [CUBE_COLORS[0], CUBE_COLORS[1], CUBE_COLORS[2], CUBE_COLORS[4], CUBE_COLORS[3], CUBE_COLORS[5]]
    n = 0
    for level, count in enumerate([3, 2, 1]):
        for i in range(count):
            cx = px0 + (i - (count - 1) / 2) * 2 * dx
            cy = cy0 - level * a
            iso_cube(draw, cx, cy, a, order[n], ow)
            n += 1

    # Выбитый кубик: отлетает вправо, наклонён
    ka = a * 0.8
    tile = int(ka * 3.2)
    tilt = Image.new("RGBA", (tile, tile), (0, 0, 0, 0))
    iso_cube(ImageDraw.Draw(tilt), tile / 2, tile / 2 - ka * 0.55, ka, CUBE_COLORS[1], ow)
    tilt = tilt.rotate(-28, resample=Image.BICUBIC)
    tx, ty = P(0.86, 0.55)
    img.alpha_composite(tilt, (int(tx - tile / 2), int(ty - tile / 2)))

    # полосы скорости за шаром
    bx, by = P(0.66, 0.25)
    br = W * 0.12
    for k, (off, length, alpha) in enumerate([(-0.55, 0.15, 200), (0.0, 0.2, 230), (0.55, 0.13, 180)]):
        # шар летит из правого верхнего угла влево-вниз → хвост вправо-вверх
        ang = math.radians(-35)
        nx, ny = -math.sin(ang), math.cos(ang)
        sx0 = bx + nx * off * br + math.cos(ang) * br * 0.9
        sy0 = by + ny * off * br + math.sin(ang) * br * 0.9
        sx1 = sx0 + math.cos(ang) * W * length
        sy1 = sy0 + math.sin(ang) * W * length
        draw.line([(sx0, sy0), (sx1, sy1)], fill=(255, 255, 255, alpha), width=int(W * 0.03))
        r = W * 0.015
        draw.ellipse((sx1 - r, sy1 - r, sx1 + r, sy1 + r), fill=(255, 255, 255, alpha))
        draw.ellipse((sx0 - r, sy0 - r, sx0 + r, sy0 + r), fill=(255, 255, 255, alpha))

    # шар
    bs = int(br * 2)
    b = ball(bs)
    ring = Image.new("RGBA", (bs + ow * 4, bs + ow * 4), (0, 0, 0, 0))
    ImageDraw.Draw(ring).ellipse((0, 0, bs + ow * 4 - 1, bs + ow * 4 - 1), fill=OUTLINE + (255,))
    img.alpha_composite(ring, (int(bx - br - ow * 2), int(by - br - ow * 2)))
    img.alpha_composite(b, (int(bx - br), int(by - br)))
    return img


def save(img, name):
    img.resize((OUT, OUT), Image.LANCZOS).save(os.path.join(OUT_DIR, name))


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    bg = background(S)

    # Адаптивная иконка: лаунчер показывает центральные 66% и может обрезать кругом → сцена ~54%
    fg = foreground(S, 0.54)
    save(bg, "icon_bg.png")
    save(fg, "icon_fg.png")

    # Обычная квадратная — сцена крупнее, с AR-рамкой
    full = bg.convert("RGBA")
    full.alpha_composite(foreground(S, 0.74, brackets=True))
    save(full, "icon_full.png")

    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).ellipse((0, 0, S - 1, S - 1), fill=255)
    rnd = bg.convert("RGBA")
    rnd.alpha_composite(foreground(S, 0.66))
    rnd.putalpha(mask)
    save(rnd, "icon_round.png")

    # Превью: адаптивная иконка в масках «круг», «сквиркл», и маленький размер
    adaptive = bg.convert("RGBA")
    adaptive.alpha_composite(fg)
    # лаунчер показывает центральные 72/108 = 66.7% слоя
    crop = int(S * (1 - 72 / 108) / 2)
    vis = adaptive.crop((crop, crop, S - crop, S - crop)).resize((512, 512), Image.LANCZOS)
    circ = Image.new("L", (512, 512), 0)
    ImageDraw.Draw(circ).ellipse((0, 0, 511, 511), fill=255)
    sq = Image.new("L", (512, 512), 0)
    ImageDraw.Draw(sq).rounded_rectangle((0, 0, 511, 511), radius=150, fill=255)
    prev = Image.new("RGBA", (512 * 3 + 160 + 80, 600), (235, 235, 240, 255))
    c1 = vis.copy(); c1.putalpha(circ)
    c2 = vis.copy(); c2.putalpha(sq)
    prev.alpha_composite(c1, (40, 44))
    prev.alpha_composite(c2, (40 + 512 + 40, 44))
    small = c2.resize((96, 96), Image.LANCZOS)
    prev.alpha_composite(small, (40 + 1024 + 80 + 40, 44))
    small2 = c1.resize((48, 48), Image.LANCZOS)
    prev.alpha_composite(small2, (40 + 1024 + 80 + 64, 200))
    prev.save(os.path.join(ROOT, "Tools", "icon_preview.png"))  # вне Assets — в игру не попадает
    print("ok:", OUT_DIR)


if __name__ == "__main__":
    sys.exit(main())
