"""Breed masks and preview sheets (GG-T1798, docs/research/sprite-recolor.md section 4).

    python tools/breed_masks.py masks   frog-00 bat-00 ...   # writes Portraits/<name>-mask.png (+ .meta, sRGB off)
    python tools/breed_masks.py preview frog-00 bat-00 ... --out DIR   # one sheet per creature + a combined sheet

The mask is the art's size: R body, G secondary, B keep-as-is (eyes, teeth, claws, gems); B wins overlaps; a pixel
with no channel set is left untouched (outlines' white sticker rim, for one). The preview runs the same math as
Resources/Shaders/BreedRecolor.shader (Gamma colour space: the project's m_ActiveColorSpace is 0) with the ramps from
Content/breeds.json, so what the sheet shows is what BreedArt bakes on the board.

Rules (deterministic, no model):
  1. k-means (k=6) on the opaque pixels in OKLab, chroma weighted up so hue separates before lightness.
  2. Clusters near the body's hue (the largest chromatic cluster) are body; the largest remaining cluster that differs
     enough (hue >= 35 deg, or a pale low-chroma belly/membrane) is secondary, with any clusters like it.
  3. Keep: small connected blobs (< 1.5% of the silhouette) that are very saturated, very bright or off the body's
     hue: eyes, teeth, tongues, gems. Low-chroma near-white pixels on the silhouette rim stay untouched (sticker edge).
  4. Keep dilated 2 px, every channel feathered (gaussian 1.2 px) so no halo where a recoloured body meets an eye.
"""
import argparse, json, os, re, sys, uuid

import numpy as np
from PIL import Image
from scipy import ndimage
from scipy.cluster.vq import kmeans2

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PORTRAITS = os.path.join(ROOT, "Assets", "GridGame", "Resources", "Portraits")
BREEDS = os.path.join(ROOT, "Assets", "GridGame", "Resources", "Content", "breeds.json")


# ---- colour ------------------------------------------------------------------------------------------------------

def srgb_to_linear(c):
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def linear_to_srgb(c):
    c = np.clip(c, 0, 1)
    return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(c, 1 / 2.4) - 0.055)


def linear_to_oklab(c):
    l = 0.4122214708 * c[..., 0] + 0.5363325363 * c[..., 1] + 0.0514459929 * c[..., 2]
    m = 0.2119034982 * c[..., 0] + 0.6806995451 * c[..., 1] + 0.1073969566 * c[..., 2]
    s = 0.0883024619 * c[..., 0] + 0.2817188376 * c[..., 1] + 0.6299787005 * c[..., 2]
    l, m, s = (np.cbrt(np.maximum(x, 0)) for x in (l, m, s))
    return np.stack([0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s,
                     1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s,
                     0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s], -1)


def oklab_to_linear(c):
    l = c[..., 0] + 0.3963377774 * c[..., 1] + 0.2158037573 * c[..., 2]
    m = c[..., 0] - 0.1055613458 * c[..., 1] - 0.0638541728 * c[..., 2]
    s = c[..., 0] - 0.0894841775 * c[..., 1] - 1.2914855480 * c[..., 2]
    l, m, s = l ** 3, m ** 3, s ** 3
    return np.clip(np.stack([4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
                             -1.2684380046 * l + 2.6097574011 * m - 0.3413510813 * s,
                             -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s], -1), 0, 1)


def hex_rgb(h, default=(1.0, 1.0, 1.0)):
    if not h:
        return np.array(default)
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)])


def hue_deg(lab):
    return np.degrees(np.arctan2(lab[..., 2], lab[..., 1])) % 360


def hue_gap(a, b):
    d = abs(a - b) % 360
    return min(d, 360 - d)


# ---- masks -------------------------------------------------------------------------------------------------------

def art_path(name):
    return os.path.join(PORTRAITS, name if name.endswith(".png") else name + ".png")


def load(name):
    im = np.asarray(Image.open(art_path(name)).convert("RGBA"), dtype=np.float64) / 255
    return im[..., :3], im[..., 3]


