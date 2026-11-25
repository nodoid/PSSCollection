#!/usr/bin/env python3
"""Draws the collection's app icon and writes every size each platform needs.

The icon is generated rather than hand-drawn so the whole repository (including its art)
can be rebuilt from source. Run from the repository root: python3 tools/make_icons.py
"""
import json
import os
import shutil
import subprocess
import sys

try:
    from PIL import Image, ImageDraw
except ImportError:  # pragma: no cover - developer convenience only
    sys.exit("Pillow is required: python3 -m pip install --user Pillow")

SIZE = 1024
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

GLYPHS = {
    "P": ["11110", "10001", "10001", "11110", "10000", "10000", "10000"],
    "S": ["01111", "10000", "10000", "01110", "00001", "00001", "11110"],
    "O": ["01110", "10001", "10001", "10001", "10001", "10001", "01110"],
    "R": ["11110", "10001", "10001", "11110", "10100", "10010", "10001"],
    "I": ["01110", "00100", "00100", "00100", "00100", "00100", "01110"],
    "C": ["01110", "10001", "10000", "10000", "10000", "10001", "01110"],
}


def draw_pixel_text(draw, text, x, y, scale, fill):
    cursor = x
    for character in text:
        rows = GLYPHS.get(character)
        if rows is None:
            cursor += 6 * scale
            continue
        for row_index, row in enumerate(rows):
            for column_index, bit in enumerate(row):
                if bit == "1":
                    left = cursor + column_index * scale
                    top = y + row_index * scale
                    draw.rectangle([left, top, left + scale - 1, top + scale - 1], fill=fill)
        cursor += 6 * scale


