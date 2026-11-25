#!/usr/bin/env python3
"""Generates the app icon and the launch splashes from the game's own pixel font.

Every image is drawn from PixelFont.cs, so the store icon, the phone splash and
the game itself share one typeface. Run from the repository root:

    python3 tools/make_branding.py
"""

from __future__ import annotations

import random
import re
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parent.parent
FONT_SOURCE = ROOT / "PSSCollection.Core" / "Graphics" / "PixelFont.cs"

NAVY_TOP = (4, 7, 14)
NAVY_BOTTOM = (10, 16, 30)
CYAN = (80, 226, 244)
MAGENTA = (232, 78, 150)
GREEN = (86, 226, 140)
AMBER = (248, 192, 92)
WHITE = (234, 245, 252)
DIM = (120, 146, 176)

GLYPH_W, GLYPH_H, ADVANCE = 5, 7, 6


def load_glyphs() -> dict[str, list[str]]:
    source = FONT_SOURCE.read_text(encoding="utf-8")
    glyphs: dict[str, list[str]] = {}
    for match in re.finditer(r"\['(\\?.)'\]\s*=\s*\"([01]+)\"", source):
        char, bits = match.group(1), match.group(2)
        glyphs[char] = [bits[row * GLYPH_W:(row + 1) * GLYPH_W] for row in range(GLYPH_H)]
    if len(glyphs) < 40:
        raise SystemExit(f"only {len(glyphs)} glyphs read from {FONT_SOURCE}")
    return glyphs


GLYPHS = load_glyphs()


def text_size(text: str, pixel: int) -> tuple[int, int]:
    if not text:
        return 0, GLYPH_H * pixel
    return ((len(text) * ADVANCE - 1) * pixel, GLYPH_H * pixel)


def draw_text(draw: ImageDraw.ImageDraw, text: str, centre_x: int, top: int, pixel: int, colour) -> tuple[int, int]:
    width, height = text_size(text, pixel)
    x = centre_x - width // 2
    for index, char in enumerate(text):
        glyph = GLYPHS.get(char, GLYPHS[" "])
        origin = x + index * ADVANCE * pixel
        for row, bits in enumerate(glyph):
            for column, bit in enumerate(bits):
                if bit != "1":
                    continue
                left = origin + column * pixel
                draw.rectangle([left, top + row * pixel, left + pixel - 1, top + (row + 1) * pixel - 1],
                               fill=colour)
    return width, height


