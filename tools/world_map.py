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

    python tools/world_map.py cost <image> --region <id> [--cell 4] [--out DIR]       (GG-T1670)

cost runs classify, then writes:
    world_map-<region>-cost.npz     cost: float32 grid (one value per cell, inf = water), water: per-cell body label, cell
    world_map-<region>-water.png    16-bit full-resolution water body labels (0 = land, 1 = largest body)
    world_map-<region>-cost.png     debug overlay: cost heatmap, water outlines with body number and median width
    world_map-<region>-cost.json    stats: cost spread, mean cost per class, per water body area/bbox/length/widths
Pixel cost = class cost * (1 + slope_weight * slope), slope being the painted shading's lightness gradient (a valley
floor is flat-lit, a ridge has bright and dark faces), averaged per cell; a cell with any water is impassable. Per
region palette keys tune it: cell, slope_weight, slope_full, slope_sigma, water_grow (hysteresis), water_join,
water_link, water_min_area, water_gap (bridge broken river reaches).

Later steps (routes, spots, tiles) are further subcommands on the same parser.
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

CELL = 4             # working-grid cell, px
SLOPE_SIGMA = 2.0    # px, smoothing before the lightness gradient
SLOPE_FULL = 4.0     # lightness gradient (L per px) that counts as full slope
SLOPE_WEIGHT = 1.5   # full slope multiplies a pixel's class cost by 1 + this
WATER_JOIN = 2       # px radius of the closing that mends broken river reaches
WATER_LINK = 4       # px: water pieces this close share one label
WATER_MIN_AREA = 150  # px: smaller water bodies are specks
WATER_GAP = 0        # px: water bodies this close are joined by a channel (one broken river, one body)


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
    if args.suggest:
        print(json.dumps({args.region: suggest_entry(image, args.k)}, indent=2))
        return 0
    img, index, entry, centres, cluster_class, weight, _ = classify(image, args.region, args.k)
    classes = entry["classes"]
    names = [c["label"] for c in classes]
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


def suggest_entry(image, k=None):
    img, lab = prepare(image, BLUR)
    weight = np.array([LIGHTNESS_WEIGHT, 1.0, 1.0])
    k = k or 8
    centres = fit(lab * weight, k) / weight
    clusters = [{"rgb": rgb_to_hex(rgb_of(c)), "label": guess_label(c)} for c in centres]
    labels = sorted({c["label"] for c in clusters}, key=list(DEFAULT_COSTS).index)
    return {
        "image": image.name,
        "k": k,
        "classes": [{"label": l, "cost": DEFAULT_COSTS[l]} for l in labels],
        "clusters": clusters,
    }


def classify(image, region, k=None):
    """Class index image for a painted map. Returns (img, index, entry, centres, cluster_class, weight, feat)."""
    entry = load_palettes().get(region)
    if entry is None:
        print("no palette entry for region '%s'; using guessed labels (run --suggest and add one)" % region,
              file=sys.stderr)
        entry = suggest_entry(image, k)
    img, lab = prepare(image, entry.get("blur", BLUR))
    weight = np.array([entry.get("lightness_weight", LIGHTNESS_WEIGHT), 1.0, 1.0])
    feat = lab * weight

    classes = entry["classes"]
    names = [c["label"] for c in classes]
    pal = sorted(entry["clusters"], key=lambda c: not c.get("pin", False))
    pal_feat = srgb_to_lab(np.array([hex_to_rgb(c["rgb"]) for c in pal], dtype=np.float64)) * weight
    pal_class = np.array([names.index(c["label"]) for c in pal])
    k = k or entry.get("k", len(pal))

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
    return img, index, entry, centres, cluster_class, weight, feat


# ---------------------------------------------------------------- cost

def disk(radius):
    r = int(radius)
    y, x = np.ogrid[-r:r + 1, -r:r + 1]
    return x * x + y * y <= radius * radius


def slope_map(img, sigma):
    """Painted shading as slope: gradient magnitude of Lab lightness (L units per pixel), lightly smoothed first
    so brush texture counts less than the light/shadow faces of a ridge."""
    from scipy import ndimage
    l = ndimage.gaussian_filter(srgb_to_lab(np.asarray(img, dtype=np.float64))[..., 0], sigma)
    return np.hypot(ndimage.sobel(l, 0), ndimage.sobel(l, 1)) / 8.0


