"""Static release content lint (GG-T1119): finds player-visible text and content that must not ship, in the style of
tools/polish_lint.py (a table by default, --json for machines). Report only, never a gate: tools/nightly.ps1 lists it.

    python tools/release_lint.py            # table
    python tools/release_lint.py --json     # machine-readable

Checks, over Assets/GridGame/Resources/Content/*.json (+ Content/Text/*.json) and the App UI string literals
(App/Automation is skipped, as polish_lint does):
  lorem            lorem ipsum text
  placeholder      TODO / TBD / FIXME / placeholder / dummy / test text in a player-visible string
  test-stage       a stage marked IsTest (or named test-*) that a campaign region, quest, bounty or encounter reaches
  missing-name     an empty Name / Title / Description / Card / Term / Definition
  placeholder-art  a class portrait, item icon or ability icon whose file is missing (the game draws the mannequin,
                   the pouch emblem or the purple placeholder square instead)

Story text tracked by GG-T497 (the crawls and conversations that are lorem until the story lands) is reported in its
own "known: GG-T497" bucket and is not a failure. The exit code is always 0.
"""
import glob
import json
import os
import re
import sys
from collections import Counter, defaultdict

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import asset_names  # noqa: E402

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RES = os.path.join(ROOT, "Assets", "GridGame", "Resources")
CONTENT = os.path.join(RES, "Content")
APP = os.path.join(ROOT, "Assets", "GridGame", "App")
SKIP_DIRS = {os.path.join(APP, "Automation")}  # dev-only tooling, never shipped

LOREM = re.compile(r"\blorem ipsum\b|\bdolor sit amet\b|\bconsectetur adipiscing\b", re.I)
PLACEHOLDER = re.compile(r"\b(?:TODO|TBD|FIXME|placeholder|lorem|test (?:text|string|name|stage|dialogue))\b"
                         r"|\bXXX\b|\basdf\b|\bfoo bar\b", re.I)
NAME_KEYS = {"Name", "Title", "Description", "Card", "Term", "Definition"}
SKIP_KEYS = {"$schema", "Notes", "Comment", "_comment", "hash", "source", "context"}
# Story files GG-T497 will replace: a lorem hit here is known, not a failure.
STORY_FILES = {"crawls.json", "conversations.json", "dialogues.json", "quests.json", "campaign.json"}


def load(path):
    with open(path, encoding="utf-8-sig") as f:
        return json.load(f)


def rel(path):
    return os.path.relpath(path, ROOT).replace("\\", "/")


def strings(node, where=""):
    """Every (path, string) in a JSON tree, skipping bookkeeping keys."""
    if isinstance(node, str):
        yield where, node
    elif isinstance(node, dict):
        for k, v in node.items():
            if k in SKIP_KEYS:
                continue
            yield from strings(v, f"{where}.{k}" if where else k)
    elif isinstance(node, list):
        for i, v in enumerate(node):
            yield from strings(v, f"{where}[{i}]")


def rows(data):
    for i, r in enumerate(data if isinstance(data, list) else []):
        if isinstance(r, dict):
            yield str(r.get("Id") or r.get("Key") or r.get("Class") or i), r


def scan_text(findings, known, path, data):
    base = os.path.basename(path)
    texts = data.get("texts") if isinstance(data, dict) else None
    for where, s in strings(data):
        kind = "lorem" if LOREM.search(s) else "placeholder" if PLACEHOLDER.search(s) else None
        if not kind:
            continue
        owner = where.split(".")[1] if texts is not None and where.startswith("texts.") else where.split(".")[0]
        item = (kind, rel(path), owner, s[:70])
        (known if base in STORY_FILES and kind == "lorem" else findings).append(item)


def scan_names(findings, path, data):
    for label, r in rows(data):
        for k in NAME_KEYS:
            if k in r and (r[k] is None or (isinstance(r[k], str) and not r[k].strip())):
                findings.append(("missing-name", rel(path), f"{label}.{k}", "empty"))
    for key, t in ((data.get("texts") or {}).items() if isinstance(data, dict) else []):
        p = t.get("payload") or {}
        for k in ("short", "definition", "term", "title"):
            if k in p and not str(p[k]).strip():
                findings.append(("missing-name", rel(path), f"{key}.{k}", "empty"))


def scan_test_stages(findings, contents):
    stages = contents.get("stages.json") or []
    test_ids = {s["Id"] for s in stages if s.get("IsTest") or str(s.get("Id", "")).startswith("test")}
    reach = defaultdict(set)
    for name in ("campaign.json", "quests.json", "bounties.json", "encounters.json", "dialogues.json", "regionrules.json"):
        for where, s in strings(contents.get(name)):
            if s in test_ids:
                reach[s].add(f"{name}:{where}")
    for sid, where in sorted(reach.items()):
        findings.append(("test-stage", "Assets/GridGame/Resources/Content/stages.json", sid, "reached from " + sorted(where)[0]))