def backdrop(size: tuple[int, int], seed: int = 7) -> Image.Image:
    """Navy gradient with the faint grid and stars the menus use."""
    width, height = size
    image = Image.new("RGB", size, NAVY_TOP)
    draw = ImageDraw.Draw(image)

    for y in range(height):
        ratio = y / max(1, height - 1)
        colour = tuple(round(NAVY_TOP[channel] + (NAVY_BOTTOM[channel] - NAVY_TOP[channel]) * ratio)
                       for channel in range(3))
        draw.line([(0, y), (width, y)], fill=colour)

    # A soft glow in the middle so the roundel has something to sit on.
    glow = Image.new("RGB", size, (0, 0, 0))
    glow_draw = ImageDraw.Draw(glow)
    radius = int(min(width, height) * 0.55)
    glow_draw.ellipse([width // 2 - radius, height // 2 - radius, width // 2 + radius, height // 2 + radius],
                      fill=(6, 18, 30))
    glow = glow.filter(ImageFilter.GaussianBlur(radius // 2))
    image = Image.blend(image, Image.blend(image, glow, 0.35), 0.6)

    draw = ImageDraw.Draw(image)
    step = max(24, min(width, height) // 16)
    grid = (60, 190, 210)
    for x in range(0, width, step):
        fade = 0.05 + 0.05 * (1 - abs(x - width / 2) / (width / 2))
        draw.line([(x, 0), (x, height)], fill=tuple(round(NAVY_TOP[c] + (grid[c] - NAVY_TOP[c]) * fade)
                                                    for c in range(3)))
    for y in range(0, height, step):
        draw.line([(0, y), (width, y)], fill=tuple(round(NAVY_TOP[c] + (grid[c] - NAVY_TOP[c]) * 0.04)
                                                     for c in range(3)))

    stars = random.Random(seed)
    for _ in range(size[0] * size[1] // 9000):
        x, y = stars.randrange(width), stars.randrange(height)
        shade = stars.choice([(90, 120, 150), (140, 180, 210), (200, 230, 245)])
        draw.point((x, y), fill=shade)

    return image


def roundel(image: Image.Image, centre: tuple[int, int], radius: int, pixel: int) -> None:
    draw = ImageDraw.Draw(image, "RGBA")
    cx, cy = centre

    def ring(r: int, colour, thickness: int) -> None:
        draw.ellipse([cx - r, cy - r, cx + r, cy + r], outline=colour, width=thickness)

    draw.ellipse([cx - radius, cy - radius, cx + radius, cy + radius], fill=(10, 26, 40, 150))
    ring(radius, CYAN + (230,), max(3, radius // 40))
    ring(int(radius * 0.88), CYAN + (70,), max(2, radius // 90))

    text_pixel = pixel
    width, height = text_size("PSS", text_pixel)
    while width > radius * 1.45:
        text_pixel -= 1
        width, height = text_size("PSS", text_pixel)

    draw_text(draw, "PSS", cx, cy - height // 2, text_pixel, WHITE)

    # The three colour chips the sprites are built from.
    chip = max(4, text_pixel // 3)
    gap = chip * 2
    total = chip * 3 + gap * 2
    left = cx - total // 2
    top = cy + height // 2 + chip * 2
    for index, colour in enumerate([GREEN, MAGENTA, CYAN]):
        x = left + index * (chip + gap)
        draw.rectangle([x, top, x + chip - 1, top + chip - 1], fill=colour)


def make_icon() -> Image.Image:
    size = 1024
    image = backdrop((size, size), seed=11)
    roundel(image, (size // 2, size // 2 + 10), 380, 30)
    return image


def fit(text: str, max_width: int, start_pixel: int) -> int:
    """The largest pixel size at which the text stays inside max_width."""
    pixel = max(1, start_pixel)
    while pixel > 1 and text_size(text, pixel)[0] > max_width:
        pixel -= 1
    return pixel


def make_splash(size: tuple[int, int]) -> Image.Image:
    width, height = size
    image = backdrop(size, seed=3)
    draw = ImageDraw.Draw(image)

    roundel(image, (width // 2, int(height * 0.235)), int(height * 0.125), 14)

    # Lay the lock-up out top to bottom, one block after another, so no line can
    # ever collide with the next however wide the screen is.
    cursor = int(height * 0.40)

    big = fit("ORIC COLLECTION", int(width * 0.84), height // 22)
    small_title = fit("THE PSS", int(width * 0.60), max(2, round(big * 0.52)))

    _, first_h = draw_text(draw, "THE PSS", width // 2, cursor, small_title, WHITE)
    cursor += int(first_h * 1.3)

    _, second_h = draw_text(draw, "ORIC COLLECTION", width // 2, cursor, big, WHITE)
    cursor += int(second_h * 1.6)

    draw.line([(width * 0.18, cursor), (width * 0.82, cursor)], fill=CYAN + (110,), width=max(2, height // 420))
    cursor += int(height * 0.035)

    small = fit("5 CLASSIC ORIC GAMES BASED ON THE PSS ORIGINALS", int(width * 0.80), height // 60)
    _, small_h = draw_text(draw, "5 CLASSIC ORIC GAMES BASED ON THE PSS ORIGINALS", width // 2, cursor,
                           small, DIM)
    cursor += int(small_h * 2.2)

    names = "INVADERS   CENTIPEDE   HOPPER   ELEKTRO STORM   LIGHT CYCLE"
    names_pixel = fit(names, int(width * 0.88), max(2, round(small * 1.15)))
    draw_text(draw, names, width // 2, cursor, names_pixel, AMBER)

    credit = max(2, small - 1)
    draw_text(draw, "(C) 2025 PAUL F. JOHNSON", width // 2, height - credit * 13, credit, DIM)
    return image


def make_logo() -> Image.Image:
    """A transparent lock-up for Android, which centres it on its own background."""
    size = 512
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)

    roundel(image, (size // 2, int(size * 0.42)), int(size * 0.27), 12)

    title = fit("THE PSS ORIC COLLECTION", int(size * 0.9), size // 16)
    draw_text(draw, "THE PSS ORIC COLLECTION", size // 2, int(size * 0.80), title, WHITE)
    return image


def save(image: Image.Image, path: Path, mode: str = "RGB") -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    image.convert(mode).save(path, optimize=True)
    print(f"  {path.relative_to(ROOT)}  {image.size[0]}x{image.size[1]}")


def main() -> None:
    print("branding:")
    icon = make_icon()
    save(icon, ROOT / "art" / "icon-1024.png")
    save(icon, ROOT / "PSSCollection.iOS" / "AppIcon.xcassets" / "AppIcon.appiconset" / "icon-1024.png")

    for folder, edge in [("mipmap-mdpi", 48), ("mipmap-hdpi", 72), ("mipmap-xhdpi", 96),
                         ("mipmap-xxhdpi", 144), ("mipmap-xxxhdpi", 192)]:
        save(icon.resize((edge, edge), Image.LANCZOS), ROOT / "PSSCollection.Android" / "Resources" / folder / "icon.png")

    splash = make_splash((2560, 1440))
    save(splash, ROOT / "PSSCollection.iOS" / "LaunchArt.png")
    save(make_logo(), ROOT / "PSSCollection.Android" / "Resources" / "drawable-nodpi" / "splash.png", "RGBA")
    save(make_logo().resize((1024, 1024), Image.LANCZOS), ROOT / "art" / "splash-logo.png", "RGBA")
    save(splash, ROOT / "art" / "splash-2560x1440.png")


if __name__ == "__main__":
    main()