def grow_water(water, feat, centres, is_water, ratio):
    """Hysteresis: pixels whose colour is nearly as close to a water cluster as to the nearest land cluster
    (distance ratio <= 1 + ratio) join the water when connected to it, mending river reaches lost to shading."""
    from scipy import ndimage
    flat = feat.reshape(-1, 3)
    dw = np.full(len(flat), np.inf)
    dl = np.full(len(flat), np.inf)
    for c, w in zip(centres, is_water):
        d = np.sqrt(((flat - c) ** 2).sum(-1))
        if w:
            np.minimum(dw, d, out=dw)
        else:
            np.minimum(dl, d, out=dl)
    weak = (dw <= dl * (1 + ratio)).reshape(water.shape) | water
    labels, n = ndimage.label(weak, structure=np.ones((3, 3)))
    seeded = np.zeros(n + 1, bool)
    seeded[np.unique(labels[water])] = True
    seeded[0] = False
    return seeded[labels]


def ranked_labels(water, link, min_area=0):
    """Label water pieces (pieces within `link` px share a label), drop those under min_area px, number the rest
    1..n by area, largest first. Returns (labels, dropped count)."""
    from scipy import ndimage
    grouped = ndimage.binary_dilation(water, disk(link)) if link > 0 else water
    labels, n = ndimage.label(grouped, structure=np.ones((3, 3)))
    labels = np.where(water, labels, 0)
    areas = np.bincount(labels.ravel(), minlength=n + 1)
    keep = areas >= min_area
    keep[0] = False
    remap = np.zeros(n + 1, dtype=np.int32)
    order = sorted(np.flatnonzero(keep), key=lambda i: -areas[i])
    remap[order] = np.arange(1, len(order) + 1)
    return remap[labels], int(np.count_nonzero(areas[1:] > 0) - len(order))


