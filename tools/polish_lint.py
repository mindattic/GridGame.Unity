"""Static Polish Standard lint (GG-T251, docs/POLISH.md): counts literal colors, sizes, spacing and durations
written at the call site instead of through the design tokens (App/UI/Tokens.cs, Theme.uss :root), faux bold,
per-site text-fit checks, random durations, hand-written easing curves and off-taxonomy sound cues.

    python tools/polish_lint.py            # table
    python tools/polish_lint.py --json     # machine-readable, read by tools/polish.py
    python tools/polish_lint.py --hits     # every POL-K hit, one line each (screen, file:line, what)

Scores follow POLISH.md section 3: a rule's score is the share of its subjects that pass. For "token" rules the
subject is each use site (a token use passes, a literal fails); for "never" rules the subject is each file
(a file with zero hits passes); for POL-F1 the subject is each distinct cue name.

The kit rules (GG-T1373, BIBLE §26.7, Ui.cs "the menu kit") read App/Screens only and score each screen class (a class
with zero hits passes): POL-K1 a screen draws its own page title ("title"/"h1" text, a "header" element) instead of
Ui.Header; POL-K2 a raw Ui.Window instead of a kit role (Panel, ListPanel, DetailPanel, Footer); POL-K3 a tappable
(Ui.Button, Ui.MenuRow, new Button) with no KindIcon; POL-K4 an inline style.margin*/padding* set in a screen.
"""
import json
import os
import re
import sys
from collections import Counter, defaultdict

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
APP = os.path.join(ROOT, "Assets", "GridGame", "App")
USS = os.path.join(ROOT, "Assets", "GridGame", "Resources", "UI", "Theme.uss")
TOKEN_FILES = {os.path.join(APP, "UI", "Tokens.cs")}
PALETTE_FILES = {os.path.join(APP, "UI", "WindowThemes.cs")}  # theme definitions: color data, exempt from POL-B1 (GG-T1184)
SKIP_DIRS = {os.path.join(APP, "Automation")}  # dev-only tooling, never shipped

NUM = r"-?\d+(?:\.\d+)?f?"

# rule -> (kind, literal regexes, token regexes)
CS_RULES = {
    # a literal size passed to FloatText( or board.Float( is counted by float_size_literals below (GG-T829)
    "POL-A1": ("token", [rf"\bfontSize\s*=\s*{NUM}\b"], [r"\bTypeScale\.\w+"]),
    "POL-B1": ("token", [r"\bnew\s+Color32?\s*\(", r"\bColor\.(?:white|black|red|green|blue|yellow|cyan|magenta|gray|grey)\b"],
               [r"\bPalette\.\w+"]),
    "POL-C1": ("token", [rf"\bstyle\.(?:margin|padding)\w*\s*=\s*{NUM}\b", rf"\bstyle\.(?:width|height|left|right|top|bottom)\s*=\s*{NUM}\b"],
               [r"\bSpace\.\w+", r"\bSize\.\w+", r"\bFit\.\w+", r"\bLength\.Percent\("]),  # GG-T272 stage-relative units
    "POL-E1": ("token", [rf"\bTween\.(?:Run|Wait|Move|Fade|Shake|Pop)\(\s*{NUM}\s*[,)]"], [r"\bTiming\.\w+"]),
    "POL-E2": ("token", [r"Mathf\.Sin\(\s*\w+\s*\*\s*Mathf\.PI\s*\)", r"\bMathf\.SmoothStep\("], [r"\bEase\.\w+"]),
    "POL-A3": ("never", [r"\bFontStyle\.Bold(?:AndItalic)?\b"], []),
    "POL-A6": ("never", [r"\.Length\s*>\s*\d+\s*\?"], []),
    "POL-E3": ("never", [r"\bTween\.\w+\(\s*Random\.Range"], []),
}
USS_RULES = {
    "POL-A1": ("token", [r"font-size:\s*\d"], [r"font-size:\s*var\(--type"]),
    "POL-B1": ("token", [r"\brgba?\(", r"#[0-9a-fA-F]{3,8}\b"], [r"var\(--color"]),
    "POL-C1": ("token", [r"\b(?:margin|padding)[\w-]*:\s*-?\d+px"], [r"\b(?:margin|padding)[\w-]*:\s*var\(--space"]),
    "POL-A3": ("never", [r"-unity-font-style:\s*bold"], []),
}
CUE = re.compile(r"\bSfx\(\s*\"([^\"]+)\"")
CUE_OK = re.compile(r"^(ui|battle|element|status|story)\.")


