"""Reads a painted relief map for the Overworld and derives terrain from its colours (GG-T1669, epic GG-T1652).

The owner paints one top-down relief map per region (brown ranges, snow, green valleys, forest, teal rivers, swamp
channels...). The painting is the map art shown as-is; this tool only reads it. Palettes differ per map, so the
classifier is per-region: k-means on the image's colours (CIE Lab, after a median blur so classes come out as blobs),
then each cluster is labelled by the nearest colour in that region's tools/world_map_palettes.json entry. The palette's
colours also seed k-means, so re-clustering keeps the same labels. A majority (mode) filter on the label image removes
speckle.

    python tools/world_map.py classify <image> --region <id> [--k 8] [--out DIR]
    python tools/world_map.py classify <image> --region <id> --suggest      # print a starter palette entry

classify writes, into --out (default: next to the image):
    world_map-<region>-classindex.png   8-bit image, pixel = index into the region's "classes" list
    world_map-<region>-classes.png      debug overlay: class colours blended over the art, with a legend
    world_map-<region>-classes.json     summary: per class its travel cost and area %, plus the fitted clusters

--suggest ignores any existing palette entry, clusters the image and guesses a label per cluster (water = blue/teal or
dark low-saturation slate, snow = near-white, rock = brown, grass = light yellow-green, forest = dark green); paste the
printed entry into world_map_palettes.json under the region id and correct the labels. A cost of null is impassable.

Later steps (cost map, routes, spots, tiles) are further subcommands on the same parser.
"""
import argparse
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

PALETTES = Path(__file__).resolve().parent / "world_map_palettes.json"

# Overlay colour per label; labels not listed fall back to a grey.
LABEL_COLOURS = {
    "water": (40, 110, 255),
    "snow": (255, 255, 255),
    "rock": (200, 70, 30),
    "grass": (170, 240, 60),
    "forest": (0, 110, 40),
    "reeds": (255, 210, 0),
    "marsh": (190, 120, 255),
    "sand": (255, 230, 150),
    "shadow": (60, 60, 60),
}

DEFAULT_COSTS = {"water": None, "snow": 8, "rock": 6, "grass": 1, "forest": 3, "reeds": 4, "marsh": 5, "sand": 2}

SAMPLE = 60000
LIGHTNESS_WEIGHT = 0.5  # Lab L is scaled by this before clustering, so hue/chroma separate classes more than shading
BLUR = 5
MODE = 7
OVERLAY_ALPHA = 0.55


# ---------------------------------------------------------------- colour

def srgb_to_lab(rgb):
    """rgb: (..., 3) floats 0..255 -> (..., 3) CIE Lab (D65)."""
    c = np.asarray(rgb, dtype=np.float64) / 255.0
    c = np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    m = np.array([[0.4124564, 0.3575761, 0.1804375],
                  [0.2126729, 0.7151522, 0.0721750],
                  [0.0193339, 0.1191920, 0.9503041]])
    xyz = c @ m.T / np.array([0.95047, 1.0, 1.08883])
    f = np.where(xyz > 216 / 24389, np.cbrt(xyz), (24389 / 27 * xyz + 16) / 116)
    return np.stack([116 * f[..., 1] - 16, 500 * (f[..., 0] - f[..., 1]), 200 * (f[..., 1] - f[..., 2])], axis=-1)


def hex_to_rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def rgb_to_hex(rgb):
    return "#%02x%02x%02x" % tuple(int(round(v)) for v in rgb)


# ---------------------------------------------------------------- k-means

def kmeans(x, k, init=None, pinned=0, iters=40, seed=1):
    """Lloyd k-means on rows of x. init: optional (m, d) seed centroids, m <= k; the rest come from k-means++.
    The first `pinned` seeds never move, so a rare but distinct colour (a thin river) keeps its own cluster."""
    rng = np.random.default_rng(seed)
    centres = [] if init is None else [np.asarray(c, dtype=np.float64) for c in init[:k]]
    if not centres:
        centres.append(x[rng.integers(len(x))])
    while len(centres) < k:
        d = np.min(((x[:, None, :] - np.array(centres)[None]) ** 2).sum(-1), axis=1)
        centres.append(x[rng.choice(len(x), p=d / d.sum())])
    c = np.array(centres)
    for _ in range(iters):
        lab = nearest(x, c)
        moved = np.array([x[lab == i].mean(0) if i >= pinned and np.any(lab == i) else c[i] for i in range(k)])
        if np.allclose(moved, c, atol=1e-3):
            break
        c = moved
    return c


