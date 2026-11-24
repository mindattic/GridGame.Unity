"""GG-T757: the postgame XP wall, worked out from content instead of a soak (numbers for the owner's GG-T474 pick).

    python tools/xp_wall.py [--markdown]

Reads the XP shapes of Assets/GridGame/Core/Rules/Formulas.cs (XpToNext, XpReward, StatsAtLevel, mirrored below),
every campaign stage's waves (stages.json, a wave's Group expanded from encounters.json), each stage's level
(LevelOverride, else campaign index + 1, BIBLE "The postgame curve") and each foe's XpReward/BaseStats/Growth
(classes.json). It then plays the campaign straight through on paper and prints, for every postgame stage from The
Bloom I to Morrow's Shadow III:

  - Arrive: the level a party that cleared every earlier stage exactly once arrives at,
  - Foe: the level the stage's enemies spawn at,
  - Gap: Foe - Arrive,
  - Hunts: replays of the previous stage that straight-through party needs to reach the foes' level,
  - Total hunts (in the run summary): replays a party needs over the whole postgame if it closes every gap before
    moving on (hunt the previous stage until level >= the next stage's foes, then clear it).

for the current curve and for each option in docs/RELEASE_SCOPE.md "Measured: the postgame level wall". The options
are modelled here only; no balance value in the game changes (GG-T474 is the owner's pick).

Model (deliberately simple, the same for every option so they compare fairly): every hero earns the battle's full
XP (MetaServices: XpEach = the battle's XP to each hero); one stage is one battle that kills every listed foe once
at Normal difficulty (RewardXp 1), no NG+, no pincer-chain XP bonus, no snake segments, remains or summons; enemy
level = max(spawn Level, stage level), enemy stats = StatsAtLevel with no buffs. The party starts at level 1.
Not part of any automated suite.
"""
import json
import math
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONTENT = os.path.join(ROOT, "Assets", "GridGame", "Resources", "Content")
STATS = ("Strength", "Vitality", "Agility", "Speed", "Stamina", "Intelligence", "Wisdom", "Luck")
POSTGAME_FIRST = "bloom-01"
MAIN_LAST_LEVEL = 24  # the Cathedral's last stage; "past 24" in the options below
MAX_LEVEL = 99
HUNT_CAP = 9999


def load(name):
    with open(os.path.join(CONTENT, name + ".json"), encoding="utf-8") as f:
        data = json.load(f)
    return data if isinstance(data, list) else next(iter(data.values()))


CLASSES = {c["Id"]: c for c in load("classes")}
GROUPS = {g["Id"]: g for g in load("encounters")}
STAGES = {s["Id"]: s for s in load("stages")}
with open(os.path.join(CONTENT, "campaign.json"), encoding="utf-8") as _f:
    THEMES = json.load(_f)["Themes"]
CAMPAIGN = [sid for t in THEMES for sid in t["Stages"]]
REGION_OF = {sid: t["Id"] for t in THEMES for sid in t["Stages"]}


def csround(x):
    """C# Math.Round default (banker's rounding); Python's round() is the same."""
    return int(round(x))


# ---- Formulas.cs mirrors -------------------------------------------------------------------------------------------

def xp_to_next(level):
    return 50 + level * level * 10


def stats_at_level(cls, level):
    s = {k: float(cls.get("BaseStats", {}).get(k, 0)) for k in STATS}
    growth = cls.get("Growth") or {}
    milestones = {m["Level"]: m.get("Growth") or {} for m in (cls.get("Milestones") or []) if m}
    for lv in range(2, max(1, level) + 1):
        for k in STATS:
            s[k] += growth.get(k, 0)
        for k, v in milestones.get(lv, {}).items():
            s[k] = s.get(k, 0) + v
    return {k: math.floor(v) for k, v in s.items()}


def xp_reward_parts(cls, level):
    """(the XpReward + level term, the stat term) of Formulas.XpReward before rounding."""
    s = stats_at_level(cls, level)
    phys = s["Strength"] + s["Vitality"]
    magic = s["Intelligence"] + s["Wisdom"]
    return cls.get("XpReward", 0) + level * 2.0, phys * 0.10 + magic * 0.08


