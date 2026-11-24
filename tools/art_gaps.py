"""Art gap report (GG-T752; was docs/ART_GAPS.md Appendix A). Read-only: it loads the content JSON and lists the art
folders under Assets/GridGame/Resources, then applies the lookups and fallbacks the game uses at runtime, with every
file name resolved through tools/asset_names.py (the hyphenated -00 layout, GG-T561):

  Portrait      Portraits/<ClassDef.PortraitFile>   (Portrait / MalePortrait / PortraitVariant)  else the mannequin
  Backdrop      Backgrounds/<set>-NN                (theme Backgrounds key, else theme id)        else base theme, greenvalley
  Board tile    Tiles/<theme>-00                    (theme Tiles key, else theme id)              else base theme, Tiles/tile-00
  Icons         AbilityIcons/ (else legacy Icons/<Icon>), ItemIcons/, StatusIcons/ (+ stunned), UI/ (pickups)

Not a build gate and runs no game players. Usage:

  python -I tools/art_gaps.py [REPO_ROOT] [--json OUT.json]

Prints a short text summary; the full report goes to --json (use '-' for stdout).
"""
import argparse
import collections
import glob
import hashlib
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import asset_names as an  # noqa: E402

IMAGE_EXT = (".png", ".jpg", ".jpeg")
MANNEQUIN = "mannequin"
DEFAULT_THEME = "greenvalley"
MAX_PER_SET = 32  # Backdrops.MaxPerTheme


def base_theme(theme):
    """Backdrops.BaseTheme: trailing digits dropped (greenvalley2 -> greenvalley)."""
    t = (theme or "").rstrip("0123456789")
    return t or theme


def strings(x):
    if isinstance(x, str):
        yield x
    elif isinstance(x, dict):
        for v in x.values():
            yield from strings(v)
    elif isinstance(x, list):
        for v in x:
            yield from strings(v)


class Res:
    def __init__(self, root):
        self.res = os.path.join(root, "Assets", "GridGame", "Resources")
        self.content = os.path.join(self.res, "Content")
        self._files = {}

    def load(self, name):
        path = os.path.join(self.content, name + ".json")
        if not os.path.exists(path):
            return []
        with open(path, encoding="utf-8-sig") as f:
            return json.load(f)

    def files(self, folder):
        """{file name without extension: path} of the images in a Resources folder."""
        if folder not in self._files:
            d = os.path.join(self.res, folder)
            self._files[folder] = {os.path.splitext(f)[0]: os.path.join(d, f) for f in sorted(os.listdir(d))
                                   if f.lower().endswith(IMAGE_EXT)} if os.path.isdir(d) else {}
        return self._files[folder]

    def has(self, folder, name):
        return bool(name) and name in self.files(folder)


def md5(path):
    with open(path, "rb") as f:
        return hashlib.md5(f.read()).hexdigest()


