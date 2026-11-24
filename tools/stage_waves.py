"""Per-wave stage comparison from kept soak records (GG-T758; grew out of artifacts/t276-cave/an.py). Read-only: no
soak, no game. For each stage it reads the casual (or --source) battle records under one or more polish run dirs and
prints, per wave (1-based, as the player counts them):

  reach / clear    fights that started the wave, fights that cleared it, defeats inside it
  entering HP      party HP % and heroes standing when the wave's first action begins
  foes             roster, level, total max HP, content weight (base Str+Vit+Sta, ArmorPlate, support, abilities)
  cost             actions, hero damage taken, hero healing received, foe damage taken (avg per fight that reached it)
  damage source    hero damage split by how it landed: basic hit, enemy pincer, ability (area when it hit 2+ heroes),
                   reactive (counterstrike), status tick, and whether the dealer was buffed (rally etc.) at the time

plus the party's level against the stage's recommended level. Usage:

  python -I tools/stage_waves.py RUN_DIR [RUN_DIR ...] --stages cave-01,cave-02,cave-03 [--source casual]
                                 [--detail-wave 3] [--content DIR]

RUN_DIR is an artifacts/polish/<stamp> folder (records/<source>/<source>_<stage>_<n>.json). --content defaults to
Assets/GridGame/Resources/Content; pass an override dir's parent when a run used GAIDEN_CONTENT_OVERRIDE_DIR (files
missing there fall back to the default).
"""
import argparse
import collections
import glob
import json
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONTENT = os.path.join(ROOT, "Assets", "GridGame", "Resources", "Content")
NOISE_BUFFS = {"protection"}  # the hero Shield/guard stance, applied to the whole party every guard turn


def load_classes(override):
    classes = {c["Id"]: c for c in json.load(open(os.path.join(CONTENT, "classes.json"), encoding="utf-8"))}
    if override and os.path.exists(os.path.join(override, "classes.json")):
        for c in json.load(open(os.path.join(override, "classes.json"), encoding="utf-8")):
            classes[c["Id"]] = c
    return classes


def weight(cls):
    """Static content weight of one foe class: base Str+Vit+Sta, plate, support label, abilities."""
    if not cls:
        return 0, 0, "", []
    b = cls.get("BaseStats", {})
    sup = cls.get("Support") or {}
    return (b.get("Strength", 0) + b.get("Vitality", 0) + b.get("Stamina", 0), cls.get("ArmorPlate", 0) or 0,
            sup.get("Label", ""), cls.get("Abilities", []))


def classify_events(events, units, buffs):
    """Yield (kind, source_class, amount, crit, dealer_buffs) for each Damaged event that hit a hero.
    buffs: unit id -> set of active buff ids, updated in place."""
    ctx = {}  # actor id -> label of what it is doing in this action
    for e in events:
        t = e["type"]
        if t in ("TurnStarted", "ActionBegan"):
            ctx = {}
        elif t == "Attack":
            a = e["AttackerId"]
            if a not in ctx or not ctx[a].startswith("ability"):
                ctx[a] = "support-hit" if e.get("IsSupport") else ("basic-ranged" if e.get("IsRanged") else "basic")
        elif t == "AbilityResolved":
            tg = [x for x in e.get("Targets", []) if units.get(x, ("", ""))[1] == "Hero"]
            ctx[e["CasterId"]] = "ability:%s%s" % (e["AbilityId"], " (area)" if len(tg) >= 2 else "")
        elif t == "Triggered":
            if e.get("Kind") == "Reactive":
                ctx[e["UnitId"]] = "reactive:%s" % e["AbilityId"]
        elif t == "PincerFormed" and e.get("Team") == "Enemy":
            for a in (e.get("Attacker1"), e.get("Attacker2")):
                ctx[a] = "enemy-pincer"
            for s in e.get("Supporters", []) or []:
                ctx[s] = "enemy-pincer-support"
        elif t == "BuffApplied":
            buffs.setdefault(e["UnitId"], set()).add(e["BuffId"])
        elif t == "BuffRemoved":
            buffs.get(e["UnitId"], set()).discard(e["BuffId"])
        elif t == "Damaged":
            tc, tt = units.get(e["TargetId"], ("?", "?"))
            if tt != "Hero":
                continue
            src = e.get("SourceId", -1)
            if src is None or src < 0:
                yield "status:%s" % (e.get("Note") or "?"), "(none)", e["Amount"], False, ()
                continue
            sc, st = units.get(src, ("?", "?"))
            kind = ctx.get(src) or ("note:%s" % e["Note"] if e.get("Note") else "unlabelled")
            if st == "Hero":
                kind = "friendly:" + kind
            yield kind, sc, e["Amount"], e.get("Hit") == "Critical", tuple(sorted(buffs.get(src, set()) - NOISE_BUFFS))


