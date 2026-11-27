#!/usr/bin/env python3
"""Ability cadence: the level at which each hero class gains each new ability on the skill hexagon.

Usage:
    python tools/skillhex/cadence.py [SKILLHEX] [--abilities PATH] [--classes PATH]
                                     [--max-level N] [--gap N] [--seed N] [--out DIR]

Each hero class on the board starts on its Start path at level 1 (every ability on it is known) and gains 1 SP a level
(SkillHexProgress). After each level-up it spends its SP the way BoardBot.Spend does for a party of that one hero: pick
the target ability with the best value per planner cost (BoardBot.PickTarget: Importance / (1 + abilities covering the
need), plus BaseValue), walk the board's cheapest route there (SkillHexBoard.CheapestRoute with the planner's
pass-through weight) one node at a time, and bank what it can't spend. --seed only breaks ties between equal picks, as
in the game.

Writes DIR/data.js (window.ABILITY_CADENCE) for the page at docs/design/ability-cadence/index.html, and prints every
gap longer than --gap levels between two new abilities (counted from level 1, and up to --max-level after the last one).
Static: reads skillhex.json, abilities.json and classes.json only. Standard library only.
"""
import argparse
import heapq
import json
import os
import sys

REPO = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..'))
CONTENT = os.path.join(REPO, 'Assets', 'GridGame', 'Resources', 'Content')
DEFAULT_HEX = os.path.join(CONTENT, 'skillhex.json')
DEFAULT_OUT = os.path.join(REPO, 'docs', 'design', 'ability-cadence')
DIRS = [(1, 0), (0, 1), (-1, 1), (-1, 0), (0, -1), (1, -1)]

# BoardBot.Importance and BoardBot.BaseValue.
IMPORTANCE = {'Heal': 3.0, 'Damage': 2.0, 'Area': 2.0, 'Cleanse': 1.5, 'Support': 1.5, 'Control': 1.5}
BASE_VALUE = 0.5
EPS = 1e-6
NO_TARGET = None


def load_json(path, what):
    try:
        with open(path, encoding='utf-8-sig') as f:
            return json.load(f)
    except FileNotFoundError:
        sys.exit(f'error: {what} not found: {path}')
    except json.JSONDecodeError as e:
        sys.exit(f'error: {what} is not valid JSON ({path}): {e}')


def covers(a):
    """BoardBot.Covers: the needs one ability covers, in the same order (duplicates kept, as there)."""
    if a is None:
        return []
    shape = a.get('Shape') or 'SingleActor'
    filt = a.get('Filter') or 'EnemyOnly'
    buff = a.get('ApplyBuff')
    foes = filt == 'EnemyOnly' or shape == 'AllEnemies'
    out = []
    if (a.get('BaseHeal') or 0) > 0:
        out.append('Heal')
    if a.get('RemovesDebuffs'):
        out.append('Cleanse')
    if (a.get('BaseDamage') or 0) > 0:
        out.append('Damage')
        if shape not in ('SingleActor', 'Self', 'SingleTile'):
            out.append('Area')
    if buff and not foes:
        out.append('Support')
    if (buff and foes) or (a.get('Knockback') or 0) > 0 or (a.get('Pull') or 0) > 0 or (a.get('Pushback') or 0) > 0:
        out.append('Control')
    return out


def fnv(seed, a, b):
    """BoardBot.Hash: FNV-1a over the seed and the UTF-16 code units of two strings."""
    h = 2166136261

    def mix(v):
        nonlocal h
        h = ((h ^ (v & 0xFFFFFFFF)) * 16777619) & 0xFFFFFFFF
    mix(seed)
    for ch in _units(a):
        mix(ch)
    mix(0x1F)
    for ch in _units(b):
        mix(ch)
    return h


def _units(s):
    data = (s or '').encode('utf-16-le')
    return [data[i] | (data[i + 1] << 8) for i in range(0, len(data), 2)]


