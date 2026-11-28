import json, sys
out_json, out_md = sys.argv[1], sys.argv[2]
REFERENCE = "iphone15pro"
IN_TARGET = {"iphone15pro", "iphone15promax", "iphone16", "iphone16plus", "iphone16pro", "iphone16promax", "iphone16e",
             "iphone17", "iphone17pro", "iphone17promax", "iphoneair",
             "galaxys23", "galaxys23plus", "galaxys23ultra", "galaxys23fe", "galaxys24", "galaxys24plus", "galaxys24ultra",
             "galaxys24fe", "galaxys25", "galaxys25plus", "galaxys25ultra", "galaxys25edge", "galaxys25fe",
             "galaxyzflip5", "galaxyzflip6", "galaxyzfold5cover", "galaxyzfold6cover"}
TIER = {"A14": 1, "A15": 1, "Snapdragon 888 / Exynos 2100": 1, "Snapdragon 8 Gen 1 / Exynos 2200": 1, "Exynos 2200 / Snapdragon 8 Gen 1": 1,
        "A16": 2, "A17 Pro": 2, "A18": 2, "Snapdragon 8 Gen 2": 2, "Snapdragon 8 Gen 3": 2, "Exynos 2400 / Snapdragon 8 Gen 3": 2,
        "Exynos 2400e": 2, "Exynos 2400": 2, "Tensor G4": 2,
        "A18 Pro": 3, "A19": 3, "A19 Pro": 3, "Snapdragon 8 Elite": 3}
