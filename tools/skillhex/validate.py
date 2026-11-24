#!/usr/bin/env python3
"""Validate the skill hexagon board (skillhex.json) against its schema and the game content.

Usage:
    python tools/skillhex/validate.py [SKILLHEX] [--abilities PATH] [--classes PATH] [--schema PATH]

Defaults are relative to the repo root:
    SKILLHEX     docs/design/skill-hexagon/skillhex.json
    --schema     skillhex.schema.json next to SKILLHEX
    --abilities  Assets/GridGame/Resources/Content/abilities.json
    --classes    Assets/GridGame/Resources/Content/classes.json

Checks (see docs/design/skill-hexagon/PLAN.md):
    - schema (only when the jsonschema package is installed; otherwise a warning)
    - Stats and Families: six of each, Families run From corner k To corner k+1
    - every lattice point within Radius appears exactly once, and nothing outside it
    - Ability and Open nodes sit only where (Q - R) mod 3 == 0; Passive nodes everywhere else
    - each ability id exists in abilities.json, is not a MergedInto alias stub, and is placed at most once
    - each hero's Class exists in classes.json (null only for Status New)
    - each Recipe sums to 4; Signatures are unique
    - each Start path is on the lattice, connected through lattice adjacency, and holds its Signature's node

Prints a summary and the Open slot gap list grouped by wedge. Exits 1 when any error is found.
Standard library only, plus jsonschema when available.
"""
import argparse
import json
import math
import os
import sys
from collections import Counter, defaultdict

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
DEFAULT_HEX = os.path.join(REPO, 'docs', 'design', 'skill-hexagon', 'skillhex.json')
CONTENT = os.path.join(REPO, 'Assets', 'GridGame', 'Resources', 'Content')
STAT_IDS = ['VIT', 'STR', 'AGI', 'SPD', 'WIS', 'INT']
DIRS = [(1, 0), (0, 1), (-1, 1), (-1, 0), (0, -1), (1, -1)]
RECIPE_POINTS = 4


def load_json(path, what):
    try:
        with open(path, encoding='utf-8-sig') as f:
            return json.load(f)
    except FileNotFoundError:
        sys.exit(f'error: {what} not found: {path}')
    except json.JSONDecodeError as e:
        sys.exit(f'error: {what} is not valid JSON ({path}): {e}')


def is_slot(q, r):
    """Ability slot: (Q - R) mod 3 == 0 (Python's % is already non-negative)."""
    return (q - r) % 3 == 0


def lattice(n):
    return {(q, r) for q in range(-n, n + 1) for r in range(-n, n + 1) if abs(q + r) <= n}


