"""
GG-T1837: capture every screen for the screen-by-screen UI audit (docs/research/MobileGameAccessibility.md section 3).

    python tools/uiaudit.py [--device iphone17pro|pixel9|ipad|all] [--screens hub,battle,...] [--dry-run] [--build]

Walks the StartScreens ids (Assets/GridGame/App/Automation/StartScreens.cs, plus the results screens) with `goto`, then
the scenario-only spots (battle, boss, overworld, camp on the road, skill hex, summon, caravan) with the in-session
`scenario` endpoint, one player at a time at the device's size and safe area (tools/scenario.py). Per screen it writes
docs/design/ui-audit/captures/<device>/<screen>.png and an entry in that folder's facts.json: tap targets and text sizes
in pt/dp and mm (design px through the stage scale and the device's ppi), text contrast sampled from the screenshot,
safe-area intrusions and the whole polish measure. Refuses to start while any other GridGame player is running.
"""
import argparse, json, os, re, shutil, statistics, subprocess, sys, time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import harness
import scenario
from polish import contrast

ROOT = harness.ROOT
OUT = os.path.join(ROOT, "docs", "design", "ui-audit", "captures")
START_SCREENS = os.path.join(ROOT, "Assets", "GridGame", "App", "Automation", "StartScreens.cs")

# name: (ppi, device px per point, unit); sizes from MobileGameAccessibility.md section 1.
METRICS = {
    "iphone17pro": (460, 3.0, "pt"),
    "pixel9": (422, 2.625, "dp"),
    "ipad": (264, 2.0, "pt"),
}
AUDIT_DEVICES = ["iphone17pro", "pixel9", "ipad"]

# The screen the player is launched on, with a mid-game save that has every Camp station.
HOME_SPEC = {"target": "screen:hub", "allFeatures": True, "level": 20, "gold": 5000}

# Scenario-only spots: (capture name, tools/scenarios preset). These replace the bare goto of the same id.
SCENARIO_SCREENS = [
    ("battle", "camp-midgame"),
    ("battle-boss", "first-boss"),
    ("overworld", "overworld"),
    ("camp", "greenvalley-start"),
    ("skill-hex", "skill-hex"),
    ("summon", "summon"),
    ("caravan", "caravan"),
]
# The state "screen" each scenario capture must land on: Camp is the Hub screen, and until an overworld map screen exists
# the overworld preset lands there too.
LANDS_ON = {"battle": "battle", "battle-boss": "battle", "overworld": "hub", "camp": "hub", "skill-hex": "talents"}
# Screens that move or animate on their own get longer to settle.
SLOW = {"battle", "battle-boss", "overworld", "camp", "splash", "summon"}

MIN_TARGET, MIN_GAP, CAPTION_FLOOR = 132.0, 24.0, 30.0  # design px, AutomationServer.Polish.cs


def goto_screens():
    text = open(START_SCREENS, encoding="utf-8").read()
    block = re.search(r"All\s*=\s*\{(.*?)\};", text, re.S).group(1)
    ids = re.findall(r'"([^"]+)"', block) + ["results", "results-mid"]
    taken = {n for n, _ in SCENARIO_SCREENS}
    return [i for i in ids if i not in taken]


def plan(screens_filter):
    items = [(s, "goto", s) for s in goto_screens()] + [(n, "scenario", os.path.join(ROOT, "tools", "scenarios", p + ".json")) for n, p in SCENARIO_SCREENS]
    if screens_filter:
        want = set(screens_filter)
        unknown = want - {n for n, _, _ in items}
        if unknown:
            sys.exit("unknown screen(s): " + ", ".join(sorted(unknown)) + "\nknown: " + ", ".join(n for n, _, _ in items))
        items = [i for i in items if i[0] in want]
    return items


