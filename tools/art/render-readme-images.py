from __future__ import annotations

import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageOps


ROOT = Path(__file__).resolve().parents[2]
DOC_IMAGES = ROOT / "docs" / "images"
ASSETS = ROOT / "assets"
BACKDROP = DOC_IMAGES / "smart-nap-backdrop-source.png"
LOGO = ASSETS / "smart-nap-logo-v2.png"

FONT = Path("C:/Windows/Fonts/segoeui.ttf")
FONT_BOLD = Path("C:/Windows/Fonts/segoeuib.ttf")
FONT_SEMIBOLD = Path("C:/Windows/Fonts/segoeuib.ttf")


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(str(FONT_BOLD if bold else FONT), size)


def canvas(size=(1600, 900)) -> Image.Image:
    if BACKDROP.exists():
        img = Image.open(BACKDROP).convert("RGB")
        img = ImageOps.fit(img, size, method=Image.Resampling.LANCZOS)
    else:
        img = Image.new("RGB", size, "#05080d")
    veil = Image.new("RGBA", size, (3, 8, 14, 96))
    return Image.alpha_composite(img.convert("RGBA"), veil)


def rounded(draw: ImageDraw.ImageDraw, xy, radius, fill, outline=None, width=1):
    draw.rounded_rectangle(xy, radius=radius, fill=fill, outline=outline, width=width)