# ---- content ------------------------------------------------------------------------------------------------------

def stage_spawns(stage):
    out = []
    for w in stage.get("Waves") or []:
        if w.get("Enemies"):
            out += [(e["Class"], e.get("Level", 1)) for e in w["Enemies"]]
        elif w.get("Group") in GROUPS:
            g = GROUPS[w["Group"]]
            out += [(e["Class"], max(1, g.get("Level", 1))) for e in g["Enemies"]]
    return out


def current_stage_level(index):
    st = STAGES[CAMPAIGN[index]]
    return st["LevelOverride"] if st.get("LevelOverride") is not None else index + 1


# ---- options ------------------------------------------------------------------------------------------------------

class Option:
    """One curve: how a stage's level, a kill's XP and a level's cost are worked out."""

    def __init__(self, key, label, stage_level=current_stage_level, kill_xp=None, to_next=xp_to_next):
        self.key, self.label, self.stage_level, self.to_next = key, label, stage_level, to_next
        self.kill_xp = kill_xp or (lambda cls, lv, hero_lv: max(1, csround(sum(xp_reward_parts(cls, lv)))))
        self._cum = [0, 0]  # _cum[L] = total XP to reach level L

    def total_for(self, level):
        while len(self._cum) <= level:
            n = len(self._cum)
            self._cum.append(self._cum[-1] + self.to_next(n - 1))
        return self._cum[level]

    def level_of(self, total):
        lv = 1
        while lv < MAX_LEVEL and total >= self.total_for(lv + 1):
            lv += 1
        return lv

    def stage_xp(self, index, hero_level):
        lv = self.stage_level(index)
        return sum(self.kill_xp(CLASSES[c], max(sp, lv), hero_level) for c, sp in stage_spawns(STAGES[CAMPAIGN[index]]))

    def hunts_to(self, total, index, target):
        """Replays of stage `index` from `total` XP until level >= target: (hunts, total after)."""
        n = 0
        while self.level_of(total) < target and n < HUNT_CAP:
            total += self.stage_xp(index, self.level_of(total))
            n += 1
        return n, total


def late_kill_square(coef):
    def kill(cls, lv, hero_lv):
        base, stats = xp_reward_parts(cls, lv)
        return max(1, csround(base + stats + coef * max(0, lv - MAIN_LAST_LEVEL) ** 2))
    return kill


def late_kill_cost_scaled(cls, lv, hero_lv):
    """Kill XP grows with the cost of a level past 24, so a postgame level costs the kills a level-24 one does."""
    base = max(1, csround(sum(xp_reward_parts(cls, lv))))
    if lv <= MAIN_LAST_LEVEL:
        return base
    return max(1, csround(base * xp_to_next(lv) / xp_to_next(MAIN_LAST_LEVEL)))


def per_region_levels(step, finale):
    """Every postgame region fights at one level, `step` above the region before (The Bloom at 25); Morrow's Shadow III
    keeps `finale` so the campaign still ends high."""
    first = CAMPAIGN.index(POSTGAME_FIRST)
    regions = []
    for sid in CAMPAIGN[first:]:
        if REGION_OF[sid] not in regions:
            regions.append(REGION_OF[sid])

    def level(index):
        if index < first:
            return current_stage_level(index)
        if index == len(CAMPAIGN) - 1 and finale:
            return finale
        return 25 + step * regions.index(REGION_OF[CAMPAIGN[index]])
    return level


def bent_curve(level):
    """XpToNext stays quadratic to 24, then grows only by its slope at 24 (480 a level)."""
    if level <= MAIN_LAST_LEVEL:
        return xp_to_next(level)
    return xp_to_next(MAIN_LAST_LEVEL) + 20 * MAIN_LAST_LEVEL * (level - MAIN_LAST_LEVEL)