def bridge_gaps(labels, gap):
    """Join water bodies whose nearest points are within `gap` px with a straight channel as wide as the narrower
    body, closest pairs first, so one painted river broken by a highlight or a forest overhang becomes one body."""
    from scipy import ndimage
    n = int(labels.max())
    h, w = labels.shape
    slices = ndimage.find_objects(labels)
    edt = ndimage.distance_transform_edt(labels > 0)
    widths = [max(1.0, 2 * float(edt[s][labels[s] == i + 1].max()) - 1) for i, s in enumerate(slices)]
    pairs = []
    for i, s in enumerate(slices):
        y0, y1 = max(0, s[0].start - gap), min(h, s[0].stop + gap)
        x0, x1 = max(0, s[1].start - gap), min(w, s[1].stop + gap)
        win = labels[y0:y1, x0:x1]
        d, (iy, ix) = ndimage.distance_transform_edt(win != i + 1, return_indices=True)
        for j in np.unique(win[(win > i + 1) & (d <= gap)]):
            sel = np.flatnonzero((win == j).ravel())
            k = sel[np.argmin(d.ravel()[sel])]
            py, px = divmod(int(k), win.shape[1])
            pairs.append((float(d.ravel()[k]), i, int(j) - 1,
                          (x0 + int(ix[py, px]), y0 + int(iy[py, px])), (x0 + px, y0 + py)))
    parent = list(range(n))

    def root(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a
    canvas = Image.fromarray((labels > 0).astype(np.uint8) * 255)
    draw = ImageDraw.Draw(canvas)
    joined = 0
    for _, i, j, a, b in sorted(pairs):
        if root(i) != root(j):
            parent[root(i)] = root(j)
            draw.line([a, b], fill=255, width=max(2, int(round(min(widths[i], widths[j])))))
            joined += 1
    return np.asarray(canvas) > 0, joined


def water_bodies(water, join, link, min_area, gap=0):
    """Water mask -> (label image, components, specks, bridged gaps). The mask is closed with a disk of radius `join`
    so highlights and thin reaches do not break a river; pieces within `link` px of each other share a label (one river,
    one lake); bodies under min_area px are dropped as specks; bodies within `gap` px are joined by a channel. Width per
    body comes from the distance transform along its medial ridge."""
    from scipy import ndimage
    if join > 0:
        water = ndimage.binary_closing(np.pad(water, int(join) + 1, mode="edge"), disk(join))
        water = water[int(join) + 1:-int(join) - 1, int(join) + 1:-int(join) - 1]
    labels, specks = ranked_labels(water, link, min_area)
    joined = 0
    if gap > 0 and labels.max() > 1:
        water, joined = bridge_gaps(labels, gap)
        labels, _ = ranked_labels(water, link)

    edt = ndimage.distance_transform_edt(labels > 0)
    ridge = (labels > 0) & (edt >= ndimage.maximum_filter(edt, size=3))
    bodies = []
    for i, sl in enumerate(ndimage.find_objects(labels), start=1):
        if sl is None:
            continue
        mine = labels[sl] == i
        r = edt[sl][mine & ridge[sl]]
        ys, xs = np.nonzero(mine)
        bodies.append({
            "label": i,
            "area_px": int(mine.sum()),
            "bbox": [sl[1].start, sl[0].start, sl[1].stop, sl[0].stop],
            "centroid": [round(float(xs.mean()) + sl[1].start, 1), round(float(ys.mean()) + sl[0].start, 1)],
            "length_px": int(len(r)),
            "width_median_px": round(float(2 * np.median(r) - 1), 1),
            "width_max_px": round(float(2 * r.max() - 1), 1),
        })
    return labels, bodies, specks, joined


def block_reduce(a, cell, fn):
    h, w = a.shape
    gh, gw = -(-h // cell), -(-w // cell)
    pad = np.pad(a, ((0, gh * cell - h), (0, gw * cell - w)), mode="edge")
    return fn(pad.reshape(gh, cell, gw, cell), axis=(1, 3))


COST_RAMP = np.array([(40, 170, 60), (240, 230, 60), (240, 120, 30), (190, 20, 40)], dtype=np.float64)


def ramp(t):
    t = np.clip(t, 0, 1) * (len(COST_RAMP) - 1)
    i = np.minimum(t.astype(int), len(COST_RAMP) - 2)
    f = (t - i)[..., None]
    return COST_RAMP[i] * (1 - f) + COST_RAMP[i + 1] * f


def draw_cost_overlay(img, cost, labels, bodies, cell, lo, hi):
    from scipy import ndimage
    art = np.asarray(img, dtype=np.float64)
    h, w = art.shape[:2]
    heat = ramp((np.nan_to_num(cost, posinf=hi) - lo) / max(hi - lo, 1e-6))
    heat = np.repeat(np.repeat(heat, cell, 0), cell, 1)[:h, :w]
    blocked = np.repeat(np.repeat(np.isinf(cost), cell, 0), cell, 1)[:h, :w]
    out = art * (1 - OVERLAY_ALPHA) + heat * OVERLAY_ALPHA
    out[blocked] = art[blocked] * 0.5 + np.array([20, 60, 160]) * 0.5
    edge = (labels > 0) & ~ndimage.binary_erosion(labels > 0, np.ones((3, 3)))
    out[ndimage.binary_dilation(edge)] = (0, 255, 255)
    im = Image.fromarray(out.clip(0, 255).astype(np.uint8))
    draw = ImageDraw.Draw(im)
    try:
        font = ImageFont.truetype("arial.ttf", 18)
        big = ImageFont.truetype("arial.ttf", 22)
    except OSError:
        font = big = ImageFont.load_default()
    for b in bodies[:12]:
        x, y = b["centroid"]
        draw.text((x + 6, y - 9), "#%d w%.0f" % (b["label"], b["width_median_px"]), fill=(255, 255, 255), font=font,
                  stroke_width=2, stroke_fill=(0, 0, 0))
    x0, y0 = 16, 16
    draw.rectangle([x0, y0, x0 + 300, y0 + 92], fill=(0, 0, 0))
    for i in range(260):
        draw.line([x0 + 20 + i, y0 + 12, x0 + 20 + i, y0 + 34], fill=tuple(int(v) for v in ramp(i / 259)))
    draw.text((x0 + 20, y0 + 38), "cost %.1f" % lo, fill=(255, 255, 255), font=big)
    draw.text((x0 + 200, y0 + 38), "%.1f" % hi, fill=(255, 255, 255), font=big)
    draw.text((x0 + 20, y0 + 64), "cyan = water bodies (%d)" % len(bodies), fill=(255, 255, 255), font=font)
    return im


def cmd_cost(args):
    image = Path(args.image)
    img, index, entry, centres, cluster_class, _, feat = classify(image, args.region, args.k)
    classes = entry["classes"]
    names = [c["label"] for c in classes]
    cell = args.cell or entry.get("cell", CELL)
    slope_weight = entry.get("slope_weight", SLOPE_WEIGHT)
    slope_full = entry.get("slope_full", SLOPE_FULL)

    water = index == names.index("water") if "water" in names else np.zeros(index.shape, bool)
    grow = entry.get("water_grow", 0)
    if grow > 0 and water.any():
        water = grow_water(water, feat, centres, cluster_class == names.index("water"), grow)
    labels, bodies, specks, joined = water_bodies(water, entry.get("water_join", WATER_JOIN),
                                                  entry.get("water_link", WATER_LINK),
                                                  entry.get("water_min_area", WATER_MIN_AREA),
                                                  entry.get("water_gap", WATER_GAP))
    slope = np.clip(slope_map(img, entry.get("slope_sigma", SLOPE_SIGMA)) / slope_full, 0, 1)
    class_cost = np.array([np.inf if c["cost"] is None else c["cost"] for c in classes], dtype=np.float64)
    base = class_cost[index]
    base = np.where(np.isinf(base) & (labels == 0), np.nan, base)  # specks dropped from water: cost of what's around
    px = base * (1 + slope_weight * slope)

    finite = np.where(np.isfinite(px), px, 0.0)
    count = block_reduce(np.isfinite(px).astype(np.float64), cell, np.sum)
    filler = np.nanmedian(np.where(np.isinf(px), np.nan, px))
    cost = np.where(count > 0, block_reduce(finite, cell, np.sum) / np.maximum(count, 1), filler)
    water_grid = block_reduce(labels, cell, np.max)
    cost = np.where(water_grid > 0, np.inf, cost).astype(np.float32)

    out = Path(args.out) if args.out else image.parent
    out.mkdir(parents=True, exist_ok=True)
    stem = "world_map-%s" % args.region
    np.savez_compressed(out / ("%s-cost.npz" % stem), cost=cost, water=water_grid.astype(np.int32), cell=cell)
    Image.fromarray(labels.astype(np.uint16)).save(out / ("%s-water.png" % stem))
    land = cost[np.isfinite(cost)]
    lo, hi = float(np.percentile(land, 1)), float(np.percentile(land, 99))
    draw_cost_overlay(img, cost, labels, bodies, cell, lo, hi).save(out / ("%s-cost.png" % stem))

    by_class = {}
    for i, c in enumerate(classes):
        sel = (index == i) & np.isfinite(px)
        if c["cost"] is not None and sel.any():
            by_class[c["label"]] = round(float(np.mean(px[sel])), 2)
    stats = {
        "region": args.region,
        "image": str(image),
        "size": [img.width, img.height],
        "cell": cell,
        "grid": [int(cost.shape[1]), int(cost.shape[0])],
        "slope_weight": slope_weight,
        "slope_full": slope_full,
        "cost": {"min": round(float(land.min()), 2), "median": round(float(np.median(land)), 2),
                 "max": round(float(land.max()), 2), "impassable_pct": round(100.0 * np.isinf(cost).mean(), 2)},
        "mean_cost_by_class": by_class,
        "water": {"bodies": len(bodies), "specks_removed": specks, "gaps_bridged": joined, "components": bodies},
    }
    (out / ("%s-cost.json" % stem)).write_text(json.dumps(stats, indent=2), encoding="utf-8")
    print("grid %dx%d (cell %d px), cost median %.2f, impassable %.2f%%" % (
        stats["grid"][0], stats["grid"][1], cell, stats["cost"]["median"], stats["cost"]["impassable_pct"]))
    print("mean cost by class: %s" % ", ".join("%s %.2f" % kv for kv in by_class.items()))
    for b in bodies[:8]:
        print("water #%d  area %d px  length %d  width median %.1f max %.1f" % (
            b["label"], b["area_px"], b["length_px"], b["width_median_px"], b["width_max_px"]))
    print("%d water bodies, %d specks removed, %d gaps bridged" % (len(bodies), specks, joined))
    print("wrote %s" % (out / ("%s-cost.png" % stem)))
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
    c = sub.add_parser("cost", help="travel-cost grid, water bodies and a debug overlay (runs classify)")
    c.add_argument("image")
    c.add_argument("--region", required=True, help="region id in world_map_palettes.json")
    c.add_argument("--k", type=int, help="cluster count (default: the palette entry's k)")
    c.add_argument("--cell", type=int, help="working-grid cell in px (default: the palette entry's cell, else %d)" % CELL)
    c.add_argument("--out", help="output directory (default: the image's directory)")
    c.set_defaults(func=cmd_cost)
    args = p.parse_args(argv)
    return args.func(args)


if __name__ == "__main__":
    sys.exit(main())
