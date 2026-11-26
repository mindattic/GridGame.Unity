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

## Continuation (GG-T1147, 2026-11-25)

A second fresh profile (Warden, Barbarian, Cleric) on a dev player rebuilt from this checkout with
`tools/unity.ps1 -Method GridGame.EditorTools.GridGameBuild.BuildWindowsDev` (batch mode, no editor open; it overwrote
`Build/Windows`, and the working tree carried other agents' uncommitted edits). It has the HeroTimeline turn modes and
the Board tab. The tutorial battle was not played by coach: `call autoplay on=1` won it (the GG-T1146 loss does not
reproduce under autoplay), so tutorial lessons were not re-checked. Played through the real UI: title, New Game, party
pick (preview, lock-in), prologue crawl (lorem, GG-T497), Tithe Moors I, II and III (all won on autoplay, default turn
mode PartyWindow), results with the tome Teach-to buttons, Camp with the Merchant joined. The player then crashed
(GG-T1151) and the run stopped at the budget.

- **Video:** `R:\MindAttic\Capture\GridGame.Unity6.11.26T03.13.35Z_GridGame.Unity` (the crashed session)
- **Screenshots:** `artifacts/GG-T1147-fp/` (git-ignored): `01`-`05` title and party pick, `06`-`08` prologue and Camp, `10`-`19` stages and
  the three battles' results, `20`-`21` Camp, `campA-*` the Merchant lessons.

| Ticket | Issue |
|---|---|
| GG-T1151 | Player crashed in UnityPlayer after Capture stopped, in Camp during the Merchant lessons |
| GG-T1152 | Results: "1 orbs carry over" / "0 orbs carry over" |

Regression check of the GG-T494 findings, as far as reached: GG-T506 (party strip) holds, no clipped names (Barbarian is shown
smaller than its neighbours); GG-T513 fixed (button reads "To Camp"); GG-T521 fixed (the Blacksmith joined after Tithe
Moors III); GG-T514 still open (the tome Teach-to rows sit below about 14 rows, 18-results-tomes.png). Not reached: GG-T504, 505,
507-512, 515-520, 522.

Still not covered (split off): expeditions beyond Tithe Moors, every Camp vendor, Summon Circle, Diamond/Shard Store, Blacksmith, Abilities
Board tab, Party tab mini-boards, the other turn modes.

# Fresh-player run 2, part 3 (GG-T1153, 2026-11-26)

New profile on the dev player, no recording (`artifacts/GG-T1153-fp/`, git-ignored), `harness.py`-style `campaign` autoplay
through nine battles (Tithe Moors, Lantern Road, Shardfall I-II reached Lv 10, 6 heroes), then a driven tour.

- **Covered:** Lantern Road and Shardfall expeditions (results `stage*-result.png`); Merchant Buy/Sell/Bargain; Blacksmith;
  Alchemist; Manasmith; Bounties; Quests; Summon Circle (list, odds button); Shard Store; Mercenaries; Codex; Bestiary; Talk;
  Items; Settings; Heroes Equip/Abilities/Board/Party (mini-boards); turn modes AnyHeroEveryTurn, UnmovedThisRound, HeroTimeline,
  PartyWindow (one autoplayed battle each, `m-*` and `n-*`).
- **Not reached (player died, see below):** the Summon draw and reveal (GG-T517), Merchant tap-scroll (GG-T508), a second brew
  (GG-T509), Echo and Rusty Sword art (GG-T510/511), the capture socket (GG-T522, no `-capture` this run), the Inn (shut mid-expedition) and Caravan, Crucible (locked).
- **Re-check of GG-T494:** GG-T513 fixed (reads "To Camp"); GG-T515 fixed (Sell names fit); GG-T516 fixed (Draw button visible); GG-T518
  marquees read fine at hub; GG-T520 still open (about 110 Buy rows, GG-T1161); GG-T514 unchanged (loot rows under the tome milestone).
  GG-T504/505/512 sit with the tutorial run (GG-T1146).
- **GG-T1151 crash:** the player died again with `Crash!!!` right after `CaptureRunner.OnQuitting` (Application quit), crash
  report `R:\Temp\MindAttic\GridGame\Crashes\Crash_2026-11-26_043206018`, with no `-capture` argument. So the quit crash does not need
  video capture. It happened while another agent's harness launch (same checkout, same derived port) very likely sent `quit` to this player.

| Ticket | Issue |
|---|---|
| GG-T1154 | Defeat screen: "The The Shardfall expedition has failed" |
| GG-T1155 | Shardfall III locked after a defeat while the hand points at it |
| GG-T1156 | Blacksmith header subtitle clipped; opens on empty Repair tab |
| GG-T1157 | Shrink-to-fit labels at mixed sizes (Merchant, Party builds, Results, region rows) |
| GG-T1158 | Turn-mode timeline captions overlap (Unmoved, HeroTimeline) |
| GG-T1159 | BoardAudit: two tokens on one tile in HeroTimeline mode |
| GG-T1160 | Autoplay loses Shardfall II/III under AnyHero and HeroTimeline |
| GG-T1161 | Merchant Buy list still about 100 rows (GG-T520 recheck) |