def nearest(x, centres, chunk=200000):
    out = np.empty(len(x), dtype=np.int32)
    for s in range(0, len(x), chunk):
        d = ((x[s:s + chunk, None, :] - centres[None]) ** 2).sum(-1)
        out[s:s + chunk] = d.argmin(1)
    return out


# ---------------------------------------------------------------- palettes

def load_palettes():
    if PALETTES.exists():
        return json.loads(PALETTES.read_text(encoding="utf-8"))
    return {}


def guess_label(lab):
    """Starter label for a cluster centre in Lab, for --suggest; the owner corrects it."""
    l, a, b = lab
    chroma = float(np.hypot(a, b))
    hue = float(np.degrees(np.arctan2(b, a)) % 360)
    if l > 82 and chroma < 18:
        return "snow"
    if 150 <= hue <= 300 and chroma > 4:
        return "water"
    if l < 38 and chroma < 12:
        return "water"
    if hue < 85 and a > 2:
        return "rock"
    if l > 50 and b > 35:
        return "grass"
    return "forest"


# ---------------------------------------------------------------- classify

def prepare(path, blur=BLUR):
    img = Image.open(path).convert("RGB")
    blurred = img.filter(ImageFilter.MedianFilter(blur)) if blur > 1 else img
    lab = srgb_to_lab(np.asarray(blurred, dtype=np.float64))
    return img, lab


def fit(feat, k, seeds=None, pinned=0):
    flat = feat.reshape(-1, 3)
    rng = np.random.default_rng(0)
    sample = flat[rng.choice(len(flat), size=min(SAMPLE, len(flat)), replace=False)]
    return kmeans(sample, k, init=seeds, pinned=pinned)


def mode_filter(index, size):
    return np.asarray(Image.fromarray(index.astype(np.uint8)).filter(ImageFilter.ModeFilter(size)))


def close_mask(mask, size):
    """Morphological closing (dilate, then erode) so a river broken by highlights or thin reaches stays one water body."""
    im = Image.fromarray(mask.astype(np.uint8) * 255)
    return np.asarray(im.filter(ImageFilter.MaxFilter(size)).filter(ImageFilter.MinFilter(size))) > 0


def draw_overlay(img, index, classes, areas):
    art = np.asarray(img, dtype=np.float64)
    colours = np.array([LABEL_COLOURS.get(c["label"], (128, 128, 128)) for c in classes], dtype=np.float64)
    blended = art * (1 - OVERLAY_ALPHA) + colours[index] * OVERLAY_ALPHA
    out = Image.fromarray(blended.clip(0, 255).astype(np.uint8))
    draw = ImageDraw.Draw(out)
    try:
        font = ImageFont.truetype("arial.ttf", 22)
    except OSError:
        font = ImageFont.load_default()
    row = 30
    x0, y0 = 16, 16
    draw.rectangle([x0, y0, x0 + 300, y0 + 16 + row * len(classes)], fill=(0, 0, 0))
    for i, c in enumerate(classes):
        y = y0 + 8 + i * row
        draw.rectangle([x0 + 8, y, x0 + 32, y + 22], fill=tuple(int(v) for v in colours[i]), outline=(255, 255, 255))
        cost = "impassable" if c["cost"] is None else "cost %g" % c["cost"]
        draw.text((x0 + 42, y), "%s  %.1f%%  %s" % (c["label"], areas[i], cost), fill=(255, 255, 255), font=font)
    return out


