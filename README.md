# GridGame.Unity

A portrait-mobile tactics RPG in Unity 6: Terra Battle pincer combat on a Grandia II-style timeline that only moves when you act, plus a Final Fantasy-style camp, all on a deterministic engine-free battle core.

[![Unity](https://img.shields.io/badge/Unity-6000.4.3f1-black)](ProjectSettings/ProjectVersion.txt) [![C#](https://img.shields.io/badge/language-C%23-239120)](Assets/GridGame) [![Platforms](https://img.shields.io/badge/platforms-Windows%20%7C%20Android%20%7C%20WebGL-blue)](tools/build.ps1) [![Tests](https://img.shields.io/badge/tests-880%20passing-brightgreen)](Tests/Core.Tests) [![Status](https://img.shields.io/badge/status-in%20development-orange)](docs/BIBLE.md)

![A pincer landing in Green Valley III: two heroes flank a slime, the Mana banner reads plus 48 clean pincer, and the timeline sits above the 6 by 8 board](docs/images/battle-pincer.png)

There is no public build or store listing yet. Build it from source with the Quick start below.

## Why

- Position is the only weapon: movement never deals damage, so every drop asks whether it finishes a line.
- Thinking time is free. The clock advances only when you commit an action, so a battle plays the same on a five-minute bus ride or an all-night session.
- Every battle is reproducible: the rules are pure, seeded C# with no `UnityEngine`, so a bot can soak-test every stage in seconds.
- Content changes are data edits, not code: classes, abilities, items, recipes, stages and the campaign are JSON validated at load.
- Nothing hides in the Unity Editor: one empty scene, and the whole game is built in code.

## Features

### Battle

![Dragging the Barbarian across the board, with the ability bar, the 12-orb mana bank and the hero card below](docs/images/battle-drag.png)

- A 6 by 8 grid. Drag a hero one tile at a time, cardinal only; anyone in the way slides into the tile you just left, friend or foe.
- Pincers: trap enemies in an unbroken, ally-free straight line between two heroes to strike, with chains, supporters and 2 by 2 bosses that need a width-wide pincer.
- An act-advanced timeline. Enemy icons load toward the act line, hits in the pushback zone shove their turns back, and spell casts ride a second lane below it where hits stagger and cancel them.
- A shared, capped bank of mana orbs that pincers, crits, steals and interrupts refill, spent on a per-hero ability bar of spells, skills and items.
- Elements and status effects (Frozen, Wet, Steam, Shock, Sleep, Silence and more), traps, knockback, physical blocks, movable tiles, Power Amplifier tiles and board pickups such as Heart Tonic and Mana Shard.
- Weapon and armor wear: weapons shatter with a visible warning, and a backup weapon swaps in automatically.
- Optional Auto mode where the built-in bot plays the battle for you.

### Camp and campaign

![The Camp menu: a five-hero party at level 9 above the command list, with gold, play time and 7 of 25 stages cleared](docs/images/camp-menu.png)

- Ten themes and 25 hand-built stages (five themes, then five postgame themes), each theme opening with a skippable story crawl.
- A Final Fantasy-style Camp menu: Adventure, Items, Status, Equip, Abilities, Party, Merchant, Blacksmith (forge, upgrade, salvage, repair, Masterwork crafting), Alchemist (brew, enchant, heal), Inn, Caravan upgrades, Summon Circle, Bounties, Bestiary, Settings and Save and Quit.
- Shipped content: 64 hero and 114 enemy classes, 197 abilities, 243 items, 154 recipes, 37 drop tables, 6 skill trees, 13 buffs, 7 traps and 5 bounties.
- Original synthesized sound effects for every event, five music tracks, and pixel-art icons.

## Quick start

Prerequisites:

- Unity 6000.4.3f1 (the Editor must be closed for any batchmode build)
- .NET SDK 10 for the rules tests
- Python 3 for the harness and checks
- Node.js only for the WebGL check

Clone, test and build the Windows development player:

```powershell
git clone https://github.com/mindattic/GridGame.Unity.git
cd GridGame.Unity
pwsh tools/test.ps1
pwsh tools/build.ps1
.\Build\Windows\GridGame.exe
```

You should see the title screen. Choose New Game, pick a slot and you land at the Camp menu; Adventure starts the first stage. The window letterboxes to portrait and never stretches. Mouse acts as your finger, and Esc is back or pause.

To publish a Release build to `C:\Apps\GridGame` instead, run `run.bat` (what the Desktop shortcut runs). It asks first: 1) Deploy & Run rebuilds, publishes and launches it; 2) Run launches the build already published. Unattended, run `tools\run.ps1` (add `-Launch` to start it afterwards, `-Force` to rebuild even when up to date).