class Board:
    """The parts of SkillHexBoard the planner uses."""

    def __init__(self, doc):
        self.radius = doc['Radius']
        costs = doc.get('Costs') or {}
        self.passive = costs.get('Passive', 1)
        self.ability = costs.get('Ability', 1)
        self.pass_through = costs.get('PlannerPassThroughAbility', 4)
        self.nodes = doc.get('Nodes') or []
        self.at = {(n['Q'], n['R']): n for n in self.nodes if self.contains((n['Q'], n['R']))}

    def contains(self, p):
        q, r = p
        n = self.radius
        return abs(q) <= n and abs(r) <= n and abs(q + r) <= n

    def neighbors(self, p):
        for dq, dr in DIRS:
            nb = (p[0] + dq, p[1] + dr)
            if self.contains(nb):
                yield nb

    def takeable(self, p):
        n = self.at.get(p)
        return n is not None and n.get('Kind') in ('Ability', 'Passive')

    def price(self, p):
        n = self.at.get(p)
        if n is None:
            return 0
        return self.ability if n['Kind'] == 'Ability' else self.passive if n['Kind'] == 'Passive' else 0

    def step_weight(self, p, target, wanted):
        n = self.at.get(p)
        if n is None:
            return 0
        if n['Kind'] != 'Ability':
            return self.passive
        return self.ability if p == target or p in wanted else self.pass_through

    def dijkstra(self, owned, target, wanted):
        """Planner distances from the owned set (SkillHexBoard.CheapestRoute / BoardBot.Distances): pops in (D, Q, R)
        order like their SortedSet, stopping at the target when there is one."""
        dist, prev, heap = {}, {}, []
        for p in owned:
            if self.contains(p) and p not in dist:
                dist[p] = 0
                heap.append((0, p[0], p[1]))
        heapq.heapify(heap)
        while heap:
            d, q, r = heapq.heappop(heap)
            at = (q, r)
            if dist.get(at) != d:
                continue
            if at == target:
                break
            for nb in self.neighbors(at):
                if nb in owned or not self.takeable(nb):
                    continue
                nd = d + self.step_weight(nb, target, wanted)
                if nb in dist and nd >= dist[nb]:
                    continue
                dist[nb] = nd
                prev[nb] = at
                heapq.heappush(heap, (nd, nb[0], nb[1]))
        return dist, prev

    def cheapest_route(self, owned, target, wanted):
        if target in owned:
            return []
        if not self.takeable(target) or not owned:
            return None
        dist, prev = self.dijkstra(owned, target, wanted)
        if target not in dist:
            return None
        route, p = [], target
        while p not in owned:
            route.append(p)
            p = prev[p]
        route.reverse()
        return route


def needs_of(known, abilities):
    """BoardBot.Needs for a party of one hero knowing these abilities."""
    have = {k: 0 for k in IMPORTANCE}
    for aid in dict.fromkeys(known):
        for need in covers(abilities.get(aid)):
            have[need] += 1
    return {k: IMPORTANCE[k] / (1 + have[k]) for k in IMPORTANCE}


def value(a, needs):
    return 0.0 if a is None else BASE_VALUE + sum(needs.get(n, 0.0) for n in dict.fromkeys(covers(a)))


def wanted_of(board, owned, needs, abilities):
    """BoardBot.Wanted: unowned ability nodes covering a need the hero has none of."""
    out = set()
    for n in board.nodes:
        if n.get('Kind') != 'Ability' or not n.get('Ability'):
            continue
        p = (n['Q'], n['R'])
        if p in owned:
            continue
        if any(needs.get(k, 0) >= IMPORTANCE[k] - EPS for k in covers(abilities.get(n['Ability']))):
            out.add(p)
    return out


def pick_target(board, owned, known, cls, needs, abilities, seed):
    """BoardBot.PickTarget."""
    wanted = wanted_of(board, owned, needs, abilities)
    dist, _ = board.dijkstra(owned, NO_TARGET, wanted)
    best, best_score, best_tie = None, None, 0
    for n in board.nodes:
        if n.get('Kind') != 'Ability' or not n.get('Ability'):
            continue
        p = (n['Q'], n['R'])
        if p in owned or not board.contains(p) or n['Ability'] in known:
            continue
        a = abilities.get(n['Ability'])
        if a is None:
            continue
        reach = None
        for nb in board.neighbors(p):
            if nb in owned:
                reach = 0
                break
            if nb in dist and (reach is None or dist[nb] < reach):
                reach = dist[nb]
        if reach is None:
            continue
        score = value(a, needs) / (1.0 + reach + board.ability)
        tie = fnv(seed, cls, n['Ability'])
        if (best is None or score > best_score + EPS
                or (abs(score - best_score) <= EPS and (tie > best_tie or (tie == best_tie and p < best)))):
            best, best_score, best_tie = p, score, tie
    return best