def exists(folder, f):
    return any(os.path.exists(os.path.join(RES, folder, f + ext)) for ext in (".png", ".jpg"))


def scan_art(findings, contents):
    for c in contents.get("classes.json") or []:
        if c.get("CutInto") or c["Id"] == "mannequin":
            continue
        f = asset_names.file(c.get("Portrait") or asset_names.stem(c["Id"]), c.get("PortraitVariant", 0))
        if not exists("Portraits", f):
            findings.append(("placeholder-art", "classes.json", c["Id"], f"Portraits/{f} missing (mannequin)"))
    for it in contents.get("items.json") or []:
        f = asset_names.file(asset_names.stem(it["Id"]), 0)
        ic = it.get("Icon")
        if not exists("ItemIcons", f) and not (ic and exists("Icons", asset_names.file(asset_names.stem(ic), 0))):
            findings.append(("placeholder-art", "items.json", it["Id"], f"ItemIcons/{f} missing (pouch emblem)"))
    for a in contents.get("abilities.json") or []:
        f = asset_names.file(asset_names.stem(a["Id"]), 0)
        ic = a.get("Icon")
        if not exists("AbilityIcons", f) and not (ic and (exists("Icons", asset_names.file(asset_names.stem(ic), 0)) or exists("Icons", ic))):
            findings.append(("placeholder-art", "abilities.json", a["Id"], f"AbilityIcons/{f} missing"))


STRING = re.compile(r'"((?:[^"\\\n]|\\.)*)"')
APP_WORDS = re.compile(r"\b(?:TODO|TBD|FIXME|lorem)\b|\btest (?:text|string)\b", re.I)


def scan_app(findings):
    """String literals in App/**/*.cs that are lorem or placeholder text (block and whole-line comments skipped)."""
    for d, _, fs in os.walk(APP):
        if any(d.startswith(s) for s in SKIP_DIRS):
            continue
        for fn in fs:
            if not fn.endswith(".cs"):
                continue
            p = os.path.join(d, fn)
            with open(p, encoding="utf-8-sig", errors="replace") as fh:
                text = fh.read()
            text = re.sub(r"/\*.*?\*/", lambda m: "\n" * m.group(0).count("\n"), text, flags=re.S)
            for n, line in enumerate(text.split("\n"), 1):
                if line.lstrip().startswith("//"):
                    continue
                for m in STRING.finditer(line):
                    s = m.group(1)
                    if LOREM.search(s) or APP_WORDS.search(s):
                        findings.append(("lorem" if LOREM.search(s) else "placeholder", rel(p), f"line {n}", s[:70]))


def run():
    findings, known, contents = [], [], {}
    for p in sorted(glob.glob(os.path.join(CONTENT, "*.json"))):
        data = load(p)
        contents[os.path.basename(p)] = data
        scan_text(findings, known, p, data)
        scan_names(findings, p, data)
    for p in sorted(glob.glob(os.path.join(CONTENT, "Text", "*.json"))):
        data = load(p)
        scan_text(findings, known, p, data)
        scan_names(findings, p, data)
    scan_test_stages(findings, contents)
    scan_art(findings, contents)
    scan_app(findings)
    return findings, known


def main():
    findings, known = run()
    cols = ("check", "file", "where", "text")
    if "--json" in sys.argv:
        print(json.dumps({"failures": [dict(zip(cols, f)) for f in findings],
                          "known_GG_T497": [dict(zip(cols, f)) for f in known]}, indent=2))
        return
    print("Release content lint (GG-T1119)\n")
    print(f"{'check':<16}{'findings':>9}   files")
    by = defaultdict(list)
    for f in findings:
        by[f[0]].append(f)
    for chk in ("lorem", "placeholder", "test-stage", "missing-name", "placeholder-art"):
        fl = by.get(chk, [])
        print(f"{chk:<16}{len(fl):>9}   " + ", ".join(f"{k} ({v})" for k, v in Counter(f[1] for f in fl).most_common(4)))
    print(f"{'known: GG-T497':<16}{len(known):>9}   " + ", ".join(f"{k} ({v})" for k, v in Counter(f[1] for f in known).most_common(4)))
    print()
    for f in findings[:60]:
        print(f"  {f[0]:<15} {f[1]}  {f[2]}  {f[3]}")
    if len(findings) > 60:
        print(f"  ... {len(findings) - 60} more (--json lists all)")


if __name__ == "__main__":
    main()