## Commands

```powershell
pwsh tools/test.ps1              # rules + meta tests, bot soak over every stage, glyph and asset checks
pwsh tools/test.ps1 -Compile     # + Unity batchmode compile check (Editor must be closed)
pwsh tools/test.ps1 -Ui          # + build the player and run tools/uicheck.py against it
pwsh tools/build.ps1             # Windows development player -> Build/Windows
pwsh tools/build.ps1 -Release    # Windows Release player
pwsh tools/build.ps1 -Target Android                          # APK -> Build/Android
pwsh tools/build.ps1 -Target WebGL; node tools/webcheck.mjs   # browser build + Playwright check
run.bat                          # menu: 1) Deploy & Run (rebuild Release, publish to C:\Apps\GridGame, launch) 2) Run
pwsh tools/run.ps1 [-Launch] [-Force]   # the same deploy, no menu
tools\launch.bat                 # deploy, then pick Normal Play or Demo + Capture (muted by default)
pwsh tools/make-desktop-shortcut.ps1   # Desktop shortcut to run.bat (the menu)
```

Harness and checks (they launch the development player with its automation endpoint):

All of these launch muted by default (see Audio mute below).

```powershell
python tools/harness.py smoke          # title -> new game -> camp -> battle on Auto -> results
python tools/harness.py sweep          # screenshot + layout-audit every Camp screen
python tools/harness.py campaign [N]   # play N consecutive stages (default 6)
python tools/harness.py clips [N]      # N random-stage battle clips (needs -capture-config to record)
python tools/harness.py durability     # weapon-wear badge and shatter-warning check
python tools/harness.py bestiary       # bestiary drop check
python tools/harness.py stress [HOURS] # unattended soak (default 4h)
python tools/harness.py shot NAME      # screenshot the running player
python tools/harness.py call PATH k=v  # raw endpoint call, e.g. call state
python tools/uicheck.py                # behavioral UI assertions against the running player
python tools/androidcheck.py           # play a session on a device or emulator
python tools/records.py [dir]          # balance picture from JSON battle records
python tools/telemetry_report.py RUN   # endless-run telemetry report (win rates, heatmaps, outliers + repro seeds)
                                       #   -> artifacts/endless/RUN/report.md + report.html
```

Each worktree's harness gets its own port derived from its path; set `GRIDGAME_PORT` to override. Screenshots land in `artifacts/<run>/`.

Content and art tools: the art, audio and content authoring scripts (portraits, icons, tiles, sound effects, music,
lesson images, the weapon and counter-item generators, vendor tiers, lorem placeholders, the Midjourney male-variant
batch) live in the sibling **GridGame.Studio** repo's `tools/` with the Studio app (GG-T579, GGS-T9); they find this
checkout by themselves. The build gates stay here.

```powershell
python ../GridGame.Studio/tools/make_sfx.py         # regenerate the original sound effects
python ../GridGame.Studio/tools/import_art.py       # regenerate art and audio from the GridGame2026 reference project
python ../GridGame.Studio/tools/make_icons.py       # regenerate the pixel-art icons
python tools/check_assets.py     # verify every referenced asset exists
python tools/check_glyphs.py     # verify every UI glyph exists in the bundled fonts
```

## Player flags

| Flag | Effect |
|---|---|
| `-automation PORT` | Start the development-build HTTP control endpoint (tap, drag, state, screenshot, layout audit) |
| `-artifacts DIR` | Where automation screenshots and battle records go |
| `-demo` | Boot into an unattended self-play loop: random party, autoplayed battles, auto-advance |
| `-autobattle` | Start every battle on Auto |
| `-start SCREEN` | Open directly on a named screen |
| `-skill N` | Bot skill level for autoplay |
| `-records DIR` | Write JSON battle records to this folder |
| `-telemetry` | Enable bot telemetry |
| `-saveRoot DIR` | Keep every profile under `DIR/Profiles` instead of the real ones (the harness passes its run folder) |
| `-profile NAME` | Play as this player profile, creating it if missing (`-demo`/`-testmode` use a throwaway profile) |
| `-profiles-root DIR` | Unity.Shared's override for the Profiles folder itself; `MINDATTIC_PROFILES_ROOT` does the same |
| `-noDiagonalSlip` | Turn off diagonal slip |
| `-capture-config PATH` | Load MindAttic.Media.Capture trigger rules from this file |
| `-muted` | Mute all audio for this session only (see Audio mute below); `MINDATTIC_CLI_MUTED=1` does the same |

