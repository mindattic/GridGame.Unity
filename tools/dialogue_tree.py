#!/usr/bin/env python3
"""GG-T1295: author a conversation in one readable file, Data/Dialogue/<id>.tree (docs/PROSE.md, BIBLE section 27.1).

    python tools/dialogue_tree.py compile [TREE ...] [--drafts DIR]   tree -> its dialogues.json entry (merged by Id; the
                                                     rest of the file is left as it is) + its words as SEED texts in
                                                     Content/Text + prose-text/1 drafts in DIR for Prose
    python tools/dialogue_tree.py decompile ID [ID ...] [--out Data/Dialogue]
                                                     a dialogues.json entry and its texts back to <out>/<id>.tree
    python tools/dialogue_tree.py lint [TREE ...]    unreachable nodes, dangling next, stranded choices, effects on a
                                                     loop, the Trust / cohesion range each path reaches, orphaned
                                                     Prose dialogue keys

With no TREE, every Data/Dialogue/*.tree. Compile lints first and refuses a tree with an error.

The format (indentation is two spaces; '#' starts a comment line):

    dialogue talk_quiz_knight
    title: Advice
    trigger: Talk knight                 Kind [Id] [Other] [level=N]
    when: QuestAvailable q_bridge         a dialogue condition (one per line)
    once                                  Talk only: play once
    backdrop: desert
    crawl: A paragraph of the opening crawl (one per line; any crawl line sets HasCrawl)
    speaker hero $with                    Id Class [name="Display name"] [portrait=class]; Class "-" for none
    start: ask                            defaults to the first node

    node ask hero                         Id Speaker
      The line, wrapped over as many
      lines as it needs.
      do: AddTrust $with 5                an effect when the node is reached
      -> next                             auto-advance (no choices); none ends the dialogue
      * A reply -> right                  a choice: label, then where it goes (no arrow ends the dialogue)
        if: TrustAtLeast $with 20         a condition for offering it
        do: GrantSkillPoint $with         an effect of taking it
        wit: 1                            Wanderer talks only

A condition or effect is `Type [Id] [Other] [Amount] [role=Role]`: the first word is Id, a second Other (the pair's
second hero for AddCohesion / CohesionAtLeast), an integer Amount. Words are the game's (DialogueRunner, ContentModels);
the words a node says and a choice's label go to Prose, never to dialogues.json.

Standard library only.
"""

from __future__ import annotations

import argparse
import glob
import json
import os
import re
import shlex
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import prose_keys as K  # noqa: E402
import prose_seed  # noqa: E402
import prose_text  # noqa: E402

TREE_DIR = os.path.join("Data", "Dialogue")
DIALOGUES = os.path.join(K.CONTENT_DIR, "dialogues.json")
UNIVERSE = os.path.join("Data", "Prose", "gridgame.universe.json")
DRAFTS = os.path.join("Data", "Prose", "drafts", "dialogue")

# ContentModels.cs: DialogueTriggerKind, DialogueConditionType, DialogueEffectType.
TRIGGERS = {"ThemeStart", "StageCleared", "Talk", "Recruit", "LevelReached", "StageStart", "Wanderer",
            "Trust", "Cohesion", "Founding", "Joined"}
CONDITIONS = {"FlagSet", "FlagNotSet", "HasItem", "GoldAtLeast", "PartyHas", "RosterHas", "RosterLacks", "NpcJoined",
              "QuestAvailable", "QuestActive", "QuestDone", "TrustAtLeast", "CohesionAtLeast"}
EFFECTS = {"SetFlag", "ClearFlag", "GrantGold", "GrantItem", "RecruitHero", "JoinNpc", "JoinHero", "GrantSkillPoint",
           "StartQuest", "OpenStation", "AddTrust", "AddCohesion"}
# Effects that would pay again on every lap of a loop (Trust, cohesion and skill points pay once per save).
FARMABLE = {"GrantGold", "GrantItem"}
OUTCOME, JOIN, PART = "$outcome", "join", "part"  # DialogueDef.OutcomeNext / JoinNode / PartNode
MAX_PATHS = 20000


class TreeError(Exception):
    pass


# ------------------------------------------------------------------------------------------------ parse