def walk(board, hero, abilities, max_level, seed):
    """One class from its Start path to max_level, 1 SP a level, spending like BoardBot.Spend.
    Returns [(level, ability id)] in the order learned, the Start path's abilities at level 1."""
    cls = hero['Class']
    owned = set()
    for c in hero.get('Start') or []:
        if isinstance(c, list) and len(c) == 2 and board.contains(tuple(c)):
            owned.add(tuple(c))
    known, learned = [], []
    for p in sorted(owned):
        n = board.at.get(p)
        if n and n.get('Kind') == 'Ability' and n.get('Ability') and n['Ability'] not in known:
            known.append(n['Ability'])
            learned.append((1, n['Ability']))
    sp = 0
    for level in range(2, max_level + 1):
        sp += 1
        for _ in range(1000):
            if sp <= 0:
                break
            needs = needs_of(known, abilities)
            target = pick_target(board, owned, known, cls, needs, abilities, seed)
            if target is None:
                break
            route = board.cheapest_route(owned, target, wanted_of(board, owned, needs, abilities))
            if not route:
                break
            step = route[0]
            price = board.price(step)
            if price > sp:
                break
            sp -= price
            owned.add(step)
            n = board.at[step]
            if n['Kind'] == 'Ability' and n['Ability'] not in known:
                known.append(n['Ability'])
                learned.append((level, n['Ability']))
    return learned


def gaps(learned, max_level, limit):
    """Stretches longer than `limit` levels with no new ability: (from, to, length, closed). From level 1, between
    each pair of new levels, and from the last one to max_level (open: nothing new by then)."""
    levels = sorted({lv for lv, _ in learned} | {1})
    out = []
    for a, b in zip(levels, levels[1:]):
        if b - a > limit:
            out.append((a, b, b - a, True))
    if max_level - levels[-1] > limit:
        out.append((levels[-1], max_level, max_level - levels[-1], False))
    return out


def main(argv=None):
    ap = argparse.ArgumentParser(description='Ability cadence of every hero class on the skill hexagon.')
    ap.add_argument('skillhex', nargs='?', default=DEFAULT_HEX)
    ap.add_argument('--abilities', default=os.path.join(CONTENT, 'abilities.json'))
    ap.add_argument('--classes', default=os.path.join(CONTENT, 'classes.json'))
    ap.add_argument('--max-level', type=int, default=40, help='last level walked (default 40)')
    ap.add_argument('--gap', type=int, default=4, help='flag stretches longer than this many levels (default 4)')
    ap.add_argument('--seed', type=int, default=0, help='tie-break seed, as BoardBot.Spend takes (default 0)')
    ap.add_argument('--out', default=DEFAULT_OUT, help='folder for data.js (default docs/design/ability-cadence)')
    args = ap.parse_args(argv)

    doc = load_json(args.skillhex, 'skillhex')
    abilities = {a['Id']: a for a in load_json(args.abilities, 'abilities.json') if isinstance(a, dict) and 'Id' in a}
    classes = {c['Id']: c for c in load_json(args.classes, 'classes.json') if isinstance(c, dict) and 'Id' in c}
    board = Board(doc)

    rows = []
    for h in doc.get('Heroes') or []:
        if not h.get('Class') or not h.get('Start'):
            continue
        learned = walk(board, h, abilities, args.max_level, args.seed)
        gs = gaps(learned, args.max_level, args.gap)
        cls = classes.get(h['Class']) or {}
        rows.append({
            'Class': h['Class'],
            'Name': cls.get('Name') or h.get('Name') or h['Class'],
            'Recipe': h.get('Recipe') or {},
            'Status': h.get('Status'),
            'Signature': h.get('Signature'),
            'Abilities': [{'Level': lv, 'Id': aid, 'Name': (abilities.get(aid) or {}).get('Name') or aid,
                           'Covers': list(dict.fromkeys(covers(abilities.get(aid))))} for lv, aid in learned],
            'Gaps': [{'From': a, 'To': b, 'Length': n, 'Closed': closed} for a, b, n, closed in gs],
        })
    rows.sort(key=lambda r: r['Name'].lower())

    data = {
        'MaxLevel': args.max_level, 'GapLimit': args.gap, 'Seed': args.seed,
        'Costs': {'Passive': board.passive, 'Ability': board.ability, 'PassThrough': board.pass_through},
        'Classes': rows,
    }
    os.makedirs(args.out, exist_ok=True)
    out = os.path.join(args.out, 'data.js')
    with open(out, 'w', encoding='utf-8', newline='\n') as f:
        f.write('// Generated by tools/skillhex/cadence.py. Do not edit; re-run it after a board or ability change.\n')
        f.write('window.ABILITY_CADENCE = ' + json.dumps(data, indent=1, ensure_ascii=False) + ';\n')

    flagged = [r for r in rows if r['Gaps']]
    for r in flagged:
        spans = ', '.join(f'Lv {g["From"]}-{g["To"]}' + ('' if g['Closed'] else ' (nothing new)') for g in r['Gaps'])
        print(f'{r["Name"]}: {spans}')
    print(f'\nwrote {os.path.relpath(out, REPO)}: {len(rows)} classes, Lv 1-{args.max_level}, '
          f'{len(flagged)} with a gap over {args.gap} levels')
    return 0


if __name__ == '__main__':
    sys.exit(main())