def build_icon():
    image = Image.new("RGBA", (SIZE, SIZE), (5, 7, 16, 255))
    draw = ImageDraw.Draw(image)

    # Vertical gradient.
    for y in range(SIZE):
        t = y / (SIZE - 1)
        r = int(10 + (3 - 10) * t)
        g = int(16 + (5 - 16) * t)
        b = int(34 + (12 - 34) * t)
        draw.line([(0, y), (SIZE, y)], fill=(r, g, b, 255))

    # Faint horizon grid.
    for i in range(0, 18):
        y = 560 + i * i * 3
        if y > SIZE:
            break
        shade = 30 + i * 4
        draw.line([(0, y), (SIZE, y)], fill=(shade, shade + 24, shade + 53, 255))

    for i in range(-10, 11):
        x = SIZE // 2 + i * 96
        draw.line([(SIZE // 2, 540), (x * 2 - SIZE // 2, SIZE)], fill=(16, 30, 58, 255), width=2)

    # Roundel.
    draw.ellipse([112, 112, SIZE - 112, SIZE - 112], outline=(56, 225, 255, 255), width=10)
    draw.ellipse([150, 150, SIZE - 150, SIZE - 150], outline=(38, 96, 150, 255), width=4)

    # PSS in pixel letters.
    scale = 44
    text_width = 3 * 6 * scale - scale
    draw_pixel_text(draw, "PSS", (SIZE - text_width) // 2, 352, scale, (232, 244, 255, 255))

    # Game accent blocks along the bottom of the roundel.
    colours = [(91, 255, 158, 255), (255, 61, 127, 255), (120, 196, 255, 255), (56, 225, 255, 255)]
    block = 46
    gap = 24
    total = len(colours) * block + (len(colours) - 1) * gap
    start = (SIZE - total) // 2
    for index, colour in enumerate(colours):
        left = start + index * (block + gap)
        draw.rounded_rectangle([left, 664, left + block, 664 + block], radius=8, fill=colour)

    return image


def save(image, path, size, mode="RGBA"):
    directory = os.path.dirname(path)
    if directory:
        os.makedirs(directory, exist_ok=True)
    resized = image.resize((size, size), Image.LANCZOS)
    if mode == "RGB":
        resized = resized.convert("RGB")
    resized.save(path)


def write_ios(icon):
    folder = os.path.join(ROOT, "PSSCollection.iOS", "AppIcon.xcassets", "AppIcon.appiconset")
    os.makedirs(folder, exist_ok=True)
    save(icon, os.path.join(folder, "icon-1024.png"), 1024, "RGB")
    contents = {
        "images": [
            {
                "filename": "icon-1024.png",
                "idiom": "universal",
                "platform": "ios",
                "size": "1024x1024",
            }
        ],
        "info": {"author": "xcode", "version": 1},
    }
    with open(os.path.join(folder, "Contents.json"), "w", encoding="utf-8") as handle:
        json.dump(contents, handle, indent=2)
        handle.write("\n")


def write_android(icon):
    densities = {
        "mdpi": 48,
        "hdpi": 72,
        "xhdpi": 96,
        "xxhdpi": 144,
        "xxxhdpi": 192,
    }
    for density, size in densities.items():
        save(icon, os.path.join(ROOT, "PSSCollection.Android", "Resources", f"mipmap-{density}", "icon.png"), size)
    save(icon, os.path.join(ROOT, "PSSCollection.Android", "Resources", "drawable-xxxhdpi", "splash.png"), 384)


def write_windows(icon):
    assets = os.path.join(ROOT, "PSSCollection.WindowsDX", "Assets")
    os.makedirs(assets, exist_ok=True)
    save(icon, os.path.join(assets, "Square44x44Logo.png"), 44)
    save(icon, os.path.join(assets, "Square150x150Logo.png"), 150)
    save(icon, os.path.join(assets, "Square310x310Logo.png"), 310)
    save(icon, os.path.join(assets, "StoreLogo.png"), 50)

    wide = Image.new("RGBA", (310, 150), (6, 8, 16, 255))
    logo = icon.resize((126, 126), Image.LANCZOS)
    wide.alpha_composite(logo, (24, 12))
    draw = ImageDraw.Draw(wide)
    scale = 6
    draw_pixel_text(draw, "PSS", 168, 40, scale, (232, 244, 255, 255))
    draw_pixel_text(draw, "SS", 168, 40, scale, (232, 244, 255, 255))
    wide.save(os.path.join(assets, "Wide310x150Logo.png"))

    ico_path = os.path.join(ROOT, "PSSCollection.WindowsDX", "Icon.ico")
    icon.resize((256, 256), Image.LANCZOS).save(
        ico_path, sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
    )


def write_macos(icon):
    iconset = os.path.join(ROOT, "art", "PSSCollection.iconset")
    if os.path.isdir(iconset):
        shutil.rmtree(iconset)
    os.makedirs(iconset, exist_ok=True)

    sizes = [
        (16, "icon_16x16.png"),
        (32, "icon_16x16@2x.png"),
        (32, "icon_32x32.png"),
        (64, "icon_32x32@2x.png"),
        (128, "icon_128x128.png"),
        (256, "icon_128x128@2x.png"),
        (256, "icon_256x256.png"),
        (512, "icon_256x256@2x.png"),
        (512, "icon_512x512.png"),
        (1024, "icon_512x512@2x.png"),
    ]
    for size, name in sizes:
        save(icon, os.path.join(iconset, name), size)

    icns = os.path.join(ROOT, "PSSCollection.DesktopGL", "PSSCollection.icns")
    os.makedirs(os.path.dirname(icns), exist_ok=True)
    try:
        subprocess.run(["iconutil", "-c", "icns", iconset, "-o", icns], check=True)
    except (OSError, subprocess.CalledProcessError) as error:
        print("iconutil failed:", error)


def main():
    icon = build_icon()
    os.makedirs(os.path.join(ROOT, "art"), exist_ok=True)
    save(icon, os.path.join(ROOT, "art", "icon-1024.png"), 1024)
    write_ios(icon)
    write_android(icon)
    write_windows(icon)
    write_macos(icon)
    print("icons written")


if __name__ == "__main__":
    main()