def make_mask(rgb, alpha, seed=7):
    h, w = alpha.shape
    solid = alpha > 0.5
    lab = linear_to_oklab(srgb_to_linear(rgb))
    chroma = np.hypot(lab[..., 1], lab[..., 2])
    hue = hue_deg(lab)
    n_solid = int(solid.sum())

    # rim: within 10 px of transparency and pale/greyish -> the sticker outline, untouched
    dist = ndimage.distance_transform_edt(solid)
    rim = solid & (dist < 10) & (lab[..., 0] > 0.72) & (chroma < 0.06)

    # 1. cluster
    feats = np.stack([lab[..., 0], lab[..., 1] * 2.5, lab[..., 2] * 2.5], -1)
    idx = np.flatnonzero((solid & ~rim).ravel())
    rng = np.random.default_rng(seed)
    sample = feats.reshape(-1, 3)[rng.choice(idx, min(len(idx), 40000), replace=False)]
    np.random.seed(seed)
    cents, _ = kmeans2(sample, 6, minit="++", seed=seed)
    flat = feats.reshape(-1, 3)
    labels = np.full(h * w, -1)
    d = ((flat[idx, None, :] - cents[None, :, :]) ** 2).sum(-1)
    labels[idx] = d.argmin(1)
    labels = labels.reshape(h, w)

    stats = []
    for k in range(len(cents)):
        sel = labels == k
        n = int(sel.sum())
        if n == 0:
            stats.append(None)
            continue
        a, b = lab[..., 1][sel].mean(), lab[..., 2][sel].mean()
        stats.append(dict(k=k, n=n, L=lab[..., 0][sel].mean(), C=float(np.hypot(a, b)),
                          H=float(np.degrees(np.arctan2(b, a)) % 360)))
    live = [s for s in stats if s]
    # 2. body = the largest cluster with some colour (dark outline clusters have little chroma and go with body anyway)
    chromatic = [s for s in live if s["C"] > 0.025] or live
    primary = max(chromatic, key=lambda s: s["n"])
    body_k, sec_k = set(), set()
    for s in live:
        if s["C"] <= 0.025 and s["L"] < 0.45:
            body_k.add(s["k"])           # dark, greyish: shading and line work, follows the body ramp
        elif s["C"] > 0.025 and hue_gap(s["H"], primary["H"]) < 35:
            body_k.add(s["k"])
    rest = [s for s in live if s["k"] not in body_k and s["n"] > 0.04 * n_solid]
    if rest:
        sec = max(rest, key=lambda s: s["n"])
        for s in rest:
            pale = s["C"] < 0.05 and sec["C"] < 0.05
            if s is sec or pale or (s["C"] > 0.025 and hue_gap(s["H"], sec["H"]) < 30):
                sec_k.add(s["k"])
    # small leftover clusters: body unless the keep rule claims their pixels
    for s in live:
        if s["k"] not in body_k and s["k"] not in sec_k:
            body_k.add(s["k"])

    body = np.isin(labels, list(body_k)) & solid & ~rim
    sec = np.isin(labels, list(sec_k)) & solid & ~rim

    # 3. keep: small, striking blobs
    # Smooth the body/secondary split (a majority filter), so a furred coat doesn't come out speckled.
    sec = ndimage.median_filter(sec.astype(np.uint8), size=9).astype(bool) & solid & ~rim
    body = solid & ~rim & ~sec
    L = lab[..., 0]
    local = ndimage.uniform_filter(np.where(solid, L, 0), 25) / np.maximum(ndimage.uniform_filter(solid * 1.0, 25), 1e-3)
    off_hue = np.abs(((hue - primary["H"] + 180) % 360) - 180) > 50
    striking = solid & ~rim & (
        (chroma > max(0.12, primary["C"] * 1.8)) |
        ((chroma > 0.09) & off_hue & (chroma > primary["C"] * 1.3)) |
        ((L - local > 0.22) & (L > 0.75))            # teeth, eye glints: bright against a dark surround
    )
    striking = ndimage.binary_opening(striking, iterations=2)
    comp, n = ndimage.label(striking)
    keep = np.zeros_like(solid)
    if n:
        sizes = ndimage.sum(striking, comp, index=np.arange(1, n + 1))
        ok = []
        for i, sl in enumerate(ndimage.find_objects(comp)):
            if sl is None or not (60 < sizes[i] < 0.015 * n_solid):
                continue
            hh, ww = sl[0].stop - sl[0].start, sl[1].stop - sl[1].start
            # compact blobs only: an outline fragment is long and thin
            if sizes[i] / (hh * ww) > 0.35 and max(hh, ww) / max(1, min(hh, ww)) < 3.5:
                ok.append(i + 1)
        keep = np.isin(comp, ok)
    keep = ndimage.binary_dilation(keep, iterations=2) & solid

    mask = np.zeros((h, w, 3))
    mask[..., 0] = body
    mask[..., 1] = sec
    mask[..., 2] = keep
    for c in range(3):
        mask[..., c] = ndimage.gaussian_filter(mask[..., c], 1.2)
    mask *= (alpha > 0.02)[..., None]
    info = dict(primary_hue=round(primary["H"]), clusters=[{k: (round(v, 3) if isinstance(v, float) else v)
                                                            for k, v in s.items()} for s in live],
                body=sorted(int(k) for k in body_k), secondary=sorted(int(k) for k in sec_k),
                keep_px=int(keep.sum()))
    return np.clip(mask, 0, 1), info


