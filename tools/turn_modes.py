"""Turn-mode comparison soak (GG-T778, docs/TURN_MODES.md).

For each requested turn mode and difficulty, runs the HeldTurnSoakReport bot soak headlessly
(`dotnet test --filter`) with GRIDGAME_TURN_MODE set to that mode, on one fixed stage list and seed set, for the smart
(greedy, randomness 0) and casual (randomness 0.5) bots. The soak writes turn-mode-<mode>-<bot>.json per bot
(GG-T913); this script gathers them into artifacts/turn-modes/<date>/ as turn-mode-<mode>-<bot>-d<difficulty>.json and
prints one markdown comparison table (the docs/TURN_MODES.md metrics, all listed stages pooled), plus a per-stage win
table with --per-stage.

    python tools/turn_modes.py                              # PartyWindow, both bots, difficulty 5 and 10
    python tools/turn_modes.py --modes PartyWindow,AnyHeroEveryTurn --bots smart
    python tools/turn_modes.py --dry-run                    # print the dotnet commands and env, run nothing
    python tools/turn_modes.py --table artifacts/turn-modes/2026-11-24   # re-print the table from gathered JSONs

The soak knobs it sets (HeldTurnSoakReport): GRIDGAME_TURN_MODE, GRIDGAME_DIFFICULTY, GRIDGAME_SOAK_STAGES (the
campaign is still played in order up to the last listed stage, only the listed ones are tallied), GRIDGAME_SOAK_SEEDS
and GRIDGAME_SOAK_OUT.
"""
import argparse
import datetime
import glob
import json
import os
import shutil
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PROJ = os.path.join(ROOT, "Tests", "Core.Tests", "Core.Tests.csproj")

MODES = ("PartyWindow", "AnyHeroEveryTurn", "UnmovedThisRound", "HeroTimeline")
# The soak's own test-case labels: "smart" is the greedy HeroBot (randomness 0).
BOTS = {"smart": "greedy", "casual": "casual"}
DIFFICULTIES = (5, 10)
# One stage per main-campaign region (the nine themes before the greenvalley-04 wall, GG-T742/GG-T919), spanning early,
# mid and late campaign, with three boss stages: swamp-01 (lich), cave-03 (cyclopes) and cathedral-03 (hierarch,
# the finale). Fixed: every mode is measured on exactly these.
STAGES = ("greenvalley-01", "lanternroad-02", "desert-02", "swamp-01", "cave-03",
          "still-02", "cityruins-02", "cathedral-03", "bloom-03")
SEEDS = (1, 2, 3)

# (column, json key, format) for the ALL row of each run, docs/TURN_MODES.md metric order.
COLUMNS = (
    ("win %", "winPct", "{:.0f}"),
    ("runs", "runs", "{}"),
    ("capped", "capped", "{}"),
    ("hero acts/run", "heroActionsPerRun", "{:.1f}"),
    ("enemy turns/run", "enemyTurnsPerRun", "{:.1f}"),
    ("secs/run", "secondsPerRun", "{:.0f}"),
    ("heroes/turn", "heroesPerTurn", "{:.2f}"),
    ("legal acts/turn", "legalActionsPerTurn", "{:.1f}"),
    ("hero acts/enemy turn", "heroActionsPerEnemyTurn", "{:.2f}"),
    ("downtime max", "downtimeMax", "{}"),
    ("downtime avg", "downtimeAvg", "{:.1f}"),
    ("enemy-s/stretch", "enemyOnlySecondsPerStretch", "{:.2f}"),
    ("pincers", "pincers", "{}"),
    ("chains", "chains", "{}"),
    ("guard forced/chosen", None, None),
    ("skill/spell/item", None, None),
    ("mana guard/pincer/other", None, None),
    ("refusals", "refusals", "{}"),
)


def csv(value):
    return [v.strip() for v in value.split(",") if v.strip()]


