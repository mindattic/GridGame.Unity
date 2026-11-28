# Slice Acceptance Checklist

GG-T2106, under GG-T1992 (Complete-product acceptance) and epic GG-T1941. Each owner ask, the beat in [`docs/STORY_SLICE.md`](../STORY_SLICE.md) that delivers it, and the capture or scenario that proves it. The page is [`docs/design/slice-acceptance/`](../design/slice-acceptance/index.html), which opens from disk. Its `data.js` is this file's twin, so change both together.

Checked 2026-11-28 against main at `7318fff7`. Reference device: **iPhone 15 Pro** (`iphone15pro`). The in-target matrix is `tools/devices.json`.

## The owner's ask

> all of this has to include a prologue where it teaches me what to do, and how to travel and what to look for and how to progress, and tutorials about buying consumable, equiping them, using them in battle; learning an ability and using it to shatter armor of a guy that won't die because you can't damage a guy from the direction he is wearing armor, then learn a spell and cast that to overcome an enemy that is immune to all damage types except Lightning ... to see the entire game working with quests, and bounties, and travel, and battle, and crafting, and healing, and equipping and repairing and all of this needs to be perfectly sized with perfect labels and icons and the best code possible

## Status

- **Done, with proof** (14): landed on main and seen in a capture.
- **Built, not yet seen on screen** (14): landed on main; a preset, a target or a new game reaches it, but no capture shows it yet.
- **Missing** (15): not on main; the open tickets that cover it are named.

12 asks, 43 rows.

## How to see the proof

- **Captures** are the uiaudit set on the iPhone 15 Pro at 160dc4b4 (GG-T2029, 2026-11-28 02:42), in `docs/design/ui-audit/captures/iphone15pro/` (see `docs/design/ui-audit/BASELINE.md`).
- **Presets** are `tools/scenarios/<name>.json`. **Targets** (`stage:<id>`, `screen:<id>`, `overworld`) build a coherent save on the fly. Both run with `python tools/scenario.py <preset|target> --device iphone15pro` and land live at that point. Run one player at a time.
- **Prologue battles** (`prologue-vault`, `prologue-ambush`, `prologue-bridge-skirmish`, `prologue-bridge-a`, `prologue-bridge-b`, `prologue-ward`) are not stages of a campaign region, so `scenario.py stage:prologue-*` refuses them. Reach them by playing a new game until the harness prologue flow (GG-T2083) captures each beat.
- A row moves from Built to Done when a capture or a committed harness screenshot shows it on the reference device. It moves from Missing to Built when the open tickets it names land on main.

## The checklist

### Prologue lessons

*"a prologue where it teaches me what to do"*

| What | Beat | Status | Proof | Landed | Open |
|---|---|---|---|---|---|
| **Name, look and pronouns at the Founding** The captures show the one-step name field. They were taken before GG-T2127 landed, so the look (commander / commander_male) and pronoun picker has not been captured. | 0:00 Cold Open; 2:45 the Founding | Built, not yet seen on screen | [createprofile.png](../design/ui-audit/captures/iphone15pro/createprofile.png), [newprofile.png](../design/ui-audit/captures/iphone15pro/newprofile.png) | GG-T1957, GG-T2127 | - |
| **The prologue battles teach pincers, chains, the timeline, the clock, GUARD, abilities and Heal** campaign.json Prologue replaces Tutorial for new games, in this order: prologue-vault, prologue-ambush, prologue-bridge-skirmish, prologue-bridge-a, prologue-bridge-b, prologue-ward. The three drills stay under Play a drill. | 0:45 The Trap; 4:00 The Clock; 6:00 Abilities; 13:45 The Toll Bridge | Built, not yet seen on screen | new game, prologue battle 1 `prologue-vault`, new game, prologue battle 2 `prologue-ambush`, new game, prologue battle 3 `prologue-bridge-skirmish` | GG-T1958, GG-T1959, GG-T1960, GG-T1961, GG-T2075, GG-T2138 | - |
| **Prologue conversations and barks** 14 conversations and barks compiled from Data/Dialogue. The owner still has to publish the Prose texts (GG-T2068). | every beat | Built, not yet seen on screen | `Assets/GridGame/Resources/Content/dialogues.json` | GG-T2066, GG-T2067 | GG-T2068, GG-T1748 |
| **The onboarding steps that string the beats together and resume them** Meta/Onboarding.cs still has only Crawl, Drill, HubIntro and Done. The map, hamlet and camp beats between the battles need these steps. | Wake to Waystone | Missing | None yet | - | GG-T2077, GG-T1746, GG-T2083 |
| **Skip the prologue** | Skip all | Missing | None yet | - | GG-T2082 |