def catch_up(per_level, cap):
    """A hero below the foe's level earns +per_level XP per level behind (up to x cap), postgame only."""
    def kill(cls, lv, hero_lv):
        base = max(1, csround(sum(xp_reward_parts(cls, lv))))
        if lv <= MAIN_LAST_LEVEL:
            return base
        return max(1, csround(base * min(cap, 1 + per_level * max(0, lv - hero_lv))))
    return kill


def flat_postgame_bonus(mult):
    def kill(cls, lv, hero_lv):
        base = max(1, csround(sum(xp_reward_parts(cls, lv))))
        return base if lv <= MAIN_LAST_LEVEL else max(1, csround(base * mult))
    return kill


OPTIONS = [
    Option("now", "Current curve (option 4, keep it)"),
    Option("1a", "1a. Late kill XP: level term + 2*(L-24)^2 past 24", kill_xp=late_kill_square(2)),
    Option("1b", "1b. Late kill XP: x XpToNext(L)/XpToNext(24) past 24", kill_xp=late_kill_cost_scaled),
    Option("2a", "2a. Per-region levels: Bloom 25, +2 a region, Morrow's Shadow III 40",
           stage_level=per_region_levels(2, 40)),
    Option("2b", "2b. Per-region levels: Bloom 25, +1 a region, Morrow's Shadow III 34",
           stage_level=per_region_levels(1, 34)),
    Option("3", "3. XP curve bends past 24 (XpToNext linear, +480 a level)", to_next=bent_curve),
    Option("5a", "5a. Postgame XP bonus: kill XP x2 past 24", kill_xp=flat_postgame_bonus(2.0)),
    Option("5b", "5b. Catch-up: +50% kill XP per level behind the foe (max x4), past 24", kill_xp=catch_up(0.5, 4.0)),
]


def run(opt):
    """Rows for every postgame stage plus the hunt-as-you-go total."""
    first = CAMPAIGN.index(POSTGAME_FIRST)
    total, rows = 0, []
    for i in range(len(CAMPAIGN)):
        arrive, foe = opt.level_of(total), opt.stage_level(i)
        if i >= first:
            hunts, _ = opt.hunts_to(total, i - 1, foe)
            rows.append((STAGES[CAMPAIGN[i]]["Name"], arrive, foe, foe - arrive, hunts))
        total += opt.stage_xp(i, arrive)
    end = opt.level_of(total)
    # A party that hunts the previous stage until it matches the next one's foes, then clears it.
    total, hunted = 0, 0
    for i in range(len(CAMPAIGN)):
        if i >= first:
            n, total = opt.hunts_to(total, i - 1, opt.stage_level(i))
            hunted += n
        total += opt.stage_xp(i, opt.level_of(total))
    return rows, hunted, end


def fmt_hunts(n):
    return f"{n}+" if n >= HUNT_CAP else str(n)


def main():
    md = "--markdown" in sys.argv
    results = [(o, *run(o)) for o in OPTIONS]
    if md:
        names = [r[0] for r in results[0][1]]
        print("| Stage | " + " | ".join(o.key for o, *_ in results) + " |")
        print("|---|" + "---|" * len(results))
        for k, name in enumerate(names):
            cells = []
            for o, rows, _, _ in results:
                _, arrive, foe, gap, hunts = rows[k]
                cells.append(f"{arrive}/{foe} ({fmt_hunts(hunts)})")
            print(f"| {name} | " + " | ".join(cells) + " |")
        print("| **Total hunts, close every gap** | " + " | ".join(f"**{fmt_hunts(h)}**" for _, _, h, _ in results) + " |")
        print("| Straight-through level after Morrow's Shadow III | " + " | ".join(str(e) for _, _, _, e in results) + " |")
        return
    for o, rows, hunted, end in results:
        print(f"\n{o.label}")
        print(f"  {'Stage':28} {'Arrive':>6} {'Foe':>4} {'Gap':>4} {'Hunts':>6}")
        for name, arrive, foe, gap, hunts in rows:
            print(f"  {name:28} {arrive:>6} {foe:>4} {gap:>4} {fmt_hunts(hunts):>6}")
        print(f"  hunts to close every gap: {fmt_hunts(hunted)}; straight-through party ends at level {end}")


if __name__ == "__main__":
    main()