def strip_comments(text):
    text = re.sub(r"/\*.*?\*/", "", text, flags=re.S)
    return re.sub(r"//[^\n]*", "", text)


FLOAT_CALL = re.compile(r"\b(FloatText|Float)\(")
# a bare number, or a conditional choosing between two numbers ("e.Full ? 60 : 44")
LITERAL_SIZE = re.compile(rf"^(?:[^?]+\?\s*)?({NUM})(?:\s*:\s*({NUM}))?$")


def call_args(text, start):
    """The top-level arguments of the call whose "(" ends just before text[start], split on commas outside brackets
    and string literals."""
    args, cur, depth, quote, i = [], [], 0, None, start
    while i < len(text):
        ch = text[i]
        cur.append(ch)
        if quote:
            if ch == "\\":
                cur.append(text[i + 1:i + 2]); i += 1
            elif ch == quote:
                quote = None
        elif ch in "\"'":
            quote = ch
        elif ch in "([{":
            depth += 1
        elif ch in ")]}":
            if depth == 0:
                cur.pop()
                args.append("".join(cur).strip())
                return [a for a in args if a]
            depth -= 1
        elif ch == "," and depth == 0:
            cur.pop()
            args.append("".join(cur).strip()); cur = []
        i += 1
    return []


def literal_size(arg, floor=0.0):
    """True when arg is a literal number (or a conditional between two) of at least floor."""
    m = LITERAL_SIZE.match(arg.strip())
    return bool(m) and any(abs(float(v.rstrip("f"))) >= floor for v in m.groups() if v)


def float_size_literals(text):
    """POL-A1 (GG-T829): literal font sizes passed to FloatText( or board.Float(. A CombatText kind sizes the text, so
    BattleScreen.FloatText takes only a hold and a 0..1 magnitude (a literal of 4 or more there is a size), and
    BoardView.Float's fourth argument is its size, which must come from TypeScale."""
    n = 0
    for m in FLOAT_CALL.finditer(text):
        args = call_args(text, m.end())
        if m.group(1) == "FloatText":
            n += sum(literal_size(a, 4.0) for a in args[1:])
        elif len(args) >= 4 and literal_size(args[3]):
            n += 1
    return n


def uss_without_root(text):
    # a rule that only sets custom properties (:root, .theme-* palette overrides) defines tokens; it is not a use site (GG-T1184)
    def keep(m):
        # .theme-* per-window-theme rules are the palette definitions themselves (GG-T1185)
        if all(s.strip().startswith(".theme-") for s in m.group(1).split(",")):
            return ""
        decls = [d.strip() for d in m.group(2).split(";") if d.strip()]
        return "" if decls and all(d.startswith("--") for d in decls) else m.group(0)
    return re.sub(r"([^{}]+)\{([^{}]*)\}", keep, strip_comments(text))


def files():
    for d, _, fs in os.walk(APP):
        if any(d.startswith(s) for s in SKIP_DIRS):
            continue
        for f in fs:
            p = os.path.join(d, f)
            if f.endswith(".cs") and p not in TOKEN_FILES:
                yield p


