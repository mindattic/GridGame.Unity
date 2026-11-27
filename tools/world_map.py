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

    python tools/world_map.py crossings <image> --region <id> [--cell 4] [--out DIR]  (GG-T1787)

crossings runs cost, then finds bridge/causeway candidates over the grid's water: rays from every shore cell in 32
directions, kept when shorter than bridge_max px, within bridge_angle (25) degrees of the water's normal and over a
straight reach (structure-tensor coherence >= bridge_coherence at the midpoint, integration scale bridge_rho px); per
water body, non-max suppression by bridge_spacing px keeps the narrowest crossing of each reach. Palette keys: crossing
(bridge, or causeway for the swamp), bridge_max, bridge_spacing, bridge_cost (route cost of building one). Writes
    world_map-<region>-crossings.json   params plus each candidate's endpoints (px and grid cell), length, angle, cost
    world_map-<region>-crossings.png    debug overlay: kept crossings bold, every valid ray before spacing in grey
Water is otherwise impassable: passable_moves() gives routes their 8-neighbour moves with no step into water and no
diagonal cut past a water corner.

    python tools/world_map.py routes <image> --region <id> --points a=x,y b=x,y ... [--pairs a-b,...]  (GG-T1784)

routes runs crossings, snaps each point to the nearest land cell and finds least-cost paths (Dijkstra) over land
steps (passable_moves; step cost = mean cell cost x step length in cells) plus a jump edge per crossing (bridge_cost
+ its length in cells when new, only its length once a route has used it). Pairs default to the minimum spanning tree
over pairwise costs, routed cheapest first; cells an earlier route uses cost road_discount (0.5) as much, so routes
share roads and bridges. A route's run through pass_classes (rock, snow) longer than pass_min px is a pass, its node at
the run's highest-cost cell. Each land stretch is Douglas-Peucker simplified (simplify px) then centripetal
Catmull-Rom splined, retried with less simplification if a sample lands in water; bridges stay straight. Writes
    world_map-<region>-routes.json      Nodes (Point, Bridge/Causeway, Pass) and Edges (A, B, Points, Spline, Length px,
                                        Cost, Crossings, NewCrossings, Passes)
    world_map-<region>-routes.png       debug overlay: routes, used crossings bold, unused grey, passes as red triangles

    python tools/world_map.py spots <image> --region <id> [--cell 4] [--out DIR]       (GG-T1792)

spots runs crossings, then suggests where things go. Copses and outcrops are forest / rock clumps at cell resolution
with area in copse_area / outcrop_area px, standing on land (clump_land of the ring around them passable). Locations are
picked greedily from a score (smoothed cost, plus water_bonus at water_near px from water and clump_bonus within
clump_near of a copse or outcrop), location_spacing apart, 2 x edge_margin from the border, at most location_max. Road
ends are the cheapest land half to one edge_margin in from the border, off the corners, one per side first,
roadsign_spacing apart, at most roadsign_max. Routes come from plan_routes(leaves=road ends): a spanning tree over the
locations, then each road end joined to its cheapest location; passes as in routes. Waystones every waystone_spacing
px of road and at each road end; hunt spots on copse / outcrop centroids hunt_off px off a road, hunt_spacing apart.
Every key can be set in the region's palette entry. Writes
    world_map-<region>-spots.json       params, Spots (Location, RoadEnd, Pass, Waystone, Hunt), Crossings, Edges,
                                        Copses, Outcrops
    world_map-<region>-spots.png        debug overlay: roads, spots, clump rings, the edge band and location margin

Later steps (tiles) are further subcommands on the same parser.
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


def cost_grid(image, region, k=None, cell=None):
    """Classify, then build the per-cell travel cost (inf = water). Returns a dict of everything cost and routes use."""
    img, index, entry, centres, cluster_class, _, feat = classify(image, region, k)
    classes = entry["classes"]
    names = [c["label"] for c in classes]
    cell = cell or entry.get("cell", CELL)
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
    return {"img": img, "index": index, "entry": entry, "classes": classes, "names": names, "cell": cell,
            "slope_weight": slope_weight, "slope_full": slope_full, "labels": labels, "bodies": bodies,
            "specks": specks, "joined": joined, "px": px, "cost": cost, "water_grid": water_grid}


def cmd_cost(args):
    image = Path(args.image)
    g = cost_grid(image, args.region, args.k, args.cell)
    img, index, classes, cell, px, cost = g["img"], g["index"], g["classes"], g["cell"], g["px"], g["cost"]
    labels, bodies, specks, joined, water_grid = g["labels"], g["bodies"], g["specks"], g["joined"], g["water_grid"]
    slope_weight, slope_full = g["slope_weight"], g["slope_full"]

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


# ---------------------------------------------------------------- crossings

# Grid moves for routes: 8-neighbourhood, (dy, dx, length in cells). A diagonal step needs both orthogonal neighbours
# passable too, so a path never squeezes between two water cells that touch at a corner.
MOVES = [(dy, dx, float(np.hypot(dy, dx))) for dy in (-1, 0, 1) for dx in (-1, 0, 1) if dy or dx]


def passable_moves(cost):
    """Per move in MOVES, a bool grid: True where stepping from that cell in that direction stays on land. Water is
    never entered (no fording) and diagonals may not cut a water corner; crossings are the only way over water."""
    land = np.isfinite(cost)
    h, w = land.shape
    pad = np.pad(land, 1, constant_values=False)

    def at(dy, dx):
        return pad[1 + dy:1 + dy + h, 1 + dx:1 + dx + w]
    return [land & at(dy, dx) & (at(dy, 0) & at(0, dx) if dy and dx else True) for dy, dx, _ in MOVES]


CROSSING = "bridge"      # crossing kind (swamp: "causeway")
BRIDGE_MAX = 48          # px: longest crossing
BRIDGE_SPACING = 120     # px: crossings on one water body closer than this compete; the shortest survives
BRIDGE_COST = 30         # route cost of building a new crossing (read by routes, GG-T1784)
BRIDGE_ANGLE = 25        # degrees: a crossing runs within this of the water's normal
BRIDGE_COHERENCE = 0.7   # structure-tensor coherence at the midpoint (1 = perfectly straight reach)
BRIDGE_RHO = 14.0        # px: structure-tensor integration scale
RAY_DIRS = 32
RAY_STEP = 0.25          # cells per ray sample


def water_orientation(water, rho):
    """Structure tensor of the water mask: per pixel the water's normal angle (radians, across the reach) and the
    coherence (lambda1 - lambda2) / (lambda1 + lambda2), ~1 along a straight reach, low at bends, forks and pools."""
    from scipy import ndimage
    m = water.astype(np.float64)
    gy = ndimage.gaussian_filter(m, 1.5, order=(1, 0))
    gx = ndimage.gaussian_filter(m, 1.5, order=(0, 1))
    jxx = ndimage.gaussian_filter(gx * gx, rho)
    jyy = ndimage.gaussian_filter(gy * gy, rho)
    jxy = ndimage.gaussian_filter(gx * gy, rho)
    normal = 0.5 * np.arctan2(2 * jxy, jxx - jyy)
    coherence = np.hypot(jxx - jyy, 2 * jxy) / np.maximum(jxx + jyy, 1e-9)
    return normal, coherence


