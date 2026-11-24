"""
The sky pieces of the staged backdrop (GG-T576): the one canonical Manalith, the shards that drift across the sky,
and the ordinary Moon.

    python tools/make_sky_assets.py                       # rebuild all three from their sources
    python tools/make_sky_assets.py --manalith art.png    # a new Manalith (the shards follow it)

Writes Resources/Sky/:
  manalith-00.png         the Manalith, the owner's official art (2026-11-21; the blue, mana-coloured version
                          2026-11-22, GG-T674) trimmed to its alpha bounds with a small margin; never repainted. The
                          source is kept at Art/Sky/manalith-source.png.
  manalith-shard-00..05   fragments cut from that same image (seeded irregular polygons inside its silhouette), so the
                          drifting shards are visibly pieces of the one Manalith.
  moon-00.png             the ordinary Moon: the full moon painted in Backgrounds bloom-02 (source in Art/Backgrounds),
                          cut on a circle with a soft rim. Fixed size in every sky (Content/sky.json).
"""
import argparse
import os
import random
import shutil
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "GridGame", "Resources", "Sky")
ART = os.path.join(ROOT, "Art")
MANALITH_SRC = os.path.join(ART, "Sky", "manalith-source.png")
MOON_SRC = "bloom-02"
MOON_GUESS = (0.48, 0.40, 0.2)  # centre x, y and search radius, as fractions of the backdrop's width
SHARDS = 6


def trim(im, margin=8):
    box = im.getchannel("A").point(lambda a: 255 if a > 2 else 0).getbbox()
    im = im.crop(box)
    out = Image.new("RGBA", (im.width + 2 * margin, im.height + 2 * margin), (0, 0, 0, 0))
    out.paste(im, (margin, margin))
    return out


def manalith(src):
    im = Image.open(src).convert("RGBA")
    a = np.array(im)
    a[a[..., 3] <= 2] = 0  # stray near-zero alpha only; colour and silhouette untouched
    return trim(Image.fromarray(a, "RGBA"))


def shards(som, n=SHARDS, seed=576):
    """Irregular fragments of the Manalith: a random convex-ish polygon placed where the art is solid."""
    rnd = random.Random(seed)
    a = np.array(som)[..., 3]
    ys, xs = np.nonzero(a > 250)
    out = []
    tries = 0
    while len(out) < n and tries < 500:
        tries += 1
        size = rnd.randint(int(som.width * 0.10), int(som.width * 0.20))
        i = rnd.randrange(len(xs))
        cx, cy = int(xs[i]), int(ys[i])
        k = rnd.randint(5, 7)
        angles = sorted(rnd.uniform(0, 2 * np.pi) for _ in range(k))
        pts = [(cx + np.cos(t) * size * rnd.uniform(0.35, 0.6), cy + np.sin(t) * size * rnd.uniform(0.35, 0.6)) for t in angles]
        mask = Image.new("L", som.size, 0)
        ImageDraw.Draw(mask).polygon(pts, fill=255)
        mask = mask.filter(ImageFilter.GaussianBlur(0.8))
        m = np.minimum(np.array(mask), a)
        if (m > 128).sum() < 0.97 * (np.array(mask) > 128).sum():
            continue  # keep fragments that lie wholly inside the silhouette
        frag = np.array(som).copy()
        frag[..., 3] = m
        out.append(trim(Image.fromarray(frag, "RGBA"), 2))
    return out


def moon():
    src = os.path.join(ART, "Backgrounds", MOON_SRC + ".jpg")
    if not os.path.exists(src):
        src = os.path.join(ROOT, "Assets", "GridGame", "Resources", "Backgrounds", MOON_SRC + ".jpg")
    im = Image.open(src).convert("RGB")
    w = im.width
    gx, gy, gr = (int(v * w) for v in MOON_GUESS)
    box = (gx - gr, gy - gr, gx + gr, gy + gr)
    crop = np.asarray(im.crop(box), np.float32)
    lum = crop.mean(2)
    bright = lum > (np.median(lum) + lum.max()) / 2
    bright = ndimage.binary_opening(bright, iterations=4)  # cut the bridges to a pale cliff
    lab, n = ndimage.label(bright)
    sizes = ndimage.sum(bright, lab, range(1, n + 1))
    bright = ndimage.binary_fill_holes(lab == 1 + int(np.argmax(sizes)))
    ys, xs = np.nonzero(bright)
    cx, cy = xs.mean(), ys.mean()
    r = np.sqrt(bright.sum() / np.pi)
    pad = r * 1.08
    disc = im.crop((int(box[0] + cx - pad), int(box[1] + cy - pad), int(box[0] + cx + pad), int(box[1] + cy + pad)))
    size = 256
    disc = np.asarray(disc.resize((size, size), Image.LANCZOS)).copy()
    yy, xx = np.mgrid[0:size, 0:size]
    d = np.hypot(xx - size / 2 + 0.5, yy - size / 2 + 0.5) / (size / 2 / 1.08)
    # A cliff corner overlapping the painted moon is filled from the moon's opposite side.
    l = disc.astype(np.float32).mean(2)
    inside = d < 1.0
    occluded = ndimage.binary_dilation(inside & (l < 0.8 * np.median(l[inside])), iterations=3) & inside
    disc[occluded] = disc[::-1, ::-1][occluded]
    disc = Image.fromarray(disc)
    alpha = np.clip((1.0 - d) / 0.03, 0, 1)  # a soft rim one pixel wide at the moon's own edge
    return Image.fromarray(np.dstack([np.asarray(disc), (alpha * 255).astype(np.uint8)]), "RGBA")


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--manalith", help="a new Manalith source image (transparent PNG)")
    args = ap.parse_args()
    os.makedirs(OUT, exist_ok=True)
    if args.manalith:
        os.makedirs(os.path.dirname(MANALITH_SRC), exist_ok=True)
        shutil.copyfile(args.manalith, MANALITH_SRC)
    som = manalith(MANALITH_SRC)
    som.save(os.path.join(OUT, "manalith-00.png"), optimize=True)
    print("manalith", som.size)
    for i, s in enumerate(shards(som)):
        s.save(os.path.join(OUT, f"manalith-shard-{i:02d}.png"), optimize=True)
        print("shard", i, s.size)
    m = moon()
    m.save(os.path.join(OUT, "moon-00.png"), optimize=True)
    print("moon", m.size)


if __name__ == "__main__":
    main()