## How it works

```text
 Resources/Content/*.json --ContentLoader.Validate--> ContentDb
                                                         |
 player input / bot --> command --> BattleEngine.Apply --+--> events
                                    (Core, pure C#,          |
                                     seeded, deterministic)  v
                               BattleScreen plays events back as animations + sound
                                                         |
 Meta (pure C#): SaveData + camp services <--- results --+
```

| Layer | Where | What |
|---|---|---|
| Simulation | `Assets/GridGame/Core` | Pure C# with no `UnityEngine`. `BattleEngine.Apply(command)` returns events. Deterministic and seeded. Tuning numbers live in `Core/Battle/BattleConfig.cs`. |
| Bot | `Assets/GridGame/Core/Bot` | `HeroBot` drives soak tests, Auto mode, hints and `-demo`. |
| Meta | `Assets/GridGame/Meta` | Pure C# save model and camp services (shop, craft, repair, inn, equip, learn, party, summon, bounties); `CampRoutine` is the bot camp manager. |
| Content | `Assets/GridGame/Resources/Content` | 15 JSON files: every class, ability, buff, item, recipe, drop table, stage, campaign theme, trap and caravan upgrade. |
| App | `Assets/GridGame/App` | Unity presentation. One empty scene; `GameApp` builds everything in code with UI Toolkit and `Theme.uss`. |
| Automation | `Assets/GridGame/App/Automation` | Development-build HTTP endpoint for scripted play, input injection, screenshots and layout audits. |
| Build | `Assets/GridGame/Editor/GridGameBuild.cs` | Batchmode build and compile-check entry points used by `tools/unity.ps1`. |

No prefabs, no hand-built scenes, no Inspector wiring.

## Testing

`pwsh tools/test.ps1` runs the `Tests/Core.Tests` suite (880 tests on .NET 10, about three minutes): board rules, timeline and abilities, every catalog entry through the real engine, content validation, a bot soak over every stage with invariants checked after each command, deterministic replay, the full camp loop and a balance report. It then runs the glyph and asset checks. `-Ui` adds the player-level UI gate, and `tools/webcheck.mjs` and `tools/androidcheck.py` cover the WebGL and Android builds.

## Capture

