"""Bakes every portrait's thumbnail (GG-T719, part of GG-T713).

Owner 2026-11-23: "need to use thumbnails for every usage of an actor except where their full portrait is explicitly
used like in discussions; seeing a tiny version of a 1024x1024 character looks bad". A 1024 portrait shrunk to a 64 px
tile with no mipmaps aliases, so every small use draws a purpose-made 256 px image instead.

For each Assets/GridGame/Resources/Portraits/<name>.png this writes Assets/GridGame/Resources/Thumbs/<name>.png (and a
hand-written .meta) with the class's existing head framing baked in. The crop is exactly Ui.PortraitThumb's window: a
square of side W/Zoom centred at (X*W, Y*H) (Y measured from the top), grown by MARGIN (10%) of that side on every side
(GG-T1210: room for the handheld wobble), padded transparent where it runs off the canvas, downscaled premultiplied
(RGBa) with Lanczos to 320x320.

Which framing a file gets: the class whose PortraitFile() it is (classes.json, first in file order when two classes
share a file); male art uses MaleThumbnail, else Thumbnail. A file no class draws (aetherist-00, aetherist-male-00, the
Camp NPC's own art) gets the default head crop measured from the class medians: side = 0.42 x the alpha-bbox height,
centre x = the bbox centre, centre y = bbox top + 0.464 x side.

tools/thumbs_manifest.json records a hash of each source portrait plus the framing used; `--check` exits 1 listing
every thumb that is missing, stale (portrait or framing changed) or orphaned (its portrait is gone).

    python tools/make_thumbs.py            # (re)bake stale/missing thumbs
    python tools/make_thumbs.py --all      # rebake every thumb
    python tools/make_thumbs.py --check    # verify only
"""
import argparse
import hashlib
import json
import math
import re
import sys
import uuid
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
import asset_names  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
RES = ROOT / "Assets" / "GridGame" / "Resources"
PORTRAITS = RES / "Portraits"
THUMBS = RES / "Thumbs"
CLASSES = RES / "Content" / "classes.json"
MANIFEST = Path(__file__).resolve().parent / "thumbs_manifest.json"
TEMPLATE_META = PORTRAITS / "acolyte-female-00.png.meta"

# GG-T1210: the baked square is the owner's framing plus MARGIN of its side on every side (side x 1.2), so the
# visible (framed) window inside it is SIZE / 1.2 = ~267 px, still above the old 256. Ui.PortraitThumb draws the thumb
# at (1 + 2 MARGIN) x the host, offset -MARGIN, so at rest exactly the framing shows, and its handheld wobble drifts
# the art inside the margin. Keep MARGIN equal to Ui.ThumbMargin.
SIZE = 320
MARGIN = 0.1
# Bump when the crop/resample rule or the .meta template changes, so every thumb counts as stale.
VERSION = 2
# Default head crop for portraits no class frames (measured from the medians of the class framings).
DEFAULT_SIDE_PER_BBOX_H = 0.42
DEFAULT_CENTRE_Y_PER_SIDE = 0.464


def framings(classes):
    """{portrait file: (framing dict, owner label)}; first class in file order wins a shared file."""
    out = {}
    for c in classes:
        th = c.get("Thumbnail")
        f = asset_names.portrait_file(c)
        if th and f not in out:
            out[f] = (th, c["Id"])
        if c.get("MalePortrait"):
            mf = asset_names.portrait_file(c, male=True)
            mth = c.get("MaleThumbnail") or th
            if mth and mf not in out:
                out[mf] = (mth, c["Id"] + " (male)")
    return out


def default_framing(im):
    """Head crop for a portrait with no class framing, from its alpha bounds, as a Thumbnail {Zoom, X, Y}."""
    w, h = im.size
    bbox = im.getchannel("A").point(lambda a: 255 if a > 8 else 0).getbbox() or (0, 0, w, h)
    left, top, right, bottom = bbox
    side = DEFAULT_SIDE_PER_BBOX_H * (bottom - top)
    cx = (left + right) / 2
    cy = top + DEFAULT_CENTRE_Y_PER_SIDE * side
    return {"Zoom": round(w / side, 4), "X": round(cx / w, 4), "Y": round(cy / h, 4)}


def bake(im, th):
    """The Ui.PortraitThumb window of `im` plus MARGIN on every side, at SIZE x SIZE, transparent off the canvas."""
    w, h = im.size
    side = w / float(th["Zoom"]) * (1 + 2 * MARGIN)
    cx, cy = float(th["X"]) * w, float(th["Y"]) * h
    l, t = cx - side / 2, cy - side / 2
    r, b = l + side, t + side
    # Pad so the (float) box lies inside the canvas; padding is transparent black, which premultiplied is exact.
    pad = int(math.ceil(max(0, -l, -t, r - w, b - h))) + 2
    src = im.convert("RGBA").convert("RGBa")
    if pad:
        canvas = Image.new("RGBa", (w + 2 * pad, h + 2 * pad), (0, 0, 0, 0))
        canvas.paste(src, (pad, pad))
        src = canvas
    box = (l + pad, t + pad, r + pad, b + pad)
    return src.resize((SIZE, SIZE), Image.Resampling.LANCZOS, box=box).convert("RGBA")