### Travel

*"how to travel"*

| What | Beat | Status | Proof | Landed | Open |
|---|---|---|---|---|---|
| **The overworld map screen and the travel rules (on the road, carried orbs, make camp)** | 7:45 The Road to Tithe | Done, with proof | [overworld.png](../design/ui-audit/captures/iphone15pro/overworld.png), preset `overworld` | GG-T1785, GG-T1786, GG-T1790 | - |
| **The road to Tithe on the moors map: route line, day dial, danger pulse, the ambush on the edge** The ambush battle is built as prologue-ambush. The moors region in world.json and the travel tween with the dial and pulse are not. | 7:45 The Road to Tithe | Missing | new game, prologue battle 2 `prologue-ambush` | GG-T2075 | GG-T1743, GG-T1728, GG-T1724, GG-T1726, GG-T1739, GG-T1744 |
| **Camp at the Ford: Set up camp, the meal, eight hours, Well Rested** The capture is today's Camp screen, not the dusk Set up camp flow. | 17:45 Camp at the Ford | Missing | [camp.png](../design/ui-audit/captures/iphone15pro/camp.png) | - | GG-T1734, GG-T1736 |

### What to look for

*"what to look for"*

| What | Beat | Status | Proof | Landed | Open |
|---|---|---|---|---|---|
| **A plated foe: blows from the front clang for 0** Covered in full under Sunder. | 13:45 The Toll Bridge | Built, not yet seen on screen | new game, prologue battle 4 `prologue-bridge-a` | GG-T2069, GG-T2073 | - |
| **Info shows what hurts a foe (the element lock)** RevealResistances shows a story foe's immunities whatever the Bestiary tier. IMMUNE reads as one word. | 28:45 The Sleeping Stone | Built, not yet seen on screen | new game, prologue battle 6 `prologue-ward` | GG-T2071 | - |
| **The glint only the Commander sees; fog lifts with travel** | 25:45 The Glint | Missing | None yet | - | GG-T1730, GG-T2053, GG-T1746 |
| **An always-visible objective** | every beat | Missing | None yet | - | GG-T2078 |

### Progression

*"how to progress"*

| What | Beat | Status | Proof | Landed | Open |
|---|---|---|---|---|---|
| **The skill board and skill points** Sunder sits at node [6, 9] on the Barbarian's Start frontier. The capture is not of the Barbarian, so Sunder's node itself is not yet seen. | 17:45 Camp at the Ford | Done, with proof | [skill-hex.png](../design/ui-audit/captures/iphone15pro/skill-hex.png), [talents.png](../design/ui-audit/captures/iphone15pro/talents.png), preset `skill-hex` | GG-T2072 | - |
| **A Tome teaches a spell; the results show one Teach card per Tome** results.png shows the reworked results screen, not a Tome of Bolt Teach card. | 25:45 The Glint | Built, not yet seen on screen | [results.png](../design/ui-audit/captures/iphone15pro/results.png), new game, prologue battle 5 `prologue-bridge-b` | GG-T1969, GG-T2074 | - |
| **Waking a waystone: the tally, its circle, the region card** | 32:15 The Stone Wakes | Missing | None yet | - | GG-T1746, GG-T2077 |

### Buying, equipping and using a consumable

*"tutorials about buying consumable, equiping them, using them in battle"*

