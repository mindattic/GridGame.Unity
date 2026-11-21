"""GG-T539: the lesson popups' images, cut from a coach-marks harness run.

A run that plays through the lessons with `-coach` saves, for every popup and coach mark it meets, a screenshot
(`<tag>-NN-<lesson>-<step>.png`, taken with super=2) and the mark's geometry beside it (`.coach.json`: the boxed
target, a move's dotted route and its destination, in design pixels; `GET /coach` on the automation endpoint). This
script takes each lesson's first mark, crops the screenshot to the box, the route and the arrow with some of the board
around them, draws a thin gold frame, and writes Assets/Gaiden/Resources/Tutorial/<lesson>.png (always PNG). The image
already shows the real coach mark (the gold box, the arrow or the dotted path), so nothing is painted over the game.

    python tools/lesson_images.py artifacts/<run> [--only move,spell] [--dry]

Lesson file names: a battle lesson's coach id without its "battle-" prefix (move, pincer, spell, potion, ...); Camp's
keep theirs (camp-shop, hub-shop, equip). Re-run after a UI change to regenerate them all.
"""
import glob
import json
import os

import sys

from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "Gaiden", "Resources", "Tutorial")
DESIGN_W = 1170.0
WIDTH = 900          # output width in px
PAD = 150            # design px kept around the mark
MIN_W, MIN_H = 1000, 480
GOLD = (240, 196, 72)


def lesson_name(coach_id):
    return coach_id[len("battle-"):] if coach_id.startswith("battle-") else coach_id


def mark_bounds(c):
    """Design-px rectangle around everything the mark draws: box, destination box and route."""
    xs, ys = [], []
    for key in ("box", "goal"):
        b = c.get(key)
        if b:
            xs += [b[0], b[2]]
            ys += [b[1], b[3]]
    for p in c.get("path") or []:
        xs.append(p[0])
        ys.append(p[1])
    if not xs:
        return None
    return min(xs), min(ys), max(xs), max(ys)


def crop_box(bounds, img_w, img_h, scale):
    x0, y0, x1, y1 = bounds
    x0, y0, x1, y1 = x0 - PAD, y0 - PAD, x1 + PAD, y1 + PAD
    # Keep a readable minimum size and a landscape-ish shape for the popup.
    if x1 - x0 < MIN_W:
        cx = (x0 + x1) / 2
        x0, x1 = cx - MIN_W / 2, cx + MIN_W / 2
    if y1 - y0 < MIN_H:
        cy = (y0 + y1) / 2
        y0, y1 = cy - MIN_H / 2, cy + MIN_H / 2
    box = [int(x0 * scale), int(y0 * scale), int(x1 * scale), int(y1 * scale)]
    # Slide back inside the screenshot.
    if box[0] < 0:
        box[2] -= box[0]; box[0] = 0
    if box[1] < 0:
        box[3] -= box[1]; box[1] = 0
    if box[2] > img_w:
        box[0] -= box[2] - img_w; box[2] = img_w
    if box[3] > img_h:
        box[1] -= box[3] - img_h; box[3] = img_h
    return [max(0, box[0]), max(0, box[1]), min(img_w, box[2]), min(img_h, box[3])]


def render(png, c):
    img = Image.open(png).convert("RGB")
    scale = img.width / DESIGN_W
    bounds = mark_bounds(c)
    if bounds is None:
        return None
    cut = img.crop(crop_box(bounds, img.width, img.height, scale))
    h = int(cut.height * WIDTH / cut.width)
    # A tall route (most of the board) is capped so the popup stays short; the crop scales down to fit.
    if h > WIDTH * 1.1:
        h = int(WIDTH * 1.1)
        w = int(cut.width * h / cut.height)
        cut = cut.resize((w, h), Image.LANCZOS)
        canvas = Image.new("RGB", (WIDTH, h), (8, 10, 28))
        canvas.paste(cut, ((WIDTH - w) // 2, 0))
        cut = canvas
    else:
        cut = cut.resize((WIDTH, h), Image.LANCZOS)
    d = ImageDraw.Draw(cut)
    d.rounded_rectangle([2, 2, cut.width - 3, cut.height - 3], radius=18, outline=GOLD, width=5)
    return cut


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(2)
    run = sys.argv[1]
    only = None
    if "--only" in sys.argv:
        only = set(sys.argv[sys.argv.index("--only") + 1].split(","))
    dry = "--dry" in sys.argv
    os.makedirs(OUT, exist_ok=True)
    firsts = {}
    for js in sorted(glob.glob(os.path.join(run, "*.coach.json"))):
        c = json.load(open(js, encoding="utf-8"))
        if c.get("popup") or not c.get("box"):
            continue
        name = lesson_name(c["id"])
        if only and name not in only:
            continue
        firsts.setdefault(name, (js[: -len(".coach.json")] + ".png", c))
    for name, (png, c) in firsts.items():
        if not os.path.exists(png):
            continue
        im = render(png, c)
        if im is None:
            continue
        dest = os.path.join(OUT, name + ".png")
        print(f"{name:14s} <- {os.path.basename(png)}  {im.width}x{im.height}")
        if not dry:
            im.save(dest, optimize=True)
    print(len(firsts), "lesson images" + (" (dry run)" if dry else f" -> {OUT}"))


if __name__ == "__main__":
    main()