def analyze(paths, classes):
    st = {"n": 0, "wins": 0, "lost": collections.Counter(), "lvl": collections.Counter(), "rec": set(),
          "w": collections.defaultdict(lambda: {
              "reach": 0, "clear": 0, "hp0": 0, "mhp": 0, "stand": 0, "act": 0, "taken": 0, "healed": 0,
              "dealt": 0, "foes": collections.Counter(), "foehp": 0, "foelvl": [],
              "kind": collections.Counter(), "src": collections.Counter(), "srckind": collections.Counter(),
              "crit": 0, "buffed": collections.Counter()})}
    for p in paths:
        d = json.load(open(p, encoding="utf-8"))
        st["n"] += 1
        st["rec"].add(d.get("recommendedLevel"))
        for h in d.get("party", []):
            if not h.get("guest"):
                st["lvl"][h["level"]] += 1
        o = d["outcome"]
        won = o["phase"] == "Victory"
        st["wins"] += won
        units = {}
        for t in d["turns"]:
            for s in (t["before"], t.get("after") or {}):
                for u in s.get("units", []):
                    units[u["id"]] = (u["class"], u["team"])
        bywave = collections.defaultdict(list)
        for t in d["turns"]:
            bywave[t["wave"]].append(t)
        last = max(bywave) if bywave else 0
        if not won:
            st["lost"][last + 1] += 1
        buffs = {}
        for w, ts in sorted(bywave.items()):
            W = st["w"][w + 1]
            W["reach"] += 1
            W["clear"] += won or w < last
            heroes = [u for u in ts[0]["before"]["units"] if u["team"] == "Hero"]
            W["hp0"] += sum(u["hp"] for u in heroes)
            W["mhp"] += sum(u["maxHp"] for u in heroes)
            W["stand"] += sum(1 for u in heroes if u["hp"] > 0)
            W["act"] += len(ts)
            seen = {}
            for t in ts:
                for u in t["before"]["units"]:
                    if u["team"] != "Hero" and u["id"] not in seen:
                        seen[u["id"]] = u
                for e in t.get("events", []):
                    if e["type"] == "Damaged" and units.get(e["TargetId"], ("", ""))[1] != "Hero":
                        W["dealt"] += e["Amount"]
                    if e["type"] == "Healed" and units.get(e.get("TargetId"), ("", ""))[1] == "Hero":
                        W["healed"] += e.get("Amount", 0)
                for kind, sc, amt, crit, bf in classify_events(t.get("events", []), units, buffs):
                    W["taken"] += amt
                    W["kind"][kind.split(":")[0] if kind.startswith("status") else kind] += amt
                    W["src"][sc] += amt
                    W["srckind"][(sc, kind)] += amt
                    W["crit"] += amt if crit else 0
                    for b in bf:
                        W["buffed"][b] += amt
            for u in seen.values():
                W["foes"][u["class"]] += 1
                W["foehp"] += u["maxHp"]
                W["foelvl"].append(u["level"])
    return st


def pct(a, b):
    return 100.0 * a / b if b else 0.0


def report(stage, st, classes, detail_wave):
    n = st["n"]
    lv = ", ".join("L%d x%d" % kv for kv in sorted(st["lvl"].items()))
    print("== %s  fights %d  wins %d (%.0f%%)  recommended L%s  party %s" % (
        stage, n, st["wins"], pct(st["wins"], n), "/".join(str(r) for r in st["rec"]), lv))
    print("   defeats by wave: %s" % (dict(sorted(st["lost"].items())) or "-"))
    tot = sum(W["taken"] for W in st["w"].values()) or 1
    for w in sorted(st["w"]):
        W = st["w"][w]
        r = W["reach"]
        foes = " + ".join("%s%s" % (c, "" if k == r else " x%.1f" % (k / r)) for c, k in W["foes"].most_common())
        wsum = sum(weight(classes.get(c))[0] * k for c, k in W["foes"].items()) / r
        plate = sum(weight(classes.get(c))[1] * k for c, k in W["foes"].items()) / r
        lvl = sum(W["foelvl"]) / len(W["foelvl"]) if W["foelvl"] else 0
        print("   w%d reach %2d clear %2d | enter HP %3.0f%% standing %.1f | foes %.1f L%.0f maxHP %4.0f base(S+V+St) %3.0f "
              "plate %3.0f | actions %3.0f taken %5.0f (%2.0f%% of stage) healed %4.0f dealt %5.0f | %s" % (
                  w, r, W["clear"], pct(W["hp0"], W["mhp"]), W["stand"] / r, sum(W["foes"].values()) / r, lvl,
                  W["foehp"] / r, wsum, plate, W["act"] / r, W["taken"] / r, pct(W["taken"], tot), W["healed"] / r,
                  W["dealt"] / r, foes))
        kinds = ", ".join("%s %.0f%%" % (k, pct(v, W["taken"])) for k, v in W["kind"].most_common(6))
        srcs = ", ".join("%s %.0f" % (k, v / r) for k, v in W["src"].most_common(4))
        print("        by kind: %s | by dealer/fight: %s | crit %.0f%%" % (kinds, srcs, pct(W["crit"], W["taken"])))
        if w == detail_wave:
            print("        wave %d detail (dealer, how: avg per fight)" % w)
            for (sc, k), v in W["srckind"].most_common(12):
                print("          %-16s %-44s %5.0f" % (sc, k, v / r))
            bf = ", ".join("%s %.0f%%" % (b, pct(v, W["taken"])) for b, v in W["buffed"].most_common(6))
            print("          dealt while the dealer carried a buff: %s" % (bf or "none"))
            for c in W["foes"]:
                s, pl, sup, ab = weight(classes.get(c))
                cl = classes.get(c) or {}
                print("          %-16s base S+V+St %d plate %d support %s brain %s targeting %s abilities %s" % (
                    c, s, pl, sup or "-", cl.get("Brain"), cl.get("Targeting"), ",".join(ab) or "-"))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("runs", nargs="+")
    ap.add_argument("--stages", required=True)
    ap.add_argument("--source", default="casual")
    ap.add_argument("--detail-wave", type=int, default=3)
    ap.add_argument("--content", default=None, help="override content dir (classes.json) the run used")
    a = ap.parse_args()
    classes = load_classes(a.content)
    for run in a.runs:
        print("##### %s (%s)" % (run, a.source))
        for stage in a.stages.split(","):
            paths = sorted(glob.glob(os.path.join(run, "records", a.source, "%s_%s_*.json" % (a.source, stage))))
            if not paths:
                print("== %s  no records" % stage)
                continue
            report(stage, analyze(paths, classes), classes, a.detail_wave)
        print()


if __name__ == "__main__":
    main()