def _words(s: str, where: str) -> list:
    try:
        return shlex.split(s)
    except ValueError as e:
        raise TreeError(f"{where}: {e}")


def parse_op(s: str, kinds: set, where: str) -> dict:
    """`Type [Id] [Other] [Amount] [role=Role]` as a DialogueCondition / DialogueEffect dict."""
    w = _words(s, where)
    if not w or w[0] not in kinds:
        raise TreeError(f"{where}: unknown {w[0] if w else '(empty)'!r}")
    op, names = {"Type": w[0]}, []
    for x in w[1:]:
        if x.startswith("role="):
            op["Role"] = x[5:]
        elif re.fullmatch(r"[+-]?\d+", x):
            op["Amount"] = int(x)
        else:
            names.append(x)
    if len(names) > 2:
        raise TreeError(f"{where}: too many ids in {s!r}")
    if names:
        op["Id"] = names[0]
    if len(names) > 1:
        op["Other"] = names[1]
    return op


def parse(text: str, path: str = "<tree>") -> dict:
    """A .tree file as {"def": the dialogues.json entry, "lines": {node: text}, "labels": {node: [label]}, "crawl": [..]}."""
    d = {"Id": None, "Title": None, "Trigger": {}, "Conditions": [], "Once": False, "Backdrop": None, "HasCrawl": False,
         "Speakers": [], "Start": None, "Nodes": []}
    lines, labels, crawl = {}, {}, []
    node = choice = None
    for i, raw in enumerate(text.replace("\r\n", "\n").split("\n"), 1):
        where = f"{path}:{i}"
        if not raw.strip() or raw.lstrip().startswith("#"):
            continue
        indent = len(raw) - len(raw.lstrip(" "))
        s = raw.strip()
        if indent == 0:
            node = choice = None
            head, _, rest = s.partition(" ")
            rest = rest.strip()
            if head == "dialogue":
                d["Id"] = rest
            elif head == "title:":
                d["Title"] = rest
            elif head == "trigger:":
                w = _words(rest, where)
                if not w or w[0] not in TRIGGERS:
                    raise TreeError(f"{where}: unknown trigger {rest!r}")
                d["Trigger"] = {"Kind": w[0]}
                names = []
                for x in w[1:]:
                    if x.startswith("level="):
                        d["Trigger"]["Level"] = int(x[6:])
                    else:
                        names.append(x)
                if len(names) > 2:
                    raise TreeError(f"{where}: too many ids in trigger {rest!r}")
                if names:
                    d["Trigger"]["Id"] = names[0]
                if len(names) > 1:
                    d["Trigger"]["Other"] = names[1]
            elif head == "when:":
                d["Conditions"].append(parse_op(rest, CONDITIONS, where))
            elif s == "once":
                d["Once"] = True
            elif head == "backdrop:":
                d["Backdrop"] = rest
            elif head == "crawl:":
                d["HasCrawl"] = True
                crawl.append(rest)
            elif head == "start:":
                d["Start"] = rest
            elif head == "speaker":
                w = _words(rest, where)
                if not w:
                    raise TreeError(f"{where}: speaker needs an id")
                spk = {"Id": w[0]}
                if len(w) > 1 and w[1] != "-":
                    spk["Class"] = w[1]
                for x in w[2:]:
                    k, eq, v = x.partition("=")
                    if not eq or k not in ("name", "portrait"):
                        raise TreeError(f"{where}: speaker takes name= and portrait=, not {x!r}")
                    spk[k.capitalize()] = v
                d["Speakers"].append(spk)
            elif head == "node":
                w = rest.split()
                if len(w) != 2:
                    raise TreeError(f"{where}: node <id> <speaker>")
                node = {"Id": w[0], "Speaker": w[1], "Effects": [], "Next": None, "Choices": []}
                d["Nodes"].append(node)
                lines[w[0]], labels[w[0]] = [], []
            else:
                raise TreeError(f"{where}: unknown line {s!r}")
            continue
        if node is None:
            raise TreeError(f"{where}: indented line outside a node")
        if choice is not None and indent > 2 and (s.startswith("if:") or s.startswith("do:") or s.startswith("wit:")):
            if s.startswith("if:"):
                choice["Conditions"].append(parse_op(s[3:], CONDITIONS, where))
            elif s.startswith("do:"):
                choice["Effects"].append(parse_op(s[3:], EFFECTS, where))
            else:
                choice["Wit"] = int(s[4:])
            continue
        choice = None
        if s.startswith("* ") or s == "*":
            label, arrow, nxt = s[1:].strip().rpartition("->") if "->" in s else (s[1:].strip(), "", "")
            choice = {"Next": nxt.strip() or None, "Wit": 0, "Conditions": [], "Effects": []}
            node["Choices"].append(choice)
            labels[node["Id"]].append(label.strip())
        elif s.startswith("->"):
            node["Next"] = s[2:].strip() or None
        elif s.startswith("do:"):
            node["Effects"].append(parse_op(s[3:], EFFECTS, where))
        else:
            lines[node["Id"]].append(s)
    if not d["Id"]:
        raise TreeError(f"{path}: no `dialogue <id>` line")
    if not d["Trigger"]:
        raise TreeError(f"{path}: no trigger")
    return {"def": clean(d), "lines": {k: " ".join(v) for k, v in lines.items()}, "labels": labels, "crawl": crawl}