def report(root):
    r = Res(root)
    classes_list = r.load("classes")
    classes = {c["Id"]: c for c in classes_list}
    camp = r.load("campaign") or {}
    stages = {s["Id"]: s for s in r.load("stages")}
    enc = {e["Id"]: e for e in r.load("encounters")}
    themes = {t["Id"]: t for t in camp.get("Themes", [])}

    # Campaign order: stage numbers 1..N, the main campaign ends at Ending.Stage.
    order, stage_theme = [], {}
    for t in camp.get("Themes", []):
        for sid in t.get("Stages", []):
            order.append(sid)
            stage_theme[sid] = t["Id"]
    ending = (camp.get("Ending") or {}).get("Stage")
    main_stages = order.index(ending) + 1 if ending in order else len(order)

    def stage_classes(s):
        found = collections.OrderedDict()
        for w in s.get("Waves", []):
            for o in [w] + ([enc[w["Group"]]] if w.get("Group") in enc else []):
                for e in o.get("Enemies", []):
                    found.setdefault(e["Class"], []).append("boss:" + e["RuleBreak"] if e.get("RuleBreak") else "foe")
                for k in ("Guests", "Recruitables", "SurpriseSpeaker"):
                    for v in strings(o.get(k)):
                        if v in classes:
                            found.setdefault(v, []).append(k)
        return found

    first, foe_stages = {}, collections.defaultdict(set)
    per_stage = {}
    for i, sid in enumerate(order, 1):
        if sid not in stages:
            continue
        per_stage[sid] = stage_classes(stages[sid])
        for cid, roles in per_stage[sid].items():
            first.setdefault(cid, i)
            if "foe" in roles:
                foe_stages[cid].add(i)

    refs = collections.defaultdict(set)
    for f in glob.glob(os.path.join(r.content, "*.json")):
        if os.path.basename(f) == "classes.json":
            continue
        with open(f, encoding="utf-8-sig") as fh:
            for v in strings(json.load(fh)):
                if v in classes:
                    refs[v].add(os.path.basename(f))

    # --- Portraits ----------------------------------------------------------------------------------------------
    portraits = r.files("Portraits")
    users = an.portrait_users(classes_list)
    mannequin_file = an.portrait_file(classes[MANNEQUIN]) if MANNEQUIN in classes else an.of(MANNEQUIN)
    by_hash = collections.defaultdict(list)
    for name, path in portraits.items():
        by_hash[md5(path)].append(name)
    twin_of = {}
    identical = []
    for names in by_hash.values():
        if len(names) > 1:
            names = sorted(names)
            identical.append({"files": names, "classes": sorted({cid for n in names for cid, _ in users.get(n, [])}),
                              "mannequin_copy": mannequin_file in names})
            for n in names:
                twin_of[n] = [m for m in names if m != n]
    mannequin_twins = set(twin_of.get(mannequin_file, []))

    def portrait_status(cid):
        """own | missing (mannequin fallback) | mannequin (content names it) | mannequin-copy | shared (another class draws the file) | identical."""
        c = classes.get(cid)
        if c is None:
            return "no-class"
        f = an.portrait_file(c)
        if f not in portraits:
            return "missing"
        if cid != MANNEQUIN and f == mannequin_file:
            return "mannequin"
        if cid != MANNEQUIN and f in mannequin_twins:
            return "mannequin-copy"
        if len({u for u, _ in users.get(f, [])}) > 1:
            return "shared"
        if f in twin_of:
            return "identical"
        return "own"

    on_mannequin = []
    for cid, c in classes.items():
        st = portrait_status(cid)
        if st in ("missing", "mannequin", "mannequin-copy"):
            on_mannequin.append({"class": cid, "file": an.portrait_file(c), "why": st,
                                 "first_stage": first.get(cid), "postgame": (first.get(cid) or 0) > main_stages,
                                 "referenced_in": sorted(refs[cid])})
    on_mannequin.sort(key=lambda x: (x["first_stage"] or 999, x["class"]))
    male_missing = sorted(cid for cid, c in classes.items()
                          if c.get("MalePortrait") and an.portrait_file(c, male=True) not in portraits)
    orphan_portraits = sorted(n for n in portraits if n not in users)

    # --- Bosses ------------------------------------------------------------------------------------------------
    def reused(cid, boss_stage=None):
        st = portrait_status(cid)
        plain = sorted(s for s in foe_stages.get(cid, ()) if s != boss_stage)
        return st, plain, st != "own" or bool(plain)

    finale = []
    for i, sid in enumerate(order, 1):
        for cid, roles in per_stage.get(sid, {}).items():
            for role in roles:
                if role.startswith("boss:"):
                    st, plain, bad = reused(cid, i)
                    finale.append({"stage": i, "stage_id": sid, "class": cid, "rule": role[5:], "portrait": st,
                                   "also_plain_foe_at": plain, "reused_art": bad})
    bounties = []
    for b in r.load("bounties"):
        cid = b.get("TargetClass")
        st, plain, bad = reused(cid)
        bounties.append({"bounty": b["Id"], "class": cid, "stage_id": b.get("Stage"), "portrait": st,
                         "also_plain_foe_at": plain, "reused_art": bad})

    # --- Backdrops and tiles ------------------------------------------------------------------------------------
    def set_of(theme):
        t = themes.get(theme) or {}
        return t.get("Backgrounds") or theme

    def set_count(s):
        stem_ = an.stem(s)
        n = 0
        while n < MAX_PER_SET and r.has("Backgrounds", an.file(stem_, n)):
            n += 1
        return n

    def tile_key(theme):
        return (themes.get(theme) or {}).get("Tiles") or theme

    all_themes = list(themes) + sorted({stages[s].get("Theme") for s in stages if stages[s].get("Theme")} - set(themes))
    backdrops, tiles = [], []
    for t in all_themes:
        own = set_count(set_of(t))
        shown = next(((c, set_of(c)) for c in (t, base_theme(t), DEFAULT_THEME) if set_count(set_of(c)) > 0), None)
        backdrops.append({"theme": t, "campaign_theme": t in themes, "set": set_of(t), "images": own,
                          "shows": shown[1] if shown else "tithe-moors",
                          "borrowed": own == 0})
        own_tile = an.of(tile_key(t))
        shown_tile = next((an.of(tile_key(c)) for c in (t, base_theme(t)) if r.has("Tiles", an.of(tile_key(c)))),
                          an.of("tile"))
        tiles.append({"theme": t, "campaign_theme": t in themes, "file": own_tile, "exists": r.has("Tiles", own_tile),
                      "shows": shown_tile, "borrowed": not r.has("Tiles", own_tile)})
    stage_mismatch = [{"stage_id": sid, "stage_theme": stages[sid].get("Theme"), "campaign_theme": stage_theme[sid]}
                      for sid in order if sid in stages and stages[sid].get("Theme") != stage_theme[sid]]
    missing_stages = [sid for sid in order if sid not in stages]

    # --- Icons -------------------------------------------------------------------------------------------------
    icons = {
        "ability": [a["Id"] for a in r.load("abilities")
                    if not r.has("AbilityIcons", an.of(a["Id"])) and not r.has("Icons", an.of(a.get("Icon")))],
        "item": [i["Id"] for i in r.load("items") if not r.has("ItemIcons", an.of(i["Id"]))],
        "status": [b["Id"] for b in r.load("buffs") if not r.has("StatusIcons", an.of(b["Id"]))]
        + ([] if r.has("StatusIcons", an.of("stunned")) else ["stunned"]),
        "pickup": [p["Id"] for p in r.load("pickups") if not r.has("UI", an.of(p.get("Icon") or p["Id"]))],
        "caravan": [c["Id"] for c in r.load("caravan") if c.get("Icon") and not r.has("ItemIcons", an.of(c["Icon"]))],
    }
    content_counts = {"ability": len(r.load("abilities")), "item": len(r.load("items")),
                      "status": len(r.load("buffs")) + 1, "pickup": len(r.load("pickups")),
                      "caravan": sum(1 for c in r.load("caravan") if c.get("Icon"))}

    return {
        "main_stages": main_stages,
        "total_stages": len(order),
        "on_disk": {f: len(r.files(f)) for f in
                    ("Portraits", "Backgrounds", "Tiles", "AbilityIcons", "ItemIcons", "StatusIcons", "Icons", "UI")},
        "classes": len(classes),
        "portraits_on_mannequin": on_mannequin,
        "portraits_identical": identical,
        "male_portraits_missing": male_missing,
        "portraits_unused": orphan_portraits,
        "finale_bosses": finale,
        "bounties": bounties,
        "backdrops": backdrops,
        "tiles": tiles,
        "stage_theme_mismatch": stage_mismatch,
        "stages_missing": missing_stages,
        "icons_missing": icons,
        "icons_checked": content_counts,
    }