def cmd_classify(args):
    image = Path(args.image)
    palettes = load_palettes()
    entry = None if args.suggest else palettes.get(args.region)
    img, lab = prepare(image, (entry or {}).get("blur", BLUR))
    weight = np.array([(entry or {}).get("lightness_weight", LIGHTNESS_WEIGHT), 1.0, 1.0])
    feat = lab * weight

    if entry is None:
        k = args.k or 8
        centres = fit(feat, k) / weight
        clusters = [{"rgb": rgb_to_hex(rgb_of(c)), "label": guess_label(c)} for c in centres]
        labels = sorted({c["label"] for c in clusters}, key=list(DEFAULT_COSTS).index)
        suggestion = {
            "image": image.name,
            "k": k,
            "classes": [{"label": l, "cost": DEFAULT_COSTS[l]} for l in labels],
            "clusters": clusters,
        }
        if args.suggest:
            print(json.dumps({args.region: suggestion}, indent=2))
            return 0
        print("no palette entry for region '%s'; using guessed labels (run --suggest and add one)" % args.region,
              file=sys.stderr)
        entry = suggestion

    classes = entry["classes"]
    names = [c["label"] for c in classes]
    pal = sorted(entry["clusters"], key=lambda c: not c.get("pin", False))
    pal_feat = srgb_to_lab(np.array([hex_to_rgb(c["rgb"]) for c in pal], dtype=np.float64)) * weight
    pal_class = np.array([names.index(c["label"]) for c in pal])
    k = args.k or entry.get("k", len(pal))

    centres = fit(feat, k, seeds=pal_feat, pinned=sum(1 for c in pal if c.get("pin")))
    cluster_class = pal_class[nearest(centres, pal_feat)]
    index = cluster_class[nearest(feat.reshape(-1, 3), centres)].reshape(lab.shape[:2])
    mode = entry.get("mode", MODE)
    if mode > 1:
        index = mode_filter(index, mode)
    close = entry.get("water_close", 0)
    if close > 1 and "water" in names:
        water = close_mask(index == names.index("water"), close)
        index = np.where(water, names.index("water"), index)

    total = index.size
    areas = [100.0 * np.count_nonzero(index == i) / total for i in range(len(classes))]

    out = Path(args.out) if args.out else image.parent
    out.mkdir(parents=True, exist_ok=True)
    stem = "world_map-%s" % args.region
    Image.fromarray(index.astype(np.uint8), mode="L").save(out / ("%s-classindex.png" % stem))
    draw_overlay(img, index, classes, areas).save(out / ("%s-classes.png" % stem))
    summary = {
        "region": args.region,
        "image": str(image),
        "size": [img.width, img.height],
        "classes": [{"index": i, "label": c["label"], "cost": c["cost"], "area_pct": round(areas[i], 2)}
                    for i, c in enumerate(classes)],
        "clusters": [{"rgb": rgb_to_hex(rgb_of(c / weight)), "label": names[cluster_class[i]]}
                     for i, c in enumerate(centres)],
    }
    (out / ("%s-classes.json" % stem)).write_text(json.dumps(summary, indent=2), encoding="utf-8")
    for c in summary["classes"]:
        print("%-8s %6.2f%%  cost %s" % (c["label"], c["area_pct"], c["cost"]))
    print("wrote %s" % (out / ("%s-classes.png" % stem)))
    return 0


def rgb_of(lab):
    """Lab -> sRGB 0..255 (inverse of srgb_to_lab), for writing cluster colours as hex."""
    l, a, b = lab
    fy = (l + 16) / 116
    f = np.array([fy + a / 500, fy, fy - b / 200])
    xyz = np.where(f ** 3 > 216 / 24389, f ** 3, (116 * f - 16) / (24389 / 27)) * np.array([0.95047, 1.0, 1.08883])
    m = np.array([[3.2404542, -1.5371385, -0.4985314],
                  [-0.9692660, 1.8760108, 0.0415560],
                  [0.0556434, -0.2040259, 1.0572252]])
    c = np.clip(m @ xyz, 0, 1)
    c = np.where(c <= 0.0031308, 12.92 * c, 1.055 * c ** (1 / 2.4) - 0.055)
    return c * 255


def main(argv=None):
    p = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    sub = p.add_subparsers(dest="cmd", required=True)
    c = sub.add_parser("classify", help="k-means terrain classes for a painted relief map")
    c.add_argument("image")
    c.add_argument("--region", required=True, help="region id; its entry in world_map_palettes.json labels the clusters")
    c.add_argument("--k", type=int, help="cluster count (default: the palette entry's k, else 8)")
    c.add_argument("--out", help="output directory (default: the image's directory)")
    c.add_argument("--suggest", action="store_true", help="print a starter palette entry with guessed labels")
    c.set_defaults(func=cmd_classify)
    args = p.parse_args(argv)
    return args.func(args)


if __name__ == "__main__":
    sys.exit(main())