GridGame records itself with [MindAttic.Media.Capture](https://github.com/mindattic/MindAttic.Media.Capture), through its Unity package `com.mindattic.media.capture` (MindAttic.Media.Capture.Unity, referenced from `Packages/manifest.json` as `file:../../MindAttic.Media.Capture/unity/com.mindattic.media.capture`). It auto-starts in Editor Play sessions and development builds. Its own README covers the engine, the rule format and the `capture` agent; this section is what GridGame does with it.

Prerequisites: the capture agent installed by MindAttic.Media.Capture's `run.ps1` (`%LOCALAPPDATA%\MindAttic\Capture\bin\capture.exe`; `MINDATTIC_CAPTURE_AGENT` points elsewhere) and ffmpeg. Recording needs a GPU, so none of this works with `--headless`.

**Player flags.** `-capture video hq` records the whole session (`campaign()` documents `-capture video screen 5` for recording a soak). `-capture-config PATH` loads trigger rules; `Assets/StreamingAssets/capture.json` (one clip from `battle-start` to `battle-end`) is never auto-loaded just because it exists, and it ships with `"enabled": false`.

**Where files land.** Recordings go to the session folder under `R:\MindAttic\Capture\GridGame.Unity\<session>\`; harness screenshots stay in `artifacts/<run>/`. Preview sets go under `R:\MindAttic\Preview\GridGame.Unity\`.

**Recorded harness runs: `--record`.** Anywhere in a harness command's args, `--record` adds `-capture video hq` to every launch and loads `tools/harness.capture.json`: a 30 s replay buffer, a `harness-fail` rule that saves the last 25 s plus 2 s after as a fail replay, and a `harness-step` rule that turns each step into a chapter marker (sweep, storyarc and coach raise them). When a command fails, by exception or exit 1 on a check, the harness raises `harness-fail`, takes a `fail-COMMAND` screenshot and waits for the replay before exiting. `endless` and `clips` keep their own configs.

```powershell
python tools/harness.py sweep --record    # session video with a chapter per step; a fail replay .mp4 if a check fails
```

**Preview sets: `--preview [TICKET]`.** When the command ends, pass or fail, the PNGs it wrote under `artifacts/<run>/` (captioned with their file names), any failure replay and screenshot, and with `--record` the session video become one preview set (`capture set new/add/show`), opened in MindAttic.Media.Viewer. A ticket ID files the set under that ticket. Without the agent the run only warns.

```powershell
python tools/harness.py coach --record --preview GG-T1008
```

**Per-battle clips.** `endless N --capture` writes an enabled copy of `capture.json` into the run folder and passes it with `-capture-config`, so every encounter becomes its own clip; the summary lists the clip folders. `clips [N]` uses the built player's `capture.json`, which needs `"enabled": true` to record.

```powershell
python tools/harness.py endless 20 --capture --seed 7
```

**The automation endpoint.** While a development player records, each automation `screenshot` drops a chapter marker named after its PNG, and `tap`, `click` and `drag` add one when called with `mark=1`. The `capture` endpoint drives the engine with `op=`:

| Op | Parameters | Does |
|---|---|---|
| `marker` | `name`, extra `k=v` as data | Chapter marker |
| `raise` | `event`, extra `k=v` as data | Fires a rule event, e.g. `harness-fail` |
| `screenshot` | `tag` | Screenshot into the session folder |
| `save-replay` | `tag` | Saves the replay buffer |
| `clip` | `before`, `after` (seconds), `tag` | Saves `before` seconds of buffer plus `after` seconds more |
| `status` | | Enabled, recording, session id and folder (the default op) |

Each answers `{ok, error?, folder?}` (and `file`/`files` for saved media); with capture off it answers `{ok:false, error:"capture off"}` at once.

```powershell
python tools/harness.py call capture op=clip before=10 after=2 tag=combo
```

## Audio mute

`tools/launch.ps1` (and `tools/launch.bat`) and `tools/harness.py`'s `launch()` launch muted by
default: Unity.Shared's shared session mute (GG-T205), set either with the `-muted` command-line
switch or the `MINDATTIC_CLI_MUTED=1` environment variable, read once by
`Unity.Shared.ProfilesBootstrap` before the first scene loads. `Assets/GridGame/App/Services/SessionAudio.cs`
then zeroes `AudioListener.volume` for that run; nothing is written to this game's own save data, so a
normal launch still has sound. Pass `-Sound` to `tools/launch.ps1` to opt back into audio for manual
play; the two switches work on their own against a built exe too:

```powershell
.\Build\Windows\GridGame.exe -muted
$env:MINDATTIC_CLI_MUTED = '1'; .\Build\Windows\GridGame.exe
```

GridGame's own music/SFX volume (`Assets/GridGame/App/Services/Audio.cs`, driven by `Meta.SettingsSave`)
does not yet read from Unity.Shared profiles (that migration is the separate, larger GG-T99); this mute
is a session-only override independent of it, reaching only `AudioListener.volume`.

## Limitations

- Story crawl text is lorem ipsum placeholder until the caravan story lands.
- No public build yet; Windows, Android and WebGL are built locally.
- Several systems are built but not yet covered by a targeted test (boss phases, snake chains, enemy pincers and AI weights, Clutch, time bank and more); the bible's verified-state section tracks which.
- Not real-time, not a roguelike, not a gacha and not multiplayer, by design.

## Documentation

- [docs/BIBLE.md](docs/BIBLE.md) — the design bible and verified state
- [docs/AMENDMENTS.md](docs/AMENDMENTS.md) — recorded deviations from the bible
- [docs/USER_STORIES.md](docs/USER_STORIES.md) — the story backlog and status, generated from the workflow database
- [docs/PLAYTEST.md](docs/PLAYTEST.md) — a fifteen-minute play-test checklist
- [docs/RELEASE_CHECKLIST.md](docs/RELEASE_CHECKLIST.md) — release-candidate gates, owner-only steps and night-only runs
- [docs/PIXEL_ICONS.md](docs/PIXEL_ICONS.md) — the pixel-art icon pipeline
- [AGENTS.md](AGENTS.md) — instructions and laws for coding agents working in this repo

## License

No LICENSE file: all rights reserved. Third-party music, fonts and icon sources and their licenses are listed in [Credits.txt](Assets/GridGame/Resources/Credits.txt): music by Kevin MacLeod (CC BY 4.0), fonts under the SIL Open Font License 1.1, and icons derived from game-icons.net (CC BY 3.0).

Part of [MindAttic](https://mindattic.com) — see more projects at [github.com/mindattic](https://github.com/mindattic). Related: [GridGame2026](https://github.com/mindattic/GridGame2026), the design reference this is rebuilt from, and [MindAttic.Media.Capture](https://github.com/mindattic/MindAttic.Media.Capture).