def clean(d: dict) -> dict:
    """The entry in dialogues.json's own key order, without empty or default fields."""
    def op(o, order=("Type", "Id", "Other", "Amount", "Role")):
        return {k: o[k] for k in order if o.get(k) not in (None, "")}

    out = {"Id": d["Id"], "Title": d.get("Title") or d["Id"], "Trigger": op(d["Trigger"], ("Kind", "Id", "Other", "Level"))}
    if d.get("Conditions"):
        out["Conditions"] = [op(c) for c in d["Conditions"]]
    for k in ("Once", "Backdrop", "HasCrawl"):
        if d.get(k):
            out[k] = d[k]
    out["Speakers"] = [{k: s[k] for k in ("Id", "Class", "Name", "Portrait") if s.get(k)} for s in d["Speakers"]]
    if d.get("Start"):
        out["Start"] = d["Start"]
    nodes = []
    for n in d["Nodes"]:
        m = {"Id": n["Id"], "Speaker": n["Speaker"]}
        if n.get("Effects"):
            m["Effects"] = [op(e) for e in n["Effects"]]
        if n.get("Next"):
            m["Next"] = n["Next"]
        if n.get("Choices"):
            cs = []
            for c in n["Choices"]:
                x = {}
                if c.get("Next"):
                    x["Next"] = c["Next"]
                if c.get("Wit") or d["Trigger"].get("Kind") == "Wanderer" and "Wit" in c:
                    x["Wit"] = c.get("Wit") or 0
                if c.get("Conditions"):
                    x["Conditions"] = [op(o) for o in c["Conditions"]]
                if c.get("Effects"):
                    x["Effects"] = [op(o) for o in c["Effects"]]
                cs.append(x)
            m["Choices"] = cs
        nodes.append(m)
    out["Nodes"] = nodes
    return out


# ------------------------------------------------------------------------------------------------ dialogues.json

def _inline(v) -> str:
    """dialogues.json's compact style: { "A": 1, "B": [ { ... } ] }."""
    if isinstance(v, dict):
        return "{ " + ", ".join(f"{json.dumps(k)}: {_inline(x)}" for k, x in v.items()) + " }" if v else "{}"
    if isinstance(v, list):
        return "[ " + ", ".join(_inline(x) for x in v) + " ]" if v else "[]"
    return json.dumps(v, ensure_ascii=False)


def render_entry(d: dict) -> str:
    """One dialogues.json entry, indented as the file is (two spaces, no trailing comma)."""
    rows = []
    for k, v in d.items():
        if k == "Speakers":
            rows.append('    "Speakers": [\n' + ",\n".join("      " + _inline(s) for s in v) + "\n    ]")
        elif k == "Nodes":
            parts = []
            for n in v:
                if not n.get("Choices"):
                    parts.append("      " + _inline(n))
                    continue
                head = ", ".join(f"{json.dumps(a)}: {_inline(b)}" for a, b in n.items() if a != "Choices")
                parts.append("      {\n        " + head + ",\n        \"Choices\": [\n"
                             + ",\n".join("          " + _inline(c) for c in n["Choices"]) + "\n        ]\n      }")
            rows.append('    "Nodes": [\n' + ",\n".join(parts) + "\n    ]")
        else:
            rows.append(f"    {json.dumps(k)}: {_inline(v)}")
    return "  {\n" + ",\n".join(rows) + "\n  }"


