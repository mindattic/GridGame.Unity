# skill-hex: the skill hexagon as the template for every hero

Owner sign-off 2026-11-24: "this is it, perfect". The skill hexagon replaces classes-as-jobs, role
trees and family trees. Heroes are starting paths on one shared board; the board is the whole
character system.

- Page: `docs/design/skill-hexagon/index.html` (artifact https://claude.ai/artifact/PyBk3i2j1t2f9Fo2iFyMJQ)
- Data: `Assets/GridGame/Resources/Content/skillhex.json` (the one canonical file), schema
  `docs/design/skill-hexagon/skillhex.schema.json` (validated). The page loads `skillhex.js`, which
  `python tools/skillhex/sync.py` writes from it.
- Inspiration kept as is: `docs/design/stat-hexagon/` (heroes as 4-point recipes)

## The model (what the file says)

| Part | Rule |
|---|---|
| Stats | Six identity stats, clockwise from the top corner: VIT, STR, AGI, SPD, WIS, INT. Stamina is removed; Luck leaves hero identity. |
| Families | Six wedges, each from one stat to the next: Martial (VIT→STR), Brawn (STR→AGI), Shadow (AGI→SPD), Wild (SPD→WIS), Brains (WIS→INT), Grace (INT→VIT). |
| Board | Axial hexagonal lattice, radius 16, 817 points. Points with (Q−R) mod 3 = 0 are ability slots (271); every other point is a stat passive (546), so every ability is ringed by six passives. |
| Passives | +2 of the nearest stat, or +1/+1 where two stats are near even. Stored per node, so they can be hand-tuned. |
| Abilities | 211 placed, each at most once. 60 slots are Open: the 22 gaps for future content or DLC in [GAPS.md](GAPS.md), 15 freed by the slice-1 ability merges (GG-T974 and GG-T1126), 13 by slice 2 (GG-T975), 7 by slice 3 (GG-T976) and 3 by the slice-2 leftovers (GG-T1134, BIBLE §25.6.6.1). |
| Heroes | 65 placed heroes, 4 cut (Basher, Operative, Technician, Tinkerer), 1 new (a Wild druid-brawler, WIS 2 · STR 1 · SPD 1). Each hero = Recipe (its spot on the stat hexagon) + Signature (the move only it starts with) + Start (the connected path of nodes it joins with). |
| Growth | You own a connected set of nodes. You can take any node next to one you own. Each node costs SP (Passive 1, Ability 1 today). Any hero can reach any ability; distance is the price. |
| Party | Six heroes, each with a 5-slot bar (abilities, spells or consumables) picked from what that hero owns. |

The file is the single source, `Assets/GridGame/Resources/Content/skillhex.json`: the page reads it
(through the generated `skillhex.js`) and edits it, and the game loads it from there. One node or hero per line keeps hand edits and diffs readable.

## What changes in the game

1. **Content**: `skillhex.json` joins the content set. `families.json` becomes the six hex families.
   Every class gets `Family` set from its wedge. Role trees and family trees in `skilltrees.json`
   retire. The four cut classes leave recruit pools, stages and guests; their Codex lore stays.
2. **Core model**: a `SkillHexDef` (lattice, nodes, heroes) loaded and validated by `ContentLoader`. A `SkillHex`
   runtime gives adjacency, the frontier (what a hero can take next), route planning (cheapest
   walk, with passing through an unwanted ability weighted 4), and stat totals from owned passives.
3. **Stats**: Stamina leaves `Stats`, `BaseStats`, `Growth`, stat points, the hub and level-up
   screens, armor-set labels, and the 13 abilities that require it (they move to VIT). Luck stops being a
   class stat: crit, drops, steal, tame and amplifier use a flat base plus gear Luck. Hero stats come from
   level base plus the passives they own; per-level stat picks retire.
4. **Learning**: the board replaces talent points, trees and gold Learn. Ability `Requires` stop
   gating (the path is the gate); they can stay as display hints or be removed.
5. **Saves (v18)**: each hero gets its class Start path, plus the cheapest route to every ability it
   already knows. Spent talent points come back as SP. Cut classes convert to their nearest placed
   hero: Basher → Bruiser, Operative → Drifter, Technician → Engineer, Tinkerer → Engineer. Ability aliases
   (GG-T973) cover any merged moves.
6. **Screens**: a Skill Hex screen (pan, zoom, same colours as the page, hover or tap preview,
   buy route); the hero screen picks the 5-slot bar from owned abilities; Inspect shows board stats; a
   party view shows six mini-boards; a coach lesson and a Codex entry explain the board.
7. **Bots**: HeroBot and MetaBot spec on the board: pick target abilities by party needs and walk
   cheapest routes. This replaces BotStatSpend and talent auto-spend.
8. **Docs**: a BIBLE skill-hex section; the §25.6 family-tree sections are marked superseded.

## Order of work

1. Schema and tooling (no game change): the page reads `skillhex.json`; the page becomes an editor
   (assign abilities, edit passives, draw start paths, open and save the file); a validator script.
2. Core model, loader and runtime, additive and unused: the game loads and validates the board.
3. Owner decisions (below), then the switch: stats, learning, saves, roster, families.
4. Screens and bots.
5. Balance soaks (owner: "run the soaks"), then BIBLE.

## Owner decisions needed before step 3

- **SP economy**: SP per level, node prices (ability vs passive), and whether gold can still buy nodes.
- **Luck**: a flat base for everyone plus gear (proposed), or remove it entirely.
- **Stat growth**: from owned passives only (proposed), or keep some per-level growth.
- **Healing stat**: Grace runs INT → VIT, but healing scales off WIS today. Move healing to INT, or keep WIS.
- **Cut-class conversion**: confirm Basher → Bruiser, Operative → Drifter, Technician → Engineer, Tinkerer → Engineer.
- **New Wild hero**: name, art, kit and signature (art is owner-only).
- **Open slots**: what the 22 gaps are for (future content, DLC).
- **Signatures**: confirm the auto-picked signature moves; some sit a little off their hero's spot.

## Superseded

The family-tree feature GG-T953 (open children GG-T954 and GG-T956 to GG-T959) is superseded. Its landed
engine (GG-T977 to GG-T980: start nodes, family homes, family Learn and Requires) is retired by the
switch. The merge audit (GG-T972, BIBLE §25.6.6.1) stays useful for cutting duplicate moves off the board.
