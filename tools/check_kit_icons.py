"""Fails if two abilities in one hero kit share the same icon (same silhouette and palette in tools/icons.json), so
no kit shows two identical buttons (docs/design/ux-review/REVIEW.md, GG-T1955). Collisions that predate the rule are
listed in tools/kit_icon_baseline.json; fix one and drop its line there. New collisions fail.
    python tools/check_kit_icons.py [--write-baseline]"""
import json, os, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONTENT = os.path.join(ROOT, "Assets", "GridGame", "Resources", "Content")
BASELINE = os.path.join(ROOT, "tools", "kit_icon_baseline.json")


def collisions():
    icons = json.load(open(os.path.join(ROOT, "tools", "icons.json"), encoding="utf-8"))["ability"]
    out = set()
    for c in json.load(open(os.path.join(CONTENT, "classes.json"), encoding="utf-8-sig")):
        if c.get("Team") != "Hero":
            continue
        by_icon = {}
        for a in c.get("Abilities") or []:
            spec = icons.get(a)
            if spec:
                by_icon.setdefault((spec[0], spec[1]), []).append(a)
        out |= {"+".join(sorted(v)) + " = " + k[0] + " " + k[1] for k, v in by_icon.items() if len(v) > 1}
    return sorted(out)


found = collisions()
if "--write-baseline" in sys.argv:
    json.dump(found, open(BASELINE, "w", encoding="utf-8", newline="\n"), indent=1)
    print(f"baseline written ({len(found)} collisions)")
    sys.exit(0)
known = set(json.load(open(BASELINE, encoding="utf-8")))
new = [f for f in found if f not in known]
if new:
    for f in new:
        print("SHARED ICON IN ONE KIT:", f)
    sys.exit(1)
print(f"kit icon check OK ({len(found)} known collisions in the baseline, {len(known - set(found))} stale)")
