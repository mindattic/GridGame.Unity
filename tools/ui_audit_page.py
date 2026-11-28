"""Regenerate docs/design/ui-audit/data.js from the UI audit captures and reviews.

Inputs: docs/design/ui-audit/captures/<device>/facts.json, docs/design/ui-audit/findings-*.json and the
checklist tables in docs/research/MobileGameAccessibility.md. Each fix ticket's status is read with
"tickets ticket show <id> --json" and baked in, so rerun this after fixes land:
    python tools/ui_audit_page.py
"""
import json
import pathlib
import re
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
AUDIT = ROOT / "docs" / "design" / "ui-audit"
CHECKLIST = ROOT / "docs" / "research" / "MobileGameAccessibility.md"
TARGET = AUDIT / "data.js"
REVIEWS = ["findings-hub.json", "findings-shops.json", "findings-battle.json"]
DEVICES = ["iphone17pro", "pixel9", "ipad"]
RULE_ROW = re.compile(r"^\| ([A-Z]\d+) \| \*\*(.+?)\*\* \|")
RULE_ID = re.compile(r"[A-Z]\d+|§[\d.]+")

# Severity is set per checklist rule: high blocks reading, reaching or seeing the screen; medium breaks a
# placement, efficiency or clarity rule; low is tooling or polish.
HIGH = {"L1", "L5", "L6", "L10", "T1", "X1", "X2", "X4", "E5", "M3", "M5", "C1"}
LOW = {"T3", "X10"}


def load(path):
    return json.loads(path.read_text(encoding="utf-8"))


def checklist_rules():
    rules = {}
    for line in CHECKLIST.read_text(encoding="utf-8").splitlines():
        m = RULE_ROW.match(line)
        if m:
            rules[m.group(1)] = m.group(2)
    return rules


def rule_ids(finding):
    raw = finding.get("rule") or finding.get("checklist") or ""
    text = " / ".join(raw) if isinstance(raw, list) else raw
    if text.startswith("owner"):
        return text, []
    return text, RULE_ID.findall(text)


def severity(text, ids):
    if text.startswith("owner"):
        return "medium"
    if not ids or ids[0].startswith("§"):
        return "low"
    if any(i in HIGH for i in ids):
        return "high"
    return "low" if all(i in LOW for i in ids) else "medium"


def ticket(tid):
    out = subprocess.run(["tickets", "ticket", "show", tid, "--json"], capture_output=True, text=True,
                         encoding="utf-8", cwd=ROOT)
    if out.returncode != 0:
        return {"id": tid, "status": "unknown", "name": "", "ref": None}
    t = json.loads(out.stdout)
    return {"id": tid, "status": t.get("status"), "name": t.get("name") or "", "ref": t.get("ref")}


def facts(screen):
    tap, text = screen.get("tapTargets", {}), screen.get("text", {})
    polish = screen.get("polish", {})
    fails = {r: c["failed"] for r, c in polish.get("counts", {}).items() if c.get("failed")}
    problems = [p if isinstance(p, str) else json.dumps(p, ensure_ascii=False)
                for p in screen.get("layoutProblems", [])]
    low = sorted((i for i in text.get("items", []) if i.get("contrast") and not i["contrast"].get("pass")),
                 key=lambda i: i["contrast"]["ratio"])
    return {
        "landedOn": screen.get("landedOn"),
        "taps": [tap.get("checked", 0), tap.get("undersizedCount", 0), tap.get("tightGapCount", 0)],
        "textCount": text.get("count", 0),
        "minText": next(("%s %s" % (v, k) for k, v in (text.get("min") or {}).items() if k in ("pt", "dp")), None),
        "contrast": [text.get("contrastSamples", 0), text.get("contrastFails", 0), text.get("minContrast")],
        "lowContrast": [[i["text"], i["contrast"]["ratio"], i["contrast"]["need"],
                         i["contrast"]["fg"], i["contrast"]["bg"]] for i in low[:6]],
        "safeIntrusions": len(screen.get("safeArea", {}).get("intrusions", [])),
        "polishFails": fails,
        "layoutProblems": problems[:6],
    }


