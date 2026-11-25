#!/usr/bin/env python3
"""Builds the Stores/ tree: store artwork, framed screenshots and icons.

Screenshots come from tools/capture_* and the device runs; the artwork is drawn
from the same pixel font as the game (tools/make_branding.py). Run from anywhere:

    python3 tools/make_store_artwork.py
"""

from __future__ import annotations

import shutil
import sys
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "tools"))

import make_branding as mb  # noqa: E402

STORES = ROOT / "Stores"
ICON = ROOT / "art" / "icon-1024.png"

WHITE = mb.WHITE
DIM = mb.DIM
CYAN = mb.CYAN
AMBER = mb.AMBER
GREEN = mb.GREEN
MAGENTA = mb.MAGENTA

CAPTIONS = {
    "title": "THE PSS ORIC COLLECTION",
    "menu": "FIVE ARCADE CLASSICS IN ONE COLLECTION",
    "invaders": "INVADERS - THE CLASSIC DESCENT",
    "centipede": "CENTIPEDE - MUSHROOM FIELD",
    "hopper": "HOPPER - CROSS THE ROADS",
    "storm": "ELEKTRO STORM - DEFEND THE GRID",
    "lightcycle": "LIGHT CYCLE - TRAILS OF LIGHT",
}

ORDER = ["title", "menu", "invaders", "centipede", "hopper", "storm", "lightcycle"]


def ensure(path: Path) -> Path:
    path.mkdir(parents=True, exist_ok=True)
    return path