def parse_args(argv):
    p = argparse.ArgumentParser(description=__doc__.split("\n\n")[0], formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--modes", default="PartyWindow", help=f"comma list of {', '.join(MODES)} (default PartyWindow)")
    p.add_argument("--bots", default="smart,casual", help="comma list of smart, casual (default both)")
    p.add_argument("--difficulties", default=",".join(map(str, DIFFICULTIES)), help="comma list of 1..10 (default 5,10)")
    p.add_argument("--stages", default=",".join(STAGES), help="comma list of campaign stage ids (default: the fixed list)")
    p.add_argument("--seeds", default=",".join(map(str, SEEDS)), help="comma list of seeds (default 1,2,3)")
    p.add_argument("--out", help="output folder (default artifacts/turn-modes/<today>)")
    p.add_argument("--per-stage", action="store_true", help="also print a per-stage win-rate table")
    p.add_argument("--dry-run", action="store_true", help="print each dotnet command and its env, run nothing")
    p.add_argument("--table", metavar="DIR", help="only print the table from the JSONs already gathered in DIR")
    a = p.parse_args(argv)
    a.modes = csv(a.modes)
    a.bots = csv(a.bots)
    a.difficulties = [int(d) for d in csv(a.difficulties)]
    a.stages = csv(a.stages)
    a.seeds = [int(s) for s in csv(a.seeds)]
    for m in a.modes:
        if m not in MODES:
            p.error(f"unknown mode {m!r} (one of {', '.join(MODES)})")
    for b in a.bots:
        if b not in BOTS:
            p.error(f"unknown bot {b!r} (one of {', '.join(BOTS)})")
    for d in a.difficulties:
        if not 1 <= d <= 10:
            p.error(f"difficulty {d} is outside 1..10")
    return a


def soak_filter(bots):
    names = "|".join(f"Name~{BOTS[b]}" for b in bots)
    return f"FullyQualifiedName~HeldTurnSoakReport&({names})"


def run_one(args, mode, difficulty, out, scratch, failed):
    """One dotnet test run: both requested bots for one mode at one difficulty. Returns the gathered JSON paths."""
    raw = os.path.join(scratch, f"{mode}-d{difficulty}")
    env = {
        "GRIDGAME_TURN_MODE": mode,
        "GRIDGAME_DIFFICULTY": str(difficulty),
        "GRIDGAME_SOAK_STAGES": ",".join(args.stages),
        "GRIDGAME_SOAK_SEEDS": ",".join(map(str, args.seeds)),
        "GRIDGAME_SOAK_OUT": raw,
    }
    cmd = ["dotnet", "test", PROJ, "--filter", soak_filter(args.bots), "--logger", "console;verbosity=normal"]
    if args.dry_run:
        print(" ".join(f"{k}={v}" for k, v in env.items()))
        print("  " + subprocess.list2cmdline(cmd))
        return []
    os.makedirs(raw, exist_ok=True)
    print(f"== {mode}, difficulty {difficulty}: {', '.join(args.bots)} ==", flush=True)
    log = os.path.join(out, f"soak-{mode}-d{difficulty}.log")
    with open(log, "w", encoding="utf-8") as f:
        r = subprocess.run(cmd, cwd=ROOT, env={**os.environ, **env}, stdout=f, stderr=subprocess.STDOUT)
    if r.returncode != 0:
        print(f"   dotnet test exited {r.returncode}; see {log}", file=sys.stderr)
        failed.append(f"{mode}-d{difficulty}")
    gathered = []
    for bot in args.bots:
        src = os.path.join(raw, f"turn-mode-{mode}-{BOTS[bot]}.json")
        if not os.path.exists(src):
            print(f"   missing {src}", file=sys.stderr)
            failed.append(f"{mode}-{bot}-d{difficulty}")
            continue
        dst = os.path.join(out, f"turn-mode-{mode}-{bot}-d{difficulty}.json")
        shutil.copyfile(src, dst)
        held = os.path.join(raw, f"held-turn-{BOTS[bot]}.json")
        if os.path.exists(held):
            shutil.copyfile(held, os.path.join(out, f"held-turn-{mode}-{bot}-d{difficulty}.json"))
        gathered.append(dst)
    return gathered


def load(out, paths=None):
    """Runs from the given paths, or (--table) every turn-mode-*-d*.json in out."""
    runs = []
    for path in sorted(glob.glob(os.path.join(out, "turn-mode-*-d*.json")) if paths is None else paths):
        name = os.path.basename(path)[len("turn-mode-"):-len(".json")]
        mode, bot, diff = name.rsplit("-", 2)
        with open(path, encoding="utf-8") as f:
            data = json.load(f)
        runs.append((MODES.index(mode) if mode in MODES else 99, mode, bot, int(diff[1:]), data))
    runs.sort(key=lambda r: (r[0], r[3], r[2]))
    return runs


def cell(row, key, fmt, column):
    if column == "guard forced/chosen":
        return f"{row.get('guardsForced', 0)}/{row.get('guardsChosen', 0)}"
    if column == "skill/spell/item":
        u = row.get("uses", {})
        return f"{u.get('Skill', 0)}/{u.get('Spell', 0)}/{u.get('Item', 0)}"
    if column == "mana guard/pincer/other":
        m = row.get("mana", {})
        other = sum(v for k, v in m.items() if k not in ("guard", "pincer"))
        return f"{m.get('guard', 0)}/{m.get('pincer', 0)}/{other}"
    v = row.get(key)
    return "-" if v is None else fmt.format(v)


def table(runs, per_stage):
    lines = ["| mode | bot | diff | " + " | ".join(c[0] for c in COLUMNS) + " |",
             "|---|---|---|" + "---|" * len(COLUMNS)]
    for _, mode, bot, diff, data in runs:
        row = data.get("all", {})
        lines.append(f"| {mode} | {bot} | {diff} | " + " | ".join(cell(row, k, f, c) for c, k, f in COLUMNS) + " |")
    if per_stage and runs:
        stages = []
        for *_, data in runs:
            for s in data.get("stages", {}):
                if s not in stages:
                    stages.append(s)
        lines += ["", "Per-stage win % (wins/runs):", "",
                  "| mode | bot | diff | " + " | ".join(stages) + " |",
                  "|---|---|---|" + "---|" * len(stages)]
        for _, mode, bot, diff, data in runs:
            st = data.get("stages", {})
            cells = [f"{st[s]['winPct']:.0f} ({st[s]['wins']}/{st[s]['runs']})" if s in st else "-" for s in stages]
            lines.append(f"| {mode} | {bot} | {diff} | " + " | ".join(cells) + " |")
    return "\n".join(lines)


def main(argv=None):
    args = parse_args(sys.argv[1:] if argv is None else argv)
    if args.table:
        print(table(load(args.table), args.per_stage))
        return 0
    out = args.out or os.path.join(ROOT, "artifacts", "turn-modes", datetime.date.today().isoformat())
    scratch = os.path.join(out, "raw")
    print(f"stages: {', '.join(args.stages)}")
    print(f"seeds: {', '.join(map(str, args.seeds))}; bots: {', '.join(args.bots)}; difficulties: "
          f"{', '.join(map(str, args.difficulties))}; out: {out}")
    if not args.dry_run:
        os.makedirs(out, exist_ok=True)
    failed, gathered = [], []
    for mode in args.modes:
        for diff in args.difficulties:
            gathered += run_one(args, mode, diff, out, scratch, failed)
    if args.dry_run:
        return 0
    md = table(load(out, gathered), args.per_stage)
    with open(os.path.join(out, "table.md"), "w", encoding="utf-8") as f:
        f.write(md + "\n")
    print()
    print(md)
    if failed:
        print("FAILED: " + ", ".join(failed), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
