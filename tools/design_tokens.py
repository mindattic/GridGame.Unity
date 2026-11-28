"""Design-system tokens page data (GG-T2047): reads the tokens where they live and writes
docs/design/design-system/tokens.js, so the page in docs/design/design-system never drifts from the game.

    python tools/design_tokens.py           # rewrite tokens.js
    python tools/design_tokens.py --check   # exit 1 when tokens.js is stale or Tokens.cs and Theme.uss disagree

Sources: Theme.uss :root (--color-*, --radius-*), App/UI/Tokens.cs (Space, Size, Timing, Radius), App/Boot/GameApp.cs
(the stage bounds) and tools/devices.json (one row per device shape, measured on its representative device).
"""
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
USS = os.path.join(ROOT, "Assets", "GridGame", "Resources", "UI", "Theme.uss")
TOKENS = os.path.join(ROOT, "Assets", "GridGame", "App", "UI", "Tokens.cs")
GAMEAPP = os.path.join(ROOT, "Assets", "GridGame", "App", "Boot", "GameApp.cs")
DEVICES = os.path.join(ROOT, "tools", "devices.json")
OUT = os.path.join(ROOT, "docs", "design", "design-system", "tokens.js")

THUMB_ZONE = 0.45  # POL-C4: primary actions sit in the bottom 45 % of the safe stage
TARGET_PX, FLOOR_PX = 132, 30  # Size.Target and TypeFloor.ShrinkMin, shown in pt/dp per shape


def read(path):
    with open(path, encoding="utf-8-sig") as f:
        return f.read()


def strip_comments(text):
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
    return re.sub(r"//[^\n]*", "", text)


def uss_root(uss):
    m = re.search(r":root\s*\{(.*?)\}", uss, re.S)
    return dict(re.findall(r"(--[\w-]+)\s*:\s*([^;]+);", m.group(1)))


def colors(root):
    out = []
    for name, value in root.items():
        m = re.match(r"rgba?\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*(?:,\s*([\d.]+)\s*)?\)", value.strip())
        if name.startswith("--color-") and m:
            r, g, b = (int(m.group(i)) for i in (1, 2, 3))
            out.append({"var": name, "hex": "#%02X%02X%02X" % (r, g, b), "alpha": float(m.group(4) or 1)})
    return out


def cs_class(cs, name):
    """The const floats of one static class in Tokens.cs, in source order, as [name, value] pairs."""
    m = re.search(r"\bclass\s+" + name + r"\b[^{]*\{", cs)
    depth, i = 1, m.end()
    while depth:
        depth += {"{": 1, "}": -1}.get(cs[i], 0)
        i += 1
    body = strip_comments(cs[m.end():i - 1])
    pairs = []
    for decl in re.findall(r"\bconst\s+(?:float|int)\s+([^;]+);", body):
        for name_, value in re.findall(r"(\w+)\s*=\s*(-?[\d.]+)f?\b", decl):
            pairs.append([name_, float(value)])
    return pairs


def radii(root, uss, cs):
    tokens = {int(re.search(r"\d+", v).group()): k for k, v in root.items() if k.startswith("--radius-")}
    code = {int(v): name for name, v in cs_class(cs, "Radius")}
    if set(code) != set(tokens):
        raise SystemExit("Tokens.cs Radius %s and Theme.uss --radius-* %s disagree" % (sorted(code), sorted(tokens)))
    body = re.sub(r":root\s*\{.*?\}", "", strip_comments(uss), count=1, flags=re.S)
    uses = {px: [] for px in tokens}
    for selector, block in re.findall(r"([^{}]+)\{([^{}]*)\}", body):
        for var in set(re.findall(r"var\((--radius-\w+)\)", block)):
            px = int(re.search(r"\d+", root[var]).group())
            uses[px].extend(s.strip() for s in selector.split(",") if s.strip() not in uses[px])
    return [{"token": tokens[px], "code": "Radius." + code[px], "px": px, "uses": uses[px]} for px in sorted(tokens)]


def shapes(devices, gameapp):
    const = dict((k, int(v)) for k, v in re.findall(r"\bconst\s+int\s+(\w+)\s*=\s*(\d+)\s*;", gameapp))
    w0, h0, w1, h1 = const["DesignWidth"], const["DesignHeight"], const["MaxStageWidth"], const["MaxStageHeight"]
    out = []
    for s in devices["shapes"]:
        d = devices["devices"][s["representative"]]
        pw, ph = d["px"]
        fit = min(pw / w0, ph / h0)
        sw, sh = round(pw / fit), round(ph / fit)
        k = ph / sh  # device px per design px on the whole-px stage
        framed = sw > w1 or sh > h1
        sw, sh = min(sw, w1), min(sh, h1)
        top, bottom = round(d["safe_px"]["top"] / k), round(d["safe_px"]["bottom"] / k)
        out.append({
            "id": s["id"], "rep": s["representative"], "label": s["label"], "inTarget": s["inTarget"],
            "reference": devices["reference"] in s["devices"],
            "px": [pw, ph], "pt": d["logical"], "scale": d["scale"], "aspect": s["aspect"], "stage": [sw, sh],
            "framed": framed, "k": round(k, 4), "safeTop": top, "safeBottom": bottom,
            "thumbTop": round(top + (1 - THUMB_ZONE) * (sh - top - bottom)),
            "targetPt": round(TARGET_PX * k / d["scale"], 1), "floorPt": round(FLOOR_PX * k / d["scale"], 1),
        })
    return out


def build():
    uss, cs = read(USS), read(TOKENS)
    root = uss_root(uss)
    devices = json.loads(read(DEVICES))
    data = {
        "colors": colors(root),
        "space": sorted({int(v) for _, v in cs_class(cs, "Space") if v}),
        "size": cs_class(cs, "Size"),
        "timing": cs_class(cs, "Timing"),
        "radii": radii(root, uss, cs),
        "shapes": shapes(devices, read(GAMEAPP)),
        "reference": devices["reference"],
        "devicesChecked": devices["checked"],
    }
    rows = ",\n".join("  %s: %s" % (k, json.dumps(v, ensure_ascii=False)) for k, v in data.items())
    return ("// Generated by tools/design_tokens.py from Theme.uss, Tokens.cs, GameApp.cs and devices.json. Do not edit:\n"
            "// change the tokens and run the script (--check fails when this file is stale).\n"
            "window.DS_TOKENS = {\n" + rows + "\n};\n")


def main():
    text = build()
    current = read(OUT) if os.path.exists(OUT) else None
    if "--check" in sys.argv:
        if current != text:
            print("%s is stale: run python tools/design_tokens.py" % os.path.relpath(OUT, ROOT))
            return 1
        return 0
    if current != text:
        with open(OUT, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
        print("wrote " + os.path.relpath(OUT, ROOT))
    return 0


if __name__ == "__main__":
    sys.exit(main())