| What | Beat | Status | Proof | Landed | Open |
|---|---|---|---|---|---|
| **Buy: the shop** The shop screen is seen. The Tithe Merchant with one Healing Potion left needs the hamlet beat (GG-T2077's Shop step). | 10:45 The Hamlet of Tithe | Done, with proof | [shop.png](../design/ui-audit/captures/iphone15pro/shop.png) | - | GG-T2077 |
| **Equip: a Healing Potion on a hero's battle bar** Heroes > Abilities > Items is the equip surface. The preset opens a battle with the potion under the Consumables tab. | 10:45 The Hamlet of Tithe | Built, not yet seen on screen | preset `potion-equipped` | GG-T2076 | - |
| **Use: a hero wounded in the bridge skirmish, healed with the potion** The Collector's men wound a hero; a potion pickup and Heal. | 13:45 The Toll Bridge | Built, not yet seen on screen | new game, prologue battle 3 `prologue-bridge-skirmish` | GG-T2075 | - |
| **The coach for buy, equip and use** | 10:45 to 14:57 | Missing | None yet | - | GG-T2079 |

### Sunder on the plated Tithe Collector

*"learning an ability and using it to shatter armor of a guy that won't die"*

| What | Beat | Status | Proof | Landed | Open |
|---|---|---|---|---|---|
| **The first clash: plate to the front, rocks on his flanks, a scripted withdrawal after three clangs** Ends as BattlePhase.Withdrawn: FALL BACK banner, no defeat, no costs, the stage counts as played. | 13:45 The Toll Bridge | Built, not yet seen on screen | new game, prologue battle 4 `prologue-bridge-a` | GG-T2069, GG-T2070, GG-T2073 | - |
| **Learn Sunder at camp** The skill exists (adjacent Strike, 12 Physical, CD 3, ShattersPlate). The story-granted point at camp waits on the camp beat and the coach. | 17:45 Camp at the Ford | Built, not yet seen on screen | preset `skill-hex` | GG-T2072 | GG-T2080, GG-T1734 |
| **The rematch: Sunder shatters the plate (SHATTERED), a pincer from the front finishes him; Chain Mail and the Collector's Chest drop** The new-gear presets show the bag-upgrade badge and the compare for Chain Mail. | 22:15 The Rematch | Built, not yet seen on screen | new game, prologue battle 5 `prologue-bridge-b`, preset `new-gear`, preset `new-gear-compare` | GG-T2069, GG-T2073, GG-T2134, GG-T2061 | - |
| **The coach for learning and using Sunder** | 19:45 learn Sunder; 22:15 the pointer | Missing | None yet | - | GG-T2080 |
| **The Red Mage joins after the rematch** Her lines are in the dialogue seeds, but no stage or campaign content grants the recruit yet. | 22:15 The Rematch | Missing | None yet | GG-T2066 | GG-T2077, GG-T2082 |

### Bolt on the Lightning-only Ward-Engine

*"learn a spell and cast that to overcome an enemy that is immune to all damage types except Lightning"*

| What | Beat | Status | Proof | Landed | Open |
|---|---|---|---|---|---|
| **The Tome of Bolt in the Collector's chest teaches the Red Mage** tome_bolt is in chest_tithe; a Tome may teach a Spell. | 22:15 The Rematch; 25:45 The Glint | Built, not yet seen on screen | new game, prologue battle 5 `prologue-bridge-b` | GG-T2074, GG-T2134 | - |
| **The Ward-Engine: every resistance 0 except Lightning x1.5; the Storm Flask fallback** ward_engine has cold-tinted mecha art. The stage's Lessons cover reading Info and casting the spell. flask_storm stays the item that always works. | 28:45 The Sleeping Stone | Built, not yet seen on screen | new game, prologue battle 6 `prologue-ward` | GG-T2071, GG-T2074, GG-T2138 | - |
| **The coach for learning and casting Bolt** | 26:15 to 29:33 | Missing | None yet | - | GG-T2081 |

### Quests, bounties, battle, crafting, healing, equipping, repairing

*"the entire game working with quests, and bounties, and travel, and battle, and crafting, and healing, and equipping and repairing"*

| What | Beat | Status | Proof | Landed | Open |
|---|---|---|---|---|---|
| **Quests: the quest screen and the q_moor quests** q_moor_slimes, q_moor_harpies and q_moor_werewolf are in quests.json. Givers and targets on the map, and the two prologue quests, are still open. | Slice arc 2 and 3 | Done, with proof | [quests.png](../design/ui-audit/captures/iphone15pro/quests.png) | - | GG-T2055, GG-T2057 |
| **Bounties: the bounty board and Grimjaw the Mauler** bounty_grimjaw is in bounties.json. A roaming bounty on the moors map is still open. | Slice arc 3 | Done, with proof | [bounties.png](../design/ui-audit/captures/iphone15pro/bounties.png) | - | GG-T2056 |
| **Battle** GG-T2014 (the battle reads at a glance) is polish on a battle that already works. | every battle | Done, with proof | [battle.png](../design/ui-audit/captures/iphone15pro/battle.png), [battle-abilities.png](../design/ui-audit/captures/iphone15pro/battle-abilities.png), preset `greenvalley-start` | GG-T2118 | GG-T2014 |
| **Crafting: Crucible, Alchemist, Blacksmith, Enchantress** The stations work. Opening them at the hamlet beats, and a moors brew and forge recipe in the first hour, are still open. | Slice arc 2 | Done, with proof | [crucible.png](../design/ui-audit/captures/iphone15pro/crucible.png), [alchemist.png](../design/ui-audit/captures/iphone15pro/alchemist.png), [blacksmith.png](../design/ui-audit/captures/iphone15pro/blacksmith.png), [enchantress.png](../design/ui-audit/captures/iphone15pro/enchantress.png) | GG-T2021 | GG-T2059 |
| **Healing: camp rest, the Medic Wagon after a win on the road** Wounds carrying between road battles, and the prologue granting the Medic Wagon, are still open. | 17:45 Camp; slice arc 2 to 4 | Done, with proof | [camp.png](../design/ui-audit/captures/iphone15pro/camp.png), [results.png](../design/ui-audit/captures/iphone15pro/results.png) | GG-T1785, GG-T1790, GG-T1969 | GG-T2058 |
| **Equipping** | 22:15 The Rematch (Chain Mail) | Done, with proof | [equip.png](../design/ui-audit/captures/iphone15pro/equip.png), preset `new-gear`, preset `new-gear-compare` | GG-T2061, GG-T2134 | - |
| **Repairing: the Blacksmith repair list** The repair lesson at the hamlet or camp after the Collector fights is still open. | after the Collector fights | Done, with proof | [blacksmith.png](../design/ui-audit/captures/iphone15pro/blacksmith.png) | GG-T1865, GG-T1883 | GG-T2060 |

### The boss (the Captain) and the ending

*"a complete experience, even if abbreviated"*

| What | Beat | Status | Proof | Landed | Open |
|---|---|---|---|---|---|
| **The Captain at The Tithe Moors III: boss intro, phases, rally at half HP, TwinTurn, defeat scene** battle-boss.png shows the boss presentation (GG-T1952), but it was captured before the Captain landed (0e2f3c46), so the Captain himself is not yet seen. Balance by calculation is GG-T2064. | Slice arc 4 | Built, not yet seen on screen | preset `greenvalley-boss`, [battle-boss.png](../design/ui-audit/captures/iphone15pro/battle-boss.png) | GG-T1952, GG-T2062 | GG-T2064 |
| **The slice ending crawl, credits, back to the map with the road dormant** The credits screen exists (title > Credits). The slice ending that leads to it does not. | Slice arc 5 | Missing | [credits.png](../design/ui-audit/captures/iphone15pro/credits.png) | - | GG-T2063 |

### Sizing

*"perfectly sized ... the latest models of Apple and Samsung phones"*

| What | Beat | Status | Proof | Landed | Open |
|---|---|---|---|---|---|
| **The device matrix and tools know every target phone** Reference: iPhone 15 Pro. In target: iPhone 15 Pro and newer, Galaxy S23 and newer. | all | Done, with proof | `tools/devices.json`, `docs/devices/DEVICES.md` | GG-T1993, GG-T2116, GG-T2119 | - |
| **Baseline captures on every in-target iPhone shape** On the 15 Pro: 0 overflow, 0 clipped, 0 overlap, 0 under the cutout. 7 screens have tap targets under 44 pt; 26 contrast fails; 52 home-indicator hits. | all | Done, with proof | `docs/design/ui-audit/BASELINE.md`, `docs/design/ui-audit/captures/iphone15pro/findings.json` | GG-T2029 | - |
| **Every screen fits every shape and holds its performance budget** | all | Missing | None yet | - | GG-T2122, GG-T2114, GG-T2098, GG-T2099, GG-T2100, GG-T2115, GG-T2101 |

### Labels and icons

*"perfect labels and icons"*

| What | Beat | Status | Proof | Landed | Open |
|---|---|---|---|---|---|
| **No two abilities in a hero kit share an icon** | all | Done, with proof | `tools/check_kit_icons.py` | GG-T1955, GG-T1983, GG-T1980, GG-T1981 | - |
| **The label and icon audit, its fixes, one complete icon set and the sign-off** | all | Missing | None yet | - | GG-T2094, GG-T2095, GG-T2096, GG-T2123, GG-T2124, GG-T2001, GG-T2097 |

### Code health

*"the best code possible"*

| What | Beat | Status | Proof | Landed | Open |
|---|---|---|---|---|---|
| **Screens one per file; battle, content and automation split; the size guard** polish_lint flags .cs files over 1200 lines and files that hold two screens. | none | Done, with proof | `tools/size_baseline.json`, `tools/polish_lint.py`, `docs/CODE_STYLE.md` | GG-T1995, GG-T1997, GG-T1998, GG-T2024, GG-T2035, GG-T2036 | - |
| **MetaServices split by domain: heroes** | none | Missing | None yet | - | GG-T2037 |

## After this checklist

- GG-T2107: Harness slice flow: drive the whole slice end to end
- GG-T2108: Acceptance run on the reference device (the iPhone 15 Pro, per the GG-T1992 note)
- GG-T2109: Acceptance run on the Galaxy S25
- GG-T2110: OWNER: play the slice