def merge_entry(text: str, entry: dict) -> str:
    """dialogues.json's text with this entry replacing the one of its Id (or appended); every other byte is kept."""
    lines = text.split("\n")
    start = end = None
    for i in range(1, len(lines)):
        if lines[i - 1] == "  {" and lines[i].strip() == f'"Id": {json.dumps(entry["Id"])},':
            start = i - 1
            for j in range(i, len(lines)):
                if lines[j] in ("  }", "  },"):
                    end = j
                    break
            break
    block = render_entry(entry)
    if start is not None and end is not None:
        comma = "," if lines[end].endswith(",") else ""
        lines[start:end + 1] = (block + comma).split("\n")
    else:
        close = max(i for i, l in enumerate(lines) if l.strip() == "]")
        prev = max(i for i in range(close) if lines[i].strip())
        if lines[prev] == "  }":
            lines[prev] = "  },"
        lines[close:close] = block.split("\n")
    out = "\n".join(lines)
    got = [x for x in json.loads(out) if x.get("Id") == entry["Id"]]
    if got != [entry]:
        raise TreeError(f"dialogues.json: merging {entry['Id']} did not round-trip")
    return out


# ------------------------------------------------------------------------------------------------ lint

def _targets(n: dict) -> list:
    out = [n["Next"]] if n.get("Next") else []
    out += [c["Next"] for c in n.get("Choices") or [] if c.get("Next")]
    return [t for x in out for t in ((JOIN, PART) if x == OUTCOME else (x,))]


def _sccs(nodes: dict) -> list:
    """Tarjan: the node sets that form a loop (a cycle of two or more, or a node that leads to itself)."""
    index, low, stack, on, out, n = {}, {}, [], set(), [], [0]

    def visit(v):
        index[v] = low[v] = n[0]
        n[0] += 1
        stack.append(v)
        on.add(v)
        for w in _targets(nodes[v]):
            if w not in nodes:
                continue
            if w not in index:
                visit(w)
                low[v] = min(low[v], low[w])
            elif w in on:
                low[v] = min(low[v], index[w])
        if low[v] == index[v]:
            comp = set()
            while True:
                w = stack.pop()
                on.discard(w)
                comp.add(w)
                if w == v:
                    break
            if len(comp) > 1 or v in _targets(nodes[v]):
                out.append(comp)

    sys.setrecursionlimit(max(1000, 4 * len(nodes) + 100))
    for v in nodes:
        if v not in index:
            visit(v)
    return out


def _bond(e: dict):
    if e.get("Type") == "AddTrust":
        return "trust " + (e.get("Id") or "?")
    if e.get("Type") == "AddCohesion":
        return "cohesion " + "+".join(sorted([e.get("Id") or "?", e.get("Other") or "?"]))
    return None


def ranges(d: dict) -> tuple:
    """Every path from the start (a loop is walked once) -> ({'trust hero' | 'cohesion a+b': (min, max)}, paths, capped)."""
    nodes = {n["Id"]: n for n in d["Nodes"]}
    start = d.get("Start") or (d["Nodes"][0]["Id"] if d["Nodes"] else None)
    totals, paths, capped = [], [0], [False]

    def add(acc, effects):
        acc = dict(acc)
        for e in effects or []:
            k = _bond(e)
            if k:
                acc[k] = acc.get(k, 0) + (e.get("Amount") or 0)
        return acc

    def walk(nid, seen, acc):
        if paths[0] >= MAX_PATHS:
            capped[0] = True
            return
        n = nodes.get(nid)
        if n is None or nid in seen:
            totals.append(acc)
            paths[0] += 1
            return
        seen = seen | {nid}
        acc = add(acc, n.get("Effects"))
        steps = [(c.get("Next"), c.get("Effects")) for c in n.get("Choices") or []] or [(n.get("Next"), None)]
        for nxt, eff in steps:
            a = add(acc, eff)
            if not nxt:
                totals.append(a)
                paths[0] += 1
            else:
                for t in ((JOIN, PART) if nxt == OUTCOME else (nxt,)):
                    walk(t, seen, a)

    if start:
        walk(start, frozenset(), {})
    keys = sorted({k for t in totals for k in t})
    return {k: (min(t.get(k, 0) for t in totals), max(t.get(k, 0) for t in totals)) for k in keys}, paths[0], capped[0]


