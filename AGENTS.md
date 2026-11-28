# GridGame.Unity — agent entrypoint

Read the shared protocol at ..\mindattic-agent-standard\AGENTS.md, then this file, then docs/BIBLE.md.

## What this is
A clean rebuild of GridGame2026 (Terra Battle pincer combat + Grandia II timeline + FF-style camp).
D:\Projects\MindAttic\GridGame2026 is the frozen design reference — read it, never edit it.

## Laws (do not break)
1. **Core is pure C#.** `Assets/GridGame/Core` and `Assets/GridGame/Meta` have `noEngineReferences`; no `UnityEngine`.
   The battle changes only through `BattleEngine.Apply(command) → events`. Presentation never mutates state.
2. **Content is data.** Classes, abilities, buffs, items, recipes, drops, stages, campaign live in
   `Assets/GridGame/Resources/Content/*.json` and must pass `ContentLoader.Validate`.
3. **No editor work.** One empty scene; `GameApp` builds everything in code (UI Toolkit + `Theme.uss`).
   No prefabs, no Inspector wiring, never ask the owner to click around in the Unity Editor.
4. **Done = verified by play, not asserted.** Before claiming anything works:
   - `pwsh tools/test.ps1` (dotnet rules/meta/catalog/soak tests + glyph check) — green.
   - `pwsh tools/build.ps1` then `python tools/harness.py smoke|sweep|campaign` and LOOK at the screenshots
     in `artifacts/`. Unity batchmode needs the Editor closed — ask the owner to quit it, never kill it.
5. **Every effect plays out over time** (no single-frame swaps) and **every event has a sound**.
6. **The bible is the brief.** Gameplay changes update docs/BIBLE.md (+ docs/AMENDMENTS.md for deviations)
   and docs/USER_STORIES.md statuses. If a request contradicts the bible, say so and ask.
7. Commit at verified milestones only; end messages with the session attribution lines.

## Map
- `Core/Battle/BattleRunner*.cs` — the rules (setup, movement/pincers, abilities, timeline, enemy turns)
- `Core/Battle/BattleConfig.cs` — every tuning number
- `Core/Bot/HeroBot.cs` — bot player (soak tests, autoplay, hints); `Meta/CampRoutine.cs` — bot camp manager
- `App/Battle/BattleScreen*.cs` — HUD, drag input, event playback
- `App/Screens/*.cs` — title, camp hub, vendors, results; `App/UI/Ui.cs` — widget kit
- `App/Automation/AutomationServer.cs` — dev-build HTTP control (tap/drag/screenshot/state/layout)
- `tools/` — test, build, harness, asset and glyph checks (the art/audio/content authoring scripts and the thumbnail
  framer live in the sibling GridGame.Studio repo, GG-T579)

## Showing the owner something
The owner never plays to a point. Turn their words ("show me the first boss", "the Summon screen") into a scenario:
pick a preset or write a spec (every field optional; fill each gap yourself), then run
`python tools/scenario.py <preset|target> [--spec file.json] --device iphone17pro [--replace] [--build]`.
It launches a detached player on a throwaway "Scenario" profile at that device's size and safe area, leaves the window
open and adds a screenshot to a preview set. Say in one line what was set up. One game instance at a time (--replace).
- Presets (`tools/scenarios/*.json`): `overworld`, `first-boss`, `final-boss`, `camp-midgame`, `summon`, `caravan`,
  `skill-hex`, and per region `<theme>-start` / `<theme>-boss` (greenvalley, lanternroad, desert, swamp, cave, still,
  cityruins, cathedral, bloom, greenvalley2, cityruins2, swamp2, desert2, cave2, morrowsshadow).
- Spec fields (`Assets/GridGame/App/Dev/Scenario.cs`): `target` (first-boss, boss:N, boss:<class>, final-boss,
  stage:<id>, region:<theme>, screen:<id>, overworld), `party`, `level`, `progress`, `gear`, `caravan`, `gold`, `food`,
  `onTheRoad`, `carriedOrbs`, `allFeatures`, `settings`, `fullStage`, `realLaunch`.