def write_meta(mask_png, art_png):
    meta = open(art_png + ".meta", encoding="utf-8").read()
    meta = re.sub(r"^guid: \w+", "guid: " + uuid.uuid4().hex, meta, count=1, flags=re.M)
    meta = re.sub(r"sRGBTexture: \d", "sRGBTexture: 0", meta)
    meta = re.sub(r"enableMipMap: \d", "enableMipMap: 0", meta)
    meta = re.sub(r"textureCompression: \d", "textureCompression: 0", meta)
    open(mask_png + ".meta", "w", encoding="utf-8", newline="\n").write(meta)


def cmd_masks(names):
    for name in names:
        rgb, alpha = load(name)
        mask, info = make_mask(rgb, alpha)
        out = art_path(name)[:-4] + "-mask.png"
        Image.fromarray((mask * 255 + 0.5).astype(np.uint8), "RGB").save(out)
        if not os.path.exists(out + ".meta"):
            write_meta(out, art_path(name))
        print(f"{name}: body {info['body']} secondary {info['secondary']} keep {info['keep_px']} px -> {os.path.relpath(out, ROOT)}")


# ---- the shader, in numpy ----------------------------------------------------------------------------------------

def ramp(stops, t):
    """BreedArt.Ramp: stops evenly spaced, lerped in sRGB (Color.Lerp on the hex values), 64 texels, bilinear."""
    cols = np.array([hex_rgb(s) for s in stops])
    if len(cols) == 1:
        cols = np.vstack([cols, cols])
    px = np.array([cols[min(len(cols) - 2, int(f))] * (1 - (f - min(len(cols) - 2, int(f)))) +
                   cols[min(len(cols) - 2, int(f)) + 1] * (f - min(len(cols) - 2, int(f)))
                   for f in np.linspace(0, len(cols) - 1, 64)])
    x = np.clip(t, 0, 1) * 63
    i0 = np.floor(x).astype(int)
    i1 = np.minimum(i0 + 1, 63)
    f = (x - i0)[..., None]
    return px[i0] * (1 - f) + px[i1] * f


