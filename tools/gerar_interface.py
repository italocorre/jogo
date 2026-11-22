# Gera as texturas do jogo:
#  - Interface "Latão e vidro" (painéis de vidro escuro com fio dourado)  -> Assets/Resources/UI
#  - Pinceladas e grão de papel do mundo "Pintado à mão"                   -> Assets/Resources/Textures
# Uso: python3 tools/gerar_interface.py
import math, os, random
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets', 'Resources')
UI = os.path.join(ROOT, 'UI')
TX = os.path.join(ROOT, 'Textures')
os.makedirs(UI, exist_ok=True)
os.makedirs(TX, exist_ok=True)

GLASS = (6, 15, 23)
BRASS = (225, 186, 110)
GOLD = (255, 210, 122)


def save(img, folder, name):
    img.save(os.path.join(folder, name))


def rounded(size, radius, fill, outline=None, width=1, scale=4):
    w, h = size
    big = Image.new('RGBA', (w * scale, h * scale), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)
    d.rounded_rectangle([0, 0, w * scale - 1, h * scale - 1], radius * scale, fill=fill,
                        outline=outline, width=width * scale if outline else 0)
    return big.resize((w, h), Image.LANCZOS)


def glass_panel():
    s = 64
    img = rounded((s, s), 3, GLASS + (222,), BRASS + (160,), 1)
    d = ImageDraw.Draw(img)
    # Cantoneiras de latão (canto superior esquerdo e inferior direito)
    c = GOLD + (255,)
    d.line([(0, 0), (13, 0)], fill=c, width=2)
    d.line([(0, 0), (0, 13)], fill=c, width=2)
    d.line([(s - 1, s - 1), (s - 14, s - 1)], fill=c, width=2)
    d.line([(s - 1, s - 1), (s - 1, s - 14)], fill=c, width=2)
    save(img, UI, 'glass_panel.png')


def glass_slot():
    s = 64
    save(rounded((s, s), 4, GLASS + (205,), BRASS + (115,), 1), UI, 'glass_slot.png')
    img = rounded((s, s), 4, (40, 32, 14, 215), GOLD + (255,), 2)
    # brilho interno
    glow = Image.new('L', (s, s), 0)
    ImageDraw.Draw(glow).rounded_rectangle([2, 2, s - 3, s - 3], 4, outline=150, width=6)
    glow = glow.filter(ImageFilter.GaussianBlur(4))
    layer = Image.new('RGBA', (s, s), GOLD + (0,))
    layer.putalpha(glow.point(lambda v: int(v * 0.55)))
    img.alpha_composite(layer)
    save(img, UI, 'glass_slot_sel.png')


def bars():
    w, h = 64, 12
    save(rounded((w, h), 5, (255, 255, 255, 34)), UI, 'bar_track.png')
    for name, a, b in [('bar_red', (184, 53, 42), (240, 103, 79)), ('bar_blue', (31, 111, 168), (100, 200, 242)), ('bar_gold', (190, 140, 50), (255, 214, 120))]:
        arr = np.zeros((h, w, 4), np.uint8)
        for x in range(w):
            t = x / (w - 1)
            arr[:, x, :3] = [int(a[i] + (b[i] - a[i]) * t) for i in range(3)]
        arr[:, :, 3] = 255
        img = Image.fromarray(arr, 'RGBA')
        mask = rounded((w, h), 5, (255, 255, 255, 255)).getchannel('A')
        img.putalpha(mask)
        save(img, UI, name + '.png')


def key_box():
    save(rounded((40, 40), 4, (0, 0, 0, 0), GOLD + (255,), 2), UI, 'key_box.png')


def glow():
    s = 128
    yy, xx = np.mgrid[0:s, 0:s].astype(np.float32)
    d = np.sqrt((xx - s / 2 + .5) ** 2 + (yy - s / 2 + .5) ** 2) / (s / 2)
    a = np.clip(1 - d, 0, 1) ** 2
    arr = np.zeros((s, s, 4), np.uint8)
    arr[..., :3] = 255
    arr[..., 3] = (a * 255).astype(np.uint8)
    save(Image.fromarray(arr, 'RGBA'), UI, 'glow.png')


def hairline():
    w, h = 256, 4
    arr = np.zeros((h, w, 4), np.uint8)
    arr[..., :3] = BRASS
    x = np.linspace(-1, 1, w)
    a = np.clip(1 - np.abs(x) ** 3, 0, 1) * 200
    arr[1:3, :, 3] = a.astype(np.uint8)
    save(Image.fromarray(arr, 'RGBA'), UI, 'hairline.png')


def coin():
    s = 128
    img = Image.new('RGBA', (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse([4, 6, s - 4, s - 2], fill=(120, 76, 20, 255))
    d.ellipse([4, 2, s - 4, s - 6], fill=(214, 156, 52, 255))
    d.ellipse([12, 10, s - 12, s - 14], fill=(246, 200, 96, 255))
    d.ellipse([20, 18, s - 20, s - 22], outline=(190, 130, 40, 255), width=3)
    cx, cy = s // 2, s // 2 - 2
    col = (160, 104, 26, 255)
    d.line([cx, cy - 24, cx, cy + 22], fill=col, width=6)
    d.ellipse([cx - 7, cy - 33, cx + 7, cy - 19], outline=col, width=4)
    d.line([cx - 14, cy - 13, cx + 14, cy - 13], fill=col, width=5)
    d.arc([cx - 24, cy - 4, cx + 24, cy + 28], 20, 160, fill=col, width=6)
    save(img, UI, 'coin.png')


def brush():
    # Pinceladas suaves (pincel redondo) claras/escuras sobre cinza médio, sem emenda.
    s = 512
    rnd = random.Random(7)
    img = Image.new('L', (s, s), 128)
    d = ImageDraw.Draw(img)
    for _ in range(1800):
        x, y = rnd.random() * s, rnd.random() * s
        ang = rnd.gauss(0.6, 0.45)
        ln = 14 + rnd.random() * 40
        r = 2.5 + rnd.random() * 4.5
        v = int(max(60, min(200, 128 + rnd.gauss(0, 26))))
        steps = int(ln / 2)
        for k in range(steps):
            t = k / max(1, steps - 1)
            rr = r * (0.6 + 0.4 * math.sin(t * math.pi))
            px, py = x + math.cos(ang) * ln * t, y + math.sin(ang) * ln * t
            for ox in (-s, 0, s):
                for oy in (-s, 0, s):
                    cx, cy = px + ox, py + oy
                    if -20 < cx < s + 20 and -20 < cy < s + 20:
                        d.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], fill=v)
    img = img.filter(ImageFilter.GaussianBlur(1.6))
    save(img, TX, 'pinceladas.png')


def paper():
    s = 256
    rnd = np.random.default_rng(3)
    n = rnd.normal(0.5, 0.18, (s, s))
    # fibras: um pouco de ruído esticado
    big = Image.fromarray((np.clip(n, 0, 1) * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(0.6))
    save(big, TX, 'papel.png')


glass_panel()
glass_slot()
bars()
key_box()
glow()
hairline()
coin()
brush()
paper()
print('ok')
