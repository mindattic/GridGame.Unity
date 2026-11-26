# Turn modes: raw soak tables

How the modes are compared, and what each metric means: [TURN_MODES.md](../TURN_MODES.md). This file only holds the
tables `tools/turn_modes.py` prints.

## How a table is made

```
python tools/turn_modes.py --modes PartyWindow --per-stage      # writes artifacts/turn-modes/<date>/ and prints the table
python tools/turn_modes.py --dry-run                            # shows the dotnet commands and env, runs nothing
python tools/turn_modes.py --table artifacts/turn-modes/<date> --per-stage   # re-prints a gathered run
```

Fixed inputs (the same for every mode, GG-T778):

- **Stages:** greenvalley-01, lanternroad-02, desert-02, swamp-01 (boss: lich), cave-03 (boss: cyclopes),
  still-02, cityruins-02, cathedral-03 (boss: hierarch, the finale), bloom-03. One per main-campaign region, early to
  late. The campaign is played in order up to the last listed stage, so each one is met at the level a campaign
  arrives with; only the listed stages are tallied. The `*2` themes after greenvalley-04 are left out while that stage
  is a 0/4 wall for the bots (GG-T742, GG-T919).
- **Seeds:** 1, 2, 3 (up to 4 attempts per stage; a seed's run stops at a stage it cannot clear).
- **Bots:** smart (`HeroBot` randomness 0, the soak's `greedy` case) and casual (randomness 0.5).
- **Difficulty:** 5 and 10 (`GRIDGAME_DIFFICULTY`).

Each row pools every listed stage across the seeds ("ALL"). Columns follow the TURN_MODES.md metrics: win %, runs,
battle length (hero actions, enemy turns, seconds per run), decisions per player turn (heroes able to act, legal actions
among them), hero actions per enemy turn, downtime (longest enemy-turn run, its average per run, enemy-only seconds per
stretch), hero pincers and chains, Guards forced/chosen, skill/spell/item uses, mana by source (guard/pincer/other) and
bot refusals.

## PartyWindow baseline (GG-T778, GG-T921)

Run 2026-11-26 on ef9da356: `python tools/turn_modes.py --modes PartyWindow --per-stage` (fixed stages, seeds 1-3, one
serial `dotnet test` per difficulty). **PARTIAL: the casual bot at difficulty 10 is missing.** The host killed the soak
for low memory twice (the second time after the difficulty 5 cases and the smart case at difficulty 10 had finished),
so that row was not recorded; re-run it with `--bots casual --difficulties 10`. The `capped` column counts battles that
hit the 900-command cap (GG-T1175): 0 in every row here, and no casual battle was long (about 160 hero actions per run).
All recorded cases passed with 0 refusals, so no stalls or invariant breaks.

| mode | bot | diff | win % | runs | capped | hero acts/run | enemy turns/run | secs/run | heroes/turn | legal acts/turn | hero acts/enemy turn | downtime max | downtime avg | enemy-s/stretch | pincers | chains | guard forced/chosen | skill/spell/item | mana guard/pincer/other | refusals |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| PartyWindow | casual | 5 | 83 | 30 | 0 | 161.5 | 86.1 | 441 | 3.32 | 145.8 | 1.88 | 3 | 2.4 | 0.00 | 656 | 43 | 588/99 | 1579/240/16 | 2781/11179/29813 | 0 |
| PartyWindow | smart | 5 | 100 | 27 | 0 | 68.8 | 15.7 | 124 | 5.25 | 249.9 | 4.39 | 3 | 2.1 | 0.00 | 967 | 150 | 123/76 | 637/158/9 | 1500/11366/22204 | 0 |
| PartyWindow | smart | 10 | 96 | 28 | 0 | 78.9 | 22.8 | 137 | 5.06 | 219.6 | 3.46 | 3 | 2.3 | 0.00 | 1075 | 179 | 243/107 | 825/219/17 | 2104/13084/25723 | 0 |

Per-stage win % (wins/runs):

| mode | bot | diff | greenvalley-01 | lanternroad-02 | desert-02 | swamp-01 | cave-03 | still-02 | cityruins-02 | cathedral-03 | bloom-03 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| PartyWindow | casual | 5 | 100 (3/3) | 100 (3/3) | 100 (3/3) | 100 (3/3) | 100 (3/3) | 100 (3/3) | 100 (3/3) | 43 (3/7) | 50 (1/2) |
| PartyWindow | smart | 5 | 100 (3/3) | 100 (3/3) | 100 (3/3) | 100 (3/3) | 100 (3/3) | 100 (3/3) | 100 (3/3) | 100 (3/3) | 100 (3/3) |
| PartyWindow | smart | 10 | 100 (3/3) | 100 (3/3) | 100 (3/3) | 100 (3/3) | 100 (3/3) | 100 (3/3) | 100 (3/3) | 100 (3/3) | 75 (3/4) |

What it shows: the party moves freely inside its window, so heroes/turn (3.3-5.3) and legal actions per turn (146-250)
are the highest of any mode, and hero actions per enemy turn are 1.9-4.4 against about 1 in the other modes. The smart bot wins
96-100%; the casual bot wins 83% at difficulty 5 and only loses at cathedral-03 (3/7) and bloom-03 (1/2). Downtime is 3 enemy turns at most.

## AnyHeroEveryTurn (Mode A, GG-T790)

Run 2026-11-26 on 684c461a: `python tools/turn_modes.py --modes AnyHeroEveryTurn --per-stage` (fixed stages, seeds
1-3, smart and casual, difficulty 5 and 10; one serial `dotnet test` per difficulty). The first run's casual case at
difficulty 10 was killed for low memory, so that row alone was re-run the same night on 3e1a3694 with
`--bots casual --difficulties 10` (no Mode A code changed between the two).

**No stalls, soft-locks or invariant breaks.** All four cases passed
(`BotRunner.Play` throws on more than 5 refusals in a row, and the runner's tile checks throw on any broken invariant)
and the bots logged 0 refusals, so no Mode A code was changed. **Near the 900-command cap:** the casual bot's battles
are long, as under UnmovedThisRound: about 490 hero actions per run at difficulty 5 on average, and about **786 hero
actions per run at swamp-01**, where it lost 11 of 12 runs (at difficulty 10, about 620 at swamp-01 and
about 495 at cave-03). Those swamp-01 losses are very likely battles that ran into the cap rather than real defeats (the soak counts a capped battle as a plain loss, GG-T1175). The smart bot's battles
stay short (130-170 hero actions per run).

| mode | bot | diff | win % | runs | hero acts/run | enemy turns/run | secs/run | heroes/turn | legal acts/turn | hero acts/enemy turn | downtime max | downtime avg | enemy-s/stretch | pincers | chains | guard forced/chosen | skill/spell/item | mana guard/pincer/other | refusals |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| AnyHeroEveryTurn | casual | 5 | 38 | 26 | 493.5 | 496.2 | 2102 | 1.35 | 55.7 | 0.99 | 3 | 2.7 | 0.00 | 123 | 1 | 103/56 | 5697/98/40 | 443/3492/15704 | 0 |
| AnyHeroEveryTurn | smart | 5 | 88 | 17 | 132.2 | 133.4 | 802 | 2.62 | 114.5 | 0.99 | 3 | 2.6 | 0.00 | 275 | 26 | 79/21 | 1374/137/11 | 417/6022/15475 | 0 |
| AnyHeroEveryTurn | casual | 10 | 60 | 20 | 445.6 | 498.1 | 1623 | 1.57 | 56.0 | 0.89 | 4 | 3.0 | 0.00 | 123 | 4 | 885/230 | 4482/103/27 | 1205/2884/17133 | 0 |
| AnyHeroEveryTurn | smart | 10 | 88 | 16 | 172.1 | 183.8 | 849 | 2.54 | 100.1 | 0.94 | 3 | 3.0 | 0.00 | 192 | 3 | 201/58 | 1858/131/19 | 959/4709/14081 | 0 |

Per-stage win % (wins/runs; `-` = not reached, because a seed's run stops at a stage it can't clear; cityruins-02,
cathedral-03 and bloom-03 were not reached by any row):

| mode | bot | diff | greenvalley-01 | lanternroad-02 | desert-02 | swamp-01 | cave-03 | still-02 |
|---|---|---|---|---|---|---|---|---|
| AnyHeroEveryTurn | casual | 5 | 43 (3/7) | 75 (3/4) | 100 (3/3) | 8 (1/12) | - | - |
| AnyHeroEveryTurn | smart | 5 | 100 (3/3) | 100 (3/3) | 100 (3/3) | 75 (3/4) | 67 (2/3) | 100 (1/1) |
| AnyHeroEveryTurn | casual | 10 | 100 (3/3) | 100 (3/3) | 100 (3/3) | 33 (2/6) | 20 (1/5) | - |
| AnyHeroEveryTurn | smart | 10 | 100 (3/3) | 100 (3/3) | 75 (3/4) | 75 (3/4) | 100 (2/2) | - |

What it shows: close to UnmovedThisRound. About one hero action per enemy turn (0.89-0.99), downtime capped at 3-4 enemy
turns and no enemy-only stretches. The smart bot has the most choice of any mode so far (about 2.6 heroes and over 100
legal actions per turn), and Guards are rare. The smart bot wins 88% at both difficulties, but no seed got past still-02,
so some unlisted stage after cave-03 or still-02 stopped every seed. The casual bot clears the opening stages and then
stalls out at swamp-01 (and at cave-03 at difficulty 10) in the near-cap battles described above. Oddly, casual does
better at difficulty 10 (60%) than at 5 (38%), and it forces far more Guards there (885 forced against 103).

## HeroTimeline (Mode C, GG-T817)

Run 2026-11-26 on 8dc13462: `python tools/turn_modes.py --modes HeroTimeline --per-stage` (fixed stages, seeds 1-3,
smart and casual, difficulty 5 and 10; one serial `dotnet test` per difficulty, about a minute in all).

**No stalls, soft-locks or invariant breaks.** Both runs passed (`BotRunner.Play` throws on more than 5 refusals in a
row, and the runner's tile checks throw on any broken invariant), the bots logged 0 refusals, and every battle ended
well inside the 900-command cap (at most about 300 hero actions plus enemy turns per run on average). So no Mode C code
had to change. Every casual loss is a real defeat at greenvalley-01 (4 attempts on each seed), not a timeout.

| mode | bot | diff | win % | runs | hero acts/run | enemy turns/run | secs/run | heroes/turn | legal acts/turn | hero acts/enemy turn | downtime max | downtime avg | enemy-s/stretch | pincers | chains | guard forced/chosen | skill/spell/item | mana guard/pincer/other | refusals |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| HeroTimeline | casual | 5 | 0 | 12 | 60.7 | 124.7 | 373 | 1.00 | 13.6 | 0.49 | 12 | 8.5 | 2.99 | 44 | 0 | 848/95 | 215/41/45 | 1170/1205/2930 | 0 |
| HeroTimeline | smart | 5 | 50 | 28 | 108.2 | 127.0 | 476 | 1.00 | 26.1 | 0.85 | 13 | 5.4 | 2.26 | 513 | 32 | 3491/209 | 1444/288/36 | 11586/10096/24639 | 0 |
| HeroTimeline | casual | 10 | 0 | 12 | 48.6 | 132.2 | 329 | 1.00 | 18.2 | 0.37 | 20 | 10.3 | 3.35 | 25 | 1 | 612/72 | 187/17/45 | 577/977/1630 | 0 |
| HeroTimeline | smart | 10 | 20 | 20 | 118.9 | 181.2 | 549 | 1.00 | 16.7 | 0.66 | 19 | 9.4 | 2.37 | 332 | 8 | 2766/166 | 1189/172/45 | 7055/5638/12678 | 0 |

Per-stage win % (wins/runs; `-` = not reached, because a seed's run stops at a stage it can't clear):

| mode | bot | diff | greenvalley-01 | lanternroad-02 | desert-02 | swamp-01 | cave-03 | still-02 | cityruins-02 | cathedral-03 | bloom-03 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| HeroTimeline | casual | 5 | 0 (0/12) | - | - | - | - | - | - | - | - |
| HeroTimeline | smart | 5 | 25 (2/8) | 100 (2/2) | 67 (2/3) | 100 (2/2) | 67 (2/3) | 50 (2/4) | 100 (1/1) | 100 (1/1) | 0 (0/4) |
| HeroTimeline | casual | 10 | 0 (0/12) | - | - | - | - | - | - | - | - |
| HeroTimeline | smart | 10 | 9 (1/11) | 100 (1/1) | 100 (1/1) | 33 (1/3) | 0 (0/4) | - | - | - | - |

What it shows: heroes/turn is always 1.00, as Mode C intends (one hero icon acts at a time). Guard is mostly forced
(about 9 forced to 1 chosen), and hero actions per enemy turn sit below 1, so the foes out-pace the party. The casual bot
can't clear the opening stage at either difficulty. That is a balance finding for the mode comparison, not a stall.

## UnmovedThisRound (Mode B, GG-T803)

Run 2026-11-26 on 066b4101: `python tools/turn_modes.py --modes UnmovedThisRound --per-stage` (fixed stages, seeds
1-3, smart and casual, difficulty 5 and 10; one serial `dotnet test` per difficulty, about 18 minutes in all on a busy
machine).

**No stalls, soft-locks or invariant breaks found.** Both runs passed (`BotRunner.Play` throws on more than 5 refusals in
a row, and the runner's tile checks throw on any broken invariant) and the bots logged 0 refusals, so no Mode B code was
changed. One open question: the casual bot's battles are very long (about 550 hero actions per run at difficulty 5,
against the soak's 900-command cap), so some casual losses, mostly the 11 at swamp-01, may be battles that hit the cap
rather than real defeats. The soak doesn't separate the two yet (follow-up ticket).

**GG-T1175 follow-up.** The soak report and `tools/turn_modes.py` now have a `capped` column: battles that end with neither
Victory nor Defeat when BotRunner's 900-command cap runs out. Probe on 1b733a3b (Core only, `GRIDGAME_SOAK_STAGES=swamp-01`,
seed 1, casual, difficulty 5): all 4 attempts were capped (0 wins, 4 capped, about 892 hero actions and 897 enemy turns per
run, 0 refusals, 0 defeats, downtime max 3). So the swamp-01 casual "losses" are cap timeouts, not Defeats, and no refusal or
stuck-round was seen (rounds keep resetting, about 1 hero action per enemy turn). Heroes per round at the sampled turn starts
was only 1.07, which hints the party is mostly down while the battle never ends; whether that is a balance stalemate or a Mode B
fault (for instance a lone survivor that cannot damage the foes, or Defeat not firing) is still open (split ticket).

| mode | bot | diff | win % | runs | hero acts/run | enemy turns/run | secs/run | heroes/turn | legal acts/turn | hero acts/enemy turn | downtime max | downtime avg | enemy-s/stretch | pincers | chains | guard forced/chosen | skill/spell/item | mana guard/pincer/other | refusals |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| UnmovedThisRound | casual | 5 | 43 | 23 | 551.6 | 553.6 | 2286 | 1.10 | 46.9 | 1.00 | 3 | 2.7 | 0.00 | 110 | 1 | 84/47 | 5637/76/32 | 436/3142/13838 | 0 |
| UnmovedThisRound | smart | 5 | 91 | 23 | 104.1 | 108.0 | 519 | 2.36 | 106.5 | 0.96 | 3 | 2.8 | 0.00 | 457 | 29 | 117/79 | 1448/205/30 | 957/9325/25863 | 0 |
| UnmovedThisRound | casual | 10 | 27 | 15 | 362.7 | 376.3 | 1828 | 1.28 | 57.4 | 0.96 | 3 | 2.8 | 0.00 | 133 | 1 | 175/77 | 2149/96/45 | 716/3815/10064 | 0 |
| UnmovedThisRound | smart | 10 | 62 | 37 | 214.3 | 226.3 | 1320 | 2.14 | 97.6 | 0.95 | 4 | 2.8 | 0.00 | 1116 | 56 | 481/106 | 5520/415/26 | 3078/15423/54167 | 0 |

Per-stage win % (wins/runs; `-` = not reached, because a seed's run stops at a stage it can't clear):

| mode | bot | diff | greenvalley-01 | lanternroad-02 | desert-02 | swamp-01 | cave-03 | still-02 | cityruins-02 | cathedral-03 | bloom-03 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| UnmovedThisRound | casual | 5 | 60 (3/5) | 100 (3/3) | 100 (3/3) | 8 (1/12) | - | - | - | - | - |
| UnmovedThisRound | smart | 5 | 100 (3/3) | 100 (3/3) | 100 (3/3) | 75 (3/4) | 100 (3/3) | 100 (3/3) | 75 (3/4) | - | - |
| UnmovedThisRound | casual | 10 | 22 (2/9) | 100 (1/1) | 100 (1/1) | 0 (0/4) | - | - | - | - | - |
| UnmovedThisRound | smart | 10 | 100 (3/3) | 100 (3/3) | 100 (3/3) | 100 (3/3) | 75 (3/4) | 100 (3/3) | 50 (3/6) | 25 (2/8) | 0 (0/4) |

What it shows: about one hero action per enemy turn (0.95-1.00), with downtime capped at 3-4 enemy turns and no
enemy-only stretches, so foes and party trade evenly. The smart bot has more than two heroes to choose from per turn,
and Guards are rare (forced still outnumbers chosen, but far fewer than under HeroTimeline). Both bots win far more than
under HeroTimeline (smart 91% / 62%, casual 43% / 27%). At difficulty 5 the smart bot cleared cityruins-02 on every seed
but never reached cathedral-03 or bloom-03, so some unlisted stage between them stopped every seed.