def logo_lockup(size: tuple[int, int], roundel_centre, radius: int, pixel: int, layout: str) -> Image.Image:
    """The roundel plus the title, laid out for the shape of the image."""
    image = mb.backdrop(size, seed=5)
    draw = ImageDraw.Draw(image)
    mb.roundel(image, roundel_centre, radius, pixel)

    width, height = size
    if layout == "wide":
        left = int(width * 0.36)
        small = mb.fit("THE PSS", int(width * 0.24), int(radius * 0.30))
        mb.draw_text(draw, "THE PSS", left + int(width * 0.19), int(height * 0.30), small, WHITE)
        big = mb.fit("ORIC COLLECTION", int(width * 0.60), int(radius * 0.52))
        mb.draw_text(draw, "ORIC COLLECTION", left + int(width * 0.29), int(height * 0.42), big, WHITE)
        names = "INVADERS   CENTIPEDE   HOPPER   ELEKTRO STORM   LIGHT CYCLE"
        small_px = mb.fit(names, int(width * 0.60), int(radius * 0.14))
        mb.draw_text(draw, names, left + int(width * 0.29), int(height * 0.66), small_px, AMBER)
    else:
        cx = width // 2
        # Start below the roundel so the title can never run into the ring.
        cursor = roundel_centre[1] + radius + int(height * 0.045)
        small = mb.fit("THE PSS", int(width * 0.42), int(radius * 0.22))
        _, h1 = mb.draw_text(draw, "THE PSS", cx, cursor, small, WHITE)
        cursor += int(h1 * 1.5)
        big = mb.fit("ORIC COLLECTION", int(width * 0.86), int(radius * 0.34))
        _, h2 = mb.draw_text(draw, "ORIC COLLECTION", cx, cursor, big, WHITE)
        cursor += int(h2 * 1.7)
        draw.line([(width * 0.12, cursor), (width * 0.88, cursor)], fill=CYAN + (120,), width=max(2, height // 400))
        cursor += int(height * 0.05)

        # Two rows of names fit comfortably; one column of five did not.
        rows = [["INVADERS", "CENTIPEDE", "HOPPER"], ["ELEKTRO STORM", "LIGHT CYCLE"]]
        line_px = mb.fit("INVADERS   CENTIPEDE   HOPPER", int(width * 0.90), int(radius * 0.16))
        for row in rows:
            mb.draw_text(draw, "   ".join(row), cx, cursor, line_px, AMBER)
            cursor += int(line_px * 7 * 2.1)
    return image


def compose_frame(game: Image.Image, canvas_size, game_size, caption: str, tag: str) -> Image.Image:
    frame = mb.backdrop(canvas_size, seed=9)
    draw = ImageDraw.Draw(frame, "RGBA")
    scaled = game.resize(game_size, Image.NEAREST)
    x = (canvas_size[0] - game_size[0]) // 2
    y = (canvas_size[1] - game_size[1]) // 2 - int(canvas_size[1] * 0.03)
    frame.paste(scaled, (x, y))
    draw.rectangle([x - 2, y - 2, x + game_size[0] + 1, y + game_size[1] + 1],
                   outline=CYAN + (110,), width=2)

    pixel = max(2, canvas_size[0] // 420)
    mb.draw_text(draw, caption, canvas_size[0] // 2, y + game_size[1] + int(canvas_size[1] * 0.045),
                 pixel, WHITE)
    mb.draw_text(draw, tag, canvas_size[0] // 2, canvas_size[1] - int(canvas_size[1] * 0.045), max(2, pixel - 1), DIM)
    return frame


def save(image: Image.Image, relative: str, mode: str = "RGB") -> None:
    path = STORES / relative
    ensure(path.parent)
    image.convert(mode).save(path, optimize=True)
    print(f"  {relative}  {image.size[0]}x{image.size[1]}")


def copy(src: Path, relative: str) -> None:
    if not src.exists():
        print(f"  MISSING {src}")
        return
    dst = STORES / relative
    ensure(dst.parent)
    shutil.copyfile(src, dst)
    print(f"  {relative}  (from {src.relative_to(ROOT)})")


def main() -> None:
    icon = Image.open(ICON).convert("RGB")

    print("icons:")
    save(icon, "app-store/icon-1024.png")
    save(icon.resize((512, 512), Image.LANCZOS), "google-play/icon-512.png")
    save(icon.resize((300, 300), Image.LANCZOS), "microsoft-store/store-logo-300x300.png")

    print("artwork:")
    save(logo_lockup((1024, 500), (185, 250), 120, 9, "wide"), "google-play/feature-graphic-1024x500.png")
    save(logo_lockup((720, 1080), (360, 210), 130, 9, "tall"), "microsoft-store/poster-720x1080.png")
    save(logo_lockup((1080, 1080), (540, 300), 190, 12, "tall"), "microsoft-store/box-art-1080x1080.png")
    save(logo_lockup((1920, 1080), (360, 540), 230, 16, "wide"), "microsoft-store/hero-1920x1080.png")

    print("mac screenshots:")
    mac_src = Path("/tmp/mac-shots")
    for name in ORDER:
        src = mac_src / f"{name}.png"
        if not src.exists():
            print(f"  MISSING {src}")
            continue
        frame = compose_frame(Image.open(src).convert("RGB"), (2880, 1800), (2560, 1440),
                              CAPTIONS[name], "MACOS")
        save(frame, f"mac-app-store/screenshots/2880x1800/{name}.png")

    print("windows screenshots:")
    win_src = STORES / "windows" / "shots"
    for name in ORDER:
        src = win_src / f"{name}.png"
        if not src.exists():
            print(f"  MISSING {src}")
            continue
        frame = compose_frame(Image.open(src).convert("RGB"), (1920, 1080), (1280, 720),
                              CAPTIONS[name], "WINDOWS / XBOX")
        save(frame, f"microsoft-store/screenshots/1920x1080/{name}.png")

    print("ios screenshots:")
    for name in ORDER:
        # App Store Connect's required iPhone slot is the Dynamic Island medium display.
        copy(ROOT / "Stores" / "ios" / "screenshots" / "iphone-6.3" / f"{name}.png",
             f"app-store/screenshots/iphone-6.3/{name}.png")
        copy(ROOT / "Stores" / "ios" / "screenshots" / "ipad-13" / f"{name}.png",
             f"app-store/screenshots/ipad-13/{name}.png")

    print("android screenshots:")
    for name in ("menu", "invaders", "centipede", "hopper", "storm", "lightcycle"):
        copy(ROOT / "Stores" / "android" / "screenshots" / "phone" / f"{name}.png",
             f"google-play/screenshots/phone/{name}.png")


if __name__ == "__main__":
    main()