def ray_march(water, starts, bridge_max_cells):
    """Cast RAY_DIRS rays from each start cell (land on a shore) over the water grid. A ray counts when it leaves its
    start, passes only water and reaches land within bridge_max_cells. Returns (start index, direction index, end
    cell y, end cell x) for every ray that does, vectorised over starts x directions x samples."""
    h, w = water.shape
    ang = np.arange(RAY_DIRS) * (2 * np.pi / RAY_DIRS)
    dy, dx = np.sin(ang), np.cos(ang)
    t = np.arange(1, int(np.ceil(bridge_max_cells / RAY_STEP)) + 2) * RAY_STEP
    out = []
    for c0 in range(0, len(starts), 1500):
        sy, sx = starts[c0:c0 + 1500, 0], starts[c0:c0 + 1500, 1]
        yy = np.floor(sy[:, None, None] + 0.5 + dy[None, :, None] * t).astype(np.int32)
        xx = np.floor(sx[:, None, None] + 0.5 + dx[None, :, None] * t).astype(np.int32)
        inside = (yy >= 0) & (yy < h) & (xx >= 0) & (xx < w)
        wet = water[np.clip(yy, 0, h - 1), np.clip(xx, 0, w - 1)] & inside
        home = (yy == sy[:, None, None]) & (xx == sx[:, None, None])
        stop = ~wet & ~home
        k = np.argmax(stop, axis=2)
        si, di = np.nonzero(stop.any(axis=2))
        kk = k[si, di]
        ok = inside[si, di, kk] & wet[si, di, :].cumsum(axis=1)[np.arange(len(si)), kk].astype(bool)
        si, di, kk = si[ok], di[ok], kk[ok]
        out.append((si + c0, di, yy[si, di, kk], xx[si, di, kk]))
    cat = [np.concatenate(p) for p in zip(*out)] if out else [np.zeros(0, np.int64)] * 4
    return cat