# id, name, year, w, h, logical w, logical h (None = h / scale), scale, ppi, hz, soc, top, bottom (logical units), shape, [default_px, dp estimated]
rows = [
 ("iphone12mini", "iPhone 12 mini", 2020, 1080, 2340, 375, 812, 2.88, 476, 60, "A14", 50, 34, "iphone-mini"),
 ("iphone12", "iPhone 12", 2020, 1170, 2532, 390, 844, 3, 460, 60, "A14", 47, 34, "iphone-notch-390"),
 ("iphone12pro", "iPhone 12 Pro", 2020, 1170, 2532, 390, 844, 3, 460, 60, "A14", 47, 34, "iphone-notch-390"),
 ("iphone12promax", "iPhone 12 Pro Max", 2020, 1284, 2778, 428, 926, 3, 458, 60, "A14", 47, 34, "iphone-notch-428"),
 ("iphone13mini", "iPhone 13 mini", 2021, 1080, 2340, 375, 812, 2.88, 476, 60, "A15", 50, 34, "iphone-mini"),
 ("iphone13", "iPhone 13", 2021, 1170, 2532, 390, 844, 3, 460, 60, "A15", 47, 34, "iphone-notch-390"),
 ("iphone13pro", "iPhone 13 Pro", 2021, 1170, 2532, 390, 844, 3, 460, 120, "A15", 47, 34, "iphone-notch-390"),
 ("iphone13promax", "iPhone 13 Pro Max", 2021, 1284, 2778, 428, 926, 3, 458, 120, "A15", 47, 34, "iphone-notch-428"),
 ("iphone14", "iPhone 14", 2022, 1170, 2532, 390, 844, 3, 460, 60, "A15", 47, 34, "iphone-notch-390"),
 ("iphone14plus", "iPhone 14 Plus", 2022, 1284, 2778, 428, 926, 3, 458, 60, "A15", 47, 34, "iphone-notch-428"),
 ("iphone14pro", "iPhone 14 Pro", 2022, 1179, 2556, 393, 852, 3, 460, 120, "A16", 59, 34, "iphone-island-393"),
 ("iphone14promax", "iPhone 14 Pro Max", 2022, 1290, 2796, 430, 932, 3, 460, 120, "A16", 59, 34, "iphone-island-430"),
 ("iphone15", "iPhone 15", 2023, 1179, 2556, 393, 852, 3, 460, 60, "A16", 59, 34, "iphone-island-393"),
 ("iphone15plus", "iPhone 15 Plus", 2023, 1290, 2796, 430, 932, 3, 460, 60, "A16", 59, 34, "iphone-island-430"),
 ("iphone15pro", "iPhone 15 Pro", 2023, 1179, 2556, 393, 852, 3, 460, 120, "A17 Pro", 59, 34, "iphone-island-393"),
 ("iphone15promax", "iPhone 15 Pro Max", 2023, 1290, 2796, 430, 932, 3, 460, 120, "A17 Pro", 59, 34, "iphone-island-430"),
 ("iphone16", "iPhone 16", 2024, 1179, 2556, 393, 852, 3, 460, 60, "A18", 59, 34, "iphone-island-393"),
 ("iphone16plus", "iPhone 16 Plus", 2024, 1290, 2796, 430, 932, 3, 460, 60, "A18", 59, 34, "iphone-island-430"),
 ("iphone16pro", "iPhone 16 Pro", 2024, 1206, 2622, 402, 874, 3, 460, 120, "A18 Pro", 62, 34, "iphone-402"),
 ("iphone16promax", "iPhone 16 Pro Max", 2024, 1320, 2868, 440, 956, 3, 460, 120, "A18 Pro", 62, 34, "iphone-440"),
 ("iphone16e", "iPhone 16e", 2025, 1170, 2532, 390, 844, 3, 460, 60, "A18", 47, 34, "iphone-notch-390"),
 ("iphone17", "iPhone 17", 2025, 1206, 2622, 402, 874, 3, 460, 120, "A19", 62, 34, "iphone-402"),
 ("iphone17pro", "iPhone 17 Pro", 2025, 1206, 2622, 402, 874, 3, 460, 120, "A19 Pro", 62, 34, "iphone-402"),
 ("iphone17promax", "iPhone 17 Pro Max", 2025, 1320, 2868, 440, 956, 3, 460, 120, "A19 Pro", 62, 34, "iphone-440"),
 ("iphoneair", "iPhone Air", 2025, 1260, 2736, 420, 912, 3, 460, 120, "A19 Pro", 68, 34, "iphone-air"),
 ("galaxys21", "Galaxy S21", 2021, 1080, 2400, 360, None, 3, 421, 120, "Snapdragon 888 / Exynos 2100", 32, 24, "galaxy-20x9"),
 ("galaxys21plus", "Galaxy S21+", 2021, 1080, 2400, 384, None, 2.8125, 394, 120, "Snapdragon 888 / Exynos 2100", 32, 24, "galaxy-20x9"),
 ("galaxys21ultra", "Galaxy S21 Ultra", 2021, 1440, 3200, 384, None, 3.75, 515, 120, "Snapdragon 888 / Exynos 2100", 32, 24, "galaxy-20x9", (1080, 2400)),
 ("galaxys22", "Galaxy S22", 2022, 1080, 2340, 360, None, 3, 425, 120, "Snapdragon 8 Gen 1 / Exynos 2200", 32, 24, "galaxy-fhd"),
 ("galaxys22plus", "Galaxy S22+", 2022, 1080, 2340, 384, None, 2.8125, 393, 120, "Snapdragon 8 Gen 1 / Exynos 2200", 32, 24, "galaxy-fhd"),
 ("galaxys22ultra", "Galaxy S22 Ultra", 2022, 1440, 3088, 412, None, 3.5, 500, 120, "Snapdragon 8 Gen 1 / Exynos 2200", 32, 24, "galaxy-qhd", (1080, 2316)),
 ("galaxys23", "Galaxy S23", 2023, 1080, 2340, 360, None, 3, 425, 120, "Snapdragon 8 Gen 2", 32, 24, "galaxy-fhd"),
 ("galaxys23plus", "Galaxy S23+", 2023, 1080, 2340, 384, None, 2.8125, 393, 120, "Snapdragon 8 Gen 2", 32, 24, "galaxy-fhd"),
 ("galaxys23ultra", "Galaxy S23 Ultra", 2023, 1440, 3088, 412, None, 3.5, 500, 120, "Snapdragon 8 Gen 2", 32, 24, "galaxy-qhd", (1080, 2316)),
 ("galaxys23fe", "Galaxy S23 FE", 2023, 1080, 2340, 384, None, 2.8125, 403, 120, "Exynos 2200 / Snapdragon 8 Gen 1", 32, 24, "galaxy-fhd", None, True),
 ("galaxys24", "Galaxy S24", 2024, 1080, 2340, 360, None, 3, 416, 120, "Exynos 2400 / Snapdragon 8 Gen 3", 32, 24, "galaxy-fhd"),
 ("galaxys24plus", "Galaxy S24+", 2024, 1440, 3120, 412, None, 3.5, 513, 120, "Exynos 2400 / Snapdragon 8 Gen 3", 32, 24, "galaxy-qhd", (1080, 2340)),
 ("galaxys24ultra", "Galaxy S24 Ultra", 2024, 1440, 3120, 412, None, 3.5, 505, 120, "Snapdragon 8 Gen 3", 32, 24, "galaxy-qhd", (1080, 2340)),
 ("galaxys24fe", "Galaxy S24 FE", 2024, 1080, 2340, 384, None, 2.8125, 385, 120, "Exynos 2400e", 32, 24, "galaxy-fhd", None, True),
 ("galaxys25", "Galaxy S25", 2025, 1080, 2340, 360, None, 3, 416, 120, "Snapdragon 8 Elite", 32, 24, "galaxy-fhd"),
 ("galaxys25plus", "Galaxy S25+", 2025, 1440, 3120, 412, None, 3.5, 513, 120, "Snapdragon 8 Elite", 32, 24, "galaxy-qhd", (1080, 2340)),
 ("galaxys25ultra", "Galaxy S25 Ultra", 2025, 1440, 3120, 412, None, 3.5, 498, 120, "Snapdragon 8 Elite", 32, 24, "galaxy-qhd", (1080, 2340)),
 ("galaxys25edge", "Galaxy S25 Edge", 2025, 1440, 3120, 412, None, 3.5, 513, 120, "Snapdragon 8 Elite", 32, 24, "galaxy-qhd", (1080, 2340)),
 ("galaxys25fe", "Galaxy S25 FE", 2025, 1080, 2340, 384, None, 2.8125, 385, 120, "Exynos 2400", 32, 24, "galaxy-fhd", None, True),
 ("galaxyzflip5", "Galaxy Z Flip5 (main)", 2023, 1080, 2640, 393, None, 2.75, 425, 120, "Snapdragon 8 Gen 2", 32, 24, "galaxy-flip-main", None, True),
 ("galaxyzflip6", "Galaxy Z Flip6 (main)", 2024, 1080, 2640, 393, None, 2.75, 425, 120, "Snapdragon 8 Gen 3", 32, 24, "galaxy-flip-main"),
 ("galaxyzfold5cover", "Galaxy Z Fold5 (cover)", 2023, 904, 2316, 301, None, 3, 402, 120, "Snapdragon 8 Gen 2", 32, 24, "galaxy-fold-cover", None, True),
 ("galaxyzfold6cover", "Galaxy Z Fold6 (cover)", 2024, 968, 2376, 323, None, 3, 410, 120, "Snapdragon 8 Gen 3", 32, 24, "galaxy-fold-cover"),
 ("pixel9", "Pixel 9", 2024, 1080, 2424, 412, None, 2.625, 422, 120, "Tensor G4", 42, 24, "pixel-20x9"),
 ("ipad", "iPad (10th gen)", 2022, 1640, 2360, 820, 1180, 2, 264, 60, "A14", 24, 20, "ipad"),
]
SHAPES = [
 ("iphone-mini", "iphone13mini", "iPhone 12 mini / 13 mini (notch, 375 pt, native 2.88x)"),
 ("iphone-notch-390", "iphone14", "iPhone 12 / 12 Pro / 13 / 13 Pro / 14 / 16e (notch, 390 pt)"),
 ("iphone-notch-428", "iphone14plus", "iPhone 12 Pro Max / 13 Pro Max / 14 Plus (notch, 428 pt)"),
 ("iphone-island-393", "iphone16", "iPhone 14 Pro / 15 / 15 Pro / 16 (Dynamic Island, 393 pt)"),
 ("iphone-island-430", "iphone16plus", "iPhone 14 Pro Max / 15 Plus / 15 Pro Max / 16 Plus (Dynamic Island, 430 pt)"),
 ("iphone-402", "iphone17pro", "iPhone 16 Pro / 17 / 17 Pro (402 pt)"),
 ("iphone-440", "iphone17promax", "iPhone 16 Pro Max / 17 Pro Max (440 pt)"),
 ("iphone-air", "iphoneair", "iPhone Air (420 pt, 68 pt top inset)"),
 ("galaxy-20x9", "galaxys21", "Galaxy S21 / S21+ / S21 Ultra (20:9)"),
 ("galaxy-fhd", "galaxys25", "Galaxy S22-S25 base, S22+ / S23+ and the FEs (1080x2340, 19.5:9)"),
 ("galaxy-qhd", "galaxys25ultra", "Galaxy S22-S25 Ultra, S24+ / S25+, S25 Edge (1440-wide QHD+, 19.5:9)"),
 ("galaxy-flip-main", "galaxyzflip6", "Galaxy Z Flip5 / Flip6 main screen (22:9)"),
 ("galaxy-fold-cover", "galaxyzfold6cover", "Galaxy Z Fold5 / Fold6 cover screen (about 9:23)"),
 ("pixel-20x9", "pixel9", "Pixel 9 (reference shape the tools already use)"),
 ("ipad", "ipad", "iPad 10th gen (reference: letterboxed)"),
]
# GG-T2119: the camera cutout and the display's rounded corners, in pt. cutout: (type, w, h, top, corner radius); type is
# island (a pill floating below the top edge), notch (hangs from the top edge, bottom corners rounded) or punch (a circle).
# Each device's cutout and corner radius, and which of them are estimates (DEVICES.md, "Camera cutouts", cites both).
ISLAND = ("island", 126.0, 37.33, 11.33, 18.67)
PUNCH = ("punch", 18.0, 18.0, 11.0, 9.0)
CUTOUTS = {
 # id: (cutout or None, display corner radius pt, estimated fields)
 "iphone12mini": (None, 44.0, []), "iphone13mini": (None, 44.0, []),
 "iphone12": (("notch", 210.0, 30.0, 0.0, 20.0), 47.33, ["cutout"]),
 "iphone12pro": (("notch", 210.0, 30.0, 0.0, 20.0), 47.33, ["cutout"]),
 "iphone12promax": (("notch", 210.0, 30.0, 0.0, 20.0), 53.33, ["cutout"]),
 "iphone13": (("notch", 162.0, 33.0, 0.0, 20.0), 47.33, ["cutout"]),
 "iphone13pro": (("notch", 162.0, 33.0, 0.0, 20.0), 47.33, ["cutout"]),
 "iphone13promax": (("notch", 162.0, 33.0, 0.0, 20.0), 53.33, ["cutout"]),
 "iphone14": (("notch", 162.0, 33.0, 0.0, 20.0), 47.33, ["cutout"]),
 "iphone14plus": (("notch", 162.0, 33.0, 0.0, 20.0), 53.33, ["cutout"]),
 "iphone16e": (("notch", 162.0, 33.0, 0.0, 20.0), 47.33, ["cutout"]),
 **{k: (ISLAND, 55.0, ["cutout"]) for k in ("iphone14pro", "iphone14promax", "iphone15", "iphone15plus", "iphone15pro",
                                            "iphone15promax", "iphone16", "iphone16plus")},
 "iphone16pro": (("island", 126.0, 37.33, 14.0, 18.67), 62.0, ["cutout"]),
 "iphone16promax": (("island", 126.0, 37.33, 14.0, 18.67), 62.0, ["cutout"]),
 **{k: (("island", 126.0, 37.33, 14.0, 18.67), 62.0, ["cutout", "screen_radius"]) for k in ("iphone17", "iphone17pro", "iphone17promax")},
 "iphoneair": (("island", 126.0, 37.33, 20.0, 18.67), 62.0, ["cutout", "screen_radius"]),
 # Galaxy (dp): one centred punch-hole under the 32 dp status bar on every in-target model.
 **{k: (PUNCH, 30.0, ["cutout", "screen_radius"]) for k in ("galaxys23", "galaxys23plus", "galaxys23fe", "galaxys23ultra", "galaxys24",
                                                             "galaxys24plus", "galaxys24fe", "galaxys25", "galaxys25plus", "galaxys25fe")},
 **{k: (PUNCH, 26.0, ["cutout", "screen_radius"]) for k in ("galaxys24ultra", "galaxys25ultra", "galaxys25edge")},
 **{k: (PUNCH, 34.0, ["cutout", "screen_radius"]) for k in ("galaxyzflip5", "galaxyzflip6")},
 **{k: (PUNCH, 28.0, ["cutout", "screen_radius"]) for k in ("galaxyzfold5cover", "galaxyzfold6cover")},
}
devices = {}
for r in rows:
    (id_, name, year, w, h, lw, lh, scale, ppi, hz, soc, top, bot, shape), extra = r[:14], r[14:]
    default_px = extra[0] if len(extra) > 0 else None
    est_dp = bool(extra[1]) if len(extra) > 1 else False
    apple = id_.startswith("iphone") or id_ == "ipad"
    if lh is None:
        lh = round(h / scale)
    d = {"name": name, "family": "iPhone" if id_.startswith("iphone") else "iPad" if id_ == "ipad" else "Pixel" if id_.startswith("pixel") else "Galaxy",
         "year": year, "px": [w, h]}
    if default_px:
        d["default_px"] = list(default_px)
    d.update({"unit": "pt" if apple else "dp", "scale": scale, "logical": [lw, lh], "ppi": ppi,
              "safe_px": {"top": round(top * scale), "bottom": round(bot * scale), "left": 0, "right": 0},
              "aspect": round(w / h, 4), "hz": hz, "soc": soc, "soc_tier": TIER[soc], "shape": shape, "inTarget": id_ in IN_TARGET})
    cut, corner, cut_est = CUTOUTS.get(id_, (None, None, []))
    px = lambda v: round(v * scale)
    d["cutout"] = None if cut is None else {"type": cut[0], "w": px(cut[1]), "h": px(cut[2]), "top": px(cut[3]), "radius": px(cut[4]),
                                            "pt": {"w": cut[1], "h": cut[2], "top": cut[3], "radius": cut[4]}}
    d["screen_radius"] = None if corner is None else {"px": px(corner), "pt": corner}
    est = list(cut_est)
    if not apple:
        est.append("safe_px")
        if est_dp:
            est.append("scale")
    if est:
        d["estimated"] = est
    devices[id_] = d