def lint():
    lit = defaultdict(Counter)   # rule -> file -> literal count
    tok = defaultdict(Counter)   # rule -> file -> token count
    subjects_files = []
    cues = set()
    for p in files():
        rel = os.path.relpath(p, ROOT).replace("\\", "/")
        subjects_files.append(rel)
        text = strip_comments(open(p, encoding="utf-8", errors="replace").read())
        for rule, (_, lits, toks) in CS_RULES.items():
            if rule == "POL-B1" and p in PALETTE_FILES:
                continue
            for rx in lits:
                n = len(re.findall(rx, text))
                if n:
                    lit[rule][rel] += n
            for rx in toks:
                n = len(re.findall(rx, text))
                if n:
                    tok[rule][rel] += n
        n = float_size_literals(text)
        if n:
            lit["POL-A1"][rel] += n
        cues.update(CUE.findall(text))
    rel = os.path.relpath(USS, ROOT).replace("\\", "/")
    subjects_files.append(rel)
    uss = uss_without_root(open(USS, encoding="utf-8").read())
    for rule, (_, lits, toks) in USS_RULES.items():
        for rx in lits:
            n = len(re.findall(rx, uss))
            # a var() use matches no literal regex, but font-size/margin token uses must not double count
            if n:
                lit[rule][rel] += n
        for rx in toks:
            n = len(re.findall(rx, uss))
            if n:
                tok[rule][rel] += n
    # token uses inside USS literal regexes (font-size: var(...)) never matched "\d", so no correction is needed

    kinds = {r: k for r, (k, _, _) in {**USS_RULES, **CS_RULES}.items()}
    out = {}
    for rule in sorted(kinds):
        if kinds[rule] == "token":
            t, l = sum(tok[rule].values()), sum(lit[rule].values())
            checked, failed = t + l, l
        else:
            checked, failed = len(subjects_files), len(lit[rule])
        out[rule] = {
            "kind": kinds[rule], "checked": checked, "failed": failed,
            "score": round(100.0 * (checked - failed) / checked, 1) if checked else None,
            "literals": sum(lit[rule].values()), "tokens": sum(tok[rule].values()),
            "worst": lit[rule].most_common(5),
        }
    out.update(vfx_rules())
    out.update(kit_rules())
    bad = sorted(c for c in cues if not CUE_OK.match(c))
    out["POL-F1"] = {
        "kind": "cue", "checked": len(cues), "failed": len(bad),
        "score": round(100.0 * (len(cues) - len(bad)) / len(cues), 1) if cues else None,
        "worst": [(c, 1) for c in bad[:12]],
    }
    return out


CONTENT = os.path.join(ROOT, "Assets", "GridGame", "Resources", "Content")
VFX_FILES = [os.path.join(APP, "Battle", f) for f in
             ("BattleScreen.Fx.cs", "BattleScreen.Playback.cs", "BattleScreen.VfxLibrary.cs", "BattleScreen.VfxStyle.cs")]


def vfx_rules():
    """POL-E4: every element and status has a vfxstyle.json look, and the battle VFX code has no per-element or
    per-status switch of its own (GG-T253). POL-E5: three tiers that scale count, shake and hit-stop upward, read by the
    hit playback."""
    models = open(os.path.join(ROOT, "Assets", "GridGame", "Core", "Content", "ContentModels.cs"), encoding="utf-8").read()
    elements = re.search(r"enum DamageType\s*\{([^}]*)\}", models).group(1).replace(" ", "").split(",")
    buffs = [b["Id"] for b in json.load(open(os.path.join(CONTENT, "buffs.json"), encoding="utf-8"))]
    path = os.path.join(CONTENT, "vfxstyle.json")
    sheet = json.load(open(path, encoding="utf-8")) if os.path.exists(path) else {}
    have_el = {e.get("Id") for e in sheet.get("Elements", [])}
    have_st = {s.get("Id") for s in sheet.get("Statuses", [])}
    worst, failed = [], 0
    for e in elements:
        if e not in have_el:
            failed += 1; worst.append((f"element {e} has no style", 1))
    for b in buffs:
        if b not in have_st:
            failed += 1; worst.append((f"status {b} has no style", 1))
    switch = re.compile(r"case\s+DamageType\.\w+\s*:|case\s+\"(?:" + "|".join(map(re.escape, buffs)) + r")\"\s*:")
    for p in VFX_FILES:
        if not os.path.exists(p):
            continue
        n = len(switch.findall(strip_comments(open(p, encoding="utf-8", errors="replace").read())))
        if n:
            failed += 1; worst.append((os.path.relpath(p, ROOT).replace("\\", "/"), n))
    checked4 = len(elements) + len(buffs) + sum(os.path.exists(p) for p in VFX_FILES)

    tiers = {t.get("Tier"): t for t in sheet.get("Tiers", [])}
    order = [tiers.get(n) for n in ("Minor", "Major", "Ultimate")]
    t_fail, t_worst = 0, []
    for i, (name, t) in enumerate(zip(("Minor", "Major", "Ultimate"), order)):
        prev = order[i - 1] if i else None
        ok = t is not None and (prev is None or (t.get("Count", 1) > prev.get("Count", 1)
                                                  and t.get("Shake", 0) > prev.get("Shake", 0)
                                                  and t.get("HitStop", 0) > prev.get("HitStop", 0)))
        if not ok:
            t_fail += 1; t_worst.append((f"tier {name} missing or not above the one below", 1))
    play = "".join(open(p, encoding="utf-8", errors="replace").read() for p in VFX_FILES if os.path.exists(p))
    wired = "TierOf(" in play and "HitStop" in play
    if not wired:
        t_fail += 1; t_worst.append(("hit playback does not read the tier", 1))
    return {
        "POL-E4": {"kind": "coverage", "checked": checked4, "failed": failed,
                   "score": round(100.0 * (checked4 - failed) / checked4, 1) if checked4 else None, "worst": worst[:5]},
        "POL-E5": {"kind": "coverage", "checked": 4, "failed": t_fail,
                   "score": round(100.0 * (4 - t_fail) / 4, 1), "worst": t_worst},
    }


