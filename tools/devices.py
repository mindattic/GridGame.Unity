"""
GG-T2026: the target device matrix, tools/devices.json (GG-T2025), for the tools that size a player like a device
(scenario.py, uiaudit.py, harness.py aspects/safeareas).

A device name is a device id (iphone15pro, galaxys25, ...) or a shape id (iphone-402, galaxy-fhd, ...), which stands for
the shape's representative device. "all" is every in-target device and "shapes" one in-target device per shape (its
representative when that is in target); a device outside the target is still selectable by its id or shape.
"""
import json, os

PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "devices.json")
_DATA = json.load(open(PATH, encoding="utf-8"))
DEVICES = _DATA["devices"]
SHAPES = _DATA["shapes"]
SHAPE_BY_ID = {s["id"]: s for s in SHAPES}
DEFAULT = "iphone15pro"


def in_target(dev_id):
    """The owner's baseline: iPhone 15 Pro / 15 Pro Max and every iPhone 16 and 17, Galaxy S23 and newer plus the 2023+
    Z Flip and Fold covers."""
    d = DEVICES[dev_id]
    if "inTarget" in d:
        return bool(d["inTarget"])
    # TODO(GG-T2116): drop this fallback once every devices.json entry carries inTarget.
    if d["family"] == "iPhone":
        return d["year"] >= 2024 or dev_id in ("iphone15pro", "iphone15promax")
    return d["family"] == "Galaxy" and d["year"] >= 2023


TARGET = [d for d in DEVICES if in_target(d)]


def _shape_pick(s):
    if in_target(s["representative"]):
        return s["representative"]
    return next((d for d in s["devices"] if in_target(d)), None)


# one in-target device per shape, in shape order; a shape with no in-target device is left out.
SHAPE_DEVICES = [d for d in map(_shape_pick, SHAPES) if d]


def device_id(name):
    """A device id for a device or shape id; ValueError for anything else."""
    if name in DEVICES:
        return name
    if name in SHAPE_BY_ID:
        return SHAPE_BY_ID[name]["representative"]
    raise ValueError(f"unknown device or shape '{name}' (see tools/devices.json)")


def resolve(name):
    """The device ids a --device value names: one, one in-target device per shape ("shapes") or every in-target device
    ("all")."""
    if name == "shapes":
        return list(SHAPE_DEVICES)
    if name == "all":
        return list(TARGET)
    return [device_id(name)]


def screen(dev_id):
    """(width, height, safe-area top, safe-area bottom), portrait device px."""
    d = DEVICES[dev_id]
    return d["px"][0], d["px"][1], d["safe_px"]["top"], d["safe_px"]["bottom"]


def metrics(dev_id):
    """(ppi, device px per pt/dp, unit)."""
    d = DEVICES[dev_id]
    return d["ppi"], float(d["scale"]), d["unit"]


def shapes_help():
    """One line per shape for a --help epilog."""
    lines = []
    for s in SHAPES:
        pick = _shape_pick(s)
        note = "" if pick == s["representative"] else f"  [shapes/all: {pick or 'none in target'}]"
        lines.append(f"  {s['id']:18} -> {s['representative']:18} {s['label']}{note}")
    lines.append("in target (all): " + ", ".join(TARGET))
    return "\n".join(lines)