def lint_def(d: dict) -> tuple:
    """(errors, warnings, notes) for one dialogue entry."""
    errs, warns, notes = [], [], []
    nodes = {}
    for n in d["Nodes"]:
        if n["Id"] in nodes:
            errs.append(f"node {n['Id']}: defined twice")
        nodes[n["Id"]] = n
    t = d.get("Trigger") or {}
    if t.get("Kind") in ("Trust", "Cohesion") and not (t.get("Id") and 1 <= (t.get("Level") or 0) <= 5):
        errs.append(f"trigger {t['Kind']}: needs a hero Id and level=1..5")
    if t.get("Kind") == "Cohesion" and not t.get("Other"):
        errs.append("trigger Cohesion: needs the pair's second hero (Other)")
    if t.get("Kind") == "Founding" and t.get("Id"):
        errs.append("trigger Founding: takes no Id")
    spk = {s["Id"] for s in d["Speakers"]}
    start = d.get("Start") or (d["Nodes"][0]["Id"] if d["Nodes"] else None)
    if not nodes:
        errs.append("no nodes")
        return errs, warns, notes
    if start not in nodes:
        errs.append(f"start {start!r} is not a node")
    for n in d["Nodes"]:
        if n["Speaker"] not in spk:
            errs.append(f"node {n['Id']}: unknown speaker {n['Speaker']!r}")
        if n.get("Next") and n.get("Choices"):
            errs.append(f"node {n['Id']}: has both -> and choices")
        for t in _targets(n):
            if t not in nodes:
                errs.append(f"node {n['Id']}: goes to unknown node {t!r}")
        cs = n.get("Choices") or []
        if cs and all(c.get("Conditions") for c in cs):
            errs.append(f"node {n['Id']}: stranded choices (every choice has a condition; one must be unconditional)")
    seen, todo = set(), [start] if start in nodes else []
    while todo:
        v = todo.pop()
        if v in seen:
            continue
        seen.add(v)
        todo += [t for t in _targets(nodes[v]) if t in nodes]
    for n in d["Nodes"]:
        if n["Id"] not in seen:
            errs.append(f"node {n['Id']}: unreachable from {start}")
    for comp in _sccs(nodes):
        loop = sorted(comp)
        for v in loop:
            n = nodes[v]
            inside = [(f"node {v}", e) for e in n.get("Effects") or []]
            inside += [(f"node {v} choice {i + 1}", e) for i, c in enumerate(n.get("Choices") or [])
                       if c.get("Next") in comp for e in c.get("Effects") or []]
            for at, e in inside:
                msg = f"{at}: {e['Type']} on the loop {' > '.join(loop)}"
                if e["Type"] in FARMABLE and (e.get("Amount") or 1) > 0:
                    errs.append(msg + " pays on every lap (farmable)")
                else:
                    warns.append(msg + " (pays once per save; check it reads right on a second lap)")
    if not errs:
        r, n_paths, capped = ranges(d)
        for k, (lo, hi) in r.items():
            notes.append(f"{k}: {lo:+d} to {hi:+d} over {n_paths} path(s){' (capped)' if capped else ''}")
    return errs, warns, notes


