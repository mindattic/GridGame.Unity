# Code style: file size and layout (GG-T1998)

These rules keep files small enough that two agents working on different features rarely touch the same file.
`python tools/polish_lint.py --size` checks them (rule POL-S1) and exits 1 on a new offender; the full
`python tools/polish_lint.py` table shows POL-S1 next to the other polish rules.

## Limits
- **No .cs file over 1200 lines** (Assets/GridGame and Tests). Split by concern before you cross it, not after.
- **One screen per file.** A screen is any concrete `GameScreen` subclass (directly or through `HubPage` or
  `HeroScreen`). The file is named after the screen: `App/Screens/<Area>/<Name>Screen.cs`. Helpers only that screen
  uses (a row builder, a nested popup) live in the same file; helpers two screens share get their own file beside them.
- Files that broke a rule before it existed are listed in `tools/size_baseline.json` and pass. When you split one,
  drop its entry (or rerun `python tools/polish_lint.py --write-size-baseline` once the split has landed). Never add
  an entry to get past the check: split the file instead. A baselined shared file may lose screens but not gain one.

## Screens: `App/Screens/<Area>/`
| Area | Holds |
|---|---|
| `FrontDoor/` | title, save slots, character select, profiles, about and credits |
| `Hub/` | camp hub, camp groups, stage select and confirm, their launchers |
| `Camp/` | stations: shop, blacksmith, alchemist, enchantress, aetherist, caravan, crucible, bounty, bestiary |
| `Heroes/` | hero screen and its tabs (status, equip, abilities, party, skill hex), mercenaries |
| `Items/` | items screen and item UI helpers |
| `Summon/` | summon, summon history, the reveal |
| `Story/` | dialogue, talk, story crawl, story beats |
| `Settings/` | settings, save export and import |

## Battle screen: partials by concern
`BattleScreen` is one `partial class` spread over `App/Battle/BattleScreen.<Concern>.cs`. `BattleScreen.cs` holds the
fields and entry points and is the only file that names the base class; each concern gets its own file
(`BattleScreen.Hud.cs`, `BattleScreen.Lifecycle.cs`, `BattleScreen.Fx.cs`, `BattleScreen.AbilitySheet.cs` ...).
Event playback splits one level further by event family: `BattleScreen.Playback.<Family>.cs` (`.Move`, `.Combat`,
`.Spells`, `.Status`, `.Waves`). Any other class that outgrows a file follows the same pattern
(`AutomationServer.<Routes>.cs`).

## Meta: domain folders
Meta services sit one type (or one tight family) per file, grouped by domain:

| Folder | Holds |
|---|---|
| `Meta/` (root) | cross-cutting services: progression, battle report, battle factory, caravan, and the existing single-purpose files |
| `Meta/Economy/` | camp, shop, bargains, station progress, crafting, brewing, forging, enchanting, recipes, repair, inn |
| `Meta/Heroes/` | equipment, abilities, training, party, permadeath, summon |
| `Meta/Save/` | save migrations, settings, hero saves, bestiary knowledge, progress saves (bounty, quest, crucible) |

Core content follows the same idea: `Core/Content/Models/` for content definitions and
`Core/Content/Validation/<Domain>Validator.cs` for the load-time checks.

## Moving code
A split is a move, never a rewrite: same namespace, same names, same order of side effects, no behaviour change.
Commit the Unity-generated `.meta` file for every new .cs file and folder.