def build():
    rules = checklist_rules()
    reviews = [(name, load(AUDIT / name)) for name in REVIEWS]

    devices = {}
    for dev in DEVICES:
        path = AUDIT / "captures" / dev / "facts.json"
        if path.exists():
            d = load(path)
            devices[dev] = {"captured": d.get("captured"),
                            "screens": {n: facts(s) for n, s in d["screens"].items()}}

    screens = {}

    def screen(name, area):
        return screens.setdefault(name, {"name": name, "area": area, "review": None, "notes": [], "findings": []})

    findings, tids = [], []
    for name, r in reviews:
        area = name[len("findings-"):-len(".json")]
        lists = [r, r.get("screens", {})]
        for sname in r.get("scope", []):
            screen(sname, area)
        for sname, s in r.get("screens", {}).items():
            if isinstance(s, dict) and "status" in s:
                sc = screen(sname, area)
                sc["review"] = s["status"]
                sc["notes"] += [s[k] for k in ("notes", "reason") if s.get(k)]
                sc["findings"] += s.get("findings", [])
        for sname in [n for src in lists for key in ("reviewedFromScreenshot", "reviewedFromLayoutDumpOnly")
                      for n in src.get(key, [])]:
            screen(sname, area)["review"] = "reviewed"
        for sname, why in [kv for src in lists for kv in src.get("skipped", {}).items()]:
            sc = screen(sname, area)
            if sc["review"] is None:
                sc["review"] = "skipped"
                sc["notes"].append(why)

    for name, r in reviews:
        area = name[len("findings-"):-len(".json")]
        for f in r["findings"]:
            text, ids = rule_ids(f)
            entry = {
                "id": f["id"], "ticket": f["ticket"], "review": r["ticket"], "area": area,
                "rule": text, "ruleIds": ids, "severity": severity(text, ids),
                "summary": f["summary"], "detail": f.get("detail", ""), "verified": f.get("verified"),
                "screens": list(dict.fromkeys(f["screens"])),
            }
            findings.append(entry)
            tids.append(f["ticket"])
            for sname in entry["screens"]:
                screen(sname, area)["findings"].append(f["ticket"])
        tids += r.get("alreadyFiled", [])

    for dev in devices.values():
        for sname in dev["screens"]:
            screen(sname, "battle" if sname.startswith("battle") else "hub")
    for sc in screens.values():
        sc["findings"] = list(dict.fromkeys(sc["findings"]))
        tids += sc["findings"]
        sc["devices"] = [d for d in DEVICES if (AUDIT / "captures" / d / (sc["name"] + ".png")).exists()]
        sc["review"] = sc["review"] or "not-reviewed"

    tickets = {}
    for tid in dict.fromkeys(tids):
        tickets[tid] = ticket(tid)
        print("  %s %s" % (tid, tickets[tid]["status"]), file=sys.stderr)

    order = {"hub": 0, "shops": 1, "battle": 2}
    return {
        "checklist": "docs/research/MobileGameAccessibility.md",
        "reviews": [{"file": n, "ticket": r["ticket"], "capture": r.get("capture") or r.get("captures"),
                     "recheck": r.get("recheck")} for n, r in reviews],
        "falsePositives": [fp for _, r in reviews for fp in r.get("falsePositives", [])],
        "alreadyFiled": [t for _, r in reviews for t in r.get("alreadyFiled", [])],
        "rules": rules,
        "devices": devices,
        "screens": sorted(screens.values(), key=lambda s: (order.get(s["area"], 9), s["name"])),
        "findings": findings,
        "tickets": tickets,
    }


def main():
    data = build()
    TARGET.write_text("// Generated by python tools/ui_audit_page.py; ticket statuses as of the run.\n"
                      "window.UiAudit = " + json.dumps(data, ensure_ascii=False, indent=1) + ";\n",
                      encoding="utf-8", newline="\n")
    closed = sum(1 for t in data["tickets"].values() if t["status"] == "closed")
    print("%s: %d screens, %d findings, %d/%d tickets closed"
          % (TARGET.relative_to(ROOT), len(data["screens"]), len(data["findings"]), closed, len(data["tickets"])))


if __name__ == "__main__":
    main()
