"""
Generate male counterparts of every hero class's female portrait in Midjourney, download every candidate into one
folder and build a review gallery (GG-T535, GG-T541, umbrella GG-T532). Fire and forget: rerun it any time, finished
classes are skipped and jobs submitted by an interrupted run are downloaded, not resubmitted.

    python tools/male_variants.py                  # every Team:Hero class
    python tools/male_variants.py warden cleric    # just these
    python tools/male_variants.py --gallery        # only rebuild the gallery
    python tools/male_variants.py --redo cleric    # a fresh set for a class that already has one

The Midjourney side (MindAttic.Browser over CDP, the web app's own API, resume, relax fallback, reconnects) lives in
the AutoMidjourneyNav repo (package automjnav, a sibling of this repo or pip-installed). This file is only the job
list: each class gets one Edit job per prompt in PROMPTS, the portrait attached as the edit image, 4 images each.

Output (git-ignored), all candidates in one folder:
    _male_variants/<classId>_<set><n>.png   e.g. cleric_a1.png .. cleric_a4.png, cleric_b1.png .., cleric_c1.png ..
    _male_variants/sources/<classId>.png    the image that was uploaded
    _male_variants/index.html               gallery: the female portrait beside its candidates
    _male_variants/state.json, run.log
"""
import argparse
import json
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
try:
    import automjnav
except ImportError:
    sys.path.insert(0, os.path.join(os.path.dirname(ROOT), "AutoMidjourneyNav"))
    import automjnav

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import asset_names  # noqa: E402  the one asset naming rule (GG-T557)

RES = os.path.join(ROOT, "Assets", "Gaiden", "Resources")
OUT = os.path.join(ROOT, "_male_variants")

# a: the owner's prompt, verbatim (the Warden runs of 2026-11-21). b: a stronger push for a male build, since a: keeps
# the original silhouette very faithfully (skirts and all).
PROMPTS = {
    "a": "masculine male version of this exact character --ar 2:3",
    "b": "masculine male version of this exact character, a man with a broad-shouldered masculine build and a "
         "handsome male face, the same outfit, colours and weapon tailored for a man --ar 2:3",
    # c: the owner's direction after seeing a/b - short hair, muscular, no breasts or hips, a classic brave hero.
    "c": "male version of this exact character as a classic heroic man: short masculine haircut, strong square jaw, "
         "broad shoulders, muscular arms and chest, flat male chest, narrow hips, brave strong confident stance, "
         "the same outfit, colours and weapon tailored for a muscular man --no breasts, cleavage, wide hips, "
         "long hair, feminine --ar 2:3",
}


def heroes():
    with open(os.path.join(RES, "Content", "classes.json"), encoding="utf-8-sig") as f:
        classes = json.load(f)
    seen, out = set(), []
    for c in classes:
        if c.get("Team") != "Hero":
            continue
        p = portrait_path(c)
        if p in seen:  # monstertamer draws wildchild's portrait
            continue
        seen.add(p)
        out.append(c)
    return out


def portrait_path(c):
    return os.path.join(RES, "Portraits", asset_names.portrait_file(c) + ".png")


def source_image(c):
    """The portrait cut to its figure on a 2:3 off-white canvas (what gets uploaded)."""
    os.makedirs(os.path.join(OUT, "sources"), exist_ok=True)
    return automjnav.fit_to_aspect(portrait_path(c), os.path.join(OUT, "sources", c["Id"] + ".png"))


def build_gallery(classes):
    return automjnav.build_gallery(OUT, "Male hero variants", [
        {"title": c["Name"], "subtitle": f'{c["Id"]} · {c.get("Role") or ""}', "reference": portrait_path(c),
         "reference_caption": "female (in game)",
         "sets": [(f'{v}: {p.split(" --")[0]}', f'{c["Id"]}_{v}') for v, p in PROMPTS.items()]} for c in classes],
        note="Every candidate is a file in this folder named <class>_<set><n>.png; click one for full size.")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("ids", nargs="*", help="class ids (default: every hero class)")
    ap.add_argument("--gallery", action="store_true", help="only rebuild the gallery")
    ap.add_argument("--redo", action="store_true", help="generate fresh sets for the given classes")
    args = ap.parse_args()
    classes = heroes()
    if args.ids:
        unknown = set(args.ids) - {c["Id"] for c in classes}
        if unknown:
            sys.exit(f"not hero classes: {', '.join(sorted(unknown))}")
        classes = [c for c in classes if c["Id"] in args.ids]
    if args.gallery:
        print(build_gallery(heroes()))
        return
    batch = automjnav.Batch(OUT, on_downloaded=lambda _key: build_gallery(heroes()))
    jobs = [automjnav.Job(f'{c["Id"]}_{v}', prompt, edit=source_image(c), label=c["Name"])
            for c in classes for v, prompt in PROMPTS.items()]
    if args.redo:
        batch.redo([j.key for j in jobs])
    batch.run(jobs)
    print(build_gallery(heroes()))


if __name__ == "__main__":
    main()
