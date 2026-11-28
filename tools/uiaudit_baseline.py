"""
GG-T2029: per-device findings and the baseline table from tools/uiaudit.py's facts.

    python tools/uiaudit_baseline.py iphone15pro iphone16e ...

Reads docs/design/ui-audit/captures/<device>/facts.json (only the screens uiaudit.py captures) and writes that folder's
findings.json: per screen the text that overflows or is cut off, elements clipped by a parent, overlaps, off-stage
labels, text under the camera cutout, in the top inset or over the home indicator (on-screen text rects through the
stage into device px, against tools/devices.json's cutout and safe area), tap targets under 44 pt, and contrast fails (samples
under the cutout read the painted black, so they are counted apart). Prints the Markdown summary table for
docs/design/ui-audit/BASELINE.md.
"""
import json, os, sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import devices
import uiaudit

MIN_PT = 44.0
SLACK = 8  # device px a text rect may graze an inset by (glyph boxes carry padding)


def device_rect(f, rect):
    """A design-px rect -> device px, through the stage rect in the window and the window -> panel scale."""
    sx, sy, sw, sh = f["stage"]["rect"]
    s = sw / f["stage"]["design"][0]
    k = f["device"]["px"][0] / f["window"][0]
    x, y, w, h = rect
    return ((sx + x * s) * k, (sy + y * s) * k, (sx + (x + w) * s) * k, (sy + (y + h) * s) * k)


def overlaps(a, b):
    return a[0] < b[2] and b[0] < a[2] and a[1] < b[3] and b[1] < a[3]


def screen_findings(device, f):
    d = devices.DEVICES[device]
    W, H = d["px"]
    safe = d["safe_px"]
    cut = d.get("cutout")
    cut_rect = ((W - cut["w"]) / 2, cut["top"], (W + cut["w"]) / 2, cut["top"] + cut["h"]) if cut else None

    under_cutout, top_inset, home_indicator = [], [], []
    contrast_fails, contrast_cutout = [], []
    for t in f["text"]["items"]:
        r = device_rect(f, t["rect"])
        if r[1] >= H or r[3] <= 0:
            continue  # scrolled out of view
        hit = cut_rect is not None and overlaps(r, cut_rect)
        if hit:
            under_cutout.append(t["text"])
        elif safe["top"] - r[1] > SLACK:
            top_inset.append(t["text"])
        if r[3] - (H - safe["bottom"]) > SLACK:
            home_indicator.append(t["text"])
        c = t.get("contrast")
        if c and not c["pass"]:
            (contrast_cutout if hit else contrast_fails).append({"text": t["text"], "ratio": c["ratio"], "need": c["need"],
                                                                  "fg": c["fg"], "bg": c["bg"]})

    overflow, clipped, overlap, offstage = [], [], [], []
    for p in f["polish"]["findings"]:
        if p["rule"] != "POL-C5":
            continue
        det = p["detail"] or ""
        if "overflows" in det or "cut off" in det:
            overflow.append({"subject": p["subject"], "detail": det})
        if "clipped by a parent" in det:
            clipped.append(p["subject"])
        if "overlaps" in det:
            overlap.append(det)
        if "off-stage" in det:
            offstage.append({"subject": p["subject"], "detail": det})

    small = []
    for u in f["tapTargets"]["undersized"]:
        w, h = u["w"].get("pt", u["w"].get("dp")), u["h"].get("pt", u["h"].get("dp"))
        if min(w, h) < MIN_PT:
            small.append({"target": u["target"], "pt": [w, h]})

    return {"landedOn": f.get("landedOn"), "png": f["png"], "overflow": overflow, "clipped": clipped, "overlap": overlap,
            "offStage": offstage, "underCutout": under_cutout, "topInset": top_inset, "homeIndicator": home_indicator,
            "engineUnsafeArea": f["safeArea"]["intrusions"], "targetsUnder44pt": small, "contrastFails": contrast_fails,
            "contrastUnderCutout": contrast_cutout, "contrastSamples": f["text"]["contrastSamples"],
            "minTextPt": (f["text"]["min"] or {}).get("pt"), "layoutProblems": f["layoutProblems"]}


KEYS = ["overflow", "clipped", "overlap", "offStage", "underCutout", "topInset", "homeIndicator", "targetsUnder44pt",
        "contrastFails", "contrastUnderCutout"]


def build(device):
    folder = os.path.join(uiaudit.OUT, device)
    facts = json.load(open(os.path.join(folder, "facts.json"), encoding="utf-8"))
    wanted = [n for n, _, _ in uiaudit.plan(None)]
    screens, errors = {}, {}
    for name in wanted:
        f = facts["screens"].get(name)
        if not f or "error" in f:
            errors[name] = (f or {}).get("error", "not captured")
            continue
        screens[name] = screen_findings(device, f)
    totals = {k: sum(len(s[k]) for s in screens.values()) for k in KEYS}
    totals["screensWith"] = {k: sorted(n for n, s in screens.items() if s[k]) for k in KEYS}
    out = {"ticket": "GG-T2029", "device": device, "name": devices.DEVICES[device]["name"],
           "shape": devices.DEVICES[device]["shape"], "captured": facts.get("captured"), "frame": devices.frame(device),
           "screens": len(screens), "errors": errors, "totals": totals, "findings": screens}
    with open(os.path.join(folder, "findings.json"), "w", encoding="utf-8", newline="\n") as fh:
        json.dump(out, fh, indent=1)
        fh.write("\n")
    return out


def main():
    rows = [build(d) for d in sys.argv[1:]]
    head = ["device", "shape", "screens", "errors"] + KEYS
    print("| " + " | ".join(head) + " |")
    print("|" + "---|" * len(head))
    for r in rows:
        t = r["totals"]
        print("| " + " | ".join([r["device"], r["shape"], str(r["screens"]), str(len(r["errors"]))]
                                + [f"{t[k]} ({len(t['screensWith'][k])})" for k in KEYS]) + " |")


if __name__ == "__main__":
    main()