class Units:
    """Design px -> device px -> pt/dp and mm, read from the live stage bounds."""

    def __init__(self, device, sb):
        dev_w = scenario.DEVICES[device][0]
        self.ppi, self.per_pt, self.unit = METRICS[device]
        self.k = (sb["w"] / sb["design"][0]) * dev_w / sb["screen"][0]  # device px per design px

    def pt(self, v):
        return round(v * self.k / self.per_pt, 1)

    def mm(self, v):
        return round(v * self.k / self.ppi * 25.4, 1)

    def both(self, v):
        return {"px": round(v), self.unit: self.pt(v), "mm": self.mm(v)}


def sample_contrast(img, sb, t):
    """The background is the median colour of the text's rect in the screenshot (glyphs cover a minority of it); the
    lightest and darkest tenth give the worst case over a gradient or art. Text colour is composited over the median."""
    W, H = img.size
    s = sb["w"] / sb["design"][0]
    x0, y0 = max(0, int(sb["x"] + t["x"] * s)), max(0, int(sb["y"] + t["y"] * s))
    x1, y1 = min(W, int(sb["x"] + (t["x"] + t["w"]) * s)), min(H, int(sb["y"] + (t["y"] + t["h"]) * s))
    if x1 - x0 < 2 or y1 - y0 < 2:
        return None
    cell = img.crop((x0, y0, x1, y1)).resize((min(48, x1 - x0), min(16, y1 - y0)))
    px = list(cell.get_flattened_data() if hasattr(cell, "get_flattened_data") else cell.getdata())
    bg = [statistics.median(p[i] for p in px) / 255.0 for i in range(3)]
    r, g, b, a = t["color"]
    fg = [r * a + bg[0] * (1 - a), g * a + bg[1] * (1 - a), b * a + bg[2] * (1 - a)]
    by_lum = sorted(px, key=lambda p: 0.2126 * p[0] + 0.7152 * p[1] + 0.0722 * p[2])
    tenth = max(1, len(by_lum) // 10)
    dark = [statistics.median(p[i] for p in by_lum[:tenth]) / 255.0 for i in range(3)]
    light = [statistics.median(p[i] for p in by_lum[-tenth:]) / 255.0 for i in range(3)]
    ratio = contrast(fg, bg)
    worst = min(ratio, max(contrast(fg, dark), contrast(fg, light)))
    need = 3.0 if t["large"] else 4.5
    hexc = lambda c: "#" + "".join(f"{int(round(v * 255)):02x}" for v in c[:3])
    return {"fg": hexc(fg), "bg": hexc(bg), "ratio": round(ratio, 2), "worstStop": round(worst, 2), "need": need,
            "pass": ratio >= need}


def dims(detail):
    m = re.match(r"\s*(\d+)x(\d+)", detail or "")
    return (float(m.group(1)), float(m.group(2))) if m else None


def measure(device, name, method, png):
    from PIL import Image
    sb = harness.call("stagebounds")
    lay = harness.call("layout")
    safe = harness.call("safearea")
    state = harness.call("state")
    u = Units(device, sb)
    pol = lay.get("polish") or {}
    findings = pol.get("findings") or []
    counts = pol.get("counts") or {}

    img = Image.open(png).convert("RGB")
    texts = []
    for t in pol.get("texts") or []:
        texts.append({"text": t["t"], "rect": [t["x"], t["y"], t["w"], t["h"]], "size": u.both(t["size"]),
                      "large": t["large"], "contrast": sample_contrast(img, sb, t)})
    sizes = [t["size"]["px"] for t in texts]
    ratios = [t["contrast"] for t in texts if t["contrast"]]

    undersized = []
    for f in findings:
        if f["rule"] == "POL-C2" and dims(f["detail"]):
            w, h = dims(f["detail"])
            undersized.append({"target": f["subject"], "w": u.both(w), "h": u.both(h)})
    gaps = []
    for f in findings:
        m = re.match(r"\s*(\d+)px from", f["detail"] or "") if f["rule"] == "POL-C3" else None
        if m:
            gaps.append({"target": f["subject"], "gap": u.both(float(m.group(1)))})
    c2, c3 = counts.get("POL-C2") or {}, counts.get("POL-C3") or {}

    return {
        "screen": name,
        "method": method,
        "landedOn": state.get("screen"),
        "png": os.path.relpath(os.path.join(OUT, device, name + ".png"), ROOT).replace("\\", "/"),
        "device": {"name": device, "px": list(scenario.DEVICES[device][:2]), "ppi": u.ppi, "pxPerUnit": u.per_pt,
                   "unit": u.unit, "devicePxPerDesignPx": round(u.k, 4)},
        "window": sb["screen"], "stage": {"design": sb["design"], "framed": sb.get("framed"), "rect": [sb["x"], sb["y"], sb["w"], sb["h"]]},
        "tapTargets": {
            "floor": u.both(MIN_TARGET), "checked": c2.get("checkedCount", 0), "undersizedCount": c2.get("failed", 0),
            "undersized": undersized,
            "gapFloor": u.both(MIN_GAP), "gapsChecked": c3.get("checkedCount", 0), "tightGapCount": c3.get("failed", 0),
            "tightGaps": gaps, "total": pol.get("targets"),
        },
        "text": {
            "count": len(texts), "captionFloor": u.both(CAPTION_FLOOR),
            "min": u.both(min(sizes)) if sizes else None, "median": u.both(statistics.median(sizes)) if sizes else None,
            "underCaptionFloor": sum(1 for s in sizes if s < CAPTION_FLOOR - 0.5),
            "contrastSamples": len(ratios), "contrastFails": sum(1 for r in ratios if not r["pass"]),
            "minContrast": min((r["ratio"] for r in ratios), default=None),
            "items": texts,
        },
        "safeArea": {
            "insets": safe,
            "intrusions": [f["subject"] for f in findings if f["rule"] == "POL-C5" and "unsafe area" in (f["detail"] or "")],
        },
        "polish": {"counts": counts, "findings": findings, "fill": pol.get("fill")},
        "layoutProblems": lay.get("problems") or [],
    }


def settled(name, timeout=20.0):
    """Waits until the router's fade is gone for two reads in a row (state.transitioning), then a beat for the fade-out."""
    end, calm = time.time() + timeout, 0
    while time.time() < end:
        calm = calm + 1 if not harness.call("state", timeout=10).get("transitioning") else 0
        if calm >= 2:
            time.sleep(0.4)
            return
        time.sleep(0.15)
    print(f"  {name}: still transitioning after {timeout:.0f}s", flush=True)


def near_uniform(png):
    from PIL import Image, ImageStat
    img = Image.open(png).convert("L")
    img = img.crop((0, 0, img.width, int(img.height * 0.9)))  # the dev-build watermark sits at the bottom
    return max(ImageStat.Stat(img).stddev[0], 0.0) < 3.0


def capture(device, name, method, facts):
    run = f"uiaudit-{device}-{name}"
    for attempt in range(4):
        settled(name)
        src = harness.shot(run)
        if not near_uniform(src):
            break
        print(f"  {name}: near-uniform frame, retrying", flush=True)
        time.sleep(1.5)
    else:
        print(f"  {name}: still a blank frame after retries", flush=True)
    dst = os.path.join(OUT, device, name + ".png")
    os.makedirs(os.path.dirname(dst), exist_ok=True)
    shutil.copyfile(src, dst)
    facts[name] = measure(device, name, method, dst)
    f = facts[name]
    print(f"  {name:16} on {f['landedOn']}: targets {f['tapTargets']['undersizedCount']}/{f['tapTargets']['checked']} small, "
          f"contrast {f['text']['contrastFails']}/{f['text']['contrastSamples']} low, "
          f"{len(f['safeArea']['intrusions'])} in unsafe area", flush=True)


def stop(proc):
    try:
        harness.call("quit", timeout=3)
    except Exception:
        pass
    try:
        proc.wait(timeout=15)
    except Exception:
        subprocess.run(["taskkill", "/F", "/T", "/PID", str(proc.pid)], capture_output=True)
    time.sleep(1)


def audit_device(device, items, settle):
    facts_path = os.path.join(OUT, device, "facts.json")
    facts = {}
    if os.path.exists(facts_path):
        facts = json.load(open(facts_path, encoding="utf-8")).get("screens", {})
    run = f"uiaudit-{device}"
    spec = os.path.join(ROOT, "artifacts", run, "home.json")
    os.makedirs(os.path.dirname(spec), exist_ok=True)
    json.dump(HOME_SPEC, open(spec, "w", encoding="utf-8"))
    print(f"{device}: launching", flush=True)
    proc, _, _ = scenario.start(spec, device, run)
    try:
        for name, method, arg in items:
            try:
                if method == "goto":
                    try:
                        harness.call("home", timeout=10)
                    except Exception:
                        pass
                    r = harness.call("goto", screen=arg)
                else:
                    r = harness.call("scenario", preset=arg)
                    if isinstance(r, dict) and r.get("error"):
                        print(f"  {name}: in-session scenario failed ({r['error']}), relaunching", flush=True)
                        stop(proc)
                        proc, _, _ = scenario.start(arg, device, run)
                        r = {}
                if isinstance(r, dict) and r.get("error"):
                    facts[name] = {"screen": name, "method": method, "error": r["error"]}
                    print(f"  {name}: {r['error']}", flush=True)
                    continue
                time.sleep(settle * (2 if name in SLOW else 1))
                capture(device, name, method, facts)
                want = LANDS_ON.get(name)
                if want and facts[name].get("landedOn") != want:
                    print(f"  {name}: landed on {facts[name].get('landedOn')}, expected {want}", flush=True)
            except Exception as e:
                facts[name] = {"screen": name, "method": method, "error": repr(e)}
                print(f"  {name}: {e!r}", flush=True)
                if not harness.alive():
                    print("  player died, relaunching", flush=True)
                    proc, _, _ = scenario.start(spec, device, run)
    finally:
        stop(proc)
        os.makedirs(os.path.dirname(facts_path), exist_ok=True)
        with open(facts_path, "w", encoding="utf-8", newline="\n") as f:
            json.dump({"device": device, "captured": time.strftime("%Y-%m-%dT%H:%M:%S"), "screens": facts}, f, indent=1)
            f.write("\n")
    print(f"{device}: wrote {facts_path}", flush=True)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--device", default="iphone17pro", choices=AUDIT_DEVICES + ["all"])
    ap.add_argument("--screens", help="comma-separated capture names (default: every screen)")
    ap.add_argument("--dry-run", action="store_true", help="list what would be captured, launch nothing")
    ap.add_argument("--settle", type=float, default=1.5, help="seconds to wait after each screen opens")
    ap.add_argument("--build", action="store_true", help="rebuild the Windows dev player if it is stale")
    a = ap.parse_args()
    devices = AUDIT_DEVICES if a.device == "all" else [a.device]
    items = plan([s.strip() for s in a.screens.split(",") if s.strip()] if a.screens else None)

    if a.dry_run:
        for d in devices:
            w, h, top, bottom = scenario.DEVICES[d]
            print(f"{d} ({w}x{h}, safe {top}/{bottom}, window {'x'.join(map(str, scenario.window_size(d)))}): {len(items)} screens")
            for name, method, arg in items:
                print(f"  {name:16} {method:8} {arg:20} -> {os.path.relpath(os.path.join(OUT, d, name + '.png'), ROOT)}")
        return

    others = harness._other_players()
    if others:
        sys.exit("a GridGame player is already running (" + ", ".join(f"PID {p}" for p, _ in others)
                 + "); one game instance at a time, and this tool never closes someone else's. Try again once it is closed.")
    scenario.ensure_build(a.build)
    for d in devices:
        audit_device(d, items, a.settle)


if __name__ == "__main__":
    main()