def source_hash(path, th):
    h = hashlib.sha256()
    h.update(path.read_bytes())
    h.update(json.dumps({"v": VERSION, "size": SIZE, "margin": MARGIN, "framing": th}, sort_keys=True).encode())
    return h.hexdigest()[:20]


def guid_for(name):
    """Stable fresh GUID: an existing .meta keeps its own, a new one is derived from the thumb's path."""
    meta = THUMBS / (name + ".png.meta")
    if meta.exists():
        m = re.search(r"^guid: ([0-9a-f]{32})$", meta.read_text(encoding="utf-8"), re.M)
        if m:
            return m.group(1)
    return uuid.uuid5(uuid.NAMESPACE_URL, "gridgame:Resources/Thumbs/" + name).hex


def meta_text(guid):
    t = TEMPLATE_META.read_text(encoding="utf-8")
    t = re.sub(r"^guid: [0-9a-f]{32}$", "guid: " + guid, t, count=1, flags=re.M)
    t = t.replace("    enableMipMap: 0", "    enableMipMap: 1", 1)
    t = t.replace("    filterMode: 1", "    filterMode: 2", 1)
    t = re.sub(r"maxTextureSize: \d+", "maxTextureSize: 512", t)  # importer and every platform (SIZE 320 fits, no downscale)
    assert "enableMipMap: 1" in t and "filterMode: 2" in t, "template meta changed shape"
    return t


FOLDER_META = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def plan():
    classes = json.loads(CLASSES.read_text(encoding="utf-8"))
    fr = framings(classes)
    jobs = []
    for p in sorted(PORTRAITS.glob("*.png")):
        name = p.stem
        if name in fr:
            th, owner = fr[name]
            th = {"Zoom": th.get("Zoom", 1.0), "X": th.get("X", 0.5), "Y": th.get("Y", 0.5)}
        else:
            with Image.open(p) as im:
                th = default_framing(im.convert("RGBA"))
            owner = "default"
        jobs.append((name, p, th, owner))
    return jobs


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--check", action="store_true", help="exit 1 listing missing/stale/orphaned thumbs; write nothing")
    ap.add_argument("--all", action="store_true", help="rebake every thumb, not only stale ones")
    args = ap.parse_args()

    manifest = json.loads(MANIFEST.read_text(encoding="utf-8")) if MANIFEST.exists() else {}
    jobs = plan()
    names = {j[0] for j in jobs}
    problems, baked = [], 0
    new_manifest = {}
    for name, path, th, owner in jobs:
        hsh = source_hash(path, th)
        png = THUMBS / (name + ".png")
        entry = manifest.get(name)
        fresh = png.exists() and (THUMBS / (name + ".png.meta")).exists() and entry and entry.get("hash") == hsh
        if args.check:
            if not fresh:
                problems.append(f"{'missing' if not png.exists() else 'stale'}: {name} ({owner})")
            continue
        if args.all or not fresh:
            THUMBS.mkdir(parents=True, exist_ok=True)
            with Image.open(path) as im:
                bake(im, th).save(png, optimize=True)
            meta = THUMBS / (name + ".png.meta")
            guid = guid_for(name)
            meta.write_text(meta_text(guid), encoding="utf-8", newline="\n")
            baked += 1
        new_manifest[name] = {"hash": hsh, "owner": owner, "framing": th}

    orphans = sorted(p.stem for p in THUMBS.glob("*.png") if p.stem not in names) if THUMBS.exists() else []
    if args.check:
        problems += [f"orphaned: {o} (no portrait)" for o in orphans]
        if not (RES / "Thumbs.meta").exists():
            problems.append("missing: Resources/Thumbs.meta")
        if problems:
            print(f"{len(problems)} thumb problem(s); run python tools/make_thumbs.py")
            print("\n".join("  " + p for p in problems))
            return 1
        print(f"ok: {len(jobs)} thumbs up to date")
        return 0

    for o in orphans:
        (THUMBS / (o + ".png")).unlink()
        m = THUMBS / (o + ".png.meta")
        if m.exists():
            m.unlink()
    folder_meta = RES / "Thumbs.meta"
    if not folder_meta.exists():
        folder_meta.write_text(FOLDER_META.format(guid=uuid.uuid5(uuid.NAMESPACE_URL, "gridgame:Resources/Thumbs").hex),
                               encoding="utf-8", newline="\n")
    MANIFEST.write_text(json.dumps(new_manifest, indent=1, sort_keys=True) + "\n", encoding="utf-8", newline="\n")
    total = sum(p.stat().st_size for p in THUMBS.glob("*.png"))
    print(f"baked {baked}, removed {len(orphans)}, {len(jobs)} thumbs, {total / 1e6:.1f} MB")
    return 0


if __name__ == "__main__":
    sys.exit(main())