SCREENS_DIR = os.path.join(APP, "Screens")
CLASS_DECL = re.compile(r"^    (?:\w+\s+)*class\s+(\w+)", re.M)  # top-level classes only: a nested one is its screen's
KIT_HEADER = re.compile(r"\bUi\.Text\([^;]*?,\s*\"(?:title|h1)\b[^\"]*\"|\bUi\.El\(\s*\"header\b")
KIT_WINDOW = re.compile(r"\bUi\.Window\(")
KIT_TAP = re.compile(r"(?:\b(\w+)\s*=\s*)?(\bUi\.Button|\bUi\.MenuRow|\bnew\s+Button)\(")
KIT_SPACE = re.compile(r"\bstyle\.(?:margin|padding)\w*\s*=(?!=)")


def kit_hits():
    """POL-K1..K4 hits in App/Screens: (rule, screen class, file, line, what)."""
    hits, screens = [], []
    for p in sorted(os.listdir(SCREENS_DIR)):
        if not p.endswith(".cs"):
            continue
        rel = os.path.relpath(os.path.join(SCREENS_DIR, p), ROOT).replace("\\", "/")
        text = strip_comments(open(os.path.join(SCREENS_DIR, p), encoding="utf-8", errors="replace").read())
        decls = [(m.start(), m.group(1)) for m in CLASS_DECL.finditer(text)]
        screens += [n for _, n in decls]
        ends = [s for s, _ in decls[1:]] + [len(text)]

        def span(pos):
            return next(((n, s, e) for (s, n), e in zip(decls, ends) if s <= pos < e), ("(file)", 0, len(text)))

        def add(rule, pos, what):
            hits.append((rule, span(pos)[0], rel, text.count("\n", 0, pos) + 1, what))

        for m in KIT_HEADER.finditer(text):
            add("POL-K1", m.start(), "own page title")
        for m in KIT_WINDOW.finditer(text):
            add("POL-K2", m.start(), "raw Ui.Window")
        for m in KIT_TAP.finditer(text):
            # iconed when the statement itself, or a later use of the variable it is assigned to, adds a KindIcon
            region = text[m.start():span(m.start())[2]]
            var = m.group(1)
            iconed = "KindIcon(" in region[:region.find(";")] or (
                var and re.search(rf"\b{var}\.KindIcon\(|KindIcon\(\s*{var}\b", region))
            if not iconed:
                add("POL-K3", m.start(), re.sub(r"\s+", " ", m.group(2)) + " without KindIcon")
        for m in KIT_SPACE.finditer(text):
            add("POL-K4", m.start(), "inline " + text[m.start() + 6:m.end() - 1].strip())
    return hits, screens


def kit_rules():
    hits, screens = kit_hits()
    out = {}
    for rule in ("POL-K1", "POL-K2", "POL-K3", "POL-K4"):
        per = Counter(owner for r, owner, *_ in hits if r == rule)
        out[rule] = {"kind": "kit", "checked": len(screens), "failed": len(per),
                     "score": round(100.0 * (len(screens) - len(per)) / len(screens), 1) if screens else None,
                     "hits": sum(per.values()), "worst": per.most_common(5)}
    return out


def main():
    if "--hits" in sys.argv:
        for rule, owner, rel, line, what in kit_hits()[0]:
            print(f"{rule} {owner:24} {rel}:{line}  {what}")
        return
    res = lint()
    if "--json" in sys.argv:
        print(json.dumps(res, indent=1))
        return
    print(f"{'rule':8} {'score':>6} {'fail':>5}/{'chk':<5} worst")
    for rule, r in res.items():
        worst = ", ".join(f"{os.path.basename(f)}:{n}" for f, n in r["worst"][:3])
        print(f"{rule:8} {str(r['score']):>6} {r['failed']:>5}/{r['checked']:<5} {worst}")


if __name__ == "__main__":
    main()
