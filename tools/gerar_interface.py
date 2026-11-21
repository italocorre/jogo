# Gera as texturas da interface (pergaminho, madeira, latão, dobrão...). Uso: python3 gerar_interface.py Assets/Resources/UI
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageChops
import math, os, sys

OUT = sys.argv[1]
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(7)


def value_noise(w, h, scale, seed=0, octaves=5):
    r = np.random.default_rng(seed)
    out = np.zeros((h, w), np.float32)
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        s = max(2, int(scale * (2 ** o)))
        g = r.random((s + 1, s + 1)).astype(np.float32)
        img = Image.fromarray((g * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)
        out += np.asarray(img, np.float32) / 255 * amp
        tot += amp
        amp *= 0.5
    return out / tot


def rounded_mask(w, h, r, inset=0):
    m = Image.new('L', (w, h), 0)
    ImageDraw.Draw(m).rounded_rectangle([inset, inset, w - 1 - inset, h - 1 - inset], r, fill=255)
    return m


def to_img(rgb, alpha):
    a = np.clip(alpha, 0, 255).astype(np.uint8)
    c = np.clip(rgb, 0, 255).astype(np.uint8)
    return Image.fromarray(np.dstack([c, a]), 'RGBA')


# ---------- Pergaminho ----------
def parchment(w=512, h=512, name='parchment.png'):
    n = value_noise(w, h, 6, 1)
    fine = value_noise(w, h, 40, 2, 3)
    base = np.array([236, 220, 180], np.float32)
    dark = np.array([196, 160, 108], np.float32)
    t = np.clip(n * 1.2 - 0.1, 0, 1)[..., None]
    rgb = base * (1 - t) + dark * t
    rgb *= (0.92 + fine[..., None] * 0.16)
    # Bordas queimadas
    yy, xx = np.mgrid[0:h, 0:w]
    edge = np.minimum(np.minimum(xx, w - 1 - xx), np.minimum(yy, h - 1 - yy)).astype(np.float32)
    rough = value_noise(w, h, 18, 3, 3) * 22
    e = edge - rough
    burn = np.clip(1 - e / 46, 0, 1) ** 1.6
    rgb = rgb * (1 - burn[..., None] * 0.55) + np.array([90, 55, 25]) * burn[..., None] * 0.55
    # Manchas
    stains = np.clip((value_noise(w, h, 4, 9, 3) - 0.62) * 4, 0, 1)
    rgb = rgb * (1 - stains[..., None] * 0.18)
    alpha = np.clip((e - 6) * 40, 0, 255)
    to_img(rgb, alpha).save(os.path.join(OUT, name))


# ---------- Madeira ----------
def wood_tex(w, h, seed, dark=False):
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    n = value_noise(w, h, 3, seed, 4)
    grain = np.sin((yy * 0.9 + n * 60) * 0.35) * 0.5 + 0.5
    fine = value_noise(w, h, 60, seed + 1, 2)
    c1 = np.array([92, 58, 34] if not dark else [58, 36, 22], np.float32)
    c2 = np.array([126, 84, 50] if not dark else [82, 52, 32], np.float32)
    t = (grain * 0.6 + fine * 0.4)[..., None]
    rgb = c1 * (1 - t) + c2 * t
    # tábuas
    plank = 64
    seams = (np.abs(((yy + 3) % plank) - plank / 2) > plank / 2 - 1.5)
    rgb[seams] *= 0.55
    return rgb


def brass(w, h, seed=5):
    n = value_noise(w, h, 8, seed, 3)
    yy = np.mgrid[0:h, 0:w][0].astype(np.float32) / h
    base = np.array([214, 170, 84], np.float32)
    hi = np.array([255, 226, 150], np.float32)
    lo = np.array([140, 98, 40], np.float32)
    t = np.clip(1 - yy * 1.2 + (n - 0.5) * 0.3, 0, 1)[..., None]
    rgb = lo * (1 - t) + hi * t
    return rgb * 0.5 + base * 0.5


def wood_panel(w=256, h=256, name='wood_panel.png', r=26, border=10):
    rgb = wood_tex(w, h, 11, dark=True)
    outer = np.asarray(rounded_mask(w, h, r), np.float32)
    inner = np.asarray(rounded_mask(w, h, r - border, border), np.float32)
    b = brass(w, h)
    rim = (outer > 0) & (inner == 0)
    rgb[rim] = b[rim]
    # sombra interna
    sh = np.asarray(Image.fromarray(inner.astype(np.uint8)).filter(ImageFilter.GaussianBlur(6)), np.float32) / 255
    rgb = rgb * (0.55 + 0.45 * np.where(inner > 0, sh, 1)[..., None])
    img = to_img(rgb, outer * 0.97)
    d = ImageDraw.Draw(img)
    for cx, cy in [(border + 8, border + 8), (w - border - 9, border + 8), (border + 8, h - border - 9), (w - border - 9, h - border - 9)]:
        d.ellipse([cx - 5, cy - 5, cx + 5, cy + 5], fill=(240, 205, 120, 255), outline=(110, 72, 30, 255))
        d.ellipse([cx - 2, cy - 3, cx + 1, cy], fill=(255, 245, 200, 255))
    img.save(os.path.join(OUT, name))


def slot(name, glow=False, size=128):
    w = h = size
    r = 22
    rgb = wood_tex(w, h, 21, dark=True) * 0.75
    outer = np.asarray(rounded_mask(w, h, r), np.float32)
    inner = np.asarray(rounded_mask(w, h, r - 6, 6), np.float32)
    b = brass(w, h, 9) * (1.15 if glow else 0.85)
    rim = (outer > 0) & (inner == 0)
    rgb[rim] = b[rim]
    sh = np.asarray(Image.fromarray(inner.astype(np.uint8)).filter(ImageFilter.GaussianBlur(9)), np.float32) / 255
    rgb = rgb * (0.4 + 0.6 * np.where(inner > 0, sh, 1)[..., None])
    a = outer * np.where(inner > 0, 0.96, 1.0)
    if glow:
        inner_glow = np.asarray(Image.fromarray((255 - inner).astype(np.uint8)).filter(ImageFilter.GaussianBlur(10)), np.float32) / 255
        rgb += np.array([255, 200, 90]) * (inner_glow * (inner > 0))[..., None] * 0.6
    to_img(rgb, a).save(os.path.join(OUT, name))


def button(name, hover=False, w=320, h=96):
    r = 20
    rgb = wood_tex(w, h, 31 if not hover else 32) * (1.25 if hover else 1.0)
    outer = np.asarray(rounded_mask(w, h, r), np.float32)
    inner = np.asarray(rounded_mask(w, h, r - 5, 5), np.float32)
    b = brass(w, h, 4) * (1.1 if hover else 0.9)
    rim = (outer > 0) & (inner == 0)
    rgb[rim] = b[rim]
    yy = np.mgrid[0:h, 0:w][0].astype(np.float32) / h
    rgb = rgb * (1.15 - yy[..., None] * 0.35)
    to_img(rgb, outer).save(os.path.join(OUT, name))


def bar_frame(name='bar_frame.png', w=256, h=40):
    outer = np.asarray(rounded_mask(w, h, 18), np.float32)
    inner = np.asarray(rounded_mask(w, h, 13, 5), np.float32)
    rgb = brass(w, h, 13)
    rgb[inner > 0] = np.array([30, 20, 14])
    a = np.where(inner > 0, 200, outer)
    to_img(rgb, a).save(os.path.join(OUT, name))


def bar_fill(name, c_top, c_bot, w=256, h=30):
    yy = np.mgrid[0:h, 0:w][0].astype(np.float32) / (h - 1)
    top = np.array(c_top, np.float32)
    bot = np.array(c_bot, np.float32)
    rgb = top * (1 - yy[..., None]) + bot * yy[..., None]
    hl = np.exp(-((yy - 0.22) ** 2) / 0.01)[..., None] * 60
    rgb = rgb + hl
    outer = np.asarray(rounded_mask(w, h, 13), np.float32)
    to_img(rgb, outer).save(os.path.join(OUT, name))


def coin(name='coin.png', s=128):
    img = Image.new('RGBA', (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse([4, 6, s - 4, s - 2], fill=(110, 70, 20, 255))
    d.ellipse([4, 2, s - 4, s - 6], fill=(196, 140, 40, 255))
    d.ellipse([12, 10, s - 12, s - 14], fill=(240, 190, 70, 255))
    d.ellipse([18, 16, s - 18, s - 20], outline=(176, 120, 30, 255), width=3)
    # Âncora
    cx, cy = s // 2, s // 2 - 2
    col = (150, 98, 22, 255)
    d.line([cx, cy - 26, cx, cy + 24], fill=col, width=7)
    d.ellipse([cx - 8, cy - 36, cx + 8, cy - 20], outline=col, width=5)
    d.line([cx - 16, cy - 14, cx + 16, cy - 14], fill=col, width=6)
    d.arc([cx - 26, cy - 4, cx + 26, cy + 30], 20, 160, fill=col, width=7)
    d.polygon([(cx - 27, cy + 8), (cx - 32, cy + 20), (cx - 19, cy + 17)], fill=col)
    d.polygon([(cx + 27, cy + 8), (cx + 32, cy + 20), (cx + 19, cy + 17)], fill=col)
    # brilho
    hl = Image.new('L', (s, s), 0)
    ImageDraw.Draw(hl).ellipse([22, 14, 62, 40], fill=90)
    hl = hl.filter(ImageFilter.GaussianBlur(8))
    white = Image.new('RGBA', (s, s), (255, 250, 220, 0))
    white.putalpha(ImageChops.multiply(hl, img.getchannel('A')))
    img.alpha_composite(white)
    img.save(os.path.join(OUT, name))


def divider(name='divider.png', w=512, h=32):
    img = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = (120, 78, 36, 255)
    cy = h // 2
    for x in range(20, w // 2 - 22):
        a = int(255 * min(1, (x - 20) / 120))
        d.point((x, cy), fill=(c[0], c[1], c[2], a))
        d.point((w - 1 - x, cy), fill=(c[0], c[1], c[2], a))
        d.point((x, cy + 1), fill=(c[0], c[1], c[2], a))
        d.point((w - 1 - x, cy + 1), fill=(c[0], c[1], c[2], a))
    d.polygon([(w // 2, cy - 10), (w // 2 + 12, cy), (w // 2, cy + 11), (w // 2 - 12, cy)], fill=c)
    d.polygon([(w // 2, cy - 5), (w // 2 + 6, cy), (w // 2, cy + 6), (w // 2 - 6, cy)], fill=(214, 170, 84, 255))
    for sx in (-1, 1):
        d.ellipse([w // 2 + sx * 22 - 3, cy - 3, w // 2 + sx * 22 + 3, cy + 4], fill=c)
    img.save(os.path.join(OUT, name))


def compass(name='compass.png', s=256):
    img = Image.new('RGBA', (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    c = s / 2
    gold = (214, 170, 84, 255)
    dark = (90, 58, 26, 255)
    d.ellipse([10, 10, s - 10, s - 10], outline=gold, width=5)
    d.ellipse([24, 24, s - 24, s - 24], outline=dark, width=2)
    for i in range(32):
        a = i / 32 * math.tau
        r0 = s / 2 - 24
        r1 = r0 - (12 if i % 4 == 0 else 6)
        d.line([c + math.cos(a) * r0, c + math.sin(a) * r0, c + math.cos(a) * r1, c + math.sin(a) * r1], fill=dark, width=2)
    def star(rad, wid, rot, fill1, fill2):
        for k in range(4):
            a = rot + k * math.pi / 2
            tip = (c + math.cos(a) * rad, c + math.sin(a) * rad)
            l = (c + math.cos(a - math.pi / 2) * wid, c + math.sin(a - math.pi / 2) * wid)
            r = (c + math.cos(a + math.pi / 2) * wid, c + math.sin(a + math.pi / 2) * wid)
            d.polygon([(c, c), tip, l], fill=fill1)
            d.polygon([(c, c), tip, r], fill=fill2)
    star(s / 2 - 40, 16, math.pi / 4, (150, 100, 40, 255), (110, 70, 28, 255))
    star(s / 2 - 22, 22, -math.pi / 2, (235, 200, 120, 255), (180, 130, 60, 255))
    d.ellipse([c - 10, c - 10, c + 10, c + 10], fill=gold, outline=dark, width=2)
    # Norte
    d.polygon([(c, 2), (c - 9, 18), (c + 9, 18)], fill=(170, 40, 30, 255))
    img.save(os.path.join(OUT, name))


def icon_heart(name='icon_heart.png', s=64):
    img = Image.new('RGBA', (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse([6, 10, 34, 38], fill=(150, 20, 20, 255))
    d.ellipse([30, 10, 58, 38], fill=(150, 20, 20, 255))
    d.polygon([(8, 28), (56, 28), (32, 58)], fill=(150, 20, 20, 255))
    d.ellipse([10, 13, 31, 33], fill=(215, 50, 45, 255))
    d.ellipse([33, 13, 54, 33], fill=(215, 50, 45, 255))
    d.polygon([(12, 26), (52, 26), (32, 52)], fill=(215, 50, 45, 255))
    d.ellipse([15, 16, 24, 23], fill=(255, 170, 160, 255))
    img.save(os.path.join(OUT, name))


def icon_bubble(name='icon_bubble.png', s=64):
    img = Image.new('RGBA', (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for (x, y, r) in [(30, 34, 20), (50, 16, 9), (14, 12, 6)]:
        d.ellipse([x - r, y - r, x + r, y + r], fill=(90, 190, 240, 150), outline=(220, 245, 255, 255), width=3)
        d.ellipse([x - r * 0.55, y - r * 0.6, x - r * 0.15, y - r * 0.2], fill=(255, 255, 255, 230))
    img.save(os.path.join(OUT, name))


def scroll(name='scroll.png', w=320, h=72):
    # Faixa de pergaminho com pontas enroladas (para avisos e o "[E]")
    n = value_noise(w, h, 5, 41, 4)
    base = np.array([236, 218, 176], np.float32)
    dark = np.array([200, 166, 112], np.float32)
    t = np.clip(n * 1.1, 0, 1)[..., None]
    rgb = base * (1 - t) + dark * t
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    edge = np.minimum(yy, h - 1 - yy)
    rgb *= (0.75 + 0.25 * np.clip(edge / 10, 0, 1))[..., None]
    a = np.full((h, w), 255.0)
    m = np.asarray(rounded_mask(w, h, 10, 2), np.float32)
    a = np.minimum(a, m)
    img = to_img(rgb, a)
    d = ImageDraw.Draw(img)
    for x in (6, w - 22):
        d.rounded_rectangle([x, 0, x + 16, h - 1], 7, fill=(196, 160, 104, 255), outline=(120, 82, 40, 255), width=2)
        d.line([x + 8, 4, x + 8, h - 5], fill=(150, 110, 60, 255), width=2)
    img.save(os.path.join(OUT, name))


parchment()
wood_panel()
slot('slot.png')
slot('slot_selected.png', glow=True)
button('button.png')
button('button_hover.png', hover=True)
bar_frame()
bar_fill('bar_red.png', (235, 70, 55), (130, 20, 20))
bar_fill('bar_blue.png', (110, 210, 250), (20, 90, 160))
bar_fill('bar_gold.png', (255, 220, 120), (170, 110, 30))
coin()
divider()
compass()
icon_heart()
icon_bubble()
scroll()
print('ok')