shapes = []
for sid, rep, label in SHAPES:
    members = [k for k, v in devices.items() if v["shape"] == sid]
    rd = devices[rep]
    assert rd["shape"] == sid, rep
    shapes.append({"id": sid, "representative": rep, "label": label, "px": rd["px"], "aspect": rd["aspect"], "devices": members, "inTarget": any(devices[m]["inTarget"] for m in members)})
for k, v in devices.items():
    assert any(k in s["devices"] for s in shapes), k
data = {"schema": 1, "checked": "2026-11-28", "reference": REFERENCE,
        "about": "GridGame target device matrix (GG-T2025, baseline GG-T2116); docs/devices/DEVICES.md cites the sources. px: the physical panel, portrait. default_px: the FHD+ resolution Samsung ships its QHD+ panels at, which is what the app sees until the player changes it. scale: px per pt (iOS) or dp (Android, default display size). safe_px: the portrait safe area in device px. cutout: the camera cutout in device px (pt alongside): type island (pill below the top edge), notch (hangs from the top edge, bottom corners rounded) or punch (circle); w, h, top offset, corner radius; null where not recorded. screen_radius: the display's rounded-corner radius (circular approximation of Apple's continuous corner). reference: the owner's baseline device. inTarget: the device is inside the 2023+ target (iPhone 15 Pro and newer, Galaxy S23 and newer); out-of-target rows stay for comparison. estimated: fields not taken from a published figure.",
        "soc_tiers": {"1": "A14, A15, Snapdragon 888, Snapdragon 8 Gen 1, Exynos 2100, Exynos 2200",
                      "2": "A16, A17 Pro, A18, Snapdragon 8 Gen 2, Snapdragon 8 Gen 3, Exynos 2400 / 2400e, Tensor G4",
                      "3": "A18 Pro, A19, A19 Pro, Snapdragon 8 Elite"},
        "shapes": shapes, "devices": devices}
