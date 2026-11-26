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

## PartyWindow baseline (GG-T778)

> **PLACEHOLDER: not yet run.** The soak needs the owner's go-ahead before it is run (soaks and tests are only run on
> request). Once given: `python tools/turn_modes.py --modes PartyWindow --per-stage`, then paste the printed tables
> here with the date and commit hash they were run on.

| mode | bot | diff | win % | runs | hero acts/run | enemy turns/run | secs/run | heroes/turn | legal acts/turn | hero acts/enemy turn | downtime max | downtime avg | enemy-s/stretch | pincers | chains | guard forced/chosen | skill/spell/item | mana guard/pincer/other | refusals |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| PartyWindow | smart | 5 | ⬜ | | | | | | | | | | | | | | | | |
| PartyWindow | casual | 5 | ⬜ | | | | | | | | | | | | | | | | |
| PartyWindow | smart | 10 | ⬜ | | | | | | | | | | | | | | | | |
| PartyWindow | casual | 10 | ⬜ | | | | | | | | | | | | | | | | |

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
