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
