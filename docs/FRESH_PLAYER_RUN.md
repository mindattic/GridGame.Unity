# Fresh-player run (GG-T494, 2026-11-20)

A brand-new profile played through the real UI for about the first hour (49 minutes of recorded play, 0:47 on the
save's clock): title → new profile → party pick → prologue → the tutorial battle → three expeditions → Camp's
vendors, the Summon Circle (with the new odds display), the Diamond Store and the Blacksmith once he joined. Battles
were driven through real drags, taps and spell casts on the automation endpoint (the bot's suggested move, dragged
tile by tile), not autoplay.

- **Video:** `R:\MindAttic\Capture\Unity.GridGame\2026.11.21T03.55.06Z_Unity.GridGame\2026.11.21T03.55.06Z_Unity.GridGame_Video.mp4`
  (MindAttic.Media.Capture `-capture video hq`, 584x1266, 2.0 GB)
- **Screenshots:** `artifacts/fresh-player/NN-*.png` (git-ignored, local). After the fixes landed, the rebuilt player
  was checked in `artifacts/verify/v01..v06-*.png`.
- **Errors:** `Diagnostics/errors.jsonl` never got a record, and the player log has no exceptions. Its only warnings
  come from Telemetry and Capture.

## Timeline

| Time | Step | Shots |
|---|---|---|
| 0:00 | Title → Profile → + New profile → "Rook" → Create | 01–05 |
| 0:01 | New Game → Slot 1 → Choose Your Party: previewed and locked in Paladin, Barbarian, Cleric | 06–11 |
| 0:02 | Prologue crawl "Tithe Day" (placeholder lorem, GG-T497), skipped | 12–13 |
| 0:03 | Camp. Talk with the Paladin, then Adventure → The Tithe Moors I | 14–19 |
| 0:04 | Tutorial battle, five waves: Moving, Pincers, Tap to read, Shoving, Mana, Chains, Spells (Cleric Heal → Cast), Buffs, Timeline, Taunt (Paladin → Go), Pickups, Interrupting, Ready | 20–43 |
| 0:12 | Level-ups, Results, Next battle → The Tithe Moors II (the Red Mage joins as a guest) → III (Captain boss) | 44–53 |
| 0:20 | Taught the Tome of Regenerate to the Paladin. Expedition complete. Camp: Merchant (bought an Iron Sword and potions), Heroes → Equip (sword, hoods, gloves), Abilities, Alchemist (brewed), Inn | 54–72 |
| 0:26 | Adventure region select → The Lantern Road I and II | 73–79 |
| 0:33 | Back at Camp mid-expedition. The Summon Circle is open: Odds, Draw paid in gold, the reveal (Fencer). Diamond Store: exchanged 600 G for 100 Diamonds | 80–90 |
| 0:36 | The Lantern Road III (boss) → Camp → The Shardfall I | 91–98 |
| 0:45 | The Blacksmith finally joins: Repair, the guided first forge, then a lucky craft with 50 Diamonds (odds shown 24.2% / 64.2%) | 99–111 |

## Issues found

All are children of GG-T493 (EPIC QA).

### Fixed in this pass (one commit each, verified on a rebuilt player)

| Ticket | Issue |
|---|---|
| GG-T504 | Tutorial 1/15 Moving promises a glowing path, but the opening board drew none when no pincer was reachable |
| GG-T505 | Chains lesson said "slimes" in a wolf/bat wave. Spells lesson said "Confirm" but the button reads Cast |
| GG-T506 | Party pick strip clipped names ("Barbaria") and grew a line on the first lock-in |
| GG-T507 | Results: "Bag full — sold extras · 1 items". It is a per-item stack cap, and the count was wrong for one |
| GG-T508 | Merchant: every −/+ tap threw the long list back to the top |
| GG-T509 | Brew/Craft rebuilt the page on the guided or first recipe, so a second brew meant finding the recipe again |
| GG-T516 | Summon Circle: the open odds list (41 rows) pushed the Draw button off a page that doesn't scroll |
| GG-T517 | Summon reveal: the hero's name and role overlapped the page text that showed through the veil |

### Left open (design, art or tooling calls)

| Ticket | Issue |
|---|---|
| GG-T510 | Echo's portrait is a blank mannequin sketch |
| GG-T511 | The Rusty Sword icon draws a leather vest |
| GG-T512 | Tutorial wave 4: the board is empty and the timeline blank while a delayed reinforcement holds the wave. It took Taunt plus seven GUARDs, with nothing on screen saying why |
| GG-T513 | Results mid-expedition: "Continue" (to Camp) is ambiguous next to "Next battle" |
| GG-T514 | Results: the tome "Teach to…", the milestone and the expedition bonus sit below about 15 loot rows (I skipped a tome myself by tapping Continue) |
| GG-T515 | Merchant Sell: the "price · own N" value squeezes names into three hyphenated lines |
| GG-T518 | Camp marquees (locked-row hints, the Diamond refill button) read as fragments in their narrow space |
| GG-T519 | Price tags end in a bare "· 1s" (the timed job's wait) with nothing saying so |
| GG-T520 | The Merchant's first visit lists about 120 rows, including materials from regions not yet reached |
| GG-T521 | The Blacksmith joins only when worn gear drops below 60%. Starting heroes wear nothing and drops are never auto-equipped, so he joined after about 45 minutes, and only because I bought a sword |
| GG-T522 | After a `-capture` run, the capture finalizer inherits the dead player's automation socket, so the next launch on the same port fails ("player did not start") |

Already tracked and not refiled: every crawl and dialogue is still lorem ipsum (GG-T497 EPIC Story).

## Notes

- Rested shows "Unlimited" under automation (`RestedPool.Unlimited`). From the first Camp visit on I turned the real
  pool back on with `devrested limit=1`, which is what a player sees.
- What worked well: the in-tab tutorial paced lessons per wave, the drag preview's PINCER outline and chain supporters
  read clearly, the odds disclosure sits in front of every paid random draw and lucky craft, and nothing crashed.

# Fresh-player run 2 (GG-T1122, 2026-11-25), partial

Repeat of the GG-T494 route on the dev player `Build/Windows/GridGame.exe` (built 2026-11-23, before the HeroTimeline
commits of 2026-11-25), driven by `python tools/harness.py coach GG-T1122-fp --camp --record` on a new profile (Warden,
Barbarian, Cleric). Only the tutorial battle and the first Camp lessons were reached; the rest of the route is split to
GG-T1147.

- **Video:** `R:\MindAttic\Capture\GridGame.Unity\2026.11.26T02.46.25Z_GridGame.Unity\2026.11.26T02.46.25Z_GridGame.Unity_Video.mp4`
- **Screenshots:** `artifacts/GG-T1122-fp/` (git-ignored): `b1-*` tutorial battle, `camp1-*` Camp and Heroes lessons.

## Timeline

| Step | Shots |
|---|---|
| New profile, tutorial battle 1: lessons 1-15 (move to pickup) each showed a popup and marks | b1-01..37 |
| Wave 4/5 sat on lesson 16 (Interrupting); lessons 16-18 never showed; the battle ended in Defeat | GG-T1122-now |
| Camp after the defeat: Skill points lesson (Heroes > Abilities > Board) | camp1-38..42 |

## Issues

| Ticket | Issue |
|---|---|
| GG-T1145 | Abilities: battle-bar name "Lantern of the Still" wraps to 3 lines over the Supporter text |
| GG-T1146 | Tutorial battle lost while following every coach mark; lessons interrupt, plate and ready never shown (GG-T512 shape) |
| GG-T1147 | Not covered: expeditions 2-3, vendors, Summon Circle, Diamond Store, Blacksmith, Board and Party tabs, turn modes, the regression check of the 19 GG-T494 findings |