def summary(rep):
    lines = []
    d = rep["on_disk"]
    lines.append(f"Stages: {rep['total_stages']} ({rep['main_stages']} main). On disk: " +
                 ", ".join(f"{k} {v}" for k, v in d.items()))
    man = rep["portraits_on_mannequin"]
    lines.append(f"Classes on the mannequin: {len(man)} of {rep['classes']} "
                 f"({sum(1 for m in man if m['why'] == 'missing')} missing, "
                 f"{sum(1 for m in man if m['why'] == 'mannequin')} name the mannequin, "
                 f"{sum(1 for m in man if m['why'] == 'mannequin-copy')} byte copies)")
    for m in man:
        where = f"stage {m['first_stage']}" if m["first_stage"] else ("unfielded; " + ",".join(m["referenced_in"]) or "unreferenced")
        lines.append(f"  {m['class']} -> {m['file']} [{m['why']}] {where}")
    ident = rep["portraits_identical"]
    lines.append(f"Byte-identical portrait groups: {len(ident)}")
    for g in ident:
        lines.append(f"  {' = '.join(g['files'])}" + ("  (mannequin copy)" if g["mannequin_copy"] else ""))
    if rep["male_portraits_missing"]:
        lines.append(f"Male portraits named but missing (female art shows): {', '.join(rep['male_portraits_missing'])}")
    fb = [b for b in rep["finale_bosses"] if b["reused_art"]]
    lines.append(f"Finale bosses on reused art: {len(fb)} of {len(rep['finale_bosses'])}")
    for b in fb:
        lines.append(f"  stage {b['stage']} {b['class']} ({b['rule']}): portrait {b['portrait']}"
                     + (f", plain foe at {b['also_plain_foe_at']}" if b["also_plain_foe_at"] else ""))
    bb = [b for b in rep["bounties"] if b["reused_art"]]
    lines.append(f"Bounty targets on reused art: {len(bb)} of {len(rep['bounties'])}")
    for b in bb:
        lines.append(f"  {b['bounty']} -> {b['class']}: portrait {b['portrait']}"
                     + (f", plain foe at {b['also_plain_foe_at']}" if b["also_plain_foe_at"] else ""))
    nb = [b for b in rep["backdrops"] if b["borrowed"]]
    lines.append(f"Themes with no backdrop set of their own: {len(nb)} of {len(rep['backdrops'])}")
    for b in nb:
        lines.append(f"  {b['theme']} (set '{b['set']}') shows {b['shows']}")
    nt = [t for t in rep["tiles"] if t["borrowed"]]
    lines.append(f"Themes borrowing a board tile: {len(nt)} of {len(rep['tiles'])}")
    for t in nt:
        lines.append(f"  {t['theme']} ({t['file']} missing) shows {t['shows']}")
    if rep["stage_theme_mismatch"]:
        lines.append(f"Stages whose Theme differs from their campaign theme: {len(rep['stage_theme_mismatch'])}")
    im = rep["icons_missing"]
    lines.append("Icons missing: " + ", ".join(f"{k} {len(v)}/{rep['icons_checked'][k]}" for k, v in im.items()))
    for k, v in im.items():
        if v:
            lines.append(f"  {k}: {', '.join(v[:20])}" + (f" ... (+{len(v) - 20})" if len(v) > 20 else ""))
    return "\n".join(lines)


def main(argv=None):
    ap = argparse.ArgumentParser(description="Read-only art gap report (GG-T752).")
    ap.add_argument("root", nargs="?", default=os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
                    help="GridGame.Unity checkout (default: this script's repo)")
    ap.add_argument("--json", help="write the full JSON report here ('-' for stdout)")
    a = ap.parse_args(argv)
    rep = report(a.root)
    if a.json == "-":
        print(json.dumps(rep, indent=1))
        return 0
    if a.json:
        with open(a.json, "w", encoding="utf-8") as f:
            json.dump(rep, f, indent=1)
    print(summary(rep))
    return 0


if __name__ == "__main__":
    sys.exit(main())