def crossing_candidates(g):
    """Bridge/causeway candidates over the cost grid's water: rays from every shore cell in RAY_DIRS directions; keep
    a ray when it is shorter than bridge_max px, runs within bridge_angle degrees of the water's normal and the reach is
    straight (structure-tensor coherence at its midpoint >= bridge_coherence); then per water body non-max suppression
    by bridge_spacing px keeps only the narrowest crossing of each reach. Returns (kept, all valid rays, params)."""
    from scipy import ndimage
    entry, cost, cell, labels = g["entry"], g["cost"], g["cell"], g["labels"]
    p = {"crossing": entry.get("crossing", CROSSING),
         "bridge_max": entry.get("bridge_max", BRIDGE_MAX),
         "bridge_spacing": entry.get("bridge_spacing", BRIDGE_SPACING),
         "bridge_cost": entry.get("bridge_cost", BRIDGE_COST),
         "bridge_angle": entry.get("bridge_angle", BRIDGE_ANGLE),
         "bridge_coherence": entry.get("bridge_coherence", BRIDGE_COHERENCE),
         "bridge_rho": entry.get("bridge_rho", BRIDGE_RHO)}
    water = np.isinf(cost)
    land = ~water
    shore = land & ndimage.binary_dilation(water, np.array([[0, 1, 0], [1, 1, 1], [0, 1, 0]], bool))
    starts = np.argwhere(shore)
    si, di, ey, ex = ray_march(water, starts, p["bridge_max"] / cell)
    if len(si) == 0:
        return [], [], p
    a = (starts[si] + 0.5) * cell                              # px, (y, x) of the start cell centre
    b = (np.stack([ey, ex], 1) + 0.5) * cell
    length = np.hypot(*(b - a).T)
    keep = length <= p["bridge_max"]
    a, b, length, si = a[keep], b[keep], length[keep], si[keep]

    normal, coherence = water_orientation(labels > 0, p["bridge_rho"])
    h, w = labels.shape
    mid = (a + b) / 2
    my = np.clip(mid[:, 0].astype(int), 0, h - 1)
    mx = np.clip(mid[:, 1].astype(int), 0, w - 1)
    ray = np.arctan2(b[:, 0] - a[:, 0], b[:, 1] - a[:, 1])
    off = np.degrees(np.abs((ray - normal[my, mx] + np.pi / 2) % np.pi - np.pi / 2))
    coh = coherence[my, mx]
    body = labels[my, mx]
    if (body == 0).any():  # midpoint on a grid-only water cell: take the nearest water pixel's body
        _, (iy, ix) = ndimage.distance_transform_edt(labels == 0, return_indices=True)
        body = labels[iy[my, mx], ix[my, mx]]
    valid = (off <= p["bridge_angle"]) & (coh >= p["bridge_coherence"])
    rays = [{"a": a[i], "b": b[i], "length": length[i], "off": off[i], "coh": coh[i], "body": int(body[i])}
            for i in np.flatnonzero(valid)]

    kept = []
    for r in sorted(rays, key=lambda r: (r["length"], -r["coh"], r["off"])):
        m = (r["a"] + r["b"]) / 2
        if all(k["body"] != r["body"] or np.hypot(*(k["mid"] - m)) >= p["bridge_spacing"] for k in kept):
            r["mid"] = m
            kept.append(r)
    out = []
    for n, r in enumerate(sorted(kept, key=lambda r: (r["body"], r["mid"][1], r["mid"][0])), start=1):
        out.append({
            "id": "%s-%d" % (p["crossing"], n),
            "kind": p["crossing"],
            "body": r["body"],
            "a": [round(float(r["a"][1]), 1), round(float(r["a"][0]), 1)],
            "b": [round(float(r["b"][1]), 1), round(float(r["b"][0]), 1)],
            "cell_a": [int(r["a"][1] // cell), int(r["a"][0] // cell)],
            "cell_b": [int(r["b"][1] // cell), int(r["b"][0] // cell)],
            "length_px": round(float(r["length"]), 1),
            "off_normal_deg": round(float(r["off"]), 1),
            "coherence": round(float(r["coh"]), 3),
            "cost": p["bridge_cost"],
        })
    return out, rays, p


CROSSING_COLOURS = {"bridge": (255, 200, 40), "causeway": (255, 120, 220)}


def draw_crossings_overlay(img, cost, labels, cell, crossings, rays, p):
    from scipy import ndimage
    art = np.asarray(img, dtype=np.float64)
    h, w = art.shape[:2]
    out = art * 0.6
    blocked = np.repeat(np.repeat(np.isinf(cost), cell, 0), cell, 1)[:h, :w]
    out[blocked] = out[blocked] * 0.4 + np.array([20, 60, 160]) * 0.6
    edge = (labels > 0) & ~ndimage.binary_erosion(labels > 0, np.ones((3, 3)))
    out[edge] = (0, 200, 220)
    im = Image.fromarray(out.clip(0, 255).astype(np.uint8))
    draw = ImageDraw.Draw(im)
    try:
        font = ImageFont.truetype("arial.ttf", 14)
        big = ImageFont.truetype("arial.ttf", 18)
    except OSError:
        font = big = ImageFont.load_default()
    for r in rays:
        draw.line([(r["a"][1], r["a"][0]), (r["b"][1], r["b"][0])], fill=(150, 150, 150), width=1)
    colour = CROSSING_COLOURS.get(p["crossing"], (255, 255, 255))
    for c in crossings:
        draw.line([tuple(c["a"]), tuple(c["b"])], fill=(0, 0, 0), width=7)
        draw.line([tuple(c["a"]), tuple(c["b"])], fill=colour, width=4)
        for e in (c["a"], c["b"]):
            draw.ellipse([e[0] - 4, e[1] - 4, e[0] + 4, e[1] + 4], fill=(255, 255, 255), outline=(0, 0, 0))
        mx, my = (c["a"][0] + c["b"][0]) / 2, (c["a"][1] + c["b"][1]) / 2
        draw.text((mx + 7, my - 7), "%.0f" % c["length_px"], fill=(255, 255, 255), font=font,
                  stroke_width=2, stroke_fill=(0, 0, 0))
    x0, y0 = 16, 16
    draw.rectangle([x0, y0, x0 + 430, y0 + 84], fill=(0, 0, 0))
    draw.line([x0 + 12, y0 + 20, x0 + 52, y0 + 20], fill=colour, width=4)
    draw.text((x0 + 62, y0 + 9), "%d %s candidates (length px)" % (len(crossings), p["crossing"]),
              fill=(255, 255, 255), font=big)
    draw.line([x0 + 12, y0 + 46, x0 + 52, y0 + 46], fill=(150, 150, 150), width=1)
    draw.text((x0 + 62, y0 + 36), "%d valid rays before spacing" % len(rays), fill=(255, 255, 255), font=big)
    draw.text((x0 + 12, y0 + 60), "max %d px, spacing %d px, <=%d deg off normal, coherence >= %.2f" % (
        p["bridge_max"], p["bridge_spacing"], p["bridge_angle"], p["bridge_coherence"]), fill=(255, 255, 255),
        font=font)
    return im


def cmd_crossings(args):
    image = Path(args.image)
    g = cost_grid(image, args.region, args.k, args.cell)
    crossings, rays, p = crossing_candidates(g)
    out = Path(args.out) if args.out else image.parent
    out.mkdir(parents=True, exist_ok=True)
    stem = "world_map-%s" % args.region
    draw_crossings_overlay(g["img"], g["cost"], g["labels"], g["cell"], crossings, rays, p).save(
        out / ("%s-crossings.png" % stem))
    stats = {"region": args.region, "image": str(image), "cell": g["cell"], **p,
             "valid_rays": len(rays), "crossings": crossings}
    (out / ("%s-crossings.json" % stem)).write_text(json.dumps(stats, indent=2), encoding="utf-8")
    lengths = [c["length_px"] for c in crossings]
    print("%d %s candidates from %d valid rays (max %d px, spacing %d px)" % (
        len(crossings), p["crossing"], len(rays), p["bridge_max"], p["bridge_spacing"]))
    if lengths:
        print("length px: min %.1f median %.1f max %.1f" % (min(lengths), float(np.median(lengths)), max(lengths)))
    print("wrote %s" % (out / ("%s-crossings.png" % stem)))
    return 0


# ---------------------------------------------------------------- routes

ROAD_DISCOUNT = 0.5      # cost multiplier on cells an earlier route already uses, so routes share roads and bridges
PASS_CLASSES = ["rock", "snow"]
PASS_MIN = 48            # px: a route's run through pass classes longer than this is a pass
SIMPLIFY = 6.0           # px: Douglas-Peucker tolerance before splining (halved on retry when a sample lands in water)
SPLINE_STEP = 4.0        # px between spline samples


def parse_points(spec):
    """--points as name=x,y words, or one JSON file: {"name": [x, y]} or [{"Id"/"name", "X"/"x", "Y"/"y"}]."""
    if len(spec) == 1 and spec[0].lower().endswith(".json"):
        data = json.loads(Path(spec[0]).read_text(encoding="utf-8"))
        if isinstance(data, dict):
            return [(str(k), float(v[0]), float(v[1])) for k, v in data.items()]
        return [(str(d.get("Id", d.get("name"))), float(d.get("X", d.get("x"))), float(d.get("Y", d.get("y"))))
                for d in data]
    out = []
    for word in spec:
        name, xy = word.split("=", 1)
        x, y = xy.split(",")
        out.append((name, float(x), float(y)))
    return out


def snap_to_land(cost, cell, points):
    """Each (name, x px, y px) -> (name, cell y, cell x, snap distance px) at the nearest land cell."""
    from scipy import ndimage
    h, w = cost.shape
    _, (iy, ix) = ndimage.distance_transform_edt(~np.isfinite(cost), return_indices=True)
    out = []
    for name, x, y in points:
        cy, cx = min(h - 1, max(0, int(y // cell))), min(w - 1, max(0, int(x // cell)))
        sy, sx = int(iy[cy, cx]), int(ix[cy, cx])
        out.append((name, sy, sx, round(float(np.hypot(sy - cy, sx - cx)) * cell, 1)))
    return out


def route_graph(cost, moves, crossings, road, used, discount, cell):
    """Sparse directed graph over grid cells: land steps from passable_moves (mean cell cost x step length in cells,
    road cells discounted) plus a jump edge per crossing (bridge_cost + length in cells when new, only the length once
    a route has used it). Returns (csr matrix, {(u, v): crossing index})."""
    from scipy import sparse
    h, w = cost.shape
    c = np.where(np.isfinite(cost), cost, 0).astype(np.float64)
    c = np.where(road, c * discount, c).ravel()
    rows, cols, data = [], [], []
    for (dy, dx, step), ok in zip(MOVES, moves):
        u = np.flatnonzero(ok)
        v = u + dy * w + dx
        rows.append(u)
        cols.append(v)
        data.append(0.5 * (c[u] + c[v]) * step)
    jump = {}
    for i, k in enumerate(crossings):
        (ax, ay), (bx, by) = k["cell_a"], k["cell_b"]
        if max(abs(ax - bx), abs(ay - by)) <= 1 or not (np.isfinite(cost[ay, ax]) and np.isfinite(cost[by, bx])):
            continue
        u, v = ay * w + ax, by * w + bx
        wgt = k["length_px"] / cell + (0 if i in used else k["cost"])
        for e in ((u, v), (v, u)):
            if e not in jump or wgt < jump[e][1]:
                jump[e] = (i, wgt)
    for (u, v), (_, wgt) in jump.items():
        rows.append(np.array([u]))
        cols.append(np.array([v]))
        data.append(np.array([wgt]))
    g = sparse.csr_matrix((np.concatenate(data), (np.concatenate(rows), np.concatenate(cols))), shape=(h * w, h * w))
    return g, {e: i for e, (i, _) in jump.items()}


def trace(pred, src, dst):
    path = [dst]
    while path[-1] != src:
        p = pred[path[-1]]
        if p < 0:
            return None
        path.append(int(p))
    return path[::-1]


def mst(n, weights):
    """Kruskal over (cost, i, j) with finite costs; a forest when points are cut off from each other."""
    parent = list(range(n))

    def root(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]
            a = parent[a]
        return a
    out = []
    for wgt, i, j in sorted(weights):
        if np.isfinite(wgt) and root(i) != root(j):
            parent[root(i)] = root(j)
            out.append((wgt, i, j))
    return out


def douglas_peucker(pts, eps):
    if eps <= 0 or len(pts) < 3:
        return pts
    a, b = pts[0], pts[-1]
    ab = b - a
    n = np.hypot(*ab)
    rel = pts[1:-1] - a
    d = np.abs(ab[0] * rel[:, 1] - ab[1] * rel[:, 0]) / n if n > 0 else np.hypot(rel[:, 0], rel[:, 1])
    i = int(np.argmax(d))
    if d[i] <= eps:
        return np.array([a, b])
    left = douglas_peucker(pts[:i + 2], eps)
    return np.vstack([left[:-1], douglas_peucker(pts[i + 1:], eps)])


def catmull_rom(pts, step):
    """Centripetal Catmull-Rom through pts (px), about one sample per `step` px, ends included; no overshoot loops."""
    if len(pts) < 3:
        return pts
    p = np.vstack([2 * pts[0] - pts[1], pts, 2 * pts[-1] - pts[-2]])
    out = [pts[0]]
    for i in range(1, len(p) - 2):
        p0, p1, p2, p3 = p[i - 1], p[i], p[i + 1], p[i + 2]
        t1 = np.hypot(*(p1 - p0)) ** 0.5 + 1e-6
        t2 = t1 + np.hypot(*(p2 - p1)) ** 0.5 + 1e-6
        t3 = t2 + np.hypot(*(p3 - p2)) ** 0.5 + 1e-6
        k = max(2, int(np.ceil(np.hypot(*(p2 - p1)) / step)))
        t = np.linspace(t1, t2, k + 1)[1:, None]
        a1 = (t1 - t) / t1 * p0 + t / t1 * p1
        a2 = (t2 - t) / (t2 - t1) * p1 + (t - t1) / (t2 - t1) * p2
        a3 = (t3 - t) / (t3 - t2) * p2 + (t - t2) / (t3 - t2) * p3
        b1 = (t2 - t) / t2 * a1 + t / t2 * a2
        b2 = (t3 - t) / (t3 - t1) * a2 + (t - t1) / (t3 - t1) * a3
        out.extend((t2 - t) / (t2 - t1) * b1 + (t - t1) / (t2 - t1) * b2)
    return np.array(out)


def smooth_stretch(pts, cost, cell, eps, step):
    """Simplify then spline one land stretch; if a sample lands in a water cell, retry with half the tolerance, then
    with none, then keep the raw cell path. Returns (simplified points, spline, tolerance used or None)."""
    h, w = cost.shape
    tries = [eps, eps / 2, eps / 4, 0.0]
    for e in tries:
        simple = douglas_peucker(pts, e)
        spline = catmull_rom(simple, step)
        gy = np.clip((spline[:, 1] // cell).astype(int), 0, h - 1)
        gx = np.clip((spline[:, 0] // cell).astype(int), 0, w - 1)
        if np.isfinite(cost[gy, gx]).all():
            return simple, spline, e
    return pts, pts, None


def pass_runs(path, pass_grid, cost, w, cell, pass_min):
    """Runs of consecutive land cells through pass classes longer than pass_min px -> (crest cell, run px). The crest
    is the run's highest-cost cell. Crossing jumps break a run."""
    runs, cur, run_px = [], [], 0.0
    for k, u in enumerate(path):
        y, x = divmod(u, w)
        prev = path[k - 1] if k else None
        jump = prev is not None and max(abs(prev // w - y), abs(prev % w - x)) > 1
        if pass_grid[y, x] and not jump and cur:
            py, px_ = divmod(prev, w)
            run_px += float(np.hypot(y - py, x - px_)) * cell
            cur.append(u)
        else:
            if cur and run_px > pass_min:
                runs.append((max(cur, key=lambda c: cost.flat[c]), run_px))
            cur, run_px = ([u], 0.0) if pass_grid[y, x] else ([], 0.0)
    if cur and run_px > pass_min:
        runs.append((max(cur, key=lambda c: cost.flat[c]), run_px))
    return runs


def plan_routes(g, crossings, points, pairs_spec=None, leaves=None):
    """Least-cost routes between snapped points. Returns a dict with nodes, edges and the params used. Names in
    `leaves` (road ends) stay out of the spanning tree; each is joined to its cheapest non-leaf point instead."""
    from scipy.sparse import csgraph
    entry, cost, cell = g["entry"], g["cost"], g["cell"]
    h, w = cost.shape
    p = {"road_discount": entry.get("road_discount", ROAD_DISCOUNT),
         "pass_classes": entry.get("pass_classes", PASS_CLASSES),
         "pass_min": entry.get("pass_min", PASS_MIN),
         "simplify": entry.get("simplify", SIMPLIFY),
         "spline_step": entry.get("spline_step", SPLINE_STEP)}
    moves = passable_moves(cost)
    pass_ids = [i for i, n in enumerate(g["names"]) if n in p["pass_classes"]]
    pass_grid = block_reduce(np.isin(g["index"], pass_ids).astype(np.float64), cell, np.mean)[:h, :w] >= 0.5
    pass_grid &= np.isfinite(cost)

    snapped = snap_to_land(cost, cell, points)
    names = [s[0] for s in snapped]
    idx = np.array([s[1] * w + s[2] for s in snapped])
    road = np.zeros(cost.shape, bool)
    used = set()
    graph, _ = route_graph(cost, moves, crossings, road, used, p["road_discount"], cell)
    dist = csgraph.dijkstra(graph, directed=True, indices=idx)[:, idx]
    if pairs_spec:
        order = []
        for pair in pairs_spec.split(","):
            a, b = pair.split("-")
            i, j = names.index(a.strip()), names.index(b.strip())
            order.append((float(dist[i, j]), i, j))
        order.sort()
    else:
        leaf = set(leaves or ())
        core = [i for i, n in enumerate(names) if n not in leaf]
        order = mst(len(names), [(float(dist[i, j]), i, j) for a, i in enumerate(core) for j in core[a + 1:]])
        for i, n in enumerate(names):
            if n in leaf and core:
                j = min(core, key=lambda c: dist[c, i])
                if np.isfinite(dist[j, i]):
                    order.append((float(dist[j, i]), j, i))

    crossing_kind = {"bridge": "Bridge", "causeway": "Causeway"}
    nodes = [{"Id": n, "Kind": "Point", "X": round((sx + 0.5) * cell, 1), "Y": round((sy + 0.5) * cell, 1),
              "Requested": [x, y], "SnapPx": snap}
             for (n, sy, sx, snap), (_, x, y) in zip(snapped, points)]
    passes, edges, unreachable = [], [], []
    for _, i, j in order:
        graph, jump = route_graph(cost, moves, crossings, road, used, p["road_discount"], cell)
        d, pred = csgraph.dijkstra(graph, directed=True, indices=int(idx[i]), return_predecessors=True)
        path = trace(pred, int(idx[i]), int(idx[j])) if np.isfinite(d[idx[j]]) else None
        if path is None:
            unreachable.append([names[i], names[j]])
            continue
        stretches, cur, bridges = [], [path[0]], []
        for u, v in zip(path, path[1:]):
            if max(abs(u // w - v // w), abs(u % w - v % w)) > 1:
                k = jump[(u, v)]
                bridges.append(k)
                stretches.append(cur)
                cur = [v]
            else:
                cur.append(v)
        stretches.append(cur)
        for u in path:
            road.flat[u] = True
        new_bridges = [k for k in bridges if k not in used]
        used.update(bridges)

        pts_all, spline_all, eps_used = [], [], []
        for s_i, s in enumerate(stretches):
            yx = np.array([divmod(u, w) for u in s], dtype=np.float64)
            pts = (yx[:, ::-1] + 0.5) * cell
            simple, spline, e = smooth_stretch(pts, cost, cell, p["simplify"], p["spline_step"])
            eps_used.append(e)
            pts_all.extend(simple)
            spline_all.extend(spline)  # consecutive stretches join with the straight bridge segment between them
        spline_all = np.array(spline_all)
        length = float(np.hypot(*np.diff(spline_all, axis=0).T).sum())

        edge_passes = []
        for crest, run_px in pass_runs(path, pass_grid, cost, w, cell, p["pass_min"]):
            cy, cx = divmod(crest, w)
            xy = np.array([(cx + 0.5) * cell, (cy + 0.5) * cell])
            near = [q for q in passes if np.hypot(q["X"] - xy[0], q["Y"] - xy[1]) < p["pass_min"]]
            if near:
                q = near[0]
                q["RunPx"] = max(q["RunPx"], round(run_px, 1))
            else:
                q = {"Id": "pass-%d" % (len(passes) + 1), "Kind": "Pass", "X": round(float(xy[0]), 1),
                     "Y": round(float(xy[1]), 1), "RunPx": round(run_px, 1), "CrestCost": round(float(cost[cy, cx]), 2)}
                passes.append(q)
            edge_passes.append(q["Id"])
        edges.append({
            "A": names[i], "B": names[j],
            "Points": [[round(float(x), 1), round(float(y), 1)] for x, y in pts_all],
            "Spline": [[round(float(x), 1), round(float(y), 1)] for x, y in spline_all],
            "Length": round(length, 1),
            "Cost": round(float(d[idx[j]]), 1),
            "Crossings": [crossings[k]["id"] for k in bridges],
            "NewCrossings": [crossings[k]["id"] for k in new_bridges],
            "Passes": edge_passes,
            "Simplify": [None if e is None else round(e, 2) for e in eps_used],
        })
    for k in sorted(used):
        c = crossings[k]
        nodes.append({"Id": c["id"], "Kind": crossing_kind.get(c["kind"], c["kind"].title()),
                      "X": round((c["a"][0] + c["b"][0]) / 2, 1), "Y": round((c["a"][1] + c["b"][1]) / 2, 1),
                      "A": c["a"], "B": c["b"], "LengthPx": c["length_px"]})
    nodes.extend(passes)
    return {"params": p, "nodes": nodes, "edges": edges, "unreachable": unreachable, "used": sorted(used)}


def draw_routes_overlay(img, cost, cell, crossings, r):
    art = np.asarray(img, dtype=np.float64)
    h, w = art.shape[:2]
    out = art * 0.7
    blocked = np.repeat(np.repeat(np.isinf(cost), cell, 0), cell, 1)[:h, :w]
    out[blocked] = out[blocked] * 0.5 + np.array([20, 60, 160]) * 0.5
    im = Image.fromarray(out.clip(0, 255).astype(np.uint8))
    draw = ImageDraw.Draw(im)
    try:
        font = ImageFont.truetype("arial.ttf", 16)
        big = ImageFont.truetype("arial.ttf", 18)
    except OSError:
        font = big = ImageFont.load_default()
    for c in crossings:  # unused candidates, faint
        draw.line([tuple(c["a"]), tuple(c["b"])], fill=(120, 120, 120), width=2)
    for e in r["edges"]:
        line = [tuple(q) for q in e["Spline"]]
        draw.line(line, fill=(0, 0, 0), width=7, joint="curve")
        draw.line(line, fill=(255, 245, 200), width=3, joint="curve")
    colour = None
    for n in r["nodes"]:
        x, y = n["X"], n["Y"]
        if n["Kind"] in ("Bridge", "Causeway"):
            colour = CROSSING_COLOURS.get(n["Kind"].lower(), (255, 255, 255))
            draw.line([tuple(n["A"]), tuple(n["B"])], fill=(0, 0, 0), width=9)
            draw.line([tuple(n["A"]), tuple(n["B"])], fill=colour, width=5)
        elif n["Kind"] == "Pass":
            draw.polygon([(x, y - 10), (x - 9, y + 7), (x + 9, y + 7)], fill=(240, 80, 60), outline=(0, 0, 0))
            draw.text((x + 10, y - 8), n["Id"], fill=(255, 255, 255), font=font, stroke_width=2, stroke_fill=(0, 0, 0))
        else:
            rx, ry = n["Requested"]
            if n["SnapPx"] > 0:
                draw.line([(rx, ry), (x, y)], fill=(255, 255, 255), width=1)
            draw.ellipse([x - 8, y - 8, x + 8, y + 8], fill=(255, 255, 255), outline=(0, 0, 0), width=2)
            draw.text((x + 11, y - 9), n["Id"], fill=(255, 255, 255), font=big, stroke_width=3, stroke_fill=(0, 0, 0))
    kinds = {}
    for n in r["nodes"]:
        kinds[n["Kind"]] = kinds.get(n["Kind"], 0) + 1
    total = sum(e["Length"] for e in r["edges"])
    lines = ["%d routes, %.0f px total" % (len(r["edges"]), total),
             ", ".join("%d %s" % (v, k.lower()) for k, v in kinds.items()),
             "road discount %.2f, pass >%d px through %s" % (r["params"]["road_discount"], r["params"]["pass_min"],
                                                            "/".join(r["params"]["pass_classes"]) or "-")]
    if r["unreachable"]:
        lines.append("unreachable: " + ", ".join("%s-%s" % tuple(u) for u in r["unreachable"]))
    x0, y0 = 16, 16
    draw.rectangle([x0, y0, x0 + 440, y0 + 12 + 24 * len(lines)], fill=(0, 0, 0))
    for i, t in enumerate(lines):
        draw.text((x0 + 12, y0 + 6 + 24 * i), t, fill=(255, 255, 255), font=big if i == 0 else font)
    return im


def cmd_routes(args):
    image = Path(args.image)
    g = cost_grid(image, args.region, args.k, args.cell)
    crossings, _, cp = crossing_candidates(g)
    points = parse_points(args.points)
    r = plan_routes(g, crossings, points, args.pairs)
    out = Path(args.out) if args.out else image.parent
    out.mkdir(parents=True, exist_ok=True)
    stem = "world_map-%s" % args.region
    draw_routes_overlay(g["img"], g["cost"], g["cell"], crossings, r).save(out / ("%s-routes.png" % stem))
    doc = {"region": args.region, "image": str(image), "cell": g["cell"], "crossing": cp["crossing"],
           "bridge_cost": cp["bridge_cost"], **r["params"], "unreachable": r["unreachable"],
           "Nodes": r["nodes"], "Edges": r["edges"]}
    (out / ("%s-routes.json" % stem)).write_text(json.dumps(doc, indent=1), encoding="utf-8")
    for n in r["nodes"]:
        if n["Kind"] == "Point" and n["SnapPx"] > 0:
            print("%s snapped %.0f px to land" % (n["Id"], n["SnapPx"]))
    for e in r["edges"]:
        print("%-12s %-12s length %7.1f px  cost %7.1f  crossings %s  passes %s" % (
            e["A"], e["B"], e["Length"], e["Cost"], ",".join(e["Crossings"]) or "-", ",".join(e["Passes"]) or "-"))
    kinds = [n["Kind"] for n in r["nodes"]]
    print("%d routes, %.0f px total; %d %s used of %d candidates; %d passes" % (
        len(r["edges"]), sum(e["Length"] for e in r["edges"]), len(r["used"]), cp["crossing"], len(crossings),
        kinds.count("Pass")))
    if r["unreachable"]:
        print("unreachable: %s" % ", ".join("%s-%s" % tuple(u) for u in r["unreachable"]))
    print("wrote %s" % (out / ("%s-routes.png" % stem)))
    return 0


# ---------------------------------------------------------------- spots

COPSE_AREA = (600, 12000)      # px area range of a forest clump that reads as a copse
OUTCROP_AREA = (400, 9000)     # px area range of a rock clump that reads as an outcrop
CLUMP_LAND = 0.6               # share of a clump's surrounding ring that must be land (not an island)
LOCATION_SPACING = 280         # px between suggested locations
LOCATION_MAX = 12
EDGE_MARGIN = 48               # px: road ends sit in this band at the border; locations keep twice it clear
WATER_NEAR = (12, 80)          # px from water that earns a location the water bonus
CLUMP_NEAR = 80                # px from a copse or outcrop that earns the clump bonus
WATER_BONUS = 0.35
CLUMP_BONUS = 0.2
SCORE_SIGMA = 24.0             # px: cost smoothing before scoring
ROADSIGN_SPACING = 500         # px between road ends
ROADSIGN_MAX = 4
WAYSTONE_SPACING = 360         # px of road between waystones
HUNT_OFF = (40, 200)           # px a hunt spot sits off the nearest road
HUNT_SPACING = 160             # px between hunt spots
SPOT_COLOURS = {"Location": (255, 255, 255), "RoadEnd": (90, 200, 255), "Waystone": (180, 140, 255),
                "Hunt": (255, 150, 40), "Pass": (240, 80, 60), "Copse": (40, 200, 90), "Outcrop": (210, 120, 70)}


def spot_params(entry):
    keys = {"copse_area": COPSE_AREA, "outcrop_area": OUTCROP_AREA, "clump_land": CLUMP_LAND,
            "location_spacing": LOCATION_SPACING, "location_max": LOCATION_MAX, "edge_margin": EDGE_MARGIN,
            "water_near": WATER_NEAR, "clump_near": CLUMP_NEAR, "water_bonus": WATER_BONUS,
            "clump_bonus": CLUMP_BONUS, "score_sigma": SCORE_SIGMA, "roadsign_spacing": ROADSIGN_SPACING,
            "roadsign_max": ROADSIGN_MAX, "waystone_spacing": WAYSTONE_SPACING, "hunt_off": HUNT_OFF,
            "hunt_spacing": HUNT_SPACING}
    return {k: entry.get(k, v) for k, v in keys.items()}


def clumps(g, label, area, land_share):
    """Clumps of one class at cell resolution (cells at least half that class), area within `area` px, standing on
    land: at least land_share of the one-cell ring around them is passable. Returns [(kind-less dict)], plus a mask."""
    from scipy import ndimage
    cost, cell = g["cost"], g["cell"]
    h, w = cost.shape
    if label not in g["names"]:
        return [], np.zeros(cost.shape, bool)
    share = block_reduce((g["index"] == g["names"].index(label)).astype(np.float64), cell, np.mean)[:h, :w]
    lab, n = ndimage.label((share >= 0.5) & np.isfinite(cost), structure=np.ones((3, 3)))
    out, mask = [], np.zeros(cost.shape, bool)
    for i, sl in enumerate(ndimage.find_objects(lab), 1):
        ys, xs = sl[0].start, sl[1].start
        win = (slice(max(0, ys - 1), sl[0].stop + 1), slice(max(0, xs - 1), sl[1].stop + 1))
        mine = lab[win] == i
        px_area = int(mine.sum()) * cell * cell
        if not area[0] <= px_area <= area[1]:
            continue
        ring = ndimage.binary_dilation(mine) & ~mine
        if not ring.any() or np.isfinite(cost[win][ring]).mean() < land_share:
            continue
        my, mx = np.nonzero(mine)
        cy, cx = my.mean(), mx.mean()
        k = int(np.argmin((my - cy) ** 2 + (mx - cx) ** 2))  # the member cell nearest the centroid
        out.append({"X": round((mx[k] + win[1].start + 0.5) * cell, 1), "Y": round((my[k] + win[0].start + 0.5) * cell, 1),
                    "AreaPx": px_area})
        mask[win] |= mine
    out.sort(key=lambda c: -c["AreaPx"])
    return out, mask


def greedy_pick(score, cell, spacing, limit, taken=()):
    """Highest-scoring cells, each at least `spacing` px from every pick and from `taken` (px points)."""
    s = score.astype(np.float64).copy()
    h, w = s.shape
    yy, xx = np.mgrid[0:h, 0:w]
    r = spacing / cell
    for x, y in taken:
        s[(yy - y / cell) ** 2 + (xx - x / cell) ** 2 < r * r] = -np.inf
    picks = []
    while len(picks) < limit:
        k = int(np.argmax(s))
        if not np.isfinite(s.flat[k]):
            break
        cy, cx = divmod(k, w)
        picks.append(((cx + 0.5) * cell, (cy + 0.5) * cell, float(s.flat[k])))
        s[(yy - cy) ** 2 + (xx - cx) ** 2 < r * r] = -np.inf
    return picks


def along(poly, spacing, offset):
    """Points every `spacing` px along a polyline, the first at `offset` px."""
    seg = np.hypot(*np.diff(poly, axis=0).T)
    cum = np.concatenate([[0], np.cumsum(seg)])
    out = []
    for s in np.arange(offset, cum[-1], spacing):
        i = min(int(np.searchsorted(cum, s, side="right")) - 1, len(seg) - 1)
        t = (s - cum[i]) / seg[i] if seg[i] > 0 else 0
        out.append(poly[i] + t * (poly[i + 1] - poly[i]))
    return out


def suggest_spots(g, crossings):
    from scipy import ndimage
    entry, cost, cell = g["entry"], g["cost"], g["cell"]
    h, w = cost.shape
    p = spot_params(entry)
    land = np.isfinite(cost)
    copses, copse_mask = clumps(g, "forest", p["copse_area"], p["clump_land"])
    outcrops, outcrop_mask = clumps(g, "rock", p["outcrop_area"], p["clump_land"])

    # score: cheap smoothed ground, a bonus near (not on) water and near a copse or outcrop
    fill = np.where(land, cost, np.nanmax(np.where(land, cost, np.nan)))
    smooth = ndimage.gaussian_filter(fill.astype(np.float64), p["score_sigma"] / cell)
    lo, hi = np.percentile(smooth[land], [5, 95])
    base = 1 - np.clip((smooth - lo) / max(hi - lo, 1e-6), 0, 1)
    water_d = ndimage.distance_transform_edt(land) * cell
    near_water = (water_d >= p["water_near"][0]) & (water_d <= p["water_near"][1])
    clump_d = ndimage.distance_transform_edt(~(copse_mask | outcrop_mask)) * cell
    score = base + p["water_bonus"] * near_water + p["clump_bonus"] * (clump_d <= p["clump_near"])
    yy, xx = np.mgrid[0:h, 0:w]
    border = np.minimum(np.minimum(yy + 0.5, h - yy - 0.5), np.minimum(xx + 0.5, w - xx - 0.5)) * cell
    score = np.where(land & (water_d >= p["water_near"][0]) & (border >= 2 * p["edge_margin"]), score, -np.inf)
    locs = greedy_pick(score, cell, p["location_spacing"], p["location_max"])

    # road ends: the cheapest land in the falloff band (half to one edge_margin in), kept off the corners; the best per
    # side first so they spread round the map, then the next best anywhere up to roadsign_max
    m = p["edge_margin"]
    sides = [(yy + 0.5) * cell, (h - yy - 0.5) * cell, (xx + 0.5) * cell, (w - xx - 0.5) * cell]  # N, S, W, E
    band = land & (water_d >= 2 * cell) & (border >= m / 2) & (border <= m)
    band &= np.sort(np.stack(sides), axis=0)[1] >= 3 * m  # second-nearest edge far away: not a corner
    end_score = np.where(band, -smooth, -np.inf)
    ends = []
    for d in sides:
        ends += greedy_pick(np.where(d <= m, end_score, -np.inf), cell, p["roadsign_spacing"], 1,
                            [(x, y) for x, y, _ in ends])
    ends = sorted(ends, key=lambda e: -e[2])[:p["roadsign_max"]]
    ends += greedy_pick(end_score, cell, p["roadsign_spacing"], p["roadsign_max"] - len(ends),
                        [(x, y) for x, y, _ in ends])

    points = [("loc-%d" % (i + 1), x, y) for i, (x, y, _) in enumerate(locs)]
    leaves = ["end-%d" % (i + 1) for i in range(len(ends))]
    points += [(n, x, y) for n, (x, y, _) in zip(leaves, ends)]
    r = plan_routes(g, crossings, points, leaves=leaves)

    # waystones along the road network and at each road end; shared stretches don't double up
    sp = p["waystone_spacing"]
    stops = [(n["X"], n["Y"]) for n in r["nodes"] if n["Kind"] == "Point" and n["Id"].startswith("loc-")]
    stones = []
    for n in r["nodes"]:
        if n["Kind"] == "Point" and n["Id"] in leaves and any(n["Id"] in (e["A"], e["B"]) for e in r["edges"]):
            stones.append((n["X"], n["Y"], n["Id"]))
    for e in r["edges"]:
        for q in along(np.array(e["Spline"]), sp, sp / 2):
            if all(np.hypot(q[0] - x, q[1] - y) >= sp / 2 for x, y, _ in stones) and \
                    all(np.hypot(q[0] - x, q[1] - y) >= sp / 3 for x, y in stops):
                stones.append((float(q[0]), float(q[1]), None))

    # hunt spots: copse / outcrop centroids a short walk off a road
    road = np.zeros((h * cell, w * cell), np.uint8)
    road_im = Image.fromarray(road)
    rd = ImageDraw.Draw(road_im)
    for e in r["edges"]:
        rd.line([tuple(q) for q in e["Spline"]], fill=1, width=3)
    road_d = ndimage.distance_transform_edt(np.asarray(road_im) == 0) if r["edges"] else None
    hunts = []
    for kind, group in (("Copse", copses), ("Outcrop", outcrops)):
        for c in group:
            if road_d is None:
                break
            off = float(road_d[min(int(c["Y"]), h * cell - 1), min(int(c["X"]), w * cell - 1)])
            if not p["hunt_off"][0] <= off <= p["hunt_off"][1]:
                continue
            if any(np.hypot(c["X"] - q["X"], c["Y"] - q["Y"]) < p["hunt_spacing"] for q in hunts):
                continue
            hunts.append({"Id": "hunt-%d" % (len(hunts) + 1), "Kind": "Hunt", "On": kind, "X": c["X"], "Y": c["Y"],
                          "AreaPx": c["AreaPx"], "OffRoadPx": round(off, 1)})

    spots = []
    for n in r["nodes"]:
        if n["Kind"] == "Point":
            kind = "RoadEnd" if n["Id"] in leaves else "Location"
            d = {"Id": n["Id"], "Kind": kind, "X": n["X"], "Y": n["Y"]}
            if kind == "Location":
                k = int(n["Id"].split("-")[1]) - 1
                d["Score"] = round(locs[k][2], 3)
            spots.append(d)
        elif n["Kind"] == "Pass":
            spots.append({"Id": n["Id"], "Kind": "Pass", "X": n["X"], "Y": n["Y"], "RunPx": n["RunPx"]})
    for i, (x, y, at) in enumerate(stones):
        d = {"Id": "waystone-%d" % (i + 1), "Kind": "Waystone", "X": round(x, 1), "Y": round(y, 1)}
        if at:
            d["At"] = at
        spots.append(d)
    spots.extend(hunts)
    return {"params": p, "spots": spots, "routes": r, "copses": copses, "outcrops": outcrops}


def draw_spots_overlay(img, cost, cell, s):
    r = s["routes"]
    art = np.asarray(img, dtype=np.float64)
    h, w = art.shape[:2]
    out = art * 0.75
    blocked = np.repeat(np.repeat(np.isinf(cost), cell, 0), cell, 1)[:h, :w]
    out[blocked] = out[blocked] * 0.5 + np.array([20, 60, 160]) * 0.5
    im = Image.fromarray(out.clip(0, 255).astype(np.uint8))
    draw = ImageDraw.Draw(im)
    try:
        font = ImageFont.truetype("arial.ttf", 14)
        big = ImageFont.truetype("arial.ttf", 18)
    except OSError:
        font = big = ImageFont.load_default()
    m = s["params"]["edge_margin"]
    draw.rectangle([m, m, w - m, h - m], outline=(90, 200, 255), width=1)
    draw.rectangle([2 * m, 2 * m, w - 2 * m, h - 2 * m], outline=(255, 255, 255), width=1)
    for kind, group in (("Copse", s["copses"]), ("Outcrop", s["outcrops"])):
        for c in group:
            rad = max(3, (c["AreaPx"] / np.pi) ** 0.5)
            draw.ellipse([c["X"] - rad, c["Y"] - rad, c["X"] + rad, c["Y"] + rad], outline=SPOT_COLOURS[kind], width=1)
    for e in r["edges"]:
        line = [tuple(q) for q in e["Spline"]]
        draw.line(line, fill=(0, 0, 0), width=7, joint="curve")
        draw.line(line, fill=(255, 245, 200), width=3, joint="curve")
    for n in r["nodes"]:
        if n["Kind"] in ("Bridge", "Causeway"):
            colour = CROSSING_COLOURS.get(n["Kind"].lower(), (255, 255, 255))
            draw.line([tuple(n["A"]), tuple(n["B"])], fill=(0, 0, 0), width=9)
            draw.line([tuple(n["A"]), tuple(n["B"])], fill=colour, width=5)
    for q in s["spots"]:
        x, y, col, kind = q["X"], q["Y"], SPOT_COLOURS[q["Kind"]], q["Kind"]
        if kind == "Location":
            draw.ellipse([x - 10, y - 10, x + 10, y + 10], fill=col, outline=(0, 0, 0), width=3)
            draw.text((x + 13, y - 10), q["Id"], fill=col, font=big, stroke_width=3, stroke_fill=(0, 0, 0))
        elif kind == "RoadEnd":
            draw.rectangle([x - 8, y - 8, x + 8, y + 8], fill=col, outline=(0, 0, 0), width=2)
            draw.text((x + 11, y - 8), q["Id"], fill=col, font=font, stroke_width=2, stroke_fill=(0, 0, 0))
        elif kind == "Pass":
            draw.polygon([(x, y - 10), (x - 9, y + 7), (x + 9, y + 7)], fill=col, outline=(0, 0, 0))
        elif kind == "Waystone":
            draw.polygon([(x, y - 8), (x + 6, y), (x, y + 8), (x - 6, y)], fill=col, outline=(0, 0, 0))
        elif kind == "Hunt":
            draw.ellipse([x - 7, y - 7, x + 7, y + 7], fill=None, outline=(0, 0, 0), width=4)
            draw.ellipse([x - 7, y - 7, x + 7, y + 7], fill=None, outline=col, width=2)
            draw.line([(x - 4, y), (x + 4, y)], fill=col, width=2)
            draw.line([(x, y - 4), (x, y + 4)], fill=col, width=2)
    counts = {}
    for q in s["spots"]:
        counts[q["Kind"]] = counts.get(q["Kind"], 0) + 1
    lines = ["%d routes, %.0f px of road" % (len(r["edges"]), sum(e["Length"] for e in r["edges"])),
             ", ".join("%d %s" % (counts.get(k, 0), k.lower()) for k in ("Location", "RoadEnd", "Waystone", "Pass", "Hunt")),
             "%d copses, %d outcrops (rings); white box = location margin" % (len(s["copses"]), len(s["outcrops"]))]
    if r["unreachable"]:
        lines.append("unreachable: " + ", ".join("%s-%s" % tuple(u) for u in r["unreachable"]))
    x0, y0 = 16, 16
    draw.rectangle([x0, y0, x0 + 470, y0 + 12 + 24 * len(lines)], fill=(0, 0, 0))
    for i, t in enumerate(lines):
        draw.text((x0 + 12, y0 + 6 + 24 * i), t, fill=(255, 255, 255), font=big if i == 0 else font)
    return im


def cmd_spots(args):
    image = Path(args.image)
    g = cost_grid(image, args.region, args.k, args.cell)
    crossings, _, cp = crossing_candidates(g)
    s = suggest_spots(g, crossings)
    r = s["routes"]
    out = Path(args.out) if args.out else image.parent
    out.mkdir(parents=True, exist_ok=True)
    stem = "world_map-%s" % args.region
    draw_spots_overlay(g["img"], g["cost"], g["cell"], s).save(out / ("%s-spots.png" % stem))
    p = {k: list(v) if isinstance(v, tuple) else v for k, v in s["params"].items()}
    doc = {"region": args.region, "image": str(image), "cell": g["cell"], "crossing": cp["crossing"], **p,
           **r["params"], "unreachable": r["unreachable"], "Spots": s["spots"],
           "Crossings": [n for n in r["nodes"] if n["Kind"] in ("Bridge", "Causeway")], "Edges": r["edges"],
           "Copses": s["copses"], "Outcrops": s["outcrops"]}
    (out / ("%s-spots.json" % stem)).write_text(json.dumps(doc, indent=1), encoding="utf-8")
    counts = {}
    for q in s["spots"]:
        counts[q["Kind"]] = counts.get(q["Kind"], 0) + 1
    print("%d copses, %d outcrops; %d routes, %.0f px of road" % (
        len(s["copses"]), len(s["outcrops"]), len(r["edges"]), sum(e["Length"] for e in r["edges"])))
    print(", ".join("%d %s" % (v, k) for k, v in counts.items()))
    if r["unreachable"]:
        print("unreachable: %s" % ", ".join("%s-%s" % tuple(u) for u in r["unreachable"]))
    print("wrote %s" % (out / ("%s-spots.png" % stem)))
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
    c = sub.add_parser("crossings", help="bridge/causeway candidates over the cost grid's water, with a debug overlay")
    c.add_argument("image")
    c.add_argument("--region", required=True, help="region id in world_map_palettes.json")
    c.add_argument("--k", type=int, help="cluster count (default: the palette entry's k)")
    c.add_argument("--cell", type=int, help="working-grid cell in px (default: the palette entry's cell, else %d)" % CELL)
    c.add_argument("--out", help="output directory (default: the image's directory)")
    c.set_defaults(func=cmd_crossings)
    c = sub.add_parser("routes", help="least-cost routes between points over land and crossings, with passes")
    c.add_argument("image")
    c.add_argument("--region", required=True, help="region id in world_map_palettes.json")
    c.add_argument("--points", nargs="+", required=True, help="name=x,y ... (px), or one JSON file of points")
    c.add_argument("--pairs", help="a-b,c-d: route these pairs instead of the minimum spanning tree")
    c.add_argument("--k", type=int, help="cluster count (default: the palette entry's k)")
    c.add_argument("--cell", type=int, help="working-grid cell in px (default: the palette entry's cell, else %d)" % CELL)
    c.add_argument("--out", help="output directory (default: the image's directory)")
    c.set_defaults(func=cmd_routes)
    c = sub.add_parser("spots", help="suggest locations, road ends, waystones, passes and hunt spots, with routes")
    c.add_argument("image")
    c.add_argument("--region", required=True, help="region id in world_map_palettes.json")
    c.add_argument("--k", type=int, help="cluster count (default: the palette entry's k)")
    c.add_argument("--cell", type=int, help="working-grid cell in px (default: the palette entry's cell, else %d)" % CELL)
    c.add_argument("--out", help="output directory (default: the image's directory)")
    c.set_defaults(func=cmd_spots)
    args = p.parse_args(argv)
    return args.func(args)


if __name__ == "__main__":
    sys.exit(main())