def orphans(entries: list, texts: dict) -> list:
    """Prose dialogue keys no dialogues.json entry uses, and text nodes an entry no longer has."""
    by_id = {d.get("Id"): d for d in entries}
    out = []
    for key, e in sorted(texts.items()):
        if key.startswith("dialogue:"):
            d = by_id.get(key[len("dialogue:"):])
            if d is None:
                out.append(f"{key}: no dialogues.json entry")
                continue
            have = {n.get("Id") for n in d.get("Nodes") or []}
            for tn in (e.get("payload") or {}).get("nodes") or []:
                if tn.get("id") not in have:
                    out.append(f"{key}: text node {tn.get('id')!r} is not in the dialogue")
        elif key.startswith("crawl:dialogue/"):
            d = by_id.get(key[len("crawl:dialogue/"):])
            if d is None or not d.get("HasCrawl"):
                out.append(f"{key}: no dialogue with HasCrawl")
    return out


# ------------------------------------------------------------------------------------------------ compile / decompile

def docs_for(tree: dict, known: set) -> list:
    """The prose-text/1 drafts for a tree's words (prose_seed's shapes, so a seed reads like the first seed)."""
    d = json.loads(json.dumps(tree["def"]))
    for n in d["Nodes"]:
        n["Text"] = tree["lines"].get(n["Id"]) or None
        for c, label in zip(n.get("Choices") or [], tree["labels"].get(n["Id"]) or []):
            c["Label"] = label or None
    out = [prose_seed.conversation(d, known)]
    if tree["crawl"]:
        out.append(prose_seed.text("crawl", "crawl:dialogue/" + d["Id"], {"title": d["Title"], "paragraphs": tree["crawl"]},
                                   prose_seed.trigger_about(d.get("Trigger") or {}, known)))
    return out


def _known() -> set:
    return {e["id"] for e in K.load(UNIVERSE).get("entities") or []} if os.path.isfile(UNIVERSE) else set()


def _trees(paths: list) -> list:
    return paths or sorted(glob.glob(os.path.join(TREE_DIR, "*.tree")))


def _read(path: str) -> dict:
    with open(path, encoding="utf-8") as f:
        return parse(f.read(), path)


def _report(name: str, errs, warns, notes) -> None:
    for m in errs:
        print(f"  ERROR {name}: {m}")
    for m in warns:
        print(f"  warn  {name}: {m}")
    for m in notes:
        print(f"        {name}: {m}")


def cmd_lint(args) -> int:
    bad = 0
    for path in _trees(args.trees):
        try:
            tree = _read(path)
        except TreeError as e:
            print(f"  ERROR {e}")
            bad += 1
            continue
        errs, warns, notes = lint_def(tree["def"])
        _report(tree["def"]["Id"], errs, warns, notes)
        bad += bool(errs)
    entries = K.load(DIALOGUES)
    for m in orphans(entries, prose_text.load_texts()):
        print(f"  warn  orphaned Prose key {m}")
    print(f"dialogue_tree lint: {len(_trees(args.trees))} tree(s), {bad} with errors")
    return 1 if bad else 0