with open(out_json, "w", newline="\n", encoding="utf-8") as f:
    one = lambda v: json.dumps(v, ensure_ascii=False)
    f.write("{\n")
    for key in ("schema", "checked", "reference", "about", "soc_tiers"):
        f.write(f' {one(key)}: {one(data[key])},\n')
    f.write(' "shapes": [\n' + ",\n".join("  " + one(s) for s in shapes) + "\n ],\n")
    f.write(' "devices": {\n' + ",\n".join(f"  {one(k)}: {one(v)}" for k, v in devices.items()) + "\n }\n}\n")
def tgt(s):
    n = sum(devices[m]["inTarget"] for m in s["devices"])
    return "all" if n == len(s["devices"]) else "none" if n == 0 else "some"
L = ["| Shape | Representative | Portrait px | Aspect | In target | Models |", "|---|---|---|---|---|---|"]
for s in shapes:
    L.append(f"| `{s['id']}` | `{s['representative']}` | {s['px'][0]}×{s['px'][1]} | {s['aspect']:.3f} | {tgt(s)} | {', '.join(devices[m]['name'] + ('' if devices[m]['inTarget'] else ' (out)') for m in s['devices'])} |")
L += ["", "| Id | Model | Year | Portrait px | Logical | Scale | ppi | Safe px T/B/L/R | Aspect | Hz | SoC (tier) | Shape | In target |",
      "|---|---|---|---|---|---|---|---|---|---|---|---|---|"]