def wedge(q, r):
    """Wedge index k (family k runs from corner k to corner k+1), using the page's geometry:
    axial -> pixel, rotated so corner 0 points up, clockwise. None for the centre point."""
    if q == 0 and r == 0:
        return None
    x0, y0 = q + r / 2, r * math.sqrt(3) / 2
    x, y = y0, -x0  # rotate by -90 degrees, as app.js does
    ang = (math.degrees(math.atan2(y, x)) + 90 + 360) % 360
    return int(ang // 60) % 6


class Report:
    def __init__(self):
        self.errors, self.warnings = [], []

    def error(self, msg):
        self.errors.append(msg)

    def warn(self, msg):
        self.warnings.append(msg)


def check_schema(doc, schema_path, rep):
    try:
        import jsonschema
    except ImportError:
        rep.warn('jsonschema is not installed: schema check skipped (pip install jsonschema)')
        return
    if not os.path.exists(schema_path):
        rep.warn(f'schema not found, schema check skipped: {schema_path}')
        return
    schema = load_json(schema_path, 'schema')
    cls = jsonschema.validators.validator_for(schema)
    for e in sorted(cls(schema).iter_errors(doc), key=lambda e: list(e.absolute_path)):
        where = '/'.join(str(p) for p in e.absolute_path) or '(root)'
        rep.error(f'schema: {where}: {e.message}')


def check_stats_families(doc, rep):
    stats = [s.get('Id') for s in doc.get('Stats') or [] if isinstance(s, dict)]
    if len(stats) != 6:
        rep.error(f'Stats: expected 6, found {len(stats)}')
    if len(set(stats)) != len(stats):
        rep.error(f'Stats: duplicate ids {sorted(k for k, v in Counter(stats).items() if v > 1)}')
    for s in stats:
        if s not in STAT_IDS:
            rep.error(f'Stats: unknown stat {s!r}')
    fams = [f for f in doc.get('Families') or [] if isinstance(f, dict)]
    if len(fams) != 6:
        rep.error(f'Families: expected 6, found {len(fams)}')
    ids = [f.get('Id') for f in fams]
    if len(set(ids)) != len(ids):
        rep.error(f'Families: duplicate ids {sorted(k for k, v in Counter(ids).items() if v > 1)}')
    if len(stats) == 6:
        for k, f in enumerate(fams[:6]):
            want_from, want_to = stats[k], stats[(k + 1) % 6]
            if f.get('From') != want_from or f.get('To') != want_to:
                rep.error(f'Families[{k}] {f.get("Id")}: runs {f.get("From")}->{f.get("To")}, '
                          f'corner order needs {want_from}->{want_to}')
    return stats, fams


def check_nodes(doc, abilities, stats, rep):
    n = doc.get('Radius')
    if not isinstance(n, int) or n < 2:
        rep.error(f'Radius: expected an integer >= 2, found {n!r}')
        return {}, set(), {}
    points = lattice(n)
    at = {}
    seen = Counter()
    placed = defaultdict(list)
    for i, node in enumerate(doc.get('Nodes') or []):
        if not isinstance(node, dict) or not isinstance(node.get('Q'), int) or not isinstance(node.get('R'), int):
            rep.error(f'Nodes[{i}]: needs integer Q and R')
            continue
        p = (node['Q'], node['R'])
        seen[p] += 1
        if p not in points:
            rep.error(f'node {p}: outside the radius-{n} lattice')
            continue
        at[p] = node
        kind = node.get('Kind')
        slot = is_slot(*p)
        if kind in ('Ability', 'Open') and not slot:
            rep.error(f'node {p}: {kind} on a passive point ((Q-R) mod 3 != 0)')
        elif kind == 'Passive' and slot:
            rep.error(f'node {p}: Passive on an ability slot ((Q-R) mod 3 == 0)')
        elif kind not in ('Ability', 'Open', 'Passive'):
            rep.error(f'node {p}: unknown Kind {kind!r}')
        if kind == 'Ability':
            aid = node.get('Ability')
            if not aid:
                rep.error(f'node {p}: Ability node without an Ability id')
            else:
                placed[aid].append(p)
                a = abilities.get(aid)
                if a is None:
                    rep.error(f'node {p}: ability {aid!r} is not in abilities.json')
                elif a.get('MergedInto'):
                    rep.error(f'node {p}: ability {aid!r} is an alias stub (MergedInto {a["MergedInto"]!r}); '
                              f'place {a["MergedInto"]!r} instead')
        elif 'Ability' in node:
            rep.error(f'node {p}: Ability id on a {kind} node')
        if kind == 'Passive':
            st = node.get('Stats')
            if not isinstance(st, dict) or not st:
                rep.error(f'node {p}: Passive without Stats')
            else:
                for s, v in st.items():
                    if s not in stats:
                        rep.error(f'node {p}: passive stat {s!r} is not one of the six Stats')
                    if not isinstance(v, int) or v < 1:
                        rep.error(f'node {p}: passive {s} value {v!r} must be a positive integer')
        elif 'Stats' in node:
            rep.error(f'node {p}: Stats on a {kind} node')
    for p, c in sorted(seen.items()):
        if c > 1:
            rep.error(f'node {p}: listed {c} times')
    missing = sorted(points - set(seen))
    if missing:
        more = f' (+{len(missing) - 20} more)' if len(missing) > 20 else ''
        rep.error(f'{len(missing)} lattice point(s) missing: {missing[:20]}{more}')
    for aid, ps in sorted(placed.items()):
        if len(ps) > 1:
            rep.error(f'ability {aid!r} placed {len(ps)} times: {ps}')
    return at, points, placed


def connected(cells):
    cells = set(cells)
    if not cells:
        return True, []
    start = next(iter(cells))
    seen, stack = {start}, [start]
    while stack:
        q, r = stack.pop()
        for dq, dr in DIRS:
            nb = (q + dq, r + dr)
            if nb in cells and nb not in seen:
                seen.add(nb)
                stack.append(nb)
    return len(seen) == len(cells), sorted(cells - seen)


def check_heroes(doc, abilities, classes, points, placed, rep):
    heroes = [h for h in doc.get('Heroes') or [] if isinstance(h, dict)]
    by_class = Counter(h.get('Class') for h in heroes if h.get('Class'))
    for c, k in by_class.items():
        if k > 1:
            rep.error(f'hero class {c!r} listed {k} times')
    sigs = defaultdict(list)
    for h in heroes:
        cls, name, status = h.get('Class'), h.get('Name') or '?', h.get('Status')
        tag = f'hero {cls or name!r}'
        if cls is None:
            if status != 'New':
                rep.error(f'{tag}: Class is null but Status is {status!r} (only New heroes may lack a class)')
        elif cls not in classes:
            rep.error(f'{tag}: class {cls!r} is not in classes.json')
        recipe = h.get('Recipe')
        if recipe is None:
            if status != 'Cut':
                rep.error(f'{tag}: {status} hero without a Recipe')
        elif isinstance(recipe, dict):
            total = sum(v for v in recipe.values() if isinstance(v, int))
            if total != RECIPE_POINTS:
                rep.error(f'{tag}: Recipe {recipe} sums to {total}, not {RECIPE_POINTS}')
            for s in recipe:
                if s not in STAT_IDS:
                    rep.error(f'{tag}: Recipe stat {s!r} is not one of the six Stats')
        sig = h.get('Signature')
        start = h.get('Start') or []
        cells = []
        for c in start:
            if isinstance(c, list) and len(c) == 2 and all(isinstance(v, int) for v in c):
                cells.append(tuple(c))
            else:
                rep.error(f'{tag}: bad Start entry {c!r}')
        dup = sorted(p for p, k in Counter(cells).items() if k > 1)
        if dup:
            rep.warn(f'{tag}: Start lists {dup} more than once')
        off = sorted(p for p in set(cells) if p not in points)
        if off:
            rep.error(f'{tag}: Start points outside the lattice: {off}')
        if not cells and status != 'New':
            rep.error(f'{tag}: {status} hero with an empty Start')
        ok, stray = connected(set(cells) - set(off))
        if not ok:
            rep.error(f'{tag}: Start is not connected; cut off from the rest: {stray}')
        if sig is None:
            if status != 'New':
                rep.error(f'{tag}: {status} hero without a Signature')
        else:
            sigs[sig].append(cls or name)
            if sig not in abilities:
                rep.error(f'{tag}: Signature {sig!r} is not in abilities.json')
            nodes = placed.get(sig)
            if not nodes:
                rep.error(f'{tag}: Signature {sig!r} is not placed on the board')
            elif not any(p in set(cells) for p in nodes):
                rep.error(f'{tag}: Start does not hold its Signature {sig!r} at {nodes[0]}')
    for sig, owners in sorted(sigs.items()):
        if len(owners) > 1:
            rep.error(f'Signature {sig!r} shared by {owners}')
    return heroes


def main(argv=None):
    ap = argparse.ArgumentParser(description='Validate the skill hexagon board (skillhex.json).')
    ap.add_argument('skillhex', nargs='?', default=DEFAULT_HEX)
    ap.add_argument('--schema', default=None, help='default: skillhex.schema.json next to the board file')
    ap.add_argument('--abilities', default=os.path.join(CONTENT, 'abilities.json'))
    ap.add_argument('--classes', default=os.path.join(CONTENT, 'classes.json'))
    args = ap.parse_args(argv)
    schema_path = args.schema or os.path.join(os.path.dirname(os.path.abspath(args.skillhex)), 'skillhex.schema.json')

    doc = load_json(args.skillhex, 'skillhex')
    abilities = {a['Id']: a for a in load_json(args.abilities, 'abilities.json') if isinstance(a, dict) and 'Id' in a}
    classes = {c['Id']: c for c in load_json(args.classes, 'classes.json') if isinstance(c, dict) and 'Id' in c}

    rep = Report()
    check_schema(doc, schema_path, rep)
    stats, fams = check_stats_families(doc, rep)
    at, points, placed = check_nodes(doc, abilities, stats or STAT_IDS, rep)
    heroes = check_heroes(doc, abilities, classes, points, placed, rep)

    # Summary
    kinds = Counter(n.get('Kind') for n in at.values())
    slots = sum(1 for p in points if is_slot(*p))
    live = [i for i, a in abilities.items() if not a.get('MergedInto')]
    unplaced = sorted(i for i in live if i not in placed)
    status = Counter(h.get('Status') for h in heroes)
    print(f'skillhex: {os.path.relpath(args.skillhex, REPO)}')
    print(f'  lattice radius {doc.get("Radius")}: {len(points)} points, {slots} ability slots, {len(points) - slots} passive points')
    print(f'  nodes: {kinds.get("Ability", 0)} Ability, {kinds.get("Open", 0)} Open, {kinds.get("Passive", 0)} Passive')
    print(f'  abilities: {len(placed)} placed of {len(live)} in abilities.json ({len(unplaced)} not on the board)')
    print('  heroes: ' + ', '.join(f'{status.get(s, 0)} {s}' for s in ('Placed', 'Cut', 'New')))
    if unplaced:
        print(f'  not on the board: {", ".join(unplaced)}')

    # Open slot gap list, grouped by wedge
    fam_names = [f.get('Name') or f.get('Id') for f in fams] if len(fams) == 6 else [f'wedge {k}' for k in range(6)]
    groups = defaultdict(list)
    for p, node in at.items():
        if node.get('Kind') == 'Open':
            groups[wedge(*p)].append((p, node.get('Note')))
    print(f'\nOpen slots ({sum(len(v) for v in groups.values())}):')
    order = [k for k in range(6)] + [None]
    for k in order:
        if k not in groups:
            continue
        if k is None:
            label = 'Centre'
        else:
            st = stats if len(stats) == 6 else STAT_IDS
            label = f'{fam_names[k]} ({st[k]}->{st[(k + 1) % 6]})'
        items = sorted(groups[k], key=lambda t: (max(abs(t[0][0]), abs(t[0][1]), abs(t[0][0] + t[0][1])), t[0]))
        print(f'  {label}: {len(items)}')
        for (q, r), note in items:
            ring = max(abs(q), abs(r), abs(q + r))
            print(f'    [{q}, {r}] ring {ring}' + (f'  {note}' if note else ''))

    for w in rep.warnings:
        print(f'warning: {w}')
    for e in rep.errors:
        print(f'error: {e}')
    print(f'\n{len(rep.errors)} error(s), {len(rep.warnings)} warning(s)')
    return 1 if rep.errors else 0


if __name__ == '__main__':
    sys.exit(main())