def cmd_compile(args) -> int:
    trees = []
    for path in _trees(args.trees):
        try:
            tree = _read(path)
        except TreeError as e:
            print(f"  ERROR {e}")
            return 1
        errs, warns, notes = lint_def(tree["def"])
        _report(tree["def"]["Id"], errs, warns, notes)
        if errs:
            print(f"dialogue_tree compile: {path} has errors; nothing written")
            return 1
        trees.append(tree)
    with open(DIALOGUES, encoding="utf-8") as f:
        text = f.read()
    for tree in trees:
        text = merge_entry(text, tree["def"])
    with open(DIALOGUES, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    known = _known()
    docs = [doc for tree in trees for doc in docs_for(tree, known)]
    counts = prose_text.upsert_seed(docs, drafts_dir=args.drafts)
    print(f"dialogue_tree compile: {len(trees)} dialogue(s) merged into {DIALOGUES}; texts seeded {counts['seeded']}, "
          f"kept (Prose-applied) {counts['kept']}; drafts in {args.drafts}")
    return 0


def _op_text(o: dict) -> str:
    w = [o["Type"]] + [o[k] for k in ("Id", "Other") if o.get(k)]
    if o.get("Amount"):
        w.append(str(o["Amount"]))
    if o.get("Role"):
        w.append("role=" + o["Role"])
    return " ".join(shlex.quote(x) if " " in x else x for x in w)


def decompile(d: dict, texts: dict) -> str:
    h = prose_text.hydrate({"dialogues": [json.loads(json.dumps(d))]}, texts)["dialogues"][0]
    t = d.get("Trigger") or {}
    out = [f"dialogue {d['Id']}", f"title: {d.get('Title') or d['Id']}",
           "trigger: " + " ".join([t.get("Kind", "")] + [t[k] for k in ("Id", "Other") if t.get(k)]
                                  + ([f"level={t['Level']}"] if t.get("Level") else []))]
    out += [f"when: {_op_text(c)}" for c in d.get("Conditions") or []]
    if d.get("Once"):
        out.append("once")
    if d.get("Backdrop"):
        out.append(f"backdrop: {d['Backdrop']}")
    out += [f"crawl: {p}" for p in prose_seed.paragraphs(h.get("Crawl"))]
    for s in d.get("Speakers") or []:
        w = [s["Id"], s.get("Class") or "-"]
        w += [f"{k.lower()}={shlex.quote(s[k]) if ' ' in s[k] else s[k]}" for k in ("Name", "Portrait") if s.get(k)]
        out.append("speaker " + " ".join(w))
    if d.get("Start"):
        out.append(f"start: {d['Start']}")
    for n, hn in zip(d.get("Nodes") or [], h.get("Nodes") or []):
        out += ["", f"node {n['Id']} {n['Speaker']}"]
        line = hn.get("Text")
        if line and line != prose_seed.LOREM_LINE:
            out.append("  " + line)
        out += [f"  do: {_op_text(e)}" for e in n.get("Effects") or []]
        if n.get("Next"):
            out.append(f"  -> {n['Next']}")
        for c, hc in zip(n.get("Choices") or [], hn.get("Choices") or []):
            label = hc.get("Label") or ""
            if prose_seed.LOREM_CHOICE in label:
                label = ""
            out.append("  * " + " ".join(x for x in (label, f"-> {c['Next']}" if c.get("Next") else "") if x))
            out += [f"    if: {_op_text(o)}" for o in c.get("Conditions") or []]
            out += [f"    do: {_op_text(o)}" for o in c.get("Effects") or []]
            if "Wit" in c:
                out.append(f"    wit: {c['Wit']}")
    return "\n".join(out) + "\n"


def cmd_decompile(args) -> int:
    entries = {d.get("Id"): d for d in K.load(DIALOGUES)}
    texts = prose_text.load_texts()
    os.makedirs(args.out, exist_ok=True)
    for did in args.ids:
        d = entries.get(did)
        if d is None:
            print(f"  ERROR no dialogues.json entry {did!r}")
            return 1
        tree = decompile(d, texts)
        back = parse(tree, did)["def"]
        if back != clean_loaded(d):
            print(f"  ERROR {did}: the tree does not compile back to the same entry")
            return 1
        path = os.path.join(args.out, did + ".tree")
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(tree)
        print(f"dialogue_tree decompile: {path}")
    return 0


def clean_loaded(d: dict) -> dict:
    """A loaded entry in the compiler's shape (for the decompile round-trip check)."""
    e = json.loads(json.dumps(d))
    e.setdefault("Conditions", [])
    for n in e.get("Nodes") or []:
        n.setdefault("Effects", [])
        n.setdefault("Next", None)
        n.setdefault("Choices", [])
        for c in n["Choices"]:
            c.setdefault("Next", None)
            c.setdefault("Conditions", [])
            c.setdefault("Effects", [])
    return clean(e)


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="Compile, decompile and lint Data/Dialogue/<id>.tree conversations.")
    sub = ap.add_subparsers(dest="cmd", required=True)
    c = sub.add_parser("compile")
    c.add_argument("trees", nargs="*")
    c.add_argument("--drafts", default=DRAFTS)
    c.set_defaults(fn=cmd_compile)
    dc = sub.add_parser("decompile")
    dc.add_argument("ids", nargs="+")
    dc.add_argument("--out", default=TREE_DIR)
    dc.set_defaults(fn=cmd_decompile)
    li = sub.add_parser("lint")
    li.add_argument("trees", nargs="*")
    li.set_defaults(fn=cmd_lint)
    args = ap.parse_args(argv)
    return args.fn(args)


if __name__ == "__main__":
    sys.exit(main())