for k, d in devices.items():
    e = d.get("estimated", [])
    px = f"{d['px'][0]}×{d['px'][1]}" + (f" (default {d['default_px'][0]}×{d['default_px'][1]})" if "default_px" in d else "")
    sp = d["safe_px"]
    safe = f"{sp['top']}/{sp['bottom']}/{sp['left']}/{sp['right']}" + ("\\*" if "safe_px" in e else "")
    star = "\\*" if "scale" in e else ""
    L.append(f"| `{k}` | {d['name']} | {d['year']} | {px} | {d['logical'][0]}×{d['logical'][1]} {d['unit']}{star} | {d['scale']}{star} | {d['ppi']} | {safe} | {d['aspect']:.3f} | {d['hz']} | {d['soc']} ({d['soc_tier']}) | `{d['shape']}` | {'**yes**' if d['inTarget'] else 'out'} |")
L += ["", "| Id | Model | Cutout | W×H pt | Top pt | Cutout radius pt | Screen radius pt | Device px (w×h, top, r / screen r) |",
      "|---|---|---|---|---|---|---|---|"]
for k, d in devices.items():
    c, sr, e = d["cutout"], d["screen_radius"], d.get("estimated", [])
    if c is None and sr is None:
        continue
    cs, ss = ("\*" if "cutout" in e else ""), ("\*" if "screen_radius" in e else "")
    srs = f"{sr['pt']:g}{ss}" if sr else "—"
    if c:
        p = c["pt"]
        L.append(f"| `{k}` | {d['name']} | {c['type']}{cs} | {p['w']:g}×{p['h']:g} | {p['top']:g} | {p['radius']:g} | {srs} | {c['w']}×{c['h']}, {c['top']}, {c['radius']} / {sr['px'] if sr else '—'} |")
    else:
        L.append(f"| `{k}` | {d['name']} | — | — | — | — | {srs} | — / {sr['px']} |")
shape_t, dev_t, cut_t = [], [], []
cur = shape_t
for line in L:
    if line.startswith("| Id | Model | Year"):
        cur = dev_t
    if line.startswith("| Id | Model | Cutout"):
        cur = cut_t
    if line:
        cur.append(line)
old = open(out_md, encoding="utf-8").read().split("\n")
res, i = [], 0
while i < len(old):
    if old[i].startswith("| Shape |") or old[i].startswith("| Id | Model"):
        j = i
        while j < len(old) and old[j].startswith("|"):
            j += 1
        if old[i].startswith("| Shape |"):
            res += shape_t
        elif old[i].startswith("| Id | Model | Cutout"):
            res += cut_t
        else:
            res += dev_t
        i = j
    else:
        res.append(old[i])
        i += 1
with open(out_md, "w", newline="\n", encoding="utf-8") as f:
    f.write("\n".join(res))
print(len(devices), "devices", len(shapes), "shapes")