def shadow_box(base: Image.Image, xy, radius=28, fill=(8, 16, 28, 232), outline=(70, 100, 138, 160), shadow=(0, 0, 0, 130)):
    x1, y1, x2, y2 = xy
    layer = Image.new("RGBA", base.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    d.rounded_rectangle((x1, y1, x2, y2), radius=radius, fill=shadow)
    layer = layer.filter(ImageFilter.GaussianBlur(26))
    base.alpha_composite(layer)
    d = ImageDraw.Draw(base)
    d.rounded_rectangle(xy, radius=radius, fill=fill, outline=outline, width=1)


def text(draw: ImageDraw.ImageDraw, xy, value, size, color="#f5f7fb", bold=False, anchor=None):
    draw.text(xy, value, font=font(size, bold), fill=color, anchor=anchor)


def pill(draw: ImageDraw.ImageDraw, x, y, label, color, bg="#122035", w=None):
    f = font(22, True)
    tw = int(draw.textlength(label, font=f))
    width = w or tw + 42
    draw.rounded_rectangle((x, y, x + width, y + 44), radius=15, fill=bg, outline=(52, 78, 111, 180))
    draw.text((x + 21, y + 10), label, font=f, fill=color)
    return width


def load_logo(size: int) -> Image.Image:
    logo = Image.open(LOGO).convert("RGBA")
    bbox = logo.getbbox()
    if bbox:
        logo = logo.crop(bbox)
    logo.thumbnail((size, size), Image.Resampling.LANCZOS)
    out = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    out.alpha_composite(logo, ((size - logo.width) // 2, (size - logo.height) // 2))
    return out


def draw_logo_lockup(base: Image.Image, x: int, y: int, scale=1.0):
    d = ImageDraw.Draw(base)
    icon = load_logo(int(78 * scale))
    d.rounded_rectangle((x, y, x + int(88 * scale), y + int(88 * scale)), radius=int(24 * scale), fill=(7, 14, 24, 220), outline=(255, 166, 41, 160))
    base.alpha_composite(icon, (x + int(5 * scale), y + int(5 * scale)))
    text(d, (x + int(108 * scale), y + int(10 * scale)), "SMART NAP", int(31 * scale), bold=True)
    text(d, (x + int(110 * scale), y + int(48 * scale)), "BACKGROUND CONTROL", int(15 * scale), "#8fb3d9", bold=True)


def draw_dashboard(base: Image.Image, x: int, y: int, w: int, h: int):
    d = ImageDraw.Draw(base)
    shadow_box(base, (x, y, x + w, y + h), 24, (6, 13, 24, 245), (255, 166, 41, 150))
    d.rounded_rectangle((x + 1, y + 1, x + 95, y + h - 1), radius=24, fill=(5, 13, 24, 245))

    for i, c in enumerate(["#ffa629", "#4b93ff", "#aebbd0", "#aebbd0", "#aebbd0"]):
        yy = y + 92 + i * 75
        d.rounded_rectangle((x + 25, yy, x + 70, yy + 45), radius=12, fill=(14, 27, 45, 235), outline=(42, 65, 94, 190))
        d.ellipse((x + 42, yy + 17, x + 53, yy + 28), fill=c)

    tx = x + 135
    text(d, (tx, y + 58), "Dashboard", 36, bold=True)
    text(d, (tx, y + 101), "Smart Background Nap", 18, "#9eb8d6")
    status_x = x + w - 370
    for label, bw in [("LIVE", 86), ("MOTOR ACTIVE", 132), ("STARTUP ON", 116)]:
        d.rounded_rectangle((status_x, y + 55, status_x + bw, y + 91), radius=18, fill=(15, 73, 49, 220), outline=(38, 184, 111, 180))
        text(d, (status_x + bw // 2, y + 65), label, 14, "#2ee184", True, "ma")
        status_x += bw + 14

    hero = (tx, y + 135, x + w - 65, y + 345)
    d.rounded_rectangle(hero, radius=18, fill=(12, 24, 41, 238), outline=(54, 84, 120, 170))
    text(d, (tx + 35, y + 187), "Quiet mode", 34, bold=True)
    text(d, (tx + 36, y + 238), "Foreground stays ready.", 19, "#adc3dc")
    px = tx + 38
    for label, color in [("CPU priority", "#7db3ff"), ("RAM trim", "#ffa629"), ("EcoQoS", "#2ee184"), ("Fast wake", "#b99cff")]:
        pw = pill(d, px, y + 279, label, color)
        px += pw + 14

    engine = (x + w - 430, y + 167, x + w - 95, y + 325)
    d.rounded_rectangle(engine, radius=18, fill=(9, 18, 32, 245), outline=(66, 98, 138, 190))
    text(d, (engine[0] + 28, engine[1] + 29), "Nap Engine", 25, bold=True)
    d.rounded_rectangle((engine[2] - 128, engine[1] + 24, engine[2] - 28, engine[1] + 55), radius=15, fill=(13, 72, 48, 220))
    text(d, (engine[2] - 78, engine[1] + 33), "ACTIVE", 12, "#2ee184", True, "ma")
    draw_ring(d, engine[0] + 78, engine[1] + 103, 44, 0.78)
    text(d, (engine[0] + 78, engine[1] + 93), "66", 28, bold=True, anchor="mm")
    text(d, (engine[0] + 78, engine[1] + 120), "apps", 12, "#9eb8d6", anchor="mm")
    for i, (k, v) in enumerate([("NEXT", "04:32"), ("WAKE", "Fast")]):
        bx = engine[0] + 145 + i * 82
        d.rounded_rectangle((bx, engine[1] + 76, bx + 70, engine[1] + 126), radius=10, fill=(12, 26, 45, 235), outline=(46, 72, 105, 180))
        text(d, (bx + 12, engine[1] + 88), k, 10, "#8198b5", True)
        text(d, (bx + 12, engine[1] + 105), v, 15, bold=True)

    card_y = y + 372
    card_w = (w - 185) // 4
    for i, (k, v, col) in enumerate([
        ("Auto mode", "On", "#4b93ff"),
        ("Startup", "On", "#2ee184"),
        ("Smart Learning", "On", "#9a72ff"),
        ("Last result", "59 apps", "#ffa629"),
    ]):
        cx = tx + i * (card_w + 18)
        d.rounded_rectangle((cx, card_y, cx + card_w, card_y + 98), radius=14, fill=(13, 27, 47, 238), outline=(44, 72, 108, 180))
        d.rectangle((cx, card_y, cx + card_w, card_y + 4), fill=col)
        text(d, (cx + 22, card_y + 31), k, 15, "#9eb8d6")
        text(d, (cx + 22, card_y + 67), v, 28, bold=True)

    table = (tx, y + 500, x + w - 65, y + h - 45)
    d.rounded_rectangle(table, radius=16, fill=(11, 22, 38, 240), outline=(47, 74, 108, 170))
    text(d, (table[0] + 22, table[1] + 28), "Live Manager", 23, bold=True)
    text(d, (table[2] - 195, table[1] + 32), "tracking latest entries", 14, "#8fa9c9", True)
    header_y = table[1] + 62
    d.rounded_rectangle((table[0] + 22, header_y, table[2] - 22, header_y + 38), radius=8, fill=(19, 38, 63, 245))
    table_w = table[2] - table[0]
    col_app = 42
    col_score = int(table_w * 0.40)
    col_delta = int(table_w * 0.54)
    col_cpu = int(table_w * 0.66)
    col_action = int(table_w * 0.76)
    for px2, label in [(col_app, "App"), (col_score, "Score"), (col_delta, "Delta"), (col_cpu, "CPU"), (col_action, "Action")]:
        text(d, (table[0] + px2, header_y + 10), label, 14, "#a9bad0", True)
    rows = [("Discord", "164.6", "403.9 MB", "0.0", "Light  P OK  M OK  IO OK"),
            ("zen", "148.8", "0.0 MB", "0.7", "Balanced / cooldown"),
            ("chrome", "101.8", "209.5 MB", "0.0", "Deep / OK")]
    for i, row in enumerate(rows):
        ry = header_y + 45 + i * 38
        d.line((table[0] + 22, ry + 30, table[2] - 22, ry + 30), fill=(43, 65, 92, 150))
        text(d, (table[0] + col_app, ry + 5), row[0], 14)
        text(d, (table[0] + col_score, ry + 5), row[1], 14, "#2ee184", True)
        text(d, (table[0] + col_delta, ry + 5), row[2], 14, "#ffa629")
        text(d, (table[0] + col_cpu, ry + 5), row[3], 14)
        text(d, (table[0] + col_action, ry + 5), row[4], 13, "#8fc3ff", True)


def draw_ring(d, cx, cy, r, progress, width=14):
    d.ellipse((cx - r, cy - r, cx + r, cy + r), outline=(33, 50, 73), width=width)
    for i, col in enumerate(["#4b93ff", "#36d1d4", "#2ee184"]):
        start = -90 + i * progress * 240 / 3
        end = -90 + (i + 1) * progress * 240 / 3
        d.arc((cx - r, cy - r, cx + r, cy + r), start=start, end=end, fill=col, width=width)


def make_showcase():
    img = canvas()
    d = ImageDraw.Draw(img)
    draw_logo_lockup(img, 92, 74, 0.86)
    text(d, (96, 190), "Keep apps open.", 56, bold=True)
    text(d, (96, 258), "Background sleeps.", 56, bold=True)
    text(d, (100, 335), "Local Windows nap engine.", 22, "#b6c9df")
    px = 100
    for label, color in [("No telemetry", "#2ee184"), ("No app killing", "#ffa629"), ("Fast wake", "#7db3ff")]:
        px += pill(d, px, 395, label, color, bg=(13, 24, 40, 210)) + 14
    draw_dashboard(img, 690, 92, 810, 690)
    text(d, (101, 820), "Single EXE • .NET 9 / WebView2 • Smart Learning • Permission Guard", 22, "#9eb8d6")
    img.save(DOC_IMAGES / "smart-nap-showcase.png", quality=94)


def make_engine_story():
    img = canvas()
    d = ImageDraw.Draw(img)
    draw_logo_lockup(img, 82, 64, 0.68)
    text(d, (82, 175), "A quieter session without closing apps", 50, bold=True)
    text(d, (84, 233), "Smart Background Nap lowers safe background pressure, then restores responsiveness when you switch back.", 23, "#b6c9df")
    left = (86, 310, 735, 750)
    right = (865, 310, 1514, 750)
    shadow_box(img, left, 24, (20, 20, 29, 236), (94, 45, 53, 185))
    shadow_box(img, right, 24, (10, 27, 45, 238), (57, 100, 130, 185))
    text(d, (left[0] + 35, left[1] + 45), "Before", 38, "#ff7b85", True)
    text(d, (right[0] + 35, right[1] + 45), "After", 38, "#2ee184", True)
    text(d, (left[0] + 35, left[1] + 93), "Open apps keep competing in the background.", 20, "#b6c1ce")
    text(d, (right[0] + 35, right[1] + 93), "Adaptive nap tiers reduce pressure safely.", 20, "#b6c1ce")
    before = [("Browser helpers", .84), ("Chat + overlays", .68), ("Launchers", .74), ("Capture tools", .58)]
    after = [("Light nap", .32, "#7db3ff"), ("Balanced nap", .43, "#2ee184"), ("Deep nap", .22, "#ffa629"), ("Fast wake guard", .62, "#9a72ff")]
    for i, (label, pct) in enumerate(before):
        y = left[1] + 160 + i * 62
        text(d, (left[0] + 45, y), label, 20)
        d.rounded_rectangle((left[0] + 270, y + 4, left[2] - 55, y + 22), radius=9, fill=(62, 37, 47, 235))
        d.rounded_rectangle((left[0] + 270, y + 4, left[0] + 270 + int((left[2] - left[0] - 325) * pct), y + 22), radius=9, fill="#ff6674")
    for i, (label, pct, col) in enumerate(after):
        y = right[1] + 160 + i * 62
        text(d, (right[0] + 45, y), label, 20)
        d.rounded_rectangle((right[0] + 270, y + 4, right[2] - 55, y + 22), radius=9, fill=(23, 40, 61, 235))
        d.rounded_rectangle((right[0] + 270, y + 4, right[0] + 270 + int((right[2] - right[0] - 325) * pct), y + 22), radius=9, fill=col)
    for i, (title, value, col) in enumerate([("Priority", "Below normal", "#7db3ff"), ("Memory", "Low pressure", "#2ee184"), ("I/O", "Low contention", "#ffa629")]):
        x = right[0] + 45 + i * 185
        d.rounded_rectangle((x, right[3] - 96, x + 160, right[3] - 35), radius=14, fill=(12, 31, 51, 235), outline=(55, 85, 120, 160))
        text(d, (x + 17, right[3] - 75), title, 15, "#93a9c4", True)
        text(d, (x + 17, right[3] - 53), value, 16, col, True)
    d.line((760, 525, 840, 525), fill="#ffa629", width=5)
    d.polygon([(840, 525), (820, 510), (820, 540)], fill="#ffa629")
    img.save(DOC_IMAGES / "smart-nap-engine-story.png", quality=94)


def make_intelligence():
    img = canvas()
    d = ImageDraw.Draw(img)
    draw_logo_lockup(img, 82, 64, 0.68)
    text(d, (82, 175), "Smart when you want more control", 50, bold=True)
    text(d, (84, 233), "Optional learning and permission-aware passes give advanced users more reach without making the app invasive.", 23, "#b6c9df")
    cards = [
        (86, 318, 492, 742, "Smart Learning", "Learns local app behavior", "#9a72ff",
         ["Fast-wake profiles for apps you revisit", "Stronger naps under memory pressure", "Local profiles, no telemetry"]),
        (597, 318, 1003, 742, "Permission Guard", "Shows what Windows refused", "#ffa629",
         ["Lists apps that denied process changes", "One UAC pass when you choose", "Does not stay elevated"]),
        (1108, 318, 1514, 742, "Safety Model", "Built for daily use", "#2ee184",
         ["No drivers or services", "No power-plan switching", "Restore state and logs stay local"]),
    ]
    for x1, y1, x2, y2, title, subtitle, col, bullets in cards:
        shadow_box(img, (x1, y1, x2, y2), 25, (10, 23, 39, 238), (63, 94, 130, 170))
        d.rounded_rectangle((x1 + 30, y1 + 30, x1 + 92, y1 + 92), radius=19, fill=(18, 34, 55, 245), outline=col)
        d.ellipse((x1 + 50, y1 + 50, x1 + 72, y1 + 72), fill=col)
        text(d, (x1 + 30, y1 + 136), title, 32, bold=True)
        text(d, (x1 + 31, y1 + 178), subtitle, 20, "#aac0d8")
        yy = y1 + 238
        for bullet in bullets:
            d.ellipse((x1 + 34, yy + 7, x1 + 47, yy + 20), fill=col)
            text(d, (x1 + 62, yy), bullet, 19)
            yy += 62
        d.rounded_rectangle((x1 + 30, y2 - 82, x2 - 30, y2 - 34), radius=16, fill=(12, 30, 50, 235), outline=(58, 90, 130, 150))
        text(d, (x1 + 52, y2 - 68), "Local-first by design", 18, col, True)
    img.save(DOC_IMAGES / "smart-nap-intelligence.png", quality=94)


def main():
    DOC_IMAGES.mkdir(parents=True, exist_ok=True)
    make_showcase()
    make_engine_story()
    make_intelligence()


if __name__ == "__main__":
    main()