def recolor(rgb, alpha, mask, breed):
    if not breed.get("BodyRamp"):
        return rgb, alpha
    keep = mask[..., 2]
    body = mask[..., 0] * (1 - keep)
    sec = mask[..., 1] * (1 - keep)
    lin = srgb_to_linear(rgb)                       # Gamma project: ToLinear on the sample
    lab = linear_to_oklab(lin)
    lift = breed.get("LightLift", 0.0)
    t = lab[..., 0] + (1 - lab[..., 0]) * lift if lift >= 0 else lab[..., 0] * (1 + lift)
    t = np.clip(t, 0, 1)
    chroma_scale = breed.get("ChromaScale", 1.0)

    def ramped(stops):
        s = linear_to_oklab(srgb_to_linear(ramp(stops, t)))
        s[..., 1:] *= chroma_scale
        return oklab_to_linear(s)

    out = lin
    out = out + (ramped(breed["BodyRamp"]) - out) * body[..., None]
    out = out + (ramped(breed.get("SecondaryRamp") or breed["BodyRamp"]) - out) * sec[..., None]
    tint = linear_to_oklab(srgb_to_linear(hex_rgb(breed.get("KeepTint"))))
    kept = oklab_to_linear(np.stack([lab[..., 0] * 0.5 + tint[0] * 0.5,
                                     np.full_like(t, tint[1]), np.full_like(t, tint[2])], -1))
    out = out + (kept - out) * (keep * breed.get("KeepMix", 0.0))[..., None]
    a = alpha * (1 + (breed.get("Alpha", 1.0) - 1) * np.clip(body + sec, 0, 1))
    return linear_to_srgb(out), a


# ---- preview -----------------------------------------------------------------------------------------------------

def checker(h, w, cell=16):
    yy, xx = np.mgrid[0:h, 0:w]
    v = np.where(((yy // cell) + (xx // cell)) % 2 == 0, 0.16, 0.20)
    return np.stack([v * 0.95, v * 0.97, v * 1.1], -1)


def tile(rgb, alpha, size):
    bg = checker(*alpha.shape)
    img = rgb * alpha[..., None] + bg * (1 - alpha[..., None])
    return Image.fromarray((np.clip(img, 0, 1) * 255 + 0.5).astype(np.uint8)).resize((size, size), Image.LANCZOS)


def label(im, text):
    from PIL import ImageDraw, ImageFont
    d = ImageDraw.Draw(im)
    try:
        font = ImageFont.truetype("segoeui.ttf", 22)
    except OSError:
        font = ImageFont.load_default()
    d.rectangle([0, 0, im.width, 30], fill=(14, 14, 20))
    d.text((10, 2), text, fill=(235, 230, 220), font=font)
    return im


def cmd_preview(names, out_dir, size):
    breeds = json.load(open(BREEDS, encoding="utf-8"))
    os.makedirs(out_dir, exist_ok=True)
    rows = []
    for name in names:
        rgb, alpha = load(name)
        mpath = art_path(name)[:-4] + "-mask.png"
        if os.path.exists(mpath):
            mask = np.asarray(Image.open(mpath).convert("RGB"), dtype=np.float64) / 255
        else:
            mask, _ = make_mask(rgb, alpha)
        tiles = [label(tile(rgb, alpha, size), f"{name}  normal")]
        mview = mask * 0.85 + rgb * 0.15
        tiles.append(label(tile(mview, alpha, size), "mask  R body  G 2nd  B keep"))
        for b in breeds:
            if not b.get("BodyRamp"):
                continue
            r, a = recolor(rgb, alpha, mask, b)
            tiles.append(label(tile(r, a, size), f"{b['Id']}  (weight {b['Weight']})"))
        row = Image.new("RGB", (size * len(tiles), size + 0), (14, 14, 20))
        for i, t in enumerate(tiles):
            row.paste(t, (i * size, 0))
        row.save(os.path.join(out_dir, f"{name}-breeds.png"))
        rows.append(row)
        print("preview", name)
    if len(rows) > 1:
        sheet = Image.new("RGB", (rows[0].width, sum(r.height for r in rows)), (14, 14, 20))
        y = 0
        for r in rows:
            sheet.paste(r, (0, y))
            y += r.height
        sheet.save(os.path.join(out_dir, "breeds-sheet.png"))


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)
    m = sub.add_parser("masks")
    m.add_argument("names", nargs="+")
    p = sub.add_parser("preview")
    p.add_argument("names", nargs="+")
    p.add_argument("--out", required=True)
    p.add_argument("--size", type=int, default=384)
    a = ap.parse_args()
    if a.cmd == "masks":
        cmd_masks(a.names)
    else:
        cmd_preview(a.names, a.out, a.size)


if __name__ == "__main__":
    sys.exit(main())
