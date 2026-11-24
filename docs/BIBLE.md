---
codex: 1
project: GridGame
code: GG
layer: bible
status: living
updated: 2026-10-21
---

# GridGame — Project Bible

> **GG-T694 (2026-11-23):** the working title dropped "Gaiden" — this bible says GridGame throughout; past
> entries that quoted the old name verbatim as history are left as written.
>
> Single source of truth for what GridGame IS, is NOT, and the rules that keep it coherent. `README.md` says how to build/run; this says how to think about the system.
>
> GridGame is the from-scratch rebuild of GridGame2026 (declared unplayable). The old bible's mechanics are the target ("full bible parity"); the architecture is new and several owner decisions amend the old rules. Every amendment is marked **Amended 2026-10-09:** inline and summarized in [`AMENDMENTS.md`](AMENDMENTS.md). Anything the old bible specifies that the new code does not do yet is kept as spec and marked **⬜ Not yet built** — nothing is silently claimed. The parity backlog with per-story evidence is [`USER_STORIES.md`](USER_STORIES.md).
>
> The nine sections below are the L0 outline. The detailed design canon lives in [Appendix A — Design Canon](#gg-appendix-a) and is cited by `§`-anchor. Structured canon (classes, abilities, buffs, items, recipes, drops, stages, campaign, traps, bounties, summon, enchants) lives in [`Assets/GridGame/Resources/Content/*.json`](../Assets/GridGame/Resources/Content/) — prose cites entities by `Id` rather than restating every field.

---

## 1. The one sentence {#GG-§1}

A tactical grid RPG where **movement deals zero damage** — you drag heroes one tile at a time across a 6×8 grid, **sliding** displaced actors aside, to trap enemies in **pincers** (two heroes flanking an unbroken line) on an **act-advanced Timeline**, built as a pure-C# deterministic simulation that a code-built Unity 6 UI Toolkit front end plays back.

The feel target: *Terra Battle pincers + Grandia II timeline + Final Fantasy camp menu + FF8 draw economy.* See [§0 North Star](#0-north-star) for the design pillars.

## 2. The product promise {#GG-§2}

- **Position is the only weapon.** Damage is dealt by the *pincer* a new position completes, never by the move itself. Every drop asks "does this finish the line?" ([§1.2](#12-pincer-attacks))
- **The slide.** Dragging a hero onto an occupied tile displaces that actor — friend or foe — into the tile you just left. Set up flanks, eject allies, feed enemies into kill zones. ([§1.1](#11-sliding-and-displacement))
- **The Timeline runs while you hold a hero.** Every icon flows **right→left** along one bar of seconds and acts when it reaches the left edge, where a large diamond shows who acts next (**Amended 2026-11-06**, GG-T286/GG-T287, Final Fantasy Resonance–style: no Zone). The clock stands still while you look; picking up a hero starts it in real time, and your turn lasts until the next enemy icon reaches the left edge — then the hero drops where it is and that enemy acts (dropping earlier ends the turn early). A pincer pushes every foe it hits **1 s** back, and abilities with the **Pushback** keyword push up to 2 s, anywhere on the bar. ([§2](#2-the-timeline), GG-T188)
- **Shared, visible mana.** A 12-orb `ManaBank` of generic orbs is the whole party's spell budget; pincers, crits, steals and enemy-cast interrupts refill it. ([§3.1](#31-mana-the-orb-economy))
- **Casts ride the Timeline.** A spell with a cast time is a diamond on the same bar as the turns, born at its cast time, and fires when it reaches the left edge; every hit on the caster staggers it, enough stagger cancels it, and a rare LCK Clutch lets a dying healer let off one last miracle. ([§13.4](#134-the-interrupt-path))
- **Beyond the battlefield.** Five themed campaigns of hand-built stages with a skippable story crawl per theme, a Final Fantasy–style **Camp** menu (merchant, blacksmith, alchemist, inn, abilities, party, summon circle, bounties, bestiary), weapon durability with shatter rebound. **Amended 2026-11-07 (GG-T341):** the story is *Under Morrow* ([STORY.md](STORY.md)) — a crew of tithe-runaways in a stolen Concord caravan, under a moon that hangs too low and a Concord that drinks the holds dry; crawl text, NPC/hero join scenes, party talk and Advice quizzes are written from it (the GG-T120 story is retired). ([§22](#22-the-macro-loop), [§25](#25-the-camp-merged-hub), [§27](#27-story-crawl-no-dialog))
- **Story source (GG-T355, 2026-11-07).** All story, canon and player-facing narrative text is sourced from the Prose narrative engine (universe `gridgame`): the game handles gameplay, Prose handles narrative. The premise is now the owner's *the Manalith* canon (2026-11-21, amended GG-T577; Prose UniverseCanon `gridgame`, staging copy `Data/Prose/canon/`); *Under Morrow* above and in the glossary below, and *A War in Heaven* (v4), are retired history. **Phase 2 (done):** the game reads every story string from `Content/Text`, and the gameplay JSON holds none ([§27.2](#272-narrative-sourcing-prose-gg-t355)). How it flows, the key scheme and the build-time pull: [PROSE.md](PROSE.md).

Target pacing: a battle is 15–40 committed actions (~2–5 minutes), a stage ~3–4 min, a session 20–30 min. ([§0.3](#03-session-shape-target-pacing))

## 3. What it is NOT {#GG-§3}

GridGame is **NOT**:

- **Not real-time, and not alternating turns.** The clock runs only when you commit an action, so thinking time is free; but enemies act when their icons arrive, which may be zero, one or several times per action. **Amended 2026-10-09:** replaces "thinking time is game-clock time" ([§0.2](#02-non-goals-what-this-game-is-not)).
- **Not a deck-builder** — AbilityBars are deliberate loadouts, not randomized hands.
- **Not a roguelike** — no permadeath, no procedural runs; stages are authored, saves persist.
- **Not a gacha / live service** — no pulls, no energy, no premium currency. Heroes are recruited deliberately for gold at the Summon Circle. **Amended 2026-11-07 (owner, GG-T343):** *"another way is to simply summon them randomly with Diamonds monetized currency"* — the Summon Circle also offers one **random draw** for a Diamond ([§25.10.2](#25102-random-summon-for-diamonds-added-2026-11-07-owner-gg-t343)) beside the deliberate gold summon. ~~Diamonds still come only from the existing LuckyDiamond paywall hook (1 g · 1 s, [§25.18](#2518-rested-and-the-paywall-hooks-gg-t232)); there is no store, no real-money purchase and no pity system ("until I call it monetize, it's not").~~ **Amended 2026-11-20 (owner, GG-T455):** Diamonds have an exchange rate — 6 G each, 100 for $1.00 — and every Diamond price can be paid in gold; a Diamond Store sells flat-rate packs, not connected to real billing yet ([§25.18.1](#25181-the-diamond-exchange-gg-t455-owner-2026-11-20)). Still no pity system.
- **Not multiplayer** — solo offline; no co-op, PvP or leaderboards.
- **Not a grid-puzzle** — no match-3 piece-color matching; tile state is "who's standing there".
- **Not free-form movement** — one tile at a time, cardinal only, no pathfinding or diagonals.
- **Not a stat-spreadsheet** — if a player needs a wiki to build, the design failed.
- **No world map.** Narrative is a skippable per-theme text crawl ([§27](#27-story-crawl-no-dialog)) plus portrait dialogue at story beats between battles and from Camp's Talk row ([§27.1](#271-dialogue-gg-t117)); stage navigation is the Adventure list ([§28](#28-no-overworld)). **Amended 2026-10-19 (owner, GG-T117):** "no dialog system" withdrawn. **Confirmed 2026-10-20 (owner, GG-T115):** the separate overworld pitch ([design-draft-overworld-and-dialogue.md §2](design-draft-overworld-and-dialogue.md)) is withdrawn — a world map "takes away from a quick sit-down game." The game is four kinds of screen: fighting, in-town buying/upgrading, reading a text crawl, or talking with people (NPCs, party). **Amended 2026-11-02 (GG-T75):** the caravan crew's one-line barks on Camp and the Caravan screen ([§25.12.1](#25121-the-caravan-crew-and-their-barks-gg-t75-added-2026-11-02-owner-direction-2026-10-16)) are flavor text, not a fifth kind of screen and not dialogue: no branching, no choices, no overworld.

## 4. Architecture canon {#GG-§4}

**Amended 2026-10-09:** the builder/scene/manager architecture of GridGame2026 is gone. The game is a pure-C# simulation with an event-playback front end.

```
                    Unity 6000.4.3f1  ·  one near-empty scene: Assets/GridGame/Scenes/Main.unity
                                         (GameApp boots itself; nothing is authored in the scene)
 ┌───────────────────────────────────────────────────────────────────────────────────────────────┐
 │ GridGame.App (Assets/GridGame/App)  — Unity presentation only                                  │
 │   Boot/GameApp ─ builds camera, UIDocument + PanelSettings (1170×2532), letterbox, router      │
 │   UI/ScreenRouter, Ui, Tween      Screens/*Screen (UI Toolkit C# element trees + Theme.uss)     │
 │   Battle/BattleScreen(.Input/.Playback), BoardView   Services/Art, Audio, Fonts, SaveStore     │
 │   Automation/AutomationServer (dev builds), StartScreens                                       │
 └──────────────▲───────────────────────────────────────────────▲────────────────────────────────┘
                │ commands ↓  events ↑                          │ services
 ┌──────────────┴──────────────────────────┐   ┌────────────────┴──────────────────────────────┐
 │ GridGame.Core (noEngineReferences)      │   │ GridGame.Meta (noEngineReferences)            │
 │  Battle/BattleEngine.Apply(cmd)→events  │◀──│  SaveData, SaveMigrations                     │
 │  BattleRunner.* (rules), BattleState,   │   │  Progression, BattleFactory (save↔battle),    │
 │  Timeline, ManaBank, Unit, BattleConfig │   │  Shop, Crafting, Repair, Inn, Camp, Equipment,│
 │  Rules/PincerDetector, TargetResolver,  │   │  Abilities, Party, Summon, Bounties           │
 │        EnemyPlanner, Formulas           │   └───────────────────────────────────────────────┘
 │  Bot/HeroBot, BotRunner   Util/Rng,Coord│
 │  Content/ContentLoader, ContentDb, ContentModels ◀── Resources/Content/*.json              │
 └─────────────────────────────────────────┘
          ▲ compiled and run outside Unity by Tests/Core.Tests (NUnit, dotnet test)
```

### 4.1 Projects / assemblies

- `GridGame.Core` (`Assets/GridGame/Core/GridGame.Core.asmdef`, `noEngineReferences: true`) — the battle simulation, rules, content model, bot. Only dependency: `Newtonsoft.Json.dll`.
- `GridGame.Meta` (`noEngineReferences: true`, references Core) — save model and every Camp service.
- `GridGame.App` (references Core, Meta, `Unity.InputSystem`) — presentation, input, audio, automation.
- `GridGame.Editor` (Editor only) — `Editor/GridGameBuild.cs`: batchmode entry points (`EnsureScene`, `BuildWindowsDev`, `BuildWindowsRelease`, `BuildAndroid` (emulator APK), `BuildAndroidRelease` (the Google Play .aab, GG-T478, [RELEASE_BUILD](RELEASE_BUILD.md)), `BuildWebGL`, `CompileCheck`, `ReimportArt`). This file is the only "scene authoring" the project has.
- `Tests/GridGame.Core/GridGame.Core.csproj` — compiles the Unity source of Core + Meta as a plain .NET Standard 2.1 library (proving [GG-LAW-9](#GG-LAW-9) on every build); `Tests/Core.Tests/Core.Tests.csproj` — the NUnit suite on top of it, run with `dotnet test` (no Unity needed).

### 4.2 Domain model — the NOUNS

- **Unit** — `Core/Battle/Unit.cs`: hero or enemy (`Team`), `Def` (`ClassDef`), level, `LevelStats` + `GearStats` → `Effective` stats, HP, footprint (`W`,`H`), buffs, cooldowns, `Bar` of 5 `AbilitySlot`s, weapon state, snake-chain links, boss phase, `DamageDealt` (threat). ([§3.2](#32-hp-and-stats))
- **ClassDef** — one entry of `classes.json` (172: 62 hero classes, 110 enemy classes). ([§23](#23-character-classes))
- **AbilityDef** — `abilities.json` (25: 15 spells, 6 skills, 4 enemy charges). ([§7](#7-the-spell-catalog))
- **BuffDef / BuffInstance** — `buffs.json` (10). ([§8](#8-buffs-and-debuffs))
- **ItemDef** — `items.json` (225: 155 equipment, 54 materials, 16 consumables). ([§24](#24-equipment-items-materials-currency))
- **ManaBank** — `Core/Battle/ManaBank.cs`: a capped count of 12 generic orbs. ([§3.1](#31-mana-the-orb-economy))
- **TimelineEntry** — `Core/Battle/Timeline.cs`: a Turn icon (enemy, integer IP 0..1000) or a Cast icon (remaining ticks). ([§2](#2-the-timeline))
- **BattleState** — `Core/Battle/BattleState.cs`: units, timeline, traps, bank, wave, phase, clock, seeded `Rng`, accumulated `BattleResults`. Cloneable (previews, bot look-ahead).
- **SaveData** — `Meta/SaveData.cs`: one save slot. ([§15](#15-save))

### 4.3 Key services — the VERBS

- `BattleEngine.Create(content, setup, events)` — builds a battle from a `BattleSetup` (stage, party `HeroSpec`s, seed, unlocked slots, recommended level).
- `BattleEngine.Apply(state, command)` → `List<BattleEvent>` — **the only way a battle changes.** One committed command resolves, the clock advances, arrivals act, control returns. ([§4.5](#45-the-command--event-contract))
- `BattleEngine.PreviewMove / ValidTargets / GetSlotStatus / TicksUntilTurn / ActionsUntilTurn` — read-only queries the UI and bot use (preview runs on a cloned state).
- `PincerDetector.Detect(state, team, dropped)` — board-wide pincer scan, chain ordering, supporters. ([§1.2](#12-pincer-attacks))
- `TargetResolver.Tiles / Collect / IsValidPick` — the (shape × mode × filter) triad. ([§5](#5-targeting))
- `BattleRunner.ResolveAbility` — the dispatcher; adding a spell never touches it. ([§6](#6-the-spell-dispatcher))
- `EnemyPlanner.PickTarget / PlanStep / PlanCast / LineToward / FootprintStepLegal` — positional AI. ([§14](#14-ai))
- `Formulas` — stats-at-level, HP, hit/crit, physical/magic/spell damage, cast time, XP curve. ([§13.1](#131-damage-formulas))
- `BattleFactory.Build / Commit` — save → battle setup, finished battle → save. ([§22.1](#221-per-transition-state-contract))
- `HeroBot.Decide` / `BotRunner.Play` — a player stand-in that drives the real command API (soak tests, in-game Auto battle, first-battles hints).
  - **It uses the whole bar (GG-T246, 2026-11-05, owner: "the Abilities need to be utilized in the gameplay demos, to see if they are useful or not"):** a spell's mana price falls as the shared bank fills (`OrbPenalty`: 1.5 per orb when empty, down to 0.3 near full and 0.2 when full, since every orb minted into a full bank is lost). Every effect is valued from its own numbers:
    - foe debuffs by what the BuffDef does (halt/immobile, slow, vulnerability, blind, silence, weakness, damage over time)
    - ally buffs by damage-up, damage-taken-down on an exposed ally, regen on a hurt one, haste
    - knockback (more when it peels a foe off a hurt hero), pull, swap, step-back when hurt and engaged
    - revive with someone down, ethers only into a bank with room, single-buff cures (the first-scan-per-class score went with Scan, GG-T290)

    A clean pincer stays the strongest move, so abilities win only on real value. Measured with `AbilityUsageSoak` + `records.py --abilities` (§22.6, `docs/research/AbilityUsage.md`).
  - **It saves for a big spell (GG-T373, [§3.1.1](#311-costs)):** while a living hero's unlocked bar holds a spell of 5+ orbs that the bank can hold but not yet pay for (`HeroBot.SavingFor`, the nearest such cost), every cheaper spell pays an extra 0.6 per orb (casual bot 0.3). Only a cheap spell worth clearly more than its price goes off, and the bank climbs to the big one. The base `OrbPenalty` keeps the big spell itself affordable to the bot: at most about 5.5 for any cost once it can be paid. Unmeasured, pending `/test`.
- `CampRoutine.Run` (Meta) + `MetaHeuristics` (Core/Bot/MetaBot.cs) — "what a sensible player does at Camp" (repair, rest, recruit into an open party slot, forge/equip better gear, learn a spell, fill empty slots, restock potions) through the same services the screens call; drives the campaign balance simulation.

### 4.4 Content is data {#GG-§4.4}

**Amended 2026-10-09:** every class, ability, buff, item, recipe, drop table, stage and campaign is JSON under `Assets/GridGame/Resources/Content/`, loaded by `ContentLoader.Load` (Newtonsoft, `MissingMemberHandling.Error`, string enums) and cross-checked by `ContentLoader.Validate` at boot (problems are logged by `GameApp.LoadContent`; the test suite fails on any).

| File | Holds |
|---|---|
| `classes.json` | 172 `ClassDef`s — stats, growth, milestones, tags, resistances, learnable abilities, default loadout, drop table, move range, XP reward, boss phases, charge ability/shape, trap, snake segments, footprint, lore/card text, portrait crop |
| `abilities.json` | 25 `AbilityDef`s — cost letters, cast time, cooldown, shape/radius/mode/filter, motion, damage type/base, heal, buff + chance, cleanse, steal, hasten, Pushback (seconds), teleport, VFX/SFX keys (Scan retired, GG-T290) |
| `buffs.json` | 10 `BuffDef`s — kind, unit (Turns/Ticks), duration, DOT, immobile, halts timeline, breaks on damage/move, incoming-damage / timeline-speed / accuracy multipliers, blocks spells, expire chain, letter, color |
| `items.json` | 231 `ItemDef`s (155 equipment, 55 materials, 18 consumables, 3 recipe scrolls; GG-T45 added `mat_pure_slime_gel` + Greater Healing Potion/Antidote) — type, rarity, slot, cost, stack, durability, healing/damage, stats, battle-start orbs, on-use ability, orb grants, cures, revive, escape, camp-only, bonus-vs-tag, resistances, vendor flag |
| `recipes.json` | 142 `RecipeDef`s — station (Forge / Alchemy / Enchant), inputs, gold, optional base item, result; some Alchemy recipes also carry a potency variant ([§24.6.4](#2464-reagent-potency-gg-t45-owner-direction)) |
| `droptables.json` | 37 `DropTableDef`s — optional coin range, weighted item entries (chance, min, max); one per enemy family, each carrying that family's own signature material (added 2026-10-15, GG-T43) |
| `stages.json` | 19 `StageDef`s — 15 campaign + 4 test (`IsTest`), theme, waves of `SpawnDef` (class, level, optional fixed tile); a wave may instead name an encounter group (`WaveDef.Group`) |
| `encounters.json` | 182 `EncounterGroupDef`s (GG-T184) — one enemy family's mix on fixed enemy-half tiles (a named formation: Pincer, Wedge, Crown ...) at a level; 2-5 foes, every Crucible family has a group of every wave size. `EncounterCatalog.Validate`: no two groups are the same foes on the same tiles, no two campaign waves are the same foes, levels and tiles, a group backs one campaign wave at most |
| `campaign.json` | 5 main themes × 3 stages + 5 postgame themes × 2 bonus stages (added 2026-10-13, GG-T7/T10; each bonus stage carries a `StageDef.LevelOverride` instead of relying on flattened-index `RecommendedLevel`), in play order, with each theme's story-crawl text |
| `traps.json` | 1 `TrapDef` (`venom_snare`: 6 dmg + Poisoned, lay chance 0.45) |
| `bounties.json` | 12 `BountyDef`s — a titled marked variant of an enemy class, its stage and wave, an optional rule-break trait, item reward, optional skill points (GG-T431) |
| `summon.json` | Summon Circle pool (7 classes), base price 250, step 250 |
| `enchants.json` | 6 `EnchantDef`s (Flame/Frost/Spark/Shadow/Poison/Holy, the last 2 added 2026-10-13) — reference data; the actual enchant crafts are `Station: Enchant` recipes |

### 4.5 The command → event contract

A **command** (`Core/Battle/Commands.cs`) is a player decision: `MoveCommand` (hero id + path of cardinal steps + drag-timer seconds left), `UseSlotCommand` (hero, slot, target tile) or `ShieldCommand`. `BattleEngine.Apply` resolves it synchronously and returns an ordered list of `BattleEvent`s (`Core/Battle/BattleEvents.cs`: `Stepped`, `Displaced`, `PincerFormed`, `Attack`, `Damaged`, `Healed`, `Died`, `Loot`, `OrbMinted`, `OrbsSpent`, `TimelineAdvanced`, `PushedBack`, `Hastened`, `TurnStarted`, `TurnSkipped`, `CastQueued`, `CastStaggered`, `CastCancelled`, `Clutch`, `AbilityResolved`, `AbilityRefused`, `ItemUsed`, `BuffApplied/Ticked/Removed`, `Announce`, `TrapPlaced/Triggered`, `WeaponWorn/Shattered`, `BossPhase`, `WaveStarted/Cleared`, `Revived`, `BattleEnded`, `PlayerControl`). The simulation has already finished when the view sees them; the view only plays them back (`BattleScreen.Play` in `App/Battle/BattleScreen.Playback.cs`, the event player). A refused command emits `AbilityRefusedEvent` and changes nothing — no clock, no cost. The same seed + content + command list always reproduces the same event list.

## 5. The Laws {#GG-§5}

This project **inherits the org-wide house rules** in [`../../MindAttic.HouseRules.md`](../../MindAttic.HouseRules.md) — do not restate them here. The most load-bearing for this repo: [`HOUSE-LAW-1`](../../MindAttic.HouseRules.md#HOUSE-LAW-1) whole-number versioning (`VERSION` → `PlayerSettings.bundleVersion`), [`HOUSE-LAW-2`](../../MindAttic.HouseRules.md#HOUSE-LAW-2) soft-disable over delete, [`HOUSE-LAW-8`](../../MindAttic.HouseRules.md#HOUSE-LAW-8) verified-not-asserted done. GridGame ships **zero LLM features** — nothing AI lands in `Assets/`.

Project-specific laws:

### {#GG-LAW-1} Movement never deals damage
Sliding a hero through or onto an enemy is **not** an attack. Damage flows ONLY through pincers ([§1.2](#12-pincer-attacks)), enemy attacks, abilities/items ([§6](#6-the-spell-dispatcher)) and environmental sources (DOT, traps, weapon shatter). *(Source: `BattleRunner.WalkPath`; test `Movement_never_deals_damage`.)*

### {#GG-LAW-2} A pincer is two allies flanking an unbroken, ally-free line
Same row OR column, every tile strictly between them covered by opponents — no gaps, no allies, at least one opponent. No diagonal pincers. A 2×2 boss covering several line tiles counts as one opponent. **Amended 2026-11-06 (owner, GG-T289/GG-T294):** the two allies must be **pincer-eligible** (`Unit.CanPincer`): heroes always; an enemy only if it is **Sentient** — it has a society or plans ahead, however rudimentary. Beasts, slimes, yetis, scorpions and mindless undead never pincer; it is about sentience, not undeath ([§1.3](#13-sentient-restriction)). *(Source: `PincerDetector.TryPair`.)*

### {#GG-LAW-3} ~~Code is the source of truth; the `.unity` is the print-out~~ — RETIRED
**Amended 2026-10-09:** there are no scene builders, no `BuilderAutoRebuild`, no `BuilderDriftChecker`. The one scene is an empty shell; `GameApp` builds everything at runtime. Superseded by [GG-LAW-9](#GG-LAW-9).

### {#GG-LAW-4} ~~Guardrails are enforced at pre-push~~ — RETIRED
**Amended 2026-10-09:** the `SerializedFieldBan`, `ResourcesLoadBan` and `InstantiateBan` guardrails are gone. There are no prefabs and no Inspector wiring to guard; `Resources.Load` is the sanctioned loader for content JSON, art, audio, fonts and the UI theme. Superseded by [GG-LAW-9](#GG-LAW-9)–[GG-LAW-12](#GG-LAW-12).

### {#GG-LAW-5} Every spell is a (shape × mode × filter) triad
Adding a spell is a new `abilities.json` row picking targeting axes and effect knobs; it never touches `BattleRunner.ResolveAbility`. *(Source: `TargetResolver.cs`, `BattleRunner.Abilities.cs`; test `Every_hero_ability_resolves_against_a_live_board`.)*

### {#GG-LAW-6} Mana is one shared, visible, capped pool
A single 12-orb `ManaBank` for the whole party — no per-hero MP. Over-minting past 12 fades out (felt as "leaving value on the table"). **Amended 2026-11-02 (GG-T189):** orbs have fractional progress — the one forming bubble (`BattleState.ManaCharge`, [§3.1.9](#319-mana-charge)) fills toward the next orb; a bubble that rolls over while the bank holds 12 mints an orb that fades like any other, keeping the remainder. *(Source: `ManaBank.Add`, `BattleRunner.AddCharge`; tests `Bank_overflow_fades_extra_orbs`, `Guard_bubble_rolls_into_an_orb_and_fades_when_the_bank_holds_12`.)*

### {#GG-LAW-7} Verify-then-checkpoint; the bible is the brief
Land a feature end-to-end and verify it before committing — no mid-phase commits. If code and bible disagree, reconcile in writing. **Amended 2026-10-09:** the old "every system ships a `DebugManager.Demo_*` button" rule is replaced by harness evidence ([GG-LAW-12](#GG-LAW-12)) — a scripted automation step or a test is the demo.

### {#GG-LAW-8} Portrait-mobile is locked; never stretch
The UI is a fixed 1170×2532 design stage. **Amended 2026-10-09:** `GameApp.FitStage` (not the old `AspectGuard`) scales the stage to the limiting axis and centers it; the rest of the screen is black bars. The game never stretches or squashes. **Amended 2026-10-21 (GG-T169, owner: "on-device experience is king"):** 1170×2532 is now the *minimum* stage, not a fixed one — phones from 9:21 to 9:16 fill the whole screen (the stage grows taller or wider to match), and only shapes outside that range (tablets, ultra-tall) get bars, with an outline frame around the stage ([§26.3](#263-adaptive-stage-gg-t169)). Still never stretched or squashed. *(Source: `App/Boot/GameApp.cs`.)*

### {#GG-LAW-9} Core has no UnityEngine
`GridGame.Core` and `GridGame.Meta` are `noEngineReferences: true` and must compile and run under plain `dotnet test`. All randomness goes through the seeded `Core/Util/Rng` (xoshiro128\*\*); no `System.Random`, no time reads, no static mutable battle state. The presentation never decides a rule; it sends commands and plays back events. *(Source: asmdefs; test `Battles_replay_identically_from_seed`.)*

### {#GG-LAW-10} Content is JSON validated at load
Game content lives only in `Resources/Content/*.json`. `ContentLoader.Load` rejects unknown fields, empty or duplicate ids; `ContentLoader.Validate` resolves every cross-reference (classes, items, abilities, buffs, drop tables, traps, stages, themes, summon pool, enchant essences). A content change is done only when `Real_content_loads_and_cross_references_resolve` is green.

### {#GG-LAW-11} Every glyph the UI prints must exist in the bundled fonts
`tools/check_glyphs.py` scans every non-comment line of `App/**/*.cs`, every content JSON and `Credits.txt` for non-ASCII characters and fails if any is missing from Outfit → Noto Sans → Noto Sans Symbols 2. It runs inside `tools/test.ps1`. No tofu boxes ship.

### {#GG-LAW-12} "Done" requires harness evidence
A story is ✅ only with evidence a script produced: a named test in `Tests/Core.Tests` (rules, meta, catalog, bot soak), and for anything visible, an automation-harness step (`tools/harness.py smoke|sweep`, screenshots + layout audit in `artifacts/<run>/`). Asserting "it works" without a test or a harness run is not done ([HOUSE-LAW-8](../../MindAttic.HouseRules.md#HOUSE-LAW-8)).

### {#GG-LAW-13} Multiplayer-ready
This game is built so co-op or PvP can be added without re-architecting, per Unity.Multiplayer's [Multiplayer-ready standard](https://mindattic.github.io/Unity.Multiplayer/articles/multiplayer-ready.html) ([UM-LAW-4](https://github.com/mindattic/Unity.Multiplayer/blob/main/docs/BIBLE.md#um-law-4)): every player intent is a serializable command stamped with a player id and submitted through `ISession`; one authority owns game state and presentation only reads state and events; there is no global "the player", only player ids and `ISession.LocalPlayer`; rules are deterministic (seeded RNG, fixed tick, no `UnityEngine.Random` or frame time); entities have stable ids; state and commands serialize; the game runs on a `LocalSession` behind `ISession`; and a test plays commands from two player ids. A change that breaks any of these is a bug. No netcode package is in the project (owner 2026-10-21, GG-T192: groundwork only).

How Gaiden meets it (audit 2026-11-02, GG-T192): **commands** — `MoveCommand` / `UseSlotCommand` / `ShieldCommand` are plain data (hero id, tiles, slot) carrying `BattleCommand.Player` (a `PlayerId` seat), round-tripped by `CommandCodec`; **pipeline** — `BattleScreen.Commit` stamps `ISession.LocalPlayer` and submits to `LocalSession`, which applies one queued command per session tick through `BattleEngine.Apply` and raises its events; **authority** — `BattleState` + `BattleEngine` in engine-free Core ([GG-LAW-9](#GG-LAW-9)); each hero has an `Owner` seat and the engine refuses (`AbilityRefusedEvent`, "Not your hero") a command for a hero another seat owns, while party-wide Guard is open to every seat; **no global player** — the single-player game is seat 0 everywhere, and only presentation asks `LocalPlayer`; **determinism** — `Rng` (xoshiro128\*\*) seeded in the state, the act-advanced clock ([§2.0](#20-the-act-advanced-clock)) is the rules' fixed tick, no frame time in rules; **stable ids** — `Unit.Id` assigned by the authority in setup order; **serialization** — setup + seed + `LocalSession.CommandLog` replays a battle exactly (`LocalSession.Replay`), and `BattleRecorder.Snapshot` is the state's JSON view. *(Tests: `MultiplayerReadyTests` — two seats, wrong-seat refusal, command JSON round-trip, replay equality, one-seat session identical to the engine.)* Known gaps, out of scope here: the meta/camp layer is one save profile (fine for battle co-op, not for split campaigns), and `BattleState` has no full load-from-snapshot yet (replay from the command log is the restore path).

### {#GG-LAW-14} Polish never regresses
"Polish" has one official, measured definition: [`docs/POLISH.md`](POLISH.md) — eight dimensions (type, color, layout/touch, information/feedback, motion/VFX cohesion, audio, balance/pacing, art cohesion), each rule with an ID (`POL-A1`…), a numeric threshold and a source (Apple HIG, Material 3, WCAG 2.2, Nielsen, Fitts/Hick, the 12 principles of animation, EBU R128). Every test cycle runs the polish pass (`tools/polish.py`) and a review checklist against the recorded clip; **no dimension score may drop** below `docs/polish/baseline.json`, a drop is a `fix` ticket worked first, and the three lowest rule groups become the next cycle's tickets. Thresholds only tighten; loosening one is an owner decision in `docs/AMENDMENTS.md` (owner 2026-11-05, GG-T249).

## 6. Verified state {#GG-§6}

> Status legend: ✅ done (verified) · 🟡 partial · ⬜ planned · living.

**Build/test environment:**

- **Engine:** Unity `6000.4.3f1` (`ProjectSettings/ProjectVersion.txt`).
- **Rules + meta suite:** `pwsh tools/test.ps1` → `dotnet test Tests/Core.Tests` then `tools/check_glyphs.py`. `-Compile` adds a Unity batchmode compile (`GridGameBuild.CompileCheck` via `tools/unity.ps1`, which refuses to run while the project is open in the Editor). Last run (2026-10-09): **84 tests, 84 passed, ~36 s.**
- **Suites** (`Tests/Core.Tests/`): `BoardRulesTests` (slide, pincer geometry, chains, supporters, 2×2 boss, waves, victory/loot), `TimelineAndAbilityTests` (act-advanced clock, enemy turns, shield, pushback, spells, cast queue + cap, Frozen/Wet/Steam/Shock, potion, teleport, Quicken, enemy charge, Sleep, weapon wear, Double Attack / Counter passives), `CatalogTests` (every hero ability, every enemy charge, every consumable's effect, every recipe, every class through the real engine; Smoke Bomb escape; Camp potion/Tent), `ContentAndSoakTests` (content validation, bot soak over every stage, deterministic replay), `MetaLoopTests` (new game, save round-trip, full loop through every Camp service, defeat), `BalanceReport` (plays the whole campaign with natural progression for greedy / casual / button-masher bots, with and without `CampRoutine`, and prints a win% / actions / HP-left table per stage; asserts a greedy bot can finish), shared fixtures in `TestKit.cs` (`Mini()` content, `AssertInvariants`).
- **Bot soak:** `Bots_play_every_stage_without_breaking_invariants` plays all 19 stages × 12 seeds at bot randomness 0 and 0.6 (300-action cap) asserting structural invariants after every command (HP in range, one unit per tile, in bounds, bank ≤ 12, no timeline entry owned by a dead unit, both teams alive while the battle runs). `BotRunner` throws on a soft-lock (6 refusals in a row).
- **Automation harness:** dev players compile in `AutomationServer` (localhost HTTP, `-automation <port>`); taps/drags/presses are dispatched straight into the UI Toolkit panel, so they land whether or not the player window has OS focus, and reply `ok:false` with an error when they hit nothing or never reach the panel (GG-T121); `tools/harness.py smoke` plays title → new game → camp → Adventure → crawl → battle on Auto → results; `sweep` screenshots and layout-audits 15 Camp screens. Artifacts: `artifacts/smoke1/`, `artifacts/sweep/`, `artifacts/run1/` (ad-hoc drag/pincer captures).
- **`-demo`:** boots straight into a scripted self-play loop with no harness attached — character select (auto-picks the starting three at random, §25.15; amended 2026-10-19, GG-T118) → crawl (auto-skipped) → battle (autoplayed) → results (auto-advances to the next stage, or retries on defeat) → …, restarting with a fresh save and a fresh random party when the campaign ends or nothing is launchable (`GameApp.DemoMode`). Formalizes what `tools/harness.py campaign` drives over HTTP as a startup flag, for unattended soak-play or a demo reel.
  - **`-demo -endless` (added 2026-11-02, GG-T57):** no Title, Hub, Camp or crawl — boots straight into generate encounter (`EncounterGenerator`, GG-T55: a synthetic 5-wave `endless-` stage) + a level-appropriate geared party for it (`EncounterParty`, GG-T56) → battle (autoplayed) → results (auto-advance, win or lose) → the next fresh encounter, forever (`GameApp.EndlessDemo`). `-seed S` sets the base seed (default: the clock) and encounter *i* (0-based) uses seed **S+i**, so any encounter of a run can be reproduced on its own; `-count N` stops cleanly (quits the player) after N encounters. Each encounter runs on its own throwaway in-memory save on slot -1 (`Persist` never writes it — no real save slot is touched; the battle is still committed to it so the results screen shows), and the previous encounter's synthetic stage is pruned from the live `ContentDb` before the next is registered, and the last one on stop (the GG-T23 rule). One `[EndlessDemo] encounter N seed S: …` line per launch and one result line per encounter.
    - **Reproduce one encounter by seed (added 2026-11-02, GG-T64):** a battle record's `seed` (and the seed in the launch line) *is* the encounter seed, so `-demo -endless -seed S -count 1` replays exactly the encounter a record or report flagged: the same location, waves, levels, party and gear (`Meta.EndlessEncounter.Build(content, S)` — the one pure seed → setup build both the player and Core use; it prunes any earlier `endless-` stage first, because the generator borrows its location from any non-test stage), the same battle RNG (`BattleSetup.Seed` = S), and the autoplay bot's RNG seeded from S (`BattleScreen.BotSeed` = S, i.e. `EndlessEncounter.Bot(S)`; the view's `UnityEngine.Random` is also seeded with S at launch). Headless, `EndlessEncounter.Replay(content, S)` plays the same encounter in Core with that bot and returns outcome, turns (commands), wave and clock — deterministic per seed, so a flagged outlier becomes a debuggable repro in a test. The player's simulated cursor still runs on frame timing, so a sub-100 `-skill` (drag overshoots, slip attempts) can diverge from the headless replay; at the default skill 100 the decisions are the same. *(Verified: `EndlessEncounterTests` — two builds from one seed are identical even with another encounter in between; two headless replays of a seed give the same outcome, turn count, wave and clock.)*
    - **Stall guard (added 2026-11-02, GG-T61; caps moved to `BattleConfig` 2026-11-22, GG-T588):** an endless encounter never hangs the loop. One that has taken more than `BattleConfig.StallActionCap` (600) player actions (one -demo battle needed 412), run more than `StallRealSecondsCap` (900 s) of real time, or sat `StallIdleSecondsCap` (75 s) with no action taken and nothing animating is ended as **stalled** (`App/Boot/EndlessStability`, `BattleScreen.EndStalled`): no further command is accepted, its battle record is saved, its telemetry session closes with a `stalled` event and outcome (reason, seconds, actions, ticks, wave, units standing), the result line says `stalled`, and the loop goes on to the next seed. `-stallTurns N` / `-stallSeconds S` override the first two for a run. The guard is view-side and draws no randomness, so battle determinism is untouched. For GG-T21 (a player that vanished with no log), the run rewrites `endless-heartbeat.txt` every 5 s (encounter in flight, memory, last sample) and logs unhandled exceptions, Unity's low-memory signal and the quit.
- **`-testmode` (GG-T93, owner 2026-10-19, bot-only):** self-plays the whole *mechanical* loop end to end with no story, crawl, overworld or tutorial — a throwaway save on slot -1 (never written; `TestLoop.NewSave`: the small starting party, tutorial done, every crawl seen, every Camp NPC joined (GG-T119: the headless beats would bring them too, but cycle 1 shouldn't wait for them); **amended 2026-10-19, GG-T118:** no story is shown, but `TestCycle.Begin` plays each stage's pending story beats headlessly (`DialogueRunner.PlayPendingBeatsHeadless`, first choice) so the story recruits still join, and the cycle line reports them as `story: …`) → launch a stage → battle (autoplayed) → results → **back to the Hub every fight** → `CampRoutine` (salvage/repair, Inn, forge/buy/equip, learn + assign, potions) → launch again, forever (`GameApp.TestMode`). Unlike `-demo` it visits camp and spends between every battle. Difficulty follows the party: on a `SaveData.TestMode` save `Progression.StageRecommendedLevel` returns `Progression.PartyLevel` (rounded average hero level) instead of `RecommendedLevel(campaignIndex)`/`LevelOverride`, and spawns stay max(authored, recommended), so enemies climb as the party levels. Stage pick (`TestLoop.PickStage`): the active expedition's next battle, else battle 1 of the highest unlocked theme, stepping back one theme per 3 losses in a row (a lost expedition restarts its theme). One `[TestMode] cycle N | stage … | party Lv a->b | enemy Lv … | result | +XP | +gold | mats … | camp gold x->y: <every CampRoutine action>` line per cycle; the automation `state` dump carries `testmode.cycles`. *(Verified: `TestModeTests` — enemy level rises with party level in test mode and is unchanged otherwise, stage picking, a 4-cycle headless loop; `tools/harness.py testmode N` runs the real player and screenshots each cycle's battle, level-up, results and camp into `artifacts/testmode/` plus `testmode.log`, checking every cycle logged, battle/results/hub seen per cycle, party level never falling and zero errors.)*
- **Tile-invariant safety net (added 2026-10-11; root causes closed 2026-10-19, GG-T97):** two actors never share a tile once a slide is over — see [§1.1.3](#113-one-actor-per-tile--the-occupancy-guarantee-added-2026-10-19-owner-direction-gg-t97) for the rule and the guarantee per path. Every position write goes through `Relocate` / `MoveMovable` / `AddUnit`; `EnforceTileInvariant` (after every command and `BattleEngine.Create`) is only the last-resort net: in release it self-heals and logs a `TileConflictEvent` (error log, dev-build banner, `board_audit.tile_conflicts`); under `BattleEngine.StrictTiles` — on for the whole test suite — it throws, and the check also runs after every enemy turn, cast and reinforcement inside a command. Root causes closed by GG-T97: a snake head stepping onto the amplifier left it under segment 1; a Hopper leap didn't check movables; a Revival Charm on a full board was spent for nothing; a reinforcement with no room was silently dropped (it now waits); an egg's event reported its intended tile even when it spawned elsewhere; a hero start with no free tile fell back onto the taken one; and the view placed spawned/displaced tokens at their end-of-command tiles mid-playback. The strict soak (every stage × 24 seeds × 2 bots, 1500-action cap, plus 2500-action Crucible runs) and the 191 battle records on disk (2026-10-19: none contains a `TileConflict` event or an overlapping state) show no remaining occurrence.

**Proven working** (evidence per story in [`USER_STORIES.md`](USER_STORIES.md)):

- ✅ Board rules: slide/displacement, edge stop, no-op drag cancel, pincer geometry, chains, supporters, 2×2 immovable + width pincer, waves, victory/defeat (`BoardRulesTests`).
- ✅ Act-advanced timeline: clock only on committed actions, enemy turns on arrival, Pushback keyword (no Zone since GG-T286), Quicken, Shield, Frozen halt (`TimelineAndAbilityTests`, `TimelineResonanceTests`).
- ✅ Mana: shared capped bank of generic orbs, pincer mint, supporter mint, crit bonus orb, overflow.
- ✅ Every hero ability, enemy charge, recipe and class resolves through the real engine (`CatalogTests`).
- ✅ Determinism and bot soak across all content (`ContentAndSoakTests`).
- ✅ Camp loop: new game, save round-trip, battle commit, merchant, forge, equip, repair, inn, learn + assign, summon, party cap, bounty (`MetaLoopTests`); every Camp screen renders (harness sweep).

- ✅ Every consumable has a working battle effect or is camp-only; Smoke Bomb escape; Camp item use.

**Built but not yet asserted** (🟡 — see USER_STORIES): boss phases, snake chain, traps, enemy pincers and AI weights (soak-only), Clutch, time bank, battle-start orbs, buff tick timing details, wounds carry-over, first-battles hints, colorblind/reduce-motion.

**Verification gates (2026-10-10):** `tools/test.ps1` (152 dotnet tests + glyph + audio checks; `-Ui` adds 45 player checks), `tools/webcheck.mjs` (WebGL, 4 checks), `tools/androidcheck.py` (Android emulator, 7 checks); earlier `tools/test.ps1` (148+ dotnet tests incl. every ability/charge/item/recipe/class, bot soak, balance report), `tools/uicheck.py` (35 assertions against the real player: screens, music, HUD, drag preview, targeting, long-press, pause, hints, settings, letterbox, fps, zero errors), `tools/harness.py campaign` (consecutive autoplayed stages). See [AMENDMENTS](AMENDMENTS.md) "parity batch" for the systems added in that pass (Ranged/Mechanical archetypes, stacking policies, armor wear, relic passives, weapon swap, Black/Green spells, 55 class kits).

**Amended 2026-10-21 (GG-T17):** `tools/harness.py campaign 25` cleared the full 25-stage campaign (15 main + 10 postgame, GG-T10) end to end in one unattended live run for the first time, including the Anchor Wastes expedition (stages 9–11, 0-based) that had been the automation frontier in earlier sessions. Four single-attempt losses (stages 8, 13, 17, 19) each recovered on the harness's first scripted retry; no stage hit `DIFFICULTY_WALL`, no stall/timeout, and `errors: []` — zero engine errors across the whole run, with real gold/level progression through all 25 stages (`dotnet tests` 1024/1024 the same session). Confirms GG-T20's orphaned-confirm-veil fix (closing GG-T16) also cleared the click-reliability risk that had made a full unbroken run unachievable before.

## 7. Active frontier {#GG-§7}

- **Parity backlog** — [`USER_STORIES.md`](USER_STORIES.md): every 🟡 needs a targeted test; every ⬜ is unbuilt spec (full 7-line combat feed with icons, long-press tooltips, armor wear, relic passives, Ranged archetype). **Amended 2026-10-13:** "distinct kits for the 55 default-kit classes" is done — see §23.2.2; ability-level `CastVfx/ProjectileVfx/ImpactVfx/LingerVfx` name wiring is partially done (§6.2: Scan/Cleanse/Hasten utility spells) but still open for damage/buff spells, which read by `DamageType`/buff id instead.
- **Defects fixed 2026-10-09** — Mug now hits once (its row's BaseDamage); Silence also stops enemy charges (`EnemyPlanner.PlanCast`); Frozen skips the boundary of the action that applied it, so it costs the enemy one full cycle; the time bank carries until the next enemy turn instead of clearing each action. Each has a regression test in `AiAndSystemsTests`.
- **V2 direction** (not in this build window): GridGame2026 `docs/rfc/0002-v2-vision.md` — random summoning, branching story, HD art, content breadth.
- **Open design questions** — [§29](#29-open-design-questions).

## 8. Quality bar {#GG-§8}

Definition of done for a feature (also the per-story DoD in [`USER_STORIES.md`](USER_STORIES.md)):

1. **Compiles** — `tools/test.ps1` green (dotnet) and, for App changes, `tools/test.ps1 -Compile` shows zero `error CS`.
2. **Bible section updated** — no silent drift ([GG-LAW-7](#GG-LAW-7)).
3. **Rules are tested** — a named test in `Tests/Core.Tests` for every new rule; content changes keep `Real_content_loads_and_cross_references_resolve` and the bot soak green.
4. **Visible things are harnessed** — a `tools/harness.py` step (or a new one) reaches it and the layout audit is clean; screenshots land in `artifacts/<run>/`.
5. **Glyph check green** ([GG-LAW-11](#GG-LAW-11)).
6. **One commit at the end** — verify-then-checkpoint, no mid-phase commits.
7. **Polish doesn't regress** — for visible or balance-affecting work, the polish pass ([`POLISH.md`](POLISH.md)) scores no dimension below the baseline ([GG-LAW-14](#GG-LAW-14)).

A story is ✅ only when 1–7 hold; otherwise 🟡/⬜ ([GG-LAW-12](#GG-LAW-12)).

### 8.1 Release scope (GG-T392, owner 2026-11-16)

Owner: *"we are aiming for a polished, releasable game once the story has been established; enough development of more and more systems, need to refine and balance everything that exists, cut or at least disable that which doesn't."* Release polish refines and balances what serves the core loop (profile → 5-wave stages → gold for repair/weapons/skills/items → a story scroll every 5 stages → wanderers by Trust and wits → Diamond and gold summons → stage select hunts → Camp) and hides what doesn't; no new systems. `Meta/Features.cs` is the one switch table: a switch that is off hides only the App entry points (Camp rows, panels, Settings rows, strips, result rows); rules, content, saves, bots, Test Mode and automation are unchanged, and `-allfeatures` turns every switch on. Hidden at release: the Crucible (§22.10), the bounty board (§22.4), caravan upgrades and barks (§25.12), the menu theme, font and text-wrapping presets, and Camp's separate Inn: Respec row (Talents keeps its per-hero Respec). The verdict table, a balance watch-list and the open cut questions are in [RELEASE_SCOPE.md](RELEASE_SCOPE.md).

**Debug launch options need an explicit opt-in in release (GG-T566, owner: "in release mode the debug options have to be specified explicitly").** Every flag read through `App.CommandLine` (`-allfeatures`, `-start`, `-saveRoot`, `-profile`, `-testmode`, `-demo`, `-skill`, `-telemetry`, `-autobattle`, `-automation`, `-rested`, `-seed`, ... ) plus Unity.Shared's `--headless` and `-profiles-root` / `MINDATTIC_PROFILES_ROOT` are ignored by a non-development player (Android passes intent extras as command-line args) unless `-enableDebugOptions` is also given; one log line says they were ignored. Editor and development builds (the harness, `tools/deploy-apk.ps1`'s development APK, the Android Emulator Suite) are unchanged.

**One number format (GG-T566).** At startup (`GameApp.UseInvariantCulture`, `SubsystemRegistration`) the current and default thread cultures are set to the invariant culture, so timers, counts and prices format the same on every device ("1.5s", never "1,5s"). Dates the player sees are formatted explicitly: the save-slot list shows local `yyyy-MM-dd HH:mm`.

## 9. Glossary {#GG-§9}

- **Slide / displacement** — dragging a hero onto an occupied tile moves the displaced actor (ally or foe) into the tile the hero just left. ([§1.1](#11-sliding-and-displacement))
- **Pincer** — two allies in the same row/column with a contiguous, ally-free, ≥1-opponent line between them; the only source of hero physical damage. ([§1.2](#12-pincer-attacks))
- **Supporter** — an ally connected to a pincer attacker by an unbroken cardinal chain of allies, within its class's support range; performs its class's **support action** (strike the trapped line at 50%, heal, buff, haste, mana, cleanse, lend crit, echo a spell, inflict a status, shove…). ([§1.2.3](#123-supporters))
- **Support action** (`SupportDef`) — a class's effect as a supporter: Effect, Magnitude, Target (Attacker / Chain / Party / Self), Range along the chain. Enemies and classes without one strike at 50% from beside the attacker. ([§1.2.3](#123-supporters))
- **Action** — one committed command (move, slot use, shield); it ends the player's turn: the clock runs to the next enemy arrival (`BattleConfig.ActionTicks` = 20 ticks only when no enemy icon can arrive). ([§2.0](#20-the-held-turn), GG-T188)
- **Held turn** — the real-time window from picking a hero up until the next enemy icon reaches the left edge; when it runs out the hero is dropped where it is (forced drop). ([§2.0](#20-the-held-turn))
- **Turn mode** (`TurnMode`, GG-T762) — the rule for which heroes may act and when. **PartyWindow** (the default, today's rule): every able hero acts once per window. ⬜ **Any hero every turn**: one hero per party turn, any hero. ⬜ **Any hero who hasn't moved this round**: one hero per party turn, each once per round. ⬜ **Heroes on the timeline**: each hero has its own icon on the bar. It is a profile setting that New Game can lock on the save. ([§2.13](#213-turn-modes-gg-t762))
- **Window / party turn** — under PartyWindow, the time until the next enemy icon reaches the left edge, shared by the whole party, in which each able hero acts once (`ActedThisWindow`). ([§2.0](#20-the-held-turn))
- **Round** — ⬜ under Any hero who hasn't moved this round: the run of party turns in which every able hero acts once before the round resets. ([§2.13.2](#213-turn-modes-gg-t762))
- **TurnRules** — the one Core seam where a turn mode decides who may act, what an action spends and ends, what Guard gives up and banks, and what a turn boundary resets; nothing else reads `TurnMode`. ([§2.13](#213-turn-modes-gg-t762))
- **Tick / IP** — the clock unit (10 per second); turn icons load Initiative Points 0..1000 internally, but every icon is drawn at `u = secondsUntilAct / BarSeconds` (10 s; 0 = the left edge, acting now). ([§2](#2-the-timeline))
- ~~**Pushback Zone**~~ — retired 2026-11-06 (GG-T286). **Pushback** — an ability/item keyword in seconds (and 1 s on every foe a hero pincer hits): the foe acts that much later, anywhere on the bar, at most 2 s and once per action. ([§2.3](#23-pushback-a-keyword-in-seconds))
- **Hasten / Quicken** — the inverse: slide an icon forward toward the left edge. ([§2.7.1](#271-hasten--quicken--the-inverse))
- **Cast icon** — a spell with a cast time is a diamond on the same bar as the turns, born at `castSeconds / BarSeconds`, and resolves when it reaches the left edge. ([§2.6](#26-one-track-diamond-icons-the-prominence-diamond))
- **Prominence diamond** — the large diamond at the timeline's left end showing who acts next, with its seconds ("NOW" while acting). ([§2.6](#26-one-track-diamond-icons-the-prominence-diamond))
- **Sentient** — the enemy tag for a foe with a society or that plans ahead; only Sentient enemies (and every hero) may be pincer attackers. ([§1.3](#13-sentient-restriction))
- **Clutch** — the reward for a really cool moment (flash, "Clutch!", bonus mana charge): a rare LCK interrupt outcome on hero casts (the hit is shrugged and the cast resolves on the spot), a hero's crit landed while Blind, a kill at 15 % HP or less (Last Stand), the blow that clears a wave (Finisher), two pincers from one move (Double Pincer), a foe shoved into a lethal hazard (Hazard Kill) or a foe's charge interrupted in its last half second (Last-Tick Interrupt); one per action. ([§13.4.1](#1341-clutch-added-2026-11-21-gg-t566))
- **ManaBank / orb** — shared 12-orb pool of identical orbs; any orbs pay any spell. ([§3.1](#31-mana-the-orb-economy))
- **Drag timer** — optional Terra Battle–style 4 s limit per drag (Settings). ([§2.9](#29-drag-timer-optional))
- **Time-banked orbs** — Blue orbs minted from drag-timer seconds left at the drop. ([§3.1.8](#318-time-banked-orbs))
- **Countdown badge** — the number on an enemy: actions you get before it acts. ([§2.10](#210-countdown-badge))
- **AbilityBar** — the 5-slot Skill / Spell / Item bar of the selected hero; 2 usable on a fresh save, up to 5 by campaign progress. ([§4](#4-the-abilitybar))
- **Camp** — the Final Fantasy–style hub menu (`HubScreen`). ([§25](#25-the-camp-merged-hub))
- **Event player** — `BattleScreen.Play`, which animates an event list one awaited beat at a time.
- ~~**Undearth**~~ — struck 2026-10-10 (owner): the missing-sun / Undearth lore is removed in favor of the story premise that landed in GG-T120 (US-348) — itself retired in GG-T341 for *Under Morrow*.
- ~~**Morrow** — the moon, hanging so low it fills a third of the sky and "nearing" every lifetime; it pulls at **mana**, the current that lets a body heal and a spell catch.~~ **Superseded 2026-11-21 (GG-T575):** "Morrow" is not a moon's name any more; it survives only in the region **Morrow's Shadow** ("morrow" = tomorrow) and the chapter **Morrow Holds**. **Owner decision 2026-11-23 (GG-T648):** keep Morrow as a place name, final: Morrow's Shadow and its stages I–III, the region rule **Morrow's Dark** (`morrows-dark`) and the chapter **Morrow Holds** all stay; only the moon lost the name. The sky holds the Moon and the Manalith (canon: Prose `gridgame`, staging copy `Data/Prose/canon/`; staging in [§27.3](#273-the-sky-two-moons-and-the-staged-backdrop-gg-t575-design-gg-t576-builds-it)):
  - **The Moon** — the ordinary one: round, pale, familiar, always the same size in every region; nothing in the story happens to it.
  - **The Manalith** — **not a moon** (owner 2026-11-21, GG-T577): an ancient thing that was stopped long ago and hangs in the sky, orbiting, and is now coming closer. A lumpy, crusted, grown-looking mass of bladed crystal and blue-violet lumps flushed with red, with faint light in its cracks, alive and asleep, growing larger in the sky chapter by chapter until it fills the sky over Morrow's Shadow. Always "the Somnolith" in full, never "Morrow", "the moon", "the Lith" or a nickname.
- **Mana** — the Manalith's energy and the life in every person, the same thing; orbs, heals and spells run on it. Always "aether", never "essence" (GG-T577).
- **The tithe** — the mana the Choir's collectors take from every hold once a year, sung up by the Lullaby to keep the Manalith asleep; pay too much and you come back **Hollow**. The surplus feeds **the Hierarch**, the villain. (Canon: Prose `gridgame` GG-PEOPLES; the Pale Concord and the Stills are retired, GG-T577.)
- **Story crawl** — the skippable per-theme intro text shown on first entry into a theme. ([§27](#27-story-crawl-no-dialog))
- **Summon Circle** — the Camp service where gold recruits a hero class. ([§25.10](#2510-summon-circle))
- **Bounty** — a posted hunt for a marked, tougher variant of a foe: take it, play the stage its clue names, kill the one wearing the skull, collect gold + an item at the board. ([§22.4](#224-bounty-board))
- **Tap term / `[term]`** (*added 2026-11-06, GG-T300, owner: "anything in brackets is a tap tooltip"*) — content text marks a keyword in square brackets; the player sees it highlighted and underlined (light blue `Palette.Term`, brackets hidden) and a **tap** on it opens a small definition card. Every bracketed term must resolve, case-insensitively, to an `Id`, `Term` or `Aliases` entry of `Resources/Content/glossary.json` (`GlossaryEntry`: `Id`, `Term`, `Aliases[]`, `Text`, optional `LinkKind` Buff/Ability/Stat/Item + `LinkId` for live numbers on the card); `ContentLoader.Validate` (`Glossary.Validate`) fails on an unknown term in any ability, item (incl. relic passive), class or skill-tree text, and requires an entry for every status name. Text without brackets is always valid. The glossary is seeded with every status, the stats, the timeline words (Pushback, Guard, Cast time, Haste, Slow, Stun, Sentient, Pincer, Supporter, Prominence, Tempo, Interrupt), the reach/reaction words ([directly], [adjacent], [surrounded], [Counterstrike], [Reactive], [Passive], [Active], [Dodge]) and the class-card archetype words.
- **Chance words** (*GG-T300*) — a description that doesn't print a number says how likely with one word, one band each (`Glossary.ChanceBands`, `Glossary.ChanceWord(p)`): **[slight]** ~10% (≤15%) · **[small]** ~20% (16–27%) · **[fair]** ~35% (28–42%) · **[good]** ~50% (43–62%) · **[likely]** ~75% (63%+).

For combat/targeting/VFX sub-vocabulary see [Appendix §18 Glossary](#18-glossary).

---
<a id="gg-appendix-a"></a>

# Appendix A — Design Canon

> The detailed reference behind the nine sections above; the L0 sections and `USER_STORIES.md` cite it by `§` number (numbering follows the GridGame2026 bible; §16 and §21 are intentionally absent).

## 0. North Star

A tactical-RPG hybrid where the player **drags heroes one tile at a time across a 6×8 grid** to flank enemies and trigger **pincer attacks**. Combat runs on a **timeline** of enemy icons that "load" left → right and act when they arrive; the clock advances only when the player commits an action. Magic, items and class skills are managed through the **AbilityBar**. Between battles the party rests at a Final Fantasy–style **Camp** menu.

The game is a **deterministic pure-C# simulation** (`GridGame.Core`) with a **code-built UI Toolkit front end** (`GridGame.App`) that plays the simulation's events back. There are no prefabs, no hand-built scenes and no Inspector wiring; art, audio and content load from `Resources`. **One exception (amended 2026-11-05, owner, GG-T269):** premade particle-effect prefabs under `Resources/Vfx/`, spawned and driven by code ([§6.2](#62-vfx-pipeline)).

### 0.1 Design pillars

1. **The verb is "slide".** Heroes never "land on" enemies. They flank into tiles; damage comes from the geometry that forms, not the move itself.
2. **Time is the resource the player manages.** Every decision trades against "whose icon lands first?" — but the clock waits for you. **Amended 2026-10-09:** time is spent per action, not per real second. **Amended again 2026-10-21 (owner, GG-T188 — reverses the act-advanced clock):** the clock waits while you *look*, but runs in real time while you *hold* a hero; your turn is the time from pickup until the next enemy icon reaches ACT.
3. **Mana is a shared, visible pool.** The 12-slot ManaBank is the whole party's spell budget.
4. **Every spell is a (shape × mode × filter) triad.** Adding a spell is a data row.
5. **The simulation is the truth.** **Amended 2026-10-09:** replaces "Code is the source of truth; the builder is the spec". Core decides; the view only animates what Core already resolved.
6. **Verify-then-checkpoint.** Land a feature end-to-end with test + harness evidence before committing.
7. **The bible is the brief.** If code and bible disagree, fix the disagreement in writing.

### 0.2 Non-goals (what this game is NOT)

- **Not real-time, and not "press attack, wait for enemy to press attack."** **Amended 2026-10-09:** the old bible said "the timeline is continuous; thinking time is also game-clock time." In GridGame the clock is **act-advanced**: it does not move while you look, drag or pick a target. Each committed action spends 2.0 s of game clock, during which any number of enemy icons and casts may arrive and resolve in arrival order. There is no hero turn order — any living, mobile hero may act each time. The pressure comes from the countdown badges and the Zone, not from a ticking wall clock (an optional drag timer restores Terra Battle's hand pressure, §2.9). **Amended 2026-10-21 (owner, GG-T188):** still not real-time while you think — but a held hero runs the clock in real time, and each committed action ends your turn at the next enemy arrival ([§2.0](#20-the-held-turn)).
- Every other non-goal is unchanged from [§3](#GG-§3): not a deck-builder, roguelike, gacha / live service, multiplayer game, grid-puzzle, free-form mover or stat-spreadsheet.

### 0.3 Session shape (target pacing)

| Beat | Target | Felt as |
|---|---|---|
| One **battle** | 15–40 committed actions (~2–5 min at default battle speed) | A single decisive engagement |
| One **Camp visit** | 30–60 s | Quick tweak, not a menu trawl |
| One **stage** (battle + results + Camp) | 3–4 min | A "round" |
| One **session** (5–8 stages) | 20–30 min | A commute, a couch break |
| Full **playthrough** (15 campaign stages + farming) | 6–10 h | A weekend campaign |

**Amended 2026-10-09:** battle length is measured in actions because wall-clock time now depends on the player and on Settings → Battle speed. `BalanceReport.Campaign_pacing_table` is the tuning dashboard: it plays the campaign (15 main + 10 postgame stages, as of 2026-10-13) in order with natural progression (XP, carried wounds, Inn, retries) for bots of three skill levels, with and without `CampRoutine`, and prints win %, average actions and HP left per stage.

---

## 1. The Board

- **6 columns × 8 rows** (`BattleConfig.Cols/Rows`). **Amended 2026-10-09:** coordinates are 0-based `Coord(X, Y)`, X = column left→right, Y = row top→bottom; (0,0) is top-left.
- Each tile holds at most **one actor**; occupancy is footprint-aware via `BattleState.UnitAt(tile)` / `Unit.Occupies(tile)`.
- Heroes start at fixed tiles (1,6), (4,6), (2,7), (3,7), (0,7), (5,7) unless a `HeroSpec.Start` overrides. Enemies use their `SpawnDef` tile if the footprint fits, else a random free tile in rows 0–4 (then anywhere). **Amended 2026-10-09** (spawn placement is now explicit rule, not scene layout).
- On screen the board is drawn by `App/Battle/BoardView.cs`: 180-px tiles at design (45, 450).

### 1.1 Sliding and displacement

The player drags a hero **one tile at a time, cardinal only**. The UI turns the finger position into cardinal steps (`BattleScreen.ExtendPath`: dominant axis first; if that step is blocked, try the other axis once, else hold position) and the committed `MoveCommand.Path` is a list of one-tile steps.

When a step enters a tile **already occupied** by any actor (ally **or** enemy):
- The dragging hero takes that tile.
- The occupant is moved to the tile the dragging hero **just left**.
- This cascades step by step as the hero passes through several occupants.

```
before:              H A E E . .
drag H east 1 tile:  A H E E . .    (A moved into H's old tile)
drag H east again:   A E H E . .    (the first E moved into H's old tile)
```

**Refused steps** (`StepBlockedEvent`; the rest of the path is dropped): a non-adjacent step (diagonal), a step off the board ("edge"), or a step into a multi-tile enemy or snake-chain member ("immovable"). **Amended 2026-10-09:** a drag that ends with nothing changed (no position, HP or trap change) is a **cancel**, not an action — no clock cost (`Noop_drag_costs_no_time`).

Side effects of a step: Sleep (and any `BreaksOnMove` buff) is removed from a displaced actor and from the dragger; a hero that steps or is displaced onto a trap triggers it ([§14.2](#142-enemy-archetypes-design-palette)).

**Damage rule**: movement never deals damage ([GG-LAW-1](#GG-LAW-1)).

**The core strategy (owner, 2026-11-03, GG-T219).** "One of the most important strategies is to slide through allies and opponents to set up even more devastating pincer attacks … moving opponents into lines to execute the best pincer attack." The slide is not travel; it is how a turn *builds* the pincer. Every occupant a hero passes through lands on the tile the hero just left, so a walk drags a column of foes back along its own route — straightening a scattered pack into one unbroken line — while allies passed through are dropped behind it as attackers or supporter-chain links. The best turn walks a route that ends with the most foes in a single line between two heroes, with the longest supporter chains behind them. Features that show or score moves (the shove preview, GG-T190; the drag trail below; bot and hint scoring) serve this play first.

**The drag trail (GG-T219).** Like Terra Battle and Friends & Dragons, a moving unit leaves a trail. Owner: "the trail should just be a trail renderer … just a fast wide trail that fades out in about 1.5-2 squares worth of distance … not anchored to anything … moving the heroes and units can be a diagonal or free hand thing, not perfectly right angled changes." `TrailLayer` is that trail renderer: while a token moves, its real drawn position (the finger's free-hand path for the held hero, the tween for a walking unit) is sampled every frame (4 px minimum vertex distance) into a polyline and drawn **under** the token as a triangle-strip streak as wide as the token (a soft full-width band over a brighter core), so it shows just behind it. It is never anchored and its length is time, not distance: each point lives 0.15 s (owner: "it needs to be fading quick fast"), with alpha falling off as the square of its remaining life, so a faster, longer move draws a longer streak and it is gone a moment after the unit stops. Owner: "it gets longer the more quickly and distant the movement is but always catches up very quickly instead of fading out to a point, the whole end fades out so that the end is still almost as thick as the start" — the tail keeps ~88% of the token's width and fades out by age. Gold for heroes, red for foes; a displaced actor shows its shove arrow instead (GG-T190).

### 1.1.1 Physical blocks (added 2026-10-10, owner direction)

Owner: "the grid can have physical blocks that disallow entrance … nobody moving through or landing or spawning on a physical block."

- `StageDef.Blocks` lists block tiles (rocks, pillars, dead trees); `BattleState.Blocks` holds them for the battle. **`BattleState.Walkable(c)`** (on the board and not a block) is the single collider check every rule uses: hero steps (`StepBlockedEvent` "block"), enemy steps and 2×2 footprints, snake moves, teleports, spawns (initial and reinforcements), trap laying and amplifier spawns/shoves.
- Displacement can never push a unit into a block — the displaced unit always takes the tile the mover just left.
- A block is not an opponent, so a block inside a line **breaks the pincer**. Blocks also stop ranged line of sight and line charges.
- Content: Arkgrave III, all three Anchor Wastes and all three city-ruin stages carry 2–3 blocks, kept off the hero start rows (6–7). The bot soak plays every stage with them.
- View: a raised, bevelled stone block (the biome tile, lit top edge, dark base, drop shadow).
- *(Tests: `A_hero_cannot_step_onto_a_block`, `A_block_in_the_line_breaks_the_pincer`, `Nobody_spawns_or_walks_onto_blocks`, `Real_stages_carry_their_blocks`.)*

### 1.1.2 Movable tiles (added 2026-10-10, owner direction)

Owner: "nothing can spawn on top of the Power Amplifier but they can push it around like any other actor … a MovableTile as opposed to a stone which can't be moved."

- `MovableTile` (`BattleState.Movables`, kind `Amplifier` for now) occupies its tile like an actor: `BattleState.FreeForSpawn(c)` (walkable, no unit, no board item — no movable and, since GG-T227, no pickup) gates every spawn, reinforcement, egg, guest, recruitable, snake segment, blink/burrow surfacing, revive, trap and trail (the amplifier spawn uses the stricter `FreeForItem`, which also keeps it off traps — GG-T129). Units already on the board that are pulled, step back or are knocked on use `FreeToEnter` (no unit, no movable): they may enter a pickup's tile.
- It is **displaced exactly like an actor**: a hero stepping into it swaps it into the tile just left; an enemy stepping into it does the same; a 2×2 drags it to its vacated lane tile; knockback pushes it ahead if there's room. It is never an attacker, a target or a pincer opponent, so it breaks a pincer line like any gap.
- Stones (§1.1.1) are the immovable kind — colliders nothing enters or lands on.
- **One item per tile (added 2026-10-19, owner: "power ups can't share a space either", GG-T129):** a movable is a board *item* as well as an actor, so it never shares a tile with a pickup (§13.3.1) either. It never spawns on a pickup (`BattleState.FreeForItem`), and a shove never lands it on one: a hero swap or enemy step whose "tile just left" holds a pickup sends it to the nearest open, item-free tile instead (`ShoveMovable` → `NearestMovableSpot`; the pickup stays where it lies), and knockback treats a pickup beyond it as a wall (Crash). See §1.1.3.
- **Amplifier alignment (decision, owner may overrule):** since heroes can't stand on a movable tile, a pincer attacker is *aligned* with the amplifier when it is cardinally adjacent to it.
- *(Test: `The_amplifier_is_a_movable_tile_heroes_push_and_nothing_spawns_on`.)*

### 1.1.3 One actor per tile — the occupancy guarantee (added 2026-10-19, owner direction, GG-T97)

Owner: "there can never be two actors occupying the same tile after sliding is over."

**The rule.** Once a slide (any step, shove, swap or appearance) is over, every tile holds at most one actor — a living unit (corpses, remains and eggs included; the dead are off the board) *or* a movable tile — and every actor stands on walkable tiles (§1.1.1), footprint-aware for 2×2s. The only in-between is the two halves of one swap (the stepper arrives, then its partner takes the tile just left), which never survives to the end of a beat.

**Board items too (added 2026-10-19, owner direction, GG-T129).** Owner: "power ups can't share a space either; I see multiple pickups sitting on top of each other." Every tile holds **at most one board item — a pickup (§13.3.1) or a movable tile (§1.1.2) — ever**, and every item lies on a walkable tile. Items are separate from units: ~~a unit may stand on a pickup (pickups don't block; a hero collects it on entry, an enemy just stands there), but never on a movable.~~ **Amended 2026-11-03 (owner direction, GG-T228):** no unit ever stands on a pickup either — a hero entering a pickup's tile collects it, and any other unit that enters one shoves it exactly like a movable (see *Pickups move like actors* below). One check gates every item placement, `BattleState.FreeForItem` (walkable, no unit, no trap, `ItemFree`: no pickup and no movable) — pickup drops and the amplifier spawn both use it — and `MoveMovable` refuses a landing tile that holds a pickup. (Only a hero-path *preview* ignores pickups there: the preview doesn't collect them, so the tiles the walk crossed still list pickups the real move picks up first.)

**The chokepoint.** Every position write in Core goes through one of three `BattleRunner` methods, which refuse a footprint that isn't open (`FootprintOpen`: walkable, no other living unit, no movable — with the step's own swap partner / shoved heroes / shoved movable named as yielding):
- `Relocate(unit, to)` — every move of a unit already on the board;
- `MoveMovable(movable, to)` — every shove of the Power Amplifier;
- `AddUnit(unit)` — every unit that appears.
A refused write moves nothing (release) or throws (`BattleEngine.StrictTiles`). New units are placed with `FootprintFree` (`FreeForSpawn` + no trap) / `RandomFreeTile` first, so a refusal there is always a bug.

**Nothing appears on a pickup (added 2026-11-03, owner direction, GG-T227).** Owner: "no two units or powerups can share the same tile, make sure nothing spawns on a tile thats already occupied." `FreeForSpawn` used to check units and movables only, so a new wave (pickups outlive the wave that dropped them), a reinforcement, a guest, a recruitable, a snake segment, an egg, a blinker/burrower surfacing, a revived hero, a laid trap or a trail could all appear on a pickup — the unit-on-powerup stacks the owner saw. Since GG-T227 it also requires `ItemFree` (no pickup), so every appearance path in the table below skips pickup tiles footprint-aware (a 2×2 needs all four tiles pickup-free) and falls back exactly as it does for an occupied tile.

**Pickups move like actors (added 2026-11-03, owner direction, GG-T228).** Owner: "no two units or powerups can share the same tile … make sure powerups and spawns get moved the same way actors get moved." Heroes still collect a pickup by entering its tile (walking, displaced, teleporting, stepping back). Any other unit that enters one — an enemy step or skirmisher retreat, a hopper leap, a 2×2 lane step, a snake head, a blink, a teleport, a non-hero knocked back, pulled or displaced — shoves it with `ShovePickupsUnder` → `ShovePickup`, the `ShoveMovable` rules: onto the tile the mover just left (a 2×2's vacated tile on that lane; a snake's tail tile) when it is walkable with no unit, no movable and no other pickup, else the nearest such tile (`NearestSpot`); with no such tile anywhere the pickup fades (`PickupExpiredEvent`) rather than stay under a unit. Each shove emits `PickupMovedEvent` (From, To, ByUnitId), which the view slides like the amplifier with a slide cue. Spawns never need it: they never appear on a pickup (GG-T227). `FindTileConflict` now reports a unit on a pickup (strict throws; release slides the pickup off); `TestKit.AssertInvariants` checks it in every soak. Enemy AI never sought pickups, so no planner change.

**The guarantee, path by path:**

| Path | How the footprint is guaranteed free | When there's no room |
|---|---|---|
| Hero drag step (`WalkPath`) | Enters an empty tile, or swaps: the occupant (or movable) takes the tile the hero just left — the one tile the step frees. Multi-tile, snake and remains occupants refuse the step ("immovable"), and so does a **Heavy** one (`StepBlockedEvent` reason "heavy", GG-T891; a Floating Heavy unit swaps like any other, §14.2); a diagonal slip only enters open space. | Step refused (`StepBlockedEvent`), rest of the path dropped. |
| Enemy 1×1 step (`StepEnemy`) / skirmisher retreat | Destination must hold no unit; a movable there is shoved into the tile just left. | No step. |
| Hopper leap (`LeapEnemy`) | Landing tile must be fully open (no unit, **no movable**). | No leap. |
| 2×2 step + `VacatedTile` shoves | `FootprintStepLegal` refuses any entered tile with an enemy (incl. corpses/remains/eggs); each hero/movable on an entered tile goes to the tile vacated **on its own lane** — one entered and one vacated tile per lane, so two shoves can never meet. | No step. |
| Snake chain (`FollowSnake`) | Each segment slides into the tile the one ahead just left; a movable the head walked onto goes behind the **tail** (it used to land under segment 1 — fixed in GG-T97). | The chain stops at the first segment that can't follow. |
| Knockback (`Knockback`) | Each tile of the push must be walkable and unit-free; a movable ahead is pushed on only if the tile beyond it is free; a pickup it lands on is shoved back to the tile it left (GG-T228). A **Heavy** target fizzles ("Too heavy!", GG-T891). | Crash (unit / movable that can't move) or Slam (edge / block): damage, no move. |
| Teleport ability, delayed or instant | Landing footprint must be open at resolution time. | "Blocked", caster stays. |
| Blink / burrow (`SurfaceBeside`) | Only a free, trap-free tile beside the prey. | Normal turn instead. |
| Movable shoves (`ShoveMovable`, hero swaps included) | Only onto a walkable tile nobody stands on that holds no pickup and no other movable (GG-T129). | Nearest open, item-free tile (`NearestMovableSpot`); never left under a unit or on a pickup. |
| Power Amplifier spawn | `FreeForItem`: no unit, no trap, **no pickup** (it used to ignore pickups — GG-T129). | Not spawned this time. |
| Pickup shoves (`ShovePickup`, GG-T228) | A non-hero entering a pickup's tile sends it to the tile it just left (lane-aware for 2×2s, behind the tail for snakes) if no unit, movable or other pickup is there. | Nearest open, item-free tile; none at all → it fades. Never left under a unit. |
| Pickup drop (`MaybeDropPickup`) | `FreeForItem` on the fallen enemy's tile, else a cardinal neighbour. | No drop. |
| Party at battle start | `HeroSpec.Start`/default start if free, else a random free tile in rows 4–7, else anywhere. | The hero sits the battle out off the board (only possible on a stage walled with blocks). |
| Wave spawns (`SpawnWave`, incl. snakes) | `SpawnDef` tile if the footprint fits, else random free rows 0–4, else anywhere; segments on free adjacent tiles. | That spawn is skipped. |
| Delayed reinforcements (§14.8) | As wave spawns. | **Waits** in the queue and retries every tick until a tile frees; gives up only when no other enemy is left to free one (so the wave can still end). With no foe on the board the earliest queued group lands now instead of waiting out its delay (GG-T512). |
| Eggs (`LayEgg`) | A free, trap-free tile beside the layer's footprint; the event reports the egg's real tile. | No egg this turn. |
| Remains / corpses (`LeaveRemains`), rising and hatching | On the tile the unit just vacated by dying (the remains/egg is removed before its successor spawns). | Not left / successor placed by the spawn rules. |
| Guests (`SpawnGuests`) | Their tile if free, else random free rows 3–7, else anywhere. | Guest doesn't join. |
| Revival Charm (`Revive`) | The free, trap-free tile nearest the user — never back onto its old tile if someone stands there now. | Refused before the charm is spent ("No room!"). |

**The net.** `BattleRunner.EnforceTileInvariant` still runs after every command (and after `BattleEngine.Create`) as a last resort, and since GG-T129 `FindTileConflict` covers items as well (two pickups on a tile, a pickup under a movable, a pickup off the board): in a release build it nudges the newer actor to a free tile (a movable on a pickup slides to the nearest item-free tile; the newer of two stacked pickups fades with a `PickupExpiredEvent`, so its token goes too) and emits a `TileConflictEvent`, which the App logs as an error, shows on screen in dev builds and counts in `/state → board_audit`. Under `BattleEngine.StrictTiles` (the whole test suite turns it on; `BattleState.StrictTilesOverride` lets one test opt out) it throws `TileConflictException` instead — and the same check also runs after every enemy turn, cast and reinforcement *inside* a command, so a conflict is caught where it happens.

**The view.** Playback places every token where the event says it was at that moment — spawn/reinforcement tiles (`WaveStartedEvent.SpawnedAt`, `ReinforcementsArrivedEvent.SpawnedAt`, GG-T98), remains/eggs/hatchlings/risen/guests at their event `At`, and the drag prefix snaps each unit to its last prefix step, not its end-of-command tile. `BoardAudit` checks after every beat that no two tokens are drawn on one tile (a swap's two halves excepted), as playback settles that every token sits exactly on its unit's `State` tile (before `SyncAll` could hide a miss as a jump), and after `SyncAll` that tokens are unique and rendered on their tiles. Since GG-T228 pickup tokens move (`PickupMoveAnim`, `BoardView.PlacePickup` keeps the drawn tile in step, `SyncPickups` re-places them, and a drop is drawn on its event tile, not the end-of-command one), and the audit also fails a unit token drawn on a pickup token — per beat for non-hero tokens once no move beat is next, and against the state for every unit. Since GG-T129 it audits **item tokens** the same way (`AuditItemTokens`): after every beat no two item tokens (pickups, the amplifier diamond) are drawn on one tile, and against the state no stale token — every drawn pickup still lies where the state has it, every pickup in the state is drawn, and the amplifier is drawn exactly where (and only while) it stands. Collected and faded pickups remove their tokens (`PickupCollectAnim` / `PickupExpiredAnim`, with `SyncPickups` as the net), a used amplifier hides its diamond.

*(Tests: `TileOccupancyTests` — strict throw, release heal (unit and amplifier), long drag through a crowd, knockback into unit/edge/block/amplifier, 2×2 shoving a crowded lane (two heroes, hero + amplifier) and blocked by an enemy body, snake head onto the amplifier, delayed teleport onto a tile taken meanwhile, blink with no free side, colliding party/enemy spawn tiles, a party bigger than the board, guest onto a taken tile, reinforcement onto its own taken tile and onto a full board, egg laying, remains/rising, revive onto an occupied tile and on a full board; GG-T129 board items: the amplifier never spawns on a pickup, an enemy leaving a pickup shoves the amplifier aside, a hero swap never sends it onto a pickup, knockback stops at a pickup behind the amplifier, strict throw on a pickup under the amplifier, release heal of two stacked pickups, and a strict soak with drops and amplifier shoves (`Strict_soak_with_drops_and_amplifier_shoves_never_stacks_items`); GG-T227 spawn sources aimed at a pickup: a new wave (1×1 and 2×2) on pickups left from the last, a reinforcement, an egg, a guest, a recruitable and snake segments, a blinker's only surface tile, a revive whose nearest tiles all hold pickups; `TestKit.AssertInvariants` asserts items apart in every soak; and the strict bot soak: every stage × 24 seeds × 2 bot temperaments at a 1500-action cap plus long Crucible runs. Harness: `python tools/harness.py boardaudit N` plays N autoplayed battles in the real player and fails on any `board_audit` problem, item tokens included.)*

### 1.2 Pincer attacks

A pincer is **two allies in the same row OR column, with every tile between them covered by opponents** — no gaps, no allies in the line, at least one opponent ([GG-LAW-2](#GG-LAW-2)). **Amended 2026-11-06 (owner, GG-T289/GG-T294):** both allies must be pincer-eligible — every hero, and only **Sentient** enemies ([§1.3](#13-sentient-restriction)); a trapped hero line flanked by two wolves is just two melee neighbours.

```
Valid horizontal pincer (3 enemies):     Valid vertical (2 enemies):
H E E E H .                              . H . . . .
                                         . E . . . .
INVALID — gap:   H E . E H .             . E . . . .
INVALID — ally:  H E A E H .             . H . . . .
INVALID — diagonal: no.
```

#### 1.2.1 Trigger

`BattleRunner.ResolvePincers(Team.Hero, hero)` runs after:
- a committed hero drag (`DoMove`), and
- a Teleport landing (`ResolveAbility`, [§5.3](#53-special-targeting-flows)).

The scan (`PincerDetector.Detect`) is **board-wide** — every valid pair resolves, not only pairs involving the dropped hero. Enemies run the same scan for their own team on their turn ([§14](#14-ai)).

**Amended 2026-10-11** (owner: a manual drag out and back into the start tile wasn't pincering): a drag that ends with no position/HP/trap/pickup change is normally a free cancel ([§1.1](#11-movement), `Noop_drag_costs_no_time`) — but if the hero's final tile (even its own start tile) completes a pincer, that always counts as the turn; `DoMove` checks `PincerDetector.Detect` before deciding a drag was a no-op. *(Test: `Dragging_out_and_back_to_the_start_tile_still_pincers_if_it_lands_back_in_position`.)*

#### 1.2.2 Ordering of multiple pincers

**Amended 2026-10-09** (`PincerDetector.Order`):
1. The first pair is one **including the dropped hero** (if any), oriented so it is attacker A.
2. Next, a pair sharing the previous link's second attacker (**chain**), oriented so the shared hero leads.
3. Otherwise the pair **nearest** (Manhattan) to the previous link's end.

`PincerFormedEvent.ChainIndex` numbers the links; the view shows "PINCER!" then "CHAIN ×N!".

#### 1.2.3 Supporters

~~**Amended 2026-10-09:** any living ally cardinally adjacent to a pincer attacker is a supporter and strikes every trapped opponent at 50%.~~

**Amended 2026-10-21 (GG-T187, owner — "Terra Battle's best idea"): every class gets a support action.** A living ally (other than the partner) **connected to a pincer attacker by an uninterrupted cardinal chain of allies** is a supporter when its distance along that chain (1 = touching the attacker) is within its class's support **Range** (`PincerDetector.FindSupporters`, a breadth-first walk over allies only). Any empty tile, foe or obstacle **breaks the chain**; every ally is a link whatever its own range, so a striker standing beside the attacker carries a healer two tiles back. There is no line-of-sight/axis requirement. Supporters of both attackers are merged and de-duplicated; one reached from both sides supports attacker A. Supporters need not be pincer-eligible (an enemy supporter need not be Sentient, [§1.3](#13-sentient-restriction)).

Each supporter then performs its class's **`SupportDef`** (`classes.json` → `Support`: `Effect`, `Magnitude`, `Target`, `Range`, optional `Buff` and cut-in `Label`) — `BattleRunner.PerformSupport`, announced by a `SupportActionEvent`:

| Effect | What it does | Magnitude |
|---|---|---|
| **Strike** | a physical strike on **every trapped opponent** (the old supporter hit — now one support type among many) | damage fraction (0.5 = `BattleConfig.SupporterDamageFraction`) |
| **Heal** | heals each target | fraction of the target's max HP |
| **Buff** | applies `Buff` to each target — ATK up (`empowered`), shield (`protection`), `regen`, taunt (`taunting` on Self) | — (the buff's own duration) |
| **Haste** | each target acts sooner: its turn icon gains that many seconds of IP at its own rate, or its cast is rushed by that many seconds; a hasted hero also lifts party Tempo by `round(s / BarSeconds / 0.15)` (min 1). **Amended 2026-11-06 (GG-T298):** seconds, not a bar fraction | seconds (≤ 3) |
| **Mana** | fills the mana meter (100 charge = one orb, [§3.1.9](#319-mana-charge)); hero team only | charge |
| **Cleanse** | strips every debuff from each target | — |
| **Crit** *(GG-T298)* | the attacker it is chained to gains that much crit chance on its blows **in this pincer** (Double Attack follow-ups included). Resolves **before** the attackers strike; several stack; the attacker's crit is capped at 75 % while supported | crit fraction (0.2 = +20 points, ≤ 0.5) |
| **Cast** *(GG-T298)* | echoes a random **damaging spell from its own kit** (class kit, then a hero's bar: Spell kind, BaseDamage > 0, not ally-only; one battle-Rng draw when it has two or more) on **every trapped opponent** at reduced power: the spell's own damage × resistances × caster INT power × Magnitude. No mana, no cast time, no crit, no rider status. A silenced supporter, or one with no damaging spell, does nothing | power fraction (≤ 1) |
| **Status** *(GG-T298)* | each trapped opponent gains `Buff` (a debuff: poison, burn, blind, slow, silence…) with that chance — one Rng draw per foe below 1, none at 1; immunities and wards still apply | chance 0..1 |
| **Pushback** *(GG-T298)* | that many seconds of Pushback on every trapped opponent, **added to the pincer's own push** (hero pincers: `PincerPushbackSeconds` on a foe they hurt), so each foe is still pushed **once per action**, capped at `PushbackMaxSeconds` (2 s). Works for either team | seconds (≤ 2) |

**Target** is **Attacker** (the attacker the chain reaches), **Chain** (every ally on the chain from the supporter to that attacker, both ends), **Party** (every living ally) or **Self**; Strike, Cast, Status and Pushback always hit the trapped line and Crit always goes to the attacker. A class with no `Support` uses the default: Strike at 50% from range 1, exactly the pre-amendment rule — since GG-T307 no shipped enemy relies on it ([Enemy supports](#enemy-supports-gg-t307) below). Hero defaults by role: Strikers strike (range 1), Ranged strike from range 2, Healers heal (attacker, chain or party, range 2–3), Tanks guard/taunt/rally (range 2), Mages give mana or haste (range 2), Supports haste, buff, shield, regen, cleanse or give mana (range 2–3). Supporters still count as participants for the mana charge divisor ([§3.1.9](#319-mana-charge)). The pincer outline links each supporter to the unit its chain runs through (`PincerFormedEvent.SupporterLinks`), and each supporter's cut-in card and float text name its action. *(Tests: `Supporters_join_through_an_unbroken_chain_of_allies_within_their_range`, `A_gap_in_the_chain_breaks_support`, `A_healer_supporter_heals_the_attacker_it_is_chained_to`, `Every_hero_class_performs_its_own_support_action`.)*

**Amended 2026-11-06 (GG-T298, owner — "Each supporter gives some kind of passive or active effect when they are a supporter: a Cleric sends a heal to the attacker; a ninja might increase your crit %; a wizard might randomly cast a spell on enemies in the line — so some characters are just plain better for support because of their passive benefits."):** the four effects above join the list, and the 34 hero classes that only struck now each bring an archetype edge:

- **Crit** (+25–35 %): Assassin *Killing Intent*, Black/Blue/Chroma Ninja *Shadow Edge* / *Still Water* / *Prism Edge*, Duelist *Read the Opening*, Fencer *Riposte Eye*, Swordmaster *Perfect Form*, Ronin *Iaido*, Ripper *Find the Vein*, Night Hunter *Hunter's Mark*.
- **Status** (35–50 %): poison — Slasher *Rusty Blade*, Hexstalker *Venom Rain*; blind — Yellow Ninja *Flash Powder*, Thief *Pocket Sand*; slow — Vampire *Mesmerize*, Hexwarden *Snare Shot*; silence — Hexsniper *Hex Round*.
- **Volley** *(GG-T834, GG-T915)*: a thrown follow-up that strikes each trapped foe for Magnitude × damage (a real, undodgeable support hit, `AttackEvent.IsThrown`) and, where it lands, inflicts `Buff` with `Chance`: Red Ninja *Fire Shuriken* (30 %, burning 40 %), Green Ninja *Poison Dagger* (30 %, poisoned 50 %). The cut-in names it, then each hit is thrown from the supporter to its target per the class's `SupportDef`: `Projectile` (a vfx.json Loop prefab flies the path, a Once one bursts at impact, none draws a dot), `Motion` (Arc curves, Straight flies flat, Spin flies flat as a spinning cross) and `Sfx` (default battle.whoosh). Reduce Motion flattens every throw.
- **Pushback** (+0.5 s; Panda Brawler +0.75 s): Basher *Bash*, Bruiser *Shove*, Pugilist *Haymaker*, Prizefighter *Uppercut*, Street Fighter *Shoulder Check*, Martial Artist *Palm Strike*, Panda Brawler *Belly Bump*, Lancer *Lance Drive*.
- **Haste**: Speedster *Slipstream* (1 s).
- **Strike** stays the identity of the pure melee: Barbarian *Rampage* (60 %), Fighter *Follow Through*, Striker *Flurry*, Sellsword *Paid Blade*, Raider *Plunder*, Odachi *Great Arc*.
- **Cast** (40 % power) goes to the hex-type mages, which trade Mana for it: Hexer *Hex Echo*, Harbinger *Arcane Echo*.
- The existing Haste supports convert to seconds: Courier 1.5 s, Operative 1.25 s, Drifter 1 s, Dervish 0.75 s (chain).

`ContentLoader.Validate` checks each effect's range, that a Status names a known debuff, that a Cast class has a damaging spell in its kit, and that the generated sentence's [terms] are in the glossary. Every effect is plain `SupportDef` data, so an enemy class given a `Support` uses them unchanged (GG-T307).

<a id="enemy-supports-gg-t307"></a>**Enemy supports. Amended 2026-11-06 (GG-T307, owner — "Every monster needs support abilities."):** every one of the 115 enemy classes carries its own named `Support`. None of them uses the default strike. A monster stands as a supporter whenever its chain reaches the attacker of a Sentient enemy pincer, and it does not need to be Sentient itself, because only the two attackers must be. A tamed monster with the Pincer keyword brings the same block. Bosses reach 2 and hit harder:

| Family | Effect (Label) |
|---|---|
| Bats, vultures, flying monkey, sand maw | **Status** blinded 30–40 % (*Screech*, *Eye Peck*, *Flung Grit*, *Sandblast*); bat01 **Heal** 8 % (*Blood Share*) |
| Wolves, werewolf | **Crit** +20–25 % (*Pack Instinct*), +30 % (*Blood Frenzy*) |
| Frogs, toad, naga | **Status** poisoned 40–50 % (*Toxic Skin*, *Wart Venom*) or **Pushback** 0.5 s (*Tongue Lash*); Naga (boss) poisons every trapped hero, reach 2 (*Coil Venom*) |
| Slimes, marsh shamblers, ice mauler, tree golem 06 | **Status** slowed 35–50 % (*Ooze*, *Bog Grip*, *Rime Grip*, *Root Snare*) |
| Scorpion, skelepedes, corpse | **Status** poisoned 30–40 % (*Venom Sting*, *Rot Bite*, *Rot Miasma*) |
| Soldiers, ceramic knights, captain | **Buff** protection (*Close Ranks*, *Shield Wall*), ward (*Kiln Ward*), empowered (*Press the Attack*); Captain empowers the whole chain, reach 2 (*Battle Cry*) |
| Templars | **Heal** 12 % (*Battle Hymn*) or **Cleanse** the chain (*Purify*) |
| Gangers, goblin thug, lurkers, steppin razors, prototypes | **Status** blinded 35 % (*Pocket Sand*) or **Crit** +20–25 % (*Gang Up*, *Cheap Shot*, *Ambush*, *Razor Rhythm*, *Overclock*) |
| Oni, cyclopes, troll, mecha armor, yeti, bone pile | **Pushback** 0.5–0.75 s (*Club Sweep*, *Stomp*, *Piston Ram*, *Snow Shove*, *Rattle* 0.25 s); Cyclops bosses 1 s at reach 2 (*Boulder Toss*), Mountain Troll 1 s (*Landslide Shove*) |
| Tech gremlins, cyber zombies, phantom | **Status** silenced 25–30 % (*Short Circuit*, *Signal Jam*, *Wail*); gremlin01 **Haste** 0.75 s (*Tinker Boost*) |
| Mannequin | **Haste** its attacker 0.75 s (*Puppet Strings*) |
| Undead, ghost, gravestone | **Status** slowed 25–40 % (*Grave Grasp*, *Grave Chill*) |
| Tree golems | **Buff** regen (*Verdant Sap*) |
| Hags | **Cast** their Miasma at 50 % (*Hex Echo*) |
| Swamp Mistress | **Heal** the chain 12 %, reach 2 (*Fen Mending*) |
| Reaper | **Crit** +40 %, reach 2 (*Death Mark*) |
| Demon Lord | **Status** burning 75 %, reach 2 (*Hellbrand*) |
| Chronomancer | **Haste** all its allies 1 s, reach 2 (*Time Skip*) |
| Naga egg | **Heal** 5 % (*Brood Bond*) |

How each effect resolves from the enemy side:

- **Heal, Buff, Cleanse** land on enemy allies exactly as on heroes. A **Turns** buff lent to the enemy whose own turn closed the pincer gets one extra turn (`BattleRunner.supportTurnOwner`). An enemy's turn buffs tick down as its turn ends, so without that extra turn the buff would expire before any hero could hit it.
- **Haste** moves enemy turn icons left by that many seconds, or rushes an enemy cast. It lends no party tempo, because tempo belongs to the heroes.
- **Pushback** adds to the enemy pincer's own push, which is 0 s because only hero pincers carry `PincerPushbackSeconds`. A hero has no turn icon, so the push has nothing to delay on its turn. What it does hit is a pending hero **cast**: the cast slips by that many seconds, is never cancelled, is pushed once per action and is capped at 2 s.
- **Status, Cast and Crit** hit the trapped heroes, or lend the enemy attacker crit, through the same code as the hero side.
- **Mana** is hero-only. `ContentLoader.Validate` rejects a Mana support on any non-hero class.

The Bestiary shows the named sentence at the abilities tier (10 defeats), for example "As a [Supporter] (reach 1), Tongue Lash: adds 0.5 s of [Pushback] to each trapped foe." A foe's InspectCard shows the same sentence once it reaches that tier. *(Tests: `EnemySupportTests`.)*

*Presentation:* the supporter's cut-in card names the `Label`; its float and feed line add the effect ("Shadow Edge! +30% Crit", "Hex Echo! Fireball", "Venom Rain! Poisoned", "Shove! Pushback +0.5 s"), one sound per effect (POL-D4, reused ids: heal, orb, crit, status.debuff, whoosh, powerup, cast). `SupportText.Describe` renders the rule as one sentence with glossary [terms] — "As a [Supporter] (reach 1): gives the attacker +30% [Crit] chance on its pincer blows." — on the hero InspectCard (a foe's once its abilities are known), the hero page, the Abilities screen, the mercenary offers and the new-game party pick. *(Tests: `SupportEffectTests`, `Every_hero_class_performs_its_own_support_action`.)*

#### 1.2.4 Resolution order inside one pincer

1. `PincerFormedEvent` (attackers, opponents, supporters).
2. Attacker A strikes each living opponent in line order; then attacker B does the same. **Both endpoints hit the whole line.** A hero whose weapon is on its last durability point strikes at ×1.5 and then shatters ([§24.5](#245-weapon-durability)). An attacker with the **Double Attack** passive strikes each opponent a second time at ×0.5; any struck defender with **Counter Attack** that survives a landing hit strikes back at ×0.5 ([§7](#7-the-spell-catalog)).
3. Each supporter performs its support action (`SupportActionEvent`, then its effect): a Strike supporter strikes each living opponent at 50%; the others heal, buff, haste, charge mana, cleanse, echo a spell, inflict a status or add Pushback ([§1.2.3](#123-supporters)). **Amended 2026-11-06 (GG-T298):** a **Crit** supporter acts first, between steps 1 and 2 (its `SupportActionEvent` precedes the blows and its loan ends after step 2); the pincer's Pushback (one push per foe: its own seconds plus every Pushback supporter's, capped) lands after step 3.
4. (Hero team only) each living attacker, then each living supporter, mints one orb.

**Amended 2026-11-24 (owner, GG-T833, battle-feel epic GG-T823): a pincer resolves in three phases.** Owner: *"you must be able to tell a hero was buffed by a supporter before attacking, and a ninja threw a shuriken after the pincer."* Supports that help the attacker used to land after the blows they should have empowered; now steps 2–3 run as:

- **Prep** — each living supporter whose support helps the attacker acts, in supporter order, before any blow: **Crit** (the loan, as before), **Buff** on the Attacker, Chain or Party, **Heal**, **Haste**, **Cleanse** and **Mana**. An Empowered or healed attacker strikes with it in this same pincer.
- **Blows** — attacker A strikes the line, then attacker B (step 2 unchanged), and the Crit loan ends.
- **Follow-up** — the **Double Attack** follow-ups (each pincer blow's `OnAttack`, held back until both attackers have struck), each attacker's **OnPincerAsAttacker** triggers, then the supporters that hit the trapped foes — **Strike**, **Cast**, **Status**, **Pushback** — and a **Self** Buff (a taunt). The Pushback push lands after it.

Each supporter's `OnPincerAsSupporter` triggers fire right after its own support, in whichever phase that is. `PincerPhaseEvent { Phase, PincerIndex, End }` brackets each phase (an opening and a closing event, `PincerIndex` = the pincer's `ChainIndex`) so the view can stage them. Enemy pincers use the same phases. Melee, counter and other non-pincer blows still fire `OnAttack` at once. *(Tests: `SupportEffectTests.A_supporters_empowered_raises_the_same_pincers_damage`, `A_follow_up_status_lands_after_the_blows_inside_the_follow_up_phase`.)*

A pair whose opponents are all already dead (killed by an earlier link) is skipped. **Amended 2026-10-09:** the old cosmetic "Respite" spin is not built.

#### 1.2.5 No pincer = legal

A drop with no valid pincer is a legal action: the hero stays, the clock advances one action.

#### 1.2.6 Power Amplifier (added 2026-10-10, owner request)

Owner: "Add in the Power Amplifier from the original Terra Battle … if you align attacker1 or attacker2 to it it enhances the attack; it spawns on a random schedule modified by the cumulative luck of the Hero team; the Enemy team cannot utilize it but can move through it to reposition it, with the intent of breaking enhanced pincer combos."

- **Spawn:** when control returns to the player and no amplifier is on the board, it appears on a random free tile (`FreeForItem`: no unit, trap or pickup — two items never share a tile, §1.1.3) with chance `clamp(0.12 + 0.003 × party Luck, 0, 0.45)` (`Formulas.AmplifierChance`; `AmplifierSpawnedEvent`). At most one at a time. It is a movable tile (§1.1.2): it occupies its tile and is pushed around like an actor.
- **Use:** a hero **pincer attacker cardinally adjacent to it** (aligned, §1.1.2) when its pincer resolves strikes at **×1.75** and mints a bonus orb ("amplifier"); the amplifier is used up (`AmplifierUsedEvent`). Supporters don't trigger it.
- **Enemies:** never use it. An enemy stepping into it **pushes it to the tile the enemy just left** (a 2×2 drags it along its lane; `AmplifierMovedEvent`). Clever personalities (Weakest, Threat) seek it out to break an amplified setup (+4 step score).
- **View:** a pulsing gold diamond on its tile; pops in, slides when shoved, flashes a ring and "AMPLIFIED!" when used.
- *(Tests: `Amplifier_chance_grows_with_party_luck_and_caps`, `An_attacker_on_the_amplifier_strikes_harder_adds_mana_charge_and_uses_it_up`, `Enemies_cannot_use_the_amplifier_but_stepping_on_it_shoves_it_back`.)*

#### 1.2.7 Outline & view layering (GG-T65 investigation, 2026-10-21)

The pincer outline (`board.Lines.Outline`, `BoardView.GroupRect`) is drawn **exactly one tile thick along the pincer's axis**, widened only by the same multi-tile-opponent allowance [GG-LAW-2](#GG-LAW-2) already states (`PincerGeometry.Error`'s guard: one big opponent may widen it to its own width; two opposed big opponents, `2×widest−1`).

GG-T65 was opened against a screenshot of a ~2×3 gold loop wrapping a "SUPPORT" cut-in card, two hero portraits and a "MISS" float — wider than this battle's geometry should ever draw. Investigating it found two separate, since-fixed view defects, not a hole in `PincerDetector`/`PincerGeometry`:

- **GG-T98** (closed): the outline read live `State` positions, already end-of-command by playback time — a unit that moved later in the same command (an enemy turn, knockback, a later chain link) stretched the box across wherever it ended up. Fixed by snapshotting every member's tile onto `PincerFormedEvent`/`UnitFootprint` at formation time, guarded by `PincerGeometry.Error` in dev builds. *(Test: `PincerGeometryTests.Pincer_reports_formation_time_tiles_when_the_foe_darts_away_in_the_same_command`.)*
- **GG-T128** (closed): an earlier pass at this same ticket's z-order need (cut-in cards must never cover the outline) reparented `board.Lines` out of the board and onto `App.Overlay`, so it stopped inheriting `boardHost`'s camera twist/zoom/shake — it then sat still while the board (and anything riding it) tilted or shook, reading as a mismatched box around whatever UI shared the frame. Fixed by putting the line layer back inside the board's transformed tree and meeting the z-order need with a board-local `cutInLayer` sitting just below `board.Lines` instead.

Current layering (verified against both fixes, `BattleScreen.cs`'s board-build comment: "tiles, units, marks, fx, cut-in cards, lines"): `board.Lines` (outline + drag path) and `cutInLayer` (`ActionCard` pop-ups — `SupporterPop`, chained `PopUpCards`, `CasterSlide`) both live inside `board.Root`/`boardHost`, so the outline and these cards ride every camera twist, zoom, drift and shake together, and the cards never draw over the outline. The full-screen `PincerCutIn` portrait sweep (first link of a chain only) is deliberately left outside that tree — a cinematic overlay unrelated to tile alignment, same as the GridGame2026 cut-in it ports (US-145) — and GG-T128's own fix evidence recorded that this split still meets GG-T65's original z-order need. GG-T126 separately confirmed the Pause veil draws above every one of these battle layers, including mid-chain.

No change was needed in `PincerDetector`/`PincerGeometry`/`BoardView.GroupRect`: the shape rule they enforce (one tile thick, widened only by a multi-tile opponent) is correct and tested; the ~2×3 box this ticket reported was these two now-fixed view bugs.

#### 1.2.8 Combo (GG-T849, tests GG-T911)

The hero **combo** is a display-and-stats meter, Devil May Cry style: it builds while the party acts and shows a rank. It has **no reward** - nothing in the battle reads it. `ComboTracker` (`Core/Battle/Combo.cs`) lives on `BattleState.Combo`, is cloned with the state and is deterministic (no Rng).

**What credits it (`ComboKind`).** Each credit goes to one hero: `Pincer` (both attackers of a hero pincer), `ChainLink` (both attackers of chain link 2 and later), `Support` (a `SupportActionEvent` - the supporter earns it, not the attackers), `FollowUp` (a supporter or attacker's follow-up in the follow-up phase), `Status` (a hero inflicted a debuff on a foe), `Kill` (a hero's blow killed a foe) and `TileKill` (a foe a hero shoved died to a tile hazard - credited to the shover). A hero acting inside a pincer it is not one of the two attackers of counts as a *supporter* (`ComboShare.SupporterPoints`); pincer and chain credits are *attacker* points.

**Points** are `BattleConfig.Combo*Points` (Pincer 10, ChainLink 15, Support 8, FollowUp 10, Status 6, Kill 12, TileKill 18 - first-pass, balance GG-T276). **Variety:** a contributor, kind or kind+effect new to the window adds `ComboNewContributorBonus` (6), `ComboNewKindBonus` (4) and `ComboNewEffectBonus` (3); the window's opening credit earns none. **Halving:** the same unit repeating the same kind and effect earns half the base each time (10, 5, 2, ... never below 1). **Ranks** D, C, B, A, S, SS, SSS are reached at `BattleConfig.ComboRankThresholds` = 10, 30, 60, 100, 150, 220, 300.

**Window.** One combo is one party turn as `TurnRules` defines it: it closes at `TurnRules.IsTurnBoundary` (every `TurnModes.Available` mode - the window running out, or a wave clearing) and at `BattleEndedEvent`. Closing emits `ComboEndedEvent` (final rank, score, hits, per-unit attacker/supporter `Breakdown`) and resets the tracker; a window with no credit emits nothing. `ComboChangedEvent` fires on every credit (points, new score and rank, `RankUp`).

**Results.** `BattleResults.BestComboRank` / `BestComboScore` keep the highest-scoring window; `ComboAttackerPoints` and `ComboSupporterPoints` total each hero class's points across the battle.

**Decision (GG-T911): enemy chains get no combo.** Only `Team.Hero` units can be credited, and an enemy pincer or chain emits no `ComboChangedEvent`/`ComboEndedEvent`: the enemy side has no meter or callout of its own, because its pincer floats already show the chain.

**The meter (GG-T851, `App/Battle/BattleScreen.Combo.cs`).** The combo events play off the beat (they never hold up playback) on a meter pinned inside the board's top-right corner, in screen space, taking no input: the board is the same design size on every stage aspect, so it is portrait-safe everywhere. A Shapes frame (§11.6, its border tinted the rank's colour, D cool grey up through C green, B blue, A violet, S gold, SS orange to SSS red) holds the rank letter, which slams in bigger and hotter per rank with a frame flash, a screen shake and a `battle.powerup` sting pitched up per rank (S and up strobe white as they settle); a rank word and the running score and hit count sit beside it. A decay bar along the frame's foot refills on every credit and drains over 3 s; run dry, the meter sags until the next credit. Each credit pops a contributor chip under the frame - the hero's thumbnail, "+N" and the credit's label in the `CombatTextKind.Combo` style - mana blue for a supporter (a `Support` credit, or a follow-up by anyone but the pincer's two attackers), so their share is seen; three chips at most. `ComboEndedEvent` stamps the final rank and score, lists each hero's total with its attacker / supporter split (`ui.diamond` at S and up), holds, then fades the meter out. Reduce Motion keeps every number, colour and sound but drops the slam, rotation, shake, strobe and chip slides for plain fades.

*(Tests: `ComboTests` - supporter credit, variety vs halving, bonuses, rank thresholds, foes never credited, enemy pincer silent, window close in every available turn mode and at battle end, results stats.)*

<a id="13-humanoid-restriction"></a>

### 1.3 Sentient restriction

**Amended 2026-11-06 (owner, GG-T289, refined by GG-T294): a pincer takes a society, not a body shape.** ~~Only **Humanoid** actors can be pincer attackers (`Unit.IsHumanoid`; enemies need the `Humanoid` tag).~~ Heroes are always pincer-eligible. An enemy is eligible only if its class is tagged **`Sentient`** (`Unit.CanPincer`): it has a society or plans ahead, however rudimentary — soldiers, templars, captains, mecha and robots, goblins, gremlins, orcs and trolls, frog folk, hags, naga, and **intelligent undead** such as vampires and mummies. **Beasts, slimes, yetis, scorpions, tree golems, remains and mindless undead** (the shambling undead, cyber-zombies, skelepedes) never pincer. It is about sentience, not undeath (owner 2026-11-06). `CanPincer` replaces `IsHumanoid` everywhere it was read: `PincerDetector`, the enemy turn's pincer resolve, `EnemyPlanner`'s pincer-seeking and its Threat-vs-Closest `Auto` targeting, and the Bestiary's targeting label. *(Tests: `PincerEligibilityTests` — `Sentient_enemies_pincer_a_hero_line`, `Enemies_without_a_society_never_pincer`, `A_frog_closes_the_pincer_on_its_own_turn`, `A_yeti_flanking_a_hero_only_melees`, `Heroes_always_pincer`, `Real_content_tags_society_not_body`.)*

**Amended 2026-11-06 (owner, GG-T303): the Pincer keyword.** `Unit.CanPincer` = hero team **or** `Sentient` **or** the **Pincer** keyword (`Unit.HasPincerKeyword`: a tamed or domesticated monster, or a class tagged `Pincer`). A tamed beast is on the hero team, so it ends and supports *hero* pincers with the party; once its tame wears off it is a mindless enemy again and never pincers ([§25.11.3](#25113-taming-and-domestication-added-2026-11-06-owner-gg-t303)).

Multi-tile actors are never attackers. Sentient 1×1 enemies **seek** pincers (+50 move score, [§14.1.2](#1412-score-weight-cheat-sheet)) — **Amended 2026-11-06 (owner, GG-T306):** and actively **engineer** them, swapping places with monsters and heroes to complete a pincer, set up an end or slot a supporter into the chain ([§14.1.3](#1413-pincer-planning-gg-t306)) — and, **Amended 2026-10-09**, **resolve** them on their turn: after moving, if the enemy is one end of an enemy-team pincer, every such pincer resolves against the trapped heroes (no orbs are minted for enemies); otherwise it melees ([§14.1](#141-enemy-planning)).

### 1.4 A full action, end-to-end (worked example)

Cleric `[C]` at (2,3) and Knight `[K]` at (5,3); two goblins `[g]` at (3,3) and (4,3).

```
Step 0:   col: 0  1  2   3   4   5          Step 1 (Knight drags west one tile):
  row 3:      .  . [C] [g] [g] [K]            row 3:  .  . [C] [g] [K] [g]
```

1. The Knight enters (4,3); the goblin there is displaced into (5,3) (`DisplacedEvent`).
2. `DoMove` commits; `ResolvePincers` finds C(2,3) — g(3,3) — K(4,3): one opponent, valid.
3. The Knight (the dropped hero, attacker A) strikes, then the Cleric; no supporters.
4. Two orbs mint, one per attacker.
5. The pincer carries Pushback: the goblin it hit acts 1 s later, wherever its icon was ([§2.3](#23-pushback-a-keyword-in-seconds), **amended 2026-11-06**, GG-T286).
6. The clock runs until the next enemy icon reaches the left edge and that enemy takes its turn (GG-T188, [§2.0](#20-the-held-turn)); any cast that reaches 0 on the way resolves.
7. Control returns: hero turn-unit buffs and skill cooldowns tick, enemy Frozen ticks, `PlayerControlEvent`.

That is the full beat: **drag → slide cascade → pincer → strikes → orbs → pushback → clock → enemy turns/casts → control returns.** *(Test: `Worked_example_1_4_slide_then_pincer`.)*

### 1.5 Multi-tile actors (2×2 bosses)

Enemies may occupy a rectangle (`ClassDef.Footprint`, e.g. `cyclops00` = 2×2); **heroes are always 1×1**. `Unit.Pos` is the anchor (top-left); the actor covers `Pos + (dx,dy)` for `dx<W, dy<H`. Occupancy, adjacency (`Unit.IsAdjacentTo`) and distance (`Unit.DistanceTo`) are footprint-aware. Spawn needs the whole rectangle free (`FootprintFree`, which also refuses trap tiles).

**The four rules:**
1. **Immovable anchor.** A hero step into any boss tile is refused ("immovable"); the boss is never displaced. Pincer hits still push its **timeline icon**.
2. **Pincer by flanking its width.** Every tile between the two attackers must be covered by opponents; the boss's tiles count as **one** opponent. *(Test: `Two_by_two_boss_blocks_slides_and_is_pincered_across_its_width`.)*
3. **Boss shoves heroes (on its turn).** **Amended 2026-10-09:** the boss steps its footprint one cardinal tile (`EnemyPlanner.FootprintStepLegal`: in bounds, no other enemy in the way); every hero in a newly entered tile is moved directly to **the tile the body vacated on that hero's lane** (`BattleRunner.VacatedTile`) — no cascade, never off-board. The step aborts only if a non-hero occupies an entered tile. Shoved heroes wake (BreaksOnMove) and trigger traps.
4. **One timeline icon** regardless of size; the boss melees via footprint adjacency; it never pincers (multi-tile units are not attackers).

---

## 2. The Timeline

**Amended 2026-11-06 (owner, GG-T286/GG-T287, inspired by Final Fantasy Resonance): one bar, no Zone, icons flow LEFT.** A horizontal track under the top HUD. Every icon (enemy turns and every cast, hero or enemy) moves left at its own steady rate, and **reaching the left edge is acting**. Where an icon sits is simply how long until it acts: `u = secondsUntilAct / BarSeconds` (`BattleEngine.SecondsUntilAct`, `BattleEngine.BarU`), with `BattleConfig.BarSeconds` = **10 s**; anything further off than 10 s waits at the right end. There is no ACT zone, no Pushback Zone and no crawl: the old `ZoneStart`/`ZoneU`/`ZoneCrossingTicks` are gone.

**Amended 2026-11-06 (owner, GG-T318): Speed sets where a foe re-enters, not how fast it travels; a recovery beat
spaces the foes.** Owner: "I want the 'average' time to move to be 4 seconds … when an enemy restarts on the bar they
start 4 seconds away on the far right but their speed is calculated so that they start a little more to the left the
faster they are, that's the main point of the speed stat for enemies; there would also need to be a 'respawn interval'
which takes place after the enemy attacks but before they appear back on the bar, the idea is to distribute enemies so
that the player can fight back." And: "this is the lifeblood of the entire game, this is what sets it apart from Terra
Battle's fixed 4 seconds."
- Every icon now drains its seconds at **one rate** ([§2.1](#21-speed-and-pace)). `BarSeconds` = **6 s**, the latest a
  foe re-enters.
- After acting, a foe leaves the bar for its **recovery**, then re-enters at its **entry**, set by its Speed: an
  average foe 4 s out, a faster one further left ([§2.12](#212-recovery-and-spacing-gg-t318)).
- *Why no Grandia-style ACT zone* (the owner asked for this to be weighed without preconceptions): Grandia 2's ACT
  stretch made commitment visible, aimed counterplay at it, and gave actions different time costs.
  - Heroes have no icons here (the window is the party's), so a hero-side COM/ACT stretch has nowhere to live.
  - Enemy charges already are that committed, cancellable stretch for spells (§13.4).
  - A band would break "position = seconds until acting".
  - So the action-dependent cost became the recovery (`RecoverSeconds`). Telegraphing a physical attack's target in an
    enemy's last seconds is a candidate follow-up.

```
 prominence     u = secondsUntilAct / BarSeconds (BarSeconds = 6 s)          recovery
  diamond   0 s  1    2    3    4    5    6 s                                 tray
   ◆◆◆      ◆━━━◇━━━━◇━━━━◇━━━━◇━━━━◆━━━━◇                                  ◇◇◇   [GUARD]
  ◆next◆    ◇ = 1 s delimiter, ◆ = 5 s delimiter (no Zone, no ACT)          ↑ just acted: off the bar,
   ◆◆◆      ◀── every icon (turn or cast) moves left at ONE rate              counting down its recovery,
 "NOW"/2.4s     (20 IP/tick × tempo × status × dial); at 0 s it acts          then slides on at its entry
                fast foe re-enters here ─┘    └─ an average foe, 4 s out
```

Turns and casts share the one axis, so a cast is a racer like any turn: it is born at `castSeconds / BarSeconds` and acts when it reaches the left edge. *(Tests: `TimelineResonanceTests` — `An_icon_sits_at_its_seconds_until_acting_over_the_bar`, `A_cast_is_born_at_its_cast_time_on_the_bar`, `Slow_on_an_enemy_lets_an_in_flight_cast_overtake_it`.)*

<a id="20-the-act-advanced-clock"></a>

### 2.0 The held turn

**Amended 2026-10-09 (owner decision):** the clock is **act-advanced**. **Amended again 2026-10-21 (owner, GG-T188 — reverses the act-advanced clock's "an action is 2.0 s"):** the timeline **runs while a hero is held**.

- Time is integer **ticks**, `BattleConfig.TicksPerSecond` = 10. A turn icon still loads `BarLength` = 1000 IP internally, but it is **drawn** at its seconds until acting (`u = secondsUntilAct / BarSeconds`, GG-T286).
- **Looking is free.** Nothing moves while you inspect, select or pick a target.
- **Picking up a hero starts the turn.** The timeline then runs in real time: the player's turn is the time from pickup until the **next enemy icon reaches the left edge** (acts) — `BattleEngine.TurnTicks` (the soonest `TicksUntilTurn`, no future pushback assumed), shown as `TurnWindowSeconds` = ticks ÷ 10 real seconds. ~~max(`MinTurnSeconds` 2 s, …) (the floor slowed the playback on a very short turn)~~ — **GG-T311:** no floor; a gap too short for a drag can't start one (below).
- **Forced drop.** When the window runs out the hero is dropped **where it is** — the path walked so far is the move — and that enemy acts. `MoveCommand.HeldSeconds` carries the seconds held; at or past the window (`BattleEngine.IsTimeUp`) the drop is forced (`TurnTimeUpEvent`) and the turn ends **even if the hero never left its tile**.
- ~~**Dropping earlier ends the turn early.** Every accepted command ends the player's turn: the clock runs until an enemy turn resolves.~~ **Amended 2026-11-06 (owner, GG-T308): the window is the party's, and every hero acts once in it.** Owner: "Pushing an enemy back gives you more time to move your selected hero — slide through heroes and enemies alike and create more alignments … The pushback happens after the drop (the hero acts) or as the result of an Ability, Item or Spell, all three of which consume that hero's turn."
  - **The window** is the time until the next enemy (or enemy charge's owner) reaches the left of the bar — `BattleEngine.TurnTicks`, the same gold hold lane and seconds readout. It is a **budget shared by the party**.
  - **Each living, able hero acts once per window** (`Unit.ActedThisWindow`; `BattleEngine.HeroesLeftToAct`): one drag, or one Ability/Item/Spell (or Talk). A hero who has acted can't be picked up or use its bar (`CanMove` false, `SlotStatus.Acted`; the engine refuses "Already acted").
  - **What an action spends** (`BattleEngine.CommandTicks`, run after the action resolves, measured against the window as it stood at pickup): a **drag** spends the share of the window it was held (`HoldTicks` = ⌊held ÷ `TurnWindowSeconds` × `TurnTicks`⌋, at least 1 tick; a forced drop spends all of it) — the same rate the view plays the bar at; an **Ability, Item or Spell** spends a fixed `BattleConfig.HeroActionSeconds` = **1 s** (a spell's cast time still rides the bar separately).
  - **Pushback and Slow buy time.** A pincer's pushback or a Pushback keyword lands with the drop/ability, *before* the time is spent, so pushing the foe that ends the window lengthens it for the heroes still to act; a Slow on it does the same through its rate.
  - **When the window ends:** if the time runs out during an action, that enemy acts there and then (the rest of that action's cost is not carried over); when **every** able hero has acted, the clock runs on to the next enemy turn — leftover time is **not** banked (only Guard banks it); Guard gives the rest of the window up ([§2.5](#25-auto-skip--shield)). After the enemy turn a **new window** begins and every hero may act again (`PlayerControlEvent.NewWindow`). Clearing a wave also opens a fresh window (no advance); with no enemy icon able to arrive (none left, all Frozen) a spent window is the fixed `ActionTicks` = 20 ticks.
  - **Turn boundaries are windows.** Mid-window control returns with `PlayerControlEvent { NewWindow = false }` and nothing else; hero buffs and skill cooldowns, Frozen on foes, tempo effects, pickups, corpses, the Amplifier roll, Regenerate and the TurnStart/TurnEnd triggers all count **windows** (party turns), not hero actions. ActionEnd triggers still fire after each hero's action.
  - **Seen:** a hero that has acted greys out with a ✓ on its token until the window ends (`UnitView.SetActed`, eased); mid-window the selection moves to a hero who can still act and a banner says "N heroes can still act · 2.3s left"; the next enemy keeps its gold ring; the hold lane shows what is left of the window.
- **Amended 2026-11-06 (owner, GG-T311): minimum action time — heroes go first, but only act in a gap wide enough.** Owner: "Heroes go first in the round, but unless they can get in a pincer between the intervals between enemies hitting left they have to sacrifice their turn, and that could lead to several enemy attacks in a row before a window wide enough for the player to act becomes available."
  - **The gap** is the window as it stands: seconds until the next enemy turn icon reaches the left edge (`BattleEngine.TurnTicks`; an enemy charge landing does not end it).
  - **An action starts only if the gap is at least its minimum** (`BattleEngine.MinActionSeconds`, `ActionFits`, compared in whole ticks rounded up — `NeedTicks`): a **drag** needs `DragPickupSeconds` **0.5 s** + `DragSecondsPerTile` **0.35 s** × one tile = **0.85 s** (`MinDragSeconds`; the human drag model is `DragSeconds(tiles)` = 0.5 + 0.35 × tiles); an **Ability, Item, Spell or Talk** needs its use time, `HeroActionSeconds` **1 s**. Too short: `CanMove` is false, the slot reads `SlotStatus.TooLittleTime`, Talk is unavailable, and the engine refuses the command with "Too little time" at no cost.
  - **A drag spends at least its minimum** (`CommandTicks` = max(held share, 0.85 s), never more than the window): a flick is still a pickup and a step. During the drag real time is spent as before, and the forced drop still applies.
  - **Forced sacrifice.** When heroes are left to act but no action of theirs fits (`BattleEngine.TooLittleTime`), the party can only **Guard**: it gives the sliver up, the enemy acts, and the next gap opens. If that one is too short as well, the party Guards again — a tight cluster of foes is several enemy attacks in a row. The forced Guard **keeps Guard's Protection on every hero and banks the time left as mana charge** (`GuardChargeFor`, so a 0.3 s sliver is 3 charge): it softens the blow without replacing an action.
  - **Rally** (`BattleState.Rally`): each forced Guard slows every enemy icon one tempo step (×0.8, stacking to `RallyMaxStacks` **5** ≈ ×0.33, on top of — outside the clamp of — the party's tempo), and the next hero action resets it (a new wave too). Without it, foes whose turns all fall closer together than an action (three wolves lapping in 2 s) would lock the party out for ever; with it, "several enemy attacks in a row" always end in a window. The banner adds "Rally N: foes slowed".
  - **Heroes act first.** A wave's enemy turn icons are held back at spawn so none reaches the left edge sooner than `OpeningWindowSeconds` = **3 s** (`BattleRunner.OpenPartyWindow`) — the party always opens the battle and every wave. ~~A foe too fast for that started from the beginning of the bar, a full lap.~~ **GG-T318:** the wave is scheduled one foe at a time through the spacer ([§2.12](#212-recovery-and-spacing-gg-t318)), each from its entry with recovery raised to the 3 s floor.
  - **GG-T318:** with recovery and spacing a lock of gaps all too short is rare (a crowded wave, eggs, remains), and Rally stays as its safety valve.
  - **Opening gaps.** Clusters are the danger; a pincer's pushback, a Pushback keyword or a Slow on the foe at the front of a cluster opens a gap behind it (pushing the *second* foe of a tight pair before the first acts turns the next gap from a forced Guard into a window).
  - **Seen:** the bar draws the current gap as a soft lane from the left edge to the next enemy icon with its seconds — **green** "move or skill", **amber** "move only", **red** "Guard" (`BattleScreen.SyncGap`); with no room, a banner says "Too little time — Guard (0.4s gap, a move needs 0.85s)" and GUARD is lit gold (`SyncGuardCall`); a slot reads "Too little time — needs 1.0s". Automation `/state` carries `too_little_time`.
  - **Bots model a human** (`HeroBot.ThinkSeconds`: smart **0.4 s**, casual **1.0 s** of hesitation while holding; a hop of `QuickDragTiles` 2 or fewer thinks half as long, and after `HurryAfterGuards` 2 Guards in a row it hurries with no think time — a player locked out of a usable window eventually just moves): a bot drag holds think + `DragSeconds(tiles)` (`MaxDragSteps(s, think)` tiles fit; a longer path is a forced drop), so a gap shorter than think + pickup + a tile offers it no drag; it never acts in a gap too short and Guards when forced; the smart bot still prices pushback/Slow by the hero actions the gap they open makes room for.
  - **Tuning (GG-T311):** `IpPerTickPerSpeed` 1.6 → **4.2** (Speed 10 laps in 2.4 s) and the difficulty dial gains an `EnemySpeed` column ([§22.9](#229-difficulty-settings-scaffolding-added-2026-10-11-owner-direction)). Per-stage 8 soak (15 stages), smart / casual wins: difficulty 10 **100% / 100% → 79% / 33%** (greenvalley 83/25, desert 91/41, swamp 66/25, cave 87/58, cityruins 66/16; no stage unwinnable for the smart bot), difficulty 5 100/100 → 99/85, difficulty 1 100/100 → 100/98. A campaign bot that never manages its camp no longer finishes at 10; the greedy+camp bot finishes 3/4 seeds (the trio no-story run 2/4).
  *(Tests: GG-T311 `MinActionTimeTests` — `Heroes_act_first_in_every_wave`, `An_action_is_refused_when_the_gap_is_shorter_than_its_minimum`, `Two_enemies_close_together_force_two_sacrifices_in_a_row`, `Pushback_opens_a_gap_that_then_allows_an_action`, `Bots_never_act_in_a_gap_too_short_and_replay_identically`.)*
- **The view, not Core, runs the held clock.** Core never ticks during a hold; `BattleEngine.TimelineUAfter(ticks)` gives the view where every icon would be, so the icons glide left in real time. The remaining time shows twice: a gold **lane** from the left end of the track to the next enemy icon (reddening as it runs out; GG-T286), and a **drain bar** under the held hero (green → red). A paused battle stops the hold.
- **Bot driver.** `HeroBot` drags ~~at `BotSecondsPerStep` = 0.25 s a tile~~ like a human (GG-T311): think time + 0.5 s pickup + 0.35 s a tile; a path longer than `MaxDragSteps` is cut where the time runs out (a forced drop), and every bot move carries its `HeldSeconds`. Since GG-T308 it is asked once per hero action, so it plays several actions a window; the smart bot also weighs the window as a budget (`TimeBalance`: a pushback on the foe that ends the window is worth the extra hero actions it makes room for — `ActionWorth` 4 each — and a long drag or a forced drop costs the actions it denies), prefers a setup an ally still to act can close this window, and counts a stall per window. The soak (`Bots_play_every_stage_without_breaking_invariants`) reports average turn length and forced drops per stage.
- Refused commands and no-op drags (let go early without moving) cost **nothing** (no ticks, `ActionsTaken` unchanged).
- **Cancelling a drag** (*added 2026-11-05, GG-T252*): releasing the held hero **off the board**, or back on **its own tile with nothing shoved** (the shove preview is empty), is a cancel — no command, no clock, `ui.cancel` and "Move cancelled" (`BattleScreen.DropCancels`/`CancelDrag`). A walk that returns home after shoving someone still commits (shoving is a tactic, GG-T190). A forced drop (the window ran out) is never cancelled.
- Each tick: every turn icon advances (or burns a stun tick), every cast icon counts down, tick-unit buffs accumulate ([§8.3](#83-ticking)), then arrivals resolve ([§2.4](#24-turn-order--arrival-order)).
- After the advance that ends a window (GG-T308), **control returns** (`ReturnControl`): hero turn-unit buffs and skill cooldowns tick, enemy timeline-halting buffs (Frozen) tick — skipping the boundary of the action that applied them — and `PlayerControlEvent`. The pending time bank is kept until the next enemy turn mints it.
- **Only enemies have turn icons.** **Amended 2026-10-09:** heroes have no icons; any living, mobile hero may act on any action. (The old bible drew hero portraits above the line.)

*(Tests: `Clock_only_advances_on_committed_actions`, `Unaffordable_or_silenced_spell_is_refused_without_cost`, `Enemy_acts_when_its_icon_reaches_the_trigger`, `Turn_length_is_the_time_until_the_next_enemy_icon_reaches_act`, `Forced_drop_ends_the_turn_where_the_hero_stands`, `Early_drop_ends_the_turn_and_the_next_enemy_acts_on_arrival`, `Countdown_badge_counts_turns_not_ticks`, `Bot_drags_fit_inside_the_turn_window`; GG-T308 `HeroWindowTests` — `Two_heroes_both_act_before_a_slow_enemy`, `Pushback_by_one_hero_lets_another_act_before_the_enemy`, `A_hero_cannot_act_twice_in_a_window`, `Guard_ends_the_window_and_banks_the_time_left`, `A_forced_drop_mid_window_spends_the_rest_of_it`, `A_drag_spends_its_share_of_the_window_and_a_skill_spends_HeroActionSeconds`, `Windows_replay_identically_from_the_same_commands`, `The_smart_bot_takes_several_actions_in_one_window`.)*

### 2.1 Speed and pace

**Amended 2026-11-06 (owner, GG-T286): one linear rate, the whole way.** ~~Open track at Speed × 1.6 IP/tick, then a shared 30-tick Zone crawl.~~
**Amended again 2026-11-06 (owner, GG-T318): one rate for every icon; Speed is where it re-enters.**
- ~~A turn icon gains `round(Speed × IpPerTickPerSpeed × tempo × status)` IP per tick, `IpPerTickPerSpeed` 4.2: Speed 10 laps in 2.4 s.~~
- Every turn icon gains `round(BaseIpPerTick × tempo × status)` IP per tick (`BattleEngine.TurnRatePerTick`). `BaseIpPerTick` = **20** against `BarLength` = **2000** IP, so a full load is 10 s. Enemies are also multiplied by the difficulty dial's `EnemySpeed`. The minimum is 1 IP/tick. Speed is not in the rate.
- **Speed sets the entry.** After its recovery ([§2.12](#212-recovery-and-spacing-gg-t318)) a foe re-enters the bar
  `EntrySeconds` from acting (`BattleEngine.EntrySeconds`, `EntryIp`):
  `EntryBaseSeconds × (ReferenceEnemySpeed(level) / Speed)^EntrySpeedExponent`, clamped to `EntryMin`..`EntryMax`
  (**4 s**, exponent **0.6**, **2–6 s**). `Formulas.ReferenceEnemySpeed(level)` = 11 + 0.75 × (level − 1) is the median
  enemy SPD at that level in classes.json, so a foe is judged against its peers and stays fast or slow at every level.

  | Lv1 foe | SPD | Re-enters |
  |---|---|---|
  | Wolf | 18 | ≈3.0 s |
  | Average | 11 | 4.0 s |
  | Slime | 8 | ≈4.8 s |
  | Bone pile | 5 | 6 s (cap) |
- **tempo** is the party tempo — hero Haste slows every foe, Slow on the party speeds them ([§2.11](#211-party-tempo-gg-t268)); **status** is the owner's own buffs (Slowed ×0.5; Frozen halts it entirely, `Unit.IsTimelineHalted`) — `BattleEngine.TimelineScale`. Slow, Haste and Frozen change only the **rate**, never the icon's place: a Slowed foe simply drifts left more slowly, so a cast or a faster foe behind it can **overtake** it. (A Hasten/Quicken landing *on an enemy* is the one exception: it jumps the icon forward, [§2.7.1](#271-hasten--quicken--the-inverse).)
- A cast icon counts its remaining ticks down one per tick; its `secondsUntilAct` is those ticks ÷ 10.
- `Speed` is the unit's effective SPD: level SPD + gear SPD + ½ gear AGI ([§3.2](#32-hp-and-stats)).
- *(Tests: `Timeline_rate_ignores_speed_and_speed_moves_the_entry_instead`, `Slow_on_an_enemy_lets_an_in_flight_cast_overtake_it`; GG-T318 `TimelineSpacingTests` — `An_average_foe_re_enters_four_seconds_out_and_a_faster_one_further_left`, `Entry_is_judged_against_the_level_so_a_fast_foe_stays_fast_at_every_level`, `Every_icon_flows_at_the_same_rate_whatever_its_Speed`.)*

### 2.2 Icon state

**Amended 2026-10-09:** the old per-icon modes (`Queued / Approaching / PushedBack / Stunned / Resolving`) collapse to data on `TimelineEntry`: `Ip` (turn icons, ≥ 1000 = arrived), `StunTicks` (ticks it will not advance; since GG-T286 only the part of a Pushback that IP 0 can't absorb, cleared by Hasten), `RemainingTicks` (cast icons, 0 = arrived) and `AccumulatedDelayTicks` (stagger, [§13.4](#134-the-interrupt-path)). An owner with a **timeline-halting** buff (Frozen) advances neither its turn icon nor its cast icon (`Unit.IsTimelineHalted`).

### 2.3 Pushback (a keyword, in seconds)

**Amended 2026-11-06 (owner, GG-T286, inspired by Final Fantasy Resonance): Pushback is a keyword, not a property of every hit.** ~~Any hero hit on an enemy in the Pushback Zone shoved its icon back by a depth × STR formula (`PushbackBaseU`/`PushbackMaxU`, 2026-10-11 tuning) and stunned it by AGI.~~ The Zone, the STR/AGI formula and the AGI stun are gone.
- **Only the keyword pushes.** An ability or item carrying `Pushback` (`AbilityDef.Pushback`, seconds) pushes each foe it reaches; **plain hits never push** — pincer strikes aside (below), supporter strikes, spells without the keyword, Mug, damage items, DOT, traps and shatter move no icon.
- **A hero pincer carries it.** Every trapped foe a hero pincer lands a blow on is pushed `BattleConfig.PincerPushbackSeconds` = ~~**1 s**~~ **1.5 s** (GG-T306 rebalance, [§14.1.3](#1413-pincer-planning-gg-t306)) (once, after the supporters act). Enemy pincers push nothing.
- **Anywhere on the bar.** The foe acts that many seconds later wherever its icon sits (`BattleRunner.Pushback`): it loses `ticks × its current rate` IP; whatever IP 0 can't absorb (an icon that just acted) becomes stun ticks, so the delay is always the full amount.
- **Capped and once.** One push is at most `PushbackMaxSeconds` = **2 s** (`ContentLoader.Validate` rejects a larger keyword), and a foe is pushed **at most once per action**, so a chained pincer or a multi-hit skill doesn't stack it.
- **A recovering foe recovers longer** (*GG-T318*): a foe still off the bar ([§2.12](#212-recovery-and-spacing-gg-t318)) has the push added to its recovery. It re-enters later but still at its Speed's entry, and the tray's ring grows.
- **A held cast slips instead.** A target with no turn icon but a cast in flight has the cast's remaining ticks raised by the same amount (`CastStaggeredEvent`); Pushback never cancels a cast.
- **Content:** the knockback and disruption skills carry 0.5–1.25 s (Shield Bash, Shove, Bamboo Slam, Juggernaut Charge, Covering Volley, Stasis 0.75; War Stomp, Haymaker, Phalanx Strike, Guard Break, Wide Cleave 0.5; Riposte, Overrun, Disrupt 1; Pinning Shot 1.25) and the Hinder spell the full 2 s. The old `DelayU` field is this keyword now ([§6.7](#67-movement-timeline-and-drain-gg-t183)).
- **Cast stagger is separate and unchanged** ([§13.4](#134-the-interrupt-path)): any landing hit on a caster still staggers (and can cancel) its cast.

*(Tests: `Pincer_pushes_its_foe_back_near_the_left_edge`, `Pincer_pushback_works_far_from_the_left_edge_too`, `Pincer_pushback_is_the_same_seconds_anywhere_on_the_bar_whatever_the_strength`, `TimelineResonanceTests` — `A_plain_hit_never_pushes`, `The_Pushback_keyword_pushes_its_seconds_anywhere_on_the_bar`, `One_push_is_capped`, `Validate_rejects_a_pushback_past_the_cap`.)*

### 2.4 Turn order = arrival order

Icons advance independently. Within a tick, arrivals resolve one at a time: **cast icons before turn icons, then lower entry id first** (`BattleRunner.Earlier`). **Amended 2026-10-09** (explicit tie rule). A resolving turn resets that icon to IP 0. Pushback, Hasten, Slowed and Frozen reorder icons only by changing IP/rate. **Amended 2026-11-06 (GG-T286):** turns and casts race on the one axis (`secondsUntilAct`), so the order on screen, left to right, *is* the order they act in; ties at the left edge still go casts first, then the lower entry id.

### 2.5 Auto-skip / Shield

**Unchanged by GG-T286/GG-T287** except where it sits: the **GUARD** button (the right end of the timeline, on the right gutter) issues `ShieldCommand`:
- Applies **Protection** (`buffs.json` `protection`: incoming damage ×0.85, 1 Turn) to every hero.
- Runs the clock until **one enemy turn has resolved** — the next actor reaches the left edge (cap 2000 ticks), then returns control — Protection expires at that control return, so it covers exactly the skipped stretch. **Amended 2026-11-06 (GG-T308):** Guard is the party's, not a hero's: pressed mid-window it gives up the rest of the window, so the heroes who haven't acted yet lose their actions; it banks only the seconds still left of the window (what the heroes before it spent is gone).
- Counts as an action; clears the pending time bank.

**Amended 2026-10-09:** it never auto-presses (there is no real-time window to run out). *(Test: `Shield_protects_heroes_and_skips_to_next_enemy_turn`.)*

**Amended 2026-11-02 (GG-T189, owner 2026-10-21): Guard banks unused turn time into mana.** Guard ends the turn early, and the turn time it gives up fills the forming mana bubble ([§3.1.9](#319-mana-charge)) before the clock runs:
- `charge = min(GuardMaxCharge, round(secondsLeft × GuardChargePerSecond))` — `BattleEngine.GuardCharge` / `GuardChargeFor`, reason `guard` (`ManaChargedEvent`; a rollover mints an orb with the same reason).
- `secondsLeft` = the turn window ([§2.0](#20-the-held-turn), `TurnWindowSeconds`) minus `ShieldCommand.HeldSeconds` already used (0 for the GUARD button); with no enemy icon able to arrive, the window is the `ActionTicks` turn it spends (2 s).
- Tuning in `BattleConfig`: `GuardChargePerSecond` = **10**, `GuardMaxCharge` = **40** (under half an orb). Guard ends the turn, so one conversion per turn is the per-turn limit by construction.
- Protection is kept as Guard's defensive effect. The drag-timer time bank ([§3.1.8](#318-time-banked-orbs)) is fed only by moves; Guard still clears any pending drag remainder, so the two never stack.
- The HUD shows it as the filling bubble plus a "Guard · Mana +N · Xs banked" billboard.

*(Tests: `GuardBankTests` — conversion maths, time-left proportionality, overflow at 12 orbs, Guard ending the turn after banking; `Guard_still_banks_the_time_left_to_the_next_arrival`.)*

<a id="26-one-timeline-two-lanes--turn-icons-above-cast-icons-below"></a>

### 2.6 One track, diamond icons, the prominence diamond

**Amended 2026-11-06 (owner, GG-T287, inspired by Final Fantasy Resonance): the timeline is redrawn as one track of diamonds flowing left** (`BattleScreen.Timeline.cs`). ~~Enemy turn circles on a line with the next attacker nearest ACT on the right; casts shown only as a ring around the caster (2026-10-10, "casts are not on the timeline").~~
- **One track, no labels.** A thin line from the prominence diamond (left) to the GUARD button (right), with a **small diamond delimiter every second** and a **larger one every 5 s** across the `BarSeconds` = 10 s span. No Zone overlay, no "ACT".
- **Every icon is a diamond.** A 45° bordered square whose content is clipped to the diamond (overflow hidden, stencil-masked when rotated) around a counter-rotated image: an enemy **portrait** for a turn, the **ability icon** for a cast. Nothing shows outside the diamond.
- **Borders say whose it is:** enemy turn **red**, boss **purple**, hero cast **teal** (`Palette.CastHero`), enemy charge **amber** (`Palette.CastEnemy`). The **next actor** rings **gold** with a lift on the bar, and its board tile gets a gold border too.
- **Casts ride the bar** (fixes the old line-32 split between "below the line" and "not on the timeline"): every cast in flight, hero or enemy (in-playback cast ghosts included), is a diamond on the same track, born at `castSeconds / BarSeconds`; it joins the bar only when its `CastQueuedEvent` beat plays, and a stagger or a Pushback visibly slips it right. The caster's radial `CastRing`, the target's spell chip and the "lands in 3s" banners stay.
- **The prominence diamond.** A large diamond at the far left (the left gutter) shows **whoever acts next** — a portrait for a turn, the ability icon for a cast — with its seconds left under it ("2.4s"), and **"NOW"** while that actor is acting. A new actor taking the slot plays a whoosh.
- **Approach glow.** Inside `ApproachSeconds` = **3 s** of acting, an icon's halo **and its owner's tile** (the caster's tile for a cast; `BoardView` cue layer) pulse together in the icon's own colour, ramping to full at 0 s, so icon and unit read as one; the glow starts with a channel cue. A halted (Frozen) icon doesn't glow.
- **Overlaps:** icons stay centred on the track; overlapping diamonds form a cluster and take turns on top, one per 1.2 s beat (`LayoutIcons`). A unit that isn't moving (Frozen, Asleep, stunned) shows a status badge and dims; the same debuff is a pip on its tile.
- **GUARD** sits at the right end; the hold lane ([§2.0](#20-the-held-turn)) and the tempo readout ([§2.11](#211-party-tempo-gg-t268)) anchor to the track's left end.
- **Spell VFX (owner direction):** every spell plays one readable arc (`SpellVfx`): the caster *draws energy* (motes spiral in, its frame glows in the spell colour), a *cast burst* ring, then a mote per target *curves to the space above it*, from where the element impact plays — lightning strikes down from that point, flames burst, frost rings, heals sparkle.
- **Tap to select, hold to read** (*added 2026-11-06, owner, GG-T288*): before a move — no drag, playback or aiming under way — a **tap** on any timeline icon or on the prominence diamond selects its owner exactly as tapping it on the board does (a hero becomes the selected hero, a foe fills the actor panel). The selected unit is cross-highlighted: a **white ring** on every one of its diamonds (turn, cast, the prominence slot) and a **white outline** on its board tile (`BoardView.SetSelection`). A **hold** still opens the InspectCard ([§9](#9-the-hud)); since GG-T450 the tap also opens it, as a tap card, after selecting. What either shows is gated by the Bestiary knowledge tier ([§25.11](#2511-bestiary)).

**Shared clock — casts fire "off-turn" by design.** Turns and casts advance on the same ticks; whatever arrives first resolves. Lifecycle: up to **4** hero casts may load at once (`MaxHeroCastsInFlight`; enemy charges don't count) — a fifth cast-time spell is refused before any orbs are spent; orbs are spent at cast start; the icon spawns with `ticks = SecondsToTicks(Formulas.CastTime(base, caster))`; on arrival it resolves (`ResolveCast`) and is removed. A spell with `CastTime` 0 resolves immediately.

**Amended 2026-10-09:** every hero spell in `abilities.json` has base `CastTime` 1.5 s; after WIS+INT scaling (12–15 ticks for the starting heroes) it normally resolves **inside the same action's 20-tick advance**, unless an enemy turn earlier in that advance staggers it into later actions. *(Tests: `Cast_time_spell_rides_timeline_then_resolves`, `Fifth_hero_cast_is_refused_by_cast_cap`.)*

### 2.7 Worked example: pushback math

**Amended 2026-11-06 (GG-T286):** ~~the depth × STR example (IP 920, STR 18 → 0.275 u plus a 7-tick AGI stun)~~ is replaced. *(The example below keeps the GG-T286 numbers: 1.6 IP/tick per Speed and a 1 s pincer push. With 4.2 IP/tick (GG-T311) and 1.5 s (GG-T306) the method is the same: seconds × the foe's current rate.)*

A goblin (Speed 10 → 16 IP/tick) sits at IP 920: 80 IP to go → 5 ticks → **0.5 s** from acting (u 0.05, nearly at the left edge). Two heroes pincer it.

```
push      = PincerPushbackSeconds              = 1 s = 10 ticks
IP lost   = 10 ticks × 16 IP/tick              = 160
new IP    = 920 − 160                          = 760   (240 IP to go = 15 ticks = 1.5 s, u 0.15)
```

Net: the pincer bought exactly 1 s, the same at IP 920 or IP 100 and whatever the attackers' STR. A goblin that has just acted (IP 0) can't lose IP, so it stands still for those 10 ticks instead (`StunTicks` 10). A Shield Bash (`Pushback` 0.75) in the same action adds nothing: one push per foe per action.

#### 2.7.1 Hasten / Quicken — the inverse

`BattleRunner.Hasten(target, u)` (Quicken `HastenU` 0.30; the boss `enrage` skill 0.35):
- On a unit with a **turn icon**: clears its stun and adds `round(u × 1000)` IP, clamped to 1000 (so it may fire on the next tick and can overtake icons ahead of it).
- **Amended 2026-10-09:** on a unit with **no turn icon but a cast in flight** (i.e. a casting hero): subtracts `round(u × 1000 / 16)` ticks from the cast (Quicken rushes an ally's spell by ~19 ticks). *(Test: `Quicken_hastens_an_enemy_icon`.)*
- **Amended 2026-11-05 (GG-T268):** on a **hero**, haste also raises the **party tempo** ([§2.11](#211-party-tempo-gg-t268)). Since the held turn (§2.0) heroes have no turn icons, so this is what hero haste does.

### 2.8 Spawn rules

| Trigger | Icon |
|---|---|
| Wave start (incl. battle start) | each enemy's turn icon at a random **IP 0–250** (`SpawnStaggerIp`) — **Amended 2026-10-09** (replaces u=0 + queue delay) |
| Snake segments | no icon (the head owns the chain's turn) |
| Hero spell with cast time | teal cast diamond on the bar at `castSeconds / BarSeconds` (GG-T286/GG-T287) |
| Enemy charge | amber cast diamond on the bar at its charge's `CastTime` (default `DefaultChargeCastSeconds` 2.5 s → u 0.25; not stat-scaled) |
| Pushback / Hasten | existing icon's IP changes (a held cast's remaining ticks) |

### 2.9 Drag timer (optional)

**Amended 2026-10-09 (owner decision):** Settings → "Drag timer (4 s)" (`SettingsSave.DragTimer`, off by default) restores Terra Battle's hand pressure: once a drag starts, `DragTimerSeconds` = 4 s counts down on screen (real time, unscaled by battle speed) and the drag auto-drops at 0. The seconds left at the drop travel in `MoveCommand.TimeLeftSeconds` and feed time-banked orbs ([§3.1.8](#318-time-banked-orbs)). The timer never affects the game clock. **Amended 2026-10-21 (GG-T188):** the held turn ([§2.0](#20-the-held-turn)) now gives every drag a real-time limit of its own; the optional 4 s drag timer is kept as a stricter cap and still feeds the time bank (Guard's banking of unused turn time is GG-T189).

### 2.11 Party tempo (GG-T268)

Owner 2026-11-05: hero haste "slows enemy motion down the timeline, thus you are moving hastily". Several hastes **stack**, and Slow on the party pushes the other way.

- **One signed number.** `BattleState.Tempo` is a list of `TempoEffect`s (source, signed amount, turns left). Their sum is the **net tempo** (`NetTempo`).
- **What it does.** Every turn icon not on the hero team advances at its normal rate × `TempoStep^(−net)` (`TempoStep` 1.25), clamped to **×0.5–×2** (`TempoMin/MaxMultiplier`), so neither side ever freezes or runs away. Net +1 is ×0.8, +2 ×0.64, −1 ×1.25. `BattleEngine.TimelineScale` applies it everywhere an icon's rate is read: the clock, `TicksUntilTurn`, the countdown badges (§2.10), the held-turn window (§2.0) and the icon glide.
- **Sources.**
  - A haste (`HastenU`) landing on a hero (an ability, Fade Step's landing, a pickup) adds `max(1, round(HastenU / 0.15))` points for `AbilityDef.TempoTurns` (default `DefaultTempoTurns`, 2). Quicken 0.30 is +2; Relay, Follow-through and Fade Step are +1.
  - A buff with `BuffDef.Tempo ≠ 0` applied **to a hero** adds that many points for its duration. `slowed` carries −1, so a Slow cast on the party (the Chronomancer's Temporal Drag, §14.10) cancels haste and then speeds the foes up.
  - **Cleansing gives it back** (*added 2026-11-05, GG-T275*): when a tempo buff is removed from the **last** hero carrying it (a cleanse, a cure), its tempo effect lifts with it (`BattleRunner.DropBuffTempo`). Expiry still follows the tempo effect's own turn count.
  - Haste on an enemy still slides its own icon (§2.7.1) and doesn't touch tempo.
- **Per cast.** One cast is one effect, however many allies an area haste touches.
- **Timing.** Effects count down at the party's turn boundary (`ReturnControl`), and skip the boundary of the command that applied them, so "2 turns" means the next two turns.
- **Seen and heard.** `TempoChangedEvent` updates a "Tempo +2" / "Tempo −1" readout above the timeline's left end. The track tints cool (Haste) while foes are slowed and red while the party is, with a rising or falling cue and a feed line giving the multiplier. The InspectCard of a haste ability states the tempo it grants.
- *(Tests: `TempoTests`.)*

### 2.12 Recovery and spacing (GG-T318)

Owner 2026-11-06: "a 'respawn interval' which takes place after the enemy attacks but before they appear back on the
bar, the idea is to distribute enemies so that the player can fight back and have time to push back dangerous enemies
or to cast spells that will provide healing or shielding … there would be instances when enemies would cluster, and in
those cases the player would either try to pull off a really quick move, or sacrifice their move for a defensive bonus
and convert the remaining time into a portion of mana orb." The cluster fallback is GG-T311's forced Guard ([§2.0](#20-the-held-turn)).

- **Recovery.** When a foe's turn resolves (`BattleRunner.ScheduleReentry`), its icon leaves the bar.
  - It sits in recovery for `RecoverBaseSeconds` **1 s**, plus the largest `AbilityDef.RecoverSeconds` keyword it used
    that turn (0–3 s, validated), plus the spacer's delay, ± the difficulty row's jitter.
  - Then it re-enters at its entry ([§2.1](#21-speed-and-pace)).
  - Recovery is **not** scaled by Slow, Haste, Rally or the dial. Frozen holds it. It counts in `TicksUntilAct`, so
    the window, the badge and the bots all see it.
  - Heavy, telegraphed charges recover longer:
    - **1.5 s:** fireball, temporal drag.
    - **1.0 s:** miasma, thunder, ice, enrage.
    - **0.5 s:** smite, rally, mend.
    - A wolf pack's howl recovers 0, so a pack keeps its pressure.
- **The spacer.** `TimelineSpacing.Delay` (pure) takes the foe's natural next turn and the turns of every other foe and
  every enemy charge in flight.
  - It holds the foe up to `MaxSpreadSeconds` longer, so its turn lands at least `MinGapSeconds` from all of them.
  - When nothing in that range fits, it takes the widest gap on offer. A crowded bar still clusters: big waves, eggs,
    remains, and a pushback or Slow that reshuffles the order.
  - The extra time goes into recovery, never into the entry, so the bar always reads Speed honestly.
  - The columns live on the difficulty dial ([§22.9](#229-difficulty-settings-scaffolding-added-2026-10-11-owner-direction)):
    1.6/2.0 s at level 1 down to **1.0/1.0 s** at 10, where a spaced gap always fits one skill. Every row adds ±0.25 s
    of seeded jitter.
  - The neutral row (rules tests) has no spacing and no jitter. Test stages use the neutral row.
- **Spawns.** A wave is scheduled one foe at a time in entry order (`OpenPartyWindow`), with the 3 s opening floor.
  Reinforcements, eggs and risen remains are spaced when they arrive. Tamed allies enter at their entry and are never
  spaced.
- **Pushback and haste.** A push on a recovering foe adds to its recovery (§2.3). A Hasten/Quicken on one eats its
  recovery first, then moves it along the bar.
- **Seen.** A recovering foe waits in the **recovery tray** between the track's right end and GUARD, dimmed, with a
  countdown ring, then slides onto the bar at its entry (GG-T319).
- **Tuning (GG-T321)** — `Tests/Core.Tests/TimelineTuningSoak.cs`, all runs in `docs/research/TimelineTuning.md`.
  - At difficulty 10 with campaign parties:

    | | Smart | Casual |
    |---|---|---|
    | Wins | 100% | 90% |
    | Forced Guard share | 14% | 22% |

    The median gap is 2.1 s and 22% of gaps are under a minimum drag.
  - This fixes GG-T316. Tithe Moors I with the real three heroes was 6%/0% with 35–50% forced Guards; it is now 4/4
    smart, 0/4 casual, with 18–24% forced Guards.
  - At an average 4 s, no timeline setting moves the smart bot below 97%.
  - **Amended 2026-11-07 (GG-T321, decision delegated by the owner): challenge comes from party size.** On this clock
    the starting three manage about 1.3 actions per enemy turn and six heroes about 2.4. So enemy strike and MaxHP
    (and plates) grow by `EnemyScalePerExtraHero` **0.30** for each hero fielded beyond `PartyScaleBaseHeroes` 3
    (`BattleEngine.PartyScale`, `BattleState.PartySize`). Test stages are unscaled.

    | Difficulty, party | Smart | Skilled | Casual |
    |---|---|---|---|
    | 10, campaign | 100% | 100% | 45% |
    | 10, six | 100% | 98% | 38% |
    | 5, campaign | 100% | 100% | 97% |
    | 1, campaign | 100% | 100% | 100% |

    Tithe Moors I is unchanged. The skilled column is `HeroBot.Skilled`: SkillLevel 80, 0.8 s think. A global
    strength raise was rejected because it walled the first battle; see `docs/research/TimelineTuning.md`.
  - **Amended 2026-11-17 (GG-T423): the balance soak itself was fielding the wrong party.** `tools/polish.py`'s
    `run_soak` drove `AbilityUsageSoak`'s per-stage path (the one POL-G1/G2/G6/G7 measure) with a flat five-hero
    rotating party on every stage, never setting `GRIDGAME_ABILITY_PARTY=campaign` — so every soak battle fought
    the enemy scaled for 5 heroes (the +2-over-baseline, 1.6× HP/strike `PartyScale` above) even on early stages
    the campaign only fields 3 heroes at. `run_soak` now sets `GRIDGAME_ABILITY_PARTY=campaign` for both the casual
    and greedy soaks, so these rules measure each stage against the roster size
    `AbilityUsageSoak.CampaignPartySize` says it's actually designed for. `docs/polish/baseline.json`'s
    G-dimension rules (POL-G1..G7) are re-baselined to this corrected measurement (POL-G1 17.4 → 32.5, dimension
    G 66.3 → 69.1, both still owner `ryand`'s decision on GG-T423, not a design change) — every POL-G number
    recorded before 2026-11-17 was measured under the flat-5 bug and is not a valid comparison point going
    forward. The fix didn't move every stage toward the 55–85% band: `greenvalley-01`/`02`, `cave-01`/`03`,
    `desert-01`/`02`, `lanternroad-03` and `swamp-02` still show a 0% casual win rate at the corrected party size
    (`GRIDGAME_ABILITY_PER_STAGE=4`, so n=4/stage). That looks like genuinely under-tuned early-campaign content
    now that the measurement is accurate, not sampling noise alone — flagged for the next GG-T276 balance-loop
    cycle rather than fixed here.
  - **Amended 2026-11-17 (GG-T276): the 8 corrected-measurement 0% stages above, triaged.** Each was content, not a
    repeat of the GG-T423 party-scale mismatch (`cave-01`/`02`/`03` all field the same campaign party size and
    `PartyScale`; `cave-02` is in-band, so the scaling itself isn't the driver). Root cause and fix per stage
    (commits `0202aedb`, `6583bc8f`, `fd602646`, `d1c09b5b`):
    - **`greenvalley-01`** (highest priority — the game's very first stage): the only one of the 44 campaign
      stages whose authored enemy `Level` exceeds `MetaServices.RecommendedLevel` (`campaignIndex + 1`) — every
      other stage authors flat `Level: 1` and lets `BattleRunner.Setup`'s `Math.Max(sp.Level, RecommendedLevel)`
      floor do the leveling. Wave 5's `captain`/`soldier02`/`soldier03` were hand-authored at `Level: 4` against a
      level-1, 3-hero starting roster (waves 3–4 were similarly over-authored at `Level: 2`). Reset every enemy in
      the stage to `Level: 1`.
    - **`cave-01`, `cave-03`, `desert-01`, `desert-02`, `lanternroad-03`:** the same bug class as this session's
      earlier Sentry+Vigilant/Reaver+Vigilant fix (two commits up) — a wave stacking two copies of the same
      self-buffing or `Boss`-tagged unit where no sibling stage in the theme repeats the pattern. `cave-01` wave 5
      fielded four named elites with no filler (dropped `voidsentinel`); `cave-03` wave 4 was the only wave in the
      campaign pairing two `Boss`-tagged units (`cyclops00`+`cyclops01`, swapped the latter for `goblinthug00`);
      `desert-01` wave 4 and `desert-02` wave 3 each doubled up `purpleprototype` (ability `berserk`), a pattern
      `desert-03`/`desert-04`/`desert-05` never repeat (swapped the duplicate for `scorpion`/`soldier00`);
      `lanternroad-03` wave 5 paired its `Undying` rule-break finale boss (`oni02`, GG-T234) with a second
      full-strength `oni00` instead of filler, unlike every other theme's rule-break finale (swapped for
      `steppinrazor00`).
    - **`greenvalley-02`:** no single-bug root cause found; a content-density call instead. Wave 3's four identical
      `toxic_skin` enemies trimmed to three (dropped `frog03`); wave 4's `harpy03` (`jammer_darts`, which targets
      the `Caster` role) removed, since this is the exact stage the campaign's first mage joiner
      (`greenvalley_recruit_mage`) is newly in the roster for, at her lowest level.
    - **`swamp-02`:** `naga00` (this stage's solo finale, and the only `LaysEgg` enemy in the real campaign —
      Brain `Ambusher`, GG-T262's `AmbushPatienceTurns`, plus a never-reused `nagaegg` reinforcement every
      `EggInterval` turns) is a one-off combo of mechanics with nothing to dilute it; POL-G6's own per-wave action
      counts for this stage swing from 4 to 67, consistent with runs stalling on an egg-sustain spiral rather than
      a clean win or loss. Doubled `EggInterval` 4 → 8 (content-only; `naga00` isn't used in any other real stage).
    - Build (`tools/build.ps1 -SkipProse`) is green after every commit: errors=0, warnings=1 (unchanged baseline
      warning). **Balance-soak re-verification is still outstanding**: the harness's background-task
      memory-pressure safeguard killed the post-fix `python tools/polish.py --no-player --balance` run on this
      shared machine before it produced results. Before numbers (n=4/stage, all eight at 0/4) are GG-T423's
      measurement above; after numbers are pending the next run and will be appended here once captured.
  - **Amended 2026-11-18 (GG-T276): the 6 stages still at 0/4 after the above, triaged deeper — sibling-compared,
    not just the duplicate/overtuned-element pattern.** `MINDATTIC_AGENT.md`'s GOLDEN RULE ("verification is
    build-only; tests run last, once, when the user asks... orchestrators and coordinators never instruct
    subagents to run suites or soaks") means this pass did not re-run `polish.py --balance` itself — the
    `--per-stage 12` re-measurement this cycle's brief asked for was not taken, and the after-numbers below are
    therefore from static/structural analysis (sibling wave-count, enemy-count, Boss/RuleBreak placement,
    ability-text and `HeroBot.cs` comparison), not a fresh soak. The next `/test` or explicitly-requested balance
    pass should capture real after-numbers against the before column here.
    - **Content fixes applied (4 of 6):**
      - **`greenvalley-01` wave 4** was the single largest wave among all 15 sampled early-campaign stages (5 foes,
        vs. a sibling max of 4: `greenvalley-03` wave 3/5, `desert-03` wave 4) and carried a redundant second
        `goblinthug00` alongside the `ganger00`/`ganger01`/delayed-`soldier00` mix the rest of the wave already
        covers. Dropped the duplicate `goblinthug00`: wave 4 is now 4 foes, matching the sibling ceiling.
      - **`greenvalley-02` wave 1** opened with 3 slimes (`slime00`/`01`/`02`) where both its siblings
        (`greenvalley-01`, `greenvalley-03`) open with exactly 2 — and this is the only one of the three
        greenvalley stages that declares the `slimetrail` region rule, whose `slime02`/`slime03`-style
        `TrailRule` (`ClassDef.TrailRule`, `BattleRunner.Region.cs RegionTrail`) paints a persistent `slowed`
        hazard tile behind every slime step. A third slime is a third hazard-painting source, right at the
        stage's own opening wave, before the party (fresh off stage 1, one shop visit in) has any means to
        cleanse or outrun stacking `slowed`. Dropped `slime02`: wave 1 is back to 2 slimes
        (`slime00`+`slime01`, the same pair `greenvalley-01` wave 1 uses).
      - **`desert-01` wave 3** fielded `infernalist03` (`Brain: Medic`, `charge_blood_rite` — heals the most
        wounded ally 25 HP every other turn, plus its own `lifesteal`) alongside `infernalist02`. Checked
        against every other theme's own opening stage (`greenvalley-01`, `lanternroad-01`, `swamp-01`, `cave-01`):
        none of them field a dedicated `Medic`-role healer this early — the campaign's other `Medic` units
        (`swampmistress00`'s 30 HP `charge_mend`, stronger and cooldown-free) only appear at a theme's *finale*
        (`swamp-03`), after two stages of leveling. `desert-01` is `desert`'s own opening stage, so it was
        introducing a healer-sustain mechanic a full stage-position earlier than every sibling theme does.
        Swapped `infernalist03` → `infernalist01` (`Hunter`, `cinder_claws`+`infernal_hide`, no heal) — same
        family, same wave size, no sustain spike.
      - **`swamp-02`'s `naga00`**: re-reading `BattleRunner.Enemy.cs`'s `LayEgg`/`Hatch` past the already-applied
        `EggInterval` 4→8 fix shows the mechanic is structurally unstable, not just too frequent — `Hatch` spawns
        the egg into a full clone of whatever laid it (`egg.RisesAs = layer.Def.Id`, `egg.RisesLevel =
        layer.Level`), and that clone inherits the layer's own `LaysEgg`/`EggInterval` (it's spawned from the
        same `ClassDef`). A `nagaegg` that survives to its own turn doesn't add a minor add — it can add a
        second full-strength Boss that can itself lay more eggs: an uncapped self-cloning spiral, consistent
        with POL-G6's 4-to-67-action swing on this stage. `naga00` is confirmed the only `LaysEgg` user in the
        real campaign (`grep`'d every content file), so this is fully isolated. Removed `LaysEgg`/`EggInterval`
        from `naga00` entirely (content-only; `nagaegg` stays defined but unused) — `swamp-02`'s finale is now a
        straightforward solo Boss fight (poison DoT, `Ambusher`/`AmbushPatienceTurns` intact) without the
        recursive-reinforcement risk.
    - **Flagged as a bot-AI gap, not re-tuned (2 of 6):** `lanternroad-03` and `cave-03` are the only two of the
      five sampled themes' own finale (`-03`) stages still failing, and both pair their finale with a `RuleBreak`
      whose counterplay needs *deliberate* avoidance the bot doesn't do: `lanternroad-03`'s `Undying` (`oni02`,
      GG-T234) only refuses to fall while another foe stands — "slay its thralls first" — but `HeroBot.cs` has no
      targeting logic that deprioritizes an `Undying` foe while thralls live; `cave-03`'s `GravityStamp`
      (`cyclops02`) drags every hero within reach toward the boss every other of its turns — "keep your
      distance" — but `HeroBot.cs` has no retreat/spacing logic against it. By contrast, the *other* three
      themes' finale `RuleBreak`s all succeed without any stage changes this pass: `greenvalley-03`'s `TwinTurn`
      ("two turns unless a hero stands beside it") is satisfied by the bot's ordinary melee-approach behavior for
      free; `swamp-03`'s `MireSwap` is a low-cost reposition, not a punishing one; `desert-03`'s `AxisWard`
      already has bot-side awareness (`HeroBot.cs`'s `RuleBreakRules.WardsPincer` scoring, favoring column
      pincers). `Undying` and `GravityStamp` are the only two rule-breaks with neither free coverage nor an
      explicit bot special case — a `HeroBot` enhancement (thrall-priority targeting; stamp-range retreat) is the
      right fix, not a further content nerf on two stages whose finale bosses are each already the theme's one
      intended rule-break set-piece. No stage content changed for either.
    - **Build check blocked, not yet green, not yet committed**: `tools/build.ps1`'s Unity batchmode run was
      killed mid-flight by this shared machine's background-task memory-pressure safeguard (the same blocker
      GG-T423's note above hit for `polish.py --balance`), so this pass has *not* confirmed a clean build for
      these 4 content edits. Per `mindattic-agent-standard/AGENTS.md` Law 7 ("commit at verified milestones
      only") the edits below are staged on disk but intentionally **not committed**; per the harness's own
      guidance the killed build is not retried automatically on this pass. The next session with headroom should
      re-run `tools/build.ps1`, confirm errors=0, and commit each fix by its own path before relying on it.
      Before numbers (n=4/stage, all six at 0/4) are the prior GG-T276 pass's measurement above. After numbers:
      **not measured this pass** (GOLDEN RULE — no soak/balance run without an explicit user `/test` ask, and
      orchestrators/subagents never instruct each other to run one); the 4 content-fixed stages
      (`greenvalley-01`, `greenvalley-02`, `desert-01`, `swamp-02`) are expected to move toward the 55–85% band
      on the next real soak, and `lanternroad-03`/`cave-03` are expected to stay near 0% until `HeroBot` gets
      `Undying`/`GravityStamp`-aware targeting — see GG-T276's ticket notes for the full write-up and the
      follow-up ask.
  - **Amended 2026-11-19 (GG-T441): the GG-T276 trims kept, the invariants they broke restored.**
    - **`greenvalley-02` wave 3:** `6583bc8f`'s trim (`frog00`/`frog01`/`frog03`/`toad00` → dropped `frog03`) left
      `frog00`/`frog01`/`toad00`, the exact foes, levels and tiles of `swamp-01` wave 1, which `ContentLoader.Validate`
      refuses (no two campaign waves are the same encounter). Kept the 3-foe trim but dropped `toad00` instead of
      `frog03`: now `frog00`/`frog01`/`frog03`. All four are the same 12 VIT / 15 STR `toxic_skin` statline, so the
      wave's weight is unchanged; `toad00` still fights in `greenvalley-03` wave 5.
  - **Amended 2026-11-19 (GG-T442): placement and shape invariants the GG-T276 edits broke, restored without
    re-densifying a wave or re-stacking an elite.**
    - **`greenvalley-01`** (§14 "five waves of rising difficulty"): with every foe at `Level: 1` (`0202aedb`) the
      captain's finale (77) weighed less than wave 4 (101) and the frogs (81) less than wave 2 (91). Moved `wolf01`
      from wave 2 to the finale: wave 2 is `wolf00`+`bat00` (59), the finale `captain`/`soldier02`/`soldier03`/
      `wolf01` (109) — weights 54 < 59 < 81 < 101 < 109, no foe added, no level raised, the finale at the 4-foe
      sibling ceiling `cf707043` set, and the tutorial's wave-5 "pincer the wolf while it howls" has its wolf back.
      (`wolf00`+`wolf01` would have repeated `lanternroad-01` wave 1.)
    - **`greenvalley-02`/`greenvalley-03` wave 1** (GG-T190 shove lesson): the opening wave must hold one foe at
      `(2,5)`, above the gap between the lead hero `(1,6)` and the third `(2,7)`. `d83e1d9a` moved every slime out
      of the hero band; this puts back *one* — the lesson foe, a speed-8 slime the party reaches first — and keeps
      the rest out (`greenvalley-02`: `slime02@(2,5)` + `slime01@(3,1)`, was two slimes on row 5 before `d83e1d9a`;
      `greenvalley-03`: `slime02` back to `(2,5)`, `slime03` stays at `(4,3)`). `greenvalley-02` keeps
      `cf707043`'s two-slime opener, and its slime is now `slime02`, the `slimetrail` carrier the stage's region
      rule is pinned to. Watch `greenvalley-03`'s win rate on the next real soak: its 25% → 75% came from this move.
    - **`cave-03` wave 3:** `wraithknight02` (Reaver) → `wraithknight03` (Vigilant), so the Vigilant still guards the
      Anchor Wastes, as the only Watch elite in a wave of plain filler (`skelepede00`, `goblinthug00`) — the
      un-stacking `d83e1d9a` did is kept; the Reaver still fights in `cave-01` wave 2.
    - **`cave-01` wave 3:** `ceramicknight00` → `voidsentinel`: the same plated 18-VIT construct tier, in a 2-foe
      wave with filler, not back in the four-elite finale `fd602646` thinned.
    - **`desert-03` wave 2:** `techgremlin00` → `infernalist03`: the Shardfall's Medic now first appears in the
      theme's last stage (as `cf707043` says sibling themes do), not in `desert-01`, and not beside `desert-03`'s
      `AxisWard` finale boss; `techgremlin00` still fights in wave 4 and `desert-01`.
    - **`cityruins-04` wave 2:** dropped `mockingecho` (GG-T417 had added it; the Underspire fields only Street/Hollow/
      Choir foes, and a postgame non-finale wave holds at most 2): now `cyberzombie00`+`pitlurker`. The Mocking Echo still
      haunts Halcyon I and II.
    - **Speed tiers (8/11/15/18):** the postgame region monsters were authored off-tier; each snapped to the nearest
      tier its card reads as — 8: `holdwright`, `chainwarden` (9), `holdram` (6), `spireshover` (7), `crystalbrute`,
      `anchorhulk`, `bloatdrowner` (5); 11: `holdmarksman`, `shardcaller`, `stormcaller`, `gloommender` (10),
      `pitlurker`, `mockingecho` (12); 15: `duskmimic` (13, a flanker).
  - **Amended 2026-11-22 (GG-T276/GG-T215): the frog family poisons less often.** A full `polish.py --no-player
    --balance` pass scored G 64.0 (POL-G1 24.4, G6 32.8, G8 50.0): the early campaign sits under the 55–85% casual
    band again (`greenvalley-01` 0/4, `swamp-01` 0/4, `desert-02` 0/4), while the Still/Cathedral sit at 100%. A kept-records
    re-run of the four worst stages at 8 battles each (the same seeds both sides) showed real defeats, not stalls: the
    starting trio (Warden, Barbarian, Cleric) won `greenvalley-01` 1/8, mostly falling to the captain's finale after the
    frog wave had worn them down. Frogs poison through three channels at once — their own blows (`OnHitChance` 0.4),
    `toxic_skin` whenever struck directly (0.35), and their pincer support — so an all-melee, unarmed early party, whose
    every pincer on a frog is a direct strike, stacks poison fast. **Change:** `toxic_skin`'s trigger 0.35 → 0.25
    (description `[fair]` → `[small]`, §9 bands; it also arms `toad00` and `swampmistress00`), and `frog00`–`frog03`
    `OnHitChance` 0.4 → 0.3. Supports, the `poisoned` buff itself and every wave are unchanged; §14's five-rising-waves
    weights for `greenvalley-01` (54 < 59 < 81 < 101 < 109) leave no slack to thin a wave, so the lever is the family's kit.
    **After (same seeds):** `greenvalley-01` 1/8 → 2/8, `greenvalley-02` 3/8 → 5/8, `swamp-01` 1/8 → 1/8, `desert-02`
    4/8 → 4/8. `swamp-01`'s five wave-1 wipes are not poison: the records attribute ~2,400 damage each to `frog00`,
    `frog01` and `toad00` blows at that stage's level (84 HP frogs) against the soak's unarmed heroes — the next pass's
    target, along with whether the casual soak should field the guided starter gear a real save forges.
    GG-T215's other remaining items, checked: mercenaries (45–70 G a battle) are ~10% of a mid-campaign stage's pay
    (≈400–700 G), no change; the shop potions' flat-to-worse value per HP at the top (X-Potion 500 G / 500 HP) comes from
    the owner's own "buying stays the convenient one" price raise (AMENDMENTS, X-Potion 250 → 500) and stays for the owner's
    feel (RELEASE_SCOPE); the XP curve is GG-T474's owner decision.
  - **Amended 2026-11-22 (GG-T276): the casual soak fights from the campaign's save.** A real new save is unarmed at
    `greenvalley-01` too (two starter abilities, 200 G, no Forge or Merchant yet), but from the next stage on the soak
    fielded bare heroes no player could still have. `AbilityUsageSoak` (cc2eb8ea) can copy a camp-managing greedy
    campaign's save before each stage (heroes keep its gear, abilities, talents and level; joiners come in fresh), and
    `polish.py` now turns it on by default for the casual per-stage soak (`GRIDGAME_ABILITY_GEAR=bare` restores the old
    way). **Measured (8 a stage):** `greenvalley-01` 2/8 (unchanged, as it should be: bare there too), `greenvalley-02`
    in band, `swamp-01` 1/8 → 3/8, `desert-02` 4/8 → 7/8. **Tried and reverted:** the captain's `ArmorPlate` 24 → 18
    and Rally's radius 2 → 1 (only the captain carries Rally; neither number counts toward §14's wave weights). Kept
    records showed `greenvalley-01`'s defeats land in wave 5 with all four of the captain's people at full health and
    the trio's X-Potions spent, but the change left it at 2/8 and pushed `desert-03` to 8/8, so it went back.
    `greenvalley-01` is attrition across all five waves against a level-1 trio, not the captain alone. **Noise:** the
    snapshot campaign is replayed every run, so `swamp-01` (no captain) read 3/8 then 8/8 across two runs with no
    relevant change; 8 battles a stage can't separate a tune from that, so the next pass should use a fixed snapshot
    seed or more battles before tuning `greenvalley-01`.
  - **Amended 2026-11-22 (GG-T276 pass 3): the soak replays exactly, its snapshots are frozen, and Tithe Moors I's
    reinforcements arrive in turn.** *Noise, found:* the snapshot campaign's forging rolled each Masterwork off the clock
    (`Crafting.Craft` falls back to a `TickCount` seed when no `Rng` is passed, and `CampRoutine` passed none), so the
    same content gave different gear every run. `CampRoutine.Run` now takes an optional `Rng` and the soak seeds one per
    stage and try (23234ca3); two runs of the same content then gave 96/96 identical battles. *Cascade, found:* even
    replayed exactly, regenerating the snapshot each run made any upstream tune reshuffle every later stage's loot and
    forging (Tithe Moors I's change below took `desert-02` from 23/24 to 11/24, its Barbarian arriving with a different
    weapon). The per-stage saves are now frozen to `artifacts/balance-snapshots/` (9ae4b102), reused until
    `GRIDGAME_ABILITY_SNAPSHOT=regen`; regenerate after a tune lands to see its knock-on once. **Change:** in
    `greenvalley-01` the third frog (`frog02`) and two of the captain's soldiers (`soldier02`, `soldier03`) arrive 240,
    240 and 480 ticks into their waves (`SpawnDef.DelayTicks`, already how wave 4's `soldier00` arrives), so the trio
    meets each wave in parts. No foe, level or stat changed, so §14's rising weights (54 < 59 < 81 < 101 < 109) stand.
    **Measured (24 a stage, casual, exact replay):** `greenvalley-01` 6/24 (25%) → 14/24 (58%), with wave-5 defeats
    11 → 4. The first stage doesn't depend on the snapshot (a new save is unarmed), so that change is clean. Later
    stages were measured only with the snapshot regenerated, so their moves are the cascade above, not this tune:
    `greenvalley-02` 20 → 22/24, `swamp-01` 13 → 17/24, `desert-02` 23 → 11/24. A run against the frozen snapshot, to
    see them with the party held fixed, was cut short by the machine's low-memory guard. 58% is inside the 55–85% band
    but at its bottom; a first stage should sit near the top, so the next pass should take `greenvalley-01` further and
    run the regenerated snapshot once to check `desert-02`.
  - **Amended 2026-11-22 (GG-T276 pass 4): the Mecha Armor family's plate 40 → 30.** Against the frozen snapshots
    (24 casual a stage), `desert-02` won 11/24 (46%, under the band); kept records put 11 of its 13 defeats in wave 4
    with `mechaarmor02` still standing (wave 4 averaged 81 actions). The Mecha Armors were the most stacked foes in the
    game: the thickest plate (40, against 30 on the other `tough_hide` carriers, `templar*`/`ceramicknight*`), `tough_hide`,
    `mirror_plating` and a Kiter brain that keeps their front toward an all-melee party. **Change:** `mechaarmor00`–`02`
    `ArmorPlate` 40 → 30, in line with those siblings (plate doesn't count toward §14's wave weights). **Measured:**
    `desert-02` 11/24 → 14/24 (58%, in band); `greenvalley-01` 14/24, `greenvalley-02` 22/24 and `swamp-01` 17/24 unchanged.
    **Tried and reverted:** `mirror_plating`'s chance 0.5 → 0.35 (no change: 14/24) and a fourth foe (`wolf01`) in
    `greenvalley-02`'s finale (still 22/24, party HP left on wins 67% → 64%). Dropping `tough_hide` from the family was
    built but its run was cut by the low-memory guard, so it isn't in. **Why `greenvalley-02` stays at 92%:** the snapshot
    party arrives at level 4 against a recommended level of 2 (`greenvalley-01`'s five waves out-level the curve), and the
    campaign convention (0202aedb) authors every foe at `Level: 1` under the `RecommendedLevel` floor, so hand-leveling its
    foes would bring back the very bug that broke `greenvalley-01`. The lever is the XP curve, GG-T474 (owner decision).
    Still to do: a `GRIDGAME_ABILITY_SNAPSHOT=regen` run over `greenvalley-01`..`desert-02` to check knock-on effects (not
    run: memory), and a second look at `desert-02`'s wave 4 if it lands under the band once regenerated.
  - **Amended 2026-11-22 (GG-T276 pass 6): first measurement with owned-orb mana; no tune.** Mana is now owned Mana Orbs
    (GG-T673 spells cost 1–4 orbs; GG-T677 a new save owns 1, milestones/finds raise it; GG-T676 leftovers → Mana Dust).
    The soak already fights with the orbs a player would own: every fight is built by `BattleFactory.Build`, whose
    `OrbCapacity` reads the save's owned orbs, the campaign snapshot's `Commit` grants milestone/find/broken orbs, and
    `greenvalley-01` starts from a new save (1 orb). Snapshots regenerated (`GRIDGAME_ABILITY_SNAPSHOT=regen`, kept in
    `artifacts/balance-snapshots/d10`), 24 casual a stage, run `2026-11-22_1658`:

    | Stage | Wins | Orbs minted / battle | Wasted (row full) | Casts / battle |
    |---|---|---|---|---|
    | greenvalley-01 | 13/24 (54%) | 5.7 | 11% | 4.2 |
    | greenvalley-02 | 22/24 (92%) | 4.7 | 8% | 3.4 |
    | swamp-01 | 23/24 (96%) | 11.3 | 1% | 10.7 |
    | desert-02 | 19/24 (79%) | 15.6 | 1% | 12.2 |
    | cave-02 | 9/24 (38%) | 13.0 | 1% | 12.0 |
    | cityruins-02 | 24/24 | 17.1 | 0% | 16.9 |
    | cathedral-02 | 24/24 | 13.8 | 0% | 14.2 |

    **Read:** with one orb, early casting is scarce but used (4 casts a fight at Tithe Moors I; the 11% waste is earning
    into a full one-orb row), so the earn rates don't look wrong. Casual campaign Mana Dust: 111, 24 of it from leftover
    orbs (~22%); the gold split holds (gear 43%, repairs 22%, facilities 9%). **Why no tune:** nothing clearly traces to
    the orb economy. `cave-02`'s defeats spread over waves 2–5 (attrition, not a spike) with healthy casting;
    `cityruins-02`/`cathedral-02` at 24/24 and `swamp-01`/`greenvalley-02` above the band match the campaign party
    arriving over-levelled (GG-T474, the owner's XP-curve call), and cheaper big spells (GG-T673: 10 → 4 orbs) may add to
    it. **Next pass:** a counterfactual (the same seeds with spell orb costs at their old scale, or orbs off) to split the
    late 100% between levels and cheap spells before tuning either; `cave-02` triage; and the casual camp-managing
    campaigns finished 2/4 this run (earlier runs 4/4), which points at `cave-02` as a new wall.
- *(Tests: `TimelineSpacingTests`.)*

### 2.10 Countdown badge

**Amended 2026-10-09 (new, Terra Battle–style):** every enemy shows a number on its tile — `BattleEngine.ActionsUntilTurn` = `(TicksUntilTurn − 1) / 20`: the actions you get before it acts (**0** = it acts during your next action, drawn red). **Amended 2026-10-21 (GG-T188):** a turn now ends at each enemy arrival, so the number counts the distinct arrival ticks of the *other* enemies (each re-loading from IP 0) before this one's — the turns you get before it acts (**0** = it acts at the end of your next turn). Since GG-T308 a "turn" is the party's window (every hero acts once in it), so the number counts windows, not hero actions. **GG-T318:** each other foe's next lap is estimated as base recovery plus the trip from its entry, not a reload from IP 0. `TicksUntilTurn` simulates its icon with stun at its one linear rate (no Zone crawl since GG-T286) but no future pushback/hasten; Frozen enemies show no number. The actor panel says "Acts in N moves", "Acts after your next move!", "Frozen in time" or "Charging X!".

### 2.13 Turn modes (GG-T762)

**Added 2026-11-23 (owner, GG-T762/GG-T765). ⬜ Not yet built: this section is the skeleton the turn-mode epic fills in.** Owner: "I want to be able to try the 'any hero every turn', 'any hero who hasn't moved this round', and the 'Heroes on the timeline' modes; I want all of them created and implemented separately and sequentially and tested headlessly and figure out which works better for what and why … I can see all of them being viable game modes so each one needs to be built in carefully and organically to the entire game."

Who on the party may act, and when, is a **turn mode**. There are four. **PartyWindow** is today's rule and stays the default; the other three are separate, complete game modes, not debug switches. The enemy side, the bar, pincers, mana and content stay the same in every mode. Each mode changes only what [§2.0](#20-the-held-turn) decides for the heroes, and then whatever has to follow from that.

- **The setting** (⬜, GG-T767/GG-T773/GG-T774/GG-T775). `TurnMode` (`Core/Battle/TurnMode.cs`): `PartyWindow` (default), `AnyHeroEveryTurn`, `UnmovedThisRound`, `HeroTimeline`. `BattleSetup.TurnMode` is copied to `BattleState.TurnMode`, kept by the state clone (like Difficulty) and recorded by `BattleRecorder`/`CommandCodec`, so a replay reproduces its mode.
  - The player picks it as a profile setting, `turnMode` in `settings.json` (`GameSettings`, default `PartyWindow`), in Settings. New Game can **lock** it on the save (`SaveData`; a `SaveMigrations` bump loads old saves as `PartyWindow`).
  - **The pickers** (✅ GG-T774). Settings → **Turn mode** cycles the profile's mode like Battle speed, with a one-line explanation under it (and a note when the open save is locked). New Game's **Turn mode** row picks *Settings* (the save follows the profile) or locks the save to one mode (`TurnModeSetting.Lock`). Both offer only `TurnModes.Available` (`Core/Battle/TurnModes.cs`; `IsBuilt` is the one check): today just PartyWindow, and each mode's feature appends itself when its rules land. A mode's name and explanation are its glossary entry `glossary:turn_mode_<mode>` (`glossary.json`, words seeded in `Content/Text` through the Prose tools).
  - Tools pick it on the command line (GG-T775), through the harness, or with `GRIDGAME_TURN_MODE` for the headless soaks.
- **The resolver** (✅ GG-T773). One function, `TurnModeSetting.Resolve` (`Meta/SaveData.cs`), picks a battle's mode: the save's lock (`SaveData.TurnModeLock`, set by `TurnModeSetting.Lock`; save v15) if it has one, else the profile setting (`SettingsSave.TurnMode`). `BattleFactory.Build` takes the profile's mode and resolves it, so every battle built from a save gets it; `EndlessEncounter.Build` takes the mode its caller resolved. It feeds `BattleSetup.TurnMode` at **every** battle start: campaign and stage battles, endless, the Crucible, wanderers, bounties, test mode, the tutorial and `BattleScreen`. No battle may pick its mode any other way.
- **The `TurnRules` seam** (⬜, GG-T768/GG-T769). `Core/Battle/TurnRules.cs` is the **one** place a mode answers five questions:
  1. who may act now;
  2. whether an action ends the party's turn;
  3. what an action spends;
  4. what Guard gives up and banks;
  5. what a turn boundary resets.

  `BattleEngine` (`CanMove`, `HeroesLeftToAct`, `TooLittleTime`, `CommandTicks`, `TurnTicks`), `BattleRunner` (window end, `ReturnControl`, `PlayerControlEvent.NewWindow`, Guard), the `ActedThisWindow` reads (Tame, Wanderer, Recruitment), `HeroBot` and the battle view all ask the seam. None of them switches on `TurnMode` itself. A mode that isn't built yet throws `NotSupportedException`.
- **Comparison.** Every mode is soaked headlessly on equal terms and compared in [`TURN_MODES.md`](TURN_MODES.md). Numbers that only make sense in one mode live in per-mode `BattleConfig` constants. Content and general balance stay with the continuous balance loop (GG-T276).

#### 2.13.0 PartyWindow (today, the default)

The party's turn is the **window** until the next enemy icon reaches the left edge, and **every able hero acts once** in it. This is exactly the GG-T308/GG-T311 rule of [§2.0](#20-the-held-turn): `ActedThisWindow`, the shared window budget, minimum action time, forced Guard, Rally, and turn boundaries counted in windows. Through the seam it must **replay identically** (`Party_window_replays_identically_through_the_seam`). It is the comparison baseline.

#### What each new mode must answer

Each mode below records its final rule in its own subsection when its core-rules feature lands, and answers every one of these. "Same as PartyWindow" is a valid answer, but it has to be written down.

| Topic | The question |
|---|---|
| **Who may act** | Which heroes can be picked up or use their bar now; what greys out and why (`SlotStatus`); what ends the party's turn. |
| **Guard** | What Guard gives up, what it banks as mana charge ([§2.5](#25-auto-skip--shield)), and whether forced Guard and Rally still apply ([§2.0](#20-the-held-turn)). |
| **Mana income** | Whether orbs and charge per enemy turn stay in the band when the party takes fewer or more actions per enemy turn ([§3.1](#31-mana-the-orb-economy)). |
| **Turns buffs** | What "1 Turn" means for hero buffs, Protection, tempo effects, Frozen on foes and Regenerate ([§8.3](#83-ticking), [§2.11](#211-party-tempo-gg-t268)). |
| **Cooldowns** | What a skill cooldown counts: party turns, that hero's own actions, or rounds ([§4.1.1](#411-skill-cooldowns)). |
| **Status ticks** | When DOTs, Stun, Asleep and the TurnStart/TurnEnd/ActionEnd triggers fire. |
| **Pincers** | Whether one action still sets up a pincer that a second hero closes in the same turn; supporters and chains ([§1.2](#12-pincer-attacks)). |
| **Region rules** | Every region rule that counts turns or actions ([§14.20](#1420-region-rules-added-2026-11-08-owner-direction-gg-t359)). |
| **Tutorial** | Whether the tutorial's scripted steps and copy still read correctly, or need a per-mode variant. |
| **Post-battle stats** | What results, records and achievements count (turns, actions, "no hero acted twice"…). |
| **Crucible** | Leaderboards and seeds: per mode, or the mode locked ([§22.10](#2210-the-crucible-added-2026-10-19-owner-direction-gg-t95)). |
| **Automation** | What `/state` reports (who may act, round, hero icons) and what the harness drives. |
| **UI** | Banners, the ✓ acted marks, the hold lane, the countdown badge ([§2.10](#210-countdown-badge)), and the settings/New Game copy. |
| **Soak** | The bots (`HeroBot`, `CampRoutine`) play it well. The soak reports it on equal terms with the baseline ([`TURN_MODES.md`](TURN_MODES.md)). |

#### 2.13.1 Any hero every turn (`AnyHeroEveryTurn`) — 🟡 Core rules built (GG-T781), the rest of GG-T779 to come

Each party turn **exactly one** hero acts: any able hero, the same one again if you like (Terra Battle style, close to the pre-GG-T308 rule). That action ends the party's turn. Open questions: every topic in the table, and especially mana income and cooldowns, since there is one action per enemy turn instead of several.

**Core rules (✅ GG-T781, `TurnRules`; not yet offered in the pickers, which wait for the rest of GG-T779):**
- **Who may act.** At the start of each party turn every living, commanded hero who isn't Immobile may act, the hero who acted last turn included. The first hero action, or Guard, ends the turn. Until the turn boundary, every hero reads as acted: `HasActed` is true for all of them, `SlotStatus.Acted` greys them, `HeroesLeftToAct` is empty, and a second command is refused with "Already acted". The mark is PartyWindow's own `Unit.ActedThisWindow` on the hero who acted, cleared by `ResetTurn` at the boundary. No new state is added, so the state clone and replays carry it unchanged.
- **What an action spends.** The same as PartyWindow: a drag spends its share of the gap and a skill, item, spell or Talk spends `HeroActionSeconds`. `EndsPartyTurn` is then always true, so the clock runs on to the next enemy arrival (`SpendWindow`), as it does after PartyWindow's last hero.
- **Leftover seconds are lost, not banked.** Decision: the seconds a hero action leaves in the gap earn no mana charge. That is the same as the last hero's leftover in PartyWindow. Only Guard turns unused time into charge, through the same `GuardCharge`/`GuardChargeFor` rule as PartyWindow, so Guard stays a real choice and not a free side effect of acting fast. GG-T782 decided the rest of mana income (below).
- **Unchanged from PartyWindow.** The minimum-action gap (a command too long for the gap is refused with "Too little time"), forced Guard and Rally (`TooLittleTime`, `IsForcedGuard`), each wave's opening window, the turn boundary (an enemy arrival or a cleared wave, where the combo closes) and `ClearActed` for joining units.
**Per-turn systems (✅ GG-T782).** One action is now a whole party turn, but a party turn still spans one enemy arrival (or a cleared wave), the same stretch of enemy time as a PartyWindow window. So everything that counts turns keeps counting **party turns**, never hero actions, and no duration is scaled. Mana is earned and spent per action, so only income measured per turn needs a new answer: the time bank.
- **Guard (§2.5).** Unchanged. Protection (1 Turn) covers the enemy turn Guard skips and expires at the next boundary. The bank is the same `GuardCharge` on the whole window, capped at `GuardMaxCharge`. Guard gives up only one hero's action here, but the 40 cap is under half an orb, so it still costs a real action.
- **Mana income (§3.1).** Pincer charge (§3.1.9) is unchanged: charge per pincer and spells are both per action, so the per-action balance is PartyWindow's. Pincer orbs per enemy turn fall, and so does spending, since only one spell fits in a turn. Regen follows the clock and is unchanged. **Time bank (§3.1.8): capped at `AnyHeroMaxTimeBankOrbs` = 1** through `TurnRules.MaxTimeBankOrbs`. Every drop here is the turn's only action and an enemy turn always follows it, so PartyWindow's cap of 3 would pay out on every action, not once per window shared by the party. One orb is about PartyWindow's per-action share. The soak (`tools/turn_modes.py`) checks the band.
- **Turns buffs, Protection, tempo (§8.3, §2.11).** They keep counting party turns at `ReturnControl`. "2 Turns" is the next two party turns, whichever hero acts in them. A bystander's buff ticks too.
- **Cooldowns (§4.1.1).** They keep counting party turns for every hero, whether it acted or not. A hero who acts every turn uses a skill at the same cadence as in PartyWindow.
- **Status ticks.** DOTs follow the clock. Frozen on foes, Sleep and Stun count party turns. TurnStart/TurnEnd fire at each boundary, and ActionEnd fires after each action. All unchanged.
- **Rally.** Unchanged: a forced Guard stacks it and any hero action resets it.
- **The support extra turn (`supportTurnOwner`, GG-T307).** Unchanged. It is enemy-side: a buff gained during an enemy's own turn gets one more turn.
- Tests: `TurnModeAnyHeroTests`. They cover the same hero twice in a row, a second action in one turn being refused, Guard banking, forced Guard and Rally, a wave's fresh turn, and a bot battle that replays identically. GG-T782 adds tests for the time-bank cap, cooldowns, Turns buffs and tempo, Guard's Protection and a Frozen foe.

#### 2.13.2 Any hero who hasn't moved this round (`UnmovedThisRound`) — ⬜ Not yet built (GG-T792)

One hero acts per party turn, but **only a hero who hasn't acted this round**. When every able hero has acted, the round resets. Open questions: every topic in the table. On top of those: what counts as a round when a hero falls, joins or is revived mid-round; whether Guard uses up a hero's place in the round; and how the round shows on the HUD.

#### 2.13.3 Heroes on the timeline (`HeroTimeline`) — ⬜ Not yet built (GG-T805)

Every hero has **its own turn icon** on the shared bar with the foes (Grandia style; this lifts "heroes have no icons here", [§2](#2-the-timeline)). When a hero's icon reaches the left edge, that hero acts. Open questions: every topic in the table. On top of those: how a hero's Speed sets its entry and recovery ([§2.1](#21-speed-and-pace), [§2.12](#212-recovery-and-spacing-gg-t318)); what pushback, Hasten, Slow and party tempo do to hero icons; whether the clock runs or stops while a hero is held; and how the ACT-zone question in [§2](#2-the-timeline) changes once heroes have icons.

---

## 3. Resources

### 3.1 Mana (the orb economy)

**Amended 2026-10-10 (owner decision): mana is generic.** Every orb is identical and any spell can be paid from any orbs. `ManaBank` is a capped count (capacity **12**) shared by the party ([GG-LAW-6](#GG-LAW-6)). The HUD draws 12 orb slots; filled slots glow mana-blue, empty slots are dim.

#### 3.1.1 Costs

A spell's cost is a whole number of orbs (`AbilityDef.Cost`, an int in `abilities.json`); the UI renders it as that many orb pips (`Ui.CostPips`; above 3, one orb and "×N", GG-T372). Skills and items cost 0. ~~Content costs are 1–3 (the former letter counts: Fireball 2, Bolt 3, Heal 1, …).~~

**Amended 2026-11-08 (GG-T373, owner: "with 12 mana slots that means some of these spells need to get much more expensive, need to consider balance"): costs run 1–12 on a curve.** `ContentLoader.Validate` refuses a cost outside 0..`ManaBank.DefaultCapacity` (12), and any cost on a non-Spell. Bank gear (up to +4, §3.1.4) only adds headroom; no spell needs it. **The balance is unmeasured, pending the owner's `/test` soaks.**

- **What a party earns.** Measured (GG-T265, 206 bot battles, before the BRAVE merge): about 0.09–0.11 orbs per player action for the greedy bot and 0.05 for the casual bot. A battle is 5 waves of about 2.5 foes and about 86 player actions, so that's **8–9 orbs a battle (about 1.7 a wave) greedy, and 4–5 (about 0.9 a wave) casual**.
  - Analytic cross-check from `BattleConfig`: 12–13 foes × 105 charge × the share of their HP that pincers take (about 0.7), ÷ 1.5 for a typical three-hero pincer, × about 1.1 for chains ≈ 6 orbs. Fullness damping at a bank of 2–3 takes that to about 5. Crits, Guard (at most 0.4 a turn) and interrupts add 1–3, so play earns **6–8 orbs a battle**.
  - On top of play come whole orbs: Meditate +1 (cooldown 5), Mana Pour +1 (cooldown 4), Steal/Mug, a Mana Potion 2, an Ether 3, a Hi-Ether 6, battle-start gear 2–3, Mana Casks 1–3, and orbs carried on an expedition. So a well-stocked party can reach 15 or more.
- **The tiers.**

| Orbs | What it buys | Examples |
|---|---|---|
| 1 | single-target utility, spot heals, wards | Heal, Sleep, Silence, Antidote, Regen, Pre-emptive Ward, Hands of Mercy, Overdrive, Swap, Drench |
| 2 | single-target damage with a status rider; small heals with riders; Quicken | Shadowbolt, Omen, Thorned Hex, Drain Life, Den Mother, Slow, Flowing Current |
| 3–4 | strong single target; small-area damage; one long line | Fireball 3, Frost Nova 3, Poison 3, Hinder 3, Shock Wave 3, Bolt 4, Ward Grid 4 |
| 5–7 | strong area or party-wide effects: save for a wave or two | Red Mage Fury 5, Curse of Fate 5, Mass Heal 5, Barrier 5, Stasis 5, Cleansing Vigil 6, Sanctuary Rite 7, Frost 7 |
| 8–12 | ultimates: save most of a battle | Meteor 10 (11–12 left as headroom) |

- **The guide** (`ManaCostCurveTests.Guide`; every hero spell must sit within 2 orbs of it):
  - **Effect points per target:** 10 damage = 1 and 20 healing = 1. A status adds its strength × its chance: Frozen 1.5; Sleep 1; Burning 0.8; Poisoned and Ward 0.7; Silenced, Slowed, Regen and Empowered 0.6; Blinded 0.5; Wet and Protection 0.3. A cleanse adds 0.4, Pull 0.25 a tile, a swap 0.5, and Pushback 1.6 a second (the timeline is the game's lifeblood).
  - **Times the targets the shape expects to catch:** 1 for a single target or self, 1.6 for a radius-1 cross or diamond, 1.9 for a radius-1 square, 1.8 for a row or column, 2.4 for a plus or a radius-2 square, 2.6 for a radius-2 diamond, 2.5 for all foes and 4.5 for all allies.
  - **Flat additions:** a drain's heal, and party haste (0.8 a tempo point, capped at the 3 points of the tempo floor).
  - **Adjustments:** × 1.25 for an instant cast, × 0.8 for AdjacentOnly, × 0.85 for a 3+ turn cooldown.
  - **Priced convexly:** `cost = clamp(round(0.9 × raw^1.5), 1, 12)`.
  - *Why convex:* a burst that hits a whole wave ends it before the foes act, and saving orbs risks losing them to overflow and to a siphon (`DrainsOrbs`). So power in one cast costs more than its parts: single-target damage runs about 7–13 per orb, Meteor about 11 per orb only when it catches 2–3 foes, and Bolt or Ward Grid about 6 per orb.
- **Early kits.** The two slots open from the start (§4.7) hold spells of at most 3 orbs. The starting Cleric's and the tutorial's Heal and Pre-emptive Ward cost 1. The Drifter now opens with Frost Nova (3) and keeps Frost (10 since GG-T444: its 16-damage freeze on a 3×3 sits on the §3.1.1 curve at 10, not 7) in slot 4.
- **What reads the cost.** The spellbook price (`Abilities.Price` = 120 + 80 × cost, so Meteor costs 920 g). The VFX tier (`VfxStyleSheet.TierOf`: 6+ orbs is Ultimate, 3–5 Major). The bots (§4.3): `HeroBot.SavingFor`, and `CampRoutine.BestLearnable`, which buys a spell above 6 orbs only after every cheaper affordable choice.
- *(Tests: `ManaCostCurveTests`, `Every_hero_spell_costs_between_one_orb_and_a_full_bank`.)*

#### 3.1.2 Harvesting

**Amended 2026-10-11 (owner: "mana needs to be a more rare resource … the number of participants in the attack increases the multiplier needed to earn a mana bubble"):** pincers, crits, interrupts and the amplifier no longer mint whole orbs — they fill the **mana charge** meter ([§3.1.9](#319-mana-charge)). Orbs still mint whole through `BattleRunner.MintOrb` (event `OrbMintedEvent`, reason string) from:
- **Mana charge** rolling over 100 (reason = what filled it: `pincer`, `critical`, `interrupt`, `amplifier`, `guard`).
- **Steal / Mug** (`steal`): per target, chance `clamp01((LCK + 0.5 × AGI) / 50)` → 1 orb.
- **Battle-start gear** (`battle start`): `ItemDef.BattleStartManaOrbs` summed over the party's equipped items before wave 1 (Mage Robes 2, Wizard Robe 3).
- **Time bank** (`time bank`): see [§3.1.8](#318-time-banked-orbs).
- **Mana items** (`item`): Mana Potion 2, Ether 3, Hi-Ether 6.

The view flies each orb from its source to its slot in a 0.22 s arc (overflowing orbs fade in flight).

#### 3.1.3 Spending

`ManaBank.Spend(cost)` removes `cost` orbs or fails without spending. Orbs are spent at cast start; an interrupted cast refunds nothing. *(Test: `Any_orbs_pay_for_any_spell`.)*

#### 3.1.4 Bank-full rules (overflow)

| State | Mint |
|---|---|
| `Count + n ≤ cap` | all land |
| `Count == cap` | orb flies and fades; bank unchanged (`OrbMintedEvent.Landed = false`) |
| `Count + n > cap` | first `cap − Count` land, rest fade |

`ItemDef.BankCapacityBonus` gear raises the cap (summed over the party, max +4).

#### 3.1.4.1 The orbs row, orb costs and regeneration (GG-T673, owner 2026-11-22)

Owner: *"return the mana orbs line, doing things fills mana orbs which glow with bloom once they are active"*; *"it's easier to interpret orbs than it is to do math … make spells cost mana orbs that you regenerate with time and more so with actions"*; *"all mana orbs need to always be sized properly and spaced equally, rendered from left to right."*

- **HUD.** GG-T405's single globe is gone. A row of orbs (one per bank slot) sits a target gap under the ability row (under the board when the bar stands beside it). Every orb is the same size and the same gap apart, left to right, shrinking for a larger count so any capacity 1–20 fits. Lit orbs bloom (soft breathing glow, steady under Reduce Motion); the next empty orb shows the forming charge as a ring round its rim. Each orb earned from an action flies in as a mote with a sparkling trail along a quadratic bezier (randomised control point, so several fan out) into its own slot in order; time regen and Reduce Motion fill the orb without a flight. Aiming a spell pulses the orbs it would spend and rims the ones it still lacks in red; slot cost pips the row can't cover dim. Colours come from the blue Manalith art (GG-T674): `Palette.ManaDeep` #20193C, `ManaIndigo` #4B447D, `ManaBloom` #7A74AB, `ManaGlint` #B4516C.
- **Costs run 1–4 orbs** (`BattleConfig.MaxSpellOrbCost`, few enough to show as pips on the slot; validation refuses more). The old 1–10 costs folded monotonically, so cheap stays cheap and big stays big: 1→1 (12 spells), 2→2 (17), 3 and 4→3 (15), 5 to 10→4 (10). `HeroBot.SaveForCost` follows to the top tier (4).
- **Capacity is per battle** (`BattleSetup.OrbCapacity`, 0 = the old 12; bank gear adds its bonus; never above `BattleConfig.MaxOrbCapacity` 20). Owned Mana Orbs become a progression item in GG-T677 (start with 1, about 12–16 found in a straight run, cap 20); that ticket sets `OrbCapacity` from the save.
- **Full stops earning.** While every orb is lit, earned charge doesn't accrue at all (the ring stays empty) until a spell spends an orb. Explicit grants (potions, pickups) still overflow as in 3.1.4.
- **New sources**, on top of 3.1.9's (conservative first values, 100 charge = 1 orb, all damped by bank fullness): felling a real foe `KillCharge` 15; a hero's skill striking a foe `StrikeCharge` 5 per foe (spells excluded, so mana never feeds itself); time regen `RegenCharge` 10 every `RegenIntervalSeconds` 5 of the battle clock (about one orb per 50 clock seconds at an empty bank). Clutch! keeps `ClutchCharge` 20. Their reasons ("kill", "strike", "regen") show in the GG-T595 held-turn soak JSON beside "guard" and "pincer". Not yet measured; tune with the soak once the owner has targets.
- **Battle report.** `BattleReport.OrbsLeft` is the orbs still lit when the battle ended (GG-T676 converts them at stage end).

#### 3.1.4.2 Unspent orbs settle into Mana Dust (GG-T676, owner 2026-11-22)

Owner: *"any Mana Orbs you have at the end of a stage are converted back to manadust and added to your currencies."* The
orbs are Halcyon batteries that can't hold mana for long ([§27.3.3](#2733-the-mana-terminology-gg-t675-owner-2026-11-22)),
so at the end of a **won** stage every orb still lit in the row (`BattleReport.OrbsLeft`) settles into
**`DustEconomy.DustPerOrbLeft` = 1 Mana Dust** each, added to the bag with the stage's own Dust (`BattleReport.OrbDust`,
counted inside `DustEarned`). The results screen's Mana Dust row names the share: "+7 (4 from 4 unspent orbs)".

- **Why 1 per orb:** the same scale as `DustPerVictory` (1) and `FirstClearDustBonus` (4). A late-game row of 12–16
  owned orbs ended full pays a little more than a first clear, a real choice against casting, and still well short of a
  Shard's worth (`DustPerShard` = 10) per battle, so Shards stay valuable (GG-T625).
- **Expeditions:** orbs an expedition carries to its next battle (`CarriedOrbs`) stay orbs; the expedition's last
  battle settles them.
- **A loss** (defeat, retreat, escape): the orbs simply discharge, no Dust, like every other reward of a loss.
- The economy ledger (`polish.py --balance`) prints each run's Mana Dust and the share from orbs (`#dust`, `#orbdust`).

#### 3.1.4.3 Owned Mana Orbs are progression (GG-T677, owner 2026-11-22)

Owner: *"collecting 'Mana Orbs' becomes important because that is literally how much mana orbs you can fill in combat,
so once you fill all your mana orbs you have to cast it to gather more mana, so the impact is immediately felt because
going from one Mana Orb to two Mana Orbs doubles your magic casting abilities, and some spells require more Mana Orbs
than you have so they aren't even worth equipping."* Numbers: *"with 5 slots and a spell cost max of say 4 mana … that's
a total of 20 possible orbs in the game, with possible 12-16 being found in straight play through; this is a suggestion
will need to dial this in naturally."* Sources the owner picked: boss and milestone rewards, finds in Halcyon ruins,
and the Manasmith restoring broken orbs found as loot.

`SaveData.OwnedOrbs` (`Meta/ManaOrbs.cs`) is how many orbs the battle's row can fill (`BattleSetup.OrbCapacity`, from
`BattleFactory.Build`); a full row stops earning until a spell spends one (GG-T673). Every source is finite, and
`ContentLoader` refuses content where they add up past `BattleConfig.MaxOrbCapacity` (20).

| Source | Orbs | Where it lives |
|---|---|---|
| Starting orb | 1 | `BattleConfig.StartOwnedOrbs` |
| Milestone orbs: the first full clear of each main-campaign theme (Tithe Moors through the Cathedral, 8) and of three postgame themes (the Bloom, the Underspire, Morrow's Shadow) | 11 | `campaign.json` `MilestoneOrb` |
| Halcyon finds: each stage of the two Halcyon ruins (Halcyon ×3, the Underspire ×2) holds one orb, found on a victory there with a 25% chance until found | 5 | `campaign.json` `OrbFindChance` = 0.25 |
| Broken Mana Orbs: a 4% chance per campaign victory (`ManaOrbs.BrokenOrbChance`) while fewer than 3 have dropped; the Manasmith restores each for 20 Mana Dust + 100 G | 3 | `BattleConfig.MaxBrokenOrbs`, `ManaOrbs.RestoreDust/RestoreGold` |
| **Total possible** | **20** | |

A straight playthrough (main campaign and postgame, no replays) earns the 12 guaranteed orbs, finds about 1–2 of the 5
Halcyon orbs on single clears, and turns up about 2 Broken Mana Orbs over its ~50 victories: about 14–16, the owner's
range. Replaying Halcyon stages and longer play reach 20. Rolls are seeded by `SaveData.OrbRolls`, so a save replays
them identically.

- **Migration:** a save from before owned orbs (`OwnedOrbs` 0) gets `ManaOrbs.Derived`: the starting orb plus one per
  orb milestone it has already been paid (`SaveData.Milestones`). Nobody is stripped of casting below what progress
  earned; a pre-GG-T677 save could fill 12, so a veteran early in the campaign now fills fewer, by design.
- **Loadout:** the Abilities screen marks a spell whose orb cost is above the orbs held ("Spell · needs 3 orbs, you have
  2", in the warning colour) and dims its extra pips. It can still be equipped.
- **Manasmith:** the refinery screen shows orbs held of 20, Broken Mana Orbs carried and a "Restore a Mana Orb" action.
- **Results:** "Mana Orb found · you now hold N" with the reason (a milestone, a Halcyon find), and a Broken Mana Orb row.
- **Everywhere you fight** (campaign, expeditions, the Crucible) fills your owned orbs. Only battles built outside
  `BattleFactory.Build` (harness skirmishes, tests) keep the old default row of 12. Bounties don't drop orbs, and the
  Crucible pays no orb Dust (it commits on its own path).
- **Balance:** casting starts at one orb, so every number here and every spell cost needs the next balance pass
  (GG-T276) to measure with owned orbs.

#### 3.1.5–3.1.6 (struck 2026-10-10)

~~Spend ordering by color~~ and ~~the Colorless wildcard~~ no longer exist — there are no colors to order or substitute.

#### 3.1.7 At-a-glance mint cadence

| Event | Mana |
|---|---|
| 2-hero pincer that takes half a foe's HP | 52 charge (×1) |
| The same damage with 2 supporters joining | 26 charge (÷2) |
| Third link of a chain, same damage | 79 charge (×1.5) |
| Critical hit (physical or spell) | +10 charge |
| A Mana supporter (Red Mage, Ritualist, …) joining a pincer | its charge × 0.5 (Red Mage 30 → 15) |
| Any of the above with the bank at 6/12 · at 12/12 | × 0.65 · × 0.30 (fullness damping, [§3.1.9](#319-mana-charge)) |
| Steal/Mug, 3 adjacent enemies, 2 rolls succeed | 2 orbs |
| Cancel an enemy charge | +25 charge |
| Battle start, one Mage Robes equipped | 2 |
| Drop with ≥ 3.75 s left on the drag timer, then an enemy turn | 3 (cap) |
| Guard with 2 s of turn left (GG-T189) | 20 charge (cap 40) |

#### 3.1.8 Time-banked orbs

**Amended 2026-10-09 (owner decision):** the bank is fed by the **optional drag timer**, not by real-time seconds before an enemy trigger.
- A committed move stores `PendingTimeBankSeconds = clamp(TimeLeftSeconds, 0, 4)` (0 when the timer is off).
- At the start of the **first enemy turn** during that action's clock advance (`MintTimeBank` in `RunEnemyTurn`), it converts to **1 orb per `SecondsPerTimeBankOrb` = 1.25 s**, max `MaxTimeBankOrbs` = **3**, with a "Banked N mana" announcement.
- It is cleared at control return and by Shield. Consequence (current truth, tuning watch): a quick drop that is **not** followed by an enemy turn within the same action banks nothing.

#### 3.1.9 Mana charge

Added 2026-10-11 (owner direction). `BattleState.ManaCharge` (0–99) is the forming bubble; the HUD draws it filling the next empty orb from the centre. `AddCharge` emits `ManaChargedEvent` and every full `ManaChargePerOrb` = **100** becomes an orb.

- **Pincer:** `charge = PincerChargePerHp (105) × share × (1 + 0.25 × chainIndex) ÷ (1 + 0.5 × (participants − 2))`, minimum 2 when it lands. `share` = Σ over the trapped foes of `min(1, HP removed by this pincer ÷ MaxHP)`; `participants` = the two attackers + every supporter. So a lean two-hero pincer earns at full rate, a four-hero dog-pile at half, and chaining is the way to farm.
- **Critical** +10, **interrupt** +25, **amplifier** +30. Kills alone earn nothing.
- **Mana supporters** (`SupportEffect.Mana`: Hexer, Harbinger, Machinist, Red Mage, Ritualist, Tinkerer) charge their `Magnitude × SupportManaFactor` (0.5).
- **Fullness damping** (*Amended 2026-11-05, GG-T247*): every earned charge above (pincer, crit, interrupt, amplifier, support, Guard, Mana Siphon armor) is multiplied by `1 − ChargeFullnessDamping (0.7) × Bank.Count ÷ Bank.Capacity` before it lands (`BattleEngine.DampedCharge`; a positive charge never rounds below 1). An empty bank fills at the full rate and a full one at 30%, so mana stays easy to start and hard to max out. Explicit grants are whole orbs and skip it: items, pickups, Steal/Mug, battle-start gear, the time bank and carried orbs.
- **Guard** (GG-T189): the unused turn time at 10 charge per second, max 40 ([§2.5](#25-auto-skip--shield)). This is the "fractional orb": the bubble is progress toward the next orb, and a rollover with the bank at 12 fades ([§3.1.4](#314-bank-full-rules-overflow)) while the remainder stays in the bubble.
- The billboard shows the arithmetic ("Mana +34 · 3 heroes ÷1.5 · chain ×1.25").
- **Where the economy sits** (*measured 2026-11-05, GG-T265*, after GG-T247's tempering and GG-T262's 45% enemy HP; 206 bot battles, 12 per stage): the late-campaign flood is gone (the bank is full on 0% of greedy turns and 1–3% of casual turns, 0–7% of orbs fade, where cityruins-03 had been full on 94%). Bots bank about 0.11 orbs per player action (greedy) and 0.05 (casual), and a casual bot holds 2+ orbs on about 23% of turns. GG-T247's "casual holds 2+ orbs on ≥ 30% of turns" target is **retired**: it measures hoarding, and a bot that spends the moment it can rarely holds two whatever it earns. Raising `PincerChargePerHp` to 200 to buy a spell-per-wave earn rate lifted the casual bot only to 0.08, and flooded the Tithe Moors stages (full on 36–55% of turns) because that bot doesn't spend there, so the charge stays at 105. POLISH G3 stays the gate (no flood, ≤ 20% wasted).

*(Tests: `More_heroes_in_a_pincer_earn_less_mana_per_hp_taken`, `Charge_rolls_over_into_orbs_and_keeps_the_remainder`, `A_killing_pincer_earns_mana_by_hp_taken_not_per_hero`, `Supporters_join_but_dilute_the_mana_charge`, `ManaEconomyTests`. Instrument: `ManaEconomyReport` — `[Explicit]`, run with `--filter ManaEconomyReport`, `GRIDGAME_MANA_SEEDS=N` — bots play every campaign stage and report how full the bank runs.)*

### 3.2 HP and stats

`Unit.Effective` = `LevelStats` + `GearStats`, with **Speed = level SPD + gear SPD + ½ gear AGI** (**Amended 2026-10-09**); STR, VIT and Speed are floored at 1.

| Stat | Drives | Read in |
|---|---|---|
| HP / MaxHP | life | `Formulas.MaxHp = floor(12 + 5·VIT + level)`; an enemy spawns with `× EnemyHpScale` (0.45, GG-T262) |
| STR | physical offense; pushback; stagger push | `Formulas.Physical`, `Pushback`, `StaggerCasts` |
| VIT | HP + physical defense | `MaxHp`, `Physical` |
| AGI | Evasion; pushback stun recovery; Steal; ½ into Speed from gear | `Formulas.Evasion`, `Pushback`, Steal roll |
| SPD | timeline rate; hit chance | `RateFor`, `Formulas.HitPercent` |
| STA | reserved | — |
| INT | enemy magic offense; cast time; threat weighting | `Formulas.Magic`, `CastTime`, `PickTarget` |
| WIS | magic defense; cast time; cast poise | `Magic`, `CastTime`, `StaggerCasts` |
| LCK | crit; hit; Evasion; Steal; Clutch; damage variance tilt | `CritPercent`, `HitPercent`, `Evasion`, Steal, Clutch |

Level stats: `Formulas.StatsAtLevel` = base + growth per level from 2 + milestone bonuses at their level, each stat floored. Spell damage with a non-zero base always does ≥ 1 unless the target is immune (resistance 0).

#### 3.2.1 Stat-to-effect cheat sheet

Tank: VIT, STR (not INT) · glass cannon: INT, WIS, LCK (not VIT) · skirmisher: SPD, AGI, LCK · heavy hitter: STR, VIT (not SPD/AGI) · rogue: LCK, AGI · mystic support: WIS, INT (not STR).

### 3.3 Elemental resistance

`Unit.ResistanceMultiplier(type)` = `ClassDef.Resistances[type]` × each equipped item's `Resistances[type]` (multiplicative; missing = 1.0; 0 = immune). `DamageType`: `Physical, Fire, Ice, Lightning, Poison, Holy, Dark, Arcane`. Spell damage composes ([§13.1.2](#1312-spell-damage-dispatcher)):

```
final = round(base × resistance × (Lightning on Wet ? 1.5 : 1) × incoming-damage multiplier)
```

Current content authors no class resistances; the only resistance gear is the Sunfire Amulet (Fire ×0.7).

*Amended 2026-11-08 (GG-T371):* several enemy families now author resistances (ceramic knights, cyber zombies and tree golems Poison ×0; harpies, heavy troopers and minotaurs weak to Lightning; the Lich and the Void Sentinel resist Dark and fear Holy; Infernalists and Hellspawn resist Fire and fear Ice; wraith knights the reverse; cultists resist Arcane and fear Dark). §3.3.1 guarantees each one a counter for sale.

#### 3.3.1 There is always a way (GG-T371, added 2026-11-08)

Owner: "always a way to defeat them (cast fire against ice, be able to buy a consumable with elemental effects in case your party doesn't have what it needs specced into the build, there is always a way)." No fight may require a build the party lacks: every wall a foe or the board puts up has a counter the Merchant always sells.

- **The interaction table** (`elements.json`, `ElementTableDef`, one row per `DamageType`, content-validated):

| Element | Counters (beats foes that resist) | Its flask's status | Breaks / exploits | Tiles it clears |
|---|---|---|---|---|
| Physical | — | — | — | — |
| Fire | Ice | [Burning] | breaks [Frozen] and [Wet] | slime trail, venom snare, poison cloud |
| Ice | Fire | [Frozen] | — | mire, quicksand (frozen solid) |
| Lightning | Poison, Arcane | — | exploits [Wet] (×1.5) | shock mine (set off harmlessly) |
| Poison | — | [Poisoned] | — | — |
| Holy | Dark | — | — | poison cloud |
| Dark | Holy, Arcane | [Blinded] | — | — |
| Arcane | Physical (spells go around plates) | [Silenced] | — | — |

  - *Breaks / exploits* record what the dispatcher already does (§6); a test keeps the two in step.
  - *Tile reactions* (`TileReactionDef`: `On` a trap or tile-state id, `Kind` Clear, or Becomes `Into`) mirror GG-T359's ClearTiles and MarkTiles. GG-T370's hazard trails (slime, fire, ice, mines) add their tile-state ids to these rows.
  - Two *tool sets* sit beside the elements (`Reactions`): **disarm** (venom snare, shock mine, caltrops) and **cleanse** (slime trail, mire, quicksand, poison cloud).
  - `RuleBreaks` records how each rule-break (§14.9) is answered. All five are Positional today.
- **In battle** (`BattleRunner.Elements.cs`): a hero's ability runs its tile reactions on the traps it covers. That is its tool set (`AbilityDef.TileReaction`), plus its element's row when it deals damage.
  - A tile pick is legal when it clears a trap, even with nobody in reach (throw the kit at the mine). A tile-only use resolves without a "Missed!".
  - Each cleared trap emits `TrapClearedEvent`: the board resyncs, the trap cue plays and "Cleared!" floats.
  - Enemy abilities leave the board alone.
- **The Merchant's counters** (`GridGame.Studio/tools/counter_items.py`). All are Common, stack 5, and `SoldByVendor`, so they are on the shelf from the first expedition (§25.1). **Amended 2026-11-21 (GG-T538):** the Disarm Kit and Purifying Salts are on the first shelf and the flasks join at stock tier 2 (after The Tithe Moors III), but a counter any reachable stage needs is stocked whatever its tier (§25.0.10), so the promise holds from the first battle. Names are labels; descriptions are Prose `item:<id>` texts.

| Item | Price | Effect (picks a tile; a plus of radius 1 unless noted) |
|---|---|---|
| Fire Flask | 55 | 20 fire + [Burning] |
| Frost Flask | 60 | 16 ice + a [good] chance of [Frozen] |
| Storm Flask | 55 | 20 lightning (×1.5 on [Wet]) |
| Venom Flask | 45 | 12 poison + [Poisoned] |
| Blessed Flask | 55 | 20 holy |
| Shadow Flask | 55 | 20 dark + a [fair] chance of [Blinded] |
| Mana Flask | 55 | 20 arcane + a [fair] chance of [Silenced] |
| Purifying Salts | 30 | a 3×3 area: the cleanse set |
| Disarm Kit | 40 | a 3×3 area: the disarm set |

  They are priced between a Healing Potion (25) and a Hand Grenade (90, 28 fire in a 3×3). Flame Oil (70, one foe) and Holy Water stay drops.
- **The validator** (`ElementCounters.Validate`, run by `ContentLoader.Validate`) checks every enemy class a campaign stage fields, rule-break bosses and laid eggs included. A foe's multiplier per type is its `Resistances` × its unconditional `ReductionMultiplier` passives.
  1. Some Merchant consumable hurts it at full strength (×1 or better).
  2. Each weakness (above ×1) is a type the Merchant sells.
  3. For each element it resists, the table names a counter; the foe doesn't resist that counter too; the Merchant sells it.
  4. A foe with an armor plate (§13.5) has a non-Physical Merchant consumable it doesn't block (at or below `HeavyResistance`, 0.25).
  5. An element-gated ward (a buff it can raise that takes ×0.25 or less) is broken (`Breaks`) by an element the Merchant sells.
  6. Every rule-break in a campaign stage has a `RuleBreaks` row: Positional, or an Element or Item the Merchant sells.
  7. Every trap, and every GG-T359 tile state marked Hazard, is cleared by some Merchant consumable.

  It also checks the table's shape: a row per type, known buffs, trap and tile-state ids, reaction sets. Each problem names the stage, the foe and the gap. Current content passes with none.
- **The camp bot** (`CampRoutine`, §25.3.3) buys the next stage's counters (`ElementCounters.CountersFor`).
- *(Tests: `AlwaysAWayTests`, written and not run.)*

---

## 4. The AbilityBar

The battle HUD shows **5 slot buttons** for the **selected hero** (`BattleRunner.MaxSlots` = 5); campaign progress makes **2 to 5** usable ([§4.7](#47-progressive-slot-unlock)). Each slot is an `AbilitySlot`: an ability id, or an item stack.

### 4.1 The three kinds

| Kind | Cost | Cast time | Frame |
|---|---|---|---|
| **Skill** | free; costs the action; per-skill cooldown | instant | green |
| **Spell** | mana orbs | cast icon on the timeline (all content spells: 1.5 s base) | blue + cost pips |
| **Item** | one charge of the slot's stack | instant | warm leather; "name n/max" |

**Amended 2026-11-06 (GG-T296, owner): Active, Passive, Reactive.** Skills, spells and items are the *Active* kind: you take them. Two more kinds never sit on the bar. A **Passive** just happens (Vampire's Kiss heals when you attack; Tough Hide always softens blows). A **Reactive** fires after something happens *to* you, usually on a chance ("when [attacked directly], a [slight] chance to [Counterstrike]"). Heroes and enemies use the same system, so a character can be built to benefit from being hit, dodging or being surrounded.

- **Data:** `AbilityDef.Trigger` (`TriggerDef`): `On` (the event), `Chance` (0..1; 1 never touches the Rng; optional `ChancePerLuck`), event parameters `Count` (OnSurrounded), `Threshold` (OnLowHp) and `Status` (OnStatusGained), an optional `Condition` (owner HP below/above, has/lacks a status, adjacent foes ≥ n, the other unit's tag, the damage type), a list of `Effects`, and the guards `Cooldown` (own turns) and `OncePerAction`. `Callout` overrides the float text. Everything is checked by `ContentLoader.ValidateTrigger`.
- **Events (`TriggerOn`):** `Always` (modifiers only), `OnAttack` (a primary weapon attack, hit or miss), `OnLandedHit`, `OnDamaged`, `OnAttackedDirect` (an adjacent foe's blow lands), `OnAttackedAny` (any foe's blow or ability lands), `OnDodge` (a foe's blow or charged spell misses or is slipped by Evasion, §13.1.0), `OnSurrounded` (at turn start), `OnStatusGained`, `OnLowHp` (a hit crosses the threshold), `OnAllyKO`, `OnKill`, `OnPincerAsAttacker`, `OnPincerAsSupporter`, `TurnStart`, `TurnEnd`, `ActionEnd`. A hero's turn starts and ends each time control returns to the party; an enemy's is its own turn.
- **Effects (`TriggerEffectKind`):** `Heal` (fraction of max HP), `Lifesteal` and `Reflect` (fraction of the context's damage), `ApplyBuff`, `Counterstrike` (a blow at ×Amount on a foe in reach), `FollowUp` (strike the target again at ×Amount of the blow), `CastAbility` (an Active ability resolved instantly and free through the normal dispatcher), `Cleanse`, `ManaCharge` (heroes), `Pushback` (seconds, foes). The `Always` modifiers are `DamageMultiplier` (the owner's weapon blows), `ReductionMultiplier` (damage taken) and `Immunity` (statuses). `Who` (Self/Target/Attacker) overrides each effect's default target.
- **Engine (`BattleRunner.Triggers.cs` + pure `Rules/Triggers.cs`):** `Triggers.Sources(unit)` lists a unit's passives in a fixed order: class kit, learned passives (`Unit.Passives` from `HeroSpec.Passives` from `HeroSave.Known`), enchants on intact armor, then the weapon's named lifesteal. The funnels fire it: `Damage` (OnDamaged, OnAttackedAny, OnLowHp), `Strike` (OnDodge, OnLandedHit, OnAttackedDirect, OnAttack), `Kill` (OnKill, OnAllyKO), `ApplyBuff` (OnStatusGained), pincer resolve, turn starts and ends, and the end of a command. Owners go in unit-id order, abilities in source order, and only a chance below 1 draws from the battle Rng. **A reaction never provokes a reaction:** a Reactive can't fire from inside another Reactive's effects, and no ability re-enters itself. A Passive such as Lifesteal still rides a counter blow.
- **Presentation:** every firing emits `TriggeredEvent` ahead of its effects. The view floats the callout over the unit with a sound: a clang for Counterstrike/Reflect, a war cry for a free cast, a power-up otherwise (POL-D4). The InspectCard and the bestiary kit line show **Passive** or **Reactive** with a sentence generated from the data (`TriggerText.Describe`), keywords in [brackets].
- *(Tests: `TriggerEngineTests`, `ArmorEnchantTests`, `Double_attack_passive_strikes_twice_and_counter_strikes_back`.)*

#### 4.1.1 Skill cooldowns

A skill locks for `AbilityDef.CooldownTurns` after use: **Steal 3, Mug 2, Teleport 3**. Countdowns live per hero in `Unit.Cooldowns`. **Amended 2026-10-09:** they tick **once per committed action** at control return (the old "turn-cycle" = hero window is now one action). A cooling slot shows "CD n" and refuses ("Recharging"). *(Test: `Teleport_relocates_and_fires_pincer_then_goes_on_cooldown`.)*

#### 4.1.2 Signature passives (GG-T302, added 2026-11-06)

**Amended 2026-11-16 (GG-T388):** nine kits made distinct so no two heroes play alike: Myrmidon hooks a foe from anywhere and pulls it 2 tiles; Panda Brawler's slam taunts (feeding its surrounded bonus); Slasher's Flurry hits every adjacent foe; the Ronin steps back 2 tiles after the cut; the Fighter's Combo Strike delays the foe 1 s; Night Hunter's Night Stalk makes her Blurred; the Alchemist's Transmute cleanses and grants Protection (no heal); Knight's Oath swaps places with the ally, then wards them; the Shieldguard's (`shieldmaiden`) Valkyrie's Call revives a fallen ally at 30 HP (cooldown 6, the game's only revive skill).

Owner: "You could design a character that benefits from being hit, or from dodging hits, or from being surrounded by enemies." Every hero class carries **one or two** Passive/Reactive rows in its `classes.json` `Abilities` (they never reach the bar, `MetaServices.IsHeroUsable`), and most enemy families carry 0–2 so fights read differently. All are `abilities.json` rows on the §4.1 engine; numbers stay modest.

| Archetype | Classes | Signature (id) | Rule |
|---|---|---|---|
| Vampire | vampire | Vampire's Kiss (`vampires_kiss`), Blood Feast (`blood_feast`) | landed blow → [Lifesteal] 15%; a kill heals 10% |
| Guard Stance duelists | duelist, fencer, ronin, sellsword, swordmaster | Guard Counter (`guard_counter`) | while [Warded] (Guard Stance's ward), every direct blow is answered with a [Counterstrike] ×0.6 |
| Dodge fighters | duelist, swordmaster, dervish, martial artist (Evasive Riposte); fencer, chroma ninja, speedster, courier, hexstalker (Blur); thief, white ninja (Catch Breath) | GG-T297 rows | OnDodge synergies |
| Shield tanks | knight, defender, paladin, jade knight | Shield Wall (`shield_wall`) | [surrounded] by 2+ at turn start → [Ward] |
| Brawler tanks | myrmidon, tank, panda brawler | Unbowed (`unbowed`) | [surrounded] by 2+ → [Empowered] |
| Hit-builders | warchief, shieldguard, prizefighter | Grudge (`grudge`) | damaged → 20% [Empowered], rests 2 turns |
| | fighter, defender, sellsword | Battle Hardened (`battle_hardened`) | attacked directly → 20% [Protection] |
| | dark templar | Dark Retribution (`dark_retribution`) | attacked directly → 35% [Reflect] 30% |
| Berserkers | barbarian (Berserk); bruiser, wild child (Blood Rage `blood_rage`); odachi, ronin (Grim Resolve `grim_resolve`) | | below 50% ×1.35; crossing 50% → [Empowered]; crossing 35% → heal 15% |
| Ninjas / rogues | green ninja, slasher (Venom Edge); red ninja (Ember Edge); yellow ninja (Static Edge); blue ninja (Rime Edge 10%); black ninja (Shadow Edge); street fighter (Dirty Fighting 20%) | `*_edge`, `dirty_fighting` | landed blow → 15% status on the target |
| Finishers | assassin, night hunter (Vanish → [Blurred]); striker, ripper, harbinger, hexsniper (Bloodlust → [Empowered]); raider (Blood Feast) | | on a kill |
| Counter-punchers | pugilist (Counterpunch 20% ×0.6), lancer (Set Spear 25% ×0.75), basher (Rattle: 15% [Pushback] 0.5 s on a landed blow) | | |
| Healers | cleric, acolyte, doctor, alchemist, white ninja, lightweaver (Mending Touch: after supporting a pincer, heal the backed attacker 5%); cleric, acolyte, sage (Vigil: an ally falls → [Regen]); monk (Inner Calm: 3% each turn start); alchemist (Poison Ward); lightweaver (Pure Spirit: can't be [Silenced]) | | |
| Mages | hexer, red mage (Arcane Harvest: a kill → 10 [mana charge]); ritualist (Blood Price: damaged → 4, once per action); sage, tinkerer (Arcane Insight: supporting → 5); drifter (Chill Aura: 25% [Slow] the direct attacker) | | |
| Engineers / rangers | machinist, operative (Shock Plating: 25% [Pushback] 0.75 s on the direct attacker); engineer, technician (Self-Repair: below half HP, 5% each turn start); hexwarden (Keep Away 35%, 1 s); blue lion (Vengeance) | | |

**Enemies** (same rows, readable at the Bestiary's abilities tier): frogs, toad, Swamp Mistress — **Toxic Skin** (attacked directly → 35% [Poison] the attacker); gangers, wolves, templars, captain — **Vengeance** (an ally falls → [Empowered]); cyber zombies — Poison Ward + **Rotting Bite** (20% poison); undead, skelepedes, marsh shamblers — Poison Ward; ghost — **Ethereal** ([Physical] ×0.8) + Poison Ward; phantom — Ethereal; mecha armor — **Mirror Plating** (a landed attack → 50% [Reflect] 20%); slimes — **Gelatinous** (damaged → 25% [Protection]); hags — **Spiteful Hex** (25% [Blind] the direct attacker); lurkers — Evasive Riposte; tech gremlins — Shock Plating; flying monkey — Blur; goblin thug — Dirty Fighting; Demon Lord, oni — **Infernal Hide** (can't [Burn]); Ice Mauler, yeti — **Frostborn** (can't [Freeze]) + Chill Aura; vulture — **Carrion Feast** (an ally falls → heal 15%); reaper — Blood Feast. Bosses carry two: Cyclops 00–02 Tough Hide + Blood Rage, Naga **Serpent's Retort** (35% [Counterstrike] ×0.6) + Poison Ward, Chronomancer **Time Slip** (35% [Pushback] 1 s) + Steadfast, Demon Lord Lifesteal + Infernal Hide. Bats, soldiers, mannequin, ceramic knights, steppin' razors, prototypes, oni, troll, werewolf, tree golems and sand maw keep their GG-T296 rows.

- **Text is honest:** every chance in a description uses its band word ([slight] ≤15%, [small] ≤27%, [fair] ≤42%, [good] ≤62%); `ContentLoader.Validate` also fails a `TriggerText` sentence whose [terms] miss the glossary (new entries [Lifesteal], [Reflect], [mana charge], [Physical]; "attacked directly" is an alias of [directly]). Guard Stance now says what it does (Ward, and Guard Counter while it holds); the Training Rapier +3 lifesteal passive is *Bloodied Point*, not "Riposte"; Counter Attack's text names [attacked directly]; Mirrorcurse's node says it blinds.
- *(Tests: `SignaturePassiveTests`.)*

### 4.2 Per-hero loadouts

**Amended 2026-11-16 (GG-T388, owner: "skills start as few but they are purchased or discovered over time"):** a new hero knows only its `StarterKit` (loadout abilities 1 and 3: its role move and its signature); the rest come from the trainer at 100 G, the skill tree, or Tomes. A hero who joins later arrives knowing kit moves for its level (`TrainTo`: 3rd at Lv 3, 4th at Lv 6).

The bar follows the selected hero. Its slots come from the hero's saved bar, `HeroSave.Bar` (5 entries: ability id, `"item:<id>"`, or null), edited on the Abilities screen ([§25.6](#256-abilities)). A new hero's bar is its class `DefaultLoadout` (`Progression.NewHero`), whose abilities become **Known** ([§25.6](#256-abilities)). **Amended 2026-10-09:** there is no separate `HeroLoadouts` preset layer or `CombatLoadouts` cache — the saved bar is the bar.

Seeded loadouts (`classes.json`):

| Class | DefaultLoadout |
|---|---|
| Cleric | heal, heal, frost, item:healing_potion_basic |
| Paladin | heal, fireball, item:healing_potion_basic |
| Barbarian | fireball, bolt, item:healing_potion_basic |
| Alchemist | frost, item:healing_potion_basic, steal, item:cons_sleep_dart (duplicate potion entry dropped by `NewHero`) |
| Assassin | steal, mug, bolt, item:healing_potion_basic |
| GreenNinja | teleport, steal, fireball, item:healing_potion_basic |
| RedNinja | teleport, mug, bolt, item:healing_potion_basic |
| all 55 other hero classes | heal, fireball, frost, bolt, item:healing_potion_basic |

On a fresh save (2 slots) the starting trio's potion sits in locked slot 3 until the first clear.

### 4.3 Item stacks and effects

- `"item:<id>"` draws its charges from the **inventory**: `min(owned, MaxStack)`; a second slot of the same item takes from what remains (stacks split, never duplicate). Each use spends a charge and is recorded in `BattleResults.ItemsConsumed`, deducted from the save at commit — **Amended 2026-10-09:** on victory, defeat, retreat and escape alike.
- `"item:<id>:<n>"` is a preset stack of n charges not drawn from inventory (available for test/setup).
- A consumable's battle effect (`BattleRunner.ItemAbility`) is its `OnUseAbility` (Sleep Dart → `sleep`), else synthesized from item knobs. **Amended 2026-10-09:**

| Knob | Effect | Items |
|---|---|---|
| `BaseHealing` | single-ally heal (PickActor, AllyOnly) | Healing Potion 50, Greater Healing Potion 90, Hi-Potion 150, X-Potion 500 |
| `BaseDamage` (+`DamageType`, `BonusVsTag` ×`BonusMultiplier`) | single-enemy hit | Holy Water 40 Holy (×2 vs Undead), Flame Oil 45 Fire (×1.5 vs Beast) |
| `GrantsOrbs` | orbs (Auto) | Mana Potion 2, Ether 3, Hi-Ether 6 |
| `CuresBuff` / `CuresAll` | remove one debuff id / all debuffs from an ally | Antidote (poisoned), Greater Antidote (all), Eye Drops (blinded), Remedy (all) |
| `Revives` | first fallen hero returns on the free tile nearest the user with `BaseHealing` HP; refused if nobody has fallen | Revival Charm (50) |
| `Escape` | battle ends as **Escaped** ([§22.2](#222-failure-path-detail)) | Smoke Bomb |
| `OutOfBattleOnly` | no battle effect; Camp only | Tent |
| `OnUseAbility` + `TileReaction` | the ability also clears the traps it covers ([§3.3.1](#331-there-is-always-a-way-gg-t371-added-2026-11-08)) | Purifying Salts, Disarm Kit (the elemental flasks are `OnUseAbility` hits) |

### 4.4 Click flow per kind

All three go through `UseSlotCommand` → `BattleRunner.DoUseSlot`, which re-checks `GetSlotStatus` and the target ([§4.5](#45-slot-visual-states)); a refusal costs nothing.
- **Item** — spend a charge → resolve its ability immediately.
- **Skill** — start its cooldown → resolve immediately.
- **Spell** — `Bank.Spend(cost)` → if `CastTime > 0` queue a cast icon (target unit remembered for PickActor, tile for PickTile), else resolve immediately. Every accepted use is an action (it ends the turn at the next enemy arrival, GG-T188).

#### 4.4.1 Select → confirm → act

Added 2026-10-11 (owner: "confirmation screens when deciding to cast a spell"). With Settings → **Confirm before acting** on (default):
- A pick ability: tap the slot → legal anchors glow gold → tap a tile → the pick **locks**: its area and every unit it would touch glow (red harmful / green helpful / blue teleport), the banner asks "Poison on Wolf? Confirm below", and the actor panel becomes the **confirm card** (icon, name, cost pips, timing, "On Wolf", **Cast/Use/Go** and **Cancel**). Tapping another legal tile re-aims; tapping the same tile again confirms.
- An Auto ability (Taunt, War Cry, Mass Heal …) goes straight to the confirm card.
- Nothing is spent until Confirm (`BattleScreen.ConfirmPending`, chime `ui.confirm`); Cancel / slot re-tap / Back play `ui.cancel`.
- **Outcome preview** (*added 2026-11-05, GG-T252, POL-D1*): the confirm card lists up to three touched units with their damage (red) or heal (green) **range**, **KO** chance, statuses and their chance, and the orbs the action would mint. The numbers come from `OutcomePreviewer.Preview` (Core, pure): it runs the command on six `BattleState.Clone`s, the first with the battle's own dice and the rest re-seeded, and reads only the actor's resolution window (until the next enemy turn starts; reopened when the actor's own charged cast resolves). The real battle and its RNG are never touched (`OutcomePreviewTests`). The confirm column is 28% of the card (≥ 264 px), not a fixed 330.
- Autoplay and the setting turned off commit on the pick, as before.

### 4.5 Slot visual states

`GetSlotStatus` (in precedence order) and how `BattleScreen.SyncBar` draws it:

| Status | When | Visual |
|---|---|---|
| CannotAct | hero dead, or immobile (Frozen/Sleep) | 45% opacity; refused "Can't act" |
| Locked | slot ≥ unlocked count | "Locked", 35% opacity |
| Empty | no entry / unknown ability id | "—", 50% (an item with no battle effect, e.g. a slotted Tent, reports Ready but is refused "Empty" on use) |
| OutOfCharges | item, 0 charges | name + "0/n", 45% |
| Cooldown | skill counting down | "Name\nCD n", 45% |
| Silenced | spell while caster Silenced | red border + "Silenced" |
| Unaffordable | spell the bank can't pay | 45% |
| CastCapReached | cast-time spell with 4 hero casts in flight | 45%; "Too many spells!" |
| Ready | otherwise | full opacity |

The slot being targeted gets a gold border and 1.06× scale, and **while targeting, the actor panel shows the ability's card** (icon, name, cost pips, "Cast time 1.5s — rides the timeline" / skill recharge, description, "Tap a highlighted tile. Tap the slot again to cancel."). **Amended 2026-10-09.** ⬜ Not yet built: hover/long-press tooltips on idle slots and the radial cooldown sweep.

### 4.6 Cancel paths

Targeting is cancelled by tapping the same slot again, the hardware/Escape **Back** action (`BattleScreen.OnBack`), or committing any other command. Cancelling spends nothing.

### 4.7 Progressive slot unlock

**Amended 2026-10-11 (owner: "give each character 2 stacks of full life potions and unlock the slots to put them in … the 2 healing potion is two stacks of 5x"):** all five slots are open from the start (`Progression.StartingSlots`). Every hero's default bar is two abilities, **two X-Potion stacks (5 each, full heal, usable on any ally)**, then more abilities; a new game stocks a full stack per potion slot (5 heroes × 2 × 5). The threshold rule below is kept for reference but no longer gates anything.

**Amended 2026-10-11 (later the same day, owner direction) — one X-Potion stack, one new skill per class:** the two X-Potion slots collapsed into **one** (still a full stack of 5; a new game now stocks 5 heroes × 1 × 5), freeing the 5th bar slot for a **brand-new, genuinely unique skill per hero class** — all 62 classes, even ones that otherwise share an identical kit (e.g. the 19 "war_cry bruiser" strikers), each got their own ability authored to that class's own name/flavor rather than just its Role. All 62 are purely data-driven (`abilities.json`, `Kind: Skill` for non-casters / `Kind: Spell` for the 6 Mage-role classes), reusing the existing generic resolver and the 13 existing buffs — no new engine code. Every one has a real pixel icon (`tools/icons.json` + `GridGame.Studio/tools/make_icons.py`, reusing already-cached silhouettes), not the orb fallback. *(Tests: `New_game_has_starting_trio_gold_and_loadouts`, `A_recorded_battle_round_trips_as_json_with_every_turn`; every new ability is also auto-exercised by `CatalogTests.Every_hero_ability_resolves_against_a_live_board`.)*

**Amended 2026-11-05 (GG-T285) — one shared bag:** the party's item slots draw their charges from **one** shared inventory, in party order (`BattleFactory.Build`). Before, every hero's spec got a full copy of the inventory, so 5 owned X-Potions put a stack of 5 on every bar (25 heals) and the overdraw was clamped away after the battle. A fresh game still stocks and carries 5 × 5.

`Progression.UnlockedSlots(save)`: **2** on a fresh save, +1 when `HighestClearedStage` reaches **0, 2 and 5** (`UnlockThresholds`), max 5. The battle reads it via `BattleSetup.UnlockedSlots`; the Abilities screen renders locked cells and `Abilities.Assign` refuses them. The post-battle screen announces "Ability slot unlocked!". *(Tests: `New_game_has_starting_trio_gold_and_loadouts`, `Full_loop_battle_commit_then_every_vendor`.)*

---

## 5. Targeting

Three orthogonal enums (`ContentModels.cs`):

```
TargetShape  : Self / SingleActor / SingleTile / Square / Diamond / Cross / Plus / Row / Column / AllEnemies / AllAllies
TargetMode   : Auto / PickActor / PickTile
TargetFilter : Any / EnemyOnly / AllyOnly / EmptyOnly
```

plus `int Radius`.

### 5.1 Resolver

`TargetResolver.Tiles(anchor, shape, radius)` → tiles (clipped to the board); `Collect(state, ability, caster, anchor)` → distinct living units on those tiles that pass the filter (or the whole team for All*, or the caster for Self).

```
Square(r=1)     Diamond(r=2)      Cross(r=1)      Plus             Row          Column
· · · · ·       · · █ · ·         · · · · ·       · · █ · ·        · · · · ·    · · █ · ·
· █ █ █ ·       · █ █ █ ·         · · █ · ·       · · █ · ·        █ █ ★ █ █    · · ★ · ·
· █ ★ █ ·       █ █ ★ █ █         · █ ★ █ ·       █ █ ★ █ █        · · · · ·    · · █ · ·
· █ █ █ ·       · █ █ █ ·         · · █ · ·       · · █ · ·
· · · · ·       · · █ · ·         · · · · ·       · · █ · ·
```

| Shape | Tiles (anchor `(ax,ay)`) |
|---|---|
| Self, SingleActor, SingleTile | the anchor |
| Square(r) | Chebyshev ≤ r |
| Diamond(r) | Manhattan ≤ r |
| Cross(r) | anchor + r tiles along each cardinal arm |
| Plus | the anchor's whole row ∪ whole column |
| Row / Column | the anchor's whole row / column |
| AllEnemies / AllAllies | no tiles; the caster's opposing / own team |

Filters: EnemyOnly = other team; AllyOnly = caster's team **including the caster**; Any = everyone; EmptyOnly = no units (used by Teleport). AoE shapes never hit units their filter excludes.

### 5.2 Picker flow

`BattleScreen.OnSlot` / `BattleScreen.Input`:
- **Auto** (Self, All*, Steal/Mug's Cross around the caster) → commits immediately with the caster's tile as anchor; refused "No target" if nothing would be hit.
- **PickActor / PickTile** → `BattleEngine.ValidTargets` marks every legal anchor gold; pressing a tile previews its area (red if harmful, green if helpful, blue for Teleport); releasing on a legal tile commits `UseSlotCommand`. Legal anchors (`TargetResolver.IsValidPick`): PickActor = an occupied tile whose unit passes the filter; PickTile = any tile whose shape catches ≥ 1 valid unit; EmptyOnly = an empty, trap-free tile.
- No legal anchor → banner "No valid target", nothing spent.
- **Drag preview numbers** (*added 2026-11-05, GG-T252*): while a held hero's path makes a pincer, every foe it would hit wears a small tag with the damage range and KO chance (`OutcomePreviewer`, three samples per step), cleared on drop or cancel.

### 5.3 Special targeting flows

- **Teleport** (`IsTeleport`): EmptyOnly PickTile → the caster moves there (`TeleportedEvent`), triggers any trap, then a board-wide hero pincer scan resolves. Cooldown 3.
- **Revive / Escape / orb items** resolve on the user without a pick ([§4.3](#43-item-stacks-and-effects)).

### 5.4 Targeting session lifecycle

```
tap slot ──▶ GetSlotStatus ≠ Ready ─▶ banner reason (no state change)
   │ Ready
   ├─ Auto ──────────────────────────────▶ Commit(UseSlotCommand)
   └─ Pick* ─▶ TargetingSlot = i; mark ValidTargets
                 │ press/drag over tiles ─▶ PreviewTarget(tile)
                 │ release on legal tile ─▶ Commit(UseSlotCommand) ─▶ CancelTargeting
                 │ release elsewhere ─────▶ error sfx, keep targeting
                 └ tap slot again / Back ─▶ CancelTargeting (nothing spent)
```

Invariant: while the event player is busy (`BattleScreen.Busy`) no command can be committed (`CanCommand`), so two uses can never overlap. A 20 s playback watchdog resyncs the view if an animation ever stalls.

---

## 6. The Spell Dispatcher

`BattleRunner.ResolveAbility(caster, ability, anchor, pickedUnit)` — the single resolution path for spells, skills, items and boss phase abilities. **Amended 2026-10-09:** it is pure simulation; VFX are presentation and run later from events. Order:

1. **Teleport** → relocate + trap + pincer scan, done.
2. **Escape** → `BattlePhase.Escaped`, done. **Revive** → revive first fallen hero, done. **GrantsOrbs** → mint orbs, done.
3. Collect targets; emit `AbilityResolvedEvent` (anchor, tiles, targets). No targets → "Missed!".
4. Per living target, in order:
   1. ~~**Scan** → `ScannedEvent` (stats, HP), mark the class Scanned + Seen.~~ Retired 2026-11-06 (owner, GG-T290): Bestiary knowledge is earned by defeats ([§25.11](#2511-bestiary)); `IsScan` and `ScannedEvent` are gone.
   2. **Cleanse** (`RemovesDebuffs`: all debuffs) / **cure** (`RemovesBuff`: one id).
   3. **Fire × Frozen / Wet**: a damaging Fire hit force-expires Frozen (its chain applies Wet), then strips Wet ("Steam!").
   4. **Steal** (`StealsMana`) roll → random orb or "Missed!"; **Mug** adds a fixed physical hit (`MugDamage` 10 × physical resistance × incoming multiplier).
   5. **Damage** (`BaseDamage > 0`) → `Formulas.SpellDamage` (Lightning on Wet ×1.5, note "Shock!"), × `BonusMultiplier` vs `BonusVsTag`; then **Lightning blind roll** 30% → Blinded.
   6. **Knockback** (`Knockback`, [§6.6](#66-knockback-added-2026-10-10-owner-direction)), then **Pull** (`Pull`) and **Swap** (`SwapsPlaces`) ([§6.7](#67-movement-timeline-and-drain-gg-t183)).
   7. **Heal** (`BaseHeal`). **Amended 2026-10-22 (GG-T183):** a heal only ever lands on the caster's side. On a foe it is a **drain** when the ability's filter is `EnemyOnly` (the caster is mended instead), and nothing when the filter is `Any`; and an ability that both harms and heals never harms a friend (before this, Soul Feast, Collect the Debt, Bloodletting and Blood Harvest healed the foe they hit, and Harm into Hope scorched allies and mended enemies).
   8. **Hasten** (`HastenU`), then **Pushback** (`Pushback` seconds, foes only; was `DelayU` before GG-T286, [§2.3](#23-pushback-a-keyword-in-seconds)).
   9. **Buff** (`ApplyBuff`, chance `ApplyBuffChance`): Sleep on a Warm target lasts ×1.5 (ceil). **Amended 2026-11-05 (GG-T266):** a *beneficial* buff (`BuffKind.Buff`) never lands on a foe: on a foe-targeting ability it is the **caster's own**, granted once per cast however many foes are caught (War Stomp and Battle Cry make the caster taunt, Opening Jab empowers the caster's next strike; before this, all three buffed the enemies they hit). *(Test: `A_beneficial_buff_on_a_foe_targeting_ability_lands_on_the_caster_not_the_foe`.)*
5. **Step back** (`StepBack`): the caster backs away from the nearest foe.
6. Battle-end check.

**Amended 2026-10-22 (GG-T183):** a teleport with `HastenU` (Fade Step) also hastens the caster when it lands.

`Damage()` is the single damage choke-point: armor (snake), Sleep-break, HP, threat, death + loot, then (non-miss, with a source) cast stagger ([§13](#13-combat-resolution)). **Amended 2026-11-06 (GG-T286):** `Damage()` no longer pushes; only the Pushback keyword and a hero pincer do ([§2.3](#23-pushback-a-keyword-in-seconds)).

### 6.1 Projectile motions

`AbilityDef.Motion`: `None / Straight / Bezier / Homing / Spiral / Twist / Strike`. **Amended 2026-10-09, corrected 2026-10-13 (was stale):** `BattleScreen.Playback.ProjectilePoint` gives every motion its own curve — `Bezier` arcs, `Homing` curls in late from the side, `Spiral` corkscrews in on a shrinking radius, `Twist` oscillates side to side, `Strike` drops from directly overhead — `Straight`/`None` is the only flat line, and Reduce Motion flattens everything to it.

### 6.2 VFX pipeline

**Amended 2026-11-05 — effect prefabs (owner, GG-T269: "Prefabs that played either once or in a loop; they all might need to be normalized"):** where `vfxstyle.json` names an effect, a premade particle prefab plays instead of the code-drawn burst. The prefabs are the owned GridGame2026 set (54 prefabs plus their materials, textures and meshes, imported with their GUIDs into `Resources/Vfx` and `Assets/GridGame/Vfx`, licence record `Assets/GridGame/Vfx/SOURCES.md`).
- **Catalog:** `vfx.json` (validated) lists every effect: `Mode` Once or Loop, `Tiles` (on-screen size of its 80th-percentile particle reach), `Extent` (authored size, measured by `GridGameBuild`-style batch tool `VfxImport.Prepare` into `tools/vfx-measure.json`), `Duration` (a Once effect stops emitting then fades), `Tint`, `Cue`, `Source`.
- **Mapping:** an element lists `TierPrefabs` (Minor/Major/Ultimate). A tier it leaves out, an unmapped element (Ice and Arcane today) and Reduce Motion all keep the code-drawn effect, which stays the fallback. A status names its own `Prefab`; it never inherits its element's impact. A Loop status prefab (burning → flame, wet → bubble) stays on its unit exactly while the unit stands with that status, reconciled every frame, so death, cleanse, expiry and wave end all stop it.
- **Rendering:** an orthographic effects camera renders layer 31 into a RenderTexture, one world unit per board pixel. A composite pass rebuilds alpha from brightness, because additive legacy particles write none. A stage-sized UI Toolkit overlay inside the board's FX layer shows it, following resizes (§26), so harness screenshots and MindAttic.Capture include it.
- **Time:** pause sets every live system's `simulationSpeed` to 0 (frozen mid-burst), and battle speed scales it. Instances are pooled.
- **Imported materials:** URP-only ones moved to the legacy particle shaders, renderers with no material got `GridGameDefaultParticle`, and the old scripts and audio sources were stripped.
- **Checked:** harness `vfxprefabs` films every catalog effect into a contact sheet (only effects that read well there are mapped), pause freezes them pixel-for-pixel, 60 fps with a dozen heavy effects live, and they stay aligned at 9:23 and 3:4.

**Amended 2026-10-11 — staging (owner: "there is a rhythm and tempo to gameplay that does require pauses and transitions … if I clicked poison and an icon just appeared on an enemy it would feel meaningless"):** every action plays as readable beats (`BattleScreen.Fx.cs`, `BattleScreen.CutIns.cs`):
1. **Wind-up:** motes spiral into the caster in the spell's element colour (`CastColor`: fire orange, poison green, heals green, status spells their status colour).
2. **Charging:** a channel halo pulses on the caster and motes orbit it; a **charging bar** fills on its tile (plus the existing ring) while the timeline glides. Hero casts queue and land inside one action, so playback tracks them as *cast ghosts* from `CastQueuedEvent` to the resolve.
3. **Cast splash:** the caster's action cut-in ([§9](#9-the-hud)) — a sequential beat, never covering the flight.
4. **Projectile:** a glowing orb with a white-hot core and a comet tail of embers on a curve.
5. **Impact:** a splash ring at every target (status-only spells included); area spells wash over their tiles as a soft bloom wave from the anchor.
6. **Status lands, then its icon:** each status has its own take-hold effect (bubbling poison, converging frost, falling droplets, rising Z's, slow spiral, blinding smoke, red taunt shockwave with threat lines, rising embers …); only after it does the pip pop in and the status-coloured text float.
7. **Linger:** while a status lasts the unit keeps showing it (poison bubbles, embers, frost glitter and blue tint, drips, snores, buff sparks); damage-over-time ticks play the element (poison bubbles), not generic sparks.
- **Items:** the user produces the bottle (rises out of the frame); a potion for self is uncorked, tipped and drunk (`glug`); for someone else, or a damage item, it spins on an arc and shatters (`glass`) into a splash.
- **Skills:** melee skills (Cleave, Shield Bash) lunge with a slash arc; self/party skills (Taunt, War Cry) rear up with a shockwave.
- The billboard steps to the foot of the board when the action is in the top two rows.
- Reduce Motion keeps the beats but drops particles, shakes and camera moves.

Earlier note (2026-10-09, superseded): **Amended 2026-10-09:** there are no particle prefabs, no `VisualEffectLibrary` and no Addressables. Feedback today is code-drawn in UI Toolkit: tile marks colored by damage type, the projectile dot, floating text, shakes, pops, glows and a full-screen flash for Clutch. The `CastVfx / ProjectileVfx / ImpactVfx / LingerVfx` keys remain in `abilities.json` for a future pass.

**Amended 2026-10-13:** damage and buff-tagged spells still read their look from `DamageType`/the applied buff id (`ElementBurst`, `StatusBurst`) — already rich and per-type (Fire/Ice/Lightning/Poison/Holy/Dark each have a bespoke burst, and poisoned/burning/frozen/sleep/slowed/silenced/blinded/taunting/empowered/protection/regen each have a bespoke take-hold animation). What used to fall through to one generic purple ring — Scan (retired 2026-11-06, GG-T290, with its `ScanBurst`), pure cleanse/dispel (`RemovesDebuffs`/`RemovesBuff`), and hasten-only spells (no damage, no heal, no buff) — now get their own bespoke gold bursts (`ScanBurst`/`CleanseBurst`/`HastenBurst`, `BattleScreen.Playback.cs`) and read gold during the cast wind-up too (`CastColor`). The named `CastVfx/ProjectileVfx/ImpactVfx/LingerVfx` strings on individual abilities (e.g. distinguishing Fireball from Meteor) are still not read by any code — still ⬜ for a future pass, still code-drawn UI Toolkit if it happens, never particle prefabs (law #3).

**Amended 2026-10-19 — death-portrait dissolve restored (owner: "the only way to see the complete artwork of these enemies... a quintessential reward for killing an enemy", GG-T96):** on `DiedEvent` the board token vanishes at once and a fresh single image of the unit's **full, uncropped portrait** (`Art.Portrait`, the same in-memory texture the token was framing through its Thumbnail ([§12.1.1](#1211-thumbnails-and-portraits-added-2026-11-03-gg-t220-reworked-2026-11-23-gg-t713)), shown whole, and no frame, HP bar, pips, glow or any other part of the `UnitView`) appears on the board's fx layer (below floating numbers and the coin burst, above every token, mark and line), centred on the fallen unit's footprint (2×2 bosses on their whole footprint) and nudged back inside the board at its edges so the whole art reads. It is sized to fit a box of (2 + footprint) tiles, starts at 196/255 opacity and, frame-rate independently and scaled by Battle speed, jitters up to ±1/12 tile around its start point, shrinks as ×0.99 per 60 fps frame and fades 0.6/s until gone (≈1.3 s), exactly GridGame2026's `PortraitManager.Dissolve` + `Portrait3DInstance.DissolveRoutine`. It is non-blocking: playback waits only a 0.12 s beat, so the loot burst and the next kill in a chain play while it dissolves, and several deaths each get their own. Every dying unit gets one (corpses show the fallen class's art; units with no portrait skip it). The death sfx and the "★ X is defeated" feed line are unchanged. Reduce Motion drops the jitter; the shrink and fade still play. (`BattleScreen.Playback.cs` `DeathAnim`/`DeathDissolve`.)

**Amended 2026-11-05 — one visual language (GG-T253, owner: "it doesn't look like a collection of random effects"):** the per-type bespoke bursts above are gone. `Resources/Content/vfxstyle.json` (`VfxStyleSheet`, validated by `ContentLoader.Validate`) is the one table every effect reads ([§12.3](#123-vfx)):
- **Elements** (every `DamageType`, plus the `Heal` and `Water` families): a palette (`Core`, `Glow`, `Accent`), a particle **shape** (spark, shard, ember, bubble, mote, smoke, rune, ray, glyph, droplet), a **motion** (burst, rise, fall, implode, spiral, pulse, contract, bolt, rays, drift, billow), a timing token, count, size, reach, an optional ring and the sound family (`Cue`). `Palette.Element` reads its hue from here.
- **Statuses** (every buff id) name the element they come from and inherit everything they leave empty: Burning reads as Fire, Frozen as Ice (converging, with a held frost tint), Wet as Water, Poisoned as Poison (with a passing green tint), Regen as Heal. The rest have their own look (Sleep's drifting "z" glyph, Slowed's spiral, Silenced's closing rune ring, Blinded's smoke, Protection's and Ward's pulse, Empowered's rising embers and swell). Taunt keeps its shockwave and threat lines (a readout of who is being pulled), drawn in its style's red. `LingerEvery` sets how often a lasting status sheds a wisp.
- **Routing:** `ElementBurst`, `StatusBurst`, `TickLinger`, `HealBurst`, the spell trail and the named `ImpactVfx` flourishes all draw through one emitter (`BattleScreen.VfxStyle.cs`, `Emit`). A named flourish family (Explosion, Cloud, Sparkle, Beam, Aura, Drift, Slash, Strike) is now only a *motion* played in the ability's own element style, so Fireball and Frost share a gesture but never a look.
- **Intensity tiers** (`Tiers`: Minor, Major, Ultimate) scale particle count, size, screen shake and hit-stop (0 / 40 / 90 ms). An ability's tier is its optional `Intensity`, else derived from its price (~~3+ orbs … 2 orbs~~ — GG-T373, on the 1–12 curve: 6+ orbs or a 5+ turn cooldown = Ultimate; 3–5 orbs or 3+ turns = Major; `VfxStyleSheet.TierOf`). A pincer's hits are Major; a crit, or a hit for a quarter of the target's HP, lands one tier higher.
- **Every hit is anticipation → impact → follow-through:** the target braces (an Instant squash), the element bursts while the frame holds for the tier's hit-stop, then the target recoils past rest and settles (`Ease.Back`). Durations come only from the timing tokens and easing only from the easing tokens (`Ease.Arc` replaces the hand-written `Sin(k·π)` curves); no duration is random.
- Reduce Motion halves particle counts and drops the anticipation, follow-through, hit-stop and shakes.
- **Prefab-ready (owner direction 2026-11-05):** every element style and every tier carries an optional `Prefab` id (null today; a status inherits its element's). A follow-up brings back real effect prefabs (GridGame2026's VisualEffects/Particles plus free/CC0 packs) on an effects-camera overlay: when set, the prefab replaces the code-drawn burst (a tier's `Prefab` overrides its element's), and the code-drawn burst stays the fallback. Nothing reads the field yet.
- **Contact sheets:** `python tools/harness.py vfxsheet [RUN] [LIMIT]` plays every non-passive ability's look (dev `devvfx` endpoint: cast, flight, impact at its tier, the status; state untouched) and saves a frame strip per ability (`artifacts/<run>/strips/`) and a grid per element (`artifacts/<run>/<Element>.png`).
*(Tests: `VfxStyleTests`.)*

### 6.3 Edge cases the dispatcher handles

| Case | Handling |
|---|---|
| Target dies before its turn in the target list | skipped (`!t.Alive`) |
| PickActor cast whose target moved | anchor follows the unit's current tile at resolution |
| PickActor cast whose target died | resolves at the original tile (usually "Missed!") |
| Caster dies before its cast resolves | cast removed with its owner (`Kill` clears the owner's entries) |
| AoE overlapping the same unit twice | `Collect` de-duplicates units (a 2×2 boss is hit once) |
| Fire on Wet | Wet stripped before damage, so no Lightning ×1.5 from that Wet later |
| Lightning on Wet | ×1.5, Wet stays, then the 30% blind roll |
| Steal on any target | success always mints (stolen "from the world") |
| Battle ends mid-list | the loop stops (`S.IsOver`) |

---

### 6.6 Knockback (added 2026-10-10, owner direction)

Owner: "certain abilities would have pushbacks like a shield bash that pushes an enemy away and it hurts more if it runs into another enemy, and hurts a lot if it runs into a solid tile."

`AbilityDef.Knockback` = tiles the target is shoved straight away from the caster (dominant axis); `AdjacentOnly` limits the pick to a cardinally adjacent foe. Per tile: into stone or the board edge → **Slam** (`18 + blow` to the target, stop); into another unit → **Crash** (`8 + ½ blow` to the target and, if it's a foe, to the unit hit; stop); into a movable tile → it is pushed ahead if there's room, else a crash; otherwise the target slides (`DisplacedEvent`). 2×2 bosses and snake segments don't budge. **Shield Bash** (Skill, cooldown 2, 12 physical, knockback 2, adjacent only) is learnable by Paladin, Knight and Defender and sits in the Paladin's third bar slot. *(Tests: `Shield_bash_shoves_the_target_two_tiles_away`, `Bashing_into_a_unit_crashes_both_and_into_stone_slams_hard`, `Shield_bash_needs_an_adjacent_target`.)*

### 6.7 Movement, timeline and drain (GG-T183)

Added 2026-10-22 so abilities can differ in more than damage and status ([§7.1](#71-reskin-audit-gg-t183)). Four `AbilityDef` fields, each resolved per target by the dispatcher above:

| Field | Mechanic | Rule | Event |
|---|---|---|---|
| `Pull` (tiles) | movement | The target is dragged toward the caster one tile at a time along the longer axis. It stops beside the caster, or before any unit, block, movable or board edge. A hero dragged onto a trap springs it. | `DisplacedEvent` per tile |
| `SwapsPlaces` | movement | The caster and the target trade tiles. Both are one-tile units, so the board stays one actor per tile ([§1.1.3](#113-one-actor-per-tile--the-occupancy-guarantee-added-2026-10-19-owner-direction-gg-t97)). A hero moved onto a trap springs it. | two `DisplacedEvent`s |
| `StepBack` (tiles) | movement | After every target resolves, the caster backs straight away from the nearest foe onto free, trap-free tiles. | `DisplacedEvent` per tile |
| `Pushback` (seconds) | timeline | **Amended 2026-11-06 (GG-T286; was `DelayU`, a bar fraction):** the Pushback keyword ([§2.3](#23-pushback-a-keyword-in-seconds)). Each foe it reaches acts that many seconds later, wherever its icon sits; at most `PushbackMaxSeconds` 2 s (validated at load) and one push per foe per action. A target holding a cast has the cast slip by the same seconds instead; it never cancels the cast. Never lands on a friend. | `PushedBackEvent` / `CastStaggeredEvent` |

2×2 bosses, snake segments and remains don't budge ("Won't budge"). None of the moves is a hero step, so none of them forms a pincer: movement never deals damage ([GG-LAW-1](#GG-LAW-1)). *(Tests: `AbilityAuditTests`: `Pull_reels_a_foe_in_toward_the_caster`, `Pull_stops_beside_the_caster`, `Swap_trades_tiles_with_the_target`, `Step_back_puts_distance_between_the_caster_and_the_nearest_foe`, `Pushback_keyword_slides_the_foes_turn_icon_back`, `A_heal_aimed_at_a_foe_drains_into_the_caster`, `Harm_and_heal_in_one_burst_hurts_only_foes_and_mends_only_friends`.)*

## 7. The Spell Catalog

All entries in `abilities.json`. Every spell has base cast time **1.5 s** (scaled by WIS+INT, [§13.1.4](#1314-cast-time)). Costs follow the 1–12 orb curve of [§3.1.1](#311-costs) (GG-T373).

| Spell | Cost | Shape / Mode / Filter | Motion | Effect | Role |
|---|---|---|---|---|---|
| **Heal** | 1 | SingleActor / PickActor / AllyOnly | None | +25 HP | spot heal |
| **Mass Heal** | 5 | AllAllies / Auto / AllyOnly | None | +12 HP each | emergency top-up (cost 1 → 2, GG-T247: a party-wide heal for one orb; 2 → 5, GG-T373) |
| **Antidote** | 1 | SingleActor / PickActor / AllyOnly | None | removes all debuffs | cleanse |
| ~~**Scan**~~ | — | — | — | **Retired 2026-11-06 (owner, GG-T290):** Bestiary knowledge comes from defeats ([§25.11](#2511-bestiary)). Save migration drops Scan from every hero's Known list and bar; in class kits healers got Quicken instead, supports Silence, tanks Cleave | — |
| **Fireball** | 3 | SingleActor / PickActor / EnemyOnly | Twist | 18 Fire + Burning | burst + DOT |
| **Frost** | 7 | Square(1) / PickTile / EnemyOnly | Bezier | 10 Ice + Frozen | AoE control |
| **Bolt** | 4 | Row / PickActor / EnemyOnly | Strike | 14 Lightning (×1.5 Wet), 30% Blind | row clear |
| **Poison** | 3 | Cross(1) / PickTile / EnemyOnly | Bezier | 6 Poison + Poisoned | long tail |
| **Sleep** | 1 | SingleActor / PickActor / EnemyOnly | Homing | Asleep (breaks on damage/move) | hard CC |
| **Slow** | 2 | Row / PickActor / EnemyOnly | Bezier | Slowed (icon ×0.5) | row tempo |
| **Quicken** | 2 | SingleActor / PickActor / Any | Straight | +0.30 u (or rush a cast) + Empowered (GG-T263: heroes have no turn icons, so haste alone was inert; GG-T268) | tempo |
| **Silence** | 1 | SingleActor / PickActor / EnemyOnly | Straight | Silenced | see note |
| **Meteor** | 10 | Diamond(2) / PickTile / EnemyOnly | Strike | 22 Fire + Burning | big AoE |
| **ShockWave** | 3 | Column / PickActor / EnemyOnly | Strike | 10 Lightning | column cleave |
| **CrossHit** | 3 | Plus / PickTile / EnemyOnly | Strike | 8 Lightning | row + column |
| **Steal** (Skill, CD 3) | free | Cross(1) / Auto / EnemyOnly | Homing | per adjacent enemy: LCK+AGI roll → orb | resource grab |
| **Mug** (Skill, CD 2) | free | Cross(1) / Auto / EnemyOnly | Straight | Steal roll + 10 Physical — see note | steal + hit |
| **Teleport** (Skill, CD 3) | free | SingleTile / PickTile / EmptyOnly | None | relocate + pincer scan | mobility |
| **Curse Link** (Hexer, GG-T873) | 2 | SingleActor / PickActor / EnemyOnly | Twist | [Linked] on the picked foe **and its farthest living teammate** (ties: more HP, then lower id; `CurseLink.Partner`) for 2 turns: damage dealt to one echoes `BattleConfig.LinkEchoFraction` 50% (min 1) to the other as an "Echo" hit of the same element from the same source (`LinkEchoEvent`, then its `DamagedEvent`); the echo never echoes back. Fizzles on a lone foe. The view draws a violet tether on the link (`UnitsLinkedEvent`) and pulses it on each echo. HeroBot links a foe a hero already stands beside (a pincer target) to one no hero is within 2 tiles of (`CurseLink.Value`) *(Tests: `CurseLinkTests`)* | hit the boss behind its adds, or a spread-out pair, with one pincer |
| **Buried Charge** (Engineer Skill `engineer_buried_charge`, CD 3, INT 4, GG-T876) | free | SingleTile / PickTile / EmptyOnly | Bezier | buries a `buriedmine` ([§14.23](#1423-tactical-tiles-added-2026-11-24-battle-feel-epic-gg-t823-gg-t842)): seen by all for 2 s, then only the party sees it (faintly); whoever steps on it (a hero too) pops it out and it blows 1.5 s later, 14 Fire on its tile and neighbours | ambush |
| **Redirect Flow** (Monk Skill `monk_redirectflow`, CD 3, WIS 3 AGI 2, adjacent, GG-T887) | free | SingleActor / PickActor / AllyOnly | None | every debuff on the picked adjacent ally (but a [Linked] half) leaves it and lands, **with its remaining duration**, on the foe beside the Monk with the most HP (ties: lower id; `RedirectFlow.Receiver`), applied by the Monk so the foe's immunities and wards still hold (`AbilityDef.RedirectsDebuffs`, `DebuffsRedirectedEvent`, then a BuffRemoved "redirected" and a BuffApplied per status). Not a legal pick without a debuff on the ally and a foe beside the Monk. The view runs a jade chi stream from ally to foe with one bead per status in its own colour and a "Redirect" callout; each status gets its CombatText as it leaves (struck through) and lands (debuff chevron). HeroBot uses it only when a moved debuff is damaging or disabling (DOT, halt, hold, root, silence, fear, Slow, blind; `RedirectFlow.Value`) *(Tests: `RedirectFlowTests`)* | a cleanse that also hurts: pour a Frozen or Poisoned ally's ailment into the foe on it |
| **Barricade** (Machinist Skill `machinist_barricade`, CD 3, INT 4, GG-T934) | free | SingleTile / PickTile / EmptyOnly | Bezier | raises a `machinist_barricade` ([§14.23](#1423-tactical-tiles-added-2026-11-24-battle-feel-epic-gg-t823-gg-t842)) for 20 s: nothing walks through it, a line charge stops at it ("Blocked!") and spares whoever is behind; each foe ending its turn beside it, and each charge into it, is one of the 3 hits that knock it down | wall off a flank, shield a hero from a charge |
| **Firewall** (Red Mage Spell `firewall`, CD 2, INT 6, GG-T931) | 2 | Wall(1) / PickTile / EmptyOnly | Straight | raises a `firewall` ([§14.23](#1423-tactical-tiles-added-2026-11-24-battle-feel-epic-gg-t823-gg-t842)) three tiles wide across the caster's line to the picked tile, for 12 s: whoever steps in (a hero too) takes 6 Fire and Burning, ending a turn in it 4 Fire and Burning; Ice douses it | area denial |
| **Time Bomb** (Operative Spell `operative_timebomb`, CD 3, INT 5, GG-T874) | 2 | SingleActor / PickActor / EnemyOnly | Bezier | plants a bomb on the picked foe: a queued `timebomb` rule event (regionrules.json rule `timebombs`, 4 s of clock, its own timeline icon) whose `Schedule` has `Follow`, so it rides its carrier (`TimelineEntry.CarrierId`): its telegraphed tiles are the carrier's footprint and every tile beside it (Radius 1) and move with it however it moves (`HazardMovedEvent`; walked, shoved, dragged), re-read each clock tick, on each step and as it lands. It blows for 16 Fire on everyone on those tiles, the carrier and **heroes included**, so shove the carrier into its friends first; a carrier that falls drops it where it fell (`Dropped`). HeroBot adds `TimeBombValue`: 8 for the first foe beside the target, 4 each further, 3 for each a shove away (Manhattan 2), minus 4 per hero beside it; a lone foe or one already carrying a bomb is worth nothing. The view ticks its seconds left over the carrier as a ⏲ Timer countdown (`CombatText.Countdown`) with a fuse tick, as it is planted and at each of the party's windows, redraws its tiles as they move, and the blast plays an explosion with a fire burst over every tile and a shake *(Tests: `TimeBombTests`)* | crowd blast |

Enemy-only entries: `charge_fireball` (Fire), `charge_ice` (Ice), `charge_thunder` (Lightning), `charge_smite` (Holy) — 2.5 s charges with base 0 (damage comes from the INT-vs-WIS roll, [§13.4](#134-the-interrupt-path)); `enrage` (self Hasten 0.35, Cyclops phase 2). **Amended 2026-11-05 (GG-T283):** an enemy charge with `CooldownTurns` > 0 rests that many of the caster's own turns (counted at each turn start) before it can be charged again; `charge_smite` has 2, so a templar charges every other turn and closes in between. **Amended 2026-11-21 (GG-T566):** the count ran down at turn start, before the charge was planned, so `CooldownTurns` N rested only N−1 turns (1 did nothing). It now counts down **after** each of the caster's turns (`BattleRunner.RunEnemyTurn`; a cooldown set during the turn doesn't tick that turn), so N rests N full turns: `charge_smite`'s 2 means a templar charges, rests two turns, then may charge again.

**Passives and Reactives** (`Kind: "Passive"` / `"Reactive"`, active from a class's `Abilities`, a learned skill-tree node or an armor enchant; never on the bar, never bought). **Amended 2026-11-06 (GG-T296):** each is now a `Trigger` row on the engine of [§4.1](#41-the-three-kinds); no passive has its own code.

| Id | Kind | Trigger → effect |
|---|---|---|
| `double_attack` | Passive | OnAttack → FollowUp ×0.5 (a pincer attacker or a melee swing strikes the same foe again) |
| `counter_attack` | Reactive | OnAttackedDirect → Counterstrike ×0.5, callout "Counter!" |
| `lifesteal` | Passive | OnLandedHit → Lifesteal 25% of the damage |
| `tough_hide` | Passive | Always, physical damage → ReductionMultiplier 0.7 |
| `berserk` | Passive | Always, below 50% HP → DamageMultiplier 1.35 |
| `regenerate` | Passive | TurnEnd → Heal 6% max HP |
| `poison_ward` | Passive | Always → Immunity: poisoned ("Warded!") |
| `steadfast` | Passive | Always → Immunity: frozen, sleep |
| `thorns` | Reactive | OnAttackedAny → Reflect 25% of the damage |
| `mana_siphon` | Passive | OnLandedHit → ManaCharge 5 |

**GG-T302** adds 44 more rows (hero signatures and enemy family passives, listed in [§4.1.2](#412-signature-passives-gg-t302-added-2026-11-06)). The three proficiency passives carry no trigger; they only gate equipment ([§24.2.1](#2421-proficiency-gating-gg-t52)). Every one now works for heroes and enemies alike: a hero who learns Tough Hide, Lifesteal or Double Attack from a skill tree gets it in battle. Before, those were enemy-only code paths, and learned passives never reached the battle. *(Tests: `TriggerEngineTests`, `Double_attack_passive_strikes_twice_and_counter_strikes_back`.)*

**Notes (current truth, Amended 2026-10-09):**
- **Mug** = Steal's roll + its row's `BaseDamage` 10 Physical, one hit per target (fixed 2026-10-09; `Mug_steals_and_hits_exactly_once_per_target`).
- **Silence** blocks hero spell slots and stops a silenced enemy from starting a charge (`EnemyPlanner.PlanCast`; `Silenced_caster_does_not_start_a_charge`).
- Spell damage never misses or crits ([§13.1.2](#1312-spell-damage-dispatcher)).

### 7.1 Reskin audit (GG-T183)

Owner direction (2026-10-21, epic W): there should be **at least 100 hero abilities that differ in mechanic, each learnable at the Learn screen**. A reskin is an ability with the same shape and effect as another under a different name.

**How "mechanic" is measured.** `Abilities.MechanicKey` (`Meta/MetaServices.cs`) removes the name, the numbers (damage, heal, cost, cooldown, cast time, hasten/delay amount, buff chance) and the art. What remains is the key:
- **Payment and timing:** a skill on a cooldown, a spell cast on the timeline, or an instant spell.
- **Footprint:** shape (with its radius for Square/Diamond/Cross), auto or pick mode, filter, and whether it is adjacent-only. Mode and filter are ignored for Self and All* shapes, where they change nothing.
- **Effects:** damage, with Fire and Lightning counted apart because they thaw/steam and shock/blind, while every other element only meets a different resistance. Then heal or drain, the status it applies, cleanse/cure, steal, orbs, scan, revive, escape, bonus-vs-tag, movement (teleport, knockback, pull, swap, step back) and timeline (hasten, delay).

**Learnable** = `Abilities.LearnableByAnyHero`: a hero-usable Spell or Skill (never a passive, an enemy charge or `enrage`) that is a spell, a universal skill, or on a hero class's `Abilities` list. That is the union of what `Abilities.Learnable` offers across all hero classes.

**Before (197 rows):** 169 learnable hero abilities, **95 distinct keys**. Five of them did nothing when they resolved (only 92 of the 95 keys had an effect at all), and seven more were wired wrong:
- **No-ops:** `sprint`, `shake_it_off`, `tempoweave_shortcut`, `tempoweave_swap`, `shadowstep_fade_step` (a "teleport" that wasn't one).
- **`disengage`:** a self knockback, which the dispatcher skips.
- **Drains that healed the foe:** `attrition_soulfeast`, `opportunist_collect_the_debt`, `attrition_bloodletting`, `hexweaver_bloodharvest`.
- **`hexmenders_harmintohope`:** burned allies and healed enemies.
- **Unlearnable:** nine ranger skills (`marksman_*`, `trapper_*`, `skirmisher_covering_volley/retreat_shot/rapid_barrage`) were only granted by a skill-tree node, so no Learn screen offered them.

The biggest reskin family was the plain adjacent strike: 21 rows (Cleave, Killing Blow, Iaijutsu, Flurry, Riposte, Fleche, Knockout Punch, Wild Swing, Hit and Run…).

**What GG-T183 changed:**

| Change | Abilities |
|---|---|
| No-ops made real, using the mechanic each one's text already promised | Sprint (self hasten), Shake It Off (self cleanse), Shortcut (instant ally hasten), Swift Swap (swap places with an ally + cleanse), Fade Step (teleport + hasten), Disengage (step back 2) |
| Text the data didn't do | Followthrough now hastens as it says; Cleansing Vigil now cleanses as it says |
| Drain / harm-and-heal fixed in the dispatcher ([§6](#6-the-spell-dispatcher)) | Soul Feast, Collect the Debt, Bloodletting, Blood Harvest, Harm into Hope |
| Reskins given the mechanic their name describes | Fleche (lunge through: swap with the foe), Riposte (+ delay), Hit and Run (+ step back), Wild Swing (all 8 tiles around you), Knockout Punch (50% sleep), Retreat Shot (+ step back), Covering Volley (column + delay) |
| Skill-tree-only ranger skills put on the ranger class lists | Hexsniper: Deadeye Shot, Piercing Volley, Execution Shot, Covering Volley, Retreat Shot, Rapid Barrage. Hexwarden: Binding Shot, Pitfall Volley, Snare Net |
| **12 new abilities** | Spells, which every hero can learn: **Hinder** (delay 0.3), **Stasis** (all foes, delay 0.12), **Haste Field** (all allies, hasten), **Drain Life** (cast drain), **Undertow** (column, Ice + Wet + pull 2), **Drench** (instant 3×3 Wet, setting up Lightning ×1.5), **Flame Lance** (row, Fire + burning). Class skills: **Grappling Hook** (pull 3: Lancer, Raider, Panda Brawler, Hexwarden), **Meditate** (+1 orb: Monk, Sage, Martial Artist, White Ninja), **Shove** (adjacent knockback, no blade: Defender, Knight, Tank, Myrmidon), **Disrupt** (silence + delay: Sage, Operative, Drifter, Harbinger), **Pinning Shot** (ranged hit + delay: Hexsniper, Hexwarden) |

**After (209 rows):** 190 learnable hero abilities, **120 distinct mechanics**, and none of them is a no-op. The remaining reskin families are left on purpose, because each class keeps its own flavour of a staple move. The largest are the plain adjacent strike (16), adjacent strike + slowed (8), adjacent strike + knockback (8), adjacent strike + blinded (6), adjacent strike + poisoned (5), ranged strike (5) and self-empower (4). *(Tests: `AbilityAuditTests`: `At_least_100_learnable_hero_abilities_differ_in_mechanic` prints every remaining family, `Every_learnable_hero_ability_does_something`, `Every_counted_ability_is_offered_at_some_heros_Learn_screen`, `Reskins_share_a_key_and_new_mechanics_do_not`; `CatalogTests.Every_hero_ability_resolves_against_a_live_board` runs all 12 new rows through the engine.)*

---

### 7.2 Usefulness pass (GG-T263, GG-T264, 2026-11-05)

POLISH G4/G5 ("every ability earns its slot", "no dead or dominant abilities") read from `records.py --abilities` over the `polish.py --balance` soaks. Measured at 12 casual battles per stage plus the greedy class sweep (206 battles); the 4-per-stage default swung a stage ±25 points with party make-up alone.

- **What was wrong:**
  - **Cleave crowded out every finisher.** It is the striker baseline in every striker's slot 0, and at 24 damage on a 2-turn cooldown it beat the bespoke 8–16-damage adjacent finishers. 27 abilities never fired.
  - **`protection`** (×0.85 for 1 Turn) was too light to spend an action on.
  - **Several spells cost more than they did.**
  - **Hero haste is inert under the held turn** (heroes have no turn icons, §2.0; GG-T268).
- **Changes:**
  - **Cleave** 24 → 20.
  - **Plain finishers now out-hit it on their longer cooldowns:** Combo/Prism Fang 20, Jab-Cross 22, Contract 20, Flurry 18, Perfect Cut 24, Iaijutsu 26, Shock/Flame Fang 18, Finishing Blow 20, Reckless 24.
  - **Finishers with a rider move closer to Cleave:** blind, silence, delay, steal, knockback and swap finishers go to 12–18.
  - **Spells:** Shadow Bolt, Shock, Bloodrite and Swap cost 2 → 1. Mirror Curse 10, Overdrive 12, Frost Nova 14 and Fury 20 damage.
  - **Support:** Moonlit 22 heal. Transmute and Jury-rig also mend 10, and Howl and Insight deal 6 and 8.
  - **Haste-only tempo skills** (Quicken, Shortcut, Relay, Blitz) also empower their target.
  - **New `ward` buff** (×0.70 for 2 Turns) for every hero guard skill; Guard keeps `protection`.
  - **GG-T264 trims:** Blood Harvest is re-costed 3 → 2 but drains 7/10 (at 10/14 it measured 73 impact/use against a Spell median of 28), and `empowered` lasts 3 Turns.
- **Impact metric (GG-T264):** `records.impact` used to credit every buff, debuff and tempo effect a flat 6. It now credits what each one did after it landed (`records.credit_utility`), in HP:
  - **damage-up:** the extra share of the buffed ally's damage
  - **guard or taunt:** damage prevented, plus every hit a taunter absorbed
  - **ticks:** damage and heal ticks at face value
  - **frozen or sleep:** a skipped enemy turn counts as one enemy turn's damage
  - **slow:** the enemy turns it took away
  - **blind:** expected misses
  - **silence:** half an enemy turn per turn
- **Result (same seeds, before → after):** G 69.3 → 74.9.
  - G1 50.0 → 66.7, G4 59.7 → 73.4, G5 74.7 → 78.0, G7 61.5 → 76.9, G3 100 → 100.
  - G2 85.7 → 78.6: swamp-02's lone-hag wave deals about 1000 damage to a casual party and tips that stage, a stage-tuning matter.
    **Amended 2026-11-05 (GG-T274):** Arkgrave II's wave 2 is now a frog and a marsh shambler instead of the lone kiting hag (hags stay in Arkgrave III's finale), and its Naga closer (used on no other stage) drops to Strength 10 / Vitality 10 (was 15 / 12). Casual wins 3/12 → 9/12.
  - G6 53.5 → 50.4.
  - Remaining G5 lows are pure utility buffs (taunts, wards, empowers), which do little against 45%-HP foes. Trapline is high (78) because its row Slow takes real turns away.

## 8. Buffs and Debuffs

`BuffDef` (`buffs.json`) + `BuffInstance` (`Remaining`, `TickAccumulator`) on `Unit.Buffs`, applied and removed only by `BattleRunner.ApplyBuff / RemoveBuffsWhere / Expire`.

### 8.1 Catalog

| Id | Kind | Duration | Knobs | On expire |
|---|---|---|---|---|
| `protection` | Buff | 1 Turn | incoming damage ×0.85 — Guard's shield and the enemy rally | — |
| `ward` | Buff | 2 Turns | incoming damage ×0.70 — every hero guard/ward skill (GG-T263; it had used `protection`, too light to be worth an action) | — |
| `empowered` | Buff | 3 Turns | outgoing damage ×1.3 (GG-T264: 2 → 3 Turns; at 2 it mostly ran out before the buffed hero struck again) | — |
| `burning` | Debuff | 5 Ticks | 4 dmg/tick | → `warm` |
| `frozen` | Debuff | 1 Turn | immobile, halts timeline | → `wet` |
| `wet` | Debuff | 6 Ticks | Lightning ×1.5; Fire strips it | — |
| `warm` | Debuff | 3 Ticks | Sleep ×1.5 duration | — |
| `sleep` ("Asleep") | Debuff | 3 Turns | immobile, breaks on damage / move | — |
| `poisoned` | Debuff | 6 Ticks | 3 dmg/tick | — |
| `slowed` | Debuff | 2 Turns | timeline rate ×0.5; on a hero, party Tempo −1 ([§2.11](#211-party-tempo-gg-t268)) | — |
| `silenced` | Debuff | 2 Turns | blocks spell slots | — |
| `linked` | Debuff | 2 Turns | `Links`: always lands as a pair (Curse Link), each half naming the other (`BuffInstance.LinkedTo`); while both hold it, damage dealt to either echoes 50% (min 1) to the other, never back. Relinking either foe breaks its old link; when one falls the other's half goes too (GG-T873) | — |
| `blinded` | Debuff | 2 Turns | hit chance ×0.5 | — |
| `rooted` | Debuff | 2 Turns | `Roots`: held to its tile — no step, leap or blink, and no drag, shove, knockback, pull, swap or rule move shifts it (StepBlocked "rooted"); still acts, strikes and casts; planners and the hero bot never count on moving it (GG-T856, GG-T912, GG-T920) | — |
| `shredded` | Debuff | 3 Turns | **stacks to 3**; each stack +10% incoming **Physical** damage (`PhysicalDamageTakenBonus` 0.1, summed: 3 stacks ×1.3; other elements unaffected, `Formulas.TakenMultiplier`); each application chips the bearer's armor plate (§13.5) by `PlateChip` 4 **whatever side it faces** (`PlateHitEvent`, breaks it at 0) — GG-T857, owner: "shred armor" *(Tests: `KeywordStatusTests`)* | — |

### 8.2 Cross-effect multipliers

`BattleConfig.LightningWhenWetMultiplier` = 1.5, `SleepWhenWarmMultiplier` = 1.5, `LightningBlindChance` = 0.30.

#### 8.2.1 Interaction matrix

| On target ↓ \ hit → | **Fire** | **Ice** | **Lightning** | **Physical / other** |
|---|---|---|---|---|
| (clean) | damage + Burning (Fireball/Meteor) | damage + Frozen (Frost) | damage; 30% Blinded | damage |
| **Burning** | refresh Burning | Frozen applies; Burning keeps ticking | normal | normal |
| **Frozen** | **thaw**: Frozen expires → Wet → Wet steamed off; damage | refresh Frozen | normal; still halted | damage; still halted |
| **Wet** | **"Steam!"** Wet stripped; damage | Frozen (on expire refreshes Wet) | **×1.5 "Shock!"**; Wet stays | normal |
| **Warm** | Burning | Frozen | normal | normal |
| **Asleep** | damage **wakes** | damage wakes | damage wakes | damage wakes (a miss does not) |
| **Protection** | ×0.85 | ×0.85 | ×0.85 | ×0.85 |

**Amended 2026-10-09:** Fire on a Frozen target now nets a clean target (thaw + steam) in one hit.

#### 8.2.2 Expire chains

`BuffDef.OnExpireApply`: Burning → Warm, Frozen → Wet. A chain fires on natural expiry and on `ForceExpire` (Fire thaw) — never on cleanse or death.

#### 8.2.3 Damage- and move-breaks

`BreaksOnDamage` (Sleep): removed by any non-miss hit with amount > 0, before HP changes. `BreaksOnMove` (Sleep): removed when the bearer steps, is displaced by a slide, or is shoved by a boss.

### 8.3 Ticking

- **Tick-unit buffs** (Burning, Wet, Warm, Poisoned): one duration step — and one damage tick for DOTs — per `BuffTickInterval` = 10 ticks (1.0 s) of game clock, i.e. **2 steps per action**. DOT damage is environmental (no pushback, no stagger) and can kill (loot still drops).
- **Turn-unit buffs** — **Amended 2026-10-09:**
  - **Enemies:** non-halting turn buffs tick at the **end of the bearer's own turn** (so "2 Turns" = two of its turns).
  - **Enemies, timeline-halting (Frozen):** tick at the **shared turn boundary** — each control return — because a halted icon never reaches its own turn.
  - **Heroes:** every turn buff ticks at each control return, i.e. once per committed action.
- Consequence (current truth, tuning watch): Frost (1.5 s cast) lands mid-advance and Frozen (1 Turn) expires at that action's control return — the freeze holds only for the remainder of the action in which it lands. *(Test: `Frozen_halts_the_enemy_icon_and_expires_into_wet`.)*

### 8.4 Immobility hook

`Unit.IsImmobile` (Frozen, Sleep): a hero can't move or use slots (CannotAct); an enemy skips its turn (`TurnSkippedEvent` "asleep"/"immobile", its end-of-turn ticks still run) and plans no step or charge. `Unit.IsTimelineHalted` (Frozen) freezes its turn and cast icons.

### 8.5 Debuff icons (per actor)

**Amended 2026-10-10 (owner): one status slot per actor.** `UnitView.SetBuffs` shows a single letter pip on the tile (colour `BuffDef.Color`, radial ring = time left) that **flashes through every buff, debuff and pushback stun** on the shared 1.2 s beat, with a small count when there are several. The selected actor's card lists each one with its pip, name and a plain-language effect built from its mechanics (`Ui.BuffEffect`: "4 damage each tick", "can't move or act, timeline halted", …) and time left, so the player learns what each pip means. A unit that isn't moving also shows the badge on its timeline circle.

### 8.6 Stacking rules

`BuffDef.Stacking`: **Refresh** (default — re-applying replaces the old instance at full duration), **Ignore**, **Upgrade** (keep the longer), or **Stack** (each stack ticks and applies separately, up to `MaxStacks`; at the cap the oldest stack is replaced). **Burning stacks to 3** (amended 2026-10-10, owner: "fire has multiple stacks"). Damage-over-time ticks on the battle clock, which only advances when the player commits an action or Guards — thinking time never costs HP, so "wait and decide" stays free while every move under fire costs a tick of burn. *(Test: `Burning_stacks_up_to_its_cap_and_each_stack_burns`.)*

### 8.7 Buff lifecycle

Source (spell, item, charge, trap, expire chain, Shield) → `ApplyBuff` (refresh, `BuffAppliedEvent`) → ticks per [§8.3](#83-ticking) (`BuffTickedEvent`) or breaks per [§8.2.3](#823-damage--and-move-breaks) → `Remaining ≤ 0` → `Expire` (`BuffRemovedEvent` "expired") → `OnExpireApply` chain.

### 8.8 Mass-cleanse rules

- **Antidote spell / Remedy item**: remove all debuffs (no chains); buffs (Protection) stay. Antidote/Eye Drops items cure one id.
- **Death**: all buffs dropped silently.
- **Between battles**: buffs live on battle units only; nothing carries over.

---

## 9. The HUD

**Amended 2026-11-17 (GG-T405, GG-T408; owner: "a single mana pool on the far right ... a glass sphere filled with blue liquid ... like Diablo or PoE2 ... it should be obvious what it is and what is filling it"):** the ability row is 5 slots plus the **mana globe** (`ManaGlobe`), sharing the row evenly; the 12-orb row is gone. The globe is a bezelled glass sphere filled with blue liquid to count/cap, the count on it, MANA and the cap beneath; the charge toward the next orb (§3.1.9) is the ring on its bezel. Every orb flies in from its source (the GUARD button, the drag timer, a unit's tile) with a tag ("+1 Pincer", "+1 Guard") and a Log line; Guard shows its turn time pouring in and its charge as a share of the next orb ("Guard: 3.1s → 31% of an orb"); spends drain toward the caster; an aimed spell shows a "-N" ghost. Holding the globe explains the pool and every way it fills. The info-tab strip is 60 px (each tab a third of the column, so POL-C2's 132 px minimum does not apply). The banner band runs from the timeline frame + 8 px to the board - 8 px; banner, billboard and drag timer stay inside it, stepping down a size, then wrapping, then scrolling.

**Amended 2026-10-09:** the battle HUD is built in code by `BattleScreen.Build` on the 1170×2532 design stage (absolute design-pixel layout; the old `HudLayout` row constants are gone).

**Hold to read** (*added 2026-11-05, GG-T252, POL-D1 recognition over recall*): holding anything for `InspectCard.HoldSeconds` (0.45 s) opens one floating **InspectCard** (`App/UI/InspectCard.cs`) anchored above the finger (below when there's no room), clamped inside the stage and safe area, eased in over `Timing.Quick` with `ui.select`; releasing anywhere closes it, and the press that opened it never also acts. It works on: ability-bar slots (kind, cost pips, cooldown, reach, damage/heal, buff and its chance, movement effects, description, and why it can't be used now), units on the board (level, HP and stats or ??? until the Bestiary knows them — `BestiaryKnowledge.Tier`, GG-T290, [§25.11](#2511-bestiary) — when it acts, a charging foe's **whole spell**, its brain, plate, every status), status pips in the actor panel, **timeline icons** and the prominence diamond (turn icons → the unit; cast icons → the spell and how to push it back; a *tap* instead selects the owner, GG-T288, [§2.6](#26-one-track-diamond-icons-the-prominence-diamond)), and in menus: the Abilities screen's bar cells and Known/Learn rows, every `ItemUi.Row` (shop, equip, items — the full item detail), talent nodes, and the Bestiary's ability chips. The ability bar's five slots share the stage width with even 16-px gutters (they grow on a wide stage instead of the gaps).

**Tap to read** (*added 2026-11-19, owner, GG-T450: "you need the tap-tooltip system so you can read about things you tap once on like a hero or enemy unit or a timeline diamond or an ability"*): the same InspectCard also opens on a plain **tap** (`InspectCard.Tap`), after whatever the tap already does, as a *tap card*: it stays after release, the next press anywhere closes it and still acts on what it lands on, and re-tapping the same thing just closes it (the glossary term card is the same path). Per target: a **unit on the board** — a quick tap (no drag, released on its own tile, before the hold opens) reads it; a hero is selected *and* read, and pressing it again to drag closes the card and drags as before; a recruitable is left out (its tap talks to it); not while aiming (a board tap picks the target). A **timeline diamond** or the **prominence diamond** — selects its owner (GG-T288) and reads it. An **ability slot** — a slot that aims needs no card (aiming brings the Party page forward with the ability card); a slot that can't aim (recharging, no mana, silenced, acted, no valid target …, or tapped while the battle is playing out) shows its banner reason and opens its card. The **mana pool** and the actor panel's **status pips** read on a tap too. In camp, read-only rows read on a tap: the Status page's Known Abilities, the Abilities screen's Locked and class-passive rows, the Bestiary's ability chips and any item row with no tap action; rows whose tap selects, buys, equips or toggles keep their tap and read on a hold. Holding still reads everything above. Timeline timing and rules are untouched.

**Tap a [term]** (*added 2026-11-06, GG-T300*): wherever a description renders (the hold cards above, the ability card/confirm card in the actor panel, enemy kit lines and status rows, the Abilities screen, every item detail and weapon passive, gear compare, enchants, talent nodes and trees, the Bestiary, class/party/summon cards) it is a `GlossaryText`: text with bracketed terms becomes a wrapping row of word labels in the same classes, terms highlighted and underlined. A **tap** on a term opens a term card (headword, Buff/Debuff/Stat/Chance/Keyword, the definition, and a linked status's effect and duration) with the InspectCard's anchoring, stage/safe-area clamp, ease-in and `ui.select`; it stays after release and the next press anywhere closes it (re-tapping the same word just closes it); the tap never also acts on the row or card around it. Holding the element still opens its InspectCard (where terms are drawn but inert). Each term's hit area is padded 24 px vertically and 16 px horizontally and pulled back by the same negative margin, so layout is unchanged (POL-C2). Plain labels that receive bracketed content still tint the terms instead of showing brackets. Long GlossaryText paragraphs follow the 60-character measure rule (`Typography.Paragraphs`).

```
┌─────────────────────────────────────┐ y=30
│ [II]     Stage name · Wave n/m   N G│  pause · title/wave · coins this battle
├─────────────────────────────────────┤ y=150
│  ◆◆                                 │  prominence diamond: next actor,
│ ◆NOW◆◀━◆━·━◇━·━·━◆━·━·━◇━·━◆ [GUARD]│  "NOW"/seconds · one 10 s track,
│  ◆◆   ◀ diamonds flow left (acts)   │  ◆ every 1 s (big every 5 s);
│                                     │  turns + casts on the same bar
├─────────────────────────────────────┤ y=333
│        Action banner          3.2   │  banner (2 lines) · drag-timer secs
├─────────────────────────────────────┤ y=450
│     6 × 8 board (180-px tiles)      │  units: art, HP bar, ≤3 buff pips,
│        heroes, enemies, traps       │  countdown badge, glow
├─────────────────────────────────────┤ y=1906
│ [slot][slot][slot][slot][slot]      │  AbilityBar 5 × 196 tall
├─────────────────────────────────────┤ y=2118
│ ● ● ● ● ● ● ● ● ○ ○ ○ ○             │  12 mana orbs with letters
├─────────────────────────────────────┤ y=2210
│ [ Party ][  Lore  ][  Log  ]        │  info tabs (GG-T391)
│ Party: portrait · name · Lv · HP/   │  one window to the safe bottom;
│ stats · buffs · Skills readout      │  the shown page scrolls inside it
│ Lore: description · lore · Bestiary │
│ Log: every combat line, newest last │
└─────────────────────────────────────┘ safe bottom − 24
```

- **Banner** (`ShowBanner`) carries announcements, refusal reasons, "PINCER!", "X's turn", "Wave n/m", "VICTORY!". **Amended 2026-10-09:** it replaces the old AnnouncementWindow + combat feed. ⬜ Not yet built: the 7-line combat feed with inline icons. **Amended 2026-11-16 (owner, GG-T391):** the banner row sits a gutter under the timeline frame (y = 333, 110 px tall — two lines of the stepped-down font) and is inset by the drag-timer's width on both sides, so it stays centred and never runs under the timer; text taller than that still scrolls.
- **Info tabs** (*added 2026-11-16, owner, GG-T391: the battle screen's bottom becomes one tab panel — selected hero and skills, lore, combat log — with nothing overlapping at any aspect 9:23–3:4*): everything under the mana orbs is one tabbed window — **Party · Lore · Log** (`BattleScreen.BuildInfoTabs`). The rows under the board are stacked a 16-px gutter apart (ability bar, orbs, tabs) and the tab window stretches from y = 2210 to the safe bottom, so it alone absorbs a taller stage ([§26.3](#263-adaptive-stage-gg-t169)); each page is a ScrollView that scrolls inside it, and on the shortest stage the window is about one tap target tall. The picked tab lasts the whole battle.
  - **Amended 2026-11-21 (GG-T524):** each tab is a full **132-px-tall tap target** (`.info-tab`, transparent) with the old thin **60-px drawn face** at its bottom, flush on the window; the face takes no clicks itself. The strip starts a 24-px target gap (POL-C3) under whatever is above it, so the window gives up ~80 px on a 2532-tall stage (a 9:16-9:19.5 phone) and nothing on the board. `harness.py a11y` reports no small battle target.
  - **Amended 2026-11-21 (GG-T523):** on a stage wide enough that a cell of 228 px (a slot plus its gutters) fits between the stage edge and the board (stage width ≥ ~1616: 2:3 and 3:4 tablets), the ability bar leaves the row under the board and stands **beside** it as two columns bottom-aligned with the board: slots 1-3 on the left, slots 4-5 and the mana pool on the right, each cell full column width × 228 tall (`BattleScreen.LayoutAbilityBar`, `SideBars`; re-laid live on a resize). The info tabs then start a gutter under the board, so at 3:4 the window gets the bar's 196 px back (≈ 470 px on a bare 3:4 stage instead of ≈ 330, ≈ 320 under a notch and gesture bar instead of ≈ 180) and the tutorial card shows whole. Narrower stages keep the row. Checked with `harness.py safeareas` (artifacts/t523-safe). **Withdrawn 2026-11-23 (GG-T706):** no stage is wider than 9:16 (1424) any more, so the side columns are removed; the ability bar is always the row under the board.
  - **Party** — the actor panel (below) for the selected hero or the tapped unit, and under a hero its **Skills** readout: each unlocked bar slot's icon, name, cost pips, kind and recharge / charges, and its description (holding a row opens its InspectCard like the slot). While an ability is aimed this page shows the ability/confirm card with **Cast / Cancel side by side** at its top whatever tab was picked, and the pick comes back when the aim ends; a new tutorial lesson brings the Party page forward once and puts **Next / Skip** side by side at its top.
  - **Lore** — the unit's name, role, Prose description and lore (`ClassDef.Description` / `Lore`); for a foe, what the Bestiary knows (defeats, knowledge tier, weaknesses once known, defeats to the next tier), or "Not yet in the Bestiary" before it has been met.
  - **Log** — the combat log: every `Feed` line this battle, newest at the bottom (the last 200 kept, and kept across a rebuild), pinned to its end while shown. It replaces the fading three-line feed that drew over the board's corner; nothing draws over the board from the HUD any more. The page keeps the old feed's element name, `feed`.
- **Pincer group outline (amended 2026-10-10, owner):** instead of a line across the character art, a rounded outline *draws itself* around the whole group — Hero, foe…, Hero (e.g. a 4-tile-long box) — in 0.24 s, in gold (red for enemy pincers); each supporter is joined to its partner by a short blue link that bridges the gap between their frames, edge to edge. The drag preview shows the same outline. **Amended 2026-10-11** (owner: the shape needs a beat of realization before the cut-in, not to appear alongside it): the outline draw is now *awaited* in full (it used to fire detached and overlap the cut-in) and is followed by a short 0.18 s pause — "caught" reads before the portraits slam in — before any cut-in or pop-up card starts (`BattleScreen.PincerAnim`). **Fixed 2026-10-19 (GG-T98):** the outline is drawn from the formation-time footprints `PincerFormedEvent` now carries (`Attacker1At`/`Attacker2At`/`OpponentsAt`/`SupportersAt`), never from end-of-command state — a member that moved later in the same command (the foe's own turn, knockback, a later chain link) used to stretch it into a multi-tile box. It is always one tile thick across its axis (a 2×2 foe may widen it to its own size); `PincerGeometry.Error` guards that in tests and logs an error in dev builds. Every other playback beat reads a unit's position from its board view (`UnitView.Tile`, advanced event by event), not from `State`. **Fixed 2026-10-19 (GG-T128):** the outline layer (`board.Lines`) lives inside the board again, so it rides every camera twist, zoom, drift and screen shake and always hugs its tiles (GG-T65 had moved it onto the global Overlay). Cut-in cards (`ActionCard`: caster slides, chain pop-ups, supporter pops) and their dimming veil now sit in a stage-sized card layer inside the board just below the outline (board order: tiles, units, marks, fx, cards, outline), so a card still never covers the outline, and the cards move with the board camera; a second veil sheet dims the HUD below the board. Nothing of the battle is on the Overlay under Pause any more except the full-screen pincer cut-in, billboard and coin/orb flights, which the pause veil stays above (GG-T126).
- **Combat presentation (amended 2026-10-10, owner: "keep the frenzy and chaos of multiple attackers … each attack layered up like an accumulating math problem before exploding into the final damage number, and a clear announcement billboard … like Final Fantasy VI"):**
  - **Billboard:** an FF VI–style blue window at the top of the board names everything as it happens — "Paladin & Barbarian — Pincer", "Chain ×2", "Support: Cleric", "Power Amplifier ×1.75", each spell/skill/item with its pixel icon ("Fireball → Slime"), enemy charges ("Witch: Poison Cloud on Paladin"). Up to 3 lines; each fades after 1.6 s (÷ battle speed).
  - **Damage tally:** inside a pincer, every hit on a target adds a term to a tally above it ("10 + 5", crits as "8!", "×AMP" when amplified); when the pincer ends (its orbs mint, the next link starts, or time advances) each tally collapses and **bursts into the total** at 120 px with a screen shake. Single hits keep their normal floating number.
  - **Cut-in modes:** the full sweep on the first link (supporters' portraits slide up in a row from the bottom edge alongside it); chained links get **pop-up portrait cards** beside each attacker (bounce in, drop away); a **spell** slides its caster's portrait in from its side (heroes left, enemies right). All honour Settings → Pincer close-ups and Reduce motion.
- **Action cut-ins (amended 2026-10-11, owner: "portrait slides … should be action packed just like the board that twists and zooms, and have supporters pop in and out to show their participation bonus"):** every portrait arrives on a feathered team-colour band (bright core, racing light streaks) as a head-and-shoulders bust in a tilted white window, twisting in from an oversized tilt with a flash and an impact shake, then whipping out; the board camera punches toward the action and settles. Spells dim the stage behind the caster's cut-in and print the spell name; chained links pop both attackers with "CHAIN ×n"; each **supporter pops in at the moment it strikes** tagged "SUPPORT" and pops back out. The first-link sweep opens on a white flash with a harder push-in and twist.
- **Pincer close-up cut-in** (ported from GridGame2026's portrait slide-in, US-145/US-333): on the first pincer of an action, both attackers' portraits sweep past in lanes flanking the attacked line on the old slide curve (fast in → slight overshoot → drift back → fast out, ~1.14 s at ×1), in one of four entry patterns (counter sweep, mirrored, same-side chase, staggered one-two), with Perlin hand-held sway and a slow push-in. The world dims and the board "camera" pushes in 8.5 % on the pincer with its own sway, then settles back over 0.3 s. Chained links keep the quick beat. Off with Settings → "Pincer close-ups" or Reduce motion (`BattleScreen.PincerCutIn`).
- **Floating text** over units: damage (crit bigger + shake), heals, "Steam!", "Stagger!", "Steady!", "Interrupted!", "CLUTCH!", "SHATTER!", "Snare!", "Trap!", "Zzz…", "Revived!".
- **Actor panel**: ~~enemy HP and stats show only if the class has been Scanned (this battle or ever)~~ **Amended 2026-11-06 (owner, GG-T290):** what it shows of an enemy follows the Bestiary knowledge tier (`BestiaryKnowledge.Tier`, [§25.11](#2511-bestiary)) — Speed after 1 defeat, HP and stats after 3, weaknesses after 6, abilities and brain after 10; anything not yet known reads "???". It fills from a board tap or, since GG-T288, a tap on the unit's timeline icon ([§2.6](#26-one-track-diamond-icons-the-prominence-diamond)). While targeting it shows the ability card ([§4.5](#45-slot-visual-states)).
- **Guided first battle (amended 2026-10-11, owner: "a full tutorial with explanations and examples, the first battle needs 5 waves of increasing difficulty"):** Tithe Moors I has **five waves** — slime; two slimes; bat + level-2 slime; level-2 wolf + goblin thug; level-2 wolf + goblin + slime — and ten lessons, each an explanation plus a worked example: Moving, Pincers, Mana, Chains, Spells (with the confirm step and charge bar), The timeline, Roles & Taunt, Pickups, Interrupting, Ready. **Amended 2026-11-05 (GG-T252, POL-D7):** four more — Hold to read (done the moment a card opens; **renamed Tap to read, GG-T450** — it teaches the tap, mentions the hold, and a tap card completes it too), The hold timer (and cancelling a drag), Buffs & debuffs, Armor plates — fifteen in all with Shoving (GG-T190). A lesson completes when the player does it (or after a few actions, or Next); lessons that need particular foes wait for their wave ("Next lesson arrives in wave N"); moving lessons light up a demo move; Spells and Taunt select the hero and pulse the slot. Skip ends it for good (`SaveData.TutorialDone`).
  - **Amended 2026-11-21 (owner, GG-T539): coach marks.** Owner: "instead of DOING it for the user, it shows the user what to do with highlights and arrows and dotted lines ... draw boxes around buttons and wait for the user to click that exact area", and for moves "it will draw arrow to destination and user can only move along that path". Every lesson now opens with a **blocking popup** (`Coach`, `App/UI/Coach.cs`, on the global Overlay): "Lesson n of 17", the title, one or two lines (`Lesson.Short`), and an **image** of that lesson's mark on the real board (`Resources/Tutorial/<id>.png`, cut by `GridGame.Studio/tools/lesson_images.py` from a coach-marks harness run; a missing image is left out), with **Skip tutorial** and **Next**. After Next the stage dims except the one target, a gold box frames it (pulsing; static under Reduce motion), and an arrow from a one-line caption points at it; for a move a **dotted route** runs from the hero to a dashed destination box. **Only the box takes input** (taps elsewhere nudge the box), and the step completes only on the exact action; multi-step actions chain (tap the hero → the slot → the target → Cast/Use/Go). A move mark arms a **drag rail**: the lifted hero rides the route (the token follows the finger's nearest point on it, the path grows only along it, never back), a drop anywhere but the marked tile puts the hero back for free, and a coached drag is never forced by the hold timer or the drag timer. The lessons, in order: Moving (the bot's best move), Pincers, Tap to read (a foe), Shoving, The hold timer (lift and drop off the board), Mana (the globe, then a spell's cost pips), Chains, **Supporters** (new), Spells (a ready spell, a heal first), Buffs & debuffs, The timeline (the next-actor diamond, the bar, the same foe on the board), Roles & Taunt (whichever hero carries Taunt; the starting Tank is now the Warden, not the Paladin), **Guard** (new: tap GUARD), Pickups, Interrupting, Armor plates, Ready — 17. A lesson whose situation isn't on the board (no chain to make, no foe charging) shows its popup, keeps its words in the Tutorial tab and completes the old way (done, patience, or the tab's Next), and puts its marks up the moment the situation arrives after a later command. **Never stuck:** a target that doesn't appear within 2.5 s or a step that stops being possible (`CoachStep.Valid`: the hero can no longer move, the slot is spent) ends the marks and play goes on. **Battle systems met later** get the same treatment the first time they appear, in any battle: **Guard** (the first too-little-time gap), **Potions** (a ready item on a bar while a hero is under 80% HP: hero → item → the most hurt ally → Use) and **Reinforcements** (the arrival, then its timeline icon). **The Tutorial tab stays** as the lesson's recap: the full text and example of the lesson under way, and Next / Skip for one that is waiting for its situation; the popup is the lesson, the tab is what's left to reread once it closes. Seen lessons are save flags (`lesson:<id>`); **Skip tutorial** asks to confirm and then turns every lesson off for the save (`lessons:off`, `TutorialDone`, every camp tip seen). **Settings › How to play › Replay tutorials** (was Replay camp tips; still `replay-camp-tips`) clears them all: the first battle's lessons run again on The Tithe Moors I (`TutorialDone` false), the battle-system and Camp lessons the next time each comes up. Lesson text is UI text and lives in code (`BattleScreen.Tutorial.cs`, `CampTips.cs`, `CampCoach.cs`), not Prose (§27 "Where the words live"). Bots, demo, test mode, autoplay and harness runs without `-coach` (or `-campTips`) get no blocking lessons; the first battle then teaches through the Tutorial tab alone, as before. Automation `GET /coach` reports the lesson, the step, its caption and the box, route and destination in design px. *(Verified: a new profile through the tutorial battle and the first Camp visit with `-coach -capture video hq`, artifacts/t539-coach.)*
  - **Amended 2026-11-21 (owner, GG-T542): every lesson's situation is guaranteed.** Owner: "The tutorial battle (The Tithe Moors I) must teach every step." Chains, Interrupting, Roles & Taunt and Armor plates (and, by luck, Pickups and Spells) could show only their popup because their situation never came up on the board. Now the tutorial battle is a scripted set piece: it plays on a **fixed seed** (`BattleScreen.TutorialSeed`, so its waves and the foes' choices are the same every time; no wanderer joins it), the battle is flagged `BattleSetup.Tutorial` / `BattleState.Tutorial`, and when a lesson opens and its marks find nothing to point at, the screen sends a **`TutorialSceneCommand`** (Core, `BattleRunner.Tutorial.cs`; refused in any other battle; no time passes and no hero's turn is spent; recorded and encoded like any command) that sets the situation up before the popup shows: **Pincers / Shoving / Supporters**: a foe walks on (played as a reinforcement) at the nearest free tile where, with it standing there, some hero move makes that pincer (found by previewing the party's L-shaped moves, up to 6 tiles, against every free tile); **Chains**: up to two foes walk on into the gaps of two hero pairs that line up after one move, so the drop leaves two pincers on the board (if the party stands too tight for any chain, one hero first blinks a short way aside); **Interrupting**: a foe that can charge and that a pincer reaches starts its charge now; else one blinks a short way to where a pincer reaches it and starts; only with no charger on the board does one (a wolf, Howl) walk on; **Roles & Taunt**: as the lesson opens, a foe walks on two tiles from the Cleric (unless one is already within two tiles of a non-Tank), and the lesson waits until the Warden can act; **Pickups**: a pickup lies where a hero's move ends; **Armor plates**: a plated foe (a soldier) walks on, not touching a hero, unless a plate is already whole on the board; **Spells**: the bank tops up to the cheapest spell's cost; **Buffs**: the Tank is shielded (Protection) unless someone already wears a pip. A scene's foe is a prop, not a new threat: it arrives at **half its HP**, so the set pieces don't tip the first battle against the party (the scripted run is won). Which foe each scene brings is stage data (`StageDef.Scenes` in stages.json: `{Id, Class, Level}`, `Pickup` for the pickup; validated). Lessons that point at a hero's slot or a move (Moving, The hold timer, Spells, Taunt) now wait for their marks like the others instead of ending on the popup, and **Ready** points at the board ("Clear the last wave"), so every one of the 17 opens popup → marks. Other stages are unchanged; delayed reinforcements and the GG-T512 fast-forward are untouched (scene foes are ordinary foes of the current wave). *(Verified: `python tools/harness.py coach t542-coach --camp`: all 17 battle lessons showed popup and marks, the battle was won, then results → To Camp → the Merchant's Camp-menu lesson → its buy lesson.)*
  - **Amended 2026-11-23 (GG-T731): Mana orbs lesson.** A new lesson, **Mana orbs** (`orbs`), comes right after The hold timer and before Mana, so the first battle has **18** lessons: what an orb *is* before Mana teaches how to earn one. Its popup: "These glass orbs hold your mana. Mana dust flows in and fills them with glowing light; a full orb breathes, ready to spend on an ability." Its marks are two info steps: the orb row (empty orbs are clear glass that mana dust fills with glowing light), then the first orb (a full orb glows and breathes: ready to spend on an ability). The copy describes the orbs as GG-T730 draws them (empty glass sphere, glowing fluid filling it, a quiet breathing pulse when full). It needs no scene; it completes on its marks, on the first orb minted, or after 2 actions. The harness's `BATTLE_LESSONS` lists it.
- **First-battles hint** — **Amended 2026-10-09 (new):** until the third campaign stage is cleared (`HighestClearedStage < 2`), after 5 s of idle input (not on Auto, not targeting) the board shows the move `HeroBot` would make — the hero glows, its path is drawn to a glowing tile, banner "Hint: drag X to the glowing tile" — or "Tip: trap an enemy between two heroes" when no pincer is available. Any input clears it.
- Tile highlights and pincer lines draw **over** the units so previews stay readable.
- Long banner text steps its font down (48 → 38 → 32) so it never runs off the stage.

### 9.1 Combat text (GG-T826, epic GG-T823)

**Owner 2026-11-24:** "action packed and bombastic like Devil May Cry": combat text varies by damage, health, debuff, buff and timer, with one cohesive feel. Battle text may use its own faces and sizes, the allowed exception to the one GUI text size (§26.6, GG-T770).

- **One entry point.** `CombatText` (`App/Battle/CombatText.cs`, one per board, `BattleScreen.Text`) shows every floating battle text: `Show(unit | tile | point, kind, text, magnitude, tint?)`. `BattleScreen.FloatText` delegates to it (an explicit kind, else size 70+ reads as Callout and anything smaller as Support); `BoardView.Float` builds its bare label through `CombatText.Build` for the few callers that animate a label themselves (the pincer damage tally).
- **Kinds.** `CombatTextKind`: Damage, Crit, Overkill (the hit that takes the target to 0 HP), Heal, Buff, Debuff, StatusTick (damage-over-time ticks, sleep), Timer (countdown digits; casting, charging, pushback, stagger, interrupted), TimerEnd (a countdown hitting 0), Expire (a status running out or cleansed), Miss (miss, dodge), Immune (armored), Weak, Resist (clang, steady, resisted), Combo (pincer and chain shouts), Callout (big named moments), Support (everything else).
- **Style table.** `CombatText.StyleOf(kind)` gives each kind a face (Heavy rounded cut for impact, status and timer kinds, the Display face for Callout), a colour from `Palette`, an outline width and colour (dark ink, or a hot dark edge for Crit, Overkill, Weak and Combo), the drop shadow, a motion, a duration and a lift above the anchor. Sizes are style tokens, multiples of `TypeScale.Base`: Damage 1.5, Crit 2.0, Overkill 2.3, Combo 1.6, Callout 1.5, TimerEnd 1.4, Heal 1.3, Weak 1.2, Miss/Immune 1.1, Buff/Debuff 1.05, Resist/Support/Timer 1.0, Expire 0.95, StatusTick 0.85; each kind also grows by its share of magnitude (0..1: damage or heal as twice its share of max HP, a chain's length over 4).
- **Motions.** Slam (falls onto the target, squashes, settles, holds, drops: Damage, Crit, Overkill, Combo; GG-T668's strike), Pop (swells in and holds: Weak, Callout), Rise (Heal, Buff, Support), Arc (hops aside: Miss, Resist), Shake (jitters while it holds: Immune), Drift (slow rise with a sway: StatusTick), Drop (jolts down, shudders, sinks: Debuff), Tick (snaps in from big like a clock hand: Timer), Flash (pops and strobes white, then swells out: TimerEnd), Fade (holds, then fades and shrinks where it stood: Expire).
- **Status and timer text (GG-T830).** One look per status, in its own `BuffDef.Color`: `CombatText.Status` floats a landing buff rising after an up-chevron (▲) and a debuff dropping after a down-chevron (▼) (`InflictAnim`); `CombatText.Tick` prints a damage-over-time tick small with the status's pip glyph (`Letter`, else its first letter) before the amount (the DamagedEvent's note names the status); `CombatText.Expired` fades a status's name out struck through (`<s>`) when it expires, is cleansed, cured or broken (`BuffRemovedEvent`, all reasons but steam). `CombatText.Countdown(at, left, tint?, glyph?, zero?)` is the one countdown: 3, 2, 1 tick in as Timer digits growing toward 0, and 0 flashes as TimerEnd (showing what fires, else "0"). A status on its last turn or tick ticks its glyph and "1" (`BuffTickedEvent`); a queued hazard counts its seconds over the middle of its tiles when queued and retimed, fades out struck through when called off, and flashes its name when it lands (`BattleScreen.Region`). Mines, tile-state lifetimes and the cast countdown badge (§2.10) reuse `Countdown` as they are built.
- **Stacking.** Texts anchored on the same half-tile cell while another is still up take the lowest free slot: each slot steps up about a line and alternates sides, so simultaneous texts never overlap. A slot frees when its text ends.
- **Reduce Motion and Battle speed.** Under Reduce Motion (`Tween.Calm`) every text still shows: travel, swell, hop and jitter shrink or stop. Every duration runs through `Tween`, so Battle speed scales it.

---

## 10. Equipment (summary — see §24)

A hero has five equipment slots (`EquipmentSlot`: Weapon, Armor, Relic1–3) stored on `HeroSave`. Equipped items' `Stats` add into `Unit.GearStats`; the weapon's `BaseDamage` is added to physical offense (`Unit.WeaponPower`); resistances fold into damage ([§3.3](#33-elemental-resistance)).

---

## 11. Screen Architecture

### 11.1 Code-only boot

**Amended 2026-10-09:** `Assets/GridGame/Scenes/Main.unity` is an empty scene (the only one in the build, ensured by `GridGameBuild.EnsureScene`). `GameApp.Boot` (`[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`) creates a `DontDestroyOnLoad` `GameApp` that builds, in code: a UI-only camera (black clear), a `UIDocument` with runtime `PanelSettings` (ScaleWithScreenSize, 1170×2532, theme `UI/GridGameTheme` + style sheet `UI/Theme.uss`), the letterboxed stage, the screens host, an overlay layer and a fade layer; then loads content, the save store and audio, and shows the Title (or `-start <screen>`). There are no prefabs (except the code-spawned effect prefabs in `Resources/Vfx`, amended 2026-11-05, GG-T269, [§6.2](#62-vfx-pipeline)), no `[SerializeField]`s and no scene builders; every screen is a `GameScreen` subclass whose `Build(root)` composes UI Toolkit elements via `App/UI/Ui.cs` helpers and USS classes.

### 11.2 Screen list

`TitleScreen`, `SaveSlotScreen` (new/load), `HubScreen` (Camp), `StageSelectScreen` ("Adventure"), `StoryCrawlScreen`, `BattleScreen`, `LevelUpScreen`, `PostBattleScreen`, `ItemsScreen`, `StatusScreen`, `EquipScreen`, `AbilitiesScreen`, `PartyScreen`, `ShopScreen` ("Merchant"), `BlacksmithScreen`, `AlchemistScreen`, `SummonScreen`, `BountyScreen`, `QuestScreen` (GG-T237), `BestiaryScreen`, `SettingsScreen`, `CreditsScreen`, plus the `Confirm` modal. `Automation/StartScreens` maps ids → screens for `-start` and the `goto` endpoint.

#### 11.2.1 Screen transition graph

```
Title ──▶ SaveSlot (New / Load) ──▶ Camp (HubScreen) ◀──────────────────────────────┐
  │ Continue ─────────────────────▶   │ menu rows push sub-screens; Back pops      │
  └─ Credits                          ├─ Adventure ─▶ [StoryCrawl, first entry] ─▶ Battle
                                      ├─ Items · Status · Equip · Abilities · Party │    │
                                      ├─ Merchant · Blacksmith · Alchemist · Inn    │    ▼
                                      ├─ Summon Circle · Bounties · Bestiary        │ PostBattle
                                      ├─ Settings                                   │ (Continue → Camp,
                                      └─ Save & Quit ─▶ Title                       │  Retry / Next stage
                                                                                    └── → Battle)
New Game: SaveSlot → CharacterSelect → StoryCrawl (Prologue) → Camp.
Final victory (first time, §27.0): PostBattle → Continue → StoryCrawl (Ending) → Credits (rolling) → Title.
```

### 11.3 Screen navigation

`ScreenRouter` is a stack: `Push`, `Replace`, `ResetTo`, `Back`. Every transition fades the stage to black for **0.2 s** and rebuilds the top screen. **Amended 2026-10-09** (old fade 125 ms). Escape / hardware back calls `Router.Back`; a screen can swallow it (`OnBack`: the battle cancels targeting, then opens the pause menu). **Fixed 2026-10-19 (GG-T126): Pause pauses.** Opening the battle's pause menu sets `Tween.Paused`, so `Tween.Dt` reads 0 and every playback beat, cut-in, outline, float, coin burst, dissolve, banner scroll and ambient pulse (`Tween.Clock`) holds its exact frame; the event player and `Safe()` start no beat, the watchdog, drag timer, hints and auto battle stop, and ringing sound effects pause mid-clip (music plays on). Resume (or Back) carries on from the same frame. The pause veil is kept the topmost Overlay child, above `board.Lines`, cut-ins, the billboard and coin/orb flights. Verified by `tools/harness.py pause` (pauses mid-chain, two identical frames 2 s apart, zero beats while paused, resume finishes the chain). Screens rebuild on every show and on `Refresh()`, so they always reflect current state. UI callbacks are ordinary C# lambdas (the old "no lambdas in persistent listeners" rule is moot).

### 11.4 Build settings

`GridGameBuild.EnsureScene` writes `EditorBuildSettings.scenes = [Main.unity]`, portrait orientation, no splash, `bundleVersion` from `VERSION`. New screens need no build-settings change.

### 11.5 The visual language

**Amended 2026-11-17 (GG-T404, GG-T407; owner: the Chrono gray "looks more brown" ... "make the gradient totally editable ... many different themes ... textures superimposed"):** menu window themes are data. Presets live in `Resources/UI/themes.json` (27, checked by `WindowThemeCatalog`); the player's own (up to 8) are saved in settings.json `game.customThemes`. A theme is a gradient (four corners, FF7 style; linear at any angle with 2-4 stops; or radial; plus grain), an optional repeating texture (10 generated: granite, marble, parchment, linen, brushed, stars, scanlines, checker, noise, carbon; or a PNG in `Resources/UI/WindowTextures/`) with opacity, blend and scale, a frame style (a `.theme-*` block) with optional frame colours, and optional text and accent colours. Settings → Windows is the live editor. ~~The default is **Granite**, a steel slate (#8696A6 to #242E3C, red always lowest) with a fine granite texture;~~ **Amended 2026-11-20 (GG-T453; owner: "the gray backdrop is bad, go back to the Final Fantasy blue gradient"):** the default is **Classic** again, the Final Fantasy blue gradient (#2C46AA to #0A1246) with the classic frame, listed first; a profile still on the Granite default moves to it once (lookVersion 3), a later pick of Granite sticks; the theme and font rows are back in Settings (`Features.LookPresets`), because the owner compares looks until the interface is the best it can be. **Amended 2026-11-21 (GG-T566):** the default is now **Dark** (`SettingsSave.DefaultMenuTheme`); a profile written at lookVersion 3 and still on Classic moves to Dark once (lookVersion 4). Version 3 stamped every write, so an untouched Classic default can't be told from a deliberate pick there and the owner chose to move them all; a Classic kept from versions 1-2 (a deliberate pick then) stays, and from version 4 a pick of Classic sticks.

**Amended 2026-11-22 (GG-T678; owner: "find a balance between the dark theme now and the final fantasy ... theme that highlights the importance of Mana, the Manalith, the Halcyon technology used to contain mana ... not 'steampunk' but 'manapunk', more refined and understood, like a high tech facility but also old and abandoned"):** the default is now **Manapunk** (a version-3/4 profile still on Dark moves to it once, lookVersion 5; from version 5 a pick of Dark sticks). Its visual language, which every screen shares:
- **Panes** are smoked glass: a dark indigo gradient (#22203A to #100F1C) with a faint soft-light wear texture, sitting over the living backdrop. They read as old lab glass, not a flat card.
- **Rails** are thin aged pewter (#60646F, a darker #34363F underneath): the Halcyon facility's frame, desaturated and quiet. No brass, no gears, no rivets.
- **Contained mana** is the one accent, the Manalith's blue (#7590C8, sampled from the Manalith art; lighter #ACA5D3; deep #41386F): a lit conduit along a window's top edge and the header's underline, the active tab's top, the primary button (mana held behind glass), the selection tint. It is used sparingly, so where it appears means "this matters".
- **Buttons** are clean capsules with a fine pewter rim on a dark lip; the primary holds the mana fill. Danger stays its own wine red, from the Manalith's red lobes (#963659 rim).
- **Dividers and gauges** are thin piping lines in the rail colour; the mana orbs (GG-T673) are glass bulbs in the same language: a pewter collar, dark glass when empty, the mana blue with a soft bloom when charged.
- **Type** is unchanged: the bundled fonts with the hairline outline (GG-T385), text #E6E6EE, dim #A4A3BC, both above 6:1 on every pane (WCAG AA with margin).
- **Restraint**: ornament lives only at edges and corners, never behind text; spacing and sizes are exactly the Dark theme's (only colour changes), so the GG-T273 layout work stands. Lab-clamp corner brackets are the next ornament if the owner wants more frame; they need a child element per window, so they're left out of this first pass.
- **Lab hardware (GG-T678, second pass, after the visual check found the theme too close to Dark):** every Manapunk window carries four pewter **clamp brackets** at its corners (44 px arms on a 6 px bar with a lit top edge, a bolt head where they meet, a dark worn underside) and a **mana conduit** down its left edge: an 8 px glass tube between valve collars holding a dim charge of mana blue that now and then flickers with power (GG-T761, owner: "high tech but degrading"): the tube idles at Quake light level c for 2.5 to 9 s, then plays one Quake light style (flicker, fluorescent flicker, slow strong pulse, candle, strobe and a few variants; a = dark, z = full) at 10 Hz, sometimes a little faster or slower, with a random seed and start offset per window so no two conduits fire together; a steady glow (level h) under Reduce Motion; hidden on windows shorter than 220 px. Both live inside the window's texture layer (`WindowThemes.LabHardware`, class `window-lab`), so they add no window child, and sit in the frame's padding band, never behind text. Gauges are glass tubes in a pewter sleeve (lit top, worn bottom), and the primary button gets a bright lit top edge, a charged mana cell. Tokens: `Palette.Pewter/PewterDark/PewterLight/ConduitGlass/ConduitMana`, `Size.ClampArm/ClampBar/ClampBolt/Conduit/ConduitMinHeight`, `Timing.ConduitStep/ConduitIdleMin/ConduitIdleMax`. Piping dividers with valve nodes still wait for a screen that draws a divider element.

**Added 2026-11-21 (GG-T563; owner: "themes needs to support Brightness/Contrast/Gamma; so that they can be adjusted based on the devices on quirks when it comes to nits or w/e ... present themselves by default in the '0.5' level and be able to push the bars in either direction"):** every theme has Brightness, Contrast and Gamma, each 0..1 with **0.5 = the theme exactly as authored** (a theme or file without them reads 0.5; no migration). `Meta/WindowTone.cs` is the one mapping, per sRGB channel: gamma `c^(2^((0.5−g)·2))` (exponent 2..0.5, black and white fixed), then contrast about mid-gray ×`2^((k−0.5)·2)` (×0.5..×2), then brightness `+(b−0.5)·0.5` (−0.25..+0.25), clamped; the ranges stop short of a white or black sheet. It applies to everything the theme paints: the baked gradient (per pixel, before WindowDark's darkening), the texture tile, the frame colours, the accent (its own or derived from the toned gradient) and the text colour, which is then held to 4.5:1 against the window's lightest toned colour (lifted toward white, or toward black when even white can't make it), so the stylesheet's light text gets a readable replacement on a theme pushed bright. Settings → Windows → **Tone** has the three bars, a tick at the 0.5 centre, a live repaint and a Reset to 0.5; a change is kept at once (not via Save). A preset's tuning is the player's, per device, in settings.json `game.themeTones` (`{"<theme>": {brightness, contrast, gamma}}`), so themes.json stays pristine and an update never wipes it; a custom theme stores its own `brightness`/`contrast`/`gamma`. *(Tests: `WindowToneTests`.)* **Added 2026-11-21 (GG-T564):** the chrome Theme.uss paints follows the tone too: frames without frame colours, plain windows, buttons (and their primary/danger, pressed and hover states), tabs, the header, toasts, selected rows and cards. Theme.uss stays the one place those colours are written: `App/UI/ThemeChrome.cs` probes each element kind under the active frame style's class once, reads the resolved colours, tones them and sets them inline (re-set as an element is pressed or hovered; a 200 ms sweep picks up new elements while the theme is tuned; colours a screen set inline itself are left alone). Dim captions (`.dim`) get a floor of 3:1 against the tuned window (`WindowTone.CaptionContrast`, WCAG's large/secondary-text minimum). At 0.5/0.5/0.5 nothing is set inline.

**Amended 2026-11-16 (GG-T385, owner: "use a Chrono Trigger gray instead of FF blue gradient … more 'block outline' around fonts"):** the default menu theme is **Chrono**, a neutral gray gradient (112→40) in a light bevelled frame (`.theme-chrono`); Classic FF blue stays selectable, and a profile still on Classic/Classic moves to Chrono/Rounded once (`lookVersion`). Fonts: **M PLUS Rounded 1c** Bold for body (preset "Rounded", default) and ExtraBold for Heavy, the free cousin of the New Rodin family Japanese mobile RPGs license; **Cinzel** for display. Text depth: a dark block outline plus a hard unblurred drop shadow in three steps (small 1px/2px, body 2px/3px, heading 3px/4px, display 6px), set once in `Theme.uss` (`.depth-lg/md/sm/none`, `.text-ink`); Cinzel takes a 1px outline (`Fonts.UseDisplay`). Neutral tokens are gray; gameplay colours (hero, mana, elements, rarity) are unchanged.

**Polish rules (GG-T249):** type scale, color tokens/contrast, spacing, touch targets and motion tokens are defined and measured in [`POLISH.md`](POLISH.md) §A–§C, §E ([GG-LAW-14](#GG-LAW-14)).

**Design tokens (GG-T250):** the visual language lives in tokens, not literals. `Theme.uss` declares them as `:root` custom properties (`--color-*` semantic palette incl. teams, elements, rarity and gauges; `--type-*` on the 24…120 scale; `--space-*` on the 8-px scale) plus `.t-caption … .t-display`, and its base rules use `var()`. `App/UI/Tokens.cs` mirrors them for code: `Palette` (semantic colors, `Palette.Element(DamageType)` — the one element table, every element its own hue; Physical no longer shares Arcane's, Haste and "Pushback!" no longer borrow Ice blue), `TypeScale`, `Space`, `Timing` (instant 0.08 · quick 0.15 · standard 0.25 · emphasis 0.4 · dramatic 0.7 · cinematic 1.2+) and `Ease` (Out, InOut, Back, Snap). Battle and menu presentation take their colors and sub-second durations from them; no duration is random. **Amended 2026-11-17 (GG-T273/GG-T420):** `Theme.uss` has no literal `font-size`/`margin`/`padding` left (every one is `var(--type-*|--space-*)`), and App code's inline `.fontSize` assignments outside `FloatText` — menu/status labels, the region and timeline glyphs, `BoardView`'s board tags and badges, every `BattleScreen.Playback`/`Fx` `board.Float()` call — route through `TypeScale.*`. **Still outstanding:** `FloatText`'s own ~30 call sites (`BattleScreen.Playback/Fx/Recruit/RuleBreak/Tame/Wanderer.cs`) each pass their own literal size (POL-A1 lint doesn't even catch these, since `FloatText(` doesn't match its `Float(` pattern) — a future cycle should give them the same TypeScale snap. **Weight is a font, not a style:** USS has no `-unity-font-style: bold`; the formerly bold classes (`title h1 h2 window-title header-title btn tab--active role-badge pip`, plus `t-heavy`) get the real Outfit Bold cut (`Fonts.UseHeavy`) from `Ui.AddClasses`/`Ui.Button` and a sweep after every screen build (`Ui.ApplyHeavy`), skipping text already set to Display.

**Amended 2026-10-11 — one press feel (owner: "all button presses need feedback like depress … a standardized look and feel"):** every tappable thing goes through `Ui.Pressable` (buttons, menu rows, tabs, cards, bar cells, battle slots). On touch it sinks — buttons stand on a darker lip (thick bottom border) and drop onto it, everything shrinks to 95 % and darkens — with a soft `ui.press` tick; on release it springs back (`ease-out-back`), then the action plays its own cue (`ui.click` / `ui.select` / `ui.confirm` / `ui.cancel`). Disabled things don't sink. Battle tiles are lighter and lower-contrast so the stage background reads through the board.

One Final Fantasy–style language (`Resources/UI/Theme.uss`): deep blue gradient windows (`Art.Window`, generated) with white rounded frames, pointing-cursor menu rows (`Ui.MenuRow`, selected row highlighted), gold numbers, rarity-colored item names, gauges, tabs, primary/danger buttons, toasts on the overlay. **Amended 2026-10-10 (owner):** battle units wear the same frame as menu portraits (`.thumb` → `.unit-frame`: white 4 px rounded border over the window gradient); team reads from a thin inner keyline (hero blue, foe red, boss purple `.unit-keyline--*`). Fonts (`App/Services/Fonts.cs`): **Outfit** body, **Cinzel** display (titles), both with Noto Sans → Noto Sans Symbols 2 fallbacks ([GG-LAW-11](#GG-LAW-11)). **Amended 2026-10-09:** replaces HubTheme / UiFonts / UiKit (Attic + Outfit).

### 11.6 Vector shapes — the Shapes standard (GG-T735, GG-T736, GG-T737)

Every arrow, connector, ring, progress trace and box drawn in a `generateVisualContent` callback goes through one API, `App/UI/Shapes.cs`. Nothing draws a line by calling `Painter2D.Stroke()` twice (a wide dark stroke under a narrow coloured one): that "double stroke" left mismatched caps and joins, and its outline cut across overlapping pieces. GG-T737 retired the last ones (the cast trace `CastRing` and buff timer `RingTimer` in `BoardView.cs`, the timeline's recovery diamond `RecoverRing`, the talent tree's `TalentLinks`).

**How a shape is built.** Each shape is math, not strokes: closed polygons (a shaft is its centreline offset by half the width with mitred joins, the mitre clamped at 4×; ends are round caps; an arrowhead is part of the same outline), painted in two passes: the outline (stroked at twice `ShapeStyle.Outline`, so the body covers its inner half and the outline sits wholly outside), then one body fill over every piece. Pieces of one shape (dashes, ring loops) share both passes, so their outlines never cut across each other's bodies.

**The API** (`Shapes.*`, each takes the `Painter2D` and a `ShapeStyle`):
- `Arrow(from, to)`, `PathArrow(path)` — a solid arrow, round tail, along a straight line or a polyline.
- `DashedArrow(from, to | path, phase)` — round-capped dashes up to a solid head; `phase` (px) marches them toward the tip.
- `CurvedArrow(from, control, to)` (quadratic) and `CurvedArrow(from, c1, c2, to)` (bezier), solid or `dashed`.
- `Line(path, closed)` — a round-capped stroke along a polyline (a connector, a progress trace); `closed` makes it a mitred loop with no caps.
- `Bar(from, to)` — a straight round-capped link (an arrow with no head).
- `Chevron(center, dir, size)`, `Dot(center, radius)`.
- `Ring(center, radius, startDeg, sweepDeg)` — a full band, or an arc sweeping clockwise (screen space) from `startDeg` (0 = right, −90 = 12 o'clock) with round ends.
- `RoundedRect(rect, radius, filled, dashed, start, portion)` — a filled outlined body, or a frame centred on the rect's edge; `dashed` breaks it into dashes; `portion` < 1 draws that fraction of the perimeter clockwise from `start` (a fraction from the top edge's left end): a progress frame.
- `PointAt(path, d)` — the point `d` px along a polyline, for placing things on a route.

**ShapeStyle presets.** Start from a preset so every shape on screen shares stroke and head proportions, then adjust it with `Tint(color)`, `Thick(width)`, `Outlined(color, width)`, `Dashes(dash, gap)`, `Head(length, width)`:

| Preset | Body | Outline | Head (len × width) | Dash / gap | For |
|---|---|---|---|---|---|
| `Indicator` | 8 | 3 | 34 × 42 | 18 / 12 | on-board pointers, target and shove arrows, hint routes, the cast trace, the timeline's recovery diamond |
| `Tutorial` | 10 | 3 | 46 × 58 | 22 / 14 | coach marks: the caption's pointer, a move's route, the goal box |
| `Small` | 4 | 2 | 14 × 16 | 8 / 6 | glyph-sized marks: row chevrons, the buff timer ring, talent links |

**White body, black outline.** A shape's body is white (`Fill`, the presets' default) and its outline is near-black ink (`ShapeStyle.Ink`, 90 % alpha), so it reads over any tile, art or window gradient. Tint the body (`Tint`) only when the colour carries meaning: a team, an element, a cast's school, a talent link's progress, a pickup's gold. The outline stays ink; never drop it, never make it coloured, and never draw a coloured shape without one. A dark "track" under a lit run (the recovery diamond) is a body tinted ink at reduced alpha, still with the ink outline.

**Dashed vs solid.** Solid means *this is, or will happen*: the move or shove that will resolve, a link that exists, a timer's remaining span, a pointer at something already there. Dashed means *this could, or is suggested*: a tentative or preview route, a coach mark's proposed move and its goal box, a range or region not yet committed. A dashed route may march (`phase` advancing with time) to show direction; under Reduce Motion it holds still. Don't mix the two on one shape.

---

## 12. Asset Pipeline

### 12.0 Audio

**Amended 2026-10-09:** every cue is an authored clip under `Resources/Audio`; there is no chiptune fallback (a missing clip is silent).
- **Music** (`App/Services/Audio.Music`, per screen via `GameScreen.Music`): Title and Story crawl "TellerOfTheTales", Camp "MinstrelGuild" (sub-pages keep it), Battle "Crusade", Victory "Triumph", Defeat/Escape "MelancholyLull".
- **SFX** (`Audio.Sfx(key)`): game code speaks semantic cues (`ui.click`, `ui.select`, `ui.buy`, `ui.error`, `move`, `slide`, `pickup`, `pincer`, `hit`, `crit`, `miss`, `death`, `heal`, `orb`, `turn`, `fire`, `ice`, `lightning`, `poison`, `magic`, `cast`, `explosion`, `shatter`, `debuff`, `dash`, `trap`, `victory`, `defeat`) mapped to files in `Audio.Cue`; rapid repeats within 40 ms are thinned; 10 pooled sources.
- **Attribution** lives in `Resources/Credits.txt` (Credits screen): Kevin MacLeod CC BY 4.0 tracks, Pixabay "Triumph"; "Melancholy Lull" and the SFX packs are flagged **origin unverified — confirm or replace before commercial release**.
- `GridGame.Studio/tools/import_art.py` reproducibly imports audio (and art) from the GridGame2026 reference project.
- **Amended 2026-11-05 (GG-T254, POLISH.md §F):**
  - **Cue taxonomy:** every cue is `ui.* / battle.* / element.* / status.* / story.*` (e.g. `battle.hit`, `element.arcane` for the old `magic`, `element.water` for `splash`, `status.debuff`, `story.quest_complete`). A key's file is the key with dots as underscores (`battle.dash` → `battle_dash.ogg`); `battle.move`/`battle.hit` pick one of 6/7 variants, `ui.back` shares `ui_click`. `Audio.Canonical` keeps one alias table from the old flat names, so a stray old name still plays, and the cue log records the taxonomy key.
  - **Loudness:** `tools/loudness.py` measures BS.1770 integrated loudness (clips under 400 ms ungated) and 4×-oversampled true peak. Every SFX is normalized as `GridGame.Studio/tools/make_sfx.py` synthesizes it to **−16 LUFS ± 2, ≤ −1 dBTP**, through a look-ahead limiter for percussive cues and one corrective re-encode for what Vorbis trims. Music is re-encoded once (`--music-apply`, two-pass loudnorm, 320 kbps) to **−18 LUFS**. Before: SFX −24.1 … −11.3 (16/57 on target), music −32.0 … −11.4 (0/5). After: SFX −16.5 … −14.7 (57/57), music −18.5 … −18.0 (5/5).
  - **Ducking:** `Audio.DuckFor(seconds)` dips the music **−8 dB** (in over `Timing.Standard`, out over `Timing.Dramatic`, eased in dB) under every cut-in card (`TwistCard`), each hero's level-up fanfare, and dialogue while a line types. It is time-boxed, not push/pop, so a stopped coroutine can never leave the music stuck quiet.
- **Amended 2026-11-20 (Audio epic GG-T457: GG-T456, GG-T458–T461, GG-T465):** the music line above is superseded. Every battle stage plays one track drawn from its region's shuffle bag (`campaign.json` `Music` / `MusicBorrow`), the Camp draws from `HubMusic`, and the moments (title, story, victory, defeat, level-up, character select, credits, Crucible, boss, final boss) draw from `MomentMusic`, seeded with the stock tracks. A finale's rule-breaking boss wave (§14.9) switches to a boss / final-boss track when one exists. Track changes crossfade over 1.2 s (two music sources, unscaled time, under the volume setting and the duck). New tracks come in through `GridGame.Studio/tools/import_music.py`. Detail: [MUSIC.md](MUSIC.md). The event → cue audit, with the cues GG-T465 added (`ui.equip`, `camp.repair`, `camp.caravan`, `camp.summon`, `ui.diamond`, `battle.fizzle`, `element.steam`, `battle.wave`, `status.sleep`; new group `camp.*`), is [SFX.md](SFX.md).
- **Amended 2026-11-21 (GG-T556): attack-type hit sounds.** A landed physical hit sounds like what dealt it: one of ten attack types (slash, pierce, blunt, slam, claw, bite, sting, lash, projectile, magic) chosen by `Content/attacktypes.json` — a collision note ("Slam!", "Crash!", "Fell!"), else the resolving skill's own type, else the attacker's weapon's `WeaponType`, else the monster's natural attack (by class id or family), else `blunt`. It plays cue `battle.hit.<type>` = a random existing `battle_hit_<type>_<n>.ogg`; a type with no files yet plays the generic `battle_hit_0..6`, so nothing goes silent. Elemental hits keep their element's cue, crits keep `battle.crit`, damage-over-time keeps `status.debuff`. `ContentLoader.Validate` requires every weapon type and every enemy class to have a type. The sound brief and the file list are [AUDIO_ATTACK_TYPES.md](AUDIO_ATTACK_TYPES.md).
- **Amended 2026-11-21 (GG-T562, the asset naming rule GG-T561): audio file names.** Every music and SFX file is `word-word-00.ext` (`Core/Content/AssetNames`, `tools/asset_names.py`): `battle.dash` → `battle-dash-00.ogg`, `battle.crucible_wave` → `battle-crucible-wave-00.ogg`, `MoorlandCharge` → `moorland-charge-00.ogg`. The random-pick pools number their picks from 00: `battle-move-00..05`, `battle-hit-00..06`, `battle-hit-<type>-00..15`. Cue keys are unchanged; `campaign.json` names tracks by file (`moorland-charge-00`). The file names quoted in the lines above are the old ones.

#### 12.0.1 Announcements — cadenced callouts

Announcements (`AnnounceEvent`) are played by the event player as an awaited 0.25 s banner beat, one at a time, never an instant swap. Wired for refusals, charges ("X is charging Y!"), boss phases, "Clutch!", "Interrupted!", "Steam!", "Steal!", "Banked N mana", "Escaped!", enemy pincers.

#### 12.0.2 Pacing

**Amended 2026-10-09:** every beat duration lives in `BattleScreen.Playback.cs` (no `PacingConfig`): step 0.16 s, displacement 0.14 s, pincer 0.32 s, orb flight 0.22 s, timeline glide 0.18–0.45 s, turn pop 0.2 s, projectile 0.22 s, victory/defeat banner 1.0 s + 0.6 s before results. All durations divide by `Tween.Speed` = Settings → Battle speed (×1 / ×1.5 / ×2 / ×3). Nothing the player must read resolves in a single frame.

### 12.1 Art

**Amended 2026-10-09:** textures load with `Resources.Load` through `App/Services/Art.cs` (no Addressables, no `SpriteLibrary`):

| Key | Path | Source |
|---|---|---|
| Portrait | `Resources/Portraits/<stem>-<NN>.png` (§12.1.3) | full body on a **1024×1024 transparent canvas**, never trimmed or shrunk ([§12.1.2](#1212-portraits-are-1024-canvases-added-2026-11-07-owner-rule-gg-t352)); the unit's only image |
| Backdrop | `Resources/Backgrounds/<set>-<NN>.jpg` — owner rules 2026-11-07 (GG-T344, GG-T353): a set is named after its **story region** and numbered with zero padding (from **00** since GG-T561, §12.1.3: `tithe-moors-00..04`, `anchor-wastes-00..03`, `free-holds-00..06`; the older names below are the pre-GG-T561 ones), at the art's **original resolution** (JPEG q95; PNG only for art with transparency). Owner: *"make sure the source files for the Backgrounds all get proper naming to identify them to their world lore counterparts"*. The sets: `tithemoors_01..05`, `shardfall_01..05`, `arkgrave_01..05`, `anchorwastes_01..04`, `halcyon_01..14` (the GridGame2026 Moors, RedThorns, ElectricWasteland, UnderTheBridge and CyberNecropolis originals at 1536×3072, every one of them; CyberNecropolis's two byte-identical duplicates are skipped) and `still_01..06` (native 768×1536). Each main campaign theme names its set with `ThemeDef.Backgrounds` (`"tithemoors"`), so theme ids (`greenvalley` …) never change; a theme without one uses its id as the set. `Art.Background` wraps an index over the set's own count (`Backdrops.Resolve`); a postgame theme borrows its base theme's set (`cave2` → `anchorwastes`), then the default theme's, then `tithemoors_01`, unless the owner made it landscapes of its own and it names them (`greenvalley2` → `freeholds_01..07`, GG-T346; `cityruins2` → `underspire_01..09`, GG-T349; §22.3.4; its stages then carry the postgame theme id as their `Theme`, since the battle backdrop reads the stage's theme). Import: `GridGame.Studio/tools/import_art.py` `import_backgrounds` (no resize); Unity caps Backgrounds at 4096 (`GridGameImport`), CompressedHQ, no mips, so a 1536×3072 backdrop is about 4.5 MB of texture memory (the old 720×1440 was about 1 MB) and only the one or two on screen are loaded | biome art |
| Ability icon | `Resources/Icons/<key>-00.png` (64², §12.1.3: `MassHeal` → `mass-heal-00`) | `AbilityDef.Icon` / `ItemDef.Icon` |

Every class has a portrait and a `Thumbnail` (`Every_class_can_be_instantiated_and_has_art_reference`). **Amended 2026-11-23 (GG-T713, superseding GG-T221's "no thumbnail images"):** every small actor image (board token, timeline turn icon, menu/party/bestiary card, list row, talk-menu face) is the actor's baked 256 px thumbnail (`Resources/Thumbs/`); the full portrait is drawn only where the whole figure is meant to be seen ([§12.1.1](#1211-thumbnails-and-portraits-added-2026-11-03-gg-t220-reworked-2026-11-23-gg-t713)).

#### 12.1.1 Thumbnails and portraits (added 2026-11-03, GG-T220; reworked 2026-11-23, GG-T713)

**Current standard (owner 2026-11-23, GG-T713/GG-T719/GG-T720; supersedes GG-T220's "no duplicating assets" and GG-T221's live-only framing below).** Owner: "need to use thumbnails for every usage of an actor except where their full portrait is explicitly used like in discussions; seeing a tiny version of a 1024x1024 character looks bad, need to standardize all thumbnail and portrait usages."

- **The images.** `Resources/Thumbs/<portrait file>.png`: one per portrait, same name (`acolyte-female-00`, `acolyte-male-00`, the mannequin, NPC art like `aetherist-00`), 256×256, with the class's `Thumbnail` crop baked in (male art uses `MaleThumbnail`, else `Thumbnail`; a file no class draws gets a default head crop from its alpha bounds). Made by `tools/make_thumbs.py` (premultiplied Lanczos downscale), which also writes their `.meta`; `Editor/ThumbImport.cs` keeps their import settings: mipmaps and trilinear filtering on. Mipmaps are on only for `Thumbs/`; portraits stay without them (§12.1.2).
- **Keeping them current.** After any portrait or `Thumbnail`/`MaleThumbnail` edit (a re-import, a GridGame.Studio reframe), rerun `python tools/make_thumbs.py`, then `python tools/make_thumbs.py --check`, which exits 1 naming every thumb that is missing, stale (its portrait or framing changed, per `tools/thumbs_manifest.json`) or orphaned. Commit the regenerated thumbs with the change.
- **One helper decides.** `Art.Thumb(classId)` resolves the file exactly as `Art.Portrait` does (the hero's picked variant, or an NPC's own Portraits/ file) and loads `Thumbs/<file>`, or returns null when there is none. `Ui.PortraitThumb(host, id)` draws that thumb whole (inset 0, 100%×100%, no on-screen crop, because the crop is baked in); with no thumb it falls back to the full portrait framed live by its `Thumbnail` as described below (this also covers the mannequin). `Ui.Thumb(id, parent, size)` uses the thumb up to `Ui.ThumbMaxSize` (200 design px) and the framed portrait above it, so a big draw never upscales 256 px art.
- **Thumbnail (every small use):** board tokens and corpses (`BoardView`), timeline turn icons (`BattleScreen.Timeline`), the battle unit card (`BattleScreen`), hero list cards (`HeroScreen`), hub grids and target faces (`HubScreens`), party/stage/recruit cards and locked slots (`MenuScreens`), post-battle rows (`PostBattleScreen`), the shared card (`Ui.Card`), row kind icons for heroes/NPCs (`RowIcon.ActorThumb`, §26.7) and the talk menu's hero and NPC faces (`DialogueScreen`).
- **Full portrait (the whole figure is the point):** the dialogue stage, cut-ins and pincer shots, the death dissolve, the boss intro, the recruit reveal, the hero sheet, level-up, the Mercenary renewal, the variant (gender) picker and the summon reveal (420 px, over `ThumbMaxSize`). These draw `Art.PortraitFigure`.

**Original rule (GG-T220/GG-T221), now only the fallback and the framing source.** Owner 2026-11-03: "these new coordinates and zoom are kept in memory in the character's Thumbnail properties; it always uses the real existing portrait and simply zooms in and offsets; that way the portrait is always in memory and when the enemy dies you can just spawn a portrait of the actual full portrait to shake and die; no duplicating assets."

- Every class carries `"Thumbnail": { "Zoom", "X", "Y" }` (`ClassDef.Thumbnail`). (`X`, `Y`) is the portrait point — fractions of the portrait's width and height from its top-left — that sits at the thumbnail's centre; `Zoom` is portrait width ÷ thumbnail side (1 = the whole portrait width fills the square). Aspect is always kept, and the numbers are resolution-independent, so one framing serves the board token, the timeline icon and every card.
- A square of side S shows the portrait at width W = Zoom × S and height H = W × (portrait h ÷ w), placed at left = S/2 − X·W, top = S/2 − Y·H, clipped to the square.
- `ContentLoader.Validate` requires one on every class, with Zoom in [0.25, 20] and X/Y in [−0.5, 1.5].
- The owner frames each unit in **GridGame.Studio**'s Thumbnails tab (the sibling GridGame.Studio repo: it began here as `tools/ThumbnailStudio`, GG-T222, and moved there, GGS-T2, GG-T579), a separate desktop app that edits these numbers in `classes.json`; nothing is exported. It is set-and-forget (GG-T223). It opens from the "GridGame Studio" desktop shortcut (GridGame.Studio's `run.ps1`) with no console. Every framing change autosaves that unit's block. The studio commits only those blocks, built from `HEAD` in a temporary index so nobody else's staged or unstaged work rides along, as `GridGame Studio: reframe <names> (GG-T223)`, and pushes them, never forced. On close, if it committed anything, it starts `tools/run.ps1` in the background so the installed game shows the new thumbnails. It skips that when the game or the Unity Editor is running, because the next Deploy & Run rebuilds anyway.
- The first values (GG-T220) were recovered from the pre-cut `Resources/Thumbs` images by locating each one inside its portrait (multiscale template match, every result checked by re-rendering it), so every unit first looks exactly as before. `import_art.py` no longer cuts thumbs; a new class needs a Thumbnail before it validates.
- **Drawn live (GG-T221):** `Ui.PortraitThumb(host, classId)` puts the portrait in a clipped (`overflow: hidden`) square host as a child sized and placed in percent of it (width = Zoom×100%, height = Zoom×aspect×100%, left = 50% − X·width, top = 50% − Y·height), so it is right at any size with no layout pass. `Ui.Thumb` (cards), the board token (`UnitView.Art`; corpses frame their `CorpseOf` class), the timeline turn icon (`tl-art`, inside its circle mask) and dialogue faces all use it. A class with no Thumbnail shows its whole portrait contained; a dialogue speaker that isn't a class shows no face. Tints (bestiary silhouette, corpse grey, frost, hit flash) go on the portrait child (`thumb-art`). *(GG-T221 deleted the old `Resources/Thumbs/` and `Art.Thumb`; GG-T713 brought both back as baked images, above.)* The death dissolve shows the same `Art.Portrait` texture whole.
- Separate from `Crop` (the cut-in bust's eye framing, `BattleScreen.CutIns.cs`), which is unchanged.
- **Amended 2026-11-07 (GG-T352):** the numbers now frame the whole 1024×1024 canvas ([§12.1.2](#1212-portraits-are-1024-canvases-added-2026-11-07-owner-rule-gg-t352)). Every class's old framing, made on the trimmed image, was converted so it shows exactly what it showed before.

#### 12.1.2 Portraits are 1024 canvases (added 2026-11-07, owner rule, GG-T352)

**Amended 2026-11-16:** the import cap is 2048 (`GridGameImport` and every portrait `.meta`, GG-T387) so a 1024 canvas imports pixel for pixel on every platform and higher-resolution art would never be halved; dialogue figures sit flush with the screen edge. Portrait edges carry no staircase (GG-T389): `portrait_cut.py --dejag` measures each portrait's staircase (share of off-axis silhouette contour that is a hard 0/255 step) and replaces un-antialiased edges with the coverage of the 1 px-smoothed contour (thin strands exempt, interior and placement untouched); a portrait whose alpha bounds move gets its class's Figure box re-set, and Thumbnail boxes are never changed by tools (the owner frames thumbnails). Portrait edges carry no white fringe or specks (GG-T399): `portrait_cut.py --fringe` measures each portrait's fringe (share of edge pixels, visible within 2 px of transparency, that are near-white while the solid figure 3+ px behind them is at least 45 levels darker, plus a fog's leftover panel-border rim) and its specks (detached alpha islands under 64 px, or faint ones under 1000 px, that are not fog wisps); `--defringe` un-premultiplies those edge pixels against the white they were cut from and pulls their colour to the figure's, shrinks their alpha by a sub-pixel at most (never below 60% of what it was), flattens a fog rim to the fog behind it and drops the specks. Fog, wisps, thin strands and white figures (the mannequin, white cloaks) are not judged or touched.

Owner: "all characters are 1024x1024 with transparent backgrounds; that's the rule, that's what the assets are."

- **The file.** Every portrait file (`Portraits/<stem>-<NN>.png`, named by §12.1.3) is a 1024×1024 RGBA canvas with a transparent background, exactly as the art comes. `GridGame.Studio/tools/import_art.py` `save_portrait_assets` writes it (and `GridGame.Studio/tools/portrait_cut.py --import` and `GridGame.Studio/tools/remove_bg.py --import` go through it). A 1024² image is kept pixel for pixel. Any other size is cut to its figure (the alpha bounds, so nothing of the figure is lost), scaled down only if that is still larger than 1024, and centred. Nothing is alpha-trimmed or capped at 640 any more. The existing `.meta` (GUID) is kept.
- **Figure.** Each class carries `"Figure": { "X", "Y", "W", "H" }` (`ClassDef.Figure`): the figure's box on its portrait (the alpha bounds), as fractions of the portrait's width and height from its top-left. The import writes it into `classes.json` with a targeted edit, like GridGame.Studio's Thumbnails tab. `ContentLoader.Validate` requires one on every class, with some area and inside the portrait.
- **Whole-figure views draw the figure, not the canvas.** `Art.PortraitFigure(classId)` is the portrait cut to its Figure box as a sprite (cached per texture). Dialogue speakers and NPC icons, the cut-in bust, the pincer cut-in shots, the boss intro, level-up, the hero sheet, the Mercenary renewal and the death dissolve draw it instead of the raw texture, so each figure reads at the size it had before the canvases grew margins. `Crop` still positions within that box. `Art.FigureSize` is the figure's layout size: its box (in 1024-canvas px) fitted inside a 640 square, never enlarged, the size portraits were imported at before. Dialogue draws 640 at `FigureHeight`, so a creature smaller than a person still stands smaller. The mannequin fallback (GG-T351) uses the mannequin's own box.
- **Thumbnails** (`Ui.PortraitThumb`, §12.1.1) frame the whole canvas; since GG-T713 that frame is baked into `Resources/Thumbs/`, the only mipmapped actor art.
- **Male art (GG-T532, §25.11.2.3).** The class's `MalePortrait` file (`acolyte-male-00.png`) follows the same rule; its box is the class's `MaleFigure` (import_art writes it for a `<classId>_male` import) and its framing the optional `MaleThumbnail`; `Art.Portrait` picks the hero's version.
- **Re-import (2026-11-07).** Every portrait was re-imported at 1024. The GridGame2026 originals (`Assets/Sprites/Actor/Portraits`, by the class's `ArtSource` (its `Portrait` before GG-T557) else its `Name`) came in as they are; the 19 RGB originals with solid backgrounds went through `portrait_cut.py`'s smoke cut; the 6 off-size ones were fitted. The owner's pasted art (GG-T328 to GG-T340, in the tickets' attachments) was cut again with the smoke method at full canvas. The 7 portraits with no original anywhere (gravestone, bone pile, corpse, naga egg, hex sniper / warden / stalker) are their old image, unscaled, centred on a 1024 canvas. Each class's Thumbnail was converted from trimmed-image to canvas fractions: with the old image = the canvas cropped to the box (bx0, by0, bw, bh) and scaled uniformly, `X' = (bx0 + X·bw)/1024`, `Y' = (by0 + Y·bh)/1024`, `Zoom' = Zoom·1024/bw`. The box was found per class as whichever of the new alpha bounds or the whole canvas reproduces the old image (refined edge by edge where neither did). Every class was then checked by rendering old and new thumbnails with the game's maths.
- *(Tests: `PortraitCanvasTests`. Every portrait file is 1024×1024 8-bit RGBA with transparent top corners; every class has a Thumbnail in range whose centre lands on its figure; every Figure equals its PNG's alpha bounds. `tools/check_assets.py` fails a portrait that isn't a 1024² RGBA canvas (POL-H1).)*

#### 12.1.3 Asset file names (added 2026-11-21, owner rule, GG-T557/GG-T561)

Owner 2026-11-21: "all assets (png, ogg, w/e) should follow this naming standard 'word-word-00.{ext}' so lowercase, hyphen delimited, and numbered even if there is only 1 they should all start with a `-00`; you never know if you want to start selling Christmas costume variants".

- **The rule.** A file name is a stem (lowercase words joined by `-`) and a two-digit variant from `-00`: `acolyte-female-00.png`. `-00` is the default art; `-01`, `-02`... are alternates of the same thing (a costume). One helper builds every name: `GridGame.Core.Content.AssetNames` (`Stem(words)`, `File(stem, variant)`), and `tools/asset_names.py` is the same rule for the tools. Ids never change; only files are named this way.
- **Heroes.** `<name>-female-NN.png` and `<name>-male-NN.png`, the name being the class's `Name` (Acolyte → `acolyte-female-00`, Panda Keeper → `panda-keeper-male-00`), not its id (`sister`).
- **Enemies.** `<name>-NN.png`. Classes that share one `Name` are looks of the same enemy and number that name's files in id order (the four `Hag`s are `hag-00`..`hag-03`, `cyclops06` is `cyclops-05`); a variant with a `Name` of its own is its own name at `-00` (`harpy-raider-00`, `bulwark-siege-gunner-00`). A new look of an existing enemy is the next free number.
- **The data.** Each class names its files: `"Portrait"` is the default art's stem (`acolyte-female`, `hag`), `"MalePortrait"` the male art's (`acolyte-male`; absent = none yet), `"PortraitVariant"` the number both use (default 0). `ClassDef.PortraitFile(male, variant)` is the one lookup (`Art.Portrait`, `HeroVariants.PortraitFile`, the tools, GridGame.Studio). A class that draws another's art names that stem: the Monster Tamer `wild-child-female`, the Naga Egg `naga` (byte-identical copies were dropped), placeholders `mannequin` (`mannequin-00`). `"ArtSource"` keeps the GridGame2026 art key `import_art.py` reads.
- **Every other image (GG-T561).** Files keyed by an id are the id's words plus `-00`, the id itself unchanged: `AbilityIcons/aegisguard-hands-of-mercy-00.png` (ability `aegisguard_hands_of_mercy`), `ItemIcons/eq-sword-shadow-plus3-00.png`, `StatusIcons/shield-bash-00.png`, `Icons/mass-heal-00.png`, `Tiles/greenvalley-00.png` and the plain `tile-00.png`, `Tutorial/hub-shop-00.png`, `UI/cursor-00.png`, `UI/pk-haste-00.png`. Backdrops: `Backgrounds/<region-words>-NN.jpg` from 00 (`tithe-moors-00`); `campaign.json` names each theme's set by its stem (`"Backgrounds": "tithe-moors"`); `Backdrops.Key(set, n)` builds the key and the legacy `_0`/`_01` numbering is gone. `Art` (`Pixelled`, `StatusIcon`, `BoardTile`, `Icon`, `Cursor`) and `Coach.ImageKey` build every key through `AssetNames`; the tools (`make_icons.py`, `make_tiles.py`, `lesson_images.py`, `import_art.py`) write the same names.
- **Dialogue.** A speaker's `Portrait` names the class whose art it shows (the Lantern Merchant borrows `courier`); the file comes from that class.
- **Saves.** Saves keep class ids and the hero's `Variant`, never file names, so renaming art needs no save migration.
- *(Checks: `ContentLoader.Validate` fails a portrait name off the rule or a missing file; `tools/check_assets.py` fails a portrait file named off the rule and a class whose files don't resolve; `PortraitCanvasTests` checks every file's name.)*

### 12.2 Procedural pieces and placeholders

`Art` generates the window gradients, cursor, glow and orb textures at runtime. **Board tiles (amended 2026-10-10, owner):** GridGame2026's own tile art (`Resources/Tiles/tile-00.png`, its `tile3`; each theme's is `Tiles/<theme>-00.png`, §12.1.3; imported by `GridGame.Studio/tools/import_art.py`), recoloured per campaign biome by `GridGame.Studio/tools/make_tiles.py` (moss, sandstone, murky teal, rust-rose; the caverns keep the original slate) and tinted light/dark per square to keep the checkerboard (`Art.BoardTile(theme)`). A missing texture falls back to a purple placeholder and logs once — a gap is visible, never fatal.

### 12.3 VFX

✅ **GG-T253:** one element/status table (`Resources/Content/vfxstyle.json`) drives every effect, with intensity tiers and timing/easing tokens; code-drawn UI Toolkit, with premade effect prefabs layered on top where mapped (GG-T269, §6.2; the one exception to "no prefabs"). Details and the contact-sheet tool in [§6.2](#62-vfx-pipeline); rules in [`POLISH.md`](POLISH.md) §E (POL-E1–E8).

---

## 13. Combat Resolution

### 13.1 Damage formulas

All in `Core/Rules/Formulas.cs` (ported from the old `Formulas` with engine types removed).

#### 13.1.0 Evasion (defender, GG-T297)

*Added 2026-11-06 (owner: "characters can be built to benefit from dodging hits").* Every unit, hero or enemy, has **Evasion** in percentage points:

```
evasion = clamp( min(25, 0.2·AGI + 0.1·LCK) + Σ buff EvasionBonus + Σ gear EvasionBonus , 0, 40 )
```

A level-1 hero sits near 3.6 (AGI 12, LCK 12), a ninja near 5, a level-30 skirmisher near 9–12; buffs and gear lift it, never past 40. It takes the **top `evasion` points off the attacker's hit band** on the same single Rng draw (`roll ≤ crit% → Critical; ≤ hit% → Normal; else Miss`, then a would-be hit with `roll > hit% − evasion` is **dodged**), so a seed replays identically wherever Evasion doesn't bite. A dodge deals nothing, emits `DodgedEvent` (a "Dodge!" float, a sidestep and a whoosh) instead of the 0-damage Miss, and fires `OnDodge` for the defender.

- **Dodgeable:** enemy melee swings and shots, counterstrikes (Counter Attack, Evasive Riposte) and charged enemy spells (`Formulas.Magic`, §13.1.5).
- **Never dodged:** pincer blows, supporter blows and the follow-ups riding them (the pincer is the game's core payoff), and hero spells/skills, which already never miss (§13.1.2).
- **Sources:** `BuffDef.EvasionBonus` (validated −40..40; `blurred` +15 for 2 turns) and `ItemDef.EvasionBonus` (validated 0..20, lost when an armor piece breaks).
- **Dodge synergies** (Reactive rows in `abilities.json`, on `OnDodge`): **Evasive Riposte** (a [Counterstrike] at 0.5×; on the Duelist's kit), **Catch Breath** (heal 6% MaxHP, once per action), **Blur** (gain [Blurred], once per action). **Amended 2026-11-06 (GG-T302):** carried by the duelist, swordmaster, dervish, martial artist and lurkers (Evasive Riposte), fencer, chroma ninja, speedster, courier, hexstalker and flying monkey (Blur), and thief and white ninja (Catch Breath); see [§4.1.2](#412-signature-passives-gg-t302-added-2026-11-06).
- **Shown:** the InspectCard and battle unit panel stat line (`EVA n%`) at the Bestiary Stats tier, and the Bestiary's Stats fact (from base stats); buff and item text name their bonus; the glossary has [Evasion] and [Blurred].

#### 13.1.1 Physical (pincer, supporter, enemy melee)

```
off   = attacker STR + weapon BaseDamage           def = defender VIT (+ armor rating, unused = 0)
raw   = max(1, max(off − max(0, def − 0.25·off)·0.35, 0.20·off))
raw  ×= defender physical resistance
var   = raw × U(0.9, 1.1) × LCK tilt (0.99..1.01)
hit%  = clamp(80 + 0.5·SPD + 0.2·LCK + 3·(attacker lv − defender lv), 5, 95) × Blind accuracy (×0.5)
crit% = clamp(8 + 0.4·LCK, 0, 50) × Blind accuracy (×0.5)
                                          roll > hit% → Miss; else roll ≤ crit% → Critical; else Normal
                                          (hit before crit, GG-T566: a crit only lands on a hit, so crit% above hit%
                                           never bypasses Blind or the level shift — effective crit = min(crit%, hit%))
clutch = a hero's Critical landed while Blind is a Clutch! (§13.4.1): flash, "Clutch!", +ClutchCharge
dodge = a non-pincer, non-supporter would-be hit with roll > hit% − defender Evasion (§13.1.0)
dmg   = max(1, floor(var × (crit ? 1.6 : 1) × 1.07^(Δlevel) × multiplier))
dmg   = max(1, floor(dmg × incoming multiplier))     (immune if physical resistance ≤ 0)
```

`multiplier` = 0.5 for supporters, 1.5 for a shattering weapon. A Miss deals 0 and never pushes, staggers or wakes.

#### 13.1.2 Spell damage (dispatcher)

`Formulas.SpellDamage`: `round(base × resistance × (Lightning on Wet ? 1.5 : 1) × incoming)`, min 1 unless immune; then × `BonusMultiplier` vs `BonusVsTag` (items). No crit, no miss, no Blind. ⬜ Not yet built: unifying spells with the physical roll (old backlog).

#### 13.1.3 Healing

`Heal`: `HP = min(MaxHP, HP + BaseHeal)`; the event reports the amount actually restored. No resistance, no crit, no overheal.

#### 13.1.4 Cast time

`Formulas.CastTime(base, stats) = max(0.25·base, base × 50 / (50 + 0.5·(WIS + INT)))`, converted to ticks (`round(s × 10)`, min 1). Hero casts only; enemy charges use their authored 2.5 s.

#### 13.1.5 Enemy magic (charge resolution)

`Formulas.Magic`: same mitigation shape with INT vs WIS, element resistance, variance, the shared hit/crit roll and level scaling; plus the charge's flat `SpellDamage` (0 in content). Can miss.

#### 13.1.5 Caster power (role identity)

Added 2026-10-11. A hero's spell damage scales with **INT** (physical skills with **STR**), heals with **WIS**: `1 + (stat − 12) × 0.035`, clamped 0.7–1.6 (`Formulas.CasterPower / HealPower`). A Red Mage (INT 20) hits ×1.28, a Fighter's Fireball ×0.9. Items and enemy abilities keep their tuned numbers. **Empowered** (War Cry, Might Rune) multiplies all damage the bearer deals by 1.3. *(Test: `Caster_power_makes_a_mage_out_hit_a_fighter_and_a_sage_out_heal`.)*

### 13.2 HP delta

`BattleRunner.Damage` clamps HP at 0, emits `DamagedEvent` (amount, hit kind, type, HP after, note), credits hero threat (`Unit.DamageDealt`), kills at 0, then applies pushback and cast stagger.

### 13.3 Death and loot

At 0 HP (`Kill`): off board, buffs cleared, every timeline entry it owns removed (pending casts vanish), `DiedEvent`; enemies award loot (`AwardLoot`):
- **XP** `Formulas.XpReward = round(XpReward + 2·level + 0.10·(STR+VIT) + 0.08·(INT+WIS))`, min 1.
- **Coins**: the drop table's `CoinsMin..CoinsMax` if authored; **Amended 2026-10-09:** otherwise **≈ half the XP, ×U(0.8, 1.2)**, min 1 (no table in content authors coins). Snake segments drop no coins.
- **Items**: each drop-table entry rolls its `Chance` for `Min..Max`.
- **Kills** count by class (bounties, bestiary); snake segments don't count.

The view floats "+N G" and item names from the corpse. Totals accumulate in `BattleResults` and are committed by `BattleFactory.Commit` ([§22.1](#221-per-transition-state-contract)).

#### 13.3.1 Pickups

Added 2026-10-11 (owner: "powerups should sometimes drop"). A fallen enemy leaves a pickup with chance `0.20 + 0.004 × killer LCK` (max 0.40), on its tile or a free one beside it, never more than 3 on the board (`pickups.json`, weighted):

| Pickup | Effect |
|---|---|
| Heart Tonic | heals the collector 25 % of max HP |
| Mana Shard | 1 orb |
| Quicksilver | hastens the collector 0.3 |
| Might Rune | Empowered (×1.3 damage) for 2 turns |

The first hero to enter the tile collects it (walking, being displaced, or teleporting). Enemies don't collect; ~~one may stand on a pickup.~~ **Amended 2026-11-03 (owner direction, GG-T228): a pickup moves like an actor** — any non-hero entering its tile (step, 2×2 lane step, snake, leap, blink, teleport, knockback/pull/displacement) shoves it like a movable tile onto the tile it just left, else the nearest open item-free tile (`PickupMovedEvent`, a slide with a cue; fades only on a full board), so no unit ever shares a tile with a pickup; nothing spawns on one (GG-T227, §1.1.3). **One item per tile, ever (GG-T129, §1.1.3):** a pickup only drops on a tile that holds no other pickup and no movable (`FreeForItem`), the Power Amplifier never spawns or is shoved onto a pickup, and the view never draws two item tokens on one tile or keeps the token of a collected or faded pickup. A pickup fades after **4** player actions (a ring on the token counts down). It pops out of the fallen enemy with a bounce; collecting flies it to the hero. *(Tests: `An_enemy_stepping_onto_a_pickup_shoves_it_into_the_tile_it_just_left`, `A_two_by_two_shoves_a_pickup_to_the_tile_vacated_on_its_lane`, `A_snake_head_stepping_onto_a_pickup_shoves_it_behind_its_tail`, `A_foe_knocked_onto_a_pickup_shoves_it_back_and_a_hero_still_collects`, `A_pickup_with_nowhere_to_go_fades_rather_than_stay_under_a_unit`, strict/release unit-on-pickup; `Stepping_on_a_pickup_collects_it_and_heals`, `A_round_trip_drag_that_only_grabs_a_pickup_still_counts_as_a_move`, `Pickups_fade_after_their_lifetime`, `Fallen_enemies_sometimes_leave_pickups`; items apart: `The_amplifier_never_spawns_on_a_pickup`, `Strict_soak_with_drops_and_amplifier_shoves_never_stacks_items` and the rest of §1.1.3's GG-T129 tests.)*

### 13.5 Armor plates (added 2026-10-11, owner: "some enemies are armored and need to take damage from that side to break it off")

`ClassDef.ArmorPlate` gives a family a plate on the side it **faces** (`Unit.Facing`, turned toward the hero it wants at spawn and at the end of each of its turns — `FacingChangedEvent`). Plate HP = `ArmorPlate × (1 + 0.15 × (level − 1)) × EnemyHpScale` (the scale added 2026-11-05, GG-T262). A **physical** blow (pincer strike, supporter, Cleave, Shield Bash) from the plated side (`SideOf`, dominant axis) dents the plate instead of the body (`PlateHitEvent`, "CLANG! −n"); at 0 the plate breaks off for good (`PlateBrokenEvent`, shards, "ARMOR BROKEN!"). Blows from the flanks or behind land normally; **spells go around the plate**. **Shredded** (§8.1, GG-T857) is the exception to "from the front": each application chips a fixed `PlateChip` off the plate from any side. Families: ceramic knights 30, templars 30, mecha armour 40, soldiers 18, the captain 24. Tactics: pincer it across its line of sight, break the plate head-on, or let the mage burn it. The plate is a riveted steel bar on the tile's rim; the enemy card says where it faces and how much is left. The bot avoids lining an attacker up on the plated side. *(Tests: `A_plate_takes_the_blow_from_its_facing_side_and_the_flank_hits_flesh`, `A_pincer_across_its_line_of_sight_skips_the_plate_and_a_broken_plate_stops_blocking`, `Spells_go_around_the_plate`.)*

### 13.4 The interrupt path

Any landing hit (source not null, not a miss) on a unit with a cast in flight runs `BattleRunner.StaggerCasts(caster, attacker)` — hero casts hit by enemies, enemy charges hit by heroes.

Order per hit:
1. **Clutch** (hero casts only): chance `min(20%, LCK / 200)` → a Clutch! (§13.4.1, kind `CastThroughInterrupt`), then the cast resolves immediately (`ResolveCast`).
2. **WIS shrug**: chance `min(60%, 1.5% × WIS)` → `CastStaggeredEvent(Resisted)` "Steady!", no delay.
3. **Stagger**: `delay = 0.6 s × (1 + attacker STR / 20) / (1 + caster WIS / 15)` → ticks, added to both `RemainingTicks` and `AccumulatedDelayTicks` ("Stagger!").
4. **Cancel**: once accumulated delay ≥ the cast's original ticks, the cast is removed (`CastCancelledEvent` "interrupted", "Interrupted!"); orbs stay spent. Cancelling an **enemy charge** mints one orb ([§3.1.2](#312-harvesting)).

#### 13.4.1 Clutch! (added 2026-11-21, GG-T566)

Owner: *"if a character is blind their chance to crit is lower, but if they do crit when blind it's considered 'Clutch!' which is a bonus for doing really cool things"*; then *"there are other combinations of things that should be considered 'Clutch!'"*.

A **Clutch!** is the game's reward for a really cool moment. Every kind goes through one path, `BattleRunner.Clutch(unit, ClutchKind)`: it emits `ClutchEvent { CasterId, Kind, EntryId }` (the view's white flash + "CLUTCH!" float), announces "Clutch!", and grants the one shared, tunable bonus, **`BattleConfig.ClutchCharge` (20) mana charge** (`ManaChargedEvent` reason `clutch`, shown as "Clutch!" on the charge tally). It draws nothing from the Rng itself: whatever earns it has already rolled, so seeded replays keep their draw count. Heroes only. Kinds today:

- **`CastThroughInterrupt`**: a hero's cast forced through an interrupt by the LCK roll above (step 1); the cast then resolves.
- **`BlindCrit`**: a hero lands a **Critical while Blind**. Blind (`Unit.IsBlinded`, any accuracy-lowering debuff; today `blinded`, ×0.5) lowers the crit chance by the same factor as the hit chance, on weapon blows (`Formulas.RollHit`, [§13.1.1](#1311-physical-pincer-supporter-enemy-melee)) and on hero spell crits (`SpellCritFraction` × accuracy), so a Blind crit is rare. Hit-before-crit alone would not have lowered it (it only caps crit at the halved hit chance, which a typical 10-30 % crit sits under). The crit still earns its usual `CritCharge` too.

**Added 2026-11-21 (GG-T571, owner: "do", on the recommendation):** five more kinds, heroes only, deterministic (no Rng draw of their own), each a new `ClutchKind` on the same path:

- **`LastStand`**: a hero at or below **`BattleConfig.ClutchLastStandHpFraction` (15 %)** of max HP lands a killing blow.
- **`Finisher`**: a hero's blow fells the last foe, clearing the wave or winning the battle (`WaveClearedEvent`, then the Clutch). The last foe has to be a real one: when only remains or an egg were left to clear, or the wave ended to a tick, a hazard or a foe's own doing, there is none.
- **`DoublePincer`**: one hero move (a drop, or a teleport) closes **`BattleConfig.ClutchPincersInOneMove` (2)** or more pincers that strike; awarded to the mover as the pincer that reaches the count forms.
- **`HazardKill`**: a hero's knockback or pull carries a foe into an open hole (the fall) or onto a region danger (a rule's `Damage` effect or drag collision), and that hazard kills it in the same command. Its own Slam/Crash damage is the hero's blow, not a hazard.
- **`LastTickInterrupt`**: a hero's hit **cancels** a foe's charge ("Interrupted!") when the charge had at most **`BattleConfig.ClutchLastTickSeconds` (0.5 s)** of timeline left before landing (measured before this hit's stagger).

Kill-based kinds (`LastStand`, `Finisher`, `HazardKill`) never count a revived foe's second death (`Unit.AlreadyLooted`), remains, eggs, corpses or snake segments.

**One Clutch per action; the first to qualify wins.** An *action* is the hero's command itself (`BattleRunner.Apply`), then each arrival on the timeline that command runs into (a cast resolving, a turn, a rule event: `BeginClutchAction`). A Clutch that would follow another in the same action is dropped whole (no float, no bonus); a dropped `CastThroughInterrupt` still forces its cast through. The kinds come up in the order the action happens:

1. `DoublePincer`, as the move's second pincer forms (after the first pincer's blows);
2. at each kill inside a blow: `LastStand` (a hero dealt it) or `HazardKill` (a hole or region danger did);
3. after the blow lands: `BlindCrit`; on a hit on a caster: `CastThroughInterrupt` (a hero's cast) or `LastTickInterrupt` (a foe's charge);
4. `Finisher`, when the wave end is checked after the action's blows.

So a Blind crit that kills at 10 % HP reads as `LastStand`, and a double pincer whose first pincer lands a Blind crit reads as `BlindCrit`.

**Tuning:** the three numbers above live in `BattleConfig`; `BattleConfig.ClutchDisabled` switches any kind off (0 / below 2 also switch the numeric ones off). The bonus stays the one shared `ClutchCharge`. The view shows the kind's glossary headword small under "CLUTCH!" (`glossary:clutch_<id>`, the "Clutch: " prefix dropped). Stable ids (`ClutchKinds.Id`, glossary `clutch_<id>`): `cast_through`, `blind_crit`, `last_stand`, `finisher`, `double_pincer`, `hazard_kill`, `last_tick`. Replays carry the kind by name on `ClutchEvent`; nothing new lives in `BattleState` (the bookkeeping is the one-command runner's).

**Not adopted (future candidates, GG-T571):** a mana rollover that lands exactly on an orb; a kill with the blow that shatters the weapon; a Taunting tank surviving a hit at 1 HP; a kill while the hero is debuffed.

**Enemy charges** ([§14.2](#142-enemy-archetypes-design-palette)): `EnemyPlanner.PlanCast` lets a unit with a `ChargeAbility`, not immobile and **not adjacent to any hero** (unless its boss phase `PrefersCharge`), queue a charge on the nearest hero instead of moving. **Amended 2026-10-09:**
- Single-target charges **lock onto the hero**, wherever it moves — dodge only by interrupting (or killing).
- Line charges (`ChargeShape.Line`) lock the cardinal line from beside the caster to the edge on the dominant axis toward the target (X on ties); at resolution every hero still on those tiles is hit ("Dodged!" if none). Edge-pinned lines fall back to single-target. 🟡 Built in Core, but no enemy in `classes.json` uses `ChargeShape: Line` yet (only hero-class rows carry it).
- A charging enemy that gets another turn before its charge lands moves/attacks normally (no second charge).

*(Tests: `Enemy_charge_is_telegraphed_and_can_be_interrupted`, `Every_enemy_charge_telegraphs_and_resolves`.)*

---

## 14. AI

### 14.1 Enemy planning

`BattleRunner.RunEnemyTurn(enemy)`, when its icon arrives:
1. Mint the pending time bank (first enemy turn of the action) and emit `TurnStartedEvent`.
2. **Boss phases** advance ([§14.3](#143-ai-hooks)).
3. **Immobile** → skip the turn (end-of-turn ticks still run).
4. **Charge**: if not already casting, `PlanCast` → queue a charge and end the turn.
5. **Trap**: a trap-layer with no hero adjacent rolls `LayChance` (0.45) to arm a free, trap-free cardinal tile, ending the turn.
6. **Pincer plan** (GG-T306, [§14.1.3](#1413-pincer-planning-gg-t306)): a pincer-capable 1×1 foe first asks `EnemyPlanner.PlanPincer`. A walk that **completes** a pincer is taken at once, before step 4's charge; a walk that **sets up** an end or **joins** an ally pincer as a supporter is taken here, after the charge and trap checks. Either way it then goes straight to step 8's attack.
7. **Move** up to `MoveRange` steps (all content: 1 or 2), re-planning each step with `PlanStep`; a multi-tile step shoves heroes ([§1.5](#15-multi-tile-actors-22-bosses)); a snake head drags its chain.
8. **Attack**: a Sentient 1×1 enemy (`Unit.CanPincer`, [§1.3](#13-sentient-restriction); was Humanoid before GG-T289) that is now an end of an enemy pincer resolves those pincers ("X pincers!"); otherwise it melees the adjacent hero its **targeting personality** picks (below). No adjacent hero → no attack.
9. End of turn: its non-halting turn buffs tick; battle-end check.

**Targeting personalities (amended 2026-10-10, owner direction: "smarter enemies attack the weakest, dumb enemies the closest").** `ClassDef.Targeting` drives movement (`PlanStep`), melee choice, ranged choice and charge targets (`EnemyPlanner.PickFrom`); ties fall back to distance, then HP, then id:

| Personality | Picks | Families |
|---|---|---|
| Closest | nearest hero | slimes, undead, cyber-zombies, skelepedes, tree golems, mannequin, frogs, toad, sandmaw, cyclopes, troll, yeti, shamblers, ceramic knights, templars, naga |
| Weakest | lowest current HP, even across the board | wolves, werewolf, vulture, hags, swamp mistress, reaper, phantom, lurkers, tech gremlins, purple prototypes |
| Threat | hero who has dealt the most damage (Closest until someone has) | captain, soldiers, demon lord, oni, mecha armor, ice mauler |
| Squishy | lowest VIT (then MaxHP) | gangers, steppin' razors, scorpion, goblin thug |
| Erratic | a different hero each turn (hash of clock + id; no RNG draw) | bats, flying monkey, ghost |

`Auto` (unset) derives Weakest for INT ≥ 14, Threat for Sentient enemies (Humanoids before GG-T289), else Closest. The enemy card shows it ("Hunts the weakest hero").

**Speed tiers (amended 2026-10-10):** enemy Speed is 18 (swift: bats, wolves, flyers, werewolf, steppin' razors, phantom), 15 (quick), 11 (steady) or 8 (slow: slimes, undead, zombies, golems, mannequin, ghost). ~~Open-track rate scales with Speed and the Zone is a shared crawl.~~ **Amended 2026-11-06 (GG-T286):** the rate scales with Speed the whole way, so a fast enemy can overtake a slow one anywhere on the bar.

`EnemyPlanner` is pure: it reads state (temporarily moving the enemy's `Pos` to score a candidate, then restoring it) and never commits.

#### 14.1.1 Decision tree

```
PlanStep(state, enemy)
  immobile or snake segment? ─ yes ─▶ stay
  target = PickTarget: argmin( distance + 8·HPfrac − threat·INT·0.8 )
  candidates = {stay} ∪ {4 cardinals that are in bounds & free (1×1) | FootprintStepLegal (2×2)}
  for each c:   wounded (HP < 30%) ? score = +dist : score = −dist
                not wounded & adjacent to target here      +2
                1×1 & would be sandwiched by two heroes    −100
                c == stay                                  −0.5
                not wounded & Sentient 1×1 & forms pincer  +50
                not wounded & becomes ally-pincer supporter +25
  return argmax(score)
```

The −100 sandwich avoidance (two heroes directly on opposite sides) outweighs the +50 pincer-seek: enemies pick safe pincers, not suicide pincers.

#### 14.1.2 Score weight cheat sheet

| Factor | Weight | Notes |
|---|---|---|
| Distance to target | −1 per tile (+1 when wounded) | footprint-aware Manhattan |
| Adjacent to target | +2 | dropped when wounded |
| Sandwiched by heroes | −100 | immediate neighbors on one axis; 1×1 only |
| Stay put | −0.5 | mild advance bias |
| Forms own pincer | +50 | Sentient 1×1 (`CanPincer`, GG-T289), not wounded |
| Supports an ally's pincer | +25 | any enemy, not wounded |
| Threat (target choice) | −(DamageDealt / max) × INT × 0.8 | smart enemies hunt the top damage-dealer |
| Wounded retreat | below `RetreatHpThreshold` 0.30 | flees; flank-avoid still applies; never against a lone hero (GG-T123, [§22.10](#2210-the-crucible)) |

Constants live in `EnemyPlanner` and `BattleConfig.RetreatHpThreshold`.

#### 14.1.3 Pincer planning (GG-T306)

**Added 2026-11-06 (owner):** "The heroes and enemies with 'Pincer' keyword not only line themselves and complete pincers, they actively try to make pincers by swapping with monsters or heroes to set up a beneficial situation with enemies, themselves, and their supporters — so every monster needs support abilities."

`PincerPlanner` (Core/Rules) is one shared, pure planner for any unit that can pincer (`Unit.CanPincer`: heroes, Sentient foes, tamed or domesticated monsters).
- **Search.** It tries every walk of up to `min(steps, MaxSearchSteps = 3)` cardinal steps, never revisiting a tile. Each step either enters an empty walkable tile or **swaps** with the one-tile unit standing there, friend or foe. The occupant takes the tile just left, exactly like a hero drag ([§1.1](#11-sliding-and-displacement)). Units that can't be swapped: multi-tile actors, snake members, remains, corpses and neutrals. A hero-team walk never steps on a trap. That is at most 52 walks per unit, each scored on the board it leaves. The planner moves `Pos` to look and restores it, never draws from the Rng, and tries walks in `Coord.Cardinals` order; the first of equal scores wins, so it is deterministic.
- **Score** (`Evaluate`, the board after the walk):

| Outcome | Kind | Worth |
|---|---|---|
| The mover is an end of a pincer with live opponents (a commanded hero: any hero pincer, since its drop resolves them all) | **Complete** | `OpponentValue` 10 per live, unarmored opponent, +3 for one below 40% HP, plus each supporter's `SupportWorth` (so an ally swapped into the chain counts) |
| No pincer, but it stands at one end of an unbroken opponent line whose far tile is open (empty, or a non-pincer ally that can be swapped out) and a pincer-capable ally can reach that tile on its next action (a foe or tamed beast: its `MoveRange`; a commanded hero: the drag that fits the window) | **Setup** | `SetupBase` 6 + 1 per opponent in the line |
| It joins the chain of an ally pincer already on the board | **Support** | 0.6 × its `SupportWorth` (added to the other kinds too) |
| Each ally newly trapped in an opponent pincer | — | −12; the mover itself trapped −40 (a Berserker or Bomber foe ignores the −40) |
| Each step / each swap | — | −0.25 / −0.25 |

- **`SupportWorth`** prices a supporter by its class's `SupportDef` ([§1.2.3](#123-supporters)): Strike 2 + 4×magnitude, Heal 2 + 10×, Buff 3, Haste 2 + seconds, Mana 2 + charge/25, Cleanse 2, Crit 2 + 6×, Cast 2 + 4×, Status 2 + 3×chance, Pushback 2 + 2×seconds. This is why a healer behind an attacker can beat a second striker. Every enemy class must carry its own `Support`: `ContentLoader.Validate` rejects one that doesn't ("an enemy needs its own Support"), and all 115 do since GG-T307.
- **Foes** (`EnemyPlanner.PlanPincer`): a Sentient 1×1 foe that isn't immobile and isn't wounded enough to retreat plans with its `MoveRange`. A Sentinel, an Ambusher still lying in wait, or a Sluggish foe on its rest turn plans with 0 steps (it only pincers in place). An evasive brain (Kiter, Commander, Trapper, Medic) takes only a **Complete** walk. A walk is taken only when its score is positive. `BattleRunner.FollowPincerPlan` walks it with `SwapStepEnemy` (an empty tile is an ordinary step; an occupied one swaps, `DisplacedEvent` by the foe, and a hero shoved onto a trap springs it). With a swap it announces "X maneuvers!". Then it attacks, closing the pincer if it now stands at an end. Mindless foes (beasts, slimes, yetis…) keep the plain `PlanStep`: they never swap.
- **Tamed monsters** (`RunAllyTurn`, [§25.11.3](#25113-taming-and-domestication-added-2026-11-06-owner-gg-t303)) plan with the same planner on the hero side and walk the plan with the hero drag's `WalkPath`. They complete a hero pincer, stand at an end a hero can close, or slot into a chain. They still act on their own and shove heroes aside to do it, which can wreck a setup the player had in mind. With nothing to engineer, a tamed monster falls back to charging the nearest foe. A ranged one that already has a shot takes only a Complete walk.
- **The smart bot** (`HeroBot`, timeline-aware) adds the planner's walk to its drag candidates whenever it is not one of the straight L-drags it already previews. The walk is capped at the drag that fits the gap (`MaxDragSteps` with think time, GG-T311), and is previewed and scored like any other drag. The bot also weighs each supporter of a previewed pincer by 0.6 × `SupportWorth` instead of a flat 2.
- *(Tests: `PincerPlannerTests` — `A_sentient_enemy_swaps_with_an_ally_to_complete_a_pincer`, `A_sentient_enemy_swaps_to_set_up_an_end_an_ally_can_close`, `A_mindless_enemy_never_swaps_to_pincer`, `A_tamed_monster_moves_to_close_a_hero_pincer`, `The_hero_planner_finds_the_walk_that_closes_a_pincer`, `Planning_and_the_turns_it_drives_replay_identically`, `Every_real_enemy_class_brings_its_own_support`.)*

- **Balance (GG-T306).** Per-stage 8 soak (`AbilityUsageSoak`, 15 campaign stages, same seeds), smart / casual bot wins, before → after:

| Difficulty | Before (GG-T311) | Planner only (pincer push 1 s) | Shipped (pincer push 1.5 s) |
|---|---|---|---|
| 10 | 79% / 33% | 72% / 22% | **76% / 31%** |
| 5 | 99% / 85% | — | **99% / 78%** |
| 1 | 100% / 98% | — | **100% / 98%** |

  Engineered enemy pincers alone cost the casual bot 11 points at difficulty 10, which put it below the 30–45% band. `PincerPushbackSeconds` 1 → **1.5 s** brings it back: every hero pincer buys the party half a second more against foes that now set up their own pincers. Difficulty 10 by theme, smart bot: greenvalley 83 → 71, desert 92 → 92, swamp 67 → 62, cave 88 → 79, cityruins 67 → 75. The smart bot wins at least one battle on every stage. Casual, by theme: greenvalley 25, desert 42, swamp 25, cave 58 → 46, cityruins 17.
  The soak also exposed two stalls, fixed here. A wounded foe fleeing into a **corner** can't be pincered on either axis, so a buff-only pair could never finish it; a fleeing step now scores −`FleeCornerPenalty` (3) for a corner tile. And a smart bot whose bar held only buffs never counted as locked out, so with 1 s gaps it never dragged; it now also hurries (no think time) after `StallHurry` (4) windows without drawing blood. `BattleConfig.EnemyPincerSetups` and `EnemyPincerSwapsHeroes` (both on) are soak knobs (`GRIDGAME_TUNE`). Turning both off measured casual 31% at difficulty 10 with a 1 s push.

#### 14.1.1 Taunt

Added 2026-10-11 (owner: "a tank should be able to taunt enemies"). While any hero carries a buff with `Taunts` (Taunting, 2 turns, −25 % damage taken), `EnemyPlanner.PickFrom` only considers taunting heroes among those in reach — so walking, charging and ranged foes all go for the tank, and a taunter out of reach doesn't save an adjacent ally. Synergy: a hit staggers a hero's cast, so a taunting tank keeps the mages' spells charging. *(Tests: `A_taunting_hero_draws_every_enemy_that_can_choose`, `Taunt_only_overrides_among_heroes_in_reach`.)*

### 14.2 Enemy archetypes (design palette)

| Archetype | Lean | Tags | Behavior today |
|---|---|---|---|
| Rusher | high SPD, low VIT | Beast | loads fast; closes and melees |
| Bruiser | STR + VIT | Sentient | slow, tough; best pincer target |
| Flanker | mid STR, AGI | Sentient | seeks own pincers (+50), supports (+25) |
| Ranged | AGI, INT | Sentient | ⬜ Not yet built: keep-distance behavior and ranged attacks |
| Caster | INT, low VIT | Magic (+ affinity) | charges when no hero is adjacent; `icemauler` (`charge_ice`) is the live caster (cave-02, cave-03) |
| Beast | varies | Beast | never pincers; rushes |
| Mechanical | VIT, immunities | — | 🟡 resistances supported; no class authors them yet |
| Boss | everything | 2×2 + `BossPhases` | `cyclops00`: phases + enrage ([§14.3](#143-ai-hooks)) |
| Line caster | as Caster | `ChargeShape: Line` | 🟡 built in Core, no enemy authored ([§13.4](#134-the-interrupt-path)) |
| Trap-layer | varies | `Trap` | `scorpion` lays `venom_snare` (6 Poison + Poisoned) |
| Segmented snake | boss | `SnakeSegments` | `naga00` (3 segments) — see below |
| Heavy | VIT | `ClassDef.Heavy` | **GG-T891 / GG-T914:** never displaced by anyone else: a hero drag step into it is refused (`StepBlockedEvent` "heavy"); knockback, pull and swap fizzle ("Too heavy!"); a rule `MoveUnits` and a hole swap skip it; `PincerPlanner.Swappable` and the hero bot's shove/pull/swap scoring respect it. A unit carrying the buff id `floating` (GG-T859) is movable again (`Unit.IsHeavy`, `Unit.WontBudge`). All one tile: `heavytrooper02` (Bulwark Shieldbearer), `mechaarmor00`, `mechaarmor01`, `mechaarmor02`, `mountaintroll`, `crystalbrute`, `anchorhulk`, `minotaur02` (Herd Bouncer), `treegolem00`-`treegolem04`, `treegolem06`. *(Tests: `HeavyTests`.)* |

**Traps** (`BattleState.Traps`, `traps.json`) — **Amended 2026-10-09:** visible tiles armed by a trap-layer; triggered by a **hero** entering the tile in any way — stepping, being displaced by a slide, being shoved by a boss, or teleporting — once, then removed (damage is environmental; buff applied). Enemies never trigger traps. Trap tiles are excluded from Teleport, Revive and spawn.

**Segmented snake** — a class with `SnakeSegments` n grows n body segments on free adjacent tiles at spawn (`GrowSnake`, preferring up/left/right/down); each segment has half the head's MaxHP, no timeline icon and no plan, and follows the head's vacated trail front-to-back (`FollowSnake`). **Tail-first:** any member is **armored** (damage set to 0, "Armored!") while a later chain member lives. Chain members are immovable to drags. Segments award XP but no coins or kills.

### 14.3 AI hooks

- **Casting enemies** — [§13.4](#134-the-interrupt-path).
- **Supporter positioning** — `WouldSupportAllyPincer` (+25), reusing `PincerDetector` supporters; since GG-T306 the pincer planner also swaps allies into a chain ([§14.1.3](#1413-pincer-planning-gg-t306)).
- **Boss scripted phases** — **Amended 2026-10-09:** `ClassDef.BossPhases` is data (`HpThreshold`, `PrefersCharge`, `OnEnterAbility`, `Announce`). At the **start of the boss's own turn**, every phase whose threshold the HP fraction has dropped to is entered in order: `BossPhaseEvent`, its announcement, and its on-enter ability resolved on itself. `cyclops00`: phase 0 at ≤ 100% ("Cyclops", on its first turn), phase 1 at ≤ 50% ("ENRAGED!" + `enrage`, self-Hasten 0.35). `PrefersCharge` lets a phase charge even in melee range (no content uses it yet).
- **Threat tracking** — `Unit.DamageDealt` (hero damage dealt this battle) feeds `PickTarget`.
- **Retreat** — below 30% HP.

---

### 14.4 Enemy kits (added 2026-10-10, owner direction: "every enemy with its own unique behaviours and spells and whether they lay traps")

Generic, data-driven mechanics (`ClassDef`), so each family's kit is content, not code:

| Mechanic | Field | Effect |
|---|---|---|
| Trail | `Trail` (trap id) | the tile it steps off becomes that trap (max 3 per enemy, oldest removed) |
| On-hit | `OnHitBuff`, `OnHitChance` | a landed attack (melee, ranged, pincer) may inflict the buff |
| Death burst | `DeathBurst` | on death, damages every adjacent hero ("self-destructs!") |
| Passives | `Abilities` | `lifesteal` (heals a quarter of the damage dealt, the row's `Lifesteal` Amount 0.25; it was half before GG-T282), `tough_hide` (physical blows land at 70%), `berserk` (+35% damage below half HP), `regenerate` (6% MaxHP at the end of its turn), `double_attack`, `counter_attack`; **GG-T302** adds signature rows per family ([§4.1.2](#412-signature-passives-gg-t302-added-2026-11-06)) |
| Support charges | `ChargeAbility` with `Filter: AllyOnly` | Howl (allies in 2 tiles hasten 0.25u — only when another ally in range is already at a hero, GG-T123), Rally (allies gain Protection), Mend (heal the most wounded ally 30 — only when one is below 70%) |
| Area charge | `ChargeAbility` with an area `Shape` | Poison Cloud: 3×3, 6 poison + Poisoned |
| Traps | `Trap` | Poison Cloud (miasma), Shock Mine (10 lightning + Slowed), Caltrops (8), plus the trail traps Slime Trail / Mire (Slowed) and Quicksand (4 + Slowed) |

Family kits: slimes leave a Slime Trail · bats move 2, lifesteal · wolves Howl · werewolf moves 2, berserk, lifesteal · vulture & lurkers move 2 (lurkers' bites poison) · flying monkey moves 2, blinds · frogs/toad/skelepedes/scorpion/naga poison on hit · hags cast Poison Cloud and lay it as a trap · swamp mistress Mends · marsh shamblers leave Mire, tough hide · undead & reaper lifesteal · cyber-zombies self-destruct (14) · ceramic knights, templars (Smite), mecha armor (Thunder) and cyclopes have tough hide · tree golems regenerate · purple prototypes & oni go berserk · tech gremlins lay Shock Mines · gangers blind · steppin' razors move 2 and strike twice · goblin thug lays Caltrops · soldiers & mannequin counter · the captain Rallies · demon lord casts Fireball, lifesteals · phantom moves 2, silences · ghost slows · ice mauler & yeti may freeze · mountain troll regenerates and goes berserk · sandmaw leaves Quicksand, berserk. The enemy card and the bestiary list each kit (`Ui.KitTraits`).

### 14.5 Remains — gravestones and bone piles (added 2026-10-10, owner direction)

Owner: "some undead enemies should spawn a solid grave stone like in Darkest Dungeon where you have to delete the gravestone / pile of bones to be able to finally defeat the enemy, or after a while they will resurrect."

- `ClassDef.Remains` names the marker an enemy leaves: hooded **undead → Gravestone**, **skelepedes → Bone Pile**. On death (after its loot), the remains spawn on the same tile as an enemy unit tagged `Remains` (`RemainsLeftEvent`).
- Remains are **solid** (can't be dragged through or knocked back), never move or attack, are immune to status effects, give no loot or XP and never appear in the bestiary. They have their own slow timeline icon (Speed 5) — the countdown to resurrection.
- **Destroy them** (pincer or spell) and the undead is laid to rest for good. A wave is not cleared while remains stand.
- **Leave them** and when their icon reaches the left edge the undead **rises again** on that tile at half HP and drops nothing a second time (`RoseAgainEvent`); kill it again and it leaves fresh remains.
- **Amended 2026-11-05 (GG-T261):** remains rise on their **third** turn (`BattleConfig.RemainsTurnsToRise` 3; the first two only announce "stirs… N"), with **25 %** of the slain owner's MaxHP (`RemainsHpFraction`), and a snake's body segments leave none (one marker per creature). Found in the battle records: since GG-T188 every action runs the clock to the next enemy turn (§2.0), so a marker left by the last foe standing rose **inside the killing action** (median 0 player actions between remains and rising), and at the owner's own level a gravestone had ~110 HP — the casual bot won 0 of 6 on every Anchor Wastes and Halcyon stage and on Tithe Moors III. See AMENDMENTS 2026-11-05.
- **Amended 2026-11-21 (GG-T566): a revived enemy pays out once.** Enemies may revive (an echoed revive raises the caster's own fallen ally on the same unit). `Unit.AlreadyLooted` is set on an enemy's first death and survives the revive, so its second death awards no loot/coins/XP, no pickup roll, no kill tally (`Results.Kills`), no bounty credit and leaves **no remains** (nothing that could rise again). On-kill/on-ally-KO triggers and region death rules still fire. (A remains *rising* is unchanged: that is a new unit, already `NoLoot`.)
- Art: procedurally drawn gravestone and bone pile (ImageMagick).
- *(Tests: `A_slain_undead_leaves_solid_remains_that_block_the_wave_from_clearing`, `Remains_left_alone_raise_their_owner_at_half_hp_without_loot`, `Remains_are_solid_and_real_undead_leave_them`.)*

### 14.6 Brains — every family thinks differently (added 2026-10-11, owner: "come up with ai for every single enemy unit, they are all different in their own unique ways")

`ClassDef.Brain` sets how a family moves and decides, on top of its targeting personality (§14.1) and kit (§14.4). `EnemyPlanner.PlanStep` scores tiles with the brain's taste (`BrainScore`); `BattleRunner.BrainTurn` handles brains that change the shape of a turn. The enemy card and bestiary describe it.

| Brain | Behaviour | Families |
|---|---|---|
| Hunter | closes on its prey (the classic) | purple prototype, yeti |
| Pack | +1.5 per kin beside it — moves as a group | wolves, gangers, skelepedes |
| Ambusher | holds still until a hero is within 2 tiles, for at most 3 of its own turns (`AmbushPatienceTurns`, GG-T262) | lurkers, naga, scorpion |
| Skirmisher | adjacent at turn start → strikes first, then darts away | bats, flying monkey, steppin' razors |
| Kiter | holds ~3 tiles from heroes (melee kiters keep charging spells) | hags, mecha armour |
| Guardian | stays beside its most valuable ally (boss, caster, commander, medic) | ceramic knights, templars, ice mauler |
| Berserker | charges, ignores flanking danger, never retreats | cyclops, oni, werewolf |
| Scavenger | hunts the wounded, retreats at 50 % HP | vulture, reaper |
| Trapper | keeps ~3 tiles off to lay traps on the approach | goblin thug, tech gremlins |
| Sluggish | moves every other turn | slimes, marsh shamblers, tree golems |
| Hopper | can leap two tiles, over units or blocks | frogs, toad (toad's tongue slows) |
| Blinker | every 2nd turn appears beside its prey and strikes | ghost |
| Burrower | every 3rd turn tunnels under and bursts up beside its target | sandmaw |
| Bomber | dives where it touches the most heroes (it explodes) | cyber zombies |
| Medic | stays one tile from its most wounded ally, away from heroes | swamp mistress |
| Commander | stays ~3 tiles back with an ally between it and the heroes | captain, demon lord |
| Duelist | wants exactly one hero adjacent; avoids being outnumbered | soldiers |
| Stalker | targets casters (new `Caster` targeting), avoids crowds | phantom |
| Sentinel | never moves | mannequin, remains |
| Relentless | never retreats | undead, mountain troll |

No two families share brain + targeting + kit (`Every_enemy_family_plays_differently`). Enemy physical strikes carry `EnemyStrikeMultiplier` 0.9 (retuned after the brains with `BalanceReport`). **Amended 2026-11-05 (GG-T262):** enemies spawn with `EnemyHpScale` **0.45** of the MaxHP formula (and plate, §13.5) and strike at **1.2** — faster fights, same threat (AMENDMENTS 2026-11-05). *(Tests: `An_ambusher_waits_until_a_hero_comes_close`, `A_sentinel_never_moves_and_a_sluggish_foe_moves_every_other_turn`, `A_hopper_leaps_two_tiles_over_a_unit`, `A_bomber_dives_where_it_touches_the_most_heroes`, `A_skirmisher_strikes_then_darts_away`, `A_burrower_surfaces_beside_its_target`, `A_guardian_stays_by_its_ward`.)*

### 14.7 Corpses (added 2026-10-11, owner: "add in the corpses for some things so they get in the way like Darkest Dungeon")

Beasts and humanoids of several families (wolves, gangers, soldiers, oni, werewolf, mountain troll, yeti, goblin thug, toad, captain, lurkers, scorpion) leave a **corpse** (`Remains: "corpse"`, tags Remains + Corpse) on their tile: it blocks movement like any remains, sits in pincer lines (a pincer through it clears it — "Cleared away"), takes area spells, has no turn and never rises, **does not hold up a wave clear**, earns no mana or loot, and **rots away after 6 player actions**. It's drawn as the fallen unit's own art, upright and greyed (amended 2026-10-19, owner: "don't turn monsters sideways" — it was rotated 90°); its card says whose body it is and when it rots. *(Tests: `A_fallen_foe_leaves_a_corpse_that_blocks_the_way_but_not_the_win`, `A_corpse_blocks_movement_and_rots_away`.)*

### 14.8 Reinforcements and eggs (added 2026-10-12, owner direction)

Owner: "sometimes enemies can lay eggs that hatch, sometimes new enemies don't spawn on the first turn... every wave can offer a surprise like fighting monsters then suddenly soldiers show up."

**Delayed mid-wave spawns.** `SpawnDef.DelayTicks` (default 0 = today's immediate spawn, unchanged for every untouched wave) queues an entry into `BattleState.PendingSpawns` instead of spawning it at wave start; `BattleRunner.Tick` resolves any entry whose `TriggerClock` has arrived via `ResolvePendingSpawns`, spawning it exactly like a normal wave arrival. A wave with an unresolved pending entry **holds the wave open** even once the board is clear (`CheckEnd` checks `PendingSpawns` alongside living units), so the wave never advances out from under a reinforcement still inbound.

**Never an empty board (GG-T512, owner 2026-11-21).** A wave is never held open on an empty board: whenever no living foe is on the board (bodies and neutrals don't count) and the wave still has a queued spawn, `BattleRunner.BringReinforcementsForward` moves the **earliest-queued group** (every pending entry sharing that `TriggerClock`) up to the current clock and lands it at once through the normal placement rules (its own tile, else a free one; a footprint with no room anywhere is dropped so the wave can end). Later groups keep their own trigger tick and are pulled forward the same way if the board empties again. It runs when the last foe falls (`CheckEnd`), at the end of `SpawnWave` (a wave of only delayed spawns opens with its first group on the board) and at the top of every `Tick`. The arrivals enter the timeline as any mid-battle arrival does (their Speed's entry from now, spaced) and play the usual `ReinforcementsArrivedEvent` beat. The owner chose this over a "Reinforcements incoming" status line: the fresh-player run (GG-T494) had sat through a Taunt and seven GUARDs on a clear board in Tithe Moors I's wave 4, with nothing saying why.

**The surprise beat.** `WaveDef.SurpriseSpeaker`/`SurpriseLine` (optional, wave-scoped) fire once — on the *first* delayed entry a wave triggers, not per enemy — as a `ReinforcementsArrivedEvent`. The App layer plays a quick, per-class entrance (burst-up for `Brain.Burrower`, drop-from-above for `Flying`-tagged classes, fade-in for `Undead`-tagged classes, the ordinary wave fade+drop otherwise — derived entirely from existing `Tags`/`Brain`, no new content fields needed), then, if a line is set, a held narrative card: the speaker's portrait and name beside the line (reusing the same 360×400 bordered-frame convention as the character-select reveal and the level-up fanfare), dismissed by a tap or ~2.6s, same dual-exit idiom as the story crawl. Tithe Moors I's wave 4 is the flagship example: a goblin/bandit skirmish that a captain's soldier squad reinforces mid-fight. Automation: `/demosurprise?speaker=&line=` plays the beat on demand for testing.

**Eggs.** An egg-laying class sets `ClassDef.LaysEgg` (the egg's class id) and `EggInterval` (lay every Nth own turn, default 4); `BattleRunner.Enemy.LayEgg` drops the egg on a free adjacent tile, mirroring how `LeaveRemains` drops a grave marker. The egg class is tagged `Egg` (`Unit.IsEgg`, the `IsRemains` pattern's sibling) and is otherwise built exactly like a gravestone/bone pile (`Brain.Sentinel`, immobile, no turn except its own). When the egg's timeline turn arrives, `Hatch` spawns its `RisesAs`/`RisesLevel` class on the same tile **at full HP** (a fresh enemy, unlike a risen undead's half-HP second life) and leaves normal loot. `naga00` (Arkgrave II's solo finale) is the one showcase of this so far. *(Tests: `A_delayed_spawn_arrives_partway_through_its_wave_not_at_wave_start`, `A_waves_reinforcement_dialogue_fires_once_even_with_multiple_delayed_entries`, `An_egg_hatches_into_its_real_enemy_at_full_hp_on_schedule`, `With_no_foe_on_the_board_the_earliest_queued_reinforcement_group_lands_now`.)*

### 14.9 Rule-breaking bosses (added 2026-11-05, owner direction, GG-T234)

Owner: "a boss who 'breaks the rules' as far as standard gameplay goes." Each main-biome finale (the third stage of Tithe Moors, The Lantern Road, Shardfall, Arkgrave, Anchor Wastes, The Still, Halcyon and The Choir's Cathedral) has one boss that breaks **one** standard rule, each a different one (but see the GG-T344 amendment below), each with a counter the player can read.

- **Data:** `SpawnDef.RuleBreak` (`RuleBreak` enum: `None`, `TwinTurn`, `AxisWard`, `MireSwap`, `GravityStamp`, `Undying`) on that one spawn in the finale wave, so the same class plays by the normal rules everywhere else (the Captain fights in many stages). `ContentLoader.Validate` (`RuleBreaks.Validate`) refuses a rule-break on a guest and a `MireSwap` on a multi-tile or snake body. Copied onto `Unit.RuleBreak` at spawn (immediate or delayed); `Unit.RuleBreakCount` keeps its bookkeeping.
- **Telegraph:** on arrival, a `RuleBrokenEvent` (`Arrived`) bannering the broken rule and the counter ("The Mecha Armor ignores the pincer rule: side-to-side pincers glance off. Pincer it top to bottom!"), with a war cry, a screen shake and a **RULE BREAKER** float. Every bite after that is another `RuleBrokenEvent` with its own cue and float (DOUBLE TIME! / WARDED! / SWAP! / STAMP! / UNDYING!). All of it runs through `BattleEngine.Apply` → events (`BattleRunner.RuleBreak.cs`).

| Finale | Boss | Rule it breaks | What happens | Counter |
|---|---|---|---|---|
| Tithe Moors III | Captain | the turn order (§2) | **TwinTurn:** after its own turn, if no hero stands beside it, it marches and strikes again at once (no second cast, trap or buff tick) | engage it: a hero beside it at the end of its turn holds it to one |
| The Lantern Road III | Oni (`oni02`) | a fallen foe stays down (§13.3) | **Undying** (*added 2026-11-07, GG-T345*), the Hierarch's rule on an oni chief whose mask gets back up: the first time it falls while another foe still stands, it rises at half HP | cut down its raiders first; it rises only once |
| Shardfall III | Mecha Armor | the pincer rule (§1.2) | **AxisWard:** a hero pincer along its row (both attackers on its row) glances off, with no attacker or supporter strike ("Warded!", no mana), and cracks the ward | pincer it top to bottom; the third glancing pincer (`RuleBreakRules.AxisWardCracks`) shatters the ward and every pincer lands after, so a foe hugging an edge can't stall the fight |
| Arkgrave III | Swamp Mistress | one tile at a time (§1.1) | **MireSwap:** at the start of its turn, with a hero beside it, it trades tiles with the ally standing farthest from any hero (one-tile allies only), then takes its turn | cut down its coven first; with no ally left it has nowhere to go |
| Anchor Wastes III | Cyclops | only you move your heroes | **GravityStamp:** every 2nd own turn it stamps and drags every hero within 3 tiles (not already beside it) 2 tiles toward itself, nearest first (`Pull`); the turn before, it warns "raises a foot: it stamps next turn!" | keep out of reach on the warning, or stand ready: the pull drops heroes beside it, which is where a pincer starts |
| The Still III | Chronomancer | the turn order (§2) | **TwinTurn** (*added 2026-11-07, GG-T344*), the Captain's rule on a time-mage: the keeper of the Still's clocks takes a second turn whenever no hero stands beside her | close in on her: a hero beside her at the end of her turn holds her to one |
| Halcyon III | Kiln Guard captain (`ceramicknight06`) | the pincer rule (§1.2) | **AxisWard** (*since 2026-11-07, GG-T350; the Hierarch moved up to the cathedral*), the Mecha Armor's rule on the porcelain knight holding the spire stair: row pincers glance off its glaze and crack it; the third shatters it | pincer it top to bottom, or crack the glaze three times |
| The Choir's Cathedral III | Hierarch (`hierarch`) | a fallen foe stays down (§13.3) | **Undying:** the first time it falls while any other foe still stands (graves, eggs and bodies don't count), it rises at half HP, shedding its debuffs. Its Choir (a Cantor and a Confessor) stands beside it | silence its Choir first; it rises only once |

*Amended 2026-11-07 (GG-T344):* The Still (§22.3) is a sixth main biome, so six finales now carry a rule-breaker and two of them break the same rule. The Still III reuses TwinTurn on the Chronomancer rather than adding a sixth engine rule: she is the Still's timekeeper and already fights the party's tempo, and the owner made no new art for a boss.
*Amended 2026-11-07 (GG-T345):* The Lantern Road (§22.3.2) is a seventh main biome. Its finale reuses **Undying** on an oni (`oni02`) rather than adding an engine rule: existing units only, and the oni's lore is a mask that grew on. So two pairs of finales now share a rule (TwinTurn, Undying).
*Amended 2026-11-07 (GG-T350):* The Choir's Cathedral (§22.3.3) is an eighth main biome and the new finale. The Hierarch keeps **Undying** and moves to the Cathedral III; Halcyon III gets a Kiln Guard captain with **AxisWard**, so each of TwinTurn, AxisWard and Undying is shared by two finales and MireSwap and GravityStamp stay unique.

A frozen or sleeping rule-breaker skips its rule along with its turn. The bot handles them without special-casing beyond pincer scoring: a warded row pincer is worth +3 (it cracks the ward) instead of +10 (`HeroBot.ScoreMove`), so it prefers a column pincer. *(Tests: `RuleBreakTests` — the arrival banner on the flagged spawn only; TwinTurn's second turn and its engaged counter; AxisWard's glancing row pincer, landing column pincer and shatter; MireSwap's swap and its no-ally counter; GravityStamp's warning then pull, out-of-reach heroes untouched; Undying's single rise with a thrall standing, and staying down with none or once risen; the eight main finales carry exactly these rule-breaks and no other stage does; validation of a guest rule-break and a big-body swap.)*

*Amended 2026-11-08 (GG-T371):* elements.json's `RuleBreaks` lists how each rule is answered. All five are Positional (any party can do it), and `ContentLoader.Validate` refuses a campaign rule-break with no row, or with an Element or Item counter the Merchant doesn't sell (§3.3.1).

### 14.10 The Chronomancer (added 2026-11-05, owner direction, GG-T275)

Owner: "Stack haste, while a Chronomancer casts slow on you would be an interesting battle mechanic." A caster that fights the party's **tempo** (§2.11) instead of its health.

- **Class.** `chronomancer`, an enemy caster (Kiter brain, shoots 3 tiles, moves 1). Its portrait is the owner's own art (GG-T328, cut with `GridGame.Studio/tools/portrait_cut.py`, a 1024 canvas since GG-T352); `"Portrait": "chronomancer"` keeps `GridGame.Studio/tools/import_art.py` from overwriting it with the old `Hag03` stand-in. It is not a Crucible family: it has a boss phase, which the Crucible excludes.
- **Temporal Drag** (`charge_temporal_drag`). A telegraphed charge (2.5 s on the cast lane), `AllEnemies`: 4 Arcane to every hero plus **Slowed** (2 turns). That is one −1 tempo effect for the whole party, not one per hero, so every foe's icon runs ×1.25.
- **Rewind** (`chrono_rewind`). Once, at 40% HP (its boss phase), it heals 20 and hastens **its own** icon 0.5 u. That never touches party tempo (§2.7.1).
- **Brain.** It holds Drag while the party's net tempo is already negative (`EnemyPlanner.PlanCast`). It moves or shoots instead, and drags when the party is level or hasted, which is the tug-of-war. As a Kiter it keeps out of pincers.
- **Counterplay** (named on its kit line and in Drag's InspectCard text):
  - **interrupt**: hit it while it charges (§13.4 stagger and cancel)
  - **cleanse** Slowed off every hero (§2.11, the tempo lifts)
  - **out-haste** it: stacked haste outweighs one Drag
- **Bot.** `HeroBot` values pincering or damaging a foe that is charging a tempo drain (+6 / +5) and cleansing a negative-tempo debuff while net tempo is below zero (+4 per point).
- **Placement.** Anchor Wastes II, wave 2 (behind two ceramic knights). On the same 12 casual-bot seeds and parties it wins 9 of 12 (75%) with the Chronomancer and 8 of 12 without, inside the POL-G1 55–85% band. The wave was already long (36 actions without it, about 43 with it): the knights' tough hide is the slog, and that's left to the balance loop (GG-T276).
- *(Tests: `ChronomancerTests`. Content validates and is placed; one Drag on two heroes is −1 tempo with both Slowed; cleansing the last Slowed hero restores tempo; Drag is held while tempo is negative and cast when level or hasted; Rewind is self-only.)*

### 14.11 The Murder — harpies (added 2026-11-07, owner art, GG-T329)

The Murder (docs/STORY.md §3.2): Street kids spliced with black wings, rooftop smugglers. Three swift (Speed 18,
re-entering ≈3 s out), frail, `Sentient` flyers (`Flying`), one family (`harpy` drop table), three roles:

| Class | Role | Brain / targeting | Kit | Support |
|---|---|---|---|---|
| `harpy01` Harpy Raider | striker | Skirmisher (strike, dart a tile away) / Weakest, moves 2 | **Talon Rake** (`talon_rake`): a landed blow, 25%: [Slowed] on the target, so the party loses a point of tempo (§2.11) | Crit +25% (*Diving Strike*) |
| `harpy02` Harpy Shrieker | disruptor | Kiter / Weakest, moves 2 | **Gale Shriek** (`charge_gale_shriek`): 2 s charge, a 3×3 around one hero, 4 Arcane and `Pull` 2 — each hero caught is dragged toward the harpy, out of the formation the party built; rests a turn, recovers 0.5 s. Blur | Haste its attacker 1 s (*Tailwind*) |
| `harpy03` Harpy Spotter | ranged | Kiter / Caster, shoots 3, moves 2 | **Jammer Darts** (`jammer_darts`): a landed blow, 25%: [Silenced] | Silence 30%, reach 2 (*Jammer Drone*) |

- **Counterplay** (on their cards): they are frail, so [pincer] them when they land; cleanse the Slow; interrupt the
  shriek or keep heroes spread; close in on the spotter. Weak to Lightning (×1.5).
- **Design note.** The gale first knocked heroes *away* (Knockback 1). A hero blown into the board's edge takes the
  18-damage wall slam, and the casual bot fell from 8/12 to 1/12 on the trial stage, so the gale pulls instead: same
  "breaks your lines" identity, no slam.
- **Placement.** Tithe Moors II (`greenvalley-02`), wave 4: the three harpies replace two goblin thugs and a Redline
  ganger. Casual bot, 24 seeds, campaign parties: 11/24 with them and 11/24 without; smart bot 23/24 vs 24/24.
- *(Tests: `HarpyTests`.)*
- **Fixed 2026-11-17 (GG-T276):** Tithe Moors II's own wave 1 (unrelated to the harpies) placed two slimes at `Y: 5`
  and Tithe Moors III's wave 1 placed one at `Y: 5` — the only hand-placed enemies in the entire campaign inside the
  hero deployment band (`BattleRunner.Setup`'s default hero spawn is rows 4–7; every other authored enemy position in
  `stages.json` sits at `Y` 0–4). That put a slime on top of the party before it could set up on the very first wave
  of the stage, which the `polish.py --balance` records showed as abnormally short, losing wave 1s (3–5 player
  actions against POL-G6's 6–20 band). Moved to `Y: 1`, alongside the other wave-1 slime's `Y: 2`. `GRIDGAME_ABILITY_STAGE`-scoped soak, 8 casual battles/stage before → after: greenvalley-03 2/8 (25%) → 6/8 (75%, now inside
  the POL-G1 55–85% band); greenvalley-02 stayed low (2/8 → 1/8, inside this pass's noise) — its wave 4 harpies above
  and its slime-trail hazard (§14.22) are the likelier remaining cause there, left to the next pass.

### 14.12 The Void Sentinel (added 2026-11-07, owner art, GG-T330)

What forms where an Anchor leaks (docs/STORY.md §3.5): smoke given a body around a cyan heart of stolen mana. An
elite guardian, and the first enemy to use the Line charge built in §13.4.

- **Class.** `voidsentinel`: `Construct`, `Magic`, not `Sentient` (it never makes a pincer, but supports one). Speed 8
  (re-enters ≈4.8 s out), VIT 18, a 20-point front **armor plate** (§13.5), Guardian brain (stays beside its most
  valuable ally) with Threat targeting. Resists Dark (×0.5), weak to Holy (×1.5).
- **Void Beam** (`charge_void_beam`, `ChargeShape: Line`). A 2.5 s charge whose line, from beside it to the board's edge
  toward its target, is marked when it starts. On landing, every hero still on the line takes a Dark spell (INT vs WIS)
  and is [Silenced]. It rests a turn between beams and recovers 1.5 s.
- **Void Core** (`void_core`): the first hit that drops it below half HP flares a [Ward] on it.
- **Support:** *Void Aegis*, [Ward] on its attacker, reach 2.
- **Counterplay** (its card): step off the marked line or interrupt the charge; pincer it from the flanks, not the plate.
- **Placement.** Anchor Wastes I (`cave-01`) wave 5, added beside the two oni and the ice mauler it guards. Casual bot,
  24 seeds, campaign parties: 15/24 with it, 18/24 without; smart bot 24/24 both. Of 178 beams the casual bot let
  charge, 175 landed and caught 71 heroes: most beams hit an empty line because a hero had moved off it.
- *(Tests: `VoidSentinelTests`.)*

### 14.13 The Lich (added 2026-11-07, owner art, GG-T331)

The last captain of the Ark *Long Patience*, who refused to let his ship die and kept himself instead (docs/STORY.md
§3.5). A boss caster for the Arkgrave.

- **Class.** `lich`: `Undead`, `Sentient`, `Caster`, `Boss`. Speed 11, INT 18, WIS 16, frail body (VIT 14). Commander
  brain (keeps ~3 tiles back with an ally between it and the party), Weakest targeting, shoots 3. Resists Dark (×0.5),
  weak to Holy (×1.75); Poison Ward and Steadfast. Not tameable and not a Crucible family (it has boss phases).
- **Soulfire** (`charge_soulfire`): a 2.5 s charge on a 3×3 around its target: 6 Dark to every hero inside and a 50%
  [Burning]; recovers 1.5 s. Spread out, or interrupt it.
- **Soul Harvest** (`lich_soul_harvest`, boss phase at 50%): once, 5 Dark to every hero, and it heals 6 per hero touched.
  Enemy-only (`MetaServices` `EnemyOnly`), like Enrage and Rewind.
- **The grave.** `Remains: gravestone` (§14.5): when it falls it leaves a grave, and left standing the grave raises it on
  its third turn. That is the phylactery, with no new code. A risen Lich enters its harvest phase again.
- **Support:** *Soul Echo*, Cast its Soulfire at 50% power, reach 2.
- **Placement.** Arkgrave I (`swamp-01`) finale, wave 5: the Lich behind a marsh shambler, in place of a lurker. Casual
  bot, 24 seeds, campaign parties: 10/24 with it, 13/24 without. The waves before it are unchanged: 14 runs reached
  wave 5 both times, and the casual bot beat the Lich in 10 of its 14 tries (13 of 14 against the old wave). Smart bot
  24/24 both. The first cut (8 Dark, Burning every hit) took the stage to 8/24; Burning alone was more damage than the
  Lich's own spells. The casual bot let it rise from the grave 3 times in 24 battles.
- *(Tests: `LichTests`.)*

### 14.14 The Infernalists (added 2026-11-07, owner art, GG-T338)

The Ember's zealots (docs/STORY.md §3.6): black armour, long red capes, and the belief that the Nearing is a
homecoming. Four `Sentient` variants, one `infernalist` family. They resist Fire (×0.5), are weak to Ice (×1.5), and
three of them carry Infernal Hide (they can't be [Burned]). The family plays one combo: **burn, then execute.**

| Class | Role | Speed / brain / targeting | Kit | Support |
|---|---|---|---|---|
| `infernalist01` Zealot | brander | 15 / Hunter / Threat | **Cinder Claws** (`cinder_claws`): a landed blow, 35%: [Burning] on the target | Status burning 50% (*Cinder Brand*) |
| `infernalist02` Pyromancer | caster | 11 / Kiter / Squishy | **Hellfire** (`charge_hellfire`): 2.5 s charge, a Cross (radius 1) on a hero, 6 Fire and [Burning] on each hero caught; recovers 1.5 s | Cast at 50% (*Ember Echo*), reach 2 |
| `infernalist03` Bloodbinder | healer | 11 / Medic / Threat | **Blood Rite** (`charge_blood_rite`): 2 s charge on its most wounded ally (below 70%): heals 25 and [Empowers] it; rests a turn. Lifesteal | Heal the chain 10%, reach 2 (*Blood Pact*) |
| `infernalist04` Executioner | finisher | 11 / Duelist / Weakest | **Executioner's Verdict** (`executioners_verdict`): its blows land ×1.5 on a [Burning] foe | Crit +30% (*Mark for Death*) |

- **Engine (small).** The Verdict needed a trigger condition on the *other* unit's status, so `TriggerCondition` gains
  `OtherHasStatus` (§4.1): checked against the context's other unit (the attacker, else the target) in
  `Triggers.ConditionHolds`, written "the foe has [Burning]" by `TriggerText`, and validated as a known buff id.
- **Counterplay** (their cards): cleanse the burn before the Executioner reaches the hero, or keep burning heroes away
  from it; spread out against Hellfire; kill the Bloodbinder first or interrupt the rite; Ice spells.
- **Placement.** The Shardfall, where the Ember wakes. Shardfall I (`desert-01`) wave 3: Pyromancer and Bloodbinder in
  place of two sand maws. Shardfall II (`desert-02`) wave 2: Zealot and Executioner beside a sand maw, in place of a
  militia soldier, so the burn-then-execute combo plays out. Casual bot, 24 seeds, campaign parties: desert-01 18/24
  with them vs 19/24 without, desert-02 14/24 vs 15/24; smart bot 24/24 on both, with and without. The first
  Executioner (STR 17, VIT 15, healed by the Bloodbinder beside it) dealt twice a soldier's damage, so it was cut to
  STR 14 and VIT 13 and paired with the Zealot instead.
- *(Tests: `InfernalistTests`.)*

### 14.15 The Bulwark (added 2026-11-07, owner art, GG-T332)

The Pale Concord's soldiers (docs/STORY.md §3.1): white power armour, siege rifles, and an mana pack on every back,
each pack a portable Still. Four `Sentient` variants, one `bulwark` family, all slow or steady (Speed 8 or 11, re-entering
≈4.8 s or 4 s out). Sealed suits resist Poison (×0.5); the armour shorts out under Lightning (×1.5). The squad plays
**hold the line and tithe the party**: a shield-Still wards the soldiers, the rifles and the siege gun shoot from range,
and the Siphoner drinks the party's orbs.

| Class | Role | Speed / brain / targeting | Kit | Support |
|---|---|---|---|---|
| `heavytrooper01` Rifleman | ranged | 11 / Kiter / Squishy, shoots 3 | **Suppressing Fire** (`suppressing_fire`): a landed shot, 30%: [Blinded] | Blind 30%, reach 2 (*Covering Fire*) |
| `heavytrooper02` Shieldbearer | wall | 8 / Guardian / Threat, plate 30 | **Aegis Field** (`charge_aegis_field`): 2 s charge, [Ward] on every ally within 2 tiles; rests a turn, recovers 0.5 s | Protection on the chain (*Pack Shield*) |
| `heavytrooper03` Siphoner | tithe | 11 / Kiter / Caster | **Mana Siphon** (`charge_aether_siphon`): 2 s charge on a hero, 4 Arcane, and **an orb leaves the party's bank**; rests a turn, recovers 0.5 s | Haste its attacker 1 s, reach 2 (*Vent Mana*) |
| `heavytrooper04` Siege Gunner | artillery | 8 / Commander / Threat, plate 24 | **Siege Shot** (`charge_siege_shot`, `ChargeShape: Line`): 3 s charge down a marked line to the board's edge, a Lightning spell on every hero still on it; rests a turn, recovers 2 s | Pushback 0.75 s (*Rail Recoil*) |

- **Engine (small): the siphon.** `AbilityDef.DrainsOrbs` (0–3, validated, only on a `charge_` ability, so a hero can
  never learn it): when the charge lands on at least one hero (a single target that isn't dodged, or any hero inside an
  area), `BattleRunner.DrainOrbs` removes up to that many orbs from the party's `ManaBank`, emitting `OrbsSpentEvent`
  with `Drained` (the bank redraws and the cast cue plays; telemetry doesn't count it as mana spent) and "Siphoned an
  orb!". An empty bank announces "No mana to take". The smart bot treats a siphon charging while the bank holds orbs
  like a tempo drain (`HeroBot.ChargesTempoDrain`): worth staggering.
- **Counterplay** (their cards): spend orbs before a siphon lands, or interrupt it; flank the Shieldbearer and the Siege
  Gunner off their plates; step off the siege gun's marked line; close on the Rifleman and pincer it; Lightning.
- **Placement.** Halcyon, where the Concord holds the spire. Halcyon II (`cityruins-02`) wave 3: Shieldbearer, Rifleman
  and Siphoner in place of two Chromed Hollow. Halcyon III (`cityruins-03`) wave 2: the Siege Gunner beside the lone bat.
  Casual bot, 24 seeds, campaign parties: cityruins-02 15/24 with them vs 20/24 without (inside the 55–85% band; the
  losses still come at the reaper's wave 5, with the wounds the squad leaves), cityruins-03 17/24 vs 18/24; smart bot
  24/24 everywhere. Over the cityruins-02 casual runs the siphon landed 139 times but found orbs in the bank only 37
  times: the casual bot spends what it earns, which is exactly the counter. The first Shieldbearer also had Tough Hide;
  plate, Ward and Protection were already a slog, so it was dropped. A two-soldier wave (Shieldbearer and Siphoner, with
  the Rifleman moved beside the gun) measured 14/24 and cost cityruins-03 three wins.
- *(Tests: `BulwarkTests`.)*

### 14.16 The Anchor Watch — wraith knights (added 2026-11-07, owner art, GG-T334)

Knights sworn to guard the Anchors forever, kept going by the arches' own leaking mana long after they should have
died (docs/STORY.md §3.5): grey plate, cloaks the colour of old fire. Three `Undead`, `Sentient` variants, one
`anchorwatch` family. They resist Ice (×0.5) and Fire undoes them (×1.5). The family plays **undying**: every knight
leaves a gravestone that raises it again unless smashed (§14.5), and each role adds a different way of refusing to fall.

| Class | Role | Speed / brain / targeting | Kit | Support |
|---|---|---|---|---|
| `wraithknight01` Watch Sentry | the post | 8 / Sentinel (never moves) / Threat, plate 30 | **Rime Riposte** (`rime_riposte`, Reactive): attacked directly, 50%: a [Counterstrike] at 60% that also [Slows] the attacker. Steadfast | Ward on its attacker (*Oath Shield*) |
| `wraithknight02` Watch Reaver | the hunter | 15 / Hunter / Weakest | **Oathbound** (`oathbound`, Reactive): the first time a hit drops it below 35% HP it heals 25% and gains [Ward] (trigger `Cooldown` 99: once a battle) | Slow 35% (*Rime Edge*) |
| `wraithknight03` Watch Vigilant | the keeper | 11 / Commander / Weakest | **Ember Vigil** (`charge_ember_vigil`): 2 s charge, [Regen] on every ally within 2 tiles; rests a turn, recovers 0.5 s | Heal the chain 10%, reach 2 (*Old Fire*) |

- **No engine code.** Sentinel brain, Reactive triggers, a trigger cooldown, an AllyOnly area charge and gravestone
  remains are all existing data.
- **Counterplay** (their cards): strike the Sentry from range or accept the riposte, and flank its plate; finish the
  Reaver in one push once its oath has held; interrupt the Vigil or kill the Vigilant first; smash the graves; Fire.
- **Placement.** The Anchor Wastes. Anchor Wastes I (`cave-01`) wave 2: a Reaver beside a skelepede, in place of the
  second skelepede. Anchor Wastes II (`cave-02`) wave 3: Sentry and Vigilant beside the goblin thug, in place of an oni
  and a skelepede. Casual bot, 24 seeds, campaign parties: cave-01 14/24 with them vs 15/24 without; cave-02 6/24 vs
  7/24 (already below the band before them: most losses are still at the Chronomancer's wave 2); smart bot 24/24 on
  both. In the casual runs the Reaver's oath held 29 times and the graves stirred 134 times.
- *(Tests: `AnchorWatchTests`.)*
- **Amended 2026-11-17 (GG-T276):** the balance loop's `polish.py --no-player --balance` pass found Anchor Wastes II
  (`cave-02`) wave 3 and Anchor Wastes III (`cave-03`) wave 3 were the only early-mid-campaign waves stacking *two*
  Anchor Watch units together (Sentry + Vigilant on `cave-02`, Reaver + Vigilant on `cave-03`) — a sustain-on-sustain
  combo (Vigilant's party-wide Regen keeping the Sentry's counterstrike or the Reaver's self-heal topped up) that the
  GG-T334 placement note above already flagged as the cause of cave-02's 18/24 → 6/24 drop. Each wave's Vigilant
  (`wraithknight03`) swapped back for a plain `skelepede` (matching what it originally stood in for), leaving one
  Anchor Watch elite per wave instead of two. `GRIDGAME_ABILITY_STAGE`-scoped soak, 8 casual battles/stage before →
  after: cave-02 4/8 (50%) → 6/8 (75%, now inside the POL-G1 55–85% band); cave-03 2/8 (25%) → 3/8 (38%, still below
  band — wave 4's cyclops pair + mountain troll is the harder remaining slog there, left to the next pass). cave-01's
  single-elite wave 2 was already in band and is untouched.

### 14.17 The Choir — cultists (added 2026-11-07, owner art, GG-T335)

The Pale Concord's engineer-priests (docs/STORY.md §3.1): faceless hoods, white ponchos printed with violet
circuit-glyphs. They run the Stills and sing the mana out of people; the glyphs are the hymn. Three `Sentient`
variants, one `choir` family, every one of them a singer: each role is a telegraphed charge the party can read on the
bar and interrupt. Their own song is mana, so they resist Arcane (×0.5); they are weak to Dark (×1.5).

| Class | Role | Speed / brain / targeting | Kit | Support |
|---|---|---|---|---|
| `cultist01` Choir Cantor | the hymn | 11 / Commander / Weakest | **Tithe Hymn** (`charge_tithe_hymn`): 2.5 s charge on the whole party, 3 Arcane to every hero and a 50% [Silenced] on each; rests a turn, recovers 1.5 s | Silence 30%, reach 2 (*Hush*) |
| `cultist02` Choir Psalmist | the buffer | 15 / Kiter / Squishy | **Psalm of Certainty** (`charge_psalm_of_certainty`): 2 s charge, [Empowered] on every ally within 2 tiles; rests a turn, recovers 0.5 s | Empower its attacker, reach 2 (*Certainty*) |
| `cultist03` Choir Confessor | the absolver | 11 / Medic / Threat | **Absolution** (`charge_absolution`): 2 s charge on its most wounded ally (below 70%): heals 15, [Cleanses] every debuff, gives [Ward]; rests a turn, recovers 0.5 s | Cleanse the chain (*Absolve*) |

- **No engine code** for the songs. An AllEnemies charge with a buff chance, an AllyOnly area buff and a single-ally heal that also
  cleanses and wards are all existing dispatcher data.
- **Counterplay** (their cards): cast your spells before the hymn lands, interrupt it, or cleanse the Silence after;
  pull the Concord away from the Psalmist or interrupt the psalm; kill the Confessor first, or interrupt the absolution.
- **Placement.** Halcyon III (`cityruins-03`) wave 3: a whole Choir (Cantor, Psalmist, Confessor) in place of two
  Chromed Hollow and the Choir's training mannequin. Casual bot, 24 seeds, campaign parties: 19/24 with them vs 17/24
  with the stage as it stood (the Bulwark's Siege Gunner already in wave 2; 18/24 before either); smart bot 24/24 both.
  The stage got no harder: the three frail singers stand in for a cyber-zombie pair whose death bursts (14 to every
  adjacent hero) hurt more than a hymn, and the casual bot reached wave 5 in 23 of 24 runs.
- **The rune circle** (amended 2026-11-24, GG-T902). Every Choir role also carries **Concord Rune** (`choir_rune`,
  Skill, cooldown 2 turns): when it has no song to sing it paints a violet rune (regionrules `choirrunes`, state
  `choir_rune`: no lifetime, no harm, the Choir's own mark) on a free tile beside it (within 2, the nearest ring first,
  then the tile farthest from the party), walks onto it, and holds it. `EnemyPlanner.PlanRune` deals the free runes to
  the singers not yet on one, nearest pair first, so no two chase the same rune; a singer with none paints one while the
  Choir's runes number fewer than three, it isn't Silenced and no hero is at its side. A singer on its rune, or on its
  way, still strikes a hero beside it. Fewer than three singers never start a circle (they fight instead), and the
  slimes' lob logic never throws a rune.
  - **The circle closes** the moment the third singer steps onto a rune while the other two hold theirs
    (`TileStateDef.Circle`: `Size` 3). While fewer than 5 of the Concord stand (`SummonBelow`) it sings up a Cyber Zombie
    (`cyberzombie00`, at the closing singer's level) beside it; with 5 or more it instead **Empowers** the three singers
    on the runes. Then every rune clears (`RuneCircleEvent`, then a "cleared" tiles event), and they start again.
  - **Counterplay: position.** A hero who steps on a rune scuffs it (`SquishedBy: Hero`; the HeroBot weighs scuffing a
    rune like squishing a hatching glob), and a hero standing on a rune's tile, or in a singer's way, stops the circle
    closing. Kill one singer and the circle can't close at all; Silence a singer and it can't paint.
- *(Tests: `ChoirTests`.)*

### 14.18 The Herd — minotaurs (added 2026-11-07, owner art, GG-T339)

The Street's hired muscle (docs/STORY.md §3.2): kids spliced with horns and bull-mass in the underspire's back-room
clinics, in hoodies, cargo pants, platform sneakers and chrome. Four `Humanoid`, `Sentient` variants, one `herd` family
(`Remains: corpse`). The splice holds a body's mana in so the Stills can't sing it out, so they resist Arcane (×0.5);
the chrome grafts short out under Lightning (×1.5). The family plays **loyalty on the timeline**: they are loyal to the
crew that pays and feeds them, and every bull carries **Stampede** (`herd_stampede`, Reactive `OnAllyKO`): when any ally
falls (a bull or the crew that hired it), it casts the free `stampede_rush` on itself and its icon jumps 0.3 of the bar
(1.5 s) sooner (§2.7.1).

| Class | Role | Speed / brain / targeting | Kit | Support |
|---|---|---|---|---|
| `minotaur01` Herd Boss | the paymaster | 11 / Commander / Weakest | **Feed the Herd** (`charge_feed_the_herd`): 2 s charge when an ally is below 70%, heals every ally within 2 tiles 12 and rushes each 0.15 of the bar; rests a turn, recovers 0.5 s | Haste its attacker 1 s, reach 2 (*On the Payroll*) |
| `minotaur02` Herd Bouncer | the wall | 8 / Guardian / Threat, plate 24 | **Brass Knuckles** (`brass_knuckles`): a landed punch, 30%: [Slowed] (the party loses a point of tempo) | Ward on its attacker (*Bodyguard*) |
| `minotaur03` Herd Gorer | the charge | 11 / Hunter / Threat | **Bull Rush** (`charge_bull_rush`, `ChargeShape: Line`): 2.5 s charge down a marked line to the board's edge, 8 Physical plus its INT hit on every hero still on it; rests a turn, recovers 1.5 s | Pushback 0.75 s (*Trample*) |
| `minotaur04` Herd Breaker | the brawler | 15 / Berserker / Weakest | **Berserk**: ×1.35 below half HP; charges in, never retreats | Crit +25% (*Gore*) |

- **No engine code.** An `OnAllyKO` reactive that casts a free self-haste (`stampede_rush`, enemy-only in
  `MetaServices` `EnemyOnly` like Enrage), an AllyOnly heal-and-haste charge and a Line charge are all existing data.
- **Counterplay** (their cards): kill the Herd before the crew that hired it, or watch the bar when a ganger falls and
  [Pushback] the bull that lurches forward; kill the Boss first or interrupt the feeding; flank the Bouncer off its plate;
  step off the Gorer's line; finish the Breaker in one push; Lightning.
- **Placement.** Street territory. Tithe Moors III (`greenvalley-03`) wave 3: a Bouncer and a Gorer hired by two Redline
  gangers, in place of the other two gangers. Halcyon I (`cityruins-01`) wave 5: the Boss and the Breaker beside a
  Hollow, in place of the Choir's training mannequin and a bat. Casual bot, 24 seeds, campaign parties, difficulty 10:
  greenvalley-03 8/24 with them vs 10/24 without, cityruins-01 9/24 vs 9/24 (both stages were already below the 55–85%
  band); smart bot 24/24 on both, with and without. In the casual runs the herd stampeded 72 times and the
  Gorer rushed 134 times on greenvalley-03; on cityruins-01 the Boss fed its crew 118 times.
- *(Tests: `HerdTests`.)*

### 14.19 The Hellspawn (added 2026-11-07, owner art, GG-T340)

What the Ember makes of a beast or a person who drinks from the Shardfall's red rivers (docs/STORY.md §3.6): red scale,
black hooked claws, manes and tails of blood-light. Whatever they were is gone, so they are `Beast`s, not `Sentient` (they
never plan a pincer). Four variants, one `hellspawn` family. Born of the Ember, they resist Fire (×0.5), all carry Infernal
Hide (they can't be [Burned]) and are weak to Ice (×1.5). The family plays **the hunger**: every one carries
**Bloodscent** (`bloodscent`, Passive `Always`): its blows land ×1.5 on a foe below half HP.

| Class | Role | Speed / brain / targeting | Kit | Support |
|---|---|---|---|---|
| `hellspawn01` Ravager | the hunter | 15 / Hunter / Weakest | Bloodscent | Burning 50% (*Cinder Mane*) |
| `hellspawn02` Drinker | the runner | 18 / Skirmisher / Weakest | Bloodscent and [Lifesteal]: strikes, drinks and darts away | Haste its attacker 1 s (*Blood Rush*) |
| `hellspawn03` Emberheart | the tide | 11 / Stalker / Caster | **Red Tide** (`charge_red_tide`, Shape `Row`): 2.5 s charge, the red river floods its target's whole row, 5 Fire and [Burning] on every hero in it; rests a turn, recovers 1.5 s | Cast at 50%, reach 2 (*Red Spill*) |
| `hellspawn04` Scaleback | the wall | 8 / Relentless / Threat, plate 20 | **Searing Scales** (`searing_scales`, Reactive `OnAttackedDirect`, 35%): [Burning] on the attacker | Protection on the chain (*Scale Wall*) |

- **Engine (small).** Bloodscent needed a trigger condition on the *other* unit's HP, so `TriggerCondition` gains
  `OtherHpBelow` (§4.1, like GG-T338's `OtherHasStatus`): checked against the context's other unit in
  `Triggers.ConditionHolds` (strictly below the fraction), written "the foe is below 50% HP" by `TriggerText`, and
  validated to 0..1 by `ContentLoader.Validate`.
- **Red Tide** is the first enemy charge with the `Row` shape: it follows its target, so the hero it picked is always
  caught, and the rest of a row the party lined up for a pincer can step out of it.
- **Counterplay** (their cards): keep the party above half HP or keep the wounded out of reach; interrupt the tide or leave
  the row; [pincer] the Drinker when it lands; strike the Scaleback from range or cleanse the burn; Ice.
- **Placement.** Shardfall III (`desert-03`), the stage the Infernalists left untouched. Wave 2: Ravager and Drinker
  beside the tech gremlin, in place of two militia soldiers. Wave 4: Scaleback and Emberheart beside the gremlin and the
  vulture, in place of two more soldiers. Casual bot, 24 seeds, campaign parties, difficulty 10: 13/24 with them vs 8/24
  without; smart bot 24/24 both. The stage gets easier: the four soldiers they replace are plated, countering, `Sentient`
  Duelists that pincer, and the beasts are not; most losses are still at the Captain's wave 5 (7 of 11 with them, 11 of
  16 without). In the casual runs the tide landed 103 times and the scales seared 19 attackers.
- *(Tests: `HellspawnTests`.)*

### 14.20 Region rules (added 2026-11-08, owner direction, GG-T359)

Each region of the campaign plays under its own board rules, and every region gets new monsters (Mannequin placeholder
art first) built around its rule. GG-T359 is the framework; the regions' own rules are GG-T360 (barricades), GG-T361
(crumbling floor), GG-T362 (rising water), GG-T363 (telegraphed shard strikes), GG-T364 (anchor pull) and GG-T365
(darkness that masks timeline icons).

- **Data, not code.** A rule is a `regionrules.json` record (`RegionRuleDef`): the **tile states** it can paint
  (`TileStates`), the **events** it can put on the timeline (`Events`) and the **hooks** that run its effects (`Hooks`).
  A campaign theme names the rules every one of its stages plays under (`campaign.json` theme `Rules`), and a stage can
  add its own (`stages.json` `Rules`); the theme's come first, each once (`RegionRuleCatalog.ActiveFor`). Everything is
  checked by `ContentLoader.Validate` (`RegionRuleCatalog.Validate`): ids unique across all rules, every state, event,
  buff, rule and immunity reference known, areas complete, events with a positive `Seconds`.
- **Words live in Prose.** A rule's name and description are the Prose text `rule:<id>` (kind `description`, payload
  `name` and `short`), joined by `ProseText.Join`; `regionrules.json` may not carry `Name` or `Description`
  (`ProseText.NarrativeFields`, `tools/prose_text.py NARRATIVE`, `tools/prose_keys.py game_keys`). Each rule in play is
  announced with its name and description as the battle opens (`RegionRuleEvent`).
- **Hooks** (`RuleHookDef.On`): `BattleStart` (before wave 1 spawns), `WaveSpawn` (every wave, after its foes are on the
  board), `TurnStart` / `TurnEnd` (a unit's own timeline turn: a foe's or an independent ally's icon arrives; a hero's
  at each party window), `EnterTile` (any unit stepping, being shoved or pulled into a tile) and `UnitDeath`. Filters:
  `Team`, `State` (the subject stands on that tile state), `Wave`, `Every` (every Nth match) and `Chance`.
- **Effects** (`RuleEffectDef.Kind`): `MarkTiles`, `ClearTiles`, `ModifyTiles` (a counter; at the state's `Threshold`
  it becomes its `Becomes` state, or clears), `Damage`, `Heal`, `ApplyBuff`, `MoveUnits` (toward/away from an anchor or
  a point, or a direction, a free tile at a time), `Schedule` (a timeline event), `AdjustEvents` (later, sooner, or
  `Cancel`: the rule timers), `PlaceAnchor` / `MoveAnchor` (named board points) and `SetFlag` / `ClearFlag` (board-wide
  flags; `mask-timeline` makes the view hide whose foe icon is whose) and `GrantOrbs` (Amount mana orbs into the party's
  bank per hero reached, GG-T376). An effect's tiles are its `Area` (`Self`,
  `Target`, `Anchor`, `EventTiles`, `Tiles`, `Row`, `Column`, `RandomFree`, `RandomUnits`, `WithState`, `All`, grown by
  `Radius`); unit effects reach the units on the area, the subject, or everyone, filtered by `Team`.
- **Persistent tile state.** A painted tile (`TileMark`) keeps its state, the rule that painted it, a counter and an
  optional lifetime (`LifeSeconds` on the clock). The board draws every one as a wash in its state's colour with its
  glyph and its counter (`BoardView.SyncRegion`). A `Blocks` state is a block (`BattleState.Walkable`): nothing enters,
  lands or spawns there, and it is never painted, nor does a counter turn a tile into one, under a unit or a board item,
  so the board stays legal (§1.1). A `Hazard` state is harmful to stand on.
- **The timeline is the warning.** A rule never hurts the party out of nowhere: every hazard a rule makes is first a
  `Schedule`d **rule event**, its own icon on the timeline (`TimelineKind.Hazard`, no owner unit), counting down like a
  cast, its glyph or icon in its own colour, and its telegraphed tiles pulse on the board from the moment it is queued.
  It lands when it reaches the left edge (ties on one tick: casts, then rule events, then turns), runs its effects on
  those tiles (`EventTiles`) and leaves the bar, or re-arms for the same time (`Repeat`, a rule's heartbeat). A held
  icon reads the rule's name, description and when it lands. Retiming never brings one in under a tick, so it is always
  seen first.
- **Monsters play with the rules.** A monster's ability (`AbilityDef.RuleEffects`, any skill, spell or enemy charge,
  run once it resolves with the caster as subject and the anchor as `Target`) and its death (`ClassDef.OnDeathRule`, run
  where it fell) use the same effects: create, change or clear tile states, queue or retime events, move anchors, raise
  flags. A board-only ability doesn't announce a miss. These work on any stage, rules in play or not.
- **Immunity.** `ClassDef.RuleImmune` lists rule, tile state, rule event or ability ids the class shrugs off: their
  unit effects skip it, a rule's hooks don't fire for it, and a state's danger doesn't count for it (a flier over a
  collapsed floor, a water-born foe in the flood). It never makes a `Blocks` tile walkable.
- **Bots and soaks.** `RegionBoard.DangerAt` scores a tile's danger for a unit (a hazardous state, and more for a
  telegraphed rule event, the sooner the more). The hero bot pays for crossing danger and pays a lot to end on it
  (`HeroBot.ScoreMove`); the pincer planner never walks through it and foes don't step into it (`EnemyPlanner.PlanStep`)
  unless immune, so soaks keep working on ruled stages. Blocking tiles are plain blocks to every planner.
- **Determinism.** Rules in their listed order, hooks in file order, effects in order; only `RandomFree`/`RandomUnits`
  areas and a hook's `Chance` draw from the battle Rng. Hooks inside hooks stop four deep. Nothing runs in a preview.
- *(Tests: `RegionRuleTests`, written, not run.)*

#### 14.20.1 The Free Holds: hold barricades (GG-T360)

The first region rule (`holds-barricades`, theme `greenvalley2`, stages 28-29): a siege.

- **Barricades.** Every wave clears the last wave's barricades and throws up four new ones on free tiles (`barricade`, a
  `Blocks` state: a wall to movement, spawns and every planner, so it splits pincer lines).
- **Breaking one.** A hero ending their turn beside a barricade (Manhattan 1) wears it down by one; at 3 it falls to
  `rubble`: the barricade's pickup. The first hero to step onto it is `empowered` and clears it (an `EnterTile` hook);
  unclaimed rubble lasts 20 clock seconds.
- **Hold monsters** (Mannequin placeholder art, words in Prose `class:<id>`):
  - **Holdwright** (Guardian brain): its charge `charge_raise_barricade` raises two barricades, shores up every standing
    one (counter -1, one more hero turn to break) and gives allies within 1 tile Protection.
  - **Hold Ram** (Berserker brain): its charge `charge_ram_smash` knocks its target back and smashes every barricade within
    2 tiles to rubble, which the heroes can then claim: a double-edged foe.
  - **Hold Marksman** (Kiter brain, range 3, targets the squishy): leaves a barricade where it falls (`OnDeathRule`).
- **Waves.** Each Free Holds stage keeps its militia (4+ soldiers), Redline (a ganger), Free Holds I's two Hollow and Free
  Holds II's Captain finale, and fields all three hold monsters, two foes a wave and three in the finale; every wave
  signature stays unique.
- Balance unmeasured (owner rule: no soak). *(Tests: `FreeHoldsTests.The_free_holds_fight_behind_barricades_with_their_own_builders_rams_and_marksmen`, written, not run.)*

#### 14.20.2 Deep Shardfall: shard strikes (GG-T363)

The region rule `shardfall-strikes` (theme `desert2`, stages 34-35): read the timeline, step out from under the sky.

- **Shard strikes.** A `shard-strike` rule event targets a random hero and the four tiles around them: its icon rides the
  timeline for 4 s and its tiles pulse, then it lands for 40 Arcane damage on every unit there, foe or hero, so a foe lured
  under a strike takes it too. One is called as each wave spawns (6 s out), and again every third foe turn.
- **Crystals.** Every tile a strike lands on grows a `shard-crystal` (25 s). A unit ending its turn on one takes a Ward
  and uses the crystal up; a hero also mints the party a mana orb (`GrantOrbs`, GG-T376): go stand where the sky just
  fell.
- **Shardfall monsters** (Mannequin placeholder art, words in Prose `class:<id>`):
  - **Shardcaller** (Kiter, range 3, targets the squishy): its charge `charge_call_shards` calls a strike onto a hero and
    the tiles around them, on top of the region's own.
  - **Crystal Brute** (Relentless): immune to `shard-strike`, so it walks through the strikes; it seeds crystal on its tile
    and the four around it where it falls.
  - **Shard Skitter** (Skirmisher, fast): its death calls a strike onto its own tile and the four around it, the tiles its
    pincer stands on, so the heroes who caught it have 4 s to get clear.
- **Waves.** Two foes a wave, three in the finale (the Captain, a Crystal Brute and the sandmaw); signatures stay unique.
- Balance unmeasured (owner rule: no soak). *(Tests: `DeepShardfallTests`, written, not run.)*

#### 14.20.3 Under the Anchors: the anchor's heave (GG-T364)

The region rule `anchor-pull` (theme `cave2`, stages 36-37): the field itself moves the pieces.

- **The anchor.** Each wave sinks the named anchor `deep` on a random free tile, drawn as an `anchor-post` (a `Blocks`
  state), and cancels the last wave's heave.
- **The heave.** A repeating `anchor-heave` rule event (5 s, not a hazard: it moves, it doesn't hurt) rides the timeline;
  when it lands every unit within 3 tiles of the anchor, hero or foe, is pulled one tile toward it, a free tile at a
  time. A heave can close a pincer the party set up, or break one: read the icon and line up for it.
- **Anchor monsters** (Mannequin placeholder art, words in Prose `class:<id>`):
  - **Chain Warden** (Guardian, targets the squishy): its charge `charge_drag_anchor` hauls the anchor two tiles toward
    a hero (the post moves with it), so the next heave drags the party in.
  - **Anchor Hulk** (Sluggish): immune to `anchor-pull`, the one piece the heave never moves; a fixed end for a pincer
    or a wall the party is pulled against.
  - **Hookthrower** (Kiter, range 3): its charge `charge_hook_line` pulls one hero two tiles out of formation.
- **Waves.** Two foes a wave, three in the finale (the yeti, the ice mauler and a Chain Warden); signatures stay unique.
  The heave's tiles are the ones around the anchor when the wave spawned; the pull itself follows the anchor.
- **Collisions** (GG-T378): a unit the heave drags into another unit stops there, and both take 12 Physical
  (`RuleEffectDef.CollideDamage`). Balance unmeasured (owner rule: no soak). *(Tests: `UnderTheAnchorsTests`, written, not run.)*

#### 14.20.4 The Drowned Arks: the rising tide (GG-T362)

The region rule `rising-tide` (theme `swamp2`, stages 32-33): race the water, or fight in it.

- **The tide.** Each wave drains the last wave's water, cancels its pending rises and queues three new ones on the
  timeline: the bottom row floods at 8 s, the next at 16 s, the third at 24 s (`tide-1..3`, rule events whose tiles pulse
  first). Flooded tiles are `floodwater`, a `Hazard` state (bots keep out of it unless immune).
- **In the water.** A unit that starts its turn in floodwater, hero or foe, is `slowed` (its next timeline entry comes
  later) and `wet`. Lightning exploits `wet` (elements.json), which is how shock conducts through the flood.
- **Drowned monsters** (Mannequin placeholder art, words in Prose `class:<id>`):
  - **Tidestrider** (Skirmisher, fast): immune to `rising-tide`, the flood never slows it.
  - **Stormcaller** (Kiter, range 3): its charge `charge_conduct` strikes a hero with lightning and puts 30 Lightning
    into every hero standing in floodwater.
  - **Bloated Drowner** (Sluggish): where it falls it floods its tile and the four around it, and brings every pending
    rise 6 s sooner.
- **Waves.** Two foes a wave, three in the finale (the Swamp Mistress, a hag and a Bloated Drowner); the lurker and
  tree-golem Group waves became explicit pairs; signatures stay unique.
- Rows assume the party holds the bottom of the board. Balance unmeasured (owner rule: no soak). *(Tests:
  `DrownedArksTests`, written, not run.)*

#### 14.20.5 The Underspire: crumbling floor (GG-T361)

The Underspire (theme `cityruins2`, stages 30-31) plays under GG-T368's `crackingfloor` rule (§14.21) on every stage:
each wave cracks three free tiles; a crack counts steps, turn ends and blows, crumbles at three, and caves in to a hole
(a pit: whoever stands there falls) a timeline-telegraphed 3 s later. The board shrinks as the fight goes on.

- **Underspire monsters** (Mannequin placeholder art, words in Prose `class:<id>`), built to make more floor crack:
  - **Spire Burrower** (Burrower brain): its trail (`TrailRule`, every step) cracks each tile it leaves, never painting
    over another mark; immune to `crackingfloor`, so it never falls.
  - **Pit Lurker** (Ambusher, targets the squishy): immune to `crackingfloor`; where it dies its tile and the four around
    it start to crumble, so the heroes who pinned it have 3 s to step off.
  - **Floorbreaker** (`spireshover`, Berserker): its charge `charge_floor_shove` cracks the tiles around a hero and
    knocks them two tiles back across the floor.
- **Waves.** Two foes a wave, the unchanged finale (the Herd Boss, the Choir and Redline); every foe is an underspire
  family or one of the three; signatures stay unique.
- Balance unmeasured (owner rule: no soak). *(Tests: `UnderspireTests.The_underspire_floor_cracks_under_its_own_monsters`, written, not run.)*

#### 14.20.6 Morrow's Shadow: darkness (GG-T365)

The region rule `morrows-dark` (theme `morrowsshadow`, stages 38-40): the finale chapter is fought half-blind.

- **Darkness.** The battle opens by raising the `darkness` flag (`RuleFlags.Darkness`). A foe more than
  `RegionBoard.DarkSight` (2) tiles from every living hero, and on no lit tile, is **hidden** (`RegionBoard.Hidden`): the
  board doesn't draw it and its timeline icon is a "?" silhouette (the `mask-timeline` art), so the party knows a foe's
  turn is coming but not whose. It reappears the beat it comes into sight (`BattleScreen.SyncDarkness`, after every
  playback beat). The rules of play don't change: a hidden foe still blocks, acts and can be struck.
- **Light.** Each wave puts out the last wave's braziers and lights two new patches (a free tile and the four around it,
  never over another mark): `brazier-light`, a `Lights` tile state. A foe standing in brazier-light is always seen.
- **Dark monsters** (Mannequin placeholder art, words in Prose `class:<id>`):
  - **Lamp Snuffer** (Kiter, range 3): its charge `charge_snuff_lights` puts out every brazier and blinds a hero.
  - **Dusk Stalker** (Stalker, fast, targets the squishy): where it falls, its tile and the four around it light up.
  - **Gloom Mender** (Medic): heals its allies from out of sight.
- **Waves.** Every stage keeps the Ember, the Sentinels and the moon-changed Wild, and fields all three; the finale is
  unchanged (the Demon Lord, a Sentinel and a Bloodbinder); signatures stay unique.
- **Holy light** (GG-T377): the Aegisguard's Judgment of Warding lights the tiles around its target and the Merchant's
  Holy Burst flask a wider patch: `holy-light`, a `Lights` state lasting 12 s. It is its own rule (`holy-light`), so
  painting it elsewhere never brings Morrow's dark along; a foe starting its turn in holy light is Blinded.
- The Dusk Mimic (§14.20.7, GG-T379) wears a hero's face on the dark timeline. Balance unmeasured (owner rule: no soak). *(Tests: `MorrowsShadowTests.Morrows_shadow_is_fought_in_the_dark_by_its_own_monsters`, written, not run.)*

#### 14.20.7 The Dusk Mimic (GG-T379)

Morrow's Shadow I and II each field a **Dusk Mimic** (Ambusher, Mannequin placeholder art, words in Prose
`class:duskmimic`), tagged `Mimic` (`RegionBoard.MimicTag`). While it is hidden in the dark its timeline icon shows a
living hero's portrait (`RegionBoard.MimicFace`, one hero per mimic by id) instead of the "?", so the party can misread
whose turn is coming; seen, it shows its own. Balance unmeasured (owner rule: no soak). *(Tests:
`MorrowsShadowTests.Morrows_shadow_is_fought_in_the_dark_by_its_own_monsters`, written, not run.)*

### 14.21 Cracking floors (added 2026-11-08, owner direction, GG-T368)

Owner: *"cracking floors which open up holes to fall into that takes off some life and respawns actor next to hole (what
if there are no free spaces? figure it out)"*.

A region rule on the §14.20 framework (`regionrules.json` `crackingfloor`), plus three framework additions any rule can
use: holes (`TileStateDef.Pit`), state entry effects (`TileStateDef.OnMark`) and the `Hit` hook.

- **Cracks.** Each wave paints three `cracked` tiles on free floor (`WaveSpawn`, `RandomFree`). A cracked tile's counter
  goes up by one each time a unit **steps, is shoved or pulled onto it** (`EnterTile`), **ends its turn on it**
  (`TurnEnd`), or **is struck on it** by a foe's blow or ability and lives (`Hit`, new: it runs from `Damage` for every
  landed foe-on-foe blow). The board draws the crack with its glyph and its count (`1/3`).
- **The timeline warns first.** At 3 the crack becomes `crumbling`, whose `OnMark` queues a **cave-in** (`cavein`, 3 s)
  on that tile: its own icon on the timeline and a pulsing tile, exactly like any rule hazard (§14.20). Nobody falls the
  moment a counter ticks over; the floor gives when the icon reaches the left edge.
- **Holes.** The cave-in paints `hole` (`Blocks`, `Pit`): a block to every walk, shove, spawn and item placement, drawn
  as a near-black pit with a rim (`BoardView.SyncRegion`). The hero walk refuses it like a rock (`WalkPath` now checks
  `Walkable`, so barricades and holes stop a drag too). **This rule's holes close** after 16 clock seconds (`LifeSeconds`,
  about two rounds) and the tile is plain floor again; a rule that wants holes to stay open for the fight (the
  Underspire, GG-T361) leaves `LifeSeconds` at 0.
- **Falling.** A one-tile unit on the tile as a hole opens (`MarkTiles`, or a counter whose `Becomes` is a pit), or
  shoved or pulled into an open hole (`Knockback`, `Pull`, a rule's `MoveUnits`: it goes in rather than slamming), falls
  (`FellEvent`): it loses `FallDamage` of its max HP (15%, environmental, at least 1; it can kill) and climbs out. Bosses
  and other multi-tile units, snakes, remains, corpses and eggs never fall, and neither does a class immune to the rule
  or the state (`RuleImmune`); under them the cave-in can't open and the crumbling tile settles back to floor.
- **Where it climbs out** (the owner's question, decided): breadth-first outward from the hole, ring by ring by tile
  distance. Ring 1 is "next to the hole"; the first ring holding a tile the unit's footprint may land on wins: free for
  something to appear (no unit, block, item or trap: the GG-T227 spawn rule, `FreeForSpawn`; the faller's own old tile
  counts as free when it was shoved in), preferring a tile no rule makes dangerous, then drawn from the battle Rng. A
  second pass accepts a trap or pickup tile (a hero springs and collects it on landing). **On a board with no such tile
  anywhere**, the faller trades places with the **nearest unit** that can be moved (ties: lowest id): it climbs out where
  that unit stood, and that unit is jostled onto the hole, which **fills in** under it (no fall, no damage). With nobody
  to trade with, the hole fills in under the faller. A unit is never lost off the board to a hole.
- **Landing** springs what lies there (a trap, a pickup, the new tile's `EnterTile` hooks: a faller can land on another
  crack and count it).
- **Bots.** A crack one count from crumbling reads as half a danger (`RegionBoard.AboutToGive`), a crumbling tile under
  its cave-in as a telegraphed hazard, and an open hole as a block, so the hero bot, the pincer planner and the foes walk
  around them. The hero bot pays 12 for a shove or pull that drops a foe into an open hole, 6 for one that leaves it on
  a dangerous tile (`RegionBoard.ShoveFloorValue`).
- **View.** The fall plays as its own beat (`FellAnim`): carried to the hole if shoved, the token shrinks and fades into
  it with the shatter cue, a swap partner steps onto the filled hole, and it pops back up on its landing tile with the
  dash cue; the fall's damage follows as its own beat.
- **Placed in** Frostmaw Caverns III (`cave-03`) and The Choir's Cathedral II (`cathedral-02`), stage `Rules`. Its name
  and description are the Prose text `rule:crackingfloor` (seeded placeholder).
- *(Tests: `CrackingFloorTests`, written, not run.)*

### 14.22 Hazard trails (added 2026-11-08, owner direction, GG-T370)

Owner: *"some enemies drop slime trails when they move, some fire, some ice, some land mines... always a way to defeat
them"*.

Four region rules on the §14.20 framework, each one tile state laid by a monster as it moves, each with a counter from
the GG-T371 elemental table (`elements.json`, [§3.3.1](#331-there-is-always-a-way-gg-t371-added-2026-11-08)), so no party is ever stuck.

| Rule | Tile state | Laid by | Effect | Counters |
|---|---|---|---|---|
| `slimetrail` | `slime` (14 s) | Slimes (`slime02`, `slime03`) | stepping in it: Slowed | Fire burns it away; Purifying Salts; it dries up |
| `firetrail` | `fire` (Fire, 10 s) | Hellspawn Emberheart (`hellspawn03`, the Ember) | stepping in: 6 Fire and Burning; ending a turn on it: 4 Fire | Ice douses it; Salts; it burns out |
| `icetrail` | `ice` (Ice, 12 s) | Yeti (`yeti`, the Wild) | stepping on it: Slowed (the slip), and one step in three Frozen | Fire melts it; Salts; it thaws |
| `landmines` | `mine` (40 s) | Bulwark Siege Gunner (`heavytrooper04`), every third step | stepping on it arms it (`armedmine`), whose blast (`mineblast`, 1.5 s on the timeline) deals 14 Fire to it and the tiles round it | a hit of any element sets it off from range; shove a foe onto it; the Disarm Kit pulls it |

- **Trails** (`ClassDef.TrailRule`, new): region rule effects run on the tile a unit steps off (`Self`; it is their
  subject), every `TrailEvery`-th step (`Unit.TrailSteps`), on every enemy step, leap and swap. Each trail paints with
  `KeepMarks` (new on `RuleEffectDef`): it never paints over another mark (a crack, a barricade, another trail) unless
  it reacts with it. Each layer is `RuleImmune` to its own state: it walks its own trail, and its planner reads no danger
  there. The old slime trap trail (`slime_trail`) stays on the two early slimes; `slime02`/`slime03` lay the tile state
  instead (never both).
- **A monster brings its rule.** A state painted on a stage that doesn't list its rule brings that rule into play
  (announced with its Prose name and description, `RegionRuleEvent`), so its hooks run wherever the monster fights.
  The four rules are also listed on a fitting stage each, so they are announced as the battle opens: Tithe Moors II
  (`greenvalley-02`, slime), Shardfall III (`desert-03`, fire), Frostmaw Caverns II (`cave-02`, ice), the Still III
  (`still-03`, mines).
- **Counters are the GG-T371 table, not a second one.** `TileStateDef.Element` (new) names the element a state carries
  (fire is Fire, ice is Ice). Painted onto a tile whose mark its element's `elements.json` `Tiles` row reacts to, the
  reaction runs (Clear, or Becomes another tile state) and the incoming state is spent: fire meeting ice melts it, ice
  meeting fire douses it, fire meeting slime burns it away, fire meeting a mine sets it off. A hero's ability runs the
  same reactions on the painted tiles it covers (its element's row when it deals damage, plus its tool set:
  `ResolveTileReactions` now covers region tiles as well as traps), and a tile pick that only reaches a reacting tile is
  a legal pick that never "misses". Every element's row arms a mine (`mine` → `armedmine`); Fire's clears `slime` and
  `ice`, Ice's clears `fire`; the `disarm` set clears `mine`, the `cleanse` set (Purifying Salts) clears `slime`,
  `fire` and `ice`. A Becomes into a state with an `OnMark` runs it (the armed mine queues its own blast).
- **Mines are telegraphed.** Stepping on one (or being shoved onto it: a shoved or pulled foe runs `EnterTile`, §14.21)
  never hurts at once: it arms, and the blast rides the timeline for 1.5 s over the mine and the four tiles beside it,
  hitting whoever is still there (foes too). The armed mine itself isn't a `Hazard`; the queued blast is the danger.
- **Always a way.** Every `Hazard` state (slime, fire, ice, mine) has a Merchant clearer (`ElementCounters.Validate`
  passes: the flasks, the Salts, the Disarm Kit), each trail fades on its own, and the camp bot shops for the clearer
  of a trail any of the next stage's foes lays (`ElementCounters.CountersFor` reads `TrailRule`).
- **Bots.** Hazard states read as danger (`RegionBoard.DangerAt`), so the hero bot pays to cross them and won't end on
  them, and foes don't walk into them unless immune; a queued blast reads as a telegraphed hazard.
- Rule names and descriptions are the Prose texts `rule:slimetrail`, `rule:firetrail`, `rule:icetrail`,
  `rule:landmines` (seeded placeholders).
- *(Tests: `HazardTrailTests`, written, not run.)*

### 14.23 Tactical tiles (added 2026-11-24, battle-feel epic GG-T823, GG-T842)

Firewalls, landmines and slime globs the party lays need tiles that know which side laid them.

- **A mark records its painter.** `TileMark.OwnerTeam` / `OwnerId` (new): a hero ability's `RuleEffects` paint as the
  party (the caster's team and id); a monster's ability, `TrailRule` and `OnDeathRule` paint as its side; a rule's own
  hook or event paints for nobody (`Team.Neutral`, the board). A counter turning a mark into its `Becomes` state keeps
  the painter; an element reacting with a mark (fire arming a mine) makes the new mark the element's painter's.
- **`TileStateDef.Affects`** (new: `Any` default, `FoesOfOwner`, `AlliesOfOwner`) is read against the painter
  (`RegionBoard.Affects`): a hook that reads the subject's tile (`State` filter: EnterTile, TurnEnd, TurnStart, Hit,
  UnitDeath) passes over a subject the mark doesn't work on (it isn't even counted toward `Every`), and the unit
  effects of that hook and of the state's `OnMark` skip unaffected units. A board-painted mark works on everyone,
  whatever its `Affects`, so every existing rule plays exactly as before.
- **Bots read it.** `RegionBoard.DangerAt` reads no danger from a mark that doesn't work on the unit: the party walks
  its own firewall, foes (`EnemyPlanner`, which now checks any painted tile even on a rule-less stage) steer round it,
  and the hero bot's `ShoveFloorValue` scores shoving a foe into it.
- **No announcement.** A hero-laid state brings its rule's hooks into play silently (`EnsureRuleInPlay`): the party's
  own firewall is no region rule. A monster-laid state is announced as ever (§14.22).
- **Hatch or squish (GG-T844).** A slime vomits a glob somewhere free; left alone it grows into a slime, stepped on
  it's gone. `TileStateDef.OnExpire` (new) runs when a mark's `LifeSeconds` run out, before the mark clears (Self is
  the tile; if those effects already cleared or repainted it, it isn't cleared again). `RuleEffectKind.SpawnUnit`
  (new; `Class`, `Level`, 0 = the stage's level) puts a foe on each tile of the area, or, that tile taken, on the
  nearest tile its footprint may appear on (ring by ring, row-major within a ring; `FreeForSpawn` and no trap, so
  never on an occupied tile; no room anywhere = it doesn't come). It joins the current wave as a reinforcement does:
  the wave's level dial, a timeline entry at its Speed's entry (spaced), a snake's segments, a
  `ReinforcementsArrivedEvent`, and no action spent (`TurnRules.ClearActed`, the §2.13 seam).
  `TileStateDef.SquishedBy` (new, a team; null = nothing squishes it): a unit of that team entering the tile (a step, a
  shove, a fall's landing: the `RegionEnter` funnel, any tile of its footprint) clears the mark with a `SquishedEvent`
  and a "cleared" tiles event, then the state's `OnSquish` effects run with the squisher as subject, all before the
  tile's EnterTile hooks. Bodies, eggs, remains and units immune to the state or its rule squish nothing. `Validate`
  wants a known `Class`, `LifeSeconds` for an `OnExpire`, `SquishedBy` for an `OnSquish`, and no `SquishedBy` on a
  blocking state.
- **The slime's glob (GG-T894).** The content for the owner's line. `slime02` and `slime03` carry `charge_slime_glob`
  ("Vomit Glob": a PickTile, EmptyOnly Skill, cooldown 2 turns, whose `RuleEffects` paint `slime_glob` on its Target).
  `EnemyPlanner.PlanLob` finds a lob in a foe's class kit (any PickTile, EmptyOnly ability with a MarkTiles on its
  Target) and throws it, in place of the step and strike, when no hero is adjacent, it is off cooldown and fewer than
  `LobKinCap` 4 of the foe's kin (living foes of its portrait family) plus unhatched marks of that state stand; the
  tile is a free one (no unit, item, trap, block or mark) within `LobRange` 3 of the foe, nearest the heroes, then
  nearest the foe, then row-major. The ability resolves on the spot (`TryLob`: "Vomit Glob!", its cooldown starts).
  `slime_glob` (regionrules.json rule `slimeglobs`: not a hazard, not blocking) lasts `LifeSeconds` 8; `OnExpire`
  hatches a `slime01` with `SpawnUnit` `FromOwnerLevel` (new: the level is the painter's own, already dialled for the
  wave, plus `Level`, here -1, at least 1; no painter = the stage's level). `SquishedBy` Hero; `OnSquish` runs
  `RuleEffectKind.GrantCharge` (new: `Amount` 15 mana charge into the party's meter once, when a hero is among the
  units; reason "rule:slime_glob"). Fire burns it away (elements.json Fire `Tiles`: `slime_glob` Clear), by a hero's
  fire ability covering it or a fire mark painted onto it. The hero bot's `ScoreMove` adds `SquishValue`: each squishable
  mark the walk steps on is worth 4 (+3 as it nears its hatch) when it would hatch a foe, 1 otherwise, about half a
  pincer, so a squish beats a so-so move but never a real pincer. Placed on The Tithe Moors I (`greenvalley-01`, wave
  1's second slime is now a `slime02`) and II (`greenvalley-02`, already led by one). No `slime04` exists yet.
  *(Tests: `SlimeGlobTests`.)*
- **Hidden marks (GG-T843).** A buried mine shows for a moment, fades from sight, then pops out and goes off.
  `TileStateDef.VisibleSeconds` (new; 0 = always seen): a fresh mark is seen by everyone that long
  (`TileMark.SightTicks`, counted down by the clock before lifetimes), then hides (`TileMark.Hidden`, a "hidden"
  `RegionTilesEvent`). A hidden mark is seen only by its painter's side (`RegionBoard.Sees(mark, team)`; a
  board-painted one by nobody); the view draws only what the party sees, the party's own hidden marks faint. It shows
  again, to everyone, for good (`TileRevealedEvent` with `Cause`) when it triggers ("triggered": one of its
  state-filtered hooks runs, before its effects), when `RuleEffectKind.RevealTiles` (new; the area, only `State` when
  set) uncovers it ("revealed"), or when an element hits it ("element": a state carrying an `Element` painted onto
  it, or a hero ability's element or tool covering it, the GG-T370 reaction paths). A mark that triggers or is
  revealed inside its seen window never hides. **No cheating:** `RegionBoard.DangerAt` takes an optional viewer team
  (default the unit's own) and reads no danger from a hidden mark that side can't see, so a foe's `EnemyPlanner` walks
  onto the party's hidden mine and the party's planners onto a foe's; `ShoveFloorValue` reads the floor as the party
  knows it (the hero bot still values shoving a foe onto the party's hidden mine). No Rng: replays hide and reveal the
  same marks on the same tick. `Validate` wants `VisibleSeconds` non-negative.
- **The Engineer's Buried Charge (GG-T876).** The content for the owner's buried landmine. `engineer_buried_charge`
  ("Buried Charge", Engineer Skill, cooldown 3 turns, INT 4; a PickTile, EmptyOnly ability whose `RuleEffects` paint
  `buriedmine` on its Target) buries a charge as the party. `buriedmine` (regionrules.json rule `landmines`: hazard,
  `LifeSeconds` 40, `VisibleSeconds` 2, `Affects` Any) shows for 2 s, then only the party sees it, faintly. Any actor
  entering it, a hero too, sets off its EnterTile hook: it pops out (`TileRevealedEvent` "triggered") and becomes an
  `armedmine` whose OnMark queues the §14.22 `mineblast` (1.5 s, 14 Fire on the tile and its neighbours, then the
  armed mine clears). Counters: the Disarm Kit clears it (elements.json reaction `disarm`: `buriedmine` Clear); any
  element's hit (a hero ability's element covering it, or an element painted onto it) shows it and sets it off early
  (every element's `Tiles`: `buriedmine` Becomes `armedmine`); a `RevealTiles` effect shows it to the foes. The hero
  bot's `ScoreAbility` adds `BuryValue`: a buried hazard (a MarkTiles state that is a hazard with `VisibleSeconds`) on
  a free, unmarked tile no hero stands on or beside is worth 6 for the first foe within 4 steps whose shortest walk to
  its nearest hero crosses the tile (+2 each further foe, +1 per step nearer). The view: the pop-out flashes the tile
  red with a puff and a "Click!" Timer text over a high click, the blast's Timer countdown ticks from its queued
  hazard (GG-T830), and the boom plays an explosion with a fire burst. The blast still lands on everyone on its
  tiles by design (`Affects` Any), so the gap below doesn't touch it.
  *(Tests: `BuriedChargeTests`.)*
- **The Machinist's Barricade (GG-T934).** `machinist_barricade` ("Barricade", Machinist Skill, cooldown 3 turns, INT
  4; PickTile, EmptyOnly, `RuleEffects` paint `machinist_barricade` on its Target) raises a wall as the party, its rule
  (regionrules.json `machinist-barricade`) coming into play silently. The state `Blocks` (no unit walks, lands or
  spawns there: `S.Walkable`, so the enemy step, `FootprintStepLegal` and the pincer planner route round it, and a
  pincer line through its empty tile is broken), lasts `LifeSeconds` 20 and has `Threshold` 3 with no `Becomes`, so
  its third hit clears it. Hits: the rule's TurnEnd hook (Team Enemy) adds 1 to every barricade orthogonally beside a
  foe that ends its turn (`ModifyTiles`, Area Self, Radius 1); and a line charge runs into it. `RegionBoard.StopsCharge`
  (a blocking mark that isn't a `Pit`; a hold's barricade too) ends `EnemyPlanner.LineToward` before it, and when one
  rises on a line already marked, `ResolveDirectCharge` walks the locked line in order and stops at it: the heroes
  beyond it are spared, the resolved tiles end there, and the wall announces "Blocked!" and takes one hit
  (`ModifyTiles` +1). A hole is no wall: a charge runs over it as before. Not yet: an ability `Range` (the skill picks
  any empty tile) and a Machinist skill-tree node. *(Tests: `BarricadeTests`.)*
- **The Red Mage's Firewall (GG-T931).** Owner: *"a firewall that causes burning debuff if actors enter the tile"*.
  `TargetShape.Wall` (new): a line of 2 × `Radius` + 1 tiles through the anchor, square across the line from the caster
  (`TargetResolver.Tiles`' new optional `from`, the caster's tile: |dx| > |dy| a column, else a row; no `from` a row).
  The dispatcher, the tile reactions (`TileReactions.Area`) and `RegionAbility` pass the caster's tile, and
  `RegionAbility` now sets the rule context's `EventTiles` to the ability's own area, so a `MarkTiles` over `EventTiles`
  paints the shape. `firewall` ("Firewall", Red Mage Spell, 2 orbs, instant, cooldown 2 turns, INT 6; Wall, Radius 1,
  PickTile, EmptyOnly, Fire, no damage of its own) paints `firewall` there as the party. `firewall` (regionrules.json
  rule `firewalls`: hazard, `Element` Fire, `LifeSeconds` 12, `Affects` Any) takes `firetrail`'s hooks: entering it,
  6 Fire and Burning; ending a turn on it, 4 Fire and Burning. It burns the party too (the owner's "actors"), so the
  hero bot won't walk it and foes (`RegionBoard.DangerAt`) steer round it. Counters: Ice douses it (elements.json Ice
  `Tiles`: `firewall` Clear), the Salts' `cleanse` set clears it, it burns out. Painted onto another mark it reacts as
  any Fire state does through the Fire row (melts ice, burns slime away, sets a mine off).
  *(Tests: `FirewallTests`.)*
- Not yet: a rule event queued by a team-owned mark (a hero mine's blast) still lands on everyone on its tiles, and
  timeline events from team-owned tiles carry no owner; an enemy ability's element doesn't reveal a hidden mark.
- *(Tests: `TacticalTileTests`, written, not run.)*

## 15. Save

### 15.1 Data model

**Amended 2026-10-09:** **three save slots** replace profiles and save-state lists. **Amended 2026-11-02 (GG-T99):** profiles are back — each player profile owns its own three slots and its own settings ([§15.4](#154-player-profiles)); `Settings` is no longer part of `SaveData`.

```
SaveData                       (Meta/SaveData.cs; one per slot)
├─ Version (1), Name ("Player"), CreatedUtc, UpdatedUtc, PlaySeconds
├─ Gold (wallet), LifetimeCoins (ticker)
├─ HighestClearedStage (campaign index, −1 = none), ClearedStages{}, SeenCrawls{}, LastStage
├─ Roster[]  HeroSave: ClassId, TotalXp, Hp (0 = full), Known[], Bar[5], SkillPoints,
│                       WeaponId/Durability/Repairs, ArmorId/Durability/Repairs, Relics[3],
│                       Variant (Female/Male) + VariantPending (v14, GG-T532, §25.11.2.3)
├─ Party[]   class ids of the active squad (≤ 6, `Progression.MaxPartySize`, GG-T291)
├─ Inventory { itemId → count }, Recruits
├─ Bounty    Taken[], Slain[], Claimed{}, Refresh, Battles, Used
├─ Bestiary  { classId → Seen, Defeated (+ legacy Scanned) }  knowledge tier = BestiaryKnowledge.Tier
└─ (Settings moved to the profile's settings.json, §15.4 — an old slot's Settings block is ignored on load)
```

Level is **derived** from `TotalXp` (`Formulas.LevelFromTotalXp`, `XpToNext(L) = 50 + 10·L²`), so the curve can change without migration. `SaveMigrations.Migrate` upgrades in place (v1 is the first shape; it backfills missing sections and pads Bar/Relics) and rejects saves newer than the build. Dictionaries and sets serialize natively (Newtonsoft) — the old "lists only" limit is gone.

**Amended 2026-10-15 (GG-T49):** every level gained now also earns **skill points** — `HeroSave.SkillPoints`, an unspent balance spent on skill-tree nodes (content and UI land in GG-T50). `Formulas.SkillPointsForLevelUp(from, to)` awards `SkillPointsPerLevel` (1) for each level crossed, plus a `SkillPointMilestoneBonus` (1) on every `SkillPointMilestoneInterval`th level (5, 10, 15…) — summed per level so a multi-level jump still hits every milestone in between. It's applied everywhere a hero's level can move: a battle-end level-up, hard-won XP kept on a defeat/retreat, a guest who survives joining the roster, and a Summon Circle recruit arriving above level 1. Save shape bumped to **v2**: `SaveMigrations` backfills each existing hero once with the points they'd already have earned up to their current level, so no save is retroactively shorted by the mechanic not existing yet; re-migrating a current-version save does not grant them twice. *(Tests: `Leveling_up_in_battle_grants_skill_points_including_the_milestone_bonus`, `Hard_won_xp_on_defeat_still_grants_skill_points_when_it_crosses_a_level`, `Migrating_a_v1_save_backfills_skill_points_once_for_heroes_already_past_level_1`, `A_guest_who_survives_a_victory_joins_the_roster`, `Full_loop_battle_commit_then_every_vendor`.)*

**Amended 2026-11-23 (GG-T591, from GG-T168): level-up stat points, a separate pool.** Every level past 1 also earns one **stat point** (`Formulas.StatPointsPerLevel`, no milestone bonus) that the player puts into any of the 8 stats, +1 each (`StatPointValue`). Earned points are derived from the level (`Formulas.StatPointsForLevel` = level − 1), never stored, so every way a level moves pays them with no grant at each site, and an older save needs no migration: its heroes simply have all their points unspent. Only the picks are saved, `HeroSave.StatPicks` (stat name → points, null-safe). `Meta.StatPoints` spends (`Spend`, `Unspent`, `In`) and `BattleFactory.SpecFor` adds the picks to the hero's level stats in battle (`HeroSpec.StatBonus`), so the Status sheet's Base column and every preview show them. The spend UI is a row of "+ STR … + LCK" buttons above the skill tree on the Talents sub-tab. Picks are permanent (the Inn's Respec refunds skill points only): a hero's build is the history of which stats they picked. The camp routine (GG-T54) spends them through `BotStatSpend` (GG-T593, §25.6.5): toward the next `Requires`-locked ability on its class list, and only once none is left into the class's strongest `Growth` stat (`StatPoints.BotPick`).

*Decision: two pools, not one.* `SkillPoints` stays its own currency. It is what the skill trees run on, and those shipped as the owner's 2026-11-02 decision (§25.6.2, GG-T51: home-tree thresholds, off-tree premium, Respec all priced in skill points); quests, bounties and dialogue also pay it (§22.4, `RewardSkillPoints`, `GrantSkillPoint`). One shared pool would make every level a choice between a stat and a tree node, devalue the trees' tuned 10/20-point thresholds, and turn every quest/bounty skill-point reward into a stat reward too. A separate stat pool adds GG-T168's "free point a level on any of the 8 stats" without touching any of that. If the owner later adopts GG-T168 in full (abilities gated by `Requires` stat thresholds, GG-T590/592, with the trees retired), the skill-point pool can be folded in then. *(Tests: `StatPointsTests`.)*

### 15.2 Persistence flow

`App/Services/SaveStore`: `slot1.json`…`slot3.json` under the active profile's `persistentDataPath/Profiles/<Name>/Saves` (**amended 2026-11-02, GG-T99**; was `persistentDataPath/Saves`; `-saveRoot DIR` puts the whole Profiles folder under `DIR/Profiles`), written atomically (temp file + `File.Replace`). `GameApp.Persist` writes the slot; a settings change is written by `GameApp.SaveSettings` to the profile's settings.json instead:

| Trigger | Saved? |
|---|---|
| New game in a slot | yes |
| Every successful Camp action (buy, sell, craft, repair, equip, learn, assign, party, summon, bounty, inn, camp item) | yes, immediately |
| Settings change | the profile's settings.json, not the slot (GG-T99) |
| Story crawl first shown | yes |
| Battle end (victory, defeat, retreat, escape) | yes, after `BattleFactory.Commit` |
| Save & Quit | yes |
| Mid-battle | no (battle state is not saved; quitting mid-battle loses the battle only) |

**Amended 2026-10-20 (GG-T146):** `SaveStore.Write` never throws. A failure at any step (disk full, a locked file, permissions) is caught, logged as `[Save] slot N write failed: …`, best-effort-cleans the `.tmp` file, and is reported to the caller via a `bool` return instead of losing progress silently — mirroring how `Load` reports a bad read with a log plus a `null` sentinel rather than an exception. `GameApp.Persist` shows the "Couldn't save!" toast whenever `Write` returns `false`. *(Tests: `SaveStoreTests.Write_WhenTheTempFileCannotBeWritten_ReturnsFalseInsteadOfThrowing`, `Write_WhenTheReplaceStepFails_ReturnsFalseAndLeavesThePreviousSaveIntact`.)*

**Amended 2026-11-20 (GG-T482, phones):** the temp file is flushed to the disk (`FileStream.Flush(true)`) before the rename, so a power cut can't leave a renamed empty slot, and the rename keeps the previous slot file as a **rolling backup**, `slotN.json.bak` (`File.Replace` with a backup name; WebGL copies it first). `Load` falls back to the backup when the slot file is missing, empty or unreadable, copies it back over the slot file (so the next write rotates the good save, not the bad one, into the backup), keeps the bad file as `slotN.json.corrupt`, and logs `[Save] slot N restored from its backup`. The player loses at most the last write (one `Persist`: a purchase, a battle's result). `Exists` counts a slot with only its backup left; `Delete` removes all three files. *(Tests: `SaveStoreTests.Write_KeepsThePreviousSaveAsTheBackup`, `Load_OfCorruptFile_RestoresTheBackup_AndPutsItBack`, `Load_OfEmptyFile_RestoresTheBackup`, `Load_WithOnlyTheBackupLeft_RestoresIt`, `Delete_RemovesTheBackupToo`.)*

**Cloud save and transfer (planned follow-up, GG-T482).** Not built yet; the plan:
- **Android: Play Games Services Saved Games** (the Google Play Games plugin for Unity). One snapshot per profile slot (`<profile>/slotN`) holding the slot JSON (well under the 3 MB snapshot limit) plus `UpdatedUtc` and `PlaySeconds` in the metadata. Upload after each `Persist` when signed in (debounced to one upload a minute and on `OnApplicationPause`); on load, if the cloud snapshot is newer than the local slot, ask "Use the cloud save from <date>, <play time>?" rather than merging. The plugin adds a sign-in prompt and the Play Games dependency, so it waits for the owner's Play Console setup (GG-T478).
  **Built 2026-11-23 (GG-T608), inert until the Play Games plugin and the Play Console app id are in** (docs/RELEASE_BUILD.md "Cloud save"). `Meta/CloudSave` makes every decision: the snapshot is named `<profile>-slotN` (Play Games allows only `[A-Za-z0-9-._~]`, 100 chars; anything else becomes `_`), its body is the slot's `SaveTransfer` code (so a download goes through the same version check and `SaveMigrations.Migrate` as an import), its metadata carries the play time and a "Name · N heroes · h:mm played" description, and `Decide` compares `UpdatedUtc`: within 5 s is the same save, a newer local save is kept (the next upload replaces the cloud), a newer cloud copy is only **offered**. `CloudUploadThrottle` uploads at most once a minute, flushed on pause and quit, and a failed upload retries at the next save. `App/Services/CloudSaves`: `GameApp.Persist` hands every successful write to it (a slot still waiting when another slot is saved goes up first); **Continue** and **Load Game** ask `BeforeLoad`, which shows "Use the cloud save from <date>, <h:mm> played? This device's save is older (…) and would be replaced." (Use cloud save writes it to the slot; Cancel plays the local one) and plays the local save at once when signed out, unreadable, or after 8 s without an answer; **Load Game** gains **Restore from cloud** (and is enabled with no local saves) wherever a cloud exists, which signs in interactively if needed and fills only **empty** slots. Two devices' unsynced writes resolve inside Play Games to the longer play time. `PlayGamesCloudBackend` (plugin v11, Play Games v2: silent sign-in at launch, `OpenWithAutomaticConflictResolution` / `ReadBinaryData` / `CommitUpdate`) compiles only with `GG_PLAY_GAMES` on Android; every other build has no backend and every call is a no-op. Cloud saves carry the purchase ledger like any slot, and the Import save rule applies (GG-T469): a cloud copy whose ledger shares a store transaction id with a save in any *other* slot on the device (every profile, and `.bak`s) is neither offered nor restored, so one purchase never pays its Shards in two slots; the account wallet (GG-T620) is where purchases stop riding along. *(Tests: `CloudSaveTests`.)*
- **iOS: iCloud key-value or CloudKit** through a native plugin, the same snapshot-per-slot shape; needs the Mac and Apple account (GG-T479).
- **Export/import code (platform-free fallback):** Settings > "Transfer save" shows the slot as gzip+base64 text to copy (and a share sheet on phones), and "Import save" pastes one into an empty slot after a version check. Small and engine-only (`SaveFiles.Serialize`/`Deserialize` already exist); the open question for the owner is whether a copyable save invites editing Diamonds, which the Diamond purchase ledger (GG-T469) should be checked against first.

  **Built 2026-11-22 (GG-T607).** `Meta/SaveTransfer`: the code is `GGS1.<checksum>.<payload>` — the slot's compact JSON, gzipped and base64'd; the checksum is the first 8 hex digits of the JSON's SHA-256 and catches a code cut short or mangled by a paste (it is not a seal). `Pack`/`TryUnpack` are the one serialize/deserialize pair the cloud saves (GG-T608/T609) reuse. `TryImport` ignores the spaces and line breaks a paste adds, refuses a damaged or foreign code, a newer code format or a newer save `Version` ("Update the game, then import it"), caps what a code may expand to (8 MB), and runs the result through `SaveMigrations.Migrate` like any slot read from disk; nothing throws, every failure is a reason line. **Settings** (Title or Camp) has two rows: **Transfer save** (`SaveExportScreen`: pick a slot, the code in a read-only box, **Copy code** to the clipboard; the loaded game is exported from memory, the others read from disk) and **Import save** (`SaveImportScreen`: paste, **Paste** from the clipboard, pick an **empty** slot, **Import**). An import never overwrites a slot (every slot full → "Delete one first"). **Purchases:** an import whose purchase ledger shares a store transaction id with any save file on the device (every profile's slots and their `.bak`, the same scan the IAP store uses) is refused — a paid save can move to another device but not be copied into a second slot here, where it would pay its Shards twice. Hand-editing a code's Shards is still possible (the checksum is rebuildable); the account wallet (GG-T567, deferred) is where purchases stop living in the save. No share sheet yet (needs a native plugin); copy/paste works on every platform. UI labels are code like the rest of Settings, not Prose (story text only). *(Tests: `SaveTransferTests` — `A_code_reads_back_into_the_same_save`, `Spaces_and_line_breaks_from_a_paste_are_ignored`, `A_damaged_cut_short_or_foreign_code_is_refused_without_throwing`, `A_code_from_a_newer_format_or_save_version_is_refused`, `Packing_never_changes_the_save`, `A_paid_save_already_in_a_slot_on_this_device_is_spotted`.)*

### 15.3 Ability bar and Bestiary

- `HeroSave.Known` lists learned ability ids; `HeroSave.Bar` the five bar entries. The battle reads the bar through `BattleFactory.SpecFor`. *(Tests: `Save_round_trips_through_json`, `Full_loop_battle_commit_then_every_vendor`.)*
- Bestiary: **Seen** for every class spawned — recorded at commit even on defeat; **Defeated** counts kills on victory. **Amended 2026-11-06 (owner, GG-T290):** ~~**Scanned** for Scan targets~~ — Scan is retired; what the party knows of a class is `BestiaryKnowledge.Tier(entry)` (Meta, `SaveData.cs`), read from `Defeated` alone: Seen → name, description, lore and drops; **1** defeat → Speed; **3** → HP and base stats; **6** → elemental weaknesses and resistances; **10** → abilities, brain and targeting (`DefeatsForSpeed/Stats/Elements/Abilities`). An old save's `Scanned` flag is kept and counts as at least the stats tier. *(Tests: `BestiaryKnowledgeTests`.)*

### 15.4 Player profiles

**Added 2026-11-02 (owner, GG-T99; reverses the "settings are per save slot" amendment).** Profiles come from the shared `com.mindattic.unity.shared` package (`Packages/manifest.json` → `file:../../Unity.Shared/unity/com.mindattic.unity.shared`; its engine-free `Unity.Shared.Core` is referenced by `GridGame.Meta` and compiled into the dotnet test build):

```
persistentDataPath/Profiles/
├─ current.txt                 the profile in use
├─ <Name>/settings.json        {"format":"mindattic.settings","version":1,"common":{…},"game":{…}}
│   common: musicVolume, sfxVolume, muteMusic, muteSfx, reduceMotion, language   (every MindAttic game)
│   game:   battleSpeed, dragTimer, colorblindMode, pincerCutIns, confirmActions,
│           menuTheme, fontName, difficulty{level}   (1..10, default 10; §22.9)
│           customThemes[], themeTones{<theme>: {brightness, contrast, gamma}}   (§11.5, GG-T407/GG-T563)
├─ <Name>/Saves/slot1-3.json   that profile's save slots (§15.1)
└─ .trash/<Name>~<UTC>         deleted profiles, moved aside, never erased
```

- `Meta/PlayerProfiles.cs`: `GameSettings` maps `SettingsSave` ↔ settings.json (unknown fields are kept; the CLI `-muted` session mute is never written back); `ProfileSession` is the active profile + its settings (switch / create / rename / delete; switching reloads settings from the new profile's file); `SaveFiles` is the slot file shape shared by `SaveStore` and the tests.
- **Settings follow the profile, not the slot:** `GameApp.Settings` is the active profile's, applied at boot (so the Title already wears the player's theme, font and volumes) and the same whichever slot is loaded. The Settings screen (reachable from the Title as well as Camp) edits it and writes settings.json on every change.
- **Title:** a **Profile** row shows the current profile; it opens the picker (`ProfileSelectScreen`: tap to switch; Rename; Delete with a confirm; **+ New profile**), and `ProfileNameScreen` takes a name (create — which also switches to it — or rename), refusing names that can't be a folder on every platform or already exist. Deleting the last profile leaves a fresh `Default`. Code-built like every other screen.
- **Migration** (`LegacySaveMigration`, once, first launch): `persistentDataPath/Saves/slot*.json` are copied into the `Default` profile (a slot it already has is never overwritten), the most recently played slot's (by its `UpdatedUtc`) old `Settings` block seeds `Default/settings.json`, and the old folder is renamed `Saves.migrated`. Nothing is deleted.
- **Automation:** `-saveRoot DIR` keeps every profile under `DIR/Profiles` (the harness passes its run folder, so harness and tests never touch real profiles); Unity.Shared's `-profiles-root DIR` / `MINDATTIC_PROFILES_ROOT` name the folder outright; `-profile NAME` picks (or creates) a profile; `-demo` and `-testmode` play under a throwaway `Throwaway` profile in a temp-cache folder cleared at launch. The automation endpoint has `profile?op=list|switch|create|rename|delete`, `goto` knows `profiles` and `newprofile`, and `python tools/harness.py profiles` shoots the picker, the name screen and Settings and checks settings follow the profile.

*(Tests: `ProfileTests` — `Migration_moves_every_legacy_slot_into_the_Default_profile_and_keeps_the_old_folder`, `Migration_seeds_settings_from_the_most_recently_played_slot`, `Migration_runs_once_and_does_nothing_without_a_legacy_folder`, `Migration_never_overwrites_a_slot_the_profile_already_has`, `Each_profile_has_its_own_save_slots`, `Each_profile_has_its_own_settings`, `Switching_profiles_reloads_settings_and_remembers_the_choice`, `A_session_mute_never_reaches_settings_json`.)*

---

## 17. Workflow Discipline

**Amended 2026-10-09:** "Code-Only Workflow" is now literal — there is no editor-side authoring at all.

- Never ask the user to open the Unity Editor to place, drag or wire anything. A screen is C# in `App/Screens` or `App/Battle`; a rule is C# in `Core`; content is JSON.
- Rules change in `Core` first, with a test in `Tests/Core.Tests`; the view adapts to events.
- Never force-close Unity; `tools/unity.ps1` refuses to run while the project is open — ask the user to quit the Editor cleanly.

### 17.1 Common pitfalls

1. **Deciding rules in the view.** If `BattleScreen` needs to know an outcome, ask `BattleEngine` (`PreviewMove`, `GetSlotStatus`, `ValidTargets`, `ActionsUntilTurn`) — never re-implement.
2. **Non-determinism in Core.** No `System.Random`, `DateTime`, `UnityEngine.Random` or dictionary-order dependence in rules; the replay test catches it.
3. **Mutating state in a preview.** Previews run on `BattleState.Clone()` with `PreviewOnly` (no events, no traps/wake-ups); keep it that way.
4. **Missing glyphs.** A new symbol in UI text must pass `check_glyphs.py`.
5. **Unknown JSON fields.** `MissingMemberHandling.Error` fails the load — add the field to `ContentModels.cs` first.
6. **PowerShell 5.1 encoding.** Never round-trip `.cs`/`.json` through `Get-Content`/`Set-Content` (BOM-less UTF-8 mojibakes); use an editor or .NET `WriteAllText` with `UTF8Encoding(false)`.
7. **Playback stalls.** Every animation runs through `Safe()`, the final `SyncAll()` snaps views to truth, and a 20 s watchdog resyncs — don't bypass them.
8. **Worktree package resolution (GG-T159).** `Packages/manifest.json` pulls `com.mindattic.capture`/`com.mindattic.telemetry` via `file:../../MindAttic.Capture(/Telemetry)/unity/...`, two levels above the project root. In the main checkout that lands on the real sibling repo; in a per-ticket worktree (`<WorkspaceRoot>.worktrees\Unity.GridGame\<Ticket>\`) the same climb instead lands on `<WorkspaceRoot>.worktrees\Unity.GridGame\<Repo>`, which has no repo of its own. `tools/unity.ps1` now auto-creates a directory junction there (pointing at the real sibling repo, found by walking up to the nearest `*.worktrees` ancestor) the first time it's missing, so `tools/build.ps1`/`tools/test.ps1 -Compile` work in a fresh worktree with no manual setup; junctions, not symlinks, because creating a symlink needs admin/Developer-Mode privilege on Windows and junctions don't.

### 17.2 Cadence

- Keep going until the feature works end-to-end before committing.
- `pwsh tools/test.ps1` after every rules/content change; `-Compile` after App changes (Editor closed).
- `pwsh tools/build.ps1` then `python tools/harness.py smoke` / `sweep` for anything visible.

### 17.3 Checkpoint recipe

```
1. pwsh tools/test.ps1              → all tests + glyph check green?
2. App touched? pwsh tools/test.ps1 -Compile → zero "error CS"?
3. New rule? docs/BIBLE.md updated (§32) and a test named for it?
4. Visible change? build.ps1 + harness.py smoke/sweep → screenshots in artifacts/, layout audit clean?
5. USER_STORIES.md status + evidence updated?
6. Commit once.
```

### 17.4 Naming conventions

`*Def` = a JSON content row · `*Event` = something that happened · `*Command` = a player decision · `BattleRunner.<Concern>.cs` = one rules concern per partial file · static classes in `Meta` = Camp services returning `Result` · `*Screen` / `HubPage` = a UI Toolkit page · `*View` = presentation of a Core object (`BoardView`, `UnitView`).

### 17.5 File-edit cookbook

| Goal | File(s) | Remember |
|---|---|---|
| Tune a battle number | `Core/Battle/BattleConfig.cs` | update the bible table; run soak |
| Change a formula | `Core/Rules/Formulas.cs` | §13.1 |
| Add a spell / skill | `abilities.json` (+ a class's `Abilities`/`DefaultLoadout`) | §7 row; `CatalogTests` covers it automatically |
| Add a buff | `buffs.json` (+ a new knob in `BuffDef` and its hook in `BattleRunner` if needed) | §8 |
| Add an enemy | `classes.json` + a `stages.json` wave (+ `droptables.json`) | `Resources/Portraits/<name>-<NN>.png` (§12.1.3) + a `Thumbnail` framed in GridGame.Studio's Thumbnails tab (§12.1.1) |
| Add an item / recipe | `items.json` / `recipes.json` | `Every_recipe_is_craftable_with_its_inputs` |
| Tweak AI weights | `Core/Rules/EnemyPlanner.cs` | §14.1.2 |
| Add a Camp service | `Meta/MetaServices.cs` + a screen in `App/Screens/HubScreens.cs` + hub row + `StartScreens` id | `MetaLoopTests`, harness sweep list |
| Add a save field | `Meta/SaveData.cs` + `SaveMigrations` (bump `CurrentVersion` when shape changes) | round-trip test |
| Add a battle animation | `BattleScreen.Playback.cs` `Animate` switch | awaited beat, `Fx` for motion |
| Add a sound | `Resources/Audio/Sfx` + `Audio.Cue` | Credits.txt attribution |

---

## 18. Glossary

The L0 [§9 Glossary](#GG-§9) is canonical; additional terms: **Armored** (snake member taking 0 while a later member lives) · **Escape** (Smoke Bomb exit; wounds kept) · **Control return** (end of an action: hero buffs/cooldowns and enemy Frozen tick) · **Interrupt outcomes** (*Clutch* → resolve now, *Shrug* → no delay, *Stagger* → delay accumulates, *Cancel* → delay ≥ cast time, orbs lost) · **Passive** (always-on class ability: Double Attack, Counter Attack) · **Sentient** (an enemy with a society or that plans ahead — may be a pincer attacker; heroes always; replaced Humanoid 2026-11-06, GG-T289) · **Coins** (gold picked up in battle, banked on victory) · **Materials** (crafting inputs) · targeting axes **Shape / Mode / Filter** ([§5](#5-targeting)).

---

## 19. How to Add a New Spell — Checklist

1. Add a row to `abilities.json`: `Id`, `Name`, `Description`, `Kind: "Spell"`, `Cost` letters, `CastTime`, `Shape`/`Radius`/`Mode`/`Filter`, `Motion`, `DamageType`/`BaseDamage` or `BaseHeal`, optional `ApplyBuff`/`ApplyBuffChance`/`RemovesDebuffs`/`HastenU`, `Icon`.
2. Every hero can now **learn** it at Camp (general spell catalog, [§25.6](#256-abilities)); add it to a class's `Abilities`/`DefaultLoadout` to make it signature or starting.
3. New debuff? Add it to `buffs.json` (letter + color).
4. Add `Resources/Icons/<icon>-00.png` (§12.1.3).
5. `pwsh tools/test.ps1` — `CatalogTests.Every_hero_ability_resolves_against_a_live_board` exercises it.
6. Add the §7 row.

### 19.1 Worked example: "Quake"

Brief: pick a tile, hit a 3×3 square of enemies for 16 Physical, no debuff.

```json
{ "Id": "quake", "Name": "Quake", "Description": "The ground heaves under a 3×3 area.",
  "Kind": "Spell", "Cost": "RB", "CastTime": 1.5,
  "Shape": "Square", "Radius": 1, "Mode": "PickTile", "Filter": "EnemyOnly",
  "Motion": "None", "DamageType": "Physical", "BaseDamage": 16, "Icon": "Quake" }
```

Learn price: 120 + 80 × 2 orbs = 280 G. What you don't touch: the dispatcher, targeting UI, cast icons, bank, HUD — data only ([GG-LAW-5](#GG-LAW-5)).

---

## 20. How to Add a New Enemy — Checklist

1. Add a `ClassDef` to `classes.json` with `Team: "Enemy"`, `Tags` (`Sentient` if it has a society or plans ahead and so may pincer — vampires and mummies yes, mindless undead and beasts no, GG-T289/GG-T294), `BaseStats`, `Growth`, `Resistances`, `XpReward`, `MoveRange`, optional `DropTable`, `ChargeAbility`/`ChargeShape`, `Trap`, `BossPhases`, `Footprint`, `SnakeSegments`. **No story text** (GG-T355, [§27.2](#272-narrative-sourcing-prose-gg-t355)): `Description`, `Lore` and boss-phase `Announce` lines are not fields of `classes.json` any more.
   - The class's keys exist as soon as it does: `class:<id>` (its blurb and Bestiary lore) and `bark:bossphase/<id>/<n>` for each boss phase. Until Prose has its text it shows `(missing text: class:<id>)` and `ContentLoader.Validate` names the key.
   - Draft the words in Prose, never in this repo: `prose --texts-draft gridgame <dir>` with a `prose-text/1` file per key (`tools/prose_seed.py` shows the shape); agents only draft (RFC 0009). Run `kit push --map` so the universe gains `class-<id>`. When the owner approves and publishes, the next build's pull writes it into `Content/Text`. `python tools/prose_text.py check` lists the keys still without a text.
2. Add the class's portrait (`Resources/Portraits/<name>-<NN>.png`, §12.1.3) as a **1024×1024 transparent canvas**, never trimmed or shrunk (owner 2026-11-07: "all characters are 1024x1024 with transparent backgrounds; that's the rule, that's what the assets are", [§12.1.2](#1212-portraits-are-1024-canvases-added-2026-11-07-owner-rule-gg-t352)). Run `GridGame.Studio/tools/import_art.py`, or for owner art on a white background `python ../GridGame.Studio/tools/portrait_cut.py <image> --import <id>`, which erases the background and keeps white cloth and translucent smoke (GG-T327). Either one writes the canvas and the class's `"Figure"` box (add the class first, or copy the box it prints). The import names the file and writes the class's `"Portrait"` stem (and `"PortraitVariant"`) when it has none; a later GridGame2026 `import_art.py` run reads only classes whose `"ArtSource"` names an original there, and give it a `Thumbnail`: frame it in GridGame.Studio's Thumbnails tab ([§12.1.1](#1211-thumbnails-and-portraits-added-2026-11-03-gg-t220-reworked-2026-11-23-gg-t713)); there is no separate thumb image.
3. Put it in a `stages.json` wave; optionally add a `droptables.json` table.
4. `pwsh tools/test.ps1` — validation, catalog and soak include it automatically. The Bestiary lists it (silhouette until seen).

### 20.1 Worked example: "FrostWolf"

A Beast `{"Id":"frostwolf","Team":"Enemy","Tags":["Beast"],"Resistances":{"Ice":0.0,"Fire":2.0}, "XpReward":25,"MoveRange":1,"DropTable":"wolf00", …stats}` never pincers; `EnemyPlanner` closes distance and melees; coins default to ~½ XP; the Bestiary shows it as a silhouette until seen.

---

## 22. The Macro Loop

```
Title ─▶ (Continue | New Game → slot → CharacterSelect (pick 3) → Prologue crawl | Load Game → slot)
   ▼
Camp (HubScreen)  ◀══════════════════════════════════════════╗
   │  Adventure ─▶ stage list (newest-first)                 ║
   │  Crucible ─▶ endless waves, keeps everything (§22.10)   ║
   ▼                                                         ║
StoryCrawl (first entry into a theme only)                   ║
   ▼                                                         ║
Story beats (dialogue; a finale's boss intro; §27.1)         ║
   ▼                                                         ║
Battle — clear every wave                                    ║
   ▼                                                         ║
LevelUp — one hero at a time, only when someone leveled ═════╣
   ▼                                                         ║
PostBattle — gold, items, unlocks, bounty ════════════════════╝
             (Continue → [level interlude] → Camp · Next stage › → Battle · Retry → Battle)
             (first win over the Hierarch: Continue → Ending crawl → rolling Credits → Title)
```

- **Stage = waves.** Clearing a wave spawns the next immediately inside the same action; clearing the last wave is victory.
- **Reward beat.** `BattleFactory.Commit` writes XP, gold, items, kills, unlocks and slain bounty targets, then the save is persisted and `PostBattleScreen.RouteFrom` opens `LevelUpScreen` first if anyone leveled, else `PostBattleScreen` directly.
- **No Overworld** ([§28](#28-no-overworld)).
- **Test Mode loop (GG-T93):** `-testmode` runs this same loop with the bot at every seat — Camp → Battle → LevelUp → PostBattle → **Camp, where `CampRoutine` spends before every launch** — skipping Title/CharacterSelect/StoryCrawl/tutorial, on a party-scaled save whose enemy level tracks the party's level (see [§6](#GG-§6)).

### 22.1 Per-transition state contract

| From → To | Commit before | Hydrate on arrival |
|---|---|---|
| Camp → Battle | nothing (every Camp action already persisted) | `BattleFactory.Build`: party specs (level from XP, carried HP, weapon + durability, equipped items, bar), inventory counts for item stacks, unlocked slots, recommended level = campaign index + 1 |
| Battle → LevelUp → PostBattle | `BattleFactory.Commit` + `Persist` | `LevelUpScreen` (only if `BattleReport.LevelUps` is non-empty) then `PostBattleScreen` render the `BattleReport` |
| PostBattle → Camp / Battle | already saved | screens rebuild from `App.Save` |

**Commit rules** (`BattleFactory.Commit`) — **Amended 2026-10-09** where marked, **Amended 2026-10-11** (Retreat) where marked:
- Always: deduct consumed items; record Seen (Scanned retired 2026-11-06, GG-T290).
- **Victory**: gold += coins (also `LifetimeCoins`); add item drops; add kills to Bestiary; **every party hero — alive or fallen — gains the battle's full XP total** (not split, *Amended*); HP: level-up → full, survivor keeps wounds, fallen → 1 HP; weapon durability written back (a shattered weapon is removed); stage marked cleared, `HighestClearedStage` raised, next stage named as unlocked. A bounty target that fell is marked slain on any ending (GG-T431, §22.4).
- **Defeat**: nothing gained but hard-won XP (below); party HP reset to full.
- **Retreat** (*Amended* — no longer a Defeat): keeps hard-won XP plus `BattleConfig.RetreatKeepFraction` (50%) of the battle's gold and item counts so far, floored; party HP reset to full, same as Defeat.
- **Escaped** (*Amended*): nothing gained; wounds kept (fallen → 1 HP).
- **Crucible** (*Amended 2026-10-19*, [§22.10](#2210-the-crucible)): however the run ends — wipe, leave, escape or (in theory) clearing every wave — it **keeps everything**: gold, item drops, the full XP total (level-ups and skill points), kills and gear wear; party HP reset to full. Never touches cleared stages, `HighestClearedStage`, unlocks, `LastStage` or the expedition. `BattleFactory.Commit` hands any `crucible-*` stage to `Crucible.Commit`.

### 22.2 Failure path detail

**Amended 2026-10-11 — hard-won experience:** a defeat now keeps the XP of every foe felled (gold and loot are still lost); the results screen shows "Hard-won experience +N each" and any level-ups. Found with the battle records: with a 20-wave first battle and no reward for losing, a learning player could never get stronger.

- **Defeat** (all heroes down): "DEFEAT", no rewards but hard-won XP, party restored to full, PostBattle offers **Retry** and **Continue**.
- **Retreat** (pause menu → Retreat, confirmed) — **Amended 2026-10-11: FFIV-style partial loss, no longer a Defeat.** Its own `BattlePhase.Retreated` (mirrors how `Escaped` already gets its own phase); `BattleFactory.Commit` keeps hard-won XP plus `RetreatKeepFraction` (50%) of the battle's `Results.Coins`/`Results.Items` accumulated so far, lost otherwise. PostBattle shows "RETREATED" and the partial haul. The pause-menu row and confirm dialog quote the exact kept percentage. Mid-expedition, a retreat still ends the expedition via the normal non-victory path ([§22.5](#225-expeditions)).
- **Escape** (Smoke Bomb): "ESCAPED", no rewards, wounds remain. **Amended 2026-10-09** (new). No permadeath ([§29](#29-open-design-questions)).
- **The Crucible is the exception** (*Amended 2026-10-19*): a wipe, Leave or escape there keeps the whole haul and all XP, party restored to full ([§22.10](#2210-the-crucible)).

### 22.3 Adventure — the stage list

**Story flavour superseded 2026-11-21 (GG-T575).** The stage list, order, waves, levels and save rules in §22.3 and its subsections still hold. Their story flavour does not: it was written for the retired premises (*Under Morrow*, then *A War in Heaven*), so "Morrow" as the moon, "the moon" coming home or hanging low, the Nearing, moon-changed beasts, fallen moon-rock and the Pale Concord's tithe are retired history. The sky now holds the ordinary **Moon** (fixed size) and **the Manalith**, which is not a moon (an ancient thing stopped long ago, hanging in the sky and orbiting, coming closer chapter by chapter until it fills the sky over Morrow's Shadow; GG-T577); the life-force is **mana**, never essence; read every region's story against the canon (Prose `gridgame`, staging copy `Data/Prose/canon/`) and [§27.3](#273-the-sky-two-moons-and-the-staged-backdrop-gg-t575-design-gg-t576-builds-it). Region and chapter names stay, including **Morrow's Shadow** ("morrow" = tomorrow). A painted moon named in a backdrop entry below describes the old art, which GG-T576 splits and repaints.

**Amended 2026-11-16:** stage names are the region name plus a numeral (the Green Valley/Sandsea/Mireholt/Frostmaw/Veshker leftovers are retired; greenvalley2 is The Free Holds, GG-T386). Adventure is region, then stage, each row Cleared/New; a cleared stage is a **hunt** when no expedition is under way (`Expeditions.IsHunt`), rolling drops and wanderers afresh (GG-T388).

**Amended 2026-10-09 (owner decision):**
- **Unlocked stages only, newest first.** The list shows every unlocked campaign stage in reverse play order, so the frontier is on top; **all locked stages collapse into one teaser row** ("◇ N more stages ahead…", disabled).
- **Stage names are "<Theme> I / II / III"** (e.g. "Tithe Moors I", "Shardfall III").
- **Row content:** ★ cleared / ☆ not yet, the name, "Lv N" (recommended level), and a second line "<Theme> · N waves · drops: X, Y, Z" from the wave enemies' drop tables (first 3 distinct items) — the farming hint.
- **Unlock gating:** stage index i is unlocked when `i ≤ HighestClearedStage + 1` (`Progression.IsStageUnlocked`): linear forward, fully open backward; every unlocked stage is replayable.
- **Farming is the point:** cleared stages stay replayable and drop tables differ per enemy.
- A strip above the list shows each hunted bounty's clue (GG-T431); a region or stage a hunted target roams is marked ☠.

**Difficulty curve:** `Progression.RecommendedLevel(i) = i + 1`; spawned enemy levels are `max(authored, recommended)`; test stages are exempt. XP and coins scale with enemy level. The player's difficulty dial ([§22.9](#229-difficulty-settings-scaffolding-added-2026-10-11-owner-direction)) then scales the enemy side down from the designed 10 and the rewards with it.

**Amended 2026-11-05 (GG-T261, POL-G1/G2):** the casual-bot curve is measured by `python tools/polish.py --balance` (casual bot, rotating five-hero parties at the stage's level). Before: Anchor Wastes I–III and Halcyon I–III won 0 %, Tithe Moors III 0 %, Tithe Moors II and Shardfall II 100 % — the zeros were the remains rule (§14.5), not the waves. After the remains fix, wave trims where losses clustered and additions where it was a walk-over — Shardfall I wave 5 (−1 Mecha Armor), II waves 1/4 (+Scorpion, +Soldier), III waves 2/4 (+Tech Gremlin, +Vulture); Arkgrave I wave 5 (Hag → Lurker); Tithe Moors III waves 2/3 (+Wolf, +Ganger); Anchor Wastes I waves 3/4 (a Goblin Thug for one Ceramic Knight and for the Troll), II wave 3 (+Goblin Thug), III waves 2/5 (a Goblin Thug for one Ceramic Knight, −Ice Mauler; the Cyclops' GravityStamp stays); Halcyon II wave 1 (−Bat), III waves 1/2/4 (the two-undead Flank group, one Bat, −Undead) — every main stage now wins 25–100 % with 8 rotating parties (most 50–88 %; party make-up swings a stage ±25 %). POL-G1 17.4 → 31.0, POL-G2 71.4 → 78.6.

Campaign (`campaign.json`): Tithe Moors, The Lantern Road (GG-T345, §22.3.2), Shardfall, Arkgrave, Anchor Wastes, The Still (GG-T344, §22.3.1), Halcyon, The Choir's Cathedral (GG-T350, §22.3.3) — 3 stages each, **5 hand-authored waves per stage** ([§22.7](#227-the-first-battle), amended 2026-10-12 — every stage normalized to 5, including Tithe Moors I, which had been 20), generally grouped by enemy family (frogs with frogs, goblins with goblins) rather than mixed. **No two campaign waves are the same encounter** (*added 2026-10-22, GG-T184*): the 19 waves that repeated an earlier wave's foes now name an `encounters.json` group of the same family instead (`WaveDef.Group`), and `ContentLoader.Validate` fails on any repeat. *(Tests: `EncounterCatalogTests`.)* Final boss: the Hierarch (its own enemy class `hierarch` since GG-T350, The Choir's Cathedral III; it spawns at the stage's recommended 24). ~~(`vampire`, level 10 authored, Halcyon III)~~ **Added 2026-11-05 (GG-T234):** every main-biome finale (each theme's third stage) is fought against a **rule-breaking boss** ([§14.9](#149-rule-breaking-bosses-added-2026-11-05-owner-direction-gg-t234)): the Captain (TwinTurn), an oni (Undying, The Lantern Road III, GG-T345), Mecha Armor (AxisWard), Swamp Mistress (MireSwap), Cyclops (GravityStamp), the Chronomancer (TwinTurn, The Still III, GG-T344), a Kiln Guard captain (AxisWard, Halcyon III, GG-T350) and the Hierarch (Undying, The Choir's Cathedral III, GG-T350).

#### 22.3.1 The Still (added 2026-11-07, owner direction, GG-T344)

Owner, with six new landscapes: *"I will generate more landscapes from which you can extrapolate more story, settings levels, etc"*. The six images (a vast white Concord dome inside: mana tanks glowing cyan, a moon-oculus, mirror-polished floors, rails and catwalks, arched gates) became **The Still** (`still`), a sixth main theme between the Anchor Wastes and Halcyon — the great Still at the foot of Halcyon's spire where every Anchor beam's tithe is collected into tanks before it climbs to the Hierarch ([STORY.md](STORY.md) §5.5).
- **Campaign:** `campaign.json` order is now Tithe Moors, Shardfall, Arkgrave, Anchor Wastes, **The Still**, Halcyon, then the five postgame themes. Milestone reward `tome_mana_siphon`. Backgrounds: the owner's six landscapes as `Backgrounds/still_01..06.jpg` (`"Backgrounds": "still"`). Three stages, five waves each, like every main theme: **The Still I** (`still-01`, the queues: three Hollow; the Bulwark gate guard, Shieldbearer and Rifleman; two Chromed Hollow and a Hollow carrying the cans; a Red Seal warden with a Chromed Hollow; a Choir Confessor and two Hollow at the head of the line), **II** (`still-02`, the tank hall: two Chromed Hollow and a Hollow; Cantor and Confessor; Siphoner, Shieldbearer and a Hollow; two vat-giants grown in the tanks, `cyclops03/04`; a Psalmist, a Chromed Hollow and a Hollow), **III** (`still-03`, under the oculus: two Hollow and a Chromed Hollow; the Siege Gunner and a Rifleman; Cantor and Psalmist; a vat-giant and a Hollow; then the **Chronomancer** with TwinTurn beside a Shieldbearer and a Confessor, §14.9). Board tile `Tiles/still.png` (`GridGame.Studio/tools/make_tiles.py still`, one polished white slab with pooled mana); the tanks stand on the board as blocks.
- **Levels:** `RecommendedLevel(i) = i + 1` puts the Still at 13–15 (the Bulwark's and Choir's Crucible band) and moves Halcyon from 13–15 to 16–18 (the Hierarch fights at 18). *Since GG-T345 (§22.3.2) both are three higher: the Still 16–18, Halcyon 19–21.*
- **Story beats:** the crawl, `still_party_talk` (`ThemeStart`, the Enchantress on the queues), `boss_intro_still` (`StageStart still-03`: the Chronomancer shows them tank nine — a whole hold, Fennick's Rise, drowned and turning — and the crew learns the Hollow aren't empty, they're *stored*), and a side quest, **The Hymn Stops Here** (`q_hymn_stops`, the Enchantress: defeat three Choir Cantors).
- **Balance** (24 seeds, campaign parties of six, difficulty 10, `AbilityUsageSoak` with `GRIDGAME_ABILITY_STAGE`): casual **14/24, 20/24, 15/24** (58–83 %), smart **24/24** on all three. The first cut had two Templar pairs and a Templar beside the Choir and measured casual 6, 7 and 5/24; the losses clustered at those waves, so the Red Seal thinned to one warden per wave. Halcyon at 16–18 instead of 13–15 measured casual 6, 10 and 20/24 against 9, 15 and 19 before, with losses at the same waves (Halcyon I wave 3, Halcyon II wave 5). One bat came off each of those waves, and Halcyon now measures **8, 19 and 20/24**, smart 24/24.
- **Old saves:** `HighestClearedStage` is a campaign index, so save **v5** moves any save already past the Anchor Wastes up by three (`SaveMigrations.StillInsertedAt` 12, `StillStageCount` 3): the same furthest clear, the Still open behind it.
- **Tools:** the ability-usage and timeline soaks play the main campaign (every campaign stage before the first `LevelOverride`) instead of "the first 15", and `GRIDGAME_ABILITY_STAGE` takes a comma list.
- *(Tests: `StillTests`; `StoryArcTests` and `RuleBreakTests` list six finales; `QuestTests` wants a quest in every main theme.)*

#### 22.3.2 The Lantern Road (added 2026-11-07, owner direction, GG-T345)

Owner, with four new landscapes: *"I will generate more landscapes from which you can extrapolate more story, settings levels, etc"*. The four images (a lantern-lit pilgrim road climbing mountain gorges at night: stone stairs, shrines, torii, bamboo, rope lines of paper lanterns, a low full moon) became **The Lantern Road** (`lanternroad`), a seventh main theme between the Tithe Moors and the Shardfall: the crew climbs the pilgrim road out of the moors toward the desert ([STORY.md](STORY.md) §5, a structural entry only).
- **Campaign:** `campaign.json` order is now Tithe Moors, **The Lantern Road**, Shardfall, Arkgrave, Anchor Wastes, The Still, Halcyon, then the five postgame themes (main campaign 21 stages, 31 in all). Milestone reward `tome_steadfast`. Backgrounds: the owner's four landscapes as `Backgrounds/lanternroad_01..04.jpg` at their native 768×1536, JPEG q95 (`"Backgrounds": "lanternroad"`). Board tile `Tiles/lanternroad.png` (`GridGame.Studio/tools/make_tiles.py lanternroad`: one moonlit blue-grey flagstone, moss, a warm lantern pool). Three stages, five waves each, existing units only (the oni road-raiders, the gorge's moon-changed Wild, a little Redline and militia on the caravan's trail): **The Lantern Road I** (`lanternroad-01`: two wolves; three bats; two oni; two steppin razors; an oni with a wolf and a bat), **II** (`lanternroad-02`, two stone lanterns as blocks: a flying monkey and a bat; two steppin razors and a wolf; two oni; a werewolf with two wolves; a Redline ganger and a militia soldier), **III** (`lanternroad-03`, two blocks: two bats and a flying monkey; two steppin razors; a wolf and a werewolf; an oni and a steppin razor; then the **oni chief** `oni02` with **Undying** beside an oni and a wolf, §14.9).
- **Levels:** `RecommendedLevel(i) = i + 1` puts the Lantern Road at 4–6 and moves every later chapter three levels: the Shardfall 7–9, the Arkgrave 10–12, the Anchor Wastes 13–15, the Still 16–18, Halcyon 19–21 (the Hierarch fights at 21). Postgame stages keep their `LevelOverride`.
- **Story text is placeholder draft.** The owner ruled that story is authored in Prose, not in this repo (Prose RFC 0017, GG-T355), so the chapter carries only short functional text: the crawl, `lanternroad_party_talk` (`ThemeStart`, the Lantern Merchant on the road), `boss_intro_lanternroad` (`StageStart lanternroad-03`) and a side quest, **Masks Off** (`q_masks_off`, the Lantern Merchant: defeat three oni, so `QuestTests` still finds a quest in every main theme). All of it is to be replaced by approved Prose text.
- **Balance** (24 seeds, campaign parties, difficulty 10, `AbilityUsageSoak` with `GRIDGAME_ABILITY_STAGE`): the Lantern Road casual **19/24, 16/24, 15/24**, smart **24/24** on all three. The first cut measured casual 19, 12 and 6/24 with losses spread over the oni and steppin-razor waves, so Lantern Road II lost a wolf from its oni wave and a Redline ganger from wave 5, and III thinned waves 2–5 by one foe each. Every later main stage re-measured (casual wins of 24, before → after; smart stayed 23–24/24 everywhere): Shardfall 18/14/13 → 15/13/14, Arkgrave 10/4/6 → 10/5/4, Anchor Wastes 14/6/4 → 12/8/7, the Still 14/20/15 → 11/18/17, Halcyon 8/19/20 → 13/18/20. The insertion also moves every later stage's seeds and rotating parties, so about ±3 of 24 is noise. Two light trims where the shift pushed a stage below where it was: Shardfall III wave 5 lost a soldier (11 → **14**/24) and Arkgrave II wave 4 lost its toad (1 → **5**/24). Already low before this change and left to a balance pass: Arkgrave II–III and Anchor Wastes II–III (casual 4–8/24) and Tithe Moors I (6/24, smart 22/24).
- **Old saves:** save **v6** moves any save already past the Tithe Moors up by three (`SaveMigrations.LanternRoadInsertedAt` 3, `LanternRoadStageCount` 3), after v5's Still shift: the same furthest clear, the Lantern Road open behind it.
- **Armor sets:** every set from level 4 up moves three levels (§24.2.3); the top set, Dreadwyrm, tracks the 31-stage campaign.
- *(Tests: `LanternRoadTests`; `StoryArcTests` and `RuleBreakTests` list seven finales; `BackgroundsTests` expects the four-image `lanternroad` set; `StillTests` follows the Still's new levels and index.)*

#### 22.3.3 The Choir's Cathedral (added 2026-11-07, owner direction, GG-T350)

Four owner landscapes (a cathedral nave at night: an organ of mana pipes glowing cyan, rose windows, pews by the hundred, a moon in the vault) became **The Choir's Cathedral** (`cathedral`), the main campaign's eighth theme and **new finale**: the cathedral at the top of Halcyon's spire. The organ is the Hierarch's reservoir; the Choir sings the tithe up into it ([STORY.md](STORY.md) §5, a structural entry only).
- **Campaign:** `campaign.json` order is now Tithe Moors, The Lantern Road, Shardfall, Arkgrave, Anchor Wastes, The Still, Halcyon, **The Choir's Cathedral**, then the five postgame themes (main campaign 24 stages, 34 in all). Milestone reward `tome_counter_attack`. Backgrounds: `Backgrounds/cathedral_01..04.jpg` at their native 768×1536, JPEG q95, fresh-GUID metas (`"Backgrounds": "cathedral"`; mean luminance 0.29). Board tile `Tiles/cathedral.png` (`GridGame.Studio/tools/make_tiles.py cathedral`: one dark marble slab, pale veining, a pool of cyan organ light); the pews stand on the board as blocks. Three stages, five waves each, the Choir and the Concord only (two foes a wave, a Chromed Hollow as the lighter half): **I** (`cathedral-01`: a Cantor and a Chromed Hollow; a Kiln Guard and a Chromed Hollow; a Red Seal templar and a Chromed Hollow; a Bulwark Rifleman and a Chromed Hollow; the Siege Gunner and a Psalmist), **II** (`cathedral-02`: a Psalmist and a Chromed Hollow; a Siphoner and a Chromed Hollow; a Kiln Guard and a Cantor; a templar and a Chromed Hollow; a Shieldbearer and a Confessor), **III** (`cathedral-03`: a Cantor and a Chromed Hollow; a Kiln Guard and a Chromed Hollow; the Siege Gunner and a Confessor; a templar and a Chromed Hollow; then the **Hierarch** with **Undying** beside a Cantor and a Confessor, §14.9).
- **The Hierarch** is its own enemy class, `hierarch` (STORY.md §3.1), so the playable `vampire` hero is no longer fielded as the boss. Kit, all existing systems: charge **Tithe Draught** (`charge_tithe_draught`, 2.5 s, rests a turn: 5 Dark to every hero, and it heals for each hero it touches — a drain through the organ; interrupt it), boss phase at half HP **Open the Reservoir** (`hierarch_open_reservoir`, free and enemy-only, in `Abilities.EnemyOnly`: heals 25, Empowered, a 0.35 haste on its own icon; it prefers its charge from then on), **Vampire's Kiss** lifesteal on every landed blow, and the **Undying** rule-break (it rises once at half HP while its Choir still stands — silencing the Choir is how the party breaks the reservoir). Mesmerize support (Slowed, reach 2 like every boss). Drop table `vampire`; the Hierarch bounty (`bounty_vampire_lord`) now targets `hierarch`.
- **No portrait yet** (owner 2026-11-07, GG-T351: missing art uses the mannequin). The class has no `Portraits/hierarch.png`; it declares `"Portrait": "mannequin"` and carries the mannequin's own `Thumbnail` and `Figure` values, so validation (every class needs both, GG-T352) passes and every view frames the fallback texture exactly as the mannequin's own class does (`Art.FigureRect` reads the box of the class the texture belongs to). `CatalogTests` accepts a class that declares the mannequin placeholder in place of its own portrait file, checks it copies the mannequin's framing, and fails once real art lands so the placeholder is dropped. Dialogue speakers name it with `"Class": "hierarch", "Portrait": "mannequin"`.
- **Halcyon III** keeps its waves; its finale now ends on a **Kiln Guard captain** (`ceramicknight06`, AxisWard) beside a Hollow, holding the spire stair (the Reaper that stood beside the Hierarch came off for balance). `boss_intro_cityruins` is his (placeholder draft); the Hierarch's own lines moved verbatim to `boss_intro_cathedral`. The `Ending` ("Still Here") follows `cathedral-03` and uses the cathedral backdrop.
- **Levels:** the Cathedral runs at 22–24 (the Hierarch fights at 24); nothing before it moves. Postgame stages keep their `LevelOverride`.
- **Story text is placeholder draft** (story is authored in Prose, GG-T355): the crawl, `cathedral_party_talk` (`ThemeStart`, the Enchantress on the organ), the Kiln Guard's intro and a side quest, **Quiet the Organ** (`q_organ_quiet`, the Enchantress: defeat three Choir Confessors).
- **Balance** (24 seeds, campaign parties, difficulty 10, `AbilityUsageSoak` with `GRIDGAME_ABILITY_STAGE`, measured on top of the Herald, Manasmith and Tactician heroes, GG-T333/336/337): the Cathedral casual **17/24, 19/24, 16/24**, smart **24/24** on all three. The first cut (three-foe Concord waves, Kiln Guard pairs) measured casual 4, 0 and 3/24 (smart 24/24/23); every wave went to two foes with a Chromed Hollow as the lighter half, and Cathedral I's templar lost its Confessor. Halcyon re-measured, casual before → after: 9/17/15 → 9/17/16, smart 24/24 throughout. Halcyon I and II are unchanged content on unchanged seeds; III with the Kiln Guard captain beside the Reaper first measured 12/24 (losses at that wave), so the Reaper came off it. Halcyon I at 9/24 casual was low before this change (the new heroes moved the soak's party rotation) and is left to a balance pass.
- **Old saves:** save **v7** moves any save already into the postgame up by three (`SaveMigrations.CathedralInsertedAt` 21, `CathedralStageCount` 3); a save whose furthest clear is Halcyon III finds the Cathedral next. The `story:ending` flag is left alone, so a save that already saw the ending at Halcyon does not see it again at the Cathedral (New Game+ replays it as before).
- **Armor sets:** the campaign grows to 34 stages; Wispweave stays at 22 and the top four spread over the new range (Templar's 24 → 25, Reaper's 26 → 28, Sanguine 28 → 31, Dreadwyrm 31 → 34) so no stretch goes more than five levels without a set (§24.2.3).
- **Tools:** the harness's finale run (`tools/harness.py`) launches `cathedral-03` and checks `boss_intro_cathedral`.
- *(Tests: `CathedralTests`; `StoryArcTests` and `RuleBreakTests` list eight finales and the ending follows the Hierarch's stage; `BackgroundsTests` expects the four-image `cathedral` set; `CatalogTests` accepts the mannequin placeholder; `QuestTests` allows the eighth main-theme quest.)*

#### 22.3.4 The postgame regions (added 2026-11-07, owner direction, GG-T346)

The owner's postgame landscapes give the postgame chapters regions of their own ([STORY.md](STORY.md) §5, structural entries only; story text is placeholder draft until Prose, GG-T355). Rules shared by every region:
- A re-skinned postgame theme names its set (`"Backgrounds": "<region>"`, native 768×1536, JPEG q95, fresh-GUID metas) and its stages carry the postgame theme id as their `Theme` (`greenvalley2`, not `greenvalley`), because `BattleScreen` and the results screens read the backdrop from the stage's theme. The board tile falls back to the base theme's (`Art.BoardTile`: `Tiles/<theme>`, then `Tiles/<BaseTheme>`, then the plain tile), so `greenvalley2` keeps the moors' tile. The endless encounter scheduler (`EncounterSchedule`, GG-T60) counts the re-skinned stages as a biome of their own.
- Postgame stages keep `LevelOverride` (they are what marks the postgame for the soaks, the quest rule and `QuestTests`) and carry no rule-breaker (§14.9).
- Balance is **unmeasured**: waves are built by analogy with existing stages at similar levels, pending the owner's `/test`.
- **Order and levels** (GG-T357, see *The postgame curve* below): the Bloom, the Free Holds, the Underspire, the Drowned Arks, Deep Shardfall, Under the Anchors, Morrow's Shadow, one level a stage from 25 to 40, continuing the main campaign's `i + 1` curve. Level numbers in the region entries below are as they were added.

**The Free Holds** (GG-T346): seven owner landscapes of walled hill-forts over gold moorland under a pale moon (`freeholds_01..07`, mean luminance 0.39) became the backgrounds of **The Moors Beyond the Stills** (`greenvalley2`). Its two stages are re-skinned, not rebuilt, and renamed **The Free Holds I–II**: hold militia (`soldier00–03`) at the gates where slimes and goblin thugs stood, a Hollow hold (`undead00–02`) where the skelepedes stood, militia in place of a skelepede and a toad in II; Redline, the moor wolves and the Captain's finale wave stay. Same foe counts per wave; level 15 unchanged. The crawl gains one placeholder line about the holds' walls; `greenvalley2_party_talk` already fit and is unchanged.
- *(Tests: `FreeHoldsTests`; `BackgroundsTests` checks postgame themes with their own region set.)*

**The Street markets** (GG-T349): nine owner landscapes of the underspire's neon night markets (the white spire behind tangled cables, pink signs, market stalls in the rain; `underspire_01..09`, mean luminance 0.24, a hair under POL-H3's soft 0.25 floor, kept as made) became the backgrounds of **The Underspire** (`cityruins2`). Its two stages are renamed **The Underspire I–II** and re-cast as the Street's home turf, where everyone wants the spire after the Hierarch: Redline (`ganger`), the Murder (`harpy01–03`), the Herd (`minotaur01–04`), the Chromed Hollow (`cyberzombie`) and the Choir (`cultist01–03`) only. The undead, ghosts, Red Seal templars, bats, the mannequin, and the old finale's Vampire (a playable hero), Reaper and Demon Lord came out. I: three Redline; two Chromed Hollow (kept); two harpies and a Redline tout; two Herd; a Cantor with a Hollow and a ganger. II: three Chromed Hollow (kept); three Herd; two harpies and a ganger; a Psalmist, a Confessor and a Hollow; then the **Herd Boss** (`minotaur01`) beside a Choir Cantor and a Redline ganger, three claimants to the spire in one wave. Level 15 unchanged; crawl and `cityruins2_party_talk` already fit and are unchanged.
- *(Tests: `UnderspireTests`; `BackgroundsTests` expects the nine-image `underspire` set.)*

**The Bloom** (GG-T347): eight owner landscapes of red blossom and moss over broken white Concord pillars under a pale moon (`bloom_01..08`, mean luminance 0.42) became a **new postgame chapter**, **The Bloom** (`bloom`), placed right after the ending as the first postgame chapter: where the Hierarch's stolen mana came down, Redline harvest the Bloom, Concord holdouts burn it and the moon-changed guard it.
- **Campaign:** `campaign.json` order is now the eight main themes, **The Bloom**, then the five older postgame themes (37 stages in all). Milestone `chest_gilded` like every postgame chapter; `"Backgrounds": "bloom"`; board tile `Tiles/bloom.png` (`GridGame.Studio/tools/make_tiles.py bloom`: white stone, moss in the cracks, fallen petals). A theme id with no trailing digit is its own base theme, so it counts with the main themes in `BackgroundsTests`, but its `LevelOverride`s keep it out of the main campaign for the soaks and the quest rule.
- **Stages** *The Bloom I–III*, five waves each, two foes a wave (the Cathedral's shape) with broken pillars as blocks: Redline pairs (`ganger`), moss-golem pairs (`treegolem`), a Red Seal templar with a Bulwark trooper, and in II a werewolf with a wolf. The finale wave is the **Red Seal's last warden** (`templar05`: the sturdiest templar, Tough Hide, Vengeance, Smite charge) with a Bulwark Shieldbearer and Siphoner. No rule-breaker (postgame). Levels **25, 26, 27** (`LevelOverride`), past the Cathedral's 22–24; the older postgame chapters stay at 15.
- **Story text is placeholder draft** (Prose, GG-T355): the crawl and `bloom_party_talk` (`ThemeStart`, the Lantern Merchant). No quest (postgame chapters have none).
- **Old saves:** save **v8** moves any save already into the old postgame up by three (`SaveMigrations.BloomInsertedAt` 24, `BloomStageCount` 3); a save whose furthest clear is the Cathedral III finds the Bloom next.
- **Armor sets:** the campaign grows to 37 stages; Templar's stays at 25, Reaper's 28 → 29, Sanguine 31 → 33, Dreadwyrm 34 → 37 (a set at least every five levels, the top set at the campaign's length; §24.2.3).
- **Balance is unmeasured** (no soak): built by analogy with the Cathedral's two-foe waves at 22–24, pending the owner's `/test`.
- *(Tests: `BloomTests`; `CathedralTests` (the postgame now starts with the Bloom); `BackgroundsTests` expects the eight-image `bloom` set.)*

**Morrow's Shadow** (GG-T348): ten owner landscapes of a colossal moon over cracked plains with floating rocks (`morrowsshadow_01..10`, mean luminance 0.47) became the **final postgame chapter**, **Morrow's Shadow** (`morrowsshadow`): where Morrow hangs lowest and its pull lifts stone, the Nearing made visible.
- **Campaign:** appended after `cityruins2`, the last theme (40 stages in all), so no save index moves and no migration is needed. Milestone `chest_gilded`; `"Backgrounds": "morrowsshadow"`; board tile `Tiles/morrowsshadow.png` (`GridGame.Studio/tools/make_tiles.py morrowsshadow`: cracked pale earth under a cold moonlit cast).
- **Stages** *Morrow's Shadow I–III*, five waves each, two foes a wave, fallen moon-rock as blocks: Hellspawn pairs, Infernalist pairs, a Void Sentinel with a beast, and moon-changed Wild (wolves, a werewolf, a yeti, a vulture, a flying monkey). The finale wave is the **Demon Lord** (`demonlord`: lifesteal, Infernal Hide, Fireball charge) with a Void Sentinel and an Infernalist Bloodbinder, the Ember's master under the moon it wants home; a stand-in until the owner makes a unique boss. No rule-breaker. Levels **28, 29, 30** (`LevelOverride`).
- **Story text is placeholder draft** (Prose, GG-T355): the crawl and `morrowsshadow_party_talk` (`ThemeStart`, the Enchantress). No quest.
- **Armor sets:** the campaign grows to 40 stages; Templar's stays 25, Reaper's 29 → 30, Sanguine 33 → 35, Dreadwyrm 37 → 40 (gaps of 3, 5, 5, 5 above Wispweave's 22; §24.2.3).
- **Balance is unmeasured** (no soak), pending the owner's `/test`.
- *(Tests: `MorrowsShadowTests`; `BackgroundsTests` expects the ten-image `morrowsshadow` set.)*

**The postgame curve** (GG-T357): the five older postgame chapters still sat at level 15, below the Cathedral's 22–24, and in the order the main campaign visits their base themes rather than a story order. Now:

| # | Chapter | Theme | Stages | Levels |
|---|---|---|---|---|
| 1 | The Bloom | `bloom` | `bloom-01..03` | 25–27 |
| 2 | The Free Holds | `greenvalley2` | `greenvalley-04/05` | 28–29 |
| 3 | The Underspire | `cityruins2` | `cityruins-04/05` | 30–31 |
| 4 | The Drowned Arks | `swamp2` | `swamp-04/05` | 32–33 |
| 5 | Deep Shardfall | `desert2` | `desert-04/05` | 34–35 |
| 6 | Under the Anchors | `cave2` | `cave-04/05` | 36–37 |
| 7 | Morrow's Shadow | `morrowsshadow` | `morrowsshadow-01..03` | 38–40 |

- **Story order:** Redline's Bloom trade leads out to the holds that sell it; back to Halcyon, where the spire's fate is being bargained; then the wider questions about Morrow: leave on the Arks, stand against the Ember's march on the Anchors, go under the Anchors to what they push against, and finally Morrow's Shadow, where the Ember goes to welcome the moon home.
- **Levels:** one a stage (`LevelOverride` = campaign index + 1), so the postgame continues the main campaign's `RecommendedLevel(i) = i + 1` with no gap or dip, and Morrow's Shadow III fights at 40.
- **Waves re-levelled by analogy:** enemies spawn at the stage's level (a spawn's own `Level` is a floor, 1 throughout), so the `LevelOverride` is the re-level. The five chapters' three- and four-foe waves were built for 15; at 22–24 three-foe waves measured casual 4, 0 and 3/24 in the Cathedral's first cut against 17–19/24 for two-foe waves (§22.3.3), so at 28–37 they would be brutal. Every one of those ten stages now has the endgame shape of the Cathedral, the Bloom and Morrow's Shadow: two foes a wave, three in a stage's last wave where it had a boss or a big finale (the Captain, the Herd Boss, the Swamp Mistress, the Captain with a sandmaw, the yeti). Each wave keeps two of its old foes; catalog groups of three or four became the two-foe explicit waves of the same family, and the three two-foe groups (`lurker-diagonal-2`, `treegolem-pair-2`, `ceramicknight-column-2`) stay. Every wave is still unique across the campaign (checked statically against the wave-signature rule); five picks were shifted by one id to keep it so (Free Holds I's wolves and Hollow, Drowned Arks IV's last wave and V's golems, Deep Shardfall V's scorpion wave, Under the Anchors V's oni wave, which took the goblin thug Under the Anchors IV gave up).
- **Old saves:** save **v9** (`SaveMigrations.PostgameReorderedFrom` 27, `PostgameReorderMap`): six of the ten stages moved, none were added. `HighestClearedStage` becomes the furthest new index among every stage the save had cleared (`ReorderedPostgameFrontier`), so its furthest clear stays cleared, nothing it cleared locks again, and a chapter that moved behind the frontier opens there (e.g. a save that had cleared through Deep Shardfall V now has the Underspire and the Drowned Arks open behind it and Under the Anchors next). Saves before the Free Holds or in Morrow's Shadow do not move.
- **Armor sets:** no change needed. The campaign is still 40 stages and its last level is now 40 too, so Templar's 25, Reaper's 30, Sanguine 35 and Dreadwyrm 40 already give a set at least every five levels with the top set at the last level (§24.2.3).
- **Balance is unmeasured** (no soak), pending the owner's `/test`.
- *(Tests: `PostgameCurveTests` (order, the curve, the wave shape, v9); `FreeHoldsTests`, `UnderspireTests` and `MorrowsShadowTests` follow the new levels and order; `ArmorSetTests` checks the top set against the last level.)*

### 22.4 Bounty board

**Hidden at release (GG-T392):** `Features.Bounties`; see §8.1.

**Amended 2026-11-19 (GG-T431, owner):** *"bounty board is definetly something you want from the hub so you can hunt down especially dangerous monsters, it will have clues like 'Seen wandering the Ashlands II' and it needs some sort of bounty indicator (a skull?) so that you know its a tougher variant and that you can collect rewards at the bounty board after you kill it."* The kill-N contracts are retired (they duplicated the side quests' Defeat objectives); the board now posts **marked targets**.

- **A bounty** (`bounties.json`, `BountyDef`) is a titled, tougher variant of an existing enemy class ("Grimjaw the Mauler", a `wolf02`) that roams one campaign stage, in one wave, optionally with an extra trait: a rule-breaking boss's rule (§14.9). No new art. Validation: the target is a non-snake enemy class, the stage a real campaign stage, the wave one of its waves.
- **The poster** (Camp → **Bounties**, `BountyScreen`): the target's portrait under the bounty skull, its title, "*class* · tougher variant", the **clue** "Seen wandering *stage*" (the stage's display name, `Bounties.Clue`), and the reward. **Take** starts the hunt; a hunted poster reads **Hunting** (tap to stop; it may post again).
- **The board** (`Bounties.Board`) holds **3** bounties (`Bounties.BoardSize`): slain ones waiting to be collected first, then hunted ones, then fresh offers. Offers are unclaimed bounties on stages already unlocked, in an order seeded by the save and a turnover counter that ticks every **3** stage battles (`Bounties.RefreshBattles`). Claimed bounties never post again.
- **The hunt.** `BattleFactory.Build` gives a stage battle one `BountyMark` per hunted bounty roaming it. The target joins its wave (the next wave with room if the board is full) at the stage's level **+2** (`BattleConfig.BountyLevelBonus`), with **×2.5** HP (`BountyHpScale`) and **×1.3** every stat but Speed (`BountyStatScale`; Speed is left alone so the timeline reads the same), and its trait. It wears a pulsing **skull roundel** (☠, `BountySkull`) on its unit frame; its name is its title; the inspect card says "Bounty target · tougher than its kind … collect at the bounty board". Arrival plays a banner "☠ Bounty: *title*", the war cry and a "BOUNTY" float; its fall (after any Undying rise) plays the pickup cue, a banner and a "BOUNTY SLAIN" float (`BountyTargetEvent`).
- **The kill counts however the battle ends** (victory, defeat, escape or retreat): `Bounties.RecordBattle` (in `BattleFactory.Commit`) moves it from hunted to slain and the results screen shows "*title* slain! Collect at the board". The Crucible never carries a mark.
- **The reward** is collected at the board on the next visit: **Claim** pays **150 G + 50 G × the stage's campaign index** (`Bounties.RewardGoldBase`, `RewardGoldPerStage`), the bounty's item, and any skill points to every party hero (`RewardSkillPoints`, GG-T134), with the purchase cue and the pickup cue. A Tome reward offers its Teach row (§25.6.4). The Camp row's value reads "☠ N to collect" or "Hunting N"; the Adventure screen lists each hunt's clue above the regions and marks a region and stage a target roams with ☠.
- **Bots skip it.** `CampRoutine` never takes, hunts or claims a bounty.
- **Old saves.** A save holding a pre-GG-T431 active contract (`BountySave.ActiveId`/`Progress`) has it dropped on load and the board stays open for it (`BountySave.Used`). No save-version bump. *(Tests: `BountyHuntTests`; `MetaLoopTests`, `ParityTests.Abandoned_bounties_can_be_retaken_and_claimed_ones_never_repost` updated.)*

| Bounty | Target | Stage (wave) | Trait | Item |
|---|---|---|---|---|
| Grimjaw the Mauler | wolf02 | The Tithe Moors II (5) | | Tome of Berserk |
| Mother Gloam | slime02 | The Tithe Moors III (2) | | Healing Potion |
| Kurogane the Lantern-Breaker | oni00 | The Lantern Road I (3) | | |
| Ashpelt the Moonless | werewolf00 | The Lantern Road II (4) | TwinTurn | |
| Thirstmouth | sandmaw | The Shardfall II (2) | | |
| Rotbark the Unfelled | treegolem02 | The Arkgrave II (3) | | |
| Hollowe the Vat-Giant | cyclops00 | The Anchor Wastes I (2) | | Tome of Tough Hide + 1 SP each |
| Saint Shroud, the Unburied | undead01 | The Still I (3) | Undying | Holy Water |
| The Pale Bellringer | ghost | Halcyon II (2) | | |
| Brother Vesper, Last of the Choir | templar01 | The Choir's Cathedral II (4) | AxisWard | Revival Charm + 2 SP each |
| Sergeant Iron Tithe | heavytrooper03 | The Bloom II (3) | | |
| The Lamp-Eater | duskstalker | Morrow's Shadow I (3) | | |

~~**Amended 2026-10-09:** a Camp screen (`BountyScreen`, "Bounties"), not a StageSelect strip. Posted = every `bounties.json` contract not yet claimed (claimed ones never repost). One active slot; kills of the target class count on victory only (in the Crucible, on any ending), capped at the goal; Claim pays gold + item, plus an optional skill-point payout; Abandon resets. The camp routine prefers a skill-point contract when two finish equally soon.~~ *(Retired by GG-T431; the five kill contracts Slime Culling, Wolf Pack, Shrouds on the Road, Vat-Giants and The Hierarch are gone, their items moved to the hunts above.)*

### 22.11 Side quests (added 2026-11-05, owner direction, GG-T237)

Owner: "optional quests and bounty board … make it a complete experience." Optional story errands, separate from the bounty board: any number may run at once, none ever gates the campaign, and they live in Camp's **Quests** journal (`QuestScreen`, start id `quests`).

- **Content:** `quests.json` (`QuestDef`: `Id`, `Name`, `Giver`, `Theme`, `Unlock` stage, `Summary`, `TurnIn` line, `Objective`, rewards `RewardGold` / `RewardItem` × `RewardItemCount` / `RewardSkillPoints` (each active-party hero) / `RewardFlag` / `RewardHero` (GG-T434: a hero who joins on turn-in, like a dialogue `RecruitHero`; nothing if owned), and `Requires`). Objectives reuse what the save already tracks: **ClearStage** (optionally `MaxHeroes` — at most N of your own heroes in the fight — and `NoneFallen`), **Defeat** N of an enemy class (counted after accepting), **Bring** N of an item (read from the bag, handed over on turn-in: the fetch quests), **Flag** (a save flag), and since GG-T434 **DefeatFamily** N of any class of an enemy family (the drop table its classes share, the family `EncounterCatalog.Family` reads: `slime`, `harpy`, `bulwark`, `soldier`…), **Recruit** a named hero (owned, not domesticated), **ReachLevel** `Count` (any hero, or the one named in `Hero`) and **LearnSkill** an ability id (any hero, or `Hero`). `ContentLoader.Validate` (`QuestCatalog.Validate`) checks every reference (stages, themes, items, enemy classes and families, hero classes, abilities, levels 2–99, `Hero` only on ReachLevel/LearnSkill), that `Requires` names a real quest and never loops, and that **some dialogue offers each quest** (a `StartQuest` effect).
- **Quest chains (GG-T434):** owner, 2026-11-19: "make more side quests, and quest chains about fetching materials or killing monsters or recruiting certain people or reaching a certain level or unlocking a specific skill". A quest's `Requires` names the quest that must be done first; until then it is `Locked`, so its offer (`QuestAvailable`) doesn't come up. `QuestCatalog.Chain` walks back to the first step and forward through the quests that require each one; the journal card shows the chain under the giver line (`Quests.ChainLine`: "Part 2 of 3: Iron for the Forge (done) › Plate from the Wrecks › ?", a step not offered yet shown as "?").
- **Given in conversation:** each quest has a Camp Talk dialogue (`quest_offer_<id>`, with an NPC or `"party"`) gated on `QuestAvailable` — its `Unlock` stage is cleared and it is neither under way nor done — whose accept choice carries `StartQuest`. "Not now" loses nothing: once the offer has been heard the journal lists it under **Heard of** with an **Accept** button. Quests not heard yet are listed under **Ask around** with who to talk to. A `RewardFlag` opens a follow-up Talk (`quest_after_grown_not_forged`, the Blacksmith on what the grown cogs mean).
- **Progress** (`Meta/Quests.cs`): `Quests.RecordBattle` runs in `BattleFactory.Commit` next to the bounty, **on stage victories only** (like the bounty board; the Crucible doesn't count). The results screen shows a "Quest" row per quest it moved. Bring, Flag, Recruit, ReachLevel and LearnSkill objectives read the save live (a hero already recruited, levelled or taught counts as soon as the quest is taken).
- **Journal:** sections **Ready to hand in!** (Hand in), **Under way** (Set aside — progress lost, can be taken up again), **Heard of** (Accept), **Ask around**, **Done**. The Camp row reads "! N to hand in" when something is ready, else "N under way"; it appears once any quest is unlocked. Accepting plays `quest_accept`; handing in plays the `quest_complete` fanfare and slides in a card with the giver's thanks and the reward (`.quest-turnin`, eased in, no single-frame swap).
- **Bots:** Test Mode, the balance sim and `CampRoutine` never take quests (no Talk is played headlessly), so they never affect balance.
- **Save:** `SaveData.Quests` (`QuestSave`: `Active` id → progress, `Done`); older saves migrate to an empty journal.

| Quest | Giver | Biome · offered after | Objective | Reward |
|---|---|---|---|---|
| Wolves at the Wells | Innkeeper | Tithe Moors · stage 1 | Defeat 5 Wolf | 250 G + Hi-Potion |
| What the Frogs Know | Merchant | Tithe Moors · stage 1 | Bring 3 Frog Gland | 300 G + 2 Mana Potion |
| Travel Light | Party | Tithe Moors · stage 2 | Clear Tithe Moors III with ≤ 3 heroes | 200 G + Tome of Steadfast + 1 SP each |
| Grown, Not Forged | Blacksmith | Shardfall · stage 1 | Bring 4 Gremlin Cog | 350 G + Tome of Mana Siphon + follow-up talk |
| Antivenom | Alchemist | Shardfall · stage 2 | Defeat 4 Scorpion | 300 G + 2 Hi-Potion |
| The Hag's Hand | Alchemist (after Antivenom) | Arkgrave · stage 1 | Bring 3 Hag Claw | 400 G + Revival Charm |
| Every Voice | Enchantress | Arkgrave · stage 2 | Clear Arkgrave III, nobody falls | Hi-Ether + 1 SP each |
| Furs for the Long Cold | Innkeeper | Anchor Wastes · stage 1 | Bring 3 Yeti Fur | 500 G + 2 Tent |
| Cold Iron | Blacksmith | Anchor Wastes · stage 2 | Defeat 3 Ice Mauler | 600 G + Tome of Counter Attack + 1 SP each |
| The Ones Who Paid | Party | Halcyon · stage 1 | Defeat 5 Ghost | 700 G + 3 Holy Water + 1 SP each |

**More quests and chains (GG-T434, 2026-11-19).** Twenty more, 33 in all, unlocking steadily from the first stage to the postgame, rewards scaled to measured stage pay (about 210–270 G a stage early, 400–600 G late). The names and objectives are deliberately descriptive: the journal and offer text is lorem ipsum (GG-T387 pattern, `GridGame.Studio/tools/prose_lorem.py`; drafted to Prose as batch `quests-434`), and the story writer reads the story off the shape. Five chains: **Beasts of the Moors** (3), **The Masterless Blade** (3), **The Smith's Forge** (3), **The Alchemist's Cures** (Antivenom › The Hag's Hand › 2 new, 4), **Echoes of Halcyon** (4).

| Quest | Giver | Biome · offered after | Objective | Reward |
|---|---|---|---|---|
| Slime in the Cellar | Innkeeper | Tithe Moors · stage 1 | Defeat 6 of the slime family | 220 G + 3 Healing Potion |
| Harpies over the Wells | Innkeeper (after Slime) | Tithe Moors · stage 2 | Defeat 4 of the harpy family | 260 G + 2 Antidote |
| The Beast of the Moors | Innkeeper (after Harpies) | Tithe Moors · stage 2 | Defeat 1 Werewolf | 320 G + 1 SP each |
| A Blade Without a Lord | Party | Tithe Moors · stage 2 | Recruit the Ronin | 250 G + Hi-Potion |
| The Ronin's Measure | Party (after Blade) | Lantern Road · stage 1 | The Ronin reaches Lv 6 | 300 G + 2 Hi-Potion |
| The Guard Stance | Party (after Measure) | Lantern Road · stage 2 | The Ronin learns Guard Stance | 350 G + 1 SP each |
| Seasoned | Party | Lantern Road · stage 3 | Any hero reaches Lv 10 | 400 G + 1 SP each |
| Iron for the Forge | Blacksmith | Shardfall · stage 1 | Bring 6 Iron Ore | 380 G |
| Plate from the Wrecks | Blacksmith (after Iron) | Shardfall · stage 2 | Bring 3 Mecha Plating | 420 G + 2 Greater Healing Potion |
| Quenched in Frost | Blacksmith (after Plate) | Anchor Wastes · stage 1 | Bring 2 Frost Core | 650 G + Tome of Regenerate + the Lancer joins |
| A Contract Worth Signing | Merchant | Shardfall · stage 2 | Recruit the Sellsword | 400 G + 2 Storm Flask |
| Ichor of the Lurkers | Alchemist (after The Hag's Hand) | Arkgrave · stage 2 | Bring 4 Swamp Ichor | 500 G + Revival Charm |
| A Star to Call Down | Enchantress | Arkgrave · stage 3 | Any hero learns Meteor | 550 G + Hi-Ether |
| Holy Essence for the Still | Alchemist (after Ichor) | The Still · stage 1 | Bring 3 Holy Essence | 700 G + 2 X-Potion + 1 SP each |
| The Bulwark Breaks | Party | Halcyon · stage 1 | Defeat 6 of the Bulwark family | 750 G |
| The Reaper's Shroud | Party (after Bulwark) | Halcyon · stage 2 | Bring 2 Reaper Shroud | 800 G + 2 Hi-Ether |
| Strong Enough for the Choir | Party (after Shroud) | Cathedral · stage 1 | Any hero reaches Lv 24 | 850 G + Tome of Lifesteal + 1 SP each |
| The Last Rite | Party (after Choir) | Cathedral · stage 2 | Any hero learns Mass Heal | 900 G + 2 X-Potion + 1 SP each |
| Heartwood of the Bloom | Innkeeper | The Bloom · stage 1 | Bring 3 Heartwood | 900 G + 2 Tent + 1 SP each |
| Holds Without Masters | Party | The Free Holds · stage 4 | Defeat 10 of the soldier family | 1000 G + Tome of Berserk |

*(Tests: `QuestTests` — content valid and spread over the five biomes, an orphan quest rejected, offer gated on the unlock stage and started by the conversation, "Not now" kept in the journal, Defeat counts victories only and pays once, ClearStage's party-size and nobody-falls terms, Bring takes only the asked count and a reward flag opens the follow-up, old saves migrate; GG-T434: every new objective kind and three chains of three or more in content, bad family/hero/level/skill/reward-hero references and a looping or dangling `Requires` rejected, a chain step locked until the step before is done and the chain line in the journal, family kills summed across the family, Recruit/ReachLevel/LearnSkill read live (a named hero's level only), a recruit reward joins once. Written, not run.)*

---


### 22.6 Battle records (added 2026-10-11, owner: "record everything so you can learn how to balance and synergize, keep the records of all moves as JSON so that we can build ML around it in the future")

`BattleRecorder` (Core) writes one JSON document per battle, schema v3 (v2 added per-turn skill level / perfect command; v3, GG-T98, added formation-time footprints on PincerFormed and `SpawnedAt` on wave/reinforcement events): `source` (player / bot:greedy / bot:casual / bot:autoplay / player+autoplay), stage, seed, waves, the party (class, role, level, stats, gear, bar), the opening state and events, then every turn — the full state **before** (units with position, HP, buffs, cooldowns, bar, brain, timeline position, casts; pickups, traps, blocks, amplifier, bank, mana charge), the **command**, whether it was accepted, **every event** it produced (type + fields), and a summary after — and finally the outcome (phase, wave reached, actions, coins, XP, kills, items used, heroes standing).
- The game saves every battle (victory, defeat, retreat) to `persistentDataPath/battle-records/` (or `<artifacts>/records` under the harness, or `-records <dir>`).
- `RecordTests.Record_soak_of_the_first_battle` with `GRIDGAME_RECORDS=<dir>` writes bot-played battles; `tools/records.py` prints per-wave tables (turns, damage taken, deaths, potions, pincers, chains, mana, pickups, taunts), each role's damage/healing, damage taken by enemy brain, and the most-used abilities. The original 20-wave first battle (since compressed to 5, [§22.7](#227-the-first-battle)) and several rules (skirmisher dart, last stand, taunt valuation, hard-won experience) were tuned from these tables.
- **Per-ability usefulness (GG-T246):** `python tools/records.py --abilities <dir>` credits each hero ability with what its resolutions did. Effects are attributed from an `AbilityResolved` (or the `ItemUsed` before it) up to the next action, turn, pincer, attack or wave, so a cast-time spell is credited when it lands. It reports uses, damage, healing, kills, status and tempo/movement effects and an impact score per use, the abilities that sat on a bar but never fired, the weak ones, and the mana flow (orbs banked, overflowed and spent). `AbilityUsageSoak` (env `GRIDGAME_ABILITY_RECORDS=<dir>`, `GRIDGAME_ABILITY_ROUNDS`) feeds it every hero class in rotating parties of five across the first 15 campaign stages at their recommended levels. Findings: `docs/research/AbilityUsage.md`.

### 22.7 The first battle (amended 2026-10-12, owner: superseding "make the first battle 20 rounds" — every stage, including this one, normalized to 5 waves)

Tithe Moors I is **5 waves**, levels 1 → 5, each wave outweighing the one before it (test `The_first_battle_has_five_waves_of_rising_difficulty`): an ooze opener (slimes), a beast wave (wolves, bat), a frog/toad wave, a goblin-and-bandit skirmish that a captain's soldier squad **reinforces mid-fight** — the flagship example of the delayed-spawn/surprise mechanic ([§14.8](#148-reinforcements-and-eggs-added-2026-10-12-owner-direction)) — and a captain's-squad finale.

**Amended 2026-10-19 (GG-T118, rebalanced for the three-hero start):** the reinforcement is one soldier, not two (wave 4 is five foes, still heavier than the frog wave), and the finale's captain and two soldiers are level **4**, not 5, so levels now run 1 → 4. Measured on Tithe Moors I alone, 24 seeds, single attempt: the casual bot (randomness 0.5) won 41% with 22% HP left with the old five-hero start; the trio on the old waves won 16% / 11%; probes of the trio with a Lv 4 finale gave 45% / 31%, and with a four-foe wave 4 as well 54% / 37%; this cut sits between them, back at the old five-hero difficulty or a little kinder. The greedy bot wins 100% either way. In `BalanceReport` (up to 4 attempts) the casual bot now clears it 80% of attempts with 61% HP left (five-hero start before: 66% / 48%; trio on the old waves: 30% / 26%).

**Amended 2026-11-05 (GG-T261/GG-T262):** after the remains countdown (§14.5), `EnemyHpScale` 0.45 and strikes at 1.2 (§14.6), Tithe Moors I is unchanged in content and the casual bot (`polish.py --balance`, 8 rotating five-hero parties) clears it 75 % of the time with 59 % HP left, waves a median ~20 player actions (was 38).

### 22.5 Expeditions (added 2026-10-10, owner direction)

Owner: "the game should be grouped in sets of battles, where a battle has X battles to fight and each battle has Y waves, so there is a survival element and a reason to hold onto consumables and use mana sparingly because the challenge is going to get bigger."

- Each campaign **theme is an expedition**: its stages are the battles (3 each), fought **in order**; each battle has its waves. `SaveData.Expedition` (`ExpeditionSave`: theme, next battle, carried orbs, coins) tracks the one under way.
- **Starting:** an expedition starts on its theme's first battle (`Expeditions.CanLaunch` / `Begin`); only one at a time; battles 2+ open only as the next step.
- **Between battles:** wounds carry (as always), **leftover mana orbs carry** into the next battle's bank (`BattleSetup.StartOrbs`, minted as "carried"), consumables spent are gone. The caravan's merchant, blacksmith and alchemist stay open — that is why the shops are always there — but the **inn is closed on the road** (`Inn.Rest` refuses; Tents still work).
- **Finishing:** the last battle won completes the expedition: **+50% of its coins as a bonus**, the caravan makes camp and the party rests (full HP), the next expedition unlocks.
- **Failing:** a defeat ends the expedition (the party falls back and recovers); it restarts from battle 1. **Abandon** (Adventure), a voluntary Retreat (in-fight, or the "Retreat early" button below) or escaping also ends it, wounds kept.
- **Amended 2026-10-11 — bailing out early costs nothing already earned:** every battle won in an expedition is paid out immediately by that battle's own `Commit` call — `s.Expedition.Coins`/`CarriedOrbs` are bookkeeping for the completion bonus only, not an escrow. So mid-expedition, `PostBattleScreen`'s button row offers **"Retreat early"** next to **Next battle ›** (shown whenever a next battle is pending): it's a plain `Expeditions.Abandon` with no partial-loss math, forfeiting only the *not-yet-earned* completion bonus. An in-fight Retreat (pause menu) is different — it DOES apply the FFIV-style partial-loss formula ([§22.2](#222-failure-path-detail)), but only to that one battle's own in-progress `Results`; gold from earlier battles in the same expedition is untouched either way.
- UI: Adventure groups battles under "<Theme> Expedition" headers with status ("Battle 2/3 · 4 orbs carried", "★ Completed"); results show "Expedition: battle 1 of 3 won — 4 orbs carry over" and **Next battle ›**; camp shows the expedition strip and "Inn — On the road".
- *(Tests: `An_expedition_runs_its_battles_in_order_carrying_mana_with_the_inn_closed`, `Finishing_an_expedition_pays_a_bonus_and_rests_the_party_while_defeat_restarts_it`, `Retreating_mid_expedition_ends_it_without_touching_already_banked_gold`.)*

### 22.8 Level-up fanfare (added 2026-10-11, owner: "pomp and circumstance... the same tempo and pause that makes video games enjoyable")

`LevelUpScreen` sits between `BattleScreen` and `PostBattleScreen` whenever `BattleReport.LevelUps` is
non-empty (`PostBattleScreen.RouteFrom` decides; skipped entirely otherwise). One hero at a time:
- The portrait slams into place (`Art.Portrait`, the same bombastic reveal `CharacterSelectScreen` uses).
- The XP bar ticks fully through every level crossed in this one battle (fill → "Lv N" pop → reset)
  before a true partial fill for the level actually landed on — a hero who jumped two levels visibly
  climbs through both, not one smooth lerp.
- Stat deltas (`Formulas.StatsAtLevel` before vs. after) reveal one at a time, staggered.
- Auto-advances after a hold, but a tap anywhere or "Skip ›" jumps straight ahead — the same dual-input
  idiom `StoryCrawlScreen` already uses, not a new one.

### 22.9 Difficulty settings (scaffolding, added 2026-10-11, owner direction)

**Amended 2026-11-06 (owner, GG-T309): one dial, 1–10, starting at 10.** Owner: "This is a difficulty question, and
the higher the difficulty the greater the rewards. On a 1-10 scale you would start on 10 and let players adjust the
game if they feel it is too easy or hard." The Easy/Normal/Hard/Custom preset and its six inert multipliers (below,
superseded) are gone.

- **Where it lives.** `SettingsSave.Difficulty.Level` (`DifficultySettings`), a **profile** setting in settings.json
  (`game.difficulty = {"level": N}`, [§15.4](#154-player-profiles)), so every save slot of the profile plays it. New profiles
  start at **10**. It applies from the **next battle** (`BattleFactory.Build(…, difficulty)` copies it into
  `BattleSetup.Difficulty` → `BattleState.Difficulty`); a battle in progress keeps the level it began at.
- **One table.** `Core/Battle/DifficultyTable.cs` (`DifficultyTable.Rows`), validated by `ContentLoader.Validate`
  (`DifficultyTable.Problems`): ten rows in order, positive multipliers, every enemy column non-decreasing, every
  reward column strictly rising, and level 10's enemy side exactly 1.0 / offset 0. **Level 10 is the designed game**:
  every balance number in this bible is tuned at 10, and lowering the dial only ever softens it.

  | Level | Name | Enemy HP | Enemy damage | Enemy Lv | Enemy speed (GG-T311) | MinGap / MaxSpread (GG-T318) | Rewards (gold, XP, drop chance) |
  |---|---|---|---|---|---|---|---|
  | 1 | Gentle | ×0.55 | ×0.60 | −2 | ×0.60 | 1.60 / 2.00 s | ×0.6 |
  | 2 | Gentle | ×0.60 | ×0.65 | −2 | ×0.65 | 1.55 / 1.90 s | ×0.7 |
  | 3 | Easy | ×0.65 | ×0.70 | −1 | ×0.70 | 1.50 / 1.80 s | ×0.8 |
  | 4 | Easy | ×0.70 | ×0.75 | −1 | ×0.75 | 1.45 / 1.70 s | ×0.9 |
  | 5 | Moderate | ×0.75 | ×0.80 | −1 | ×0.80 | 1.40 / 1.60 s | ×1.0 |
  | 6 | Moderate | ×0.80 | ×0.85 | 0 | ×0.85 | 1.30 / 1.45 s | ×1.1 |
  | 7 | Hard | ×0.85 | ×0.90 | 0 | ×0.90 | 1.25 / 1.35 s | ×1.2 |
  | 8 | Hard | ×0.90 | ×0.93 | 0 | ×0.93 | 1.15 / 1.20 s | ×1.3 |
  | 9 | Very hard | ×0.95 | ×0.97 | 0 | ×0.97 | 1.10 / 1.10 s | ×1.4 |
  | 10 | Intended | ×1.00 | ×1.00 | 0 | ×1.00 | 1.00 / 1.00 s | ×1.5 |

  **Amended 2026-11-06 (GG-T318): the spacing columns.** `MinGapSeconds` / `MaxSpreadSeconds` drive the timeline spacer
  ([§2.12](#212-recovery-and-spacing-gg-t318)), and every row adds a ±0.25 s `JitterSeconds`. Less spacing is more
  pressure, so the validator lets these columns only fall as the level rises. The neutral row (level 0, rules tests)
  has no spacing and no jitter.

- **Party size** (*GG-T321*): independent of the dial, enemy strike and MaxHP also grow ×(1 + 0.30 × heroes beyond the
  starting three), §2.12.
- **What it scales.** Enemy MaxHP and armor plates at spawn (on top of `BattleConfig.EnemyHpScale`); the damage an
  enemy's blow deals a hero (after the hero's own reductions start, min 1); every enemy's turn-icon pace (`EnemySpeed`, GG-T311: slower foes leave wider gaps for the party, §2.0); how far apart foes are spaced on the timeline (`MinGap`/`MaxSpread`, GG-T318, §2.12); every non-test enemy spawn level
  (`EnemyLevelOffset`, never below 1, which also lowers that foe's stats and XP); and per kill the coins, the XP and
  every drop-table entry's chance (capped at 100%), after New Game+'s coin bonus. Test stages (`StageDef.IsTest`) keep
  the enemy side unscaled. Level 0 (`DifficultyTable.Neutral`) is the raw rules with no scaling at all — what a
  hand-built `BattleSetup` (the rules tests) gets.
- **Shown to the player.** Settings → **Difficulty**: − / the level / + (132-px targets) and one line underneath from
  `DifficultyTable.Describe`, e.g. "Intended: the designed fight. Rewards ×1.5." or "Gentle: foes 55% HP, 60% damage,
  60% speed, -2 Lv. Rewards ×0.6." The results screen names the dial under Experience ("Difficulty 10 — rewards ×1.5";
  `BattleReport.Difficulty`).
- **Migration.** A settings.json from before the dial holds the old preset: **Easy → 5**; **Custom** with enemy HP and
  damage averaging below ×1 → the level whose enemy side is nearest that average; **Normal, Hard, Custom at ×1 or more,
  and anything unknown → 10** (the old multipliers were inert, so those players were already fighting the designed
  game). It is rewritten as `{"level": N}` on the next save and the scaffold's keys are dropped. A legacy save slot's
  Settings block migrates the same way.
- **Telemetry** tags every battle with `difficulty` and the multipliers it applied (`diffEnemyHp`, `diffEnemyDamage`,
  `diffEnemyLevel`, `diffRewardGold`, `diffRewardXp`, `diffItemDropRate`); battle records carry `difficulty`.
- **Balance runs** fight at a chosen level: `GRIDGAME_DIFFICULTY=1..10` (default 10) for `AbilityUsageSoak` and
  `BalanceReport`, and `python tools/polish.py --balance --difficulty N`.
- *(Tests: `DifficultyTests` — table valid and monotonic, 10 = unscaled enemies, the reward curve and its label, new
  settings at 10, preset migration, enemy HP/levels rising with the dial on a real stage, kill rewards scaling.)*

*Superseded 2026-11-06 (GG-T309) — the original scaffold:* Owner: a Difficulty selection with presets and a Custom
mode "so when you read the telemetry you can ML it and learn what multipliers affect what; for right now just keep
everything at 1.0." `SettingsSave.Difficulty` held a `Preset` (Easy/Normal/Hard/Custom, default Normal) and six
multipliers (`EnemyHp`, `EnemyDamage`, `EnemySpeed`, `RewardGold`, `RewardXp`, `ItemDropRate`) pinned at 1.0 for every
built-in preset, none of them consumed by the battle; telemetry was tagged with all of them. The stated future
direction — higher difficulty, better rewards — is what the dial above delivers.

### 22.10 The Crucible (added 2026-10-19, owner direction, GG-T95)

**Hidden at release (GG-T392):** `Features.Crucible`; see §8.1.

Owner: "a way to gain xp and loot for the saved campaign … a waiting for the bus kind of mode."
Owner, 2026-11-19 (GG-T430): "implement the Crucible for 'toilet play' its just a horde mode that can earn you money and goes on until you die; you choose one team and go as far as you can; it dynamically generates harder and harder waves."

A quick, endless survival battle fought from camp **inside a normal saved campaign** — a Camp menu row, not a separate game mode (`Meta/Crucible.cs`, `CrucibleScreen`, `CrucibleLauncher`).

- **Entry.** Camp → **Crucible** row (right under Adventure; its value reads "Endless waves" before the first run, then "Best: N waves") → `CrucibleScreen` (what it is, first-wave level, the families you'll face, the record) → **Enter the Crucible** (`crucible_enter` cue). **Closed mid-expedition**: the row is disabled and reads "Closed on an expedition" (`Crucible.CanLaunch`) — the run's full-HP reset would otherwise heal the party between an expedition's battles for free ([§22.5](#225-expeditions)). It also refuses with no party or no foes met.
- **Pick one team** (*added 2026-11-19, GG-T430*). The Crucible screen shows **Your team N/6** — the active party (portraits and levels, the party-size cap `Progression.MaxPartySize`) — and a **Change team** button that opens the Party page and returns here. That team fights every wave; nobody swaps in mid-run.
- **One battle, many waves.** A run is one generated `StageDef` (`crucible-<seed>`, registered in the in-memory `ContentDb` only, earlier runs' stages pruned) of `Crucible.MaxWaves` (`BattleConfig.CrucibleMaxWaves`, 99; was 50 before GG-T430) waves, fought by the ordinary `BattleEngine` wave loop — no new battle loop. Because it is one battle, **wounds, mana and consumables carry wave to wave** and there is no camp in between. Effectively endless: levels climb every wave, so the party falls (or leaves) long before wave 99; clearing all 99 would be a victory, committed the same way.
- **Escalation — the horde ramp** (*tunables moved to `BattleConfig.Crucible*` and elites added 2026-11-19, GG-T430*). Wave *i* (0-based) spawns at **`PartyLevel + i × CrucibleLevelStep`** (1; `PartyLevel` = the active party's rounded mean level), with `CrucibleFirstWaveEnemies + i / CrucibleWavesPerExtraEnemy` foes (2 + i/3) capped at `CrucibleMaxWaveEnemies` (5). From wave `CrucibleFirstEliteWave + 1` (wave 5) the last foes of the wave are **elites**, `CrucibleEliteLevelBonus` (3) levels above it: one, then one more every `CrucibleWavesPerElite` (5) waves, capped at `CrucibleMaxElites` (3) (`Crucible.WaveElites`). Level, count and elites only ever climb. The wave banner adds "· N elites".
- **Wave pay** (*added 2026-11-19, GG-T430*). Clearing wave *i* banks **`CrucibleWaveGoldBase + CrucibleWaveGoldPerWave × (i+1)`** gold (10 + 5n: 15 for wave 1, 60 for wave 10; `Crucible.WaveGold`), on top of the foes' drops. It rides on the generated `WaveDef.ClearCoins`, which the engine adds to `Results.Coins` when the wave falls (`WaveClearedEvent.Coins`; 0 for every authored stage). The banner reads "Wave N cleared · +G G banked", the coins burst into the HUD gold counter with the coin chime. Banked means kept: a wipe keeps it like everything else. The levels are authored per spawn, so `BattleFactory.Build` gives a Crucible stage `RecommendedLevel` 1 and no New Game+ step — neither the campaign curve nor NG+ lifts them further.
- **Encounter groups** (*added 2026-10-22, GG-T184*). Each wave is one of that family's **authored groups** from `encounters.json` (its mix on its formation tiles), the largest that fits the wave's foe count; no group repeats in a run until every same-size group of its family has had its turn (`EncounterCatalog.Pick`). A family with no group that size tops up with random foes of the family. The endless generator (`EncounterGenerator`, GG-T55) builds its waves the same way.
- **Families.** Each wave is **one enemy family** (`ClassDef.DropTable`, house convention), never the same family twice in a row, cycling through a fresh shuffle of every family the save knows: enemies it has **met** (Bestiary seen or defeated; legacy scanned) or that fight in **the campaign so far** (cleared stages plus the next one). Bosses (`BossPhases`/`Boss` tag) and multi-tile foes stay in their own stages. So the drops are the mats this save is already crafting with. The arena (background, music, blocks) is one of the campaign-so-far stages.
- **Seeded and deterministic.** `Crucible.SeedFor(save)` mixes the save's creation time with `CrucibleSave.Runs`: the same save replays the same gauntlet (and the same battle RNG) until it fights it, and each run after is new. `Crucible.Generate(content, save, seed)` is pure.
- **Leaving.** The pause menu's Retreat row becomes **Leave the Crucible (keep all)**, confirmed with the wave count; a Smoke Bomb escape counts the same.
- **Rewards — the exception to [§22.2](#222-failure-path-detail).** Unlike any other non-victory end, a Crucible wipe or retreat **keeps all the gold, drops and XP earned so far** (that is the point of the mode), then restores the party to full HP like a defeat. Commit rules in [§22.1](#221-per-transition-state-contract); AMENDMENTS 2026-10-19.
- **Record.** `SaveData.Crucible` (`CrucibleSave`: `Best`, `Runs`, `TotalWaves`, and since GG-T430 `BestGold`, the richest run's haul, never lowered). Waves survived = waves fully cleared (the index of the wave the party fell or left on). The Camp row shows the best; a better run sets it, a worse one never lowers it.
- **Presentation.** HUD reads "Crucible wave N" (no total); each wave's banner reads "Crucible wave N · Lv L" with a rising-pitch `crucible_wave` sting. Results — the run summary: **CRUCIBLE**, "Survived N waves", "New best!" (with the `crucible_best` fanfare) or the standing best, then **Gold banked**, **Best haul**, and the rest of the haul (XP each, items, bounty); **Continue** → camp, **Again ›** → the next run. Level-ups play first as after any battle.
- **No endless stalemates** (*added 2026-10-19, GG-T123*). A run must always end by itself, for the bot and for a player alike. Two stalls were found by the bot soak and fixed in the rules (they apply to every battle, not only the Crucible):
  - **A lone hero is never fled.** A pincer needs two heroes, so one hero left standing can hurt foes only with its bar. Against a lone hero (`EnemyPlanner.LoneHero`) no foe makes a wounded retreat and every evasive brain (Commander, Trapper, Kiter, Medic) makes its last stand and an Ambusher stops lying in wait (§14.6) — they close in, so the hero either wins with spells, items and skills or falls. (Before: a wounded Flying Monkey kited the last hero round the board forever; a Scorpion ambusher waited forever for a lone hero that never came within 2 tiles.)
  - **Howl only hurries a striker.** A pure-haste support charge (Howl: no buff, no heal) is cast only when another ally in its square is already adjacent to a hero (or in a ranged foe's reach); otherwise the wolf moves. (Before: a scattered pack howled every turn, each howl hastening only the howler into its next howl, and never closed in.)
  - **The bot keeps a lone hero productive** (`HeroBot`): it spends mana and items on damage freely (cost penalty cut), closes in on the foes to bring its bar to bear, and guards (Shield) rather than wandering when nothing is worth doing.
  A player who would rather not finish a long fight still has **Leave the Crucible (keep all)**. *(Tests: `Bot_soak_over_a_crucible_run` — now deterministic, its save's creation time pinned since `Crucible.SeedFor` mixes it in — and `Crucible_bot_soak_never_stalls_across_many_saves`, 48 gauntlets fought to a wipe under the action cap.)*
- ~~Bounty kills count on any Crucible ending.~~ The Crucible never carries a bounty target (GG-T431, [§22.4](#224-bounty-board)).
- The first-battle tutorial never runs in the Crucible.
- **Cost and pace.** Entering costs `RestedPool.LaunchCost` (1) Rested like any launch; Camp → Crucible → Enter is two taps, and the pause menu's Leave ends it any time with everything kept — built for bus-stop sessions.
- *(Tests: `CrucibleTests` — `Crucible_waves_escalate_one_family_at_a_time_from_the_party_level` (now also elites and wave pay), `Crucible_horde_ramps_foes_elites_and_wave_pay_and_never_eases`, `The_best_haul_is_recorded_and_never_lowered`, `Crucible_draws_only_families_the_save_has_met_or_reached`, `Crucible_is_deterministic_per_seed_and_per_save`, `Crucible_spawns_at_the_authored_level_even_on_new_game_plus`, `Crucible_cannot_start_mid_expedition_and_says_why`, `A_crucible_wipe_or_retreat_keeps_every_reward_and_restores_the_party`, `A_worse_run_never_lowers_the_best_record`, `A_crucible_run_never_touches_campaign_progress_unlocks_or_the_expedition`, `A_crucible_wipe_keeps_the_gear_wear_it_caused`, bot soak `Bot_soak_over_a_crucible_run`. Harness: `python tools/harness.py crucible [WAVES [RUN]]` — camp → Crucible → Enter → Auto → leave after WAVES (99 = fight to a wipe) → level-ups → results → camp, asserting the haul, XP, full HP, the record and the untouched campaign.)*

## 23. Character Classes

All classes share `classes.json`; `Team` separates heroes (62) from enemies (110). **Amended 2026-10-09:** the spawn side decides the team — a hero class placed in a stage wave fights as an enemy (the Hierarch is the `vampire` class, which is also recruitable).

### 23.1 Class identity

Base stats, per-level growth, milestone bonuses, tags, resistances, learnable `Abilities`, `DefaultLoadout`, lore/card text, portrait crop. No dialogue.

### 23.2 Hero classes with seeded kits

| Class | Identity | Stat lean | Color | Kit |
|---|---|---|---|---|
| **Cleric** | white-magic healer | INT/WIS | **W** | Heal, Heal, Frost, Potion |
| **Paladin** | tank with heals | STR/VIT | **W** | Heal, Fireball, Potion |
| **Barbarian** | brute front line | even 12s | **R** | Fireball, Bolt, Potion |
| **Alchemist** | consumables + control | INT/WIS | **G** | Frost, Potion, Steal, Sleep Dart |
| **Assassin** (id `assassain`) | flanker / rogue | AGI/SPD/LCK | **B** | Steal, Mug, Bolt, Potion |
| **GreenNinja** | mobility thief | AGI/SPD/LCK | **G** | Teleport, Steal, Fireball, Potion |
| **RedNinja** | mobility striker | AGI/SPD/LCK | **R** | Teleport, Mug, Bolt, Potion |

Every other hero class has color **U** (Blue, the default) and the default kit (Heal, Fireball, Frost, Bolt, Potion). A new save's roster is **Paladin, Barbarian, Cleric** (`Progression.StartingRoster`). *(Amended 2026-11-21, GG-T533: Warden, Barbarian, Cleric; Warden is the fixed lead, §25.11.2.2.)*

#### 23.2.0 Signature moves + design rationale

| Class | Signature | Should feel like | Anti-pattern |
|---|---|---|---|
| Cleric | sustain (Heal/Mass Heal/Antidote) | "I keep the party alive when it shouldn't be." | stuck front-line |
| Paladin | tank + self-patch | "I body-block and patch myself." | out-damaging the Barbarian |
| Barbarian | Fireball + Bolt | "I delete the front rank." | slow to set up |
| Alchemist | stacks + Steal | "I never run out." | out-damaging mages |
| Assassain | Mug | "Every action I hit and harvest." | too tanky |
| GreenNinja | Teleport into a flank | "I'm in position a move sooner." | consequence-free teleport |
| RedNinja | Teleport + Mug | "I'm in their backline first." | same as Assassain |

#### 23.2.1 Class identity rules of thumb

One thing it does best plus a secondary cope; lopsided stats; colors shape the bank (W/W/W vs W/B/R); abilities reference identity.

#### 23.2.2 Classes without distinct kits

**Amended 2026-10-13 (superseded, was already stale):** this table predates a kit pass that gave every one of the 62 hero classes a bespoke signature move (`<classid>_<move>` in slot 3, e.g. `paladin_bulwark`, `myrmidon_phalanx`, `whitewitch_ward`) — verified against `classes.json`, 0 classes run the flat default kit described above. A further pass the same day grouped each role into 3-5 sub-archetypes (e.g. Tank: Aegis Guard / Juggernaut / Warlord / Deathguard; Healer: Aegis Wardens / Hexmenders / Skirmish-Menders; see `tools/icons.json`'s `ability` map for the full id list) and gave each cluster two more bespoke moves (slots 1 and 4), so every class now has 3 of its 5 slots archetype-specific — only slot 0 (the role's baseline action, e.g. `taunt`/`heal`/`cleave`) and slot 2 (`item:x_potion`) stay shared, by design (`SynergyTests.Every_hero_has_a_role_and_kits_match_it` asserts the baseline survives). A parallel `Universal` flag on `AbilityDef` (12 new Skills) lets any hero of any role learn a class-agnostic move via `Abilities.Learnable`, alongside the pre-existing rule that every Spell is already universally learnable.

### 23.3 Enemy classes

110 enemy classes, 47 used in the campaign. Enemies use only `ChargeAbility`, `Trap`, `BossPhases`, `SnakeSegments`, `Footprint`, `MoveRange` and stats; their `Abilities` list is not used.

### 23.4 Roster + party composition

`SaveData.Roster` holds every recruited hero; `Party` the active squad, **max 6** (`Progression.MaxPartySize`; **amended 2026-10-10**, owner: "up to six party members" — the board has six hero start tiles; Camp shows them in a 2-column grid). `Party.Toggle` moves a hero between party and reserve, refusing a full party and refusing to remove the last member (**Amended 2026-10-09**). XP lives on the hero, so it survives moves.

---

## 24. Equipment, Items, Materials, Currency

### 24.1 Item types

`Equipment` (weapon, armor, relic), `Consumable`, `CraftingMaterial`, `QuestItem` (never sold). Gold is not an item. **Added 2026-11-06 (GG-T301):** `RecipeScroll` (§24.6.2), `Tome` (teaches one Passive/Reactive, `TeachesAbility`) and `Chest` (rolls a droptables.json row when opened, `OpensTable`); see [§25.6.4](#2564-learnable-passive-rewards-tomes-and-chests-gg-t301).

#### 24.1.1 Rarity tiers

`ItemRarity`: Junk, Common, Uncommon, Rare, Epic, Legendary (content: 3 / 38 / 45 / 37 / 24 / 20). Names render rarity-colored (`Ui.RarityClass`).

| Rarity | Cost guide | Stat range | Source |
|---|---|---|---|
| Common | 1.0× | +1–3 | most enemies |
| Uncommon | 2.5× | +3–6 | mid enemies, forge |
| Rare | 6× | +5–10 | elites, upgrades |
| Epic | 15× | +8–15 | bosses, enchant |
| Legendary | 40× | +12–25 | end-game |

Relic/Epic+ named passives (`PincerDamageBonus`, `RegenPerTurn`, `StartsProtected`) and weapon-upgrade-line named passives ([§24.6.1](#2461-tier-ladder-gg-t35-amended-2026-10-15), GG-T47) are both built.

### 24.2 Equipment slots

Weapon, Armor, Relic I–III. `Equipment.Fits` (weapon/armor by slot; any relic in any relic slot), `Equip` (takes one from the bag, returns the old piece), `Unequip`, `Compare` (stat preview).

#### 24.2.1 Proficiency gating (GG-T52)

Every weapon carries a `WeaponClass` (`Simple`/`Martial`/`Arcane`) and every Head/Torso/Legs/Gloves/Boots piece an `ArmorWeight` (`Light`/`Medium`/`Heavy`); every hero can always use Simple weapons and wear Light/Medium armor regardless of class. `ClassDef.WeaponProficiencies` (which non-Simple classes a class starts trained in) and `ClassDef.ArmorProficiency` (the heaviest armor it starts able to wear, default Medium) set each class's out-of-the-box reach: the Tank archetype (Guardian tree) starts at Heavy armor; Warrior/Guardian/Ranger start with Martial weapons; Mage starts with Arcane; Cleric and Alchemist start Simple-weapons-only (daggers, maces) until they earn more.

`Progression.EffectiveWeaponProficiencies`/`EffectiveArmorProficiency` layer the class default together with any proficiency-unlock passive already in `HeroSave.Known` — `martial_weapons_proficiency` ("Martial Weapons"), `arcane_focus_proficiency` ("Arcane Focus") and `heavy_armor_proficiency` ("Heavy Armor Proficiency"), the same three passives §25.6's Build screen already reads via `Progression.KnownProficiencies` (staged by GG-T74). They're real skill-tree nodes today: **Mage** can unlock Heavy Armor Proficiency (so a mage can earn the right to wear armor — the ticket's own example), **Cleric** can unlock Martial Weapons, and **Alchemist** can unlock Arcane Focus, each a late node (`RequiredPointsInTree: 8`) in its tree. Actually spending a point on *any* tree node (these three included) is still the deferred follow-up noted in §25.6/§25.6.1 — until that lands, a passive only enters `Known` via a class's starting kit (none do yet) or the migration grandfather clause below.

`Equipment.ProficiencyGate(c, h, item)` returns null if `h` can equip `item`, else the unlocking passive's name; `Equipment.Fits(c, h, item, slot)` (an overload of the shape-only `Fits(item, slot)`) folds that check in, and `Equipment.Equip` refuses with `"Not proficient — needs {gate}"` without consuming the item. The Equip screen's bag listing still calls the shape-only `Fits`/`Candidates` so a piece the hero owns but can't wear stays visible — shown disabled with a 🔒 and the gate's name in place of its count — instead of silently vanishing; `Equipment.Candidates(c, s, slot, h)` (the hero-aware overload) is what the camp bot and shop-browsing code use instead, so it never buys or crafts toward gear it can't equip. A save written before this landed may already have a hero wearing something ungated at the time — `SaveMigrations.Migrate` auto-grants the matching proficiency for anything already equipped, so nobody's gear is stripped out from under them. *(Tests: `LoadoutAndProgressionTests` — effective proficiencies by class default and by passive, `ProficiencyGate`/`Fits`/`Equip`/`Candidates` behavior, the migration grandfather clause.)*

#### 24.2.2 Weapon roster (GG-T181)

**100 unique base weapons** — the 13 originals plus 87 more, authored as one table in `GridGame.Studio/tools/weapons.py` (re-run it to re-tune, then `python ../GridGame.Studio/tools/make_icons.py --missing` for any new icon). 30 Simple (clubs, sickles, slings, quarterstaves, knives, javelins, rods, lanterns, chakrams… — the Healer/Support kit, since those classes start Simple-only), 40 Martial (halberds, rapiers, katanas, glaives, whips, crossbows, flails, scythes, tridents… — Striker/Tank/Ranged) and 30 Arcane (wands, tomes, orbs, scepters, totems, staves — Mage/Support/Healer), Common through Legendary. Each has its own stat profile, and many a base trait from fields the engine already applies to any equipped item: `PincerDamageBonus`, `RegenPerTurn`, `StartsProtected`, `BattleStartManaOrbs`, `BankCapacityBonus`, `Resistances`, or an **on-hit trait** — a low-chance `PincerApplyBuff` or a small `LifestealFraction` named on every step via `PassiveName` (whose `+3` passive is then the same mechanic, stronger). Every weapon has a source: Common basics are Merchant stock (Common only, GG-T84), most are Forge recipes (gold ≈ 40% of base cost, Apprentice for Common/Uncommon, Journeyman for Rare, Master for Epic/Legendary, inputs themed on the enemy family whose trophies they use), and some are enemy drops (`droptables.json`, 0.8–6% by rarity — the Demon Lord's are the Legendary ones). Every base gets the originals' generated variants: `+1/+2/+3` (Strength and Intelligence +2/+4/+7, cost +15/+30/+50, a named passive at `+3`) and one per enchant (enchant `Bonus` added, cost +120, rarity one step up). Each weapon type has its own game-icons.net silhouette and each base its own palette (`tools/icons.json` `weapon_palette`, shared by its variants). *(Tests: `WeaponRosterTests` — 100 bases across all three classes and every rarity, distinct profiles, a shop/forge/drop source for each, a full upgrade line and enchant set for each, an icon for every weapon row; `EconomyLoopTests`; `Every_weapon_upgrade_lines_top_step_has_a_named_passive`.)*

#### 24.2.3 Armor sets (GG-T182)

`armorsets.json` holds **20 named five-piece sets — 100 armor pieces** (one Head/Torso/Legs/Gloves/Boots each, all of the set's `ArmorWeight`): 7 Light, 6 Medium, 7 Heavy, pitched from campaign level 1 (Wayfarer's, Light) to 40 (Dreadwyrm, Heavy; since the postgame curve, GG-T357, 40 is both the campaign's length and its last level, Morrow's Shadow III, and the top set tracks both; Reaper's 30, Sanguine 35 and Dreadwyrm 40 spread over the postgame's Bloom and Morrow's Shadow, GG-T347/T348; the top four sets spread over the Choir's Cathedral's 22–24 and the longer campaign, GG-T350; every set from 16 up moved three levels when The Still joined the campaign, GG-T344, and every set from 4 up moved three more when The Lantern Road did, GG-T345, so the Lantern Road's 4–6 has none of its own), a new set at most every five levels, rarity Common → Legendary with the level. `ArmorSetDef{Id, Name, Weight, Level, Description, Pieces[5], Bonuses[]}`; each `SetBonusDef{Pieces, Stats, Resistances, PincerDamageBonus, RegenPerTurn, BattleStartManaOrbs, StartsProtected}` is active while at least `Pieces` distinct pieces of the set are worn. Every set has a **2-piece** bonus (its signature stat) and a **5-piece** bonus (two stats plus a perk: an element resistance, Pincer damage, Regen, battle-start orbs or starting Protected). `ContentDb.SetByItem`/`TryArmorSetOf` maps a piece to its set; `ContentLoader.Validate` rejects a set without exactly one piece per armor slot, a piece of the wrong weight or in two sets, or a bonus tier outside 2–5.

Resolution is pure Core (`Core/Rules/ArmorSets`: `Progress`, `ActiveBonuses`, `BonusStats`, `Describe`). `BattleRunner.SpawnHero` folds the active tiers into the hero's `GearStats`/`GearResist`/`PincerDamageBonus`/`RegenPerTurn` (and battle-start orbs/Protection), tracking them in `Unit.SetBonuses`; when a piece **breaks** mid-battle (§24.5) the tiers it was holding up are taken back out and the feed reads "<Set> set bonus lost". Because `Progression.Preview` runs the same spawn, the Status totals and the Equip compare grid include set bonuses automatically.

Sources — every piece has at least one: **Merchant** (only Common pieces: Wayfarer's head/gloves/boots, Militia head/torso/boots, Bramblehide and Gutterscrap boots), **Forge** recipes (base leather/iron/cloth plus the trophies of the enemies the set is themed on; Apprentice up to level 6, Journeyman to 14, Master above; gold ≈ 40% of cost), and **drops** (the remaining pieces, 4–5% from the themed enemy families' tables, 10% off the Demon Lord). The 23 older unsetted armor pieces stay as they were.

UI: the **Status** screen's "Armor Sets" window lists each set the hero wears a piece of ("Wayfarer's set 3/5") with every tier, ✔ and green once reached; the **Equip** screen's compare panel shows the set of the current and previewed piece as one line ("Wayfarer's set 4/5 → 5/5 · ✔ 2 pieces: … · ✔ 5 pieces: …"); item details (Items, Shop, Forge) name the set, its weight and level, and both tiers. *(Tests: `Tests/Core.Tests/ArmorSetTests.cs`.)*

### 24.3 ItemDef fields

```
Id, Name, Description, Type, Rarity, Slot, WeaponType, WeaponClass, ArmorWeight, BaseCost,
SellValue (−1 = BaseCost/2), MaxStack, Durability, BaseHealing, BaseDamage, DamageType, Stats{8},
BattleStartManaOrbs, OnUseAbility, GrantsOrbs, CuresBuff, CuresAll, Revives, Escape, OutOfBattleOnly,
BonusVsTag, BonusMultiplier, Resistances{type→mult}, SoldByVendor, Icon
```

### 24.4 Inventory

`SaveData.Inventory` — item id → count, per save slot; `AddItem` removes zero entries. **Amended 2026-10-10:** durability *is* per inventory instance — `SaveData.Worn` (item id → its worn copies, each a `WornGear{Durability, Repairs}`) remembers the wear and repair count of a piece that leaves a hero's slot, instead of resetting it; see §24.5.

### 24.5 Weapon durability

- A hero's equipped weapon loses **1 durability per pincer it attacks in** (not as a supporter); items with `Durability` 0 never wear. Content weapons have 80–250 durability.
- When a pincer starts with the weapon at 1, that attacker's strikes in that pincer deal **×1.5**; then it wears to 0 and **shatters**: the wielder takes `round(15% MaxHP)` (min 1, "Shatter!", environmental), the weapon's stats leave `GearStats`, and **(amended 2026-10-15, GG-T41)** the weapon becomes a Broken fragment in the save at commit instead of being removed outright — see Broken fragments below.
- **Repair** (`Meta.Repair`): restores to `EffectiveMax = max(1, Durability − repairs)`, then `repairs++` (so each repair lowers the next ceiling by 1); cost `ceil(missing × BaseCost / Durability × RepairRate × 1.6^repairs)`, min 1, with `Repair.RepairRate` 0.15 (0.30 until GG-T448); flagged uneconomical when cost ≥ BaseCost. Every repair counts toward the growth however few points it restores, so the camp routine waits until a piece is below 60% before repairing it (§25.0.11, GG-T540). *(Tests: `Weapon_wears_per_pincer_and_shatters_at_zero`, `Full_loop_battle_commit_then_every_vendor`.)*
- **Per-instance durability** (closed 2026-10-10, US-246): unequipping a piece that isn't factory-fresh (worn below max, or repaired) files it into `SaveData.Worn` instead of resetting it; `Equipment.Equip` hands out a fresh copy first, else the **least-worn** tracked copy. *(Test: `Unequipped_gear_remembers_its_wear_and_fresh_copies_are_used_first`.)*
- **Armor wears in battle** (closed 2026-10-10): a hero's equipped armor loses **1 durability per landing hit taken** (not DOT/environmental); at 0 it breaks mid-fight — stats and resistances leave `GearStats` immediately with an "Armor broken!" feed line and crack VFX; **(amended 2026-10-15, GG-T41)** it becomes a Broken fragment at commit like a shattered weapon (no shatter damage — that stays weapon-only). Repair works as for weapons. *(Test: `Armor_wears_per_enemy_hit_and_breaks_at_zero`.)*
- **Ability-bar weapon swap** (closed 2026-10-10): a bar slot can hold `weapon:<id>` from the bag; using it swaps weapons (costs a turn, no mana; the incoming weapon enters at factory durability); the save follows the swap.
- **Shatter auto-equip** (GG-T40): if a hero's weapon shatters and a backup is slotted on their bar, it equips for free the moment that hero is next given a command — no turn cost, no player input. *(Test: `Shattered_weapon_auto_equips_the_bar_backup_on_the_hero_s_next_action`.)*
- **Broken fragments (GG-T41):** a shattered weapon or broken armor doesn't vanish — `BattleResults.BrokenWeapons`/`BrokenArmors` (hero → item ids, recorded the instant each shatters/breaks, even if a bar backup is auto-equipped afterward) turns each into a `Broken <Name>` fragment at a Victory commit (`SaveData.Broken`, item id → count), not a whole item returned to the bag. Broken pieces can't be equipped (`Equipment.Candidates` only reads `Inventory`, never `Broken`) or repaired (`Repair` only ever touches a hero's equipped gear); they're listed on the Items screen (All/Gear tabs, "can't be equipped or repaired" note) and the Blacksmith's Salvage tab, and `Crafting.SalvageBroken` scraps one for `SalvageYield(c, item, 0)` materials ([§24.6](#246-crafting-recipes)) plus Blacksmith XP. *(Tests: `A_shattered_weapon_is_recorded_in_results_so_commit_can_turn_it_into_a_broken_fragment`, `A_shattered_weapon_becomes_a_broken_fragment_instead_of_vanishing`, `A_shattered_armor_becomes_a_broken_fragment_instead_of_vanishing`, `A_weapon_that_shattered_and_had_a_bar_backup_auto_equipped_does_not_also_return_whole_to_the_bag`, `Broken_fragments_cannot_be_equipped_and_salvaging_one_grants_materials_and_blacksmith_xp`.)*
- **Telegraph (added 2026-10-15, GG-T39):** each hero's battle card shows a thin weapon/armor wear bar (`BoardView.UnitView.SetGear`); at ≤10% remaining it pulses amber, and at the weapon's last-strike durability of 1 it glows red with a cracked-weapon badge instead. The drag preview calls out an imminent shatter ("This pincer will shatter your Iron Sword!") before the drop commits, for any attacker the previewed pincer would wear to 0 — not just the dragged hero. *(Harness: `python tools/harness.py durability`.)*

### 24.6 Crafting recipes

`RecipeDef` (`recipes.json`): station, inputs, gold, optional `BaseItem` consumed, result. `Crafting.Craft` checks gold, inputs, base item and result stack room atomically.

| Station | Count | Where |
|---|---|---|
| Forge (no base) | 11 | Blacksmith → Forge |
| Forge with base item (upgrades) | 39 | Blacksmith → Upgrade — **Amended 2026-10-09:** the old unused `UpgradeLibrary` now has a UI |
| Alchemy | 14 | Alchemist → Brew |
| Enchant (base weapon + essence + 2 Arcane Dust + gold) | 52 | Alchemist → Enchant |

*(Test: `Every_recipe_is_craftable_with_its_inputs`.)* **Salvage** (`Crafting.Salvage`): break one equipment piece into 50% of its base Forge recipe's inputs (floor, min 1). **Amended 2026-10-15 (GG-T42):** every equipment piece is now salvageable — pieces without a base recipe fall back to a flat materials-by-type-and-rarity table (`Crafting.FallbackSalvage`, keyed by Weapon/Armor/Relic × rarity). Yield also scales with the piece's remaining durability (`Crafting.SalvageYield(c, item, durability)`): a near-dead or Broken piece returns proportionally less (floor), but never less than 1 of the cheapest input. Bag salvage (`Crafting.Salvage`) always eats the worst (most-worn) tracked copy first; `Crafting.SalvageEquipped` lets a hero's currently-equipped weapon/armor be scrapped directly instead of unequipping it first. **Amended 2026-10-15 (GG-T41):** `Crafting.SalvageBroken` scraps one `SaveData.Broken` fragment the same way, at durability 0 ([§24.5](#245-weapon-durability)). *(Tests: `Every_equipment_item_has_a_salvage_yield_via_recipe_or_fallback_table`, `Salvage_of_equipment_without_a_recipe_uses_the_type_and_rarity_fallback_table`, `Salvage_yield_scales_down_with_remaining_durability_but_keeps_at_least_one_of_the_cheapest_input`, `SalvageEquipped_clears_the_heros_slot_and_grants_durability_scaled_materials_instead_of_returning_it`, `Broken_fragments_cannot_be_equipped_and_salvaging_one_grants_materials_and_blacksmith_xp`.)*

#### 24.6.1 Tier ladder (GG-T35, amended 2026-10-15)

Each crafting station has its own level — **Apprentice → Journeyman → Master** — raised by XP from every craft, brew, repair and salvage done there (`StationProgress`, `SaveData.StationXp`; 150/450 total XP for Journeyman/Master, tuned in `VendorTiers.RankXp` since GG-T538, §25.0.10). `RecipeDef.Tier` (1–3, default 1 when absent from content) gates `Crafting.Craft` to the station's current level, failing with "Requires Journeyman"/"Requires Master" below it; Repair and Salvage aren't recipes and are always available, but still feed the Blacksmith's XP. Deliberately independent of §25.12 caravan upgrades (gold-bought stat bonuses, never recipe gates) so the two never double-gate the same thing — tested by `Caravan_upgrades_and_station_tiers_never_double_gate_each_other`. **Amended 2026-11-19 (GG-T432):** the gold-bought **facility** levels of [§25.12.2](#25122-facility-upgrades-gate-material-tiers-gg-t432) are a second gate on a different axis — the *material tier* (the result's rarity), not `Tier` — so a recipe needs both its station's skill tier and its facility (`Crafting.CanMake`). Caravan stat upgrades still never move station XP or open a `Tier`.

Content assignment: Forge's 11 base (no-`BaseItem`) recipes are all Apprentice; its 39 Upgrade recipes split Journeyman (the first upgrade off a base item) / Master (the deeper `+1 → +2` chains); Alchemy's 14 brews split Apprentice/Journeyman/Master by gold cost; all 78 Enchant recipes are Master (Alchemist-side, alongside Brew). The original "Iron Sword → Steel Sword → Mythril Blade → +1" ladder sketch predates this data shape; upgrades exist as data and UI. *(Tests: `A_station_starts_at_apprentice_and_levels_up_from_real_crafting_actions`, `Repair_and_salvage_grant_blacksmith_xp_but_never_alchemist`.)*

**Named upgrade passives (added 2026-10-15, GG-T47):** each weapon upgrade line's top step (13 originally; all 100 since GG-T181, §24.2.2) (its `+3`, the deepest Master-tier rung) carries a named passive — `ItemDef.PassiveName`/`PassiveDescription` (shown on the Equip compare panel and in `ItemUi.StatsText`) plus one of three data-driven mechanics, all keyed off the wielder's equipped `WeaponId` at combat time (no new Unit fields):
- `PincerApplyBuff`/`PincerApplyBuffChance`: a landed **pincer** strike (not a supporter or counter hit) has this chance to apply the named buff to the target — Flame Tongue's **Flamebrand** (20% Burn), Serpent Spear's **Serpent's Kiss** (25% Poison), Rune Axe's **Rune Frost** (15% Freeze), Shadow Blade's **Duskfang** (20% Blind), Crystal Wand's **Silencing Shard** (20% Silence).
- `LifestealFraction`: heals the wielder this fraction of any damage they land, pincer or not — Bronze Dagger's **Vampiric Edge** (5%), Rusty Sword's **Scrapper's Edge** (3%), Mystic Staff's **Soul Siphon** (5%), Hunter's Bow's **Bloodhunt** (4%).
- `AdjacentAllyDamageReduction`: while this hero stands adjacent to an ally hero, that ally takes this much less damage from any landed hit (the strongest adjacent guardian wins — doesn't stack) — War Hammer's **Guardian's Bulwark** (12%), Starfall Mace's **Celestial Aegis** (10%), Steel Sword's **Steel Bulwark** (10%), Iron Sword's **Ironwall** (8%).

*(Tests: `Flamebrand_applies_burning_on_a_landed_pincer_strike`, `Vampiric_edge_heals_the_wielder_a_fraction_of_damage_dealt`, `Guardian_passive_reduces_damage_to_an_adjacent_ally`, `Every_weapon_upgrade_lines_top_step_has_a_named_passive`.)*

#### 24.6.2 Recipe discovery (GG-T36, amended 2026-10-15)

Recipes start hidden; `RecipeDiscovery.IsDiscovered` reveals one once its station's level reaches its `Tier` (§24.6.1 — `Crafting.IsUnlocked` already derives that from `SaveData.StationXp`, so a tier-reached recipe is discovered by definition with no separate level-up event) or earlier, via `SaveData.DiscoveredRecipes`:
- **Read a recipe scroll** — a new `ItemType.RecipeScroll` item (`ItemDef.TeachesRecipe`) dropped like any other loot; the Items screen's detail pane offers "Learn recipe" (`RecipeDiscovery.Learn`), which consumes the scroll and discovers its recipe outright even if the station isn't high enough level to craft it yet (it then shows 🔒-locked, same as any other discovered-but-locked recipe).
- **First-ever pickup of the recipe's rarest input** — `RecipeDiscovery.RarestInput` picks the input with the highest `ItemRarity` among a recipe's `Inputs` (ties: higher `BaseCost`, then id); `BattleFactory.Commit`'s loot grant calls `RecipeDiscovery.OnItemAcquired` the moment an item's owned count goes from 0 to positive, discovering every still-hidden recipe whose rarest input is that item.

The Blacksmith/Alchemist recipe lists (`HubScreens.RecipeList`) show discovered recipes as before, plus a `???` row for an undiscovered recipe missing exactly one input (`RecipeDiscovery.OneAwayMissing`) naming that missing material — everything else undiscovered is hidden outright. Selecting a `???` row shows the input checklist (materials aren't secret) but not the recipe's name, result or price. Discovery only gates visibility: `Crafting.Craft`'s gold/inputs/tier checks are unchanged. Three scrolls ship in content as rare drops (`scroll_recipe_sword_tempering` off `goblinthug00`, `scroll_recipe_holy_water` off `ghost`, `scroll_recipe_holy_enchant` off `demonlord`). *(Tests: `Tests/Core.Tests/RecipeDiscoveryTests.cs`.)*

#### 24.6.3 Masterwork variants (GG-T46)

Every Forge craft and Upgrade (never Alchemy/Enchant) rolls a chance (`Crafting.MasterworkChance`) at a **Masterwork** result on a Weapon- or Armor-slot item: base 5%, +0.2% per point of the active party's combined Luck (`Formulas.StatsAtLevel` summed over `SaveData.Party`, levels included), +5% per Blacksmith station level above Apprentice ([§24.6.1](#2461-tier-ladder-gg-t35-amended-2026-10-15)), capped at 35%. A hit rolls a random **+10–20% stats** multiplier (`WornGear.Mastery`/`HeroSave.WeaponMastery`/`ArmorMastery`) and a fixed **+25% max durability** (`BattleConfig.MasterworkDurabilityMultiplier`), shown with a ★ in front of the item's name everywhere it's listed (bag, Equip, Status, Repair) and in the craft toast. The roll travels with the specific piece like per-instance durability ([§24.5](#245-weapon-durability)): unequipping remembers it, equipping restores it, and it's gone for good if the piece shatters/breaks or is swapped out for an ability-bar backup (backups always enter at factory spec). *(Tests: `Masterwork_chance_grows_with_party_luck_and_blacksmith_level_and_caps`, `Forge_craft_can_roll_a_masterwork_variant_with_boosted_stats_and_durability`, `Unequipping_a_masterwork_weapon_remembers_its_roll_and_requipping_restores_it`, `Enchant_crafts_never_roll_masterwork_even_though_they_also_touch_weapons`.)*

#### 24.6.4 Reagent potency (GG-T45, owner direction)

A handful of Alchemy recipes carry a `RecipeDef.PotencyInputs`/`PotencyResult`/`PotencyResultCount` on top of their normal `Inputs`/`Result`: brewing with a rarer reagent swapped in for the recipe's key material — e.g. a **Pure Slime Gel** (Rare, a low-chance bonus drop from the slime family's own table, [§24.7](#247-drops)) instead of a plain Slime Gel — yields a stronger version of the same potion instead of the base one, for the same gold cost. `Crafting.HasPotencyInputs`/`Crafting.Craft(c, s, r, potency: true)` check and consume the potency set instead of the normal one; `AlchemistScreen`'s Brew detail shows the greater result and its own reagent checklist and Brew button beneath the normal one whenever a recipe has one. Today: **Greater Healing Potion** (90 HP vs. 50) off `recipe_potion_healing`, and **Greater Antidote** (cures everything, not just poison) off `recipe_antidote` — both gated on the same Pure Slime Gel, so a lucky rare drop has a use beyond unlocking new recipes. Adding another potion's potency variant is a content-only change (no code): give its recipe `PotencyInputs`/`PotencyResult`. *(Test: `Brewing_with_the_potency_reagent_yields_the_greater_result_instead`.)*

#### 24.6.5 Lucky craft — the Diamond (GG-T235)

*Amended 2026-11-21 (GG-T568): the Diamond is now the **Mana Shard** on screen ([§24.9.1](#2491-the-currencies-wiki-entries-gg-t568)); a lucky craft costs 50 Shards (or 300 G).*

Owner 2026-11-05: "trying to do a lucky craft with a 'Diamond' to increase chances of a better item that's forged." The **Diamond** (`mat_diamond`, Epic crafting material, never sold by a vendor, never dropped) ~~comes **only** from the LuckyDiamond paywall hook ([§25.18](#2518-rested-and-the-paywall-hooks-gg-t232)): the Forge detail's **Buy Diamond · 1g · 1s** button runs a timed job that lands one in the bag (a full stack refunds).~~ **Amended 2026-11-20 (owner, GG-T455):** comes from the Diamond exchange ([§25.18.1](#25181-the-diamond-exchange-gg-t455-owner-2026-11-20)); the Forge detail's `buy-diamond` button is now **Diamond Store**, and a lucky craft costs **`Crafting.LuckyDiamondCost` = 50 Diamonds (or 300 G)**, paid through `DiamondExchange.Pay` on top of the recipe's own gold, so it is on offer with or without Diamonds in the bag (short of both, it offers the store). Any Forge recipe whose result can roll Masterwork ([§24.6.3](#2463-masterwork-variants-gg-t46)) also shows **Lucky craft (use 1 Diamond) · ★ N%** while one is held: the same craft as **Craft** (`Forging.Forge(c, s, r, lucky: true)` → `Crafting.Craft(…, lucky: true)`, guided first forges included), but it spends one Diamond and rolls Masterwork at `MasterworkChance(c, s, lucky: true)` — the normal chance **+40%**, capped at **75%** instead of 35% — and a lucky Masterwork's stat bonus rolls **+15–20%** instead of +10–20%. Tuning lives in `Crafting.LuckyChanceBonus`/`LuckyMaxChance`/`LuckyStatBonusMin`. A refused lucky craft (no Diamond, potency brew, ineligible result) consumes nothing. The result plays out over time (`LuckyCraftUi`): the Diamond spins up in a veil with a `magic` cue, then the result lands with `powerup` (Masterwork) or `clang` ("the Diamond's luck didn't hold"). Bots and `CampRoutine` never buy or spend Diamonds. *(Tests: `LuckyCraftTests`.)*

### 24.7 Drops

Per enemy `DropTable` ([§13.3](#133-death-and-loot)); 37 tables in content, one per enemy family. The Adventure list hints each stage's drops.

**Amended 2026-10-15 (GG-T43, owner direction):** every enemy family carries its own signature crafting/alchemy material (frogs → Frog Gland, goblins → Goblin Scrap Iron, slimes → Slime Gel, undead → Undead Bone, cyclopes → Cyclops Eye, vampire → Vampire Fang, etc.), matching the house convention that a wave clusters one family ([§22.7](#227-the-first-battle)) — so a player can pick a stage and farm it for a specific ingredient. Remains (gravestones, bone piles) and eggs still drop nothing ([§14.5](#145-remains--gravestones-and-bone-piles-added-2026-10-10-owner-direction), [§14.8](#148-reinforcements-and-eggs-added-2026-10-12-owner-direction)). The Bestiary lists a family's drops as soon as it's **Seen** ([§25.11](#2511-bestiary)). *(Test: `Every_enemy_family_has_a_drop_table_with_a_signature_material`.)*

#### 24.7.1 Material economy

```
ENEMIES ─▶ drop tables ─▶ Inventory ─┬▶ Blacksmith (forge / upgrade / salvage / repair) ─▶ gear
                                     ├▶ Alchemist (brew / enchant) ─▶ consumables, enchanted gear
                                     └▶ Merchant (sell 50%) ─▶ Gold ─▶ buy, craft, learn, summon, inn
```

### 24.8 Specific items

- **Mage Robes** (`eq_armor_mage`, Uncommon): +2 orbs at battle start; **Wizard Robe** (`eq_armor_wizard`, Rare): +3; stacks across the party, capped by the bank.
- **Sleep Dart** (`cons_sleep_dart`, stack 5): casts `sleep` (PickActor) with no mana — the item-casts-a-spell path.
- **Sunfire Amulet** (`eq_amulet_sunfire`, relic): Fire damage taken ×0.7.
- Consumables table: [§4.3](#43-item-stacks-and-effects).

### 24.9 Currency

**Amended 2026-10-09:** coins collected in battle *are* gold. `BattleResults.Coins` is credited to `SaveData.Gold` (and the `LifetimeCoins` ticker) at a **victorious** commit; coins seen in a lost or escaped battle are lost. The old `GoldTracker` bridge is gone. Gold sinks: Merchant, crafting, repair, Inn, learning abilities, Summon Circle. New saves start with **200 G**.

#### 24.9.1 The currencies (wiki entries, GG-T568)

Each currency has one stable id, one standalone description (its Prose glossary text) and its numbers in data or in one
tuning class. Player-facing names come from one place in code (`ShardText` for the premium currency).

- **Gold** (`gold`). Earned as coins in battle, quest and milestone rewards, and by selling at the Merchant. It belongs
  to the save slot (`SaveData.Gold`). It pays for nearly everything at Camp ([§24.9](#249-currency)).
- **Mana Shard** (`aetherite_shard`; glossary `glossary:aetherite_shard`). The premium currency, bought with real
  money in the **Shard Store**. It pays for a random summon, an instant Rested refill and a lucky craft
  ([§25.18.1](#25181-the-diamond-exchange-gg-t455-owner-2026-11-20)). Owner 2026-11-21: *"the real-money currency is
  'Aetherite Shard(s)'"*; it was called **Diamonds** until GG-T568, and **Aetherite Shard(s)** until GG-T675 (owner 2026-11-22: **Mana Shard(s)**, mana made stable). The rename is words only: the item id
  `mat_diamond`, the store product ids `diamonds_100` … `diamonds_2500`, the save fields and the `Diamond*` class names
  are unchanged, so saves and purchases still work. Its item (`mat_diamond`) is named "Mana Shard"; its icon is
  still the game-icons cut diamond (new crystal-shard art is listed in docs/ART_GAPS.md).

---

## 25. The Camp (merged hub)

**Amended 2026-10-09 (owner decision):** the FF-style **Camp** menu (`HubScreen`) replaces the seven vendor scenes, the `VendorNavBar` and StageSelect-as-home. It is the "merged hub" the old §25.9 deferred.

### 25.0 Camp navigation

`HubScreen`: a party window (portrait, name, level, HP gauge per active hero; tap → Status), a menu, and a side window (Gold, play time, campaign stages cleared). Menu rows, in order: **Adventure** ("Stage N"), **Items**, **Status**, **Equip**, **Abilities**, **Party**, **Merchant**, **Blacksmith**, **Alchemist**, **Inn** (cost or "Rested"), **Summon Circle**, **Bounties** (progress), **Bestiary**, **Settings**, **Save & Quit**. Each sub-screen is a `HubPage` with a header (back, title, gold) over a themed backdrop; Back returns to Camp. Every successful action toasts, plays a cue, persists and refreshes (`HubPage.Done`).

**Amended 2026-11-19 (owner, GG-T435): one Heroes row.** Status, Equip, Abilities, Talents and Party were four or five Camp rows for one hero; they are now the tabs of one **Hero screen** (`HeroScreen`, `App/Screens/HeroScreen.cs`, title "Heroes") behind a single **Heroes** row where Status used to sit (`camp-heroes`; it reads "Train a skill!" while onboarding step 7 is ahead and opens on Abilities then, §25.0.9, otherwise the party's unspent skill points, opening on Status).
- **Layout:** the header, then the tabs **Status | Equip | Abilities | Party** (`hero-tabs`; `tab-status` …), then a **roster strip** (`hero-picker`): one card per hero (`hero-pick-<classId>`: portrait, name, level, **Party** or **Reserve**), the party first, the picked hero framed in gold; it scrolls sideways when the roster is wider than the column and its cards share the row when not. The Party tab leaves the strip out (it lists every hero itself). Then the tab's own body, unchanged from the screen it was.
- **One hero across tabs:** the picked hero carries from tab to tab; picking another forgets a previewed Equip piece and a selected talent node.
- **Abilities** keeps its sub-tabs (Known, Items, Learn, Passives, Locked) and gains **Talents**: the skill trees and the per-hero Respec (§25.6.3), with no battle bar above them.
- **Ids and old routes:** the screen's id is its tab's (`status`, `equip`, `abilities`, `talents` on the Talents sub-tab, `party`), so the camp tips (`abilities`, `abilities-learn`) fire when the Abilities tab opens and the automation `goto`/`-start` names still match. `StatusScreen`, `EquipScreen`, `AbilitiesScreen`, `SkillTreeScreen` and `PartyScreen` are thin wrappers that open the Hero screen on their tab: Camp's party cards (Status, on that hero), the Crucible's **Change team** (Party) and `-start status|equip|abilities|talents|party` all land there. Every element name the tips and harness flows use (`guided-lesson`, `craft`, `learn-*`, `passive-*`, `locked-*`, `talent-*`, `bar-move-*`, `status-*`) is kept.

**Amended 2026-10-19 (owner, GG-T119):** the service rows — **Merchant**, **Blacksmith**, **Alchemist**, **Enchantress** (new, §25.3.4) and **Inn** — appear only once that Camp NPC has joined the caravan (§25.0.1); **Talk** (§27.1) sits after Party. A new save's Camp has none of them.

### 25.0.1 Camp NPCs join along the way (GG-T119)

The Merchant, Blacksmith, Alchemist, Enchantress and Innkeeper are people the party meets on the journey, not fixtures of the camp. Each joins at a story beat (§27.1) through a dialogue `JoinNpc` effect, which sets the save flag `npc:<id>` (`CampNpcs.Joined`); until then their Camp service is hidden. Every NPC also has a Camp **Talk** dialogue — from Camp's Talk row, and from a **Talk** button in the header of their own station (`HubPage.Npc`).

~~| NPC | Service | Joins (story beat) | First launch it plays before |~~ The original table (ThemeStart Tithe Moors for the Merchant; GearWorn 60%, CanBrew and FirstArmor for the Blacksmith, Alchemist and Enchantress) is superseded by §25.0.1.1 below (GG-T537).

#### 25.0.1.1 Every vendor joins on a story beat (amended 2026-11-21, owner, GG-T537; supersedes GG-T521)

Owner: "the Blacksmith and every other Camp vendor join on a story beat that is part of the campaign progression, not
through incidental triggers." The fresh-player run (docs/FRESH_PLAYER_RUN.md) met the Blacksmith 45 minutes in, and only
because a bought sword happened to wear below 60%. Now every Camp vendor and keeperless station arrives on the **clear of
a campaign stage**, one new system per stage, just before the player first needs it. No vendor waits on worn gear, gold,
loot or chance; the `GearWorn`, `CanBrew` and `FirstArmor` dialogue triggers are gone from the engine.

| # | Clear this stage | Who or what arrives | Join beat (dialogues.json) | The scene, for the writer |
|---|---|---|---|---|
| 1 | The Tithe Moors I (the tutorial) | **Merchant** (§25.1) | `greenvalley_road_merchant` | A lantern peddler who trades the moor road between tithe-takers hails the newly woken party after their first fight and throws in with people the Choir's ledgers don't list. |
| 2 | The Tithe Moors II | **Innkeeper**: the Inn and Medic Wagon (§25.3.1) | `greenvalley_after_first` | A moorland innkeeper whose village was tithed empty catches up with the caravan and offers to keep it fed and rested, since there is no one left at home to serve. |
| 3 | The Tithe Moors III (the prologue boss) | **Blacksmith**: Repair (§25.0.4) | `greenvalley_smith` | The tithe-captain's forced smith, freed when the captain falls, refused to forge for the Choir again; he will mend the gear of the sleepers who broke the levy. |
| 4 | The Lantern Road I | **Alchemist**: Brew (§25.0.5), and with him the Blacksmith's workshop (Forge, Upgrade, Salvage, lucky craft; §25.0.4, §25.0.6) | `desert_alchemist` | A splice-cutter selling cures at a lantern shrine recognises the sleepers' undimmed light and joins to study it, handing over a first bottle. |
| 5 | The Lantern Road II (chapter 1's stage) | **Summon Circle** (§25.10) and the **Diamond Store** row (§25.18.1) | `lanternroad_summon_circle` | In the shrine of the moonless mural Warden finds the Still's thaw-sigil; with the Alchemist's salts it can wake another sleeper, so the first circle is drawn at Camp. |
| 6 | The Lantern Road III (the region boss) | **Bounties** and **Mercenaries** (§22.4, §25.10.1) | `lanternroad_bounties` | Travellers at the Innkeeper's fire name the beasts and outlaws on the road; she pins their names to a board, and the sellswords who hunt them start coming by. |
| 7 | The Shardfall I | **Crucible** (§22.10) | `desert_crucible` | The Blacksmith finds a ring of fused shard-glass that replays every fight held inside it, and sets it up as a proving ground. |
| 10 | The Arkgrave I | **Enchantress** (§25.3.4) | `swamp_enchantress` | A voice in the fog over the grounded arks: a Halcyon-taught binder who sets runes into armour, and who wants to know why the sleepers' light holds when hers fades. |
| 11 | The Arkgrave II | **Caravan** (§25.12) | `swamp_caravan` | The Merchant strips axles and plating from the ark wrecks and rigs the caravan into wagons that can carry a growing camp. |

*Play order (checked 2026-11-21, GG-T538):* the **#** column is the campaign index, the order of `campaign.json`'s
`Themes` and their `Stages` (`ContentDb.CampaignStages`), which is the order a player meets and unlocks stages and what
`HighestClearedStage` counts. The Shardfall (`desert-01..03`) is stages 7 to 9 and the Arkgrave (`swamp-01..03`) 10 to
12, so the Crucible at The Shardfall I does come before the Arkgrave vendors. The `Chapters` list is a separate thing:
each chapter names the stage whose arrival plays its crawl (chapter 2, "The Grounded Arks", at `swamp-01`; chapter 7, "The
March on the Anchors", at `desert-05`, the postgame Deep Shardfall at index 35). Its numbering is not the region order.

- **Not gated:** Adventure, Items, Heroes, Talk, the Bestiary and Settings are open from the start; **Quests** appear as
  each quest's own unlock stage is cleared (§22.11); the **Rested** pool (§25.18) is the launch gate itself, not a vendor,
  and runs from the first battle.
- **The beat:** a vendor's join beat is a `StageCleared` dialogue with no conditions: a keeper's carries a `JoinNpc`
  effect on every branch, a keeperless station's an `OpenStation` effect (`DialogueEffectType.OpenStation`, ids in
  `DialogueDef.Stations`: `summon`, `bounties`, `crucible`, `caravan`). `ContentLoader.Validate` refuses a `JoinNpc` or
  `OpenStation` anywhere else. A keeper's service opens when the `npc:<id>` flag is set; a keeperless station opens on
  the stage clear itself (`CampUnlocks.SummonAfter` 5, `BountiesAfter` 6, `CrucibleAfter` 7, `CaravanAfter` 11, held to
  their beats by `CampUnlocksTests`), so a skipped scene never keeps it shut.
- **The join moment:** the victory's results screen lists the arrival ("Blacksmith joins the caravan! · At Camp",
  "Summon Circle opens at Camp! · At Camp", `CampUnlocks.Arrivals`, row `results-arrival`). The beat plays on the way back
  to Camp (`DialogueRunner.PendingInterlude` / `PendingVendorBeat`; after a chapter crawl when one is due), or at the next
  launch if the player takes "Next battle". Its toast names the vendor bare: "Blacksmith joins the caravan!". All the
  scenes are lorem seeds in Prose (`dialogue:<id>`, `tools/prose_text.py seed-missing`) until the writer drafts them from
  the descriptions above.
- **Camp rows while closed** read "Joins after The Tithe Moors III" (a keeper, from its beat's stage) or "Opens after The
  Lantern Road II" (a keeperless station); the Blacksmith's locked workshop tabs say "Opens when the Alchemist joins, after
  The Lantern Road I".
- **Existing saves** (save version 12, `SaveMigrations.EnsureVendors`, on every load): every vendor whose stage the save
  has cleared joins at once, so nobody is locked out; nobody met earlier under the old triggers ever leaves (an early
  Alchemist stays). A save from before GG-T537 takes the beats it is past as told, so it doesn't get a run of scenes at the
  next Camp; a current save past a beat still sees its scene.
- **Guided flows** still line up: the first repair tip waits for the Blacksmith (stage 3), the guided first brew for the
  Alchemist, who brings the bottle (stage 4; with loot still missing the tip says what to bring back), and the first forge
  and first armor follow when the workshop opens with him; the Enchantress's first enchant follows at stage 10.
- *(Tests: `CampNpcTests.Playing_the_campaign_in_order_meets_each_vendor_after_its_stage`,
  `Every_vendor_joins_on_its_own_stage_clear`, `The_blacksmith_joins_after_the_prologue_boss_not_on_worn_gear`,
  `The_alchemist_joins_after_the_lantern_road_i_with_a_bottle`, `The_enchantress_joins_after_the_arkgrave_i`,
  `Saves_past_a_vendor_beat_get_the_vendor_on_load`; `CampUnlocksTests`; harness `fullloop`.)*

- **Always met, early:** a join beat has no conditions and every branch of it joins (nobody can be refused away), and the services the campaign leans on (shop, rest, repair) all arrive inside the prologue's three stages (§25.0.1.1). *(Tests: `CampNpcTests` — `Every_npc_joins_on_every_branch_of_a_story_beat`, `Playing_the_campaign_in_order_meets_everyone_by_the_third_expedition`, `Every_npc_has_a_camp_talk`.)*
- **The bots meet them the same way:** `CampRoutine` (§25.3.3) uses only joined services, and `BalanceReport`'s story rows play each launch's pending beats headlessly (`DialogueRunner.PlayPendingBeatsHeadless`, GG-T118); a finished run asserts everyone was met. Its no-story yardstick rows join every NPC up front, like a pre-GG-T119 save.
- **Test Mode** (§6, GG-T93) never plays story, so `TestLoop.NewSave` **auto-joins every NPC** up front — the loop exercises repair, rest, buy, brew and learn from cycle 1. The automation sweep's preview save and `newgame npcs=1` do the same.
- **Old saves:** save version 4's migration joins every NPC on any older save (they had been using every service). New Game+ keeps flags, so nobody leaves.
- Abilities/Learn, the Summon Circle, Bounties, the Caravan and the Crucible (§22.10) have no keeper; the last four open on their own stage clears (§25.0.1.1, §25.0.3).

### 25.0.3 Stations open one at a time (GG-T174)

A new player meets the Camp one system at a time. Every station is always **listed** on the Camp menu, but a closed one is a dimmed row saying when it arrives (`CampUnlocks`):

| Station | Opens | Row while closed |
|---|---|---|
| Merchant, Inn, Blacksmith, Alchemist, Enchantress | when their keeper joins (§25.0.1.1) | "Joins after The Tithe Moors III" — read from the join beat in content |
| Summon Circle, Diamond Store | 5 cleared (The Lantern Road II) | "Opens after The Lantern Road II" |
| Bounties, Mercenaries | 6 cleared (The Lantern Road III) | "Opens after …" |
| Crucible | 7 cleared (The Shardfall I) | "Opens after …" |
| Caravan | 11 cleared (The Arkgrave II) | "Opens after …" |

**Amended 2026-11-21 (GG-T537):** the milestones moved so each stage clear brings one new thing (§25.0.1.1): Bounties from 2 to 6, the Crucible from 5 to 7, the Caravan from 10 to 11; the Diamond Store row, open from the start before, now opens with the Summon Circle (a save that bought Diamonds keeps it). So after the first win the Merchant is the one shop open (the Inn follows a stage later), and its camp tip (§25.0.2) points at the Healing Potion, scrolled into view, to spend the first battle gold on. A station the save has already used — an active bounty, a recruit, a Crucible run, a caravan upgrade — never locks again, so older saves are unaffected; Test Mode and New Game+ saves see everything. *(Tests: `CampUnlocksTests`; harness `fullloop` asserts the Merchant and Bounties start locked and the first four vendors are open after four battles.)* Long menu values wrap instead of clipping.

### 25.0.4 Onboarding step 2: the Blacksmith repairs (GG-T175)

~~The Blacksmith arrives when he is first needed: his join beat (`greenvalley_smith`) has the story trigger `GearWorn` (`Percent` 60), with a fallback theme (`desert`).~~ **Amended 2026-11-21 (GG-T537, supersedes GG-T521):** he joins after the prologue boss, The Tithe Moors III (§25.0.1.1), since gear wears from the first battle; the fresh run met him only 45 minutes in, through a bought sword. The Camp row reads "Joins after The Tithe Moors III" while he is away.

- **Repair first:** `BlacksmithScreen` opens on **Repair**, and Repair is his only open tab at first — Forge, Upgrade and Salvage are dimmed, and tapping one toasts "Opens once the Alchemist joins" (`CampUnlocks.SmithWorkshopOpen`: the Alchemist has joined, or Test Mode / New Game+; old saves joined everyone in the v4 migration, so they keep it). Repair's "Salvage instead" offer (GG-T42) also waits for the workshop. The opened Forge starts with a guided first forge (§25.0.6).
- **Guided prompt:** his camp tip (§25.0.2) is resolved for the save (`CampTips.Resolve`): it names the most worn piece ("Paladin's Iron Sword is worn down to 50%. Tap it to repair it before it shatters in battle.") and rings that piece's repair row, named `repair-<class>-<slot>` (`Repair.RowNameFor`). With nothing worn it reads as written and points at the first repair row.
- **Bots:** `CampRoutine` repairs once he has joined but forges and salvages only once the workshop is open, like the player.
- *(Tests: `CampNpcTests.The_blacksmith_joins_once_gear_first_wears_below_sixty_percent`, `CampUnlocksTests.The_blacksmith_opens_with_only_repair_until_the_alchemist_joins`, `CampTipsTests.The_blacksmith_tip_names_and_points_at_the_most_worn_piece`; harness `smithrepair` — tip rings the worn row, Forge tap stays on Repair, tapping the row repairs it.)*

### 25.0.5 Onboarding step 3: Alchemy potions from loot (GG-T176)

The Merchant sells an **Empty Bottle** (`empty_bottle`, 4 G) and the Alchemist brews **bottle recipes**: one Empty Bottle plus monster trophies looted from enemies makes one potion — Healing Potion (Wolf Pelt + Tattered Cloth), Mana Potion (2 Bat Wing), Antidote (Frog Gland), Eye Drops (Vulture Feather), Greater Antidote (Swamp Ichor + Marsh Moss, 10 G), and at Journeyman tier Hi-Potion (2 Goblin Ear + Scorpion Stinger, 15 G) and Ether (Hag Claw + Bat Wing, 5 G). A bottle recipe always costs less than buying what it makes, even with every input bought at the Merchant (`Brewing.ShopCost` < `Brewing.BuyCost`); with loot it is just the bottle. The older slime-gel/arcane-dust recipes stay.

- **The Alchemist joins after The Lantern Road I** (**amended 2026-11-21, GG-T537**; it was the `CanBrew` trigger, looted inputs for a first bottle brew with a Shardfall fallback, §25.0.1.1). He hands over one Empty Bottle on joining, so the first brew needs no shopping; three Tithe Moors battles have usually looted the rest, and if not the tip says what to bring back. The Camp row reads "Joins after The Lantern Road I" while he is away. Joining him also opens the Blacksmith's workshop (§25.0.4).
- **Guided prompt:** on the first visit `AlchemistScreen` opens with the first brewable bottle recipe selected (`Brewing.FirstBrew`) and his camp tip (`CampTips.Resolve`) names it and what goes in, says what the Merchant charges for the potion, and rings **Brew** (`craft`). With only the bottle missing it rings the recipe row and sends the player to the Merchant for one.
- **Bots:** `CampRoutine`'s potion stock brews any recipe that makes its stopgap potion, buying a single bottle from the Merchant when a bottle recipe is short only of that.
- *(Tests: `EconomyLoopTests` — `Every_dropped_crafting_material_feeds_at_least_one_recipe` (no looted material is a dead end), `Bottle_recipes_brew_from_loot_for_less_than_buying_the_potion`, `The_camp_routine_buys_a_bottle_to_brew_its_loot`; `CampNpcTests.The_alchemist_joins_once_the_loot_for_a_first_brew_is_in_the_bag`; `CampTipsTests.The_alchemist_tip_walks_the_first_bottle_brew`.)*

### 25.0.6 Onboarding step 4: forge the first weapon (GG-T177)

Once the workshop opens (§25.0.4), the Forge offers three **starter weapon recipes** (`Starter: true` in `recipes.json`): Apprentice Forge recipes that cost **no gold** and take only materials the Tithe Moors' enemies drop — **Oak Club** (Wolf Pelt + 2 Cracked Fang), **Training Rapier** (2 Broken Blade + Iron Ore) and **Tallow Lantern** (2 Slime Gel + Tattered Cloth). Between them they suit every starting hero (Paladin, Barbarian, Cleric). Content validation holds a starter recipe to one weapon, Apprentice tier, no gold.

- **The hero it suits** (`Forging.SuitedHero`): a party hero who can wield it and would be better off than with what they hold, best stat match first — the weapon's stat bonuses weighted by the class's growth (`Forging.Affinity`), so the Wisdom lantern goes to the Cleric — then the bigger gain.
- **Guided prompt:** with the repair tip behind him, `BlacksmithScreen` opens on **Forge** with the first-forge recipe selected (`Forging.First`: a carryable starter recipe that suits a party hero, ready ones first). The Forge tab has its own camp tip (`blacksmith-forge`; `HubPage.TipStation`), resolved for the save: it names the weapon, its inputs and the hero, and rings **Craft**; with materials still missing it rings the recipe row and says how many of which to bring back. The recipe detail reads "First forge: free, and the Cleric equips it".
- **Forge and equip:** tapping Craft runs `Forging.Forge` — the weapon is made and equipped on the hero it suits ("Made Tallow Lantern: the Cleric equips it"), and the save flag `onboard:first-forge` ends the guidance; later forges go to the bag as usual. Test Mode and New Game+ saves skip the guidance.
- **Bots:** `CampRoutine` forges starter weapons like any other recipe, once the workshop is open.
- *(Tests: `EconomyLoopTests.Starter_weapon_recipes_cost_no_gold_and_take_only_first_theme_drops`, `EconomyLoopTests.The_first_forge_equips_the_starter_weapon_on_the_hero_it_suits`, `CampTipsTests.The_forge_tip_walks_the_first_forge`; harness `firstforge` — Forge tip rings Craft, tapping it forges the lantern for no gold and the Cleric equips it.)*

### 25.0.7 Onboarding step 5: forge armor (GG-T178)

The armor-set recipes (GG-T182's twenty named sets, `armorsets.json`) **open at the Forge after the first weapon** (§25.0.6): until `onboard:first-forge` is set, any Forge recipe whose result is a set piece (`Forging.IsSetArmor`) is hidden from the Forge list and `Crafting.Craft` refuses it ("Armor recipes open after your first forge"; `Forging.ArmorOpen`). Plain armor and jewelry recipes are not gated. Test Mode and New Game+ saves skip the gate, and `CampRoutine` only picks open recipes. Four Apprentice set recipes take only Tithe Moors drops — **Wayfarer's Jerkin/Trousers** (Leather + Wolf Pelt, Light) and **Militia Chausses/Gauntlets** (Iron Ore + Leather + Military Insignia, Medium) — so a first piece is forgeable before the desert.

- **The hero it suits** (`Forging.SuitedWearer`): a party hero proficient in its weight who gains over what they wear in that slot, best stat match first (`Forging.Affinity`), then the bigger gain.
- **Guided prompt:** once the first weapon is made the Forge tab's tip becomes `blacksmith-armor` ("Forge Armor"; `BlacksmithScreen.TipStation`), so it appears right after the weapon is forged. `Forging.FirstArmor` picks the piece: an open Apprentice set recipe that suits a party hero and can be carried, ready ones first, then stat match, fewest missing materials, cheapest. The Forge preselects it (moving off the forged starter weapon), the tip names the piece, its inputs and the hero and rings **Craft**; short of materials or gold it rings the recipe row and says what to bring back. The recipe detail reads "First armor: the Barbarian wears it".
- **Forge and equip:** Craft runs `Forging.Forge` — the piece costs its usual gold, is made and equipped on that hero ("Made Wayfarer's Trousers: the Barbarian wears it"), and `onboard:first-armor` ends the step (the Enchantress's join, GG-T179, follows it). Later forges go to the bag.
- *(Tests: `EconomyLoopTests.Armor_recipes_open_after_the_first_weapon_and_the_first_piece_is_worn`, `EconomyLoopTests.A_first_armor_piece_is_forgeable_from_first_theme_drops`, `CampTipsTests.The_armor_tip_walks_the_first_armor_piece`; harness `firstarmor` — no set recipes before the first forge, then the armor tip rings Craft and the Barbarian wears the trousers.)*

### 25.0.8 Onboarding step 6: enchant armor for passives (GG-T179)

Alongside the six weapon-only elemental enchants (`enchants.json`, which turn a weapon into its "(Element)" variant), the Enchantress binds **armor enchants** (`armorenchants.json`, `ArmorEnchantDef`: `Id`, `Name`, `Passive`, `Inputs`, `Gold`, `Tier`) into a worn armor piece (Head/Torso/Legs/Gloves/Boots). Each grants a real `Passive` ability (abilities.json) to the wearer in battle while the piece is intact:

| Enchant | Passive | Effect | Tier | Cost |
|---|---|---|---|---|
| Regeneration | `regenerate` (the enemy kit) | heals 6% MaxHP each time control returns | Apprentice | Arcane Dust + 2 Frog Gland, 40 G |
| Poison Ward | `poison_ward` | immune to Poisoned | Apprentice | Arcane Dust + 2 Slime Gel, 40 G |
| Thorns | `thorns` | an enemy whose hit lands takes 25% of it back ("Thorns") | Apprentice | Arcane Dust + 2 Cracked Fang, 40 G |
| Steadfast | `steadfast` | immune to Frozen and Sleep — always free to move (heroes already drag freely, so this stands in for "+move") | Apprentice | Arcane Dust + 2 Wolf Pelt, 40 G |
| Riposte | `counter_attack` (the enemy kit) | an adjacent attacker is struck back at half strength | Master | 2 Arcane Dust + Shadow Essence + 2 Broken Blade, 150 G |
| Mana Siphon | `mana_siphon` | every hit the wearer lands adds 5 mana charge | Master | 2 Arcane Dust + Spark Essence + 2 Goblin Ear, 150 G |

- **Where it lives:** the enchant id is on the piece (`EquippedArmor.Enchant`), not a new item, so set membership, wear, repairs and Masterwork rolls are untouched. One enchant per piece — a new one replaces it. Unequipping keeps it on that copy (`WornGear.Enchant`; an enchanted copy is put back on first), and it goes into battle on `ArmorPieceSpec.Enchant` / `EquippedPiece.Enchant`. `BattleRunner.HasPassive` checks the class kit, then every intact enchanted piece the unit wears; a broken piece leaves `Unit.Gear`, taking its passive with it, and a piece that breaks for good loses the enchant. Warded buffs announce "Warded!". Tuning: each passive's `abilities.json` trigger Amount (the old `BattleConfig.RegeneratePercent`/`ThornsFraction`/`ManaSiphonCharge`/`EnemyLifestealFraction` constants were removed in GG-T302, unread since GG-T296).
- **Tier:** like her weapon enchants, armor enchant tiers unlock with the Alchemist's station level, and each enchant feeds it 15 XP. The Apprentice ones take Arcane Dust and Tithe Moors trophies only.
- **The Enchantress joins after The Arkgrave I** (**amended 2026-11-21, GG-T537**; it was the `FirstArmor` trigger with an Arkgrave fallback, §25.0.1.1). By then the first armor piece (§25.0.7) has normally been forged since the workshop opened at stage 4. She hands over an Arcane Dust on joining. The Camp row reads "Joins after The Arkgrave I" while she is away.
- **Guided prompt:** `ArmorEnchanting.First` picks the first enchant: an unenchanted worn piece (set pieces first, so the guided first armor), and an open enchant whose materials are in the bag, else the fewest still to find, else file order. Her **Armor** tab (`EnchantressScreen`) lists worn pieces (`piece-<class>-<slot>`) and enchants (`aenchant-<id>`) and preselects both; her camp tip (`enchantress`, `CampTips.Resolve`) names the enchant, the passive, the hero and the piece and rings **Enchant** (`craft`); short of materials it rings the enchant row and says what to bring back. The **first enchant is free** of gold (`ArmorEnchanting.Gold`), and `onboard:first-enchant` ends the guidance. Test Mode and New Game+ saves skip it.
- **Shown:** the Status screen's gear list adds "✦ Regenerate" after an enchanted piece; the Equip screen marks the slot "✦" and names the enchant in its compare panel.
- *(Tests: `EconomyLoopTests.Armor_enchants_grant_passives_and_the_first_ones_take_first_theme_drops`, `EconomyLoopTests.An_armor_enchant_binds_into_the_worn_piece_and_stays_with_it`, `CampNpcTests.The_enchantress_joins_after_the_first_armor_piece`, `CampTipsTests.The_enchantress_tip_walks_the_first_armor_enchant`, `ArmorEnchantTests` (poison ward, thorns, regeneration in battle); harness `firstenchant` — forge the lantern and trousers, then the Enchantress tip rings Enchant and Regeneration binds into the trousers for no gold.)*

### 25.0.9 Onboarding step 7: train a new skill (GG-T180)

The last step of the onboarding chain. Once the first armor enchant is bound (`onboard:first-enchant`, §25.0.8), the **Learn** tab of the Abilities screen (gold-bought abilities, §25.6, US-231) is introduced, and a guided prompt trains one new skill and slots it on the battle bar.

- **When:** `Training.Step` — the first enchant is done and `onboard:first-skill` isn't. Camp's **Abilities** row reads "Train a skill!" while it's ahead (**Amended 2026-11-19, GG-T435:** the **Heroes** row now, which then opens the Hero screen on its Abilities tab). Test Mode and New Game+ saves skip it (`Training.FirstDone`). The Learn tab itself was never locked and stays open throughout; this step is the introduction, not a gate.
- **Guided skill:** `Training.First` picks the party lead first (then the rest of the party, then the reserve) and, for that hero, the first not-yet-known skill on its own class list (else the first universal skill by name). The Abilities screen opens on **Learn**, on that hero, with a guided card at the top (`guided-lesson`: the skill, what it does, "First lesson free", the bar slot it lands in) and a **Learn** button (`craft`). Learn rows are named `learn-<abilityId>`.
- **Free and slotted:** while the step is ahead, any skill (not spell) learned is the guided one: `Abilities.Price(s, a)` is 0 for it, and `Abilities.Learn` puts it in the first empty unlocked bar slot (else the last unlocked slot, `Training.SlotFor`) and sets `onboard:first-skill`, which ends the chain. Later skills cost their usual price (§25.6: 150G + 50G per `CooldownTurns`) and are slotted by hand.
- **Camp tip:** the Abilities screen shows the `abilities-learn` tip (its own, separate from the plain `abilities` tip, so a player who opened Abilities early still gets it): it names the hero and the skill, says the first lesson is free and which slot it goes in, and rings **Learn**.
- *(Tests: `EconomyLoopTests.The_first_skill_after_the_first_enchant_is_free_and_goes_on_the_bar`, `CampTipsTests.The_training_tip_walks_the_first_skill_after_the_first_enchant`; harness `firstskill` — after the first enchant the Abilities screen opens on Learn, the tip rings Learn, and the skill is learned for no gold and sits on the lead's bar.)*

### 25.0.2 Camp tips (GG-T173)

The first time the player opens a Camp station, a one-time **camp tip** points at the thing to do there: the screen dims, a gold outline and a bouncing pointing hand (`Art.Cursor`) mark the key control, and a card names the station and says what to do in one sentence, with **Got it**. The card sits at the bottom, or at the top when the control is in the lower half, so it never covers what it points at. Combined with the NPC join order above, stations — and their tips — arrive one at a time.

- **Table:** `Meta/CampTips.cs` — station (the page's `Id`), title, sentence, target control name (a trailing `-` matches a prefix, e.g. `recipe-` = the first recipe row). Today: Merchant, Blacksmith (the most worn piece's repair row, §25.0.4), the Blacksmith's Forge tab (the first forge's Craft button, §25.0.6, then the first armor piece's, §25.0.7), Alchemist (the first bottle brew's Brew button, §25.0.5), Enchantress (the first armor enchant's Enchant button, §25.0.8), Abilities (then, after the first enchant, its Learn tab's guided first skill, §25.0.9), Bounties. The onboarding chain (GG-T174…T180) refines wording and targets here.
- **Once per save:** dismissing records the station in `SaveData.SeenCampTips` (migrated to empty on older saves). Bot-played saves (`TestMode`) never get tips.
- **Replay:** Settings › How to play › **Replay tutorials** (GG-T539; was Replay camp tips) clears the set with every other lesson.
- **Coach marks (amended 2026-11-21, owner, GG-T539):** a tip is now a lesson in the §9 coach-mark style (`CampTipOverlay` → `Coach`): the popup (title, sentence, `Resources/Tutorial/camp-<station>.png` when there is one, Skip tutorial, Next — still named `camp-tip-card` / `camp-tip-ok`, its first box `camp-tip-ring`), then the stage dims except the exact control and only it takes taps until it is used: the Merchant's + on the Healing Potion and then **Buy** (`shop-commit`); a recipe row and then **Craft / Brew / Enchant** (`craft`), or Craft alone when the tip already rings it; the worn piece's repair row; the Summon Circle's **Draw** (`summon-draw`, a new tip). A control that can't be pressed (too poor to buy) becomes tap-to-continue, and a control that isn't there ends the marks, so nothing waits forever. The tip is marked seen when its lesson ends.
- **Camp-menu lessons (GG-T539, `CampCoach`):** when a vendor joins on a story beat (§25.0.1.1) the Camp menu opens a lesson on their row (rows are named `camp-<label>`: `camp-merchant`, `camp-blacksmith`, `camp-alchemist`, `camp-enchantress`, `camp-summon-circle`), so the player walks in and the station's own tip takes over. The first time a piece in the bag fits an empty slot of a party hero (a sword just bought), **Equip gear** walks Heroes → Equip → that hero → the slot → the piece (compare) → the piece again (equip), one boxed tap at a time.
- **Automation:** demo, test mode, `-autobattle` and harness runs suppress tips so the UI drivers see the plain screens; `-campTips` turns them back on. **GG-T542:** the harness follows lessons the way a player does (`coach_follow`: Next on the popup, a tap in each box, a move's dotted route as a drag), saving a super-2 screenshot and the mark's geometry (`.coach.json`) per popup and mark for `GridGame.Studio/tools/lesson_images.py`; `camptips`, `smithrepair`, `firstbrew`, `firstforge`, `firstarmor`, `firstenchant` and `firstskill` walk the coach-mark steps under `-campTips`; `coach [--camp]` plays the tutorial battle and the real To Camp flow, `coachcamp` the repair → Forge → armor → enchant → training chain, `coachskip` Skip tutorial (with its confirm) and Replay tutorials; `--record` records any run. A camp step's tap also completes when its effect lands in the save (`CoachStep.Also`, a fingerprint of gold, bag, gear, station XP, skills): a Craft rebuilds its page under the finger and its release could miss the box, which left "Tap Craft" up after the forge and let a second tap forge whatever the next lesson had preselected. `newgame lessons=` marks coach lessons seen, so a camp-tip harness can open a station directly. `python tools/harness.py camptips` checks show / dismiss / never-again / replay and screenshots each tip. *(Tests: `CampTipsTests`.)*
- Tab buttons are named `tab-<label>` (`Ui.Tabs`) so tips and harnesses can target them.

### 25.0.10 Vendor stock and recipes grow over time (GG-T538)

Owner 2026-11-21: Merchant stock, Blacksmith recipes and Alchemist recipes start small and grow, "some are progress
gated, some are training gated, the usual". The fresh-player run (docs/FRESH_PLAYER_RUN.md, GG-T520) found the
Merchant's first visit listing about 120 rows, materials from regions not yet reached among them. Two axes, all tunables
in one class, `Meta/VendorTiers.cs`:

- **Progress tiers.** Eight tiers; tier N opens on the clear of `VendorTiers.OpensAfter[N-1]`, a region's last stage, so
  a region's goods arrive as the party reaches it. A Merchant item carries its tier in content (`ItemDef.ShopTier` in
  items.json, curated with `GridGame.Studio/tools/vendor_tiers.py --assign`; a sold crafting material joins the tier after the region
  where it first drops). A Forge, Upgrade or Alchemy recipe's tier is **derived** (`VendorTiers.RecipeTier`): the latest
  tier any of its inputs or its base item can first be had at (dropped in a campaign region, chests included; bought; or
  made by another recipe), floored by its result's rarity band (Common 1, Uncommon 2, Rare 3, Epic 5, Legendary 7) and,
  for a set piece, by its set's level band (`SetLevelCeiling`). Content added later is tiered without new fields. The
  Enchantress keeps her own gate (the Alchemist's rank, §25.3.4).
- **Training ranks.** The station ranks of §24.6.1 (`StationProgress`, now reading `VendorTiers.RankXp`): every forge and
  brew is 15 XP, a repair or salvage 8, so about 10 forges or brews make Journeyman and 30 make Master. A rank opens the
  recipes of its `Tier` and adds 5% Masterwork chance (§24.6.3).
- **What a list shows:** the Merchant's Buy tab lists `Shop.Stock(c, s)`, the open tiers. A station lists a recipe once
  its progress tier is open (`VendorTiers.RecipeOpen`), and then as before (rank-reached or discovered, §24.6.2, or a
  `???` one input short). `Shop.Buy` refuses an item off the shelf ("Hi-Potion isn't in stock yet (after The Tithe Moors
  III)"); `Crafting.Craft` is unchanged, since a progress tier only gates what is listed, as discovery does.

| Tier | Opens | Region it fits | Merchant (+new, shelf) | Forge | Upgrade | Alchemy |
|---|---|---|---|---|---|---|
| 1 | from the start | The Tithe Moors | 18, shelf 18 | 22 | 45 | 9 |
| 2 | after The Tithe Moors III | The Lantern Road | +39, 57 | 21 | 57 | 8 |
| 3 | after The Lantern Road III | The Shardfall | +22, 79 | 22 | 66 | 3 |
| 4 | after The Shardfall III | The Arkgrave | +10, 89 | 16 | 27 | 3 |
| 5 | after The Arkgrave III | The Anchor Wastes | +6, 95 | 22 | 39 | 0 |
| 6 | after The Anchor Wastes III | The Still, Halcyon | +2, 97 | 22 | 33 | 3 |
| 7 | after Halcyon III | the Cathedral, the Bloom | +2, 99 | 12 | 33 | 0 |
| 8 | after The Bloom III | the postgame | 0, 99 | 9 | 0 | 0 |

| Rank | Station XP | About | Opens |
|---|---|---|---|
| Apprentice | 0 | from the start | `Tier` 1 recipes |
| Journeyman | 150 | 10 forges or brews | `Tier` 2 recipes (the first upgrade off a base item, most potions past the basics), +5% Masterwork |
| Master | 450 | 30 forges or brews | `Tier` 3 recipes (deep upgrades, Epic and Legendary gear), +10% Masterwork |

- **First visits:** a fresh Merchant shows 18 items plus any counter a reachable stage needs (19 on a new save, with
  the Venom Flask, the counter The Tithe Moors I asks for): Healing Potion, Antidote, Eye Drops, Purifying Salts, Disarm Kit, Empty
  Bottle, Cloth, Leather, Iron Ore, a starter weapon per weapon class (Rusty Sword, Bronze Dagger, Quarterstaff,
  Apprentice's Wand) and a light armor set (Wayfarer's Cowl, Padded Vest, Leather Gloves, Leggings and Boots). A fresh
  Forge lists 22 recipes (18 until the first forge opens the set armor, §25.0.7), down from 69; a fresh Alchemist 7 (was 9).
- **There is always a way (§3.3.1) holds:** whatever its tier, every counter consumable `ElementCounters.CountersFor`
  names for any campaign stage up to the next uncleared one is on the shelf (`VendorTiers.NeededCounter`). The flasks are
  stock tier 2, the hazard tools tier 1.
- **Signposts:** the foot of the Merchant's Buy list reads **"More stock after The Tithe Moors III · +38 items"**
  (`shop-next`). A station's list opens with its next rank in crafts ("Journeyman at 150 XP: 40/150, about 8 more forges
  or repairs", `rank-next`) and ends with **Coming later** (`recipes-later`): up to `PreviewRows` = 3 recipes of the next
  rank ("🔒 Journeyman") and 3 of the next progress tier ("🔒 The Tithe Moors III", rows `recipe-locked-<id>`), each with
  "+N more after …". A locked row's detail shows the result, why it is locked ("Opens after The Tithe Moors III. Needs a
  Journeyman alchemist (you are Apprentice, 0 XP)") and its inputs.
- **Nothing goes away:** an item once bought stays on the shelf (`SaveData.ShopBought`), and a recipe once made, learned
  from a scroll or discovered stays in its list (`SaveData.CraftedRecipes`, `DiscoveredRecipes`). Test Mode and New
  Game+ see every tier.
- **Existing saves** (save version 13): tiers are read live from `HighestClearedStage` and station XP, so a save gets
  every tier its progress and rank already reach. A save from before also has every vendor item it owns marked bought and
  every Forge or Alchemy recipe whose result it owns, carried or worn, marked made (`VendorTiers.Grandfather`).
- **Guided flows:** the starter weapons, the Wayfarer and Militia set pieces (the first armor) and the bottle brews of
  Healing Potion, Mana Potion and Antidote sit in tier 1 at Apprentice; `Forging.First`, `Forging.FirstArmor` and
  `Brewing.FirstBrew` pick only listed recipes, and the Healing Potion the Merchant's tip points at is on the first shelf.
  The camp routine buys, brews and forges only what a player could see.
- **Audit:** `python ../GridGame.Studio/tools/vendor_tiers.py` (add `--list` for every row) reads the tuning from `VendorTiers.cs`, prints
  each vendor's counts per tier and rank, and fails on an orphan: a Merchant tier past the last, a recipe whose input no
  tier reaches, a vendor item or Forge/Alchemy result never obtainable, or a guided flow's recipe outside tier 1. Today
  it reports nothing orphaned. *(Tests: `VendorTierTests`, `AlwaysAWayTests.Every_counter_a_reachable_stage_needs_is_on_the_shelf_whatever_its_stock_tier`;
  harness `vendortiers`: the fresh shelf, the next-stock row, and the locked rows and their gate text at the Forge and
  the Alchemist, recorded.)*

### 25.0.11 Where the main campaign's gold goes (GG-T540)

Target: across the main campaign (through campaign.json's Ending stage, cathedral-03; the postgame is not covered),
repairs take roughly **20–35%** of the gold spent at camp. The rest goes to gear, consumables, crafting, inn rests and a
meaningful but optional share for facilities and summons. A piece repaired over and over still becomes uneconomical
(repair cost ≥ BaseCost, §24.5) and is salvaged instead.

- **Measured by** `python tools/polish.py --balance` (add `--no-player --economy-only` for just this part, about 5
  minutes). It runs `BalanceReport.Economy_ledger`: greedy and casual bots managing their camp with `CampRoutine` from the
  trio start, with story recruits, 4 seeds each. It prints battle pay, chest loot, each camp phase's share of everything
  spent, and repair, salvage and break counts, per run and per region, and scores **POL-G8** (repairs 20–35% of
  main-campaign spend).
- **Before (GG-T540 baseline, 2026-11-21):** greedy: 12,088 g earned a run, 13,954 g spent: **repair 68%**, gear 24%,
  counters 3%, skills 2%, inn 2%, potions 1%, facilities 0%. Casual: 10,746 g earned, 12,717 g spent: **repair 70%**,
  gear 20%, inn 4%, skills 3%, counters 2%, potions 1%. From the Anchor Wastes on, repairs took 83–95% of each region's
  spend. Each run made about 450 repairs over 24–34 battles, close to 19 per camp visit. (GG-T448's 97–98% included the
  postgame and predates the vendor tiers.)
- **Why:** the routine topped every worn piece back up to full on every visit, however small the scratch. Each repair
  multiplies that piece's next repair price by 1.6 and lowers its ceiling by one, so scratch repairs after every battle
  pushed the multiplier into double digits within a region. Halving `RepairRate` (GG-T448) did not help, because the
  routine simply made more repairs before reaching the uneconomical point.
- **Change:** `CampRoutine.RepairBelowFraction` = 0.6. The routine repairs a piece only once it is below 60% of its ceiling.
  A repair costs the same per point restored as before, but happens far less often. `RepairRate`, the 1.6× growth, wear
  rates, battle pay, Paywall and Diamond prices are unchanged.
- **After (2026-11-21):** greedy: 12,068 g earned, 12,540 g spent: **repair 28%**, gear 53%, facilities 9%, counters
  4%, inn 3%, skills 2%, potions 1% (52 repairs a run). Casual: 11,104 g earned, 11,700 g spent: **repair 19%**, gear
  45%, inn 18%, facilities 9%, counters 4%, skills 3%, potions 1% (88 repairs). The casual bot now finishes 4/4 runs
  (was 3/4). The highest regional repair share is the greedy bot's 46–53% in the Still and Halcyon. Breaks mid-battle
  were unchanged for the greedy bot (2 a run) and rose for the casual bot (12 → 18, mostly in the Arkgrave).
- **Side effects:** Blacksmith XP from repairs falls with the repair count, so rank now comes mostly from forging, as
  §25.0.10 intends. Summons never come up in these rows: the story fills the party first. A player who repairs every
  scratch from the Repair tab still pays the 1.6× growth on each one. ~~The Repair tab's uneconomical warning (§25.2) is
  what guards against that.~~ **Amended 2026-11-21 (GG-T544):** the Repair tab now follows the same rule: its Repair all
  takes only pieces below 60%, pieces above it say "Cheaper to wait", and each worn row shows how its price grows (§25.2).

### 25.1 Merchant

`ShopScreen` / `Meta.Shop`. Buy / Sell / Bargain tabs; Buy and Sell use separate carts with rows showing name (rarity color), unit price, owned count and a − n + stepper; the footer shows "Pay" (red if unaffordable) or "Receive" and commits the whole cart.
- **Buy stock:** every `SoldByVendor` item, sorted by type, price, name; stepper capped at `MaxStack − owned`. **Amended 2026-11-21 (GG-T538):** only the stock tiers this save's progress has opened, plus anything it bought before and the counters a reachable stage needs; the list ends with the next tier's signpost (§25.0.10). **Amended 2026-11-22 (GG-T520):** the Buy list is grouped under section headers, **Weapons, Armor, Jewelry, Consumables, Materials** (then Other), in that order, each in price order (`Shop.Sections`, `Shop.SectionOf`). Under every gear row a note names the party heroes who can equip it today (shape and proficiency, `Equipment.Fits`) and marks it an upgrade, in green, for each one whose current piece in that slot (the weaker of a ring or earring pair; empty scores 0) has a lower gear score (`MetaHeuristics.Score`, the camp routine's measure): "Upgrade for Warden · also fits Ranger", "Fits everyone, not an upgrade", or "No one in the party can use it yet" (`Shop.GearFit`, `Shop.GearFitLine`). The stock rule itself is GG-T538's: only open tiers, so no material from a region not yet reached.
- **Sell:** every owned non-quest item with a cost, at `max(1, floor(BaseCost × 0.5))`.
- **Potion economy (amended 2026-10-15, GG-T44):** every healing/cure consumable (Healing Potion, Hi-Potion, X-Potion, Revival Charm, Antidote, Eye Drops, Remedy, Tent) is now `SoldByVendor`, not just Healing Potion — the Merchant is the convenient, always-available path. Each one's Alchemy brew (§25.3) costs **2.5–5×** less gold per unit, because its reagents come from field drops instead of a gold price; gathering and brewing is the efficient path, buying stays the convenient one. *(Test: `Every_sold_healing_or_cure_potion_is_meaningfully_cheaper_brewed_than_bought`.)*
- **Fresh baseline gear (amended 2026-10-19, GG-T84):** the 16 unenhanced **Common** weapons, armor and relics (Rusty Sword, Bronze Dagger, Iron Sword, Hunter's Bow; Padded Vest, Leather Armor, Chain Mail, Iron Helm, Leather Boots, Iron Sabatons, Leather Leggings, Leather Gloves; Iron Talisman, Bone Amulet, Copper Ring, Copper Earring) are now `SoldByVendor`, bought new at full `BaseCost` and full durability -- before this, 0 of 155 equipment defs were sold and the worn Bargain shelf was the only gear buy path, so "buy replacements" at camp (owner, 2026-10-19) was impossible. Uncommon-and-up and every enhanced (+N) tier stay drop/forge-only. Where the Forge can make a sold piece, its gold cost stays under 60% of the buy price (materials come from drops), mirroring the potion rule: forging is the efficient path, buying the convenient one. *(Test: `The_merchant_sells_fresh_baseline_gear_and_only_baseline_gear`.)*
- **Bargain (added 2026-10-15, owner direction, US-246, GG-T38):** a rotating "Used / Bargain" shelf of `Meta.Bargains.ShelfSize` = 4 worn weapons/armor, picked from every equipment `ItemDef` with `Durability > 0` and `Slot` Weapon or Armor, at **35–60%** of `BaseCost` but only **10–40%** of `Durability` left — cheap power now, a real risk of shattering mid-combat before it can be repaired. Rolled deterministically from `SaveData.BargainRotation` (so reopening the Merchant never rerolls it, and the same save always replays the same shelf) and turns over once per finished battle, win or lose (`Bargains.Rotate`, called from `BattleFactory.Commit`). Each row shows a durability gauge before purchase; buying files the rolled durability straight into `SaveData.Worn` (§24.5) so equipping it carries that wear, not factory-fresh. A piece already owned (every weapon/armor is `MaxStack` 1) can't be bought again until it leaves the bag. *(Tests: `Tests/Core.Tests/BargainShelfTests.cs`.)*
- **Always a way (added 2026-11-08, owner, GG-T371):** the seven elemental flasks, Purifying Salts and the Disarm Kit are `SoldByVendor` from the start ([§3.3.1](#331-there-is-always-a-way-gg-t371-added-2026-11-08)), so a party never needs a build it lacks to answer a foe or a hazard.

### 25.2 Blacksmith

`BlacksmithScreen`: **Forge**, **Upgrade**, **Salvage**, **Repair** tabs (only Repair is open until the workshop opens, §25.0.4; the opened Forge starts with the starter weapons of §25.0.6, and its armor-set recipes open after the first weapon, §25.0.7) ([§24.5](#245-weapon-durability), [§24.6](#246-crafting-recipes)). The header shows the Blacksmith's facility ("Iron Forge", [§25.12.2](#25122-facility-upgrades-gate-material-tiers-gg-t432)), station level and XP to its next tier ([§24.6.1](#2461-tier-ladder-gg-t35-amended-2026-10-15)); a tier-locked recipe shows a 🔒 and the level required instead of its price, and a recipe above the facility shows "🔒 Iron Forge" (its detail names the facility and offers **Build Iron Forge · 400 G**; the Forge and Upgrade lists open with a **Build <next facility>** row) — GG-T432; an undiscovered recipe ([§24.6.2](#2462-recipe-discovery-gg-t36-amended-2026-10-15)) one input short shows as `???` instead, everything else undiscovered stays hidden. The recipe detail shows ✔/✘ per input with owned counts. Repair lists every roster hero's durable weapon/armor ("Paladin — Iron Sword 3/9", cost or "OK"), warning when uneconomical. **Amended 2026-10-15 (GG-T42):** when a repair is flagged uneconomical, an indented "Salvage instead" row appears directly beneath it showing the scrap yield for that piece's current durability, so the repair cost and the salvage yield sit side by side — confirming scraps the piece (`Crafting.SalvageEquipped`) instead of repairing it. **Amended 2026-10-15 (GG-T41):** the Salvage tab also lists any `Broken <Name>` fragments alongside spare bag gear, dimmed, salvaging them via `Crafting.SalvageBroken`; the Items screen's All/Gear tabs list them too (also dimmed), with a detail note that they can't be equipped or repaired.
- **Repair tab: Repair all and price hints (added 2026-11-21, owner, GG-T544):** the player gets the camp routine's
  repair rule (§25.0.11), one constant for both (`CampRoutine.RepairBelowFraction` = 0.6, tested by `Repair.Due`).
  - **Repair all** (the detail pane's primary button, `smith-repair-all`) mends only pieces worn below 60% of their
    ceiling, most worn first, as far as the purse goes (`Repair.RepairAll`). Its label counts and prices only those
    pieces ("Repair all (3) · 412 G"). With none below the threshold it reads **"Nothing needs repair yet"** and is
    disabled. Any row can still be repaired by hand at any wear.
  - **Under every worn row**, a note shows how the price grows: what 10 points of wear cost to repair now, and what
    they will cost after this repair ("6 G per 10 wear, 10 G after this repair", `Repair.WearCost`, the 1.6× growth of
    §24.5). A piece still at or above 60% leads with **"Cheaper to wait (82%)"**.
  - The tab's paragraph says why: each repair lowers the ceiling by 1 and makes the next one cost 1.6× as much, so mend
    gear once it is below 60%, not after every scratch.

### 25.3 Alchemist

`AlchemistScreen`: **Brew** (Alchemy recipes, including the Empty Bottle recipes of §25.0.5). ~~**Enchant** (Enchant recipes), **Heal** (same service as the Inn).~~ **Amended 2026-10-19 (GG-T119):** enchanting moved to the Enchantress's own station (§25.3.4) and the Heal tab is gone — it was the Inn under another name, and resting is now the Innkeeper's (§25.0.1). The header shows the Alchemist's facility ("Glass Alembic", [§25.12.2](#25122-facility-upgrades-gate-material-tiers-gg-t432)), station level and XP to its next tier ([§24.6.1](#2461-tier-ladder-gg-t35-amended-2026-10-15)); a tier-locked recipe shows a 🔒 and the level required instead of its price, and one above the facility "🔒 Glass Alembic" with a Build button, as at the Blacksmith (GG-T432); an undiscovered recipe shows as `???` or stays hidden, same convention as the Blacksmith ([§24.6.2](#2462-recipe-discovery-gg-t36-amended-2026-10-15)).

### 25.3.1 Inn

**Amended 2026-10-09 (new):** `Meta.Inn` — the Camp's rest service (hub "Inn" row; ~~and the Alchemist Heal tab~~ — removed 2026-10-19, GG-T119: the Inn is the Innkeeper's, shown once she joins, §25.0.1). Cost = `ceil(total missing HP × 0.5 G)` across the **whole roster** (reserves included); resting sets every hero to full. "Everyone is healthy" when nothing is missing.

### 25.3.2 Using items at Camp

**Amended 2026-10-09 (new):** `Meta.Camp.Use` — from the **Items** screen's detail pane, a healing consumable (not Revival Charm) can be used on a party hero (one button per hero), and a Tent (`OutOfBattleOnly`) has "Use on party", resting the whole roster. *(Test: `Camp_potion_heals_a_wounded_hero_and_tent_heals_everyone`.)*

### 25.3.3 Camp routine (auto-manage)

`Meta.CampRoutine.Run` performs a sensible Camp visit through the normal services: repair worn gear, rest if affordable, forge and equip better weapon/armor (gear score = sum of stats + ½ attack), learn one affordable spell per hero and fill empty unlocked slots, keep ≥ 6 Healing Potions. **Amended 2026-10-15 (GG-T44):** restocking now brews the Healing Potion recipe (cheap, from field-drop reagents) before falling back to buying at the Merchant once the reagents run out. Used by `BalanceReport`. 🟡 No in-game entry point yet. *(Test: `CampRoutine_brews_potions_instead_of_buying_when_it_has_the_reagents`.)*

**Amended 2026-10-15 (GG-T54):** before repairing, every `Broken` fragment is scrapped on sight (it can't be equipped or repaired anyway, §24.6.2's salvage-only fate), and a weapon/armor whose repair has become uneconomical (§24.5, cost ≥ `BaseCost`) is scrapped via `Crafting.SalvageEquipped` instead of repaired — the same trade the "Salvage instead" row offers a player. The gear-upgrade pass now also shops the Merchant's Bargain shelf (§25.1) for an affordable slot upgrade once owned gear and Forge recipes come up short, but skips any offer below `CampRoutine.MinSafeBargainDurabilityFraction` (25%) — the shelf's 10-40% durability gamble is a deliberate player choice, and an automated buyer can't weigh that risk the way a player can. Spending skill points (`HeroSave.SkillPoints`, GG-T49) is deliberately **not** part of this routine yet: there is still no skill tree to spend them into until GG-T50 lands (§25.6's amendment), so they're left to bank exactly as Learn already does. *(Tests: `CampRoutine_salvages_broken_fragments_on_every_visit`, `CampRoutine_salvages_an_equipped_piece_instead_of_an_uneconomical_repair`, `CampRoutine_never_buys_a_bargain_offer_too_close_to_shattering`, `CampRoutine_buys_a_safe_bargain_offer_for_an_empty_slot`.)*

**Amended 2026-10-19 (GG-T87):** the routine now works the two Camp services it used to skip. **Bounty Board** (§22.4): a finished contract is claimed; an active one the uncleared campaign stages can no longer finish is abandoned; with none active it accepts the posted contract the stages ahead would complete soonest (cheapest reward breaks ties) — a playthrough meets each stage once and this player doesn't grind replays, so a contract whose kill count outnumbers every remaining spawn of its target is never taken. (Content note: Slime Culling wants 5 `slime00` kills but the whole campaign spawns only 3, so it is completable only by replaying stages; Wolf Pack's 3 kills need all three `wolf00` spawns, the last at stage 15.) **Summon Circle** (§25.10): when a party slot is open and `Summon.Price` is affordable, it recruits the first pool class not yet in the roster, before the gear pass so the newcomer is equipped the same visit; a full party never spends on a reserve recruit. Every real new game starts with a full five, so `BalanceReport.Campaign_pacing_table` gained a `greedy+camp trio-start` row (three starters) to put recruiting in the loop; the table header now reports recruits and bounties claimed, and the `+camp` rows assert a bounty is claimed whenever a run finishes (and the trio row that someone is recruited). Greedy+camp and trio-start still finish 4/4. **Amended 2026-10-19 (GG-T118):** every new game now starts with three ([§25.17](#2517-small-start-story-recruits-gg-t118)), so the default rows are trio starts that gain story recruits; the old trio-start row became `greedy+camp trio no-story` (it still asserts a Summon Circle recruit) and a `greedy five-start (pre-GG-T118)` row keeps the old start as the yardstick. **Amended 2026-11-19 (GG-T431):** the Bounty Board half is retired: bounties are now hunts for marked targets (§22.4) and the routine skips the board entirely; `Campaign_pacing_table` asserts no bounty is claimed. *(Tests: `CampRoutine_recruits_into_an_open_party_slot_when_affordable`, `CampRoutine_does_not_recruit_with_a_full_party`, `BountyHuntTests.The_camp_routine_never_works_the_board`, `Campaign_pacing_table`.)*

**Amended 2026-10-19 (GG-T119):** the routine uses only the services whose Camp NPC has joined (§25.0.1) — no Blacksmith: no salvage, repair or forging; no Innkeeper: no rest; no Merchant: no Bargains or potion buying; no Alchemist: no brewing. Equipping owned gear, learning and recruiting are always available (bounties are never worked, GG-T431). *(Tests: `Camp_routine_uses_no_service_whose_npc_has_not_joined`, `Camp_routine_uses_each_service_once_its_npc_joins`.)*

**Amended 2026-11-08 (GG-T371):** with the Merchant joined, the routine buys the next campaign stage's counters (§3.3.1) last: one of each while it costs a quarter of the gold or less, slotted into a dead bar slot. *(Tests: `Camp_routine_buys_the_counter_the_next_stage_needs`, `Camp_routine_buys_no_counter_without_the_merchant`.)*

**Amended 2026-11-19 (GG-T432):** the routine forges and brews only recipes its facilities allow (`Crafting.CanMake`), and at the very end of a visit builds the next facility level (§25.12.2) of each joined station — the Forge once the workshop is open, the still once the Alchemist has joined — when it costs half the purse or less (`CampRoutine.FacilityPurseShare`), so a bot climbs the material tiers instead of stalling below them. *(Tests: `Camp_routine_builds_the_next_facility_when_it_costs_half_the_purse_or_less`, `A_station_without_its_keeper_is_never_built_by_the_bot`.)*

### 25.3.4 Enchantress (GG-T119)

`EnchantressScreen` (hub row **Enchantress**, once she joins, §25.0.1): the Enchant recipes that used to be the Alchemist's second tab, with the same recipe list and detail. She has no recipe ladder of her own to climb (a ladder that only enchanting fed could never leave Apprentice), so her recipes still unlock with the **Alchemist's** station level (§24.6.1) and enchant crafts still feed it; her header reads "Unlocks with the Alchemist: …". **Amended 2026-10-27 (GG-T179):** her station has two tabs — **Armor** (first; the armor enchants of §25.0.8) and **Weapons** (the elemental Enchant recipes above). **Amended 2026-11-17 (GG-T215):** every weapon enchant used to be Master tier and a flat 150 G regardless of the weapon (RELEASE_SCOPE watch-list: 600 recipes, no price spread, nothing enchantable before the Alchemist was maxed); each of the 600 (100 base weapons × 6 elements) is now tiered and priced off the base weapon's own `Rarity` — Common/Uncommon Apprentice at 60/90 G, Rare Journeyman at 130 G, Epic/Legendary Master at 180/250 G — so a starter weapon enchants from the first launch and price climbs with the weapon being enchanted, not just a flat tax.

### 25.4 Equip

**Amended 2026-11-19 (GG-T435):** now the **Equip** tab of the Hero screen (§25.0); the roster strip replaces the ‹ portrait › switcher. `EquipScreen` opens the Hero screen on it.

`EquipScreen`: hero switcher (‹ portrait ›), the five slots, a stat-delta grid (STR VIT AGI SPD INT WIS LCK, current → preview), and the bag's candidates for the selected slot (tap to preview, tap again to equip; "Remove" unequips). For an armor slot the compare panel also shows armor set progress for the current and previewed piece (GG-T182, [§24.2.3](#2423-armor-sets-gg-t182)).

### 25.5 Party

**Amended 2026-11-19 (GG-T435):** now the **Party** tab of the Hero screen (§25.0), drawn without the roster strip. `PartyScreen` opens the Hero screen on it (the Crucible's **Change team**).

`PartyScreen`: every roster hero as a card (thumb, name, level, MaxHP, Active/Reserve, class card text); tap toggles ([§23.4](#234-roster--party-composition)).

### 25.6 Abilities

**Amended 2026-11-19 (GG-T435):** now the **Abilities** tab of the Hero screen (§25.0), with a sixth sub-tab, **Talents** (§25.6.3). The Status sheet below (GG-T48) is likewise the Hero screen's **Status** tab; `AbilitiesScreen` and `StatusScreen` open the Hero screen on their tab.

**Amended 2026-11-16 (GG-T388):** a **Locked** tab lists every kit or tree ability a hero doesn't know yet and where it comes from: its trainer price, its skill-tree node and points, or the Tome that teaches it and where that drops.

`AbilitiesScreen` / `Meta.Abilities`: hero switcher, the 5-cell battle bar (locked cells shown), and three tabs:
- **Known** — the hero's learned abilities (name, kind/cast time, cost pips, description); tap to put it in the selected slot.
- **Items** — owned consumables; tap to put `"item:<id>"` in the slot.
- **Learn** — **Amended 2026-10-09 (owner decision):** buy new abilities with gold. Learnable = the class's own `Abilities` list plus **every hero spell in the general catalog**, minus what is known (enemy charges, `enrage` and passives excluded). Price: **spells `BattleConfig.SpellTrainBasePrice` (120) + `SpellTrainPricePerOrb` (80) per orb of cost; skills `SkillTrainBasePrice` (150) + `SkillTrainPricePerCooldown` (50) per `CooldownTurns`** (`Abilities.Price`). **Amended 2026-11-17 (GG-T215):** skills used to be a flat 300G regardless of what they did (RELEASE_SCOPE watch-list); cooldown is the power proxy skills have in place of a spell's orb cost, so a 1-turn jab now trains for 200G and a 6-turn revive/ultimate for 450G — the weighted average across the 146 skills (mode `CooldownTurns` 3, pricing 300G) is unchanged from the old flat price, so this is a spread, not a net economy shift. Confirm dialog, then the ability is Known.

`Abilities.Assign` accepts only Known abilities or consumables, refuses locked slots, and moves an entry already on the bar to the new slot (no duplicates via assignment). "Clear slot" empties it.

**Amended 2026-10-15 (GG-T49, owner decision):** heroes now also earn skill points on level-up ([§15.1](#151-data-model)) to spend on skill-tree nodes. Decision: once the tree exists, its nodes **replace gold-bought Learn for a hero's own class/off-class abilities and passives** — gold Learn above stays only for the **general spell catalog**. **GG-T50 (2026-10-15) landed the tree's content** (§25.6.1) — the Learn screen is deliberately left unchanged for now (still gold-for-class-abilities), and so is `BattleRunner`'s enemy-kit passive gating (§14.4): a tree-granted passive sits in a hero's `Known` list but has no in-battle effect yet. Spending points against a tree, a Skills tab on `AbilitiesScreen`, wiring passive effects to heroes, and the Learn-screen migration itself are follow-up work; `StatusScreen` continues to show the running skill-point balance (`HeroSave.SkillPoints`) so the currency is visible before there's anywhere to spend it.

**Amended 2026-10-21 (GG-T48, owner direction — "Hero character sheet"):** `StatusScreen` is the per-hero **Build screen** the ticket asked for. On top of its existing portrait/HP/level/XP/gear-with-durability, it now shows:
- **Base vs. gear stats** — each stat as Base (`Unit.LevelStats`) / Gear (the delta to `Unit.Effective`, colored like an Equip-screen preview) / Total, instead of only the effective sum.
- **Known Abilities** — every id in `HeroSave.Known`, name + kind, in a scrollable list (the hero's full kit can run past default-loadout size once Learn has bought a few).
- **Skill Trees** — `Progression.TreeInvestment(c, h)`: for each tree, the sum of every node's `Cost` whose `Grants` the hero already has Known, highest first. This is a read-only reading of the tree map against what the hero already knows (its default loadout plus anything gold-Learned) — **not** a spend ledger, since spending points against a tree is still GG-T49/50's deferred follow-up (previous paragraph); a hero with nothing matching a tree's nodes just shows none.
- **Proficiencies** — `Progression.KnownProficiencies`: any of the three proficiency-unlock passives (`heavy_armor_proficiency`, `martial_weapons_proficiency`, `arcane_focus_proficiency`, staged by GG-T74 for §25.6.1) already in `Known`. **Amended 2026-10-21 (GG-T52):** these now actually gate gear ([§24.2.1](#2421-proficiency-gating-gg-t52)) and are real nodes in the Mage/Cleric/Alchemist trees — still shows "None yet" for a fresh hero today, since spending a tree point on any node (these included) remains the deferred follow-up §25.6 already notes; a hero picks one up via the migration grandfather clause until then.
- **Build summary** — `Progression.BuildSummary`: `"{Class} · {tree A} {pts}[ / {tree B} {pts}…] — {role}[ who {cross-tree verb}]"` (e.g. "Paladin · Guardian 7 — a tank"), reading only off the class, its tree investment and its `Role` — a cross-tree verb ("…who heals") only appears once a non-home tree's matched nodes reach 3 points, so the line never claims a build the hero's Known list doesn't back up.

*(Tests: `BuildScreenTests` — tree investment sums only nodes whose grant is Known, a cross-tree ability counts toward its own tree and not the home tree, an empty Known list yields no tree investment, proficiencies report only what's actually known, the build summary names class/trees/role and gains its cross-tree tail only at the 3-point threshold, every real hero class produces a summary without throwing.)*

### 25.6.1 Skill trees (content only, GG-T50)

`Assets/GridGame/Resources/Content/skilltrees.json` → `ContentDb.SkillTrees` (`SkillTreeDef`/`SkillNodeDef`, `Core/Content/ContentModels.cs`): one tree per hero archetype, named by a `ClassDef.HomeTree` id every hero class must set (`ContentLoader.Validate`, same pattern as §25.13's `Role` check). A tree is a small DAG of nodes:
- `Grants` — the ability or passive id the node would unlock (must resolve in `ContentDb.Abilities`). **Amended 2026-11-06 (GG-T296):** a granted Passive or Reactive (Last Bastion's Counter Attack, Lifesteal, Double Attack, Tough Hide, Regenerate) fights with the hero. `BattleFactory.SpecFor` copies every known Passive/Reactive into `HeroSpec.Passives`; it had dropped them, so these nodes did nothing in battle.
- `Cost` — skill points to spend on it.
- `RequiredPointsInTree` — total points already spent anywhere in the tree before it can unlock.
- `Prerequisites` — sibling node ids (same tree) that must already be unlocked.

`ContentLoader.Validate` resolves every grant and prerequisite, rejects duplicate node ids within a tree and prerequisite cycles, and requires every `Team.Hero` class to name a tree that exists. Six trees ship today, one per `Role` (§25.13): **Guardian** (Tank), **Warrior** (Striker), **Ranger** (Ranged), **Mage** (Mage), **Cleric** (Healer), **Alchemist** (Support) — a deliberate simplification of the Guardian/Warrior/Rogue/Ranger/Mage/Cleric/Alchemist archetype list down to one tree per already-`Role`-validated archetype, leaving a Rogue split out of Warrior for a later pass once rogue-specific content exists. *(That split landed 2026-11-02, GG-T81: seven trees now, Warrior and Rogue both serving `Striker`; see the GG-T81 amendment below.)*

**Amended 2026-10-16 (GG-T74):** each tree deepened from a flat 5-node list to **2 shared nucleus nodes + 3 variant branches (Opener → Core → Branch Capstone) + 1 cross-branch Finale** — **72 nodes total**, every tree 12 deep. The 3 branches per tree are the bible's own §25.13 sub-archetype clusters, now actually spendable instead of only shaping a class's starting kit:

**Amended 2026-10-21 (GG-T52):** Mage, Cleric and Alchemist each picked up a 13th node (`RequiredPointsInTree: 8`, no prerequisite) granting one of the three proficiency-unlock passives ([§24.2.1](#2421-proficiency-gating-gg-t52)) — Mage → Heavy Armor Proficiency, Cleric → Martial Weapons, Alchemist → Arcane Focus. Guardian/Warrior/Ranger don't need one: their classes already start with Martial weapons (and Guardian with Heavy armor) by `ClassDef` default.

**Amended 2026-11-02 (GG-T77, amends GG-T52): interleaved attribute-bonus nodes.** `SkillNodeDef` gained an optional `GrantsStat` (`StatGrantDef`: `Stat` — a real `Stats` field name, `Strength`/`Vitality`/`Agility`/`Speed`/`Stamina`/`Intelligence`/`Wisdom`/`Luck`, never D&D shorthand — plus a flat `Amount`). A node grants **either** an ability (`Grants`) **or** a stat bonus (`GrantsStat`), never both and never neither; `ContentLoader.Validate` rejects both/neither, an unknown stat name and a non-positive amount. Every branch now interleaves 2 stat nodes (1 point each): `<branch>_stat1` (+3 of the tree's primary stat; needs the Opener and 2 points in the tree) and `<branch>_stat2` (+2 of its secondary; needs the Core and 5 points). Primary/secondary per tree: **Guardian** Vitality/Stamina, **Warrior** Strength/Speed, **Ranger** Agility/Luck, **Mage** Intelligence/Wisdom, **Cleric** Wisdom/Vitality, **Alchemist** Luck/Stamina. 20 branches (Guardian's Deathguard and Mage's Arcane Warden make four each) → 40 new nodes, 121 total. `StatGrantDef.ToStats()` gives the bonus as a `Stats` block for the point-spending/stat pipeline to add; until that pipeline reads it, a bought stat node is content only. *(Tests: `SkillTreeTests.Every_branch_interleaves_its_trees_primary_and_secondary_stat_nodes`, `Validate_rejects_a_bad_stat_node`.)*

| Tree | Branch 1 | Branch 2 | Branch 3 |
|---|---|---|---|
| Guardian (Tank) | Aegis Guard — mitigation + cleanse | Juggernaut — knockback aggression | Warlord — party buffs |
| Warrior (Striker) | Berserker — rage/bloodlust | Duelist — precision/counter | Brawler — guard break/sweep · Attrition — drain/sustain (4 branches, GG-T81) |
| Rogue (Striker, GG-T81) | Shadowstep — mobility/vanish | Skirmisher — hit-and-run | Combo Brawler — chained strikes · Opportunist — feed on ailments (4 branches) |
| Ranger (Ranged) | Marksman — crit/precision | Trapper — snares/control | Skirmisher — mobility + covering fire |
| Mage | Spellblade — melee-infused casting | Field Caster — traps/wards/support | Hexweaver — curse/DoT damage |
| Cleric (Healer) | Aegis Wardens — preemptive wards | Hexmenders — damage-for-heal | Skirmish-Menders — mobile support |
| Alchemist (Support) | Tempo Weavers — haste/slow control | Field Engineers — traps/amplifier synergy | Primal Pack — reagent/consumable mastery |

A branch's Capstone is reachable in 3 points (Opener 1 + Core 2); the tree's Finale needs 10 points in the tree and 2 branch Capstones unlocked (deliberately the same 10-point figure §25.6.2's cross-tree gating uses to open a second tree — finishing two branches of your home tree and being ready to dip into another tree land at roughly the same investment). Ranger had no reusable sub-archetype content (GG-T18), so its 3 branches (9 nodes) are new `AbilityDef`s; every other tree's 9 branch nodes reuse the already-shipped GG-T3/§25.13 cluster kits verbatim (`aegisguard_*`, `juggernaut_*`, `warlord_*`, `berserker_*`, `duelist_probingthrust`/`guardstance`/`decisivecut`, `shadowstep_*`, `spellblade_*`, `fieldcaster_*`, `hexweaver_*`, `aegiswardens_*`, `hexmenders_*`, `skirmishmenders_*`, `tempoweave_*`, `fieldeng_*`, `primalpack_*`). **Amended 2026-11-02 (GG-T74, closing pass):** Mage gained a **4th branch, Arcane Warden** (the ticket's "Barrier spell on Mage's Warden branch") — 3 new bespoke Arcane spells, no class kit reused: Opener `warden_runeward` (*Rune Ward*, 1 orb, single ally `protection`), Core `warden_spellshield` (*Spellshield*, 2 orbs, single ally `protection` + strips debuffs), Capstone `warden_barrier` (*Barrier*, 2 orbs, `AllAllies` `protection`, no heal — the mage's pure-wall counterpart to Cleric's `aegiswardens_sanctuaryrite`, which still heals and stays the node a cross-specced non-healer grinds toward in §25.6.2's worked example). No Mage class starts on the Warden branch (no class kit changed); it's tree-only content, reachable by spending points. Mage's Finale prerequisites are unchanged (Spellblade + Field Caster capstones). Node grants otherwise still reuse existing content, including the six "enemy kit" passives (§14.4) pre-dating this amendment; this pass adds 12 new `AbilityDef`s (9 Ranger branch skills + 3 proficiency-unlock passives staged for §24.2's GG-T52 gating, granted here but not yet wired to any battle effect).

A hero's starting bar is unaffected: `Progression.NewHero` already makes every `DefaultLoadout` entry Known at creation regardless of the tree (§15.1), so "the hero starts in its home tree with its default abilities pre-unlocked" holds without any new linking.

*(Tests: `SkillTreeTests` — every hero class names a real tree, every node's grant/prerequisites resolve, `ContentLoader.Validate` catches an unknown grant, an unknown prerequisite, a duplicate node id, a non-positive cost and a prerequisite cycle; `CatalogTests` resolves every new `AbilityDef` on a live board; full suite 873/873, glyph + asset checks clean.)*

**Amended 2026-10-20 (GG-T80, docs-only class/region audit):** every `Team.Hero` class's `DefaultLoadout` was matched against its own `HomeTree`'s 3 branches above, by `<cluster>_<move>` ability-id prefix (e.g. an `aegisguard_opener` sibling kit uses `aegisguard_*` ids) — how well each tree's 3 chosen branches actually cover the classes that live in that region. Requested to inform where bridge nodes and new classes would be needed on a future unified map (GG-T79's own hex-wedge sketch didn't survive — see §25.6.2's neighbor note below and `docs/USER_STORIES.md` GG-T83 — but this gap data stands on its own regardless of topology). Findings:

- **Guardian — full branch coverage, 2 orphans.** Aegis Guard (`defender`, `knight`, `paladin`), Juggernaut (`myrmidon`, `tank`) and Warlord (`shieldmaiden`, `warchief`) between them cover 7 of Guardian's 9 classes. The other 2 — `darktemplar` and `jadeknight` — both run a `deathguard_*` kit (`deathguard_mana_siphon`, `deathguard_dark_communion`) that matches none of the tree's 3 branches; §25.13's own Tank sub-archetype list already names Deathguard as a 4th cluster, it just never got a branch (follow-up: GG-T82).
- **Warrior — structurally overloaded.** Striker's 31 classes split across **8** real `<cluster>_` kits, but the tree kept only 3 as branches. Per-cluster counts: duelist ×7 (`assassain`, `duelist`, `fencer`, `ronin`, `sellsword`, `swordmaster`, `thief`), shadowstep ×6 (`blackninja`, `blueninja`, `chromaninja`, `greenninja`, `redninja`, `yellowninja`), skirmisher ×5 (`lancer`, `raider`, `speedster`, `streetfighter`, `striker`), berserker ×4 (`barbarian`, `basher`, `bruiser`, `pandagirl`), combobrawler ×4 (`fighter`, `martialartist`, `prizefighter`, `pugilist`), opportunist ×2 (`nighthunter`, `ripper`), brawler ×2 (`odachi`, `slasher`), attrition ×1 (`vampire`). Only Berserker, Duelist and Shadowstep became branches; the other 5 clusters — skirmisher, combobrawler, opportunist, brawler, attrition, **14 classes (45%)** — match no branch in their own home tree (follow-up: GG-T81, carve a Rogue region out of the excess). **Resolved 2026-11-02 (GG-T81):** see the amendment below; all 31 now match.
- **Ranger — full branch coverage (closed 2026-11-02, GG-T18).** Marksman (`hexsniper`), Trapper (`hexwarden`) and Skirmisher (`hexstalker`, new) each have one class walking them; see §25.13's Ranged sub-archetype amendment. (Originally: zero classes on any of the three branches, both classes on the pre-branch `ranged_*` baseline kit — GG-T82's Ranger half.)
- **Mage, Cleric, Alchemist — no gap.** Every class in these 3 regions matches one of its tree's 3 branches: Mage — Spellblade (`harbinger`, `redmage`), Field Caster (`drifter`, `operative`), Hexweaver (`blackwitch`, `ritualist`); Cleric — Aegis Wardens (`alchemist`, `cleric`, `doctor`), Hexmenders (`sage`, `whitewitch`), Skirmish-Menders (`monk`, `sister`, `whiteninja`); Alchemist — Tempo Weavers (`courier`, `dervish`), Field Engineers (`engineer`, `machinist`, `technician`, `tinkerer`), Primal Pack (`bluelion`, `wildchild`).

This audit is read-only — no `ClassDef`, `SkillTreeDef` or other content changed, no new tests (none apply to a docs-only pass). *(Source: `Assets/GridGame/Resources/Content/classes.json` × `skilltrees.json`, matched by script against every `Team.Hero` entry's `HomeTree` and `DefaultLoadout`.)*

**Amended 2026-11-02 (GG-T81, resolves the Warrior half of the GG-T80 audit above): Rogue split out of Warrior.** Warrior's 8 sub-archetype clusters are now divided between two regions by flavor, so every one of the 31 former Warrior classes walks a branch of its own home tree (was 17 of 31):

- **Warrior** keeps the brute-melee clusters: **Berserker** (`barbarian`, `basher`, `bruiser`, `pandagirl`), **Duelist** (`assassain`, `duelist`, `fencer`, `ronin`, `sellsword`, `swordmaster`, `thief`), plus two new branches built from the already-shipped kits, **Brawler** (`brawler_guard_break` → `brawler_wide_cleave` → `brawler_second_wind`; `odachi`, `slasher`) and **Attrition** (`attrition_bloodletting` → `attrition_dreadaura` → `attrition_soulfeast`; `vampire`). 14 classes. Nucleus (`cleave`, `war_cry`) and Finale (Undying Rampage, `lifesteal`, Berserker + Duelist capstones) unchanged.
- **Rogue** (new tree `rogue`, `HomeTree: "rogue"`) takes the mobility-and-combo clusters: **Shadowstep** (moved out of Warrior with its node ids unchanged; the six ninjas), **Skirmisher** (`skirmisher_blindside` → `skirmisher_hitandrun` → `skirmisher_runningriot`; `lancer`, `raider`, `speedster`, `streetfighter`, `striker`), **Combo Brawler** (`combobrawler_openingjab` → `combobrawler_onetwoflurry` → `combobrawler_knockoutcombo`; `fighter`, `martialartist`, `prizefighter`, `pugilist`) and **Opportunist** (`opportunist_exploit` → `opportunist_chain_the_opening` → `opportunist_collect_the_debt`; `nighthunter`, `ripper`). 17 classes. Nucleus: Light Fingers (`steal`) and Feint (`feint`); Finale: Flurry of Blades (`double_attack`, 10 points + Shadowstep and Skirmisher capstones). Rogue's Skirmisher is the melee `skirmisher_blindside` kit, a different cluster from Ranger's ranged Skirmisher branch (`skirmisher_covering_volley`); node ids are unique across all trees (`ContentLoader.Validate` rejects a clash, GG-T213), so Rogue's nodes are `rogue_skirmisher_opener/stat1/core/stat2/capstone`.
- Both trees have **4 branches** (same Opener 1 / Core 2 / Capstone 2 costs and 1/3/6 point gates as every other branch) rather than the original 3: the alternative, 3 + 5, would have left either Warrior or Rogue with a cluster and its classes still off-map. Every node grants an existing ability, so no new `AbilityDef`.
- Every new branch carries GG-T77's two interleaved stat nodes (`<branch>_stat1` +3 primary after the Opener, `<branch>_stat2` +2 secondary after the Core). Rogue's primary/secondary are **Agility/Speed**, so the moved Shadowstep branch's `shadowstep_stat1` is now +3 Agility (was Warrior's +3 Strength); Warrior's new Brawler and Attrition branches use Warrior's Strength/Speed.
- Both regions still serve `Role: "Striker"` (§25.13); no class's `Role` changed, only its `HomeTree`. The Build screen's cross-tree tail reads "…who skirmishes" for Rogue points (`Progression.BuildSummary`).
- **Place on the map:** Rogue sits between **Warrior and Ranger**, its two kin (Warrior shares its melee, Ranger its mobility and its Skirmisher name). `skilltrees.json` lists it between them. On GG-T79's role-kinship ring that order becomes Guardian - Warrior - Rogue - Ranger - Alchemist - Mage - Cleric (a 7-ring: the longest walk is 3 steps and no region has a single opposite), and on the owner's archetype wheel (§25.6.2, option D) Thief already sat between Ranger and Warrior. Which board shape ships is still GG-T83/GG-T168's call; this only fixes Rogue's neighbors.

### 25.6.2 Cross-tree specialization (GG-T51)

**Decided 2026-11-02 (owner, "do" on GG-T51): the home-tree threshold ships.** This replaces the earlier "not decided; nothing here ships" text and the notes on GG-T51, GG-T135 and GG-T167 that called this model undecided or rejected. It is how you get a tank that casts Heal, a rogue that throws Fireball, or a mage in plate. The rules (`Meta.Talents`, tuned in `Meta.TalentConfig`, both in `Assets/GridGame/Meta/Talents.cs`):

- **Opening a second tree, then a third.** A hero can open its first off-tree once it has **10 points in its home tree** (`TalentConfig.OpenTreeAt[0]`) and a second off-tree at **20** (`OpenTreeAt[1]`). The array's length is the cap: two off-trees, three trees in all. "Points in the home tree" is `Talents.PointsIn`, the same sum the node gates use (every unlocked node's `Cost`, starting-kit Known nodes included). `Talents.OffTreeSlots` is how many of those thresholds the hero has met.
- **What opens a tree.** Buying its first node with skill points (`Talents.Opened`: the home tree, or any tree with a bought node in `HeroSave.TreeNodes`). A tree where the hero only *knows* a node's grant (a gold-Learned spell, §25.6) is not opened by that: buying a node there still needs a free slot. A closed tree's nodes are Locked, and the reason reads "Opening a second tree needs 10 pts in Guardian (7 now)" or, once both slots are used, "Already specialized in 2 other trees" (`Talents.WhyClosed`).
- **Off-tree premium.** Every off-tree node costs **+1 point** on top of its `Cost` (`TalentConfig.OffTreePremium`; `Talents.Cost`). The premium is a price, not investment: the tree's points (`PointsIn`) still count the node's own `Cost`, so it does not speed up that tree's gates.
- **Deeper off-tree tiers need more points in that tree.** An off-tree node's `RequiredPointsInTree` is multiplied by **1.5, rounded up** (`TalentConfig.OffTreeGateScale`; `Talents.Required`). Start-tier nodes (0) stay open; a branch Opener (1) needs 2, a stat node (2) needs 3, a Core (3) needs 5, a Capstone (6) needs 9 and a Finale (10) needs 15.
- **Respec** (§25.6.3) refunds what was paid, premiums included (`Talents.PointsSpent`), and since it clears every bought node it closes every off-tree too.
- **The camp routine** (GG-T54, `CampRoutine`) spends off-tree only in a tree the hero is already in (`Talents.HeldTrees`: opened, or holding a Known node) and pays the off-tree price and gates there. It never opens a tree the hero has no foot in; that stays the player's choice.

All three numbers are static fields, not consts, so a balance pass can retune them in one place. The 10-point figure is also the investment §25.6.1's Finale is tuned against, so finishing two home branches and being ready to open a second tree arrive at about the same time.

*(Tests: `TalentTests` — `A_second_tree_opens_only_once_the_home_tree_has_enough_points`, `Off_tree_nodes_cost_the_premium_and_home_nodes_do_not`, `Deeper_off_tree_tiers_need_more_points_in_that_tree`, `A_third_tree_needs_the_next_threshold_and_there_is_no_fourth`, `Respec_refunds_the_off_tree_premium_and_closes_the_off_tree`.)*

#### 25.6.2.1 Topology history (GG-T83)

Before the owner's 2026-11-02 decision above, the topology was left open. Topologies tried and set aside: (A) six trees plus a toll-gated bridge (GG-T51/GG-T76; GG-T51's rules above are this option, now adopted); (B) one hex map of six wedges, cost = path length (GG-T79, which also got its own opposite-pair arithmetic wrong); (C) an octagon of the 8 stats; (D) the owner's own archetype wheel of 8 pure plus 8 hybrid archetypes, the closest so far but "not quite right". The two references are FF12's Zodiac License Boards (pick 2 of 12 boards) and FFX's Sphere Grid (one shared lattice, different start points). The full record, including why each was rejected and the later GG-T166/167/168 attempts (GG-T168, stat-threshold unlocks with no board, was awaiting owner review when GG-T51 was adopted instead), is in [`docs/research-skill-map-topology.md`](research-skill-map-topology.md). The GG-T80 audit findings above don't depend on this decision.

#### 25.6.2.2 Archetype adjacency (GG-T135, added 2026-11-02)

Built on §25.6.2's rules, not beside them: the slot thresholds, the scaled gates and Respec are unchanged; adjacency only decides **which** tree a free slot may open and **how much** the flat premium grows. The board (`Meta.TreeAdjacency`, `Assets/GridGame/Meta/TreeAdjacency.cs`) is GG-T79's role-kinship ring with Rogue between its kin (§25.6.1's Rogue note): **Guardian - Warrior - Rogue - Ranger - Alchemist - Mage - Cleric - (Guardian)**. Seven trees, so the farthest any tree is from another is 3 steps.

- **Distance is from the nearest tree the hero already holds** (the home tree plus every opened off-tree, `Talents.HeldTreeIds`), walking the ring through trees it doesn't hold (`TreeAdjacency.DistanceFrom`, `Talents.Distance`). An opened tree is measured from the *other* trees held, so it doesn't price itself at 0.
- **Premium grows with distance.** An off-tree node costs GG-T51's flat **+1** (`TalentConfig.OffTreePremium`) plus **+1 per step beyond the first** (`TreeAdjacency.PremiumPerExtraStep`; `Talents.Premium`): a neighbour +1, two steps +2. Respec refunds the recomputed price (`Talents.PointsSpent`), which matches what was paid when trees are opened outward from home; opening a farther tree before the nearer stepping stone can refund a point less.
- **Too far means route through.** A tree more than **2 steps** (`TreeAdjacency.MaxOpenDistance`) from every held tree can't be opened directly. Its nodes are Locked and the reason names the stepping stone: "Mage is 3 steps away: open Guardian first" (`TreeAdjacency.WhyTooFar`, checked in `Talents.WhyClosed` after the free-slot check). The tab still shows while a slot is free, so the player can see where the route leads.
- **Worked example (the owner's fireball barbarian in plate):** a Warrior can't open Mage at 10 home points (Warrior - Guardian - Cleric - Mage, 3 steps). It opens Guardian (a neighbour, +1 a node: plate). At 20 home points Mage is now 2 steps from Guardian, so the third tree can be Mage at +2 a node; or it opens Cleric instead (a neighbour of Guardian, +1). GG-T51's two-off-tree cap still holds, so Warrior → Guardian → Cleric → Mage as three off-trees isn't possible.
- **Tuning:** `Ring`, `ExtraEdges` (extra neighbour pairs, empty by default), `MaxOpenDistance` and `PremiumPerExtraStep` are static fields, as in `TalentConfig`. The Talents screen's off-tree note shows the distance ("neighbour" / "2 steps away") and the actual per-node premium.

*(Tests: `TalentTests` — `Every_skill_tree_sits_once_on_the_adjacency_ring`, `Ring_distance_routes_a_warrior_to_mage_through_guardian`, `A_distant_tree_must_be_routed_to_and_costs_more_per_step`. The GG-T51 tests pick the home tree's neighbours first, so they keep testing the flat premium.)*

### 25.6.3 Talents screen and Inn Respec (GG-T53)

**Amended 2026-11-19 (GG-T435):** the Talents screen is folded into Abilities: it is the **Talents** sub-tab of the Hero screen's Abilities tab (§25.0), the same point counter, Respec button, tree tabs, node graph and detail, with the hero picked on the roster strip. Camp's **Talents** row is gone; the **Heroes** row shows the party's unspent points instead. `SkillTreeScreen` opens the Hero screen there (id `talents`).

Skill points (§15.1) are now spent on §25.6.1's tree nodes. `Meta.Talents` (`Assets/GridGame/Meta/Talents.cs`) owns the rules; `HeroSave.TreeNodes` records each node bought with points as `"treeId/nodeId"` (an older save has none, so no migration step is needed).
- **Node states** — **Known** (green): the grant is already in `Known` (starting kit or gold-Learned). It counts toward the tree's points with no point spent, which is how GG-T50's "starts in its home tree with its default abilities pre-unlocked" works. **Learned** (gold): bought with points. **Available** (blue): every prerequisite node is unlocked and the tree's unlocked-node cost total (`Talents.PointsIn`, the same sum as the Build screen's `Progression.TreeInvestment`) has reached `RequiredPointsInTree`. **Locked** (dim): otherwise, and the detail panel says what is missing.
- **Spending** — `Talents.Unlock` takes `Cost` points from `HeroSave.SkillPoints`, records the node and adds its grant to `Known`. It is not auto-slotted on the bar: the Abilities screen does that. Gold Learn (§25.6) is unchanged; moving class abilities off Learn is still follow-up work.
- **Which trees show** — the home tree first, then any other tree the hero has opened or holds a Known node in, then, while an off-tree slot is free, every other tree (tab marked "(new)"; buying its first node opens it). Off-tree tabs show the premium, tier labels show the scaled points-in-tree gates, and node prices include the +1. The rules are §25.6.2's (GG-T51).
- **Screen** — Camp's **Talents** row (`SkillTreeScreen`, id `talents`; the row shows the party's unspent points and opens on the first hero who has some). It has the hero switcher, a point counter (unspent, plus spent so far), and a tab per tree when there is more than one. The tree is drawn as a node graph: one row per `RequiredPointsInTree` tier, nodes ordered by branch, and an elbow link from each prerequisite to the node it opens, coloured by progress. Tapping a node shows its detail (the tooltip): the granted ability's description, kind, cost and gate. **Learn** asks for confirmation (`Confirm`) before spending. The bought card pops in.
- **Respec at the Inn** — `Talents.Respec` costs `100 + 50 × level` gold. It refunds every point the hero spent on nodes, forgets those nodes' grants and clears them off the battle bar. Known-state abilities (the starting kit and anything gold-Learned) stay. It needs the Innkeeper (§25.0.1) and an open Inn (`Expeditions.InnOpen`), and does nothing when no points have been spent. It's offered from Camp's **Inn: Respec** row (`RespecScreen`, id `respec`: every roster hero with points spent and its price) and from the Respec button on the Talents screen.
- **Respec and gear proficiency (GG-T566, owner: "warn and ask user").** A respec can forget a proficiency passive (Heavy Armor, Martial Weapons, Arcane Focus, §24 GG-T52) that equipped gear needs. `Talents.GearLostOnRespec` lists those equipped pieces; while there are any, `Talents.Respec` refuses unless asked to unequip them (`unequipUnusable`), and then moves them to the bag (wear, repairs, Masterwork and enchant kept, `Equipment.Unequip`). Both respec confirms (`RespecScreen.Ask`) name the pieces and offer **Unequip and respec** / **Cancel**; with nothing at risk it is the plain **Pay** confirm. A hero never keeps gear it can no longer use.

GG-T78's pannable/zoomable canvas is still a separate follow-up; this graph scrolls vertically. *(Tests: `TalentTests`. Harness: `talents` and `respec` are in `tools/harness.py sweep`.)*

### 25.6.4 Learnable-passive rewards: Tomes and Chests (GG-T301)

Owner: heroes get passive/reactive abilities from class kits, skill-tree nodes, gear/enchants, **and quest completion, bounties, campaign progress, random chests and battle-finish loot**. One reward type carries all five: the **Tome**.

- **Tome** (`ItemType.Tome`, `ItemDef.TeachesAbility`): teaches one Passive or Reactive that has a `Trigger` (a battle passive) to a hero the player picks. `Tomes.Teach` uses up one Tome and adds the ability to `HeroSave.Known`; `BattleFactory.SpecFor` then sends it into battle (GG-T296's path). A hero that already knows it, or has it in its class kit, can't read it. `AbilityDef.LearnableBy` (hero class ids and/or Roles) restricts who may learn it; empty = any hero (all ten shipped passives). Ten Tomes ship, one per battle passive: Counter Attack, Thorns, Tough Hide, Regenerate, Poison Ward, Steadfast, Berserk, Mana Siphon (Uncommon/Rare), Lifesteal and Double Attack (Epic).
- **Sources.** Every source grants the Tome as an ordinary item, so the reward tables are plain data and a new signature passive (GG-T302) is one items.json row plus a reference:
  - **Quests** (`RewardItem`): Travel Light → Steadfast, Grown, Not Forged → Mana Siphon, Cold Iron → Counter Attack (§22.11).
  - **Bounties** (`RewardItem`): Grimjaw the Mauler → Berserk, Hollowe the Vat-Giant → Tough Hide (§22.4, GG-T431; were Wolf Pack and Vat-Giants).
  - **Campaign milestones** (`ThemeDef.MilestoneReward`, campaign.json): paid once, the first time a theme's last stage is won (`Milestones.Pay` in `BattleFactory.Commit`, guarded by `SaveData.Milestones`, kept through New Game+). Tithe Moors → Regenerate, Shardfall → Thorns, Arkgrave → Poison Ward, Anchor Wastes → Lifesteal, Halcyon → Double Attack; each postgame theme → a Gilded Chest. The results screen shows "*Theme* cleared! · Milestone: …".
  - **Battle-finish loot** (droptables.json): boss families carry a Tome (Hierarch Lifesteal 35 %, Cyclops Tough Hide, Mecha Armor Steadfast, Swamp Mistress Poison Ward, Demon Lord Berserk 20 %, Mountain Troll Regenerate 10 %) and a 20 % Iron Chest.
  - **Random chests** (`ItemType.Chest`, `ItemDef.OpensTable`): every enemy family's table has a 1.5 % Wooden Chest. A chest is loot like any other, so the difficulty dial's drop multiplier (§22.9, GG-T309) scales it. Opening one (`Chests.Open`) rolls its own droptables.json row (`chest_wooden` / `chest_iron` / `chest_gilded`): each entry rolls its chance, coins roll CoinsMin..CoinsMax, and if nothing rolled it gives the likeliest entry, so a chest is never empty. Rolls are seeded by `SaveData.ChestsOpened`, so a save replays its chests. Wooden: potions, ore, 6 % each of the four defensive Tomes. Iron: potions, Ether, dust, 10–12 % each of six Tomes. Gilded: X-Potion, Revival Charm, 20–25 % each of six Tomes.
  - Dialogue `GrantItem` can grant a Tome too (no beat does yet; the story is on hold).
- **Validation** (`RewardCatalog.Validate`, part of `ContentLoader.Validate`): a Tome needs `TeachesAbility` naming a Passive/Reactive with a Trigger; a Chest needs `OpensTable` naming a non-empty table that holds no chest; only Tomes and Chests may set those fields; every drop entry needs 0 < Chance ≤ 1 and 1 ≤ Min ≤ Max; `LearnableBy` names hero classes or Roles, on Passive/Reactive abilities only; a milestone names a real item. Quests and bounties already check their reward item.
- **Passive slots** (`PassiveLoadout`): class-kit passives are innate, always on and never take a slot. Every other battle passive a hero knows (Tomes and skill-tree grants) is *learned*. At most **2** are on; a 3rd opens at **Lv 10** and a 4th at **Lv 20**. Until the player picks, the first ones learned are on, so old saves and tree grants keep working. Once they switch one (`HeroSave.PassivesChosen`, `HeroSave.ActivePassives`), the list is theirs, and a newly learned passive goes straight on if a slot is free. Meta-only passives (the proficiencies) never count. Respec drops forgotten grants from the list. *Why a cap:* with every Tome on one hero, Lifesteal + Double Attack + Berserk + Counter Attack would make one unkillable build. The cap makes each Tome a choice, and the choice is one switch, which suits a short session.
- **Screens:** the results screen lists a looted Tome with its generated rule sentence (`TriggerText.Describe`, terms tappable through GlossaryText) and a **Teach to…** row of hero buttons (party first; a hero who can't learn it is greyed). A looted Chest has **Open** in place, and its haul is revealed beneath it (Tomes with their own Teach row). **Amended 2026-11-20 (GG-T514):** on a victory the results list puts milestones, unlocks and decisions (expedition progress or completion bonus, a theme milestone, a new stage or ability slot, a joining hero, a tame or wanderer offer, quest and bounty lines) above the loot, and the loot list (under a small "Loot" heading) starts with any Tome or Chest and its choice; plain drops follow, then the stack-full auto-sale line. A first-hour stage drops about 15 rows, which had pushed the Teach row off the bottom. The quest hand-in card and the bounty board (after **Claim**) offer the same Teach row, and a posted contract that pays a Tome shows what it teaches. The Items bag (All and Items tabs) shows a Tome's sentence with Teach to…, and a Chest's **Open**. **Abilities → Passives** (a 4th tab) lists the hero's learned passives with **On/Off** ("Learned passives on: 1/2 · another slot at Lv 10"), the class passives marked Innate, and any Tome in the bag this hero could **Read**. Teaching plays `battle.powerup`; opening a chest plays `battle.pickup`.
- **Bots:** `CampRoutine` opens every chest and reads each Tome to the first party hero with a free slot.
- *(Tests: `PassiveRewardTests`.)*

### 25.6.5 Stat requirements on abilities (GG-T590, from GG-T168)

**Added 2026-11-23.** Every ability the Learn screen sells (a hero-usable Skill or Spell, `Abilities.IsHeroUsable`) carries `AbilityDef.Requires`: a `Stats` vector of minimums, written in abilities.json as only the stats that gate it (`"Requires": { "Strength": 5, "Wisdom": 4 }`). Every non-zero stat must be met; a zero stat doesn't gate; null means no requirement. Passives, reactives, items, enemy charges and the boss-only abilities leave it null.

- **Scale.** Thresholds count the **stat points the hero has spent on level-ups** (GG-T591's one free point a level, `HeroSave.StatPicks`), not its effective stats, so they read on the same 0-up scale for every class. They are whole points from 1 to 10.
- **Authoring.** A pure ability needs one stat, scaled by its power proxy (a spell's orb cost, a skill's `CooldownTurns`): cheap moves 2–3, mid-tier 5–6, capstones 8–10. A hybrid needs two smaller ones, split by flavor (blade-and-spell fangs AGI + INT, holy warding VIT + WIS, tempo spells INT + SPD). GG-T168's worked trail is authored as drawn: Cleave STR 3, Reckless STR 5, Last Man Swinging STR 8, Fireball INT 8.
- **Reading it.** `AbilityDef.HasRequires`, `RequiredStats()` (label, stat, minimum, in the usual STR VIT AGI SPD STA INT WIS LCK order), `RequiresMet(points)` and `UnmetRequires(points)` (what a locked entry says it needs). The bots' stat spending is GG-T593 (below).
- *(Tests: `AbilityRequiresTests`.)*

**Amended 2026-11-23 (GG-T592): the Learn screen gates on it.** An ability becomes purchasable in the gold Learn tab the moment the hero's spent stat points (`StatPoints.Picks`, from `HeroSave.StatPicks`) meet its `Requires`. Until then it is listed after the purchasable ones as a disabled row reading **"Locked · needs INT 8 (have 3)"** (every short stat, `Abilities.Unmet` / `NeedsText`), still readable on a tap. `Abilities.Learn` refuses a locked one ("Locked: needs …"), the Locked tab's trainer line carries the same "needs" note, and the camp routine's `BestLearnable` only considers unlocked abilities.

*Decision: a hero's own kit is exempt* (`Abilities.RequiresWaived`). Its `Progression.KitAbilities` moves (GG-T388: what the class was built to do, sold at the 100 G kit price and re-granted by `TrainTo` as it levels) ignore `Requires`, and so does the guided first skill (GG-T180), which the onboarding chain must always be able to finish. Otherwise a fresh Red Mage would find its own Fireball locked at INT 8 and a hero joining at Lv 1 could not rebuild its own default bar. `Requires` still gates the same ability for every other class, so it keeps meaning "this is how far a stranger has to build toward it".

**Amended 2026-11-23 (GG-T593): the bots build toward it.** The camp routine spends level-up stat points with `Meta.BotStatSpend` (right after the skill-tree spend, before `FillBars`, so an unlock is bought on the same visit). One point at a time, it picks a **target**: of the hero's own class list, the hero-usable abilities it doesn't know and is locked out of (`Abilities.Unmet`, so kit moves and the guided first skill, being waived, are never targets), **on-build first** (every stat it is short on is one the class's own kit asks for, `BotStatSpend.KitAffinity` = the sum of its kit moves' `Requires`: Barbarian STR, Paladin VIT + WIS + a little STR), then the fewest points away, then the pricier, then class-list order. The point goes into the target's largest shortfall (ties: the stat the kit leans on more, then the stronger `Growth`). With nothing locked left on the class list, points fall back to `StatPoints.BotPick`. So a Barbarian puts its first points into STR for Skull Rattle (STR 3) before any off-build detour into Steal or Mug, and a Paladin unlocks Shield Bash, Heal and Hands of Mercy (STR/VIT/WIS) before Silence's INT, instead of stacking STR (its top growth) and never reaching a WIS ability. Each hero that spent logs one camp line, "<Class> put N stat points into <Stat> n, <Stat> n". *(Tests: `BotStatSpendTests`.)*

### 25.7 Shared Camp utilities

`App/UI/Ui.cs` (windows, menu rows, lists, tabs, gauges, cost pips, buttons, gold formatting), `ItemUi` (item rows, glyphs, detail pane), `Confirm` (modal), `GameApp.Toast`.

### 25.8 Per-screen sketches

Every Camp screen follows the same skeleton — header (‹ back, title, gold), body windows, a primary action — and is captured by `tools/harness.py sweep` (`artifacts/sweep/sweep-<screen>.png`), the living replacement for the old ASCII sketches.

### 25.9 The merged hub

✅ Done — this section's old "deliberately not yet" is resolved by §25.0 (**Amended 2026-10-09**).

### 25.10 Summon Circle

`SummonScreen` / `Meta.Summon`: recruit a hero class for gold — deliberate, not a pull. Pool (`summon.json`): GreenNinja, RedNinja, Pugilist, Ronin, Sellsword, Thief, Vampire. ~~Price **250 + 250 × recruits so far**.~~ **Amended 2026-11-05 (owner, GG-T232):** a paywall hook ([§25.18](#2518-rested-and-the-paywall-hooks-gg-t232)) — `Paywall.Cost(Summon)` (flat, ~~**800 g** since GG-T427~~ **1,500 g** since GG-T448) and the hero arrives when a `Paywall.Duration(Summon)` (1 s) summoning job finishes (`Summon.BeginRecruit` → `Paywall.Finish`); bots and tests keep the instant `Summon.Recruit` at the same price. `summon.json`'s `BasePrice`/`PriceStep` are gone. Owned classes show "Joined"; unaffordable rows are disabled. **Amended 2026-10-09:** a recruit **arrives at the roster's average TotalXp** (so it is not stranded at level 1) and **joins the party automatically when there is room**. *(Test: `Full_loop_battle_commit_then_every_vendor`.)*

#### 25.10.1 Mercenaries (added 2026-11-05, owner direction, GG-T233)

Owner: "they stay for a negotiated amount of battles at a price; whether you use them or not; they will reoffer their services when they are about to leave and you can choose to renegotiate or part ways."

- **Content:** `mercenaries.json` (`MercenaryDef`: hero `Class`, `Rate` gold per battle at the shortest term, `Terms` offered, `Temperament` Easygoing/Fair/Greedy, a board `Pitch` and a `Renewal` line), validated (hero class, listed once, positive rate, terms of 2+ battles, no duplicates, both lines). Six classes kept apart from the Summon pool, the guests, the recruitables and the story recruits: Myrmidon (Tank), Prizefighter and Raider (Striker), Ritualist (Mage), Doctor (Healer), Courier (Support).
- **Board:** Camp → **Mercenaries** (opens with the Bounty board, `Mercenaries.Open`) → `MercenaryScreen`. `Mercenaries.Board` shows **3** offers in an order seeded by the save and `MercenarySave.Refresh`, skipping heroes already in the roster; it is stable until the next battle, which turns it over.
- **Terms:** each longer term is **10% cheaper per battle** than the one before (`PerBattle`); the whole contract is **paid upfront** in ordinary gold (not a paywall hook). The merc joins at the roster's average experience and takes a party slot if one is open (`Summon.Join`).
- **Haggle:** once per merc per refresh, seeded (save + refresh + merc), odds by temperament (drop / hold / raise / walk: Easygoing 55/30/10/5, Fair 40/30/20/10, Greedy 25/25/30/20): **−20%**, unchanged, **+15%**, or the merc **walks off** until the next battle. The answer plays out over a beat ("…") with a cue per outcome.
- **Countdown:** `BattleFactory.Commit` calls `Mercenaries.CountBattle` on every battle, stage or Crucible, win or lose: every contract loses a battle **whether the merc fought or sat in reserve**. A spent contract settles (`Mercenaries.Settle`) before the next battle is built and at Camp, never mid-commit, so the last battle's XP and wear are written first: the merc's gear goes back to the bag, they leave the roster and party, and Camp announces it. They return to the board later and can be hired again.
- **Reoffer:** with **one battle left**, Camp opens `MercenaryRenewalScreen` (portrait steps in, their `Renewal` line types out): **Renew** any offered term on top of the battle left, at a loyalty discount of 10% per renewal (cap 30%); **Finish the contract** (they fight the last battle, then go; backing out means the same); or **Part ways now**.
- **Marked:** a `MERC · N left` tag on Camp's hero cards, the Party screen and the board's "Under contract" list.
- **Bots** (Test Mode, `CampRoutine`, `BalanceReport`) never hire. This is its own contract system: the dialogue vocabulary still cannot remove a hero (AMENDMENTS 2026-11-05).
- *(Tests: `MercenaryTests` — valid content apart from the summon pool, longer terms cheaper, seeded board that skips owned heroes and turns over, upfront payment at the roster's XP, countdown in reserve then departure with gear returned and rehire, a spent contract never fights, renewal at a loyalty discount and parting ways, seeded once-a-refresh haggle with walk-offs, save migration.)*

#### 25.10.2 Random summon for Diamonds (added 2026-11-07, owner, GG-T343)

*Amended 2026-11-21 (GG-T568): Diamonds read **Mana Shards** on screen ([§24.9.1](#2491-the-currencies-wiki-entries-gg-t568)); the draw button reads "Draw · 100 Shards (or 600 G)".*

Owner: *"another way is to simply summon them randomly with Diamonds monetized currency"*.

- **The draw** (`Meta/DiamondSummon.cs`): a "Call a stranger" window at the top of the Summon Circle (`summon-random`) spends ~~**`DiamondSummon.Price` = 1 Diamond** (`mat_diamond`; the one tuning constant for the draw's own price, left at 1 pending a measured pass — GG-T427 tuned the gold-priced hooks around it, deliberately cheaper per attempt than the 1,500 g guaranteed pick (GG-T448) since it's a blind draw) and **no gold**~~ **`DiamondSummon.Price` = 100 Diamonds ($1.00, or 600 G)** since GG-T455 — the same 600 g the one Diamond cost before, still well under the 1,500 g chosen-hero summon — paid through `DiamondExchange.Pay`: Diamonds first, any shortfall in gold ([§25.18.1](#25181-the-diamond-exchange-gg-t455-owner-2026-11-20)). The button reads "Draw · 100 Diamonds (or 600 G)" (a "· 1s" tail dropped, GG-T519) and its confirm names the split ("Pay 40 Diamonds and 360 G …"). It brings one hero from the **wanderer pool** (`Wanderers.Pool`, [§25.11.2](#25112-random-rescues-added-2026-11-05-owner-the-random-character-recruitment-for-saving-them-gg-t236)) — unowned heroes with no other way in, the same pool a wanderer goes back to when it parts ways — never one of the gold summons.
- **Who:** `DiamondSummon.Pick`, seeded from the save's creation time and its recruit count, fixed when the summoning starts (the job's `Arg`), so asking again doesn't reroll.
- **Over time:** it rides the ordinary Summon paywall job (`PaywallJob.Kind = Summon`, `Diamonds` = the whole Diamond price, `Paid` = the gold part of the payment), one summoning at a time (gold or Diamond), `Paywall.Duration(Summon)` (1 s) long, through `TimedJobView` (coin cue, the filling bar, the confirm cue); then a **reveal** (`SummonReveal`, `summon-reveal`): the hero's portrait swells out of the light with a power-up cue, a fanfare and their name and role, and a tap closes it. The hero arrives exactly like a gold summon (`Summon.Arrive`: the roster at its average experience, the party if there's room). A summoning that fizzles (the hero joined some other way meanwhile) gives its whole Diamond price back **as Diamonds**, however it was paid (the gold part was exchanged on the way in). A job left running survives a restart and lands at Camp like any other.
- **Diamonds** (until GG-T455) came only from the LuckyDiamond hook ([§24.6.5](#2465-lucky-craft--the-diamond-gg-t235), ~~**250 g** each since GG-T427~~ **600 g** since GG-T448: a fresh save banked 6,474 g by stage 13, which at 250 g was 26 random heroes); with none, the window says they're bought for the lucky craft at the forge. Diamonds now also pay for an instant Rested refill ([§25.18](#2518-rested-and-the-paywall-hooks-gg-t232)) alongside the lucky craft and this draw — three Diamond sinks on one currency; there was no store. **Amended 2026-11-20 (GG-T455):** Diamonds come from the Diamond Store's gold exchange or its real-money packs ([§25.18.1](#25181-the-diamond-exchange-gg-t455-owner-2026-11-20)); short of Diamonds, the window says the rest is paid in gold and offers a `summon-diamond-store` button. The draw button stays enabled while affordable in either currency, and short of both it offers the store.
- **Bots never draw:** `CampRoutine` only uses the gold summon, Test Mode saves are refused (`DiamondSummon.CanDraw`), and the soaks never touch it. The gold, deliberate summon (`Summon.Recruit`/`BeginRecruit`) is unchanged.
- *(Tests: `SummonTests` — a draw costs its Diamond price and no gold when Diamonds cover it, and the gold equivalent when there are none, it draws from the wanderer pool and the hero arrives only when the summoning lands, the pick is seeded, no Diamond no draw and one summoning at a time, a fizzled draw refunds, a running draw survives a save round trip, Test Mode and the camp routine never spend Diamonds, the gold summon is unchanged.)*

### 25.11 Bestiary

`BestiaryScreen`: a grid of every enemy class's thumb (darkened silhouette until Seen), "Seen n/110"; the detail shows name, Defeated count, lore and its family's drops (rarity-colored item names, highest chance first) once **Seen** (added 2026-10-15, GG-T43). ~~Then base stats only once Scanned.~~

**Amended 2026-11-06 (owner, GG-T290): knowledge is earned by defeats only; Scan is retired.** Each tier is `BestiaryKnowledge.Tier` (`Meta/SaveData.cs`), from the class's `Defeated` count:

| Tier | Needs | Reveals |
|---|---|---|
| Seen | spawned once | name, description, lore, drops |
| Speed | **1** defeat | SPD |
| Stats | **3** defeats | HP at Lv 1 and every base stat |
| Elements | **6** defeats | elemental weaknesses and resistances ("Immune to Poison · weak to Fire (×1.5)", `ElementLine`) |
| Abilities | **10** defeats | its abilities (as chips — hold one to read it), kit traits, brain and targeting personality |

- A locked row reads **"???"**, and a hint under the detail names the next one: **"Defeat N more to learn …"** (its speed / its HP and stats / its weaknesses / its abilities; `BestiaryKnowledge.Hint`).
- **One rule everywhere:** the battle actor panel ([§9](#9-the-hud)) and the hold-to-inspect card read the same tier, so a foe shows in battle exactly what the Bestiary knows.
- **Scan is gone:** the ability, `IsScan`, `ScannedEvent`, the battle report's scanned list, the bot's scan score and its VFX. Save migration drops Scan from every hero's Known list and bar; class kits swapped it out (healers → Quicken, supports → Silence, tanks → Cleave). An old entry marked `Scanned` keeps at least the Stats tier.
- *(Tests: `BestiaryKnowledgeTests` — `Tier_follows_the_defeat_thresholds`, `Thresholds_are_the_named_constants`, `DefeatsToNext_counts_down_to_each_tier_and_stops_at_the_last`, `An_old_scanned_entry_keeps_at_least_the_stats_tier`, `Element_line_reads_the_class_multipliers`, `Migration_drops_the_retired_scan_from_known_and_the_bar`, `No_content_references_scan`.)*

---


### 25.11 Guest allies (added 2026-10-10, owner direction)

Owner: "guest characters that you meet in battle that act as allies — you rescue them and get them to join you, unless they die in the fight, then you've lost them forever."

- `WaveDef.Guests` places a hero class into a wave (`GuestJoinedEvent`, billboard "X joins the fight! Keep them alive to recruit them."). The guest is hero-team and **player-controlled** for that battle — drag it, pincer and support with it — at the stage's recommended level, with its class loadout. A green keyline and a GUEST tag mark it.
**Amended 2026-10-19 (GG-T114, owner: "no one dies permanently, that's lame"):** a guest who falls is *down*, not lost.

- **Standing at a victory** → the guest joins the roster (and the party if there's room) at its battle level; results: "X joins the caravan!". A guest who fell and was brought back (Revival Charm, revive spell) and is standing at the end counts.
- **Falls mid-battle** → billboard "X has fallen!"; revivable like any hero (the Revival Charm picks up a fallen guest too).
- **Down at battle's end** → **not lost**: nothing is recorded, the offer stays open and the guest turns up again when that stage is replayed; results: "X fell in battle — Will return".
- Guests already recruited are skipped (`BattleSetup.ExcludedGuests`). A guest still standing keeps the battle going if the party falls.
- Permanent loss exists only behind the single off-by-default rule `Permadeath.Enabled` (§29.1): with it on (a future Ironman mode) a guest down at the end goes into `SaveData.LostGuests`, is excluded from later battles and the Summon Circle's pool, and falls are announced "for good". In the normal game it is always off and any `LostGuests` entries from older saves are ignored — those guests come back.
- Content: Dervish (Shardfall II), Drifter (Arkgrave II), Lancer (Anchor Wastes II), Machinist (first city ruin).
- *(Tests: `A_guest_who_survives_a_victory_joins_the_roster`, `A_guest_revived_mid_battle_is_recruited`, `A_guest_down_at_battle_end_can_still_be_recruited_later`, `Permadeath_is_off_by_default_and_old_lost_guests_come_back`.)*

#### 25.11.1 Mid-battle recruitment — "talk" (added 2026-11-02, GG-T132; from the since-deleted overworld/dialogue design draft §4)

Fire Emblem's talk on the real grid: some heroes aren't handed over — they have to be won.

- `WaveDef.Recruitables` (`RecruitableDef`: hero `Class`, `Level`, tile, `Dialogue`, `Conditions`) places a hero class on the board on the third team, **`Team.Neutral`**: amber keyline and a TALK tag, no timeline icon, never acts. Neither side fights it — enemies ignore it (no targeting, no pincer through it, a 2×2 won't shove it), hero damage and enemy-only abilities skip it, and it doesn't hold up a wave (a wave clears with it still standing). A hero dragged through it swaps places with it like any one-tile unit. A recruitable that wants healing enters at half HP (`HpPercent` overrides).
- **Conditions** (all must hold): `HealedBy` (any hero, or `Id` = that class, heals it — a hero ally-only ability reaches a neutral), `ParticipatedInPincerWith` (it stands beside a hero attacker — where a supporter would — when that hero's pincer strikes), `PayGold` (`Amount`, paid from the caravan's banked gold at the moment it joins — `BattleSetup.Purse`, deducted on commit whatever the outcome), `TalkedBy` (only that hero class can talk it round).
- **`TalkCommand`** (hero id, neutral id; legal only when cardinally adjacent, footprint-aware): tap the neutral with a hero beside it. Terms unmet → `RecruitOfferedEvent`: it speaks its Recruit dialogue up to the `join` node and says what it still wants — **no clock cost** (like a no-op drag). Terms met → one action: `RecruitJoinedEvent`, it speaks the `join` node, flips to `Team.Hero` as a **guest ally** (§25.11 from there on: player-controlled; standing at a victory → joins the roster; down at the end → it'll be back).
- **Dialogue:** a `DialogueDef` with `Trigger.Kind = Recruit`, `Trigger.Id` = the recruitable's class; linear nodes, no choices or effects, must have a `join` node; `$with` is the hero talking. Never played between battles.
- **Bot:** `HeroBot` talks a recruitable round the moment every term is met (`Recruitment.ReadyTalk`), values healing one that wants it, and never counts shuffling a neutral as progress.
- Content: Ronin (Tithe Moors III, wave 1 — heal me, fight beside me), Sellsword (Shardfall III, wave 1 — 150 G).
- Open (§6.1 of the draft): permanence — today a talked-round recruit joins the roster exactly like a guest.

#### 25.11.2 Random rescues (added 2026-11-05, owner: "the random character recruitment for saving them", GG-T236)

**Amended 2026-11-16 (GG-T387, owner: "winning a game of wits conversation").** Supersedes the one-question talk and the understanding rule below. *The talk* is a **game of wits**: `DialogueDef.WitsExchanges` = 3 exchanges, each a question from the wanderer and three replies for the companion, one per `DialogueChoice.Wit` (+1 wins the point, 0 neutral, -1 loses it); `DialogueScreen` shows the standing as a pip meter (the joining mark ringed in gold) that steps after each reply with a cue. *The understanding* is deterministic, no dice: the start is low trust 0, half 2, full 3, plus 1 with a companion of their own faction; each reply adds its Wit; they join when the final standing is at least `Wanderers.Threshold` = 4. *Validation:* 3 exchanges of three unconditional replies, one each of +1/0/-1, all going to the same node; a node going on to `$outcome`; `join`/`part` end the talk; faction `Regions` must be campaign themes, each claimed once. *Who:* `factions.json` `Regions` — `Wanderers.Local` prefers heroes whose faction roams the stage's region while any are left, else anyone from the pool.

~~A battle the player launches may hold a **captive**: a random hero the player doesn't own, freed as a guest ally (§25.11 from there on).~~

**Amended 2026-11-07 (owner, GG-T342): the captive is now a wanderer.** Owner: *"so you will sometimes meet a hero while fighting a monster and they will fight the monsters separately but you are able to complete pincer moves with them so you fill a trust gauge by pincering, or healing them at the end of the stage and the loot you and the team member you choose has a conversation with them, if they can come to an understanding they join, if not they go back into the pool of possible recruits"* — clarified the same day: *"the point is to gain their trust, tell them what they want to hear, and then you have a follower — another party member."*

- **The roll** (`Meta/Wanderers.cs`, was `Rescues.cs`): `Wanderers.Chance` = **25%** per campaign battle, at most one wanderer, decided when `BattleScreen` builds the battle (`Wanderers.Apply` → `BattleSetup.Wanderer`, a `WandererSpec`). Seeded from the save's creation time, the stage id and the battle seed (`Wanderers.SeedFor`), so a battle replays the same roll. **Never** on a Test Mode save, a test stage, the Crucible, or a stage that already has an authored guest or recruitable; and never in the bots' balance runs or dotnet soaks, which build battles without `Wanderers.Apply`. Authored guests (§25.11) and talk-recruitables (§25.11.1) are unchanged.
- **Who** (`Wanderers.Pool`): an unowned, not-lost hero class that has no other way in — not in the Summon Circle's gold pool, not an authored guest or recruitable, not named by a dialogue's `JoinHero`/`RecruitHero`, not a mercenary (`mercenaries.json`, GG-T233), not a Camp NPC's face. Id order, so a seed always names the same hero. The same pool feeds the Summon Circle's Diamond draw ([§25.10](#2510-summon-circle)).
- ~~**On the board:** … as a player-controlled guest (`Unit.IsGuest`) … "A captive X! Keep them alive to free them."~~ **On the board:** the wanderer steps on in wave 1 or 2 (seeded) at the stage's recommended level with its class loadout on a free tile (`RandomFreeTile`, GG-T227) as an **independent ally** (`Unit.Wanderer`, `IsIndependent`), exactly like a tamed beast (§25.11.3): hero team, never commanded (no drag, no bar, not one of `HeroesLeftToAct`), its own turn icon, and on its turn the shared ally AI (`BattleRunner.RunAllyTurn`: the pincer planner, else charge the nearest foe through anyone one tile big, then close a pincer it stands at the end of, else strike the weakest adjacent foe, else shoot one in range). As a hero-team unit it pincers with the party both ways (`Unit.CanPincer`). `WandererJoinedEvent`; billboard **"A wandering X fights the monsters! Fight beside them to earn their trust."**
- **Trust** (`Unit.Trust`/`TrustNeeded`, `TrustGrewEvent`; tuning in `BattleConfig`): the gauge is full at **6** (`WandererTrustNeeded`). **+2** (`TrustPerPincer`) for each hero pincer it is part of, as one of the two ends or as a flanking supporter, alongside at least one commanded hero (a heroes' pincer it closes on its own turn counts). **+1** (`TrustPerHeal`) each time a commanded hero restores some of its HP (a heal at full HP gives nothing). Never above full. Floats "Trust n/m", and "Trusts you!" with a war-cry cue when full.
- **Look:** a rose keyline (`unit-keyline--wanderer`, `Palette.Wanderer`) with a WANDERER tag; its timeline icon has the same rose border; tapping it says "X is a wanderer: it fights on its own. Pincer beside them or heal them to earn their trust"; the InspectCard heads it "Wanderer · role" with a trust meter (`inspect-trust`) and how to earn it.
- **Defeat and falls:** a wanderer still standing does **not** keep the party in the fight (only commanded heroes do). If it falls it is down, never lost ("X has fallen!"; a revive brings it back with its turn icon); down at the end, it simply goes back to the road — results read "The wandering X goes on their way — May be met again", nothing is recorded and it stays in the pool.
- **The talk (the "loot"):** standing at a victory, the wanderer waits on the results screen (`BattleReport.WandererOffer`, `Wanderers.Offer`) — it never joins on its own. The block shows its trust meter, the rule as it stands (`Wanderers.Outlook`) and a button per party member (a tamed monster can't talk; a faction-mate is highlighted with their faction's name). The member the player picks talks with them: a `dialogues.json` conversation with `Trigger.Kind = Wanderer` (`Id` = the wanderer's hero class, else its `factions.json` faction, else none for the generic `wanderer_met`; `Wanderers.Scene`), `"$hero"` the wanderer and `"$with"` the companion. It has **one question with three answers**: exactly one says **what they want to hear** (goes to the `heard` node); the others go to `unheard`. Leaving the results without talking lets them go.
- **The understanding** (`Wanderers.Judge`, deterministic, no dice): they **join** only if the companion says what they want to hear **and** they trust the party: **full trust**, or **at least half trust with a companion of their own faction**. Below half, nothing said will do. Then both replies go on to `"$outcome"`, which the rule resolves to the scene's `join` or `part` line.
- **Join:** a full, normal party member exactly like a freed guest (`BattleFactory.JoinRoster`): the roster, the party if there's room, at their battle level and wounds; "X joins the caravan!". **Part:** they go their own way and back into the pool — nothing recorded; owning the hero is the only thing that takes them out.
- **Factions** (`factions.json`, `FactionDef`, `ContentDb.FactionOf`): the STORY.md factions with heroes — the Street, the Free Holds, the Lantern Road, the Fen-dark, the Ember, the Wild — and their hero classes; validated (hero classes only, each in at most one faction, unique ids that aren't class ids). Talks: one generic, one per faction, and the Dark Templar's own.
- **Validation** (`WandererContent.ValidateDialogue`): a Wanderer talk names a hero class, a faction or nothing; has no effects; exactly one question of three unconditional answers, one to `heard` and the rest to `unheard`; both replies go on to `"$outcome"`; and `join` and `part` nodes end it. `"$hero"` and `"$outcome"` fit a Wanderer talk only. The GG-T236 `Rescue` trigger and the `rescue_freed` scene are retired.
- **Bots and Test Mode:** the bots and soaks never roll a wanderer and Test Mode never does, so nothing changes for them; `Wanderers.Converse` plays a talk headlessly with a chosen answer (tests). Dev/automation: `devwanderer` (alias `devcaptive`) forces the next launch's wanderer.
- *(Tests: `WanderTests` — the deterministic roll near 25%, pool exclusions, no roll in Test Mode/the Crucible/a guest stage, the roll brings an independent ally with its own icon, it fights on its own turn, trust from a hero pincer and from a heal and its cap, a lone wanderer can't hold the field, a standing one waits to talk and a fallen one returns to the pool, the understanding rule, the right answer at full trust makes a normal party member, the wrong answer or too little trust sends them back, determinism, every pool hero has a faction and a talk, Wanderer-talk and faction validation.)*

~~**Freed:** … "X is freed and joins the caravan!" … the rescue conversation (`Trigger.Kind = Rescue`) …~~ ~~**Down at the end:** … "May be found again" …~~ (both superseded above).

#### 25.11.2.1 Wanderer heroes from the owner's art (added 2026-11-07, owner, GG-T333)

**Amended 2026-11-16 (GG-T388):** **Echo** (Support, a Lantern Road wanderer) fills the one gap no hero covered, copying: *Encore* repeats an ally's last skill or spell from her own position (a copied spell still costs its orbs). Her foe twin, the **Mocking Echo**, charges an attack that turns a hero's last move back on the party (The Still II, Halcyon II, The Choir's Cathedral I; GG-T393). Both wear copies of the mannequin art until the owner's art arrives.

**Amended 2026-11-21 (GG-T531, owner):** the **Echo hero class is cut** (no art, no story role); her abilities stay. *Encore* (`echo_encore`) is now on the **Tactician**'s kit (the other turn-order support, whose Countermarch it can copy); *Mirror Skin* (`echo_mirrorskin`, Reactive: a fair chance to reflect 25% of a direct hit) is now on the **Courier**'s kit, beside Blur, and the Mocking Echo keeps it. Encore's slot still greys out with "No one to use it on" when no ally has a move to copy (GG-T413/GG-T501). The Echo leaves the Lantern Road faction, the wanderer pool and the Diamond draw; her Advice quiz and description text are dropped, and so is her portrait (the Mocking Echo has its own). An old save that holds an Echo loses her on load (`SaveMigrations.DropRetiredHeroes`: any hero whose class no longer exists leaves the roster and the party, her gear goes back to the bag with its wear, and the load logs a line). The Mocking Echo foe is unchanged.

Owner 2026-11-07: the new heroes from the art are **recruited as wanderers** — "met mid-battle, earn their trust, say what they want to hear, and they join". Each is in no other pool (not the Summon Circle, no authored guest or recruitable, no story `JoinHero`, not a mercenary), so `Wanderers.Pool` holds them and so does the Diamond draw (§25.10.2). Each has a `factions.json` faction for the half-trust bridge, an Advice quiz (`talk_quiz_<id>`) and the faction's generic wanderer talk (no hero-specific scene: story text is placeholder until it is authored in Prose, GG-T355). Portraits are 1024 canvases with `Figure` and `Thumbnail` (§12.1.2).

- **Herald** (`herald`, Healer, Speed 12, home tree Cleric, faction **the Wild**). A flower-winged messenger of the Bloom (STORY.md §4) who heals by giving her own essence. The Bloom is a growth, not a faction with heroes, so she sits with the Wild, the closest faction (tide-changed growths). Bar: Heal, Guardian's Rush, potion, **Lifegift**, Covering Withdrawal (the Skirmish-Mender cluster). Signature **Lifegift** (`herald_lifegift`): a Skill, cooldown 2, instant, an ally mends 30 (scaled by WIS like every heal) and the Herald pays **Essence**, 12% of her max HP. Passive **Rebloom** (`herald_rebloom`): at her turn start below 60% HP she mends 5%. Support *Petalfall*: heals the chain 10%, reach 3. Stats lean VIT/WIS (9/14/12/12/12/13/15/12), so she can afford the price. Where the Cleric spends orbs and wards, the Herald spends herself: free, big heals that leave the bank for the casters.
  - **Life Price** (was "Essence", renamed GG-T646 for canon: mana, never essence; glossary id `essence` kept) (`AbilityDef.LifeCost`, 0..0.5 of max HP; `Formulas.LifeCostHp`): once the heal lands the caster loses that share of her max HP, at least 1 and **never her last HP**. It is a price, not a blow: no attacker, no damage triggers, no cast stagger; a `DamagedEvent` with no source and the note "Life Price" plays the -N float and the debuff cue. Validated: a Skill or Spell ally heal (BaseHeal > 0, AllyOnly). The bot never pays it below 40% HP and weighs the HP given. Glossary [Life Price]. Player text names the six enchant materials "<Element> Aether" (ids `mat_essence_*` kept) and enchanted gear reads "Imbued with <Element> aether."
- **Manasmith** (`aetherist`, Healer, Speed 12, home tree Cleric, faction **the Fen-dark**; GG-T337). A Choir singer who reversed the hymn and pours mana in instead of drawing it out (STORY.md §4); the Fen-dark is where the Choir's runaways go (§3.5). Bar: Heal, Preemptive Ward, potion, **Mana Pour**, Sanctuary Rite (the Aegis Warden cluster). Signature **Mana Pour** (`aetherist_aetherpour`): a Skill, cooldown 4, instant, an ally mends 15 and **one orb flows into the party's bank** — the Bulwark's Siphon (§14.15) run backwards. Passives *Arcane Insight* (mana charge when she backs a pincer) and *Pure Spirit* (she can't be Silenced: the Choir's Hush doesn't take). Support *Cyan Current*: heals the attacker 10%, reach 3. Stats lean INT/WIS (7/9/11/12/12/18/18/12). Where the Herald spends HP to save orbs, the Manasmith feeds the bank so the casters can spend.
  - **An orb grant can ride a heal** (`AbilityDef.PoursOrbs`: `GrantsOrbs` on an ability that also heals, hits or buffs). It resolves on its targets like any ability, then mints its orbs ("aether"); a bare grant (Meditate, an ether) still resolves on its own with no target.
- **Tactician** (`tactician`, Support, Speed 12, home tree Alchemist, faction **the Free Holds**; GG-T336). A former Concord staff officer who planned tithe routes until she read the ledgers (STORY.md §4). The Concord has no hero faction; the Free Holds, with their militias and old oaths, are the closest fit for a defecting officer. Bar: Quicken, Stumbling Ground, potion, **Countermarch**, Swift Swap (Tempo Weaver tools for position and pace). Signature **Countermarch** (`tactician_countermarch`): a Skill, cooldown 4, no damage, on any foe. Passive *Keep Away* (a fair chance a foe that strikes her directly is pushed back 1 s). Kit also holds **Encore** (`echo_encore`, from the cut Echo, GG-T531): repeats an ally's last skill or spell from where she stands. Support *Flank Order*: Pushback 0.75 s on the pincer's foes, reach 3. Stats 10/11/12/12/12/15/15/12. The other supports move the party's tempo; she rewrites the enemy's order.
  - **Countermarch** (`AbilityDef.SwapsTurns`, `Countermarch`): the foe's turn icon trades its time to act, exactly, with the foe whose turn is furthest off: the one about to act goes to the back of the line and the straggler steps up to its time. Both must be on the bar (not recovering in the tray, not Frozen) and on the enemy side; an independent ally is never moved. On the last foe, or one off the bar, it does nothing ("Already last" / "Not on the line"). It is a trade, not a push, so the Pushback cap (§2.3) doesn't bound it: the bar holds the same turns, only who strikes next changes (a charging caster or the foe beside a wounded hero for a harmless straggler). Played back as the existing Pushback glide on the foe and Haste glide on the straggler, with their cues, and a "Countermarch!" float. Validated: a Skill or Spell that picks one foe. The smart bot prices it by the time it buys (GG-T356, `HeroBot.CountermarchValue`): the harm of the foe's next turn (`HeroBot.NextTurnHarm`: its worst blow on a hero it can reach, a KO and a completed enemy pincer weighing most, a charge it would start less), scaled by the seconds it is delayed (3 s buys all of it), less the harm the straggler does when it steps up, plus the pending hero casts that now land first; under 6 points (no heavy blow, KO or pincer averted) it skips it; the casual bot sends a threatening foe to the back. Glossary [Countermarch].
- *(Tests: `WandererHeroesTests` — each hero's kit, the wanderer pool and faction, the quiz, the art; Lifegift heals and costs essence but never the last HP; a LifeCost must pay for an ally heal; Mana Pour heals and pours an orb; a bare orb grant is unchanged; Countermarch trades two turns exactly and does nothing on the last foe or one off the bar; the smart bot countermarches to avert a KO but not a light blow, nor to trade one KO for a sooner one; a Countermarch must pick one foe.)*

#### 25.11.2.2 Warden, the fixed lead (added 2026-11-21, owner, GG-T533)

The canon lead (`Data/Prose/canon/06-arc.md`, Decisions): **the Warden**, **the Last Sleeper**, Halcyon's last guard, asleep in the Still on the night Halcyon fell, who wakes 1,200 years too late and must fix things. The Warden is the only sleeper in the story; every other hero is a present-day person (owner 2026-11-21, GG-T577, replacing "the First Sleeper" who thawed the others). Story text names the Warden with the article, like every hero ("The Warden" at the start of a sentence, "the Warden" elsewhere; GG-T565, replacing the earlier bare-name rule); the class id and assets stay `warden`. A class of its own, separate from the Knight (the Knight is unchanged). The owner's female portrait ships (`Portraits/warden.png`, 1024 canvas with `Figure` and `Thumbnail`); the male variant ships with GG-T532 (below).

- **Class** (`warden`, Tank, Speed 11, home tree Guardian, Martial weapons, Heavy armor). Stats sit in the tank band (16/18/10/11/18/8/10/11: one Strength moved to Wisdom). Theme: guardian of sleepers, keeper of light. Where the Knight swaps into danger and the Paladin wards the whole party, Warden takes the blow for one comrade and protects the party's light against the blanking-style statuses (the Lullaby's [Asleep], the Hush's [Silenced]).
- **Bar** (`DefaultLoadout`): Taunt, **Reveille**, X-Potion, **Stand Guard**, **Lantern of the Still**. A new Warden knows Taunt and Stand Guard (`Progression.StarterKit`); Reveille and the Lantern are bought from the trainer or learned in the tree, like every kit move (GG-T388).
  - **Stand Guard** (`warden_stand_guard`, signature): a Skill, cooldown 3, on one ally: they are **[Guarded]** for 2 turns.
  - **Reveille** (`warden_reveille`): a Skill, cooldown 3, on one ally: strips every debuff ([Asleep] and [Silenced] included) and hastens their turn icon 0.15u. The timeline move: a sleeper woken *and* brought forward.
  - **Lantern of the Still** (`warden_lantern`): a Skill, cooldown 5, every ally within 2 tiles (Diamond 2, centred on Warden): every debuff stripped, then [Ward]. Cleansing, plus the Paladin's Bulwark in a radius rather than the whole party.
  - Kit also holds Shield Bash, Shove, Antidote and Hands of Mercy (never Sleep or Silence: Warden doesn't put people under).
- **Passives:** *Full Light* (`warden_full_light`, Always Immunity to `sleep` and `silenced`: a sleeper's light is wholly their own, so the Lullaby and the Hush don't take) and *Last Watch* (`warden_last_watch`, Reactive: an ally falls, Warden gains [Taunting] and draws every foe).
- **Support** *Reveille*: Haste 0.75 s on every ally along the chain, reach 2 (the Dervish's band), so backing a pincer brings the party's next turns forward.
- **[Guarded]** (engine, small: `BuffDef.Intercepts`, `BuffInstance.SourceId`, `BattleConfig.GuardReach` = 2): a buff that remembers who applied it. In `BattleRunner.Damage`, the single damage choke-point, a foe's blow or hostile ability that lands on a [Guarded] unit is taken by its guardian instead, when the guardian is alive, not immobile (asleep, frozen), on the same team and within 2 tiles (Manhattan); the guardian's own reductions ([Taunting], [Ward], armor) apply, the buff is spent ("intercepted"), and the float reads "Guarded!". Source-less damage (a poison tick, a blast, Essence) and misses are never guarded. Any status or movement the blow carried still lands on the original target: the guard takes the damage only. Validated: an Intercepts buff must be a Buff. Status icon, VFX style (cyan, the colour of mana) and glossary [Guarded].
- **The fixed lead** (`Progression.LeadClass`, `Progression.IsLead`):
  - **New game:** `Progression.NewGame` always puts Warden first in the roster and the party, whatever party it is given; `StartingRoster` is Warden, Barbarian, Cleric (Tank, Striker, Healer). Character select (§25.15) opens with Warden already locked into the first slot of the strip ("Your lead: always in the party"); the hint reads "Warden leads. Pick 2 more…", so the player still picks the other two of the starting three.
  - **Locked in:** `Party.Toggle` refuses to move Warden to the reserve ("Warden leads the party").
  - **In no recruit pool:** `Wanderers.Pool` (so the Diamond draw too) and `Summon.StoryPick` skip the lead; the Summon Circle's `summon.json` pool, guests, recruitables and mercenaries don't list them.
  - **Old saves** (`SaveMigrations.EnsureLead`, on every load after `DropRetiredHeroes`): a save without Warden gets them the way a summon joins (`Summon.Join`: the roster's average experience, trained to that level), then Warden moves to the front of the party; if that makes the party more than the cap of 6, the last member steps back to the reserve (nobody leaves the roster). Idempotent, wrapped so a bad save never throws; a content set without the class is left alone.
- **Text:** the class description and lore are seed texts in `Content/Text/descriptions.json` (`class:warden`) and the glossary entry in `Content/Text/glossary.json` (`glossary:guarded`), with drafts for Prose (`prose_text.upsert_seed`); story text is the Prose engine's to author.
- **Amended 2026-11-21 (GG-T532):** the male Warden ships too (`Portraits/warden_male.png`, `MaleFigure`/`MaleThumbnail` in `classes.json`), the first class with both versions (§25.11.2.3). A new game asks the locked Warden's look right after the party pick.

#### 25.11.2.3 Hero variants: female or male, picked on joining (added 2026-11-21, owner, GG-T532)

Owner: "eventually I want to generate male counterparts for all the character classes in the game so you get one or the other, they randomly show up but when they finally join your party you get to choose the gender, same with summoning - if your diamond cost lands on a character, you get to pick male or female variant; no change to story or mechanics makes everything equal."

- **Looks only.** A hero's variant changes the art and nothing else: same class, stats, kit, tree, support, story beats and text. Heroes stay named by role with the article, "the Cleric" (§25.11.2.2), and hero-facing text never assumes a gender (see the text rule below).
- **The art.** The female art is the class's `Portrait` file (`acolyte-female-00.png`, every class has it); the male art is its `MalePortrait` file (`acolyte-male-00.png`), the same 1024 canvas rule (§12.1.2) and naming (§12.1.3). A class may carry `MaleFigure` (written by `GridGame.Studio/tools/import_art.py` when `<classId>_male` is imported) and `MaleThumbnail` (framed by hand); each falls back to the class's own `Figure`/`Thumbnail`, and `ContentLoader.Validate` checks them when set. Most classes have no male art yet (GG-T535 generates it); everything below works with partial art.
- **Save (v14).** `HeroSave.Variant` (`HeroVariant` Female, the default, or Male; serialized by name) and `HeroSave.VariantPending`. `Progression.NewHero` sets `VariantPending`, so every way a hero joins waits for the pick: new game, story `JoinHero`/`RecruitHero`, the Summon Circle (gold), the Diamond draw, a wanderer won over, a freed guest, a mercenary, a quest reward, and the Warden `EnsureLead` gives an old save. Migration v13 → v14: every hero already in the roster is Female with nothing pending; idempotent, never throws; an unknown variant value reads Female on any load. Domesticated monsters have no variants.
- **One resolver** (`App/Services/Art.cs`). `Art.Portrait(classId)` reads the version through `HeroLooks.Of(classId)`: an owned hero's `Variant`, else a look the current battle gave an unowned hero, else Female; then the male art if it exists, else the female art. Every portrait, thumbnail (`Ui.PortraitThumb` frames with `Art.ThumbnailFor`, the male framing on male art), figure (`Art.PortraitFigure`, `FigureSize`, `FigureNativeHeight`: a `<id>_male` texture uses its class's `MaleFigure`), cut-in, dialogue speaker, card, level-up and death dissolve goes through it, so no view knows about variants. `Art.Portrait(classId, variant)` / `Art.PortraitFigure(classId, variant)` draw one version explicitly (the picker). `Art.HasMaleArt(classId)` is cached.
- **The picker** (`App/UI/VariantPicker.cs`). A dimmed veil with "<Hero> joins you!", "Choose a look. Same skills, same story." and the two figures side by side, Female left and Male right, each a whole-card tap target with its label under it; the pick pops the card and fades the veil (law 5: a cue on opening, `ui.confirm` on the pick). `VariantPicker.ShowPending(then)` asks every waiting hero in join order; a class with **no male art is skipped silently and set Female**. It opens: after the new-game party pick (before the prologue, the locked Warden included); after the Diamond draw's reveal and a gold summon's arrival; and on the router's hook whenever Camp, the results screen or a Camp page (Summon Circle, Mercenaries, Quests, Party, Status, Bounties) comes up with someone waiting (story joins, wanderers, mercenaries, quests). Never over a battle, a story scene or a crawl. Camp's coach marks wait while it is up.
- **Changing it later.** The hero screen's Status tab shows **Change look** under the portrait for a class with male art: the same picker with the current look marked in gold and a Cancel.
- **Wanderers on the board** (§25.11.2) show either version at random (seeded by the battle) when the class has male art (`HeroLooks.Show`, cleared at each battle start); the talk after the battle shows the same look. Joining still asks the player.
- **Automation.** Bots, `-demo`, Test Mode, `-autobattle` and harness runs don't stop to ask (waiting heroes become Female) unless the run passes `-variants` (as the Rested gate's `-rested`). The state dump's `save.variants` maps each hero to Female/Male ("?" while pending) and `save.variantPicker` says whether it is up; `tools/harness.py variants` drives new game → the Warden's picker → Male → the male Warden at Camp → Change look → Cancel, recorded.
- **Text rule.** Hero-facing text (ability descriptions, class cards, banners, seed text) refers to a hero by role or as they/them, never he/she. GG-T532 fixed the local seed text (the Shieldguard's Valkyrie, the Panda Brawler's Slam, the Herald's Lifegift descriptions, the Herald's card, the wanderer banners). Prose-owned class descriptions that still say she/her (Herald, Manasmith, Hex Stalker, Monster Tamer, Tactician) are listed on GG-T532 for Prose; enemies and Camp NPCs keep their own pronouns.
- **Neutral class names (owner 2026-11-21, GG-T543).** Since every class has both looks, no class name is gendered. Display names only; ids, portrait files, ability ids and Prose keys are unchanged so saves and art keep working: `shieldmaiden` is the **Shieldguard** (the owner's "Shieldbearer" is already the Bulwark Shieldbearer enemy), `pandagirl` the **Panda Brawler**, `blackwitch` the **Hexer**, `whitewitch` the **Lightweaver**, `sister` the **Acolyte**. Sister Ilsabet (the Medic Wagon's crew NPC) keeps her name.
- *(Tests: `Hero_variants_new_heroes_wait_old_saves_are_female_and_choosing_sets_it`; `PortraitCanvasTests` checks a `MaleFigure` against its `_male` PNG's alpha bounds.)*

#### 25.11.3 Taming and domestication (added 2026-11-06, owner, GG-T303)

Owner: a Monster Tamer can tame an enemy monster so it fights for the party for a few turns, "like Umaro the yeti berserker in FF VI": it acts on its own, keeps its own timeline icon and AI, and may run straight through a pincer you were setting up. Fight beside it long enough and it asks to join for good.

- **The Tame effect** (`AbilityDef.Tame` = base chance, `TameTurns` = duration in the monster's own turns) is one ability field, so any source plugs in: the Monster Tamer's **Tame** (Skill, 0.6, 4 turns, cooldown 2) and the **Dog Whistle** consumable (`dog_whistle_call`, 0.35, 2 turns). Validated: a Tame is a Skill or Item ability that picks one foe (SingleActor, PickActor, EnemyOnly), 0 < Tame <= 1, TameTurns >= 1.
- **Who** (`Taming.WhyNot`): a living enemy with no society: not `Sentient` (so frog folk, soldiers, hags never), not a boss (tag `Boss`, boss phases or a rule-break), not remains, an egg or a corpse, one tile and not a snake, not tagged `Untameable`. Today that is the bats, wolves, werewolf, yeti, flying monkey, vulture, scorpion, slimes, sandmaw, tree golems, skelepedes, the mindless undead, cyber-zombies and the ghost. `TargetResolver.IsValidPick` offers only these, so a refused pick spends nothing.
- **Odds** (`Taming.Chance`, one Rng draw): base x (1.5 - the monster's HP fraction, so x0.5 fresh to x1.5 nearly dead) x level factor (1 + 0.05 per level the user is above it, x0.5..x1.5) + 0.4% per point of the user's Luck and of its Wisdom, clamped to **5-95%**. A failure is a `TameFailedEvent` ("Resisted (n%)").
- **Tamed** (`TamedEvent`): the unit flips to `Team.Hero` with `Unit.Tamed` where it stands; any charge it was casting fizzles (`CastCancelledEvent` "tamed"); it keeps (or gets) its own turn icon. It is **independent** (`Unit.IsIndependent`): never commanded (no drag, no bar; `CanMove` and `GetSlotStatus` refuse; not one of `HeroesLeftToAct`). Its own turn **doesn't end the party's window** (only enemy turns do, §2.0), its buffs tick on its own turns, and party tempo doesn't move its icon. Enemies treat it as a hero: they target it and pincer it. A tamed beast alone doesn't keep the party in the fight: when every commanded hero is down, it is a defeat.
- **Its turn** (`BattleRunner.RunAllyTurn`): start-of-turn triggers. **Amended 2026-11-06 (GG-T306):** first it asks the shared pincer planner for a walk that completes a hero pincer, sets up an end a hero can close, or slots it into a chain ([§14.1.3](#1413-pincer-planning-gg-t306)), swapping through friend and foe. Otherwise it charges the nearest foe on the shortest cardinal path **through anyone one tile big**. Friend or foe swap into the tile it leaves, exactly like a hero drag, up to its `MoveRange`. ~~It is deliberately not clever about your setup.~~ It still acts on its own, so it can disrupt your setup. Then it closes any hero pincer it stands at the end of (it carries the **Pincer** keyword, §1.3), else hits the weakest adjacent foe, else shoots one in range. Its own class `Support` backs the party's pincers (§1.2.3). End-of-turn triggers and buff ticks follow.
- **Bond** (`Unit.Bond`/`BondNeeded`, `BondGrewEvent`): each own turn fought while tamed adds 1. `BondNeeded` is the class's `TameBond` (yeti and werewolf 6), else 3 + level/5. While the bond isn't full, each turn also spends one tame turn; at 0 it **breaks free** (`TameEndedEvent`): back on `Team.Enemy` where it stands, its bond kept for a re-tame. A **full bond** ("Bonded!") stops the countdown: it stays for the rest of the battle.
- **Domestication** (`Meta/Domestication.cs`): a tamed monster still standing at a victory with a full bond asks to join (`BattleReport.TameOffers`). The results screen shows "X wants to join you!" with **Accept** and **Decline**. Accepted, it becomes a roster member (`HeroSave.Domesticated`, keyed by its enemy class, so one of each) at its battle level: in the party if a slot is free, else the reserve. In battle it spawns with the party as an independent ally (`HeroSpec.Domesticated`) with the Pincer keyword. Drawbacks: no gear (`Equipment.Equip` refuses), no learned skills or Tomes, **no skill tree** (an enemy class has no `HomeTree`), half XP (`Domestication.XpFactor`) and no growth past **Lv 30** (`LevelCap`). Camp shows a TAMED badge in place of a role.
- **Look:** a tamed or domesticated unit wears a chartreuse keyline and a TAMED tag on the board, and its timeline icon has the same `Palette.Tamed` border. A "Tamed!" float with a power-up cue; "Bond n/m" each turn; "Bonded!" with a war-cry cue; "Breaks free!" with the debuff cue. The InspectCard shows a bond meter and the turns left.
- **Getting them:** the **Monster Tamer** (Support, Wild Child's portrait, home tree Alchemist) is in the **Summon Circle** pool. Kit: Tame, Feral Howl, Alpha's Challenge, Regen, Quicken, Antidote. Passives: *Pack Leader* (after it backs a pincer, a fair chance to turn the attacker it backed [Empowered]) and *Beast Sense* (a beast that attacks it directly is pushed back 1 s). Support: *Whip Crack*, Pushback 0.5 s. The **Dog Whistle** (150 G, stack 3) is sold by the Merchant.
- Not in scope: Monster Meat transformation (GG-T304). The bot never tames yet (no score for it).
- *(Tests: `TamingTests`.)*

### 25.12 Caravan upgrades (added 2026-10-10, owner direction: "upgrades … a sense of progression")

**Hidden at release (GG-T392):** `Features.Caravan`; see §8.1.

The caravan is the party's moving base — a Concord supply caravan the crew stole on tithe day ([STORY.md](STORY.md) §4), and the shops between battles (US-348 framing; story text §27.1). `caravan.json` defines upgrades bought with gold at **Camp → Caravan** (`CaravanUpgrades.Buy`, levels in `SaveData.Caravan`):

| Upgrade | Levels | Effect |
|---|---|---|
| Mana Casks | 400 / 900 / 1800 | every battle starts with +1 / +2 / +3 orbs (`BattleSetup.StartOrbs`) |
| Medic Wagon | 600 / 1400 | the inn opens **on the road** mid-expedition at ×2 / ×1.5 price |
| Armory Wagon | 350 / 800 / 1500 | repairs −20 / −35 / −50 % (`Repair.Cost(c, s, …)`) |
| Strongbox | 500 / 1200 | expedition completion bonus +25 / +50 % |
| Scouts | 300 | Adventure lists each battle's foes |
| Enchanter's Wagon | 700 / 1600 | armor enchants −20 / −40 % gold (`ArmorEnchanting.Gold(c, s, e)`; never below 1; the guided first rune stays free) — GG-T75 |
| Quartermaster's Ledger | 450 / 1100 | salvage (bag, equipped and Broken) returns +25 / +50 % of each material, rounded half up (`CaravanUpgrades.LedgerYield`) — GG-T75 |
| Blacksmith's Forge (facility) | 400 / 1500 / 3000 | Iron / Steel / Mythril Forge: forge Rare / Epic / Legendary results (§25.12.2) — GG-T432 |
| Alchemist's Still (facility) | 300 / 1200 | Glass / Silver Alembic: brew Uncommon / Rare-and-up results (§25.12.2) — GG-T432 |

*(Test: `Caravan_upgrades_cost_gold_and_change_how_expeditions_play`.)*

#### 25.12.2 Facility upgrades gate material tiers (GG-T432)

Owner 2026-11-19: *"caravan upgrades meaning you have to unlock the shops abilities to increase the value of things that can be built there, ya thats a necessary gold sink so copper, iron, steel, increasing quality of materials means you need better facilities for Blacksmith, Alchemist, etc".*

Each crafting station has a **facility**, a caravan upgrade in `caravan.json` with a `BaseName`, a `BaseRarity` and, per level, a `Name` and the rarest result `Rarity` it can make (`Meta.Facilities`; levels in `SaveData.Caravan` under `forge` / `alembic`; `ContentLoader.Validate` checks only those two ids are facilities and that each level's rarity rises). A recipe needs the first facility level whose rarity covers its **result's rarity** (`Facilities.Required`); the existing rarities are the material tiers, so no new items were added:

| Station | Base (free) | Level 1 | Level 2 | Level 3 |
|---|---|---|---|---|
| Blacksmith (Forge + Upgrade) | Copper Forge — up to Uncommon (225 recipes) | **Iron Forge** 400 G — Rare (111) | **Steel Forge** 1,500 G — Epic (71) | **Mythril Forge** 3,000 G — Legendary (39) |
| Alchemist (Brew) | Clay Still — Common (11) | **Glass Alembic** 300 G — Uncommon (11) | **Silver Alembic** 1,200 G — Rare and up (4) | — |

The Enchantress has no facility (her enchants are gated by the Alchemist's skill tier only, §25.3.4). Prices against measured stage pay (≈210–270 G a stage at Lv 1–6, 400–600 G from Lv 9, ≈9,900 G for the main campaign): the first rung is one or two early stages, Mythril about six late stages, the whole ladder 6,400 G. A facility is bought at its own station (a **Build** row atop the recipe list, and a Build button on any recipe it locks: a coin cue, then the forge's clang or the still's glug) or as a card on the Caravan screen; buying one at the station doesn't open the Caravan row early (`CampUnlocks.CaravanOpen` ignores facilities). Locked recipes show **"🔒 Iron Forge"** in the list and "Needs Iron Forge (you have the Copper Forge)" in the detail, never hidden; `Crafting.Craft` refuses with "Needs Iron Forge" and consumes nothing. Station XP (§24.6.1) stays the separate skill axis on `Tier`; a recipe needs both (`Crafting.CanMake`). The guided first forge, first armor and first brew pick only recipes the base facility allows (all Common). **Save v10** grandfathers older saves: one facility level per station tier above Apprentice (Journeyman → Iron Forge / Glass Alembic, Master → Steel Forge / Silver Alembic), never lowering a bought one. The bot builds facilities too (§25.3.3). *(Tests: `FacilityTests`.)*

#### 25.12.1 The caravan crew and their barks (GG-T75, added 2026-11-02, owner direction 2026-10-16)

Each wagon has a crew member (`Crew`, `CrewRole` in `caravan.json`), shown on its Caravan card ("Crew: Brannoc, cask-keeper"): Brannoc the cask-keeper (Mana Casks), Sister Ilsabet the wagon medic (Medic Wagon), Old Hask the wagon farrier (Armory Wagon), Penny Tallow the purser (Strongbox), Wren and Dace the outriders (Scouts), Lio the rune apprentice (Enchanter's Wagon) and Osk the quartermaster (Quartermaster's Ledger). They are not Camp NPCs (§25.0.1): they have no station, no Talk and no join scene.

**Barks:** each crew member has a few one-line `Barks`. The Caravan screen's header and, once the Caravan station is open (`CampUnlocks.CaravanOpen`), a small "Around the wagons" window under Camp's gold/time panel each show one line with its speaker (`CaravanUpgrades.Bark(c, s, turn)`). The line changes every time the screen rebuilds. Lines come from the crews whose wagon this save has upgraded, or from every crew before the first upgrade. A bark is **flavor text, not dialogue** (§27): no portrait scene, no branching, no choices, no effects, no flags, nothing to tap. It is not an overworld either (§28).
<a id="2514-the-party-of-five-added-2026-10-11-owner-add-a-fourth-and-fifth-hero-onto-the-team--a-5-player-team-seems-about-right-for-max"></a>

### 25.14 The party of six — was five (added 2026-10-11, owner: "add a fourth and fifth hero onto the team … a 5 player team seems about right for max")

`MaxPartySize` = **5**; a new game's five are picked on `CharacterSelectScreen` (§25.15) rather than fixed. **Amended 2026-10-19 (owner, GG-T118):** the cap stays 5 but a new game starts with **three**; the other two join at story beats ([§25.17](#2517-small-start-story-recruits-gg-t118)). Recruits and rescued guests wait in reserve when the party is full. Camp shows the five in a two-column grid; class blurbs render their [keywords] as highlights.

**Amended 2026-11-06 (owner, GG-T291): the cap is six — "two attackers and two supporters for each attacker."** `Progression.MaxPartySize` = **6** (`StartingPartySize` stays **3**): one pincer's two attackers, each with two supporters chained to it ([§1.2.3](#123-supporters)). The slots fill at the existing story beats ([§25.17](#2517-small-start-story-recruits-gg-t118)): the 4th after Tithe Moors I, the 5th at Shardfall, the 6th at Anchor Wastes; the Halcyon healer (and any later recruit or rescued guest) waits in reserve. Six heroes spawn on the stage's `DefaultHeroStarts` in rows 6–7, below the enemy band, on every campaign stage, and share one item bag ([§24](#24-equipment-items-materials-currency), GG-T285). Camp shows six in its two-column grid (three rows); the Items "use on" row wraps a party of six into equal thirds. *(Tests: `PartyCapTests` — `The_cap_is_six_and_the_start_stays_three`, `A_seventh_hero_waits_in_reserve`, `Six_heroes_spawn_on_free_tiles_below_the_enemy_band_on_every_campaign_stage`, `Six_heroes_share_one_bag`; `StoryRecruitTests`.)*

### 25.15 Character select (added 2026-10-11, owner: "unlock all character and select a random cross section of heros, make character selection like a fighting came where you have a grid at the bottom then you select a tile then the full artwork appears on top and you lock in that character")

`New Game → slot` now pushes `CharacterSelectScreen` instead of calling `Progression.NewGame` with the fixed five directly. Every hero-team class in content (62, not just `StartingRoster`) is an open tile in a scrollable grid at the bottom — tiles are 164px with an even 10px margin on all sides and the row centred (`Justify.Center`), so the gutter reads the same left, right and between tiles regardless of how many columns fit or the window's width. Tapping a tile previews it — full `Art.Portrait` art, name, role badge, description — in a reveal panel above, framed in that role's colour, which **slams** into place (oversized and tilted, a `Tween.Back` overshoot down to rest plus a small landing shake and an impact cue) rather than popping in cold, matching the cut-ins' bombastic feel (bible law: no single-frame swaps). Tapping the same tile again (or "Lock In") commits it to the party strip at the top. **Amended 2026-10-19 (GG-T118):** three locks (`Progression.StartingPartySize`; a hint under the header says more heroes join along the road), not five ([§25.17](#2517-small-start-story-recruits-gg-t118)); the last lock → `Progression.NewGame(c, name, party)` (an overload taking the chosen roster; the old 2-arg form still exists for automation/tests and defaults to `StartingRoster`). `-demo` drives the identical screen itself (`auto: true`): it previews a random not-yet-picked class, pauses, locks it in, once per starting hero, so every attract-mode run shows a different squad. *(Verified: `tools/harness.py` launch, screenshot of the live grid showing even gutters, and a frame-by-frame `record` capture of the reveal panel mid-slam — tilted/oversized mid-flight, not instant.)*

### 25.16 Diagonal slip (added 2026-10-11, owner: Terra Battle's fast-swipe diagonal move — "if you moved quickly and accurately you could move diagonally between two squares… it takes skill… allow the game to skip the collision detection")

Player hero drags only (never AI — enemies/bots use the separate `StepEnemy`/`EnemyPlanner` path and never submit a diagonal step). Gated by `BattleConfig.DiagonalSlipEnabled` (mutable, default **on**; `-noDiagonalSlip` at the command line turns it off). A diagonal step is legal, destination tile fully free (no unit, no movable — never a swap, never onto anything: a glide into open space, same "two units never share a tile" invariant §(tile-conflict safety net) protects), with no softened "at most one corner" rule — the two flanking tiles' occupancy is never consulted, matching the owner's framing of the original as skipping a check rather than a designed exception. `WalkPath` (`BattleRunner.Combat.cs`, the single path both the committed move and the live drag-ghost preview run through) accepts `Coord.IsDiagonalNeighbor` alongside the existing cardinal check when the flag is on. The client (`BattleScreen.Input.ExtendPath`) only ever offers a diagonal candidate when the raw drag gesture between polled pointer-move frames is both fast (> `Tile×6` px/s) and close to a true 45° (within 12°) — a careful/slow drag always still resolves as today's safe two-cardinal-step route, so the skill gate is pointer-timing, not a toggle the player can rely on deliberately. *(Tests: `Diagonal_is_not_a_pincer_and_diagonal_step_is_refused_when_slip_is_disabled`, `Diagonal_slip_glides_onto_a_free_tile_when_enabled`, `Diagonal_slip_is_still_refused_onto_an_occupied_tile_even_when_enabled`. The engine contract is machine-verified; the client's speed/angle gesture gate is pointer-timing and was confirmed by hand, not by the automation harness — its synthetic `drag` endpoint deliberately smooths input into ~30px sub-steps for clean recordings, which never reproduces a real fast flick, so a drag through it always falls back to the safe two-step route exactly as before, with zero regression.)*

### 25.17 Small start, story recruits (GG-T118)

Owner 2026-10-19: "New Game picks a small party (2-3 heroes) instead of 5; further heroes join at story beats through dialogue." The party cap stays **5** (§25.14). **Amended 2026-11-06 (owner, GG-T291):** the cap is **6**, so the Anchor Wastes Knight now fills the sixth slot instead of waiting in reserve; only the Halcyon healer (Sage) goes to the reserve.

- **Start:** `Progression.StartingPartySize` = **3**. Character select locks in three (§25.15); the 2-arg `Progression.NewGame` (automation, tests, Test Mode) uses `StartingRoster` = Paladin, Barbarian, Cleric (Tank, Striker, Healer). `-demo` auto-picks three. **Amended 2026-11-21 (GG-T533):** Warden, the fixed lead, is always one of the three (locked into the first slot; the player picks two), and `StartingRoster` is Warden, Barbarian, Cleric ([§25.11.2.2](#251122-warden-the-fixed-lead-added-2026-11-21-owner-gg-t533)).
- **Story recruits:** a dialogue effect `JoinHero` (§27.1) — `Id` (preferred class) and/or `Role`. `Summon.StoryPick` resolves it so it **always brings someone new**: the named class if the player doesn't have it, else the first unowned hero of that `Role`, else one of a role the roster lacks (Healer, Tank, Mage, Support, Striker, Ranged), else anyone unowned (none left → no-op). The fallbacks skip the Summon Circle's pool and every stage guest, so those stay worth having; name order makes it deterministic, and the dialogue's `"$recruit"` speaker is bound to the same pick when it opens, so the hero who speaks is the hero who joins. The recruit arrives free at the roster's average experience (`Summon.Join`, like a Summon Circle recruit) and takes an open party slot, else waits in reserve. Separate from `RecruitHero`, which hires exactly one class and does nothing if it's owned.
- **The beats (`dialogues.json`, real narrative as of GG-T120):** after Tithe Moors I (before the next launch) — a Mage (Red Mage); at the start of Shardfall — a Support (Engineer), making five; at the start of Anchor Wastes — a Tank (Knight), making six (GG-T291); at the start of Halcyon — a Healer (Sage), who goes to the reserve because the party is full (before GG-T291 the last two did). Every path through each beat ends in the join (the join is on the last node, not a choice). They sit on top of the guest allies (§25.11) and the Summon Circle (§25.10).
- **Nobody is lost for good:** nothing in the dialogue vocabulary removes a hero.
- **Bots:** Test Mode and the balance sim never show story, but play every pending beat headlessly before a stage (`DialogueRunner.PlayPendingBeatsHeadless`, first choice) so the recruits still join; the Test Mode cycle line reports them as `story: …`.
- **Balance (`BalanceReport.Campaign_pacing_table`, 4 seeds, up to 4 attempts a stage):** the default rows now start with the trio and play story beats. Greedy finishes 4/4 (as before); greedy+camp 3/4 (was 2/4); Tithe Moors II onward plays like the five-hero start because the fourth hero arrives before it and the fifth at Shardfall. The only early stage the trio found harder was Tithe Moors I for the casual bot (30% of attempts vs 66% with five), so it was lightened (§22.7) and is now 80% with 61% HP left. The table keeps a `greedy+camp trio no-story` row (GG-T87's trio-start: no story recruits, so the Summon Circle step always has a slot to fill) and a `greedy five-start (pre-GG-T118)` row as the yardstick.
- *(Tests: `StoryRecruitTests` — the small start, JoinHero's named/role/missing-role fallbacks, the summon pool and guests left alone, a full party sends the recruit to the reserve, the `$recruit` face is the joiner, every path through every recruit beat adds exactly one hero, story beats grow the trio to five by Shardfall, recruits arrive at the roster's experience, Test Mode's headless join, JoinHero validation. Harness: `python tools/harness.py recruits` picks three on the real party select, clears Tithe Moors I and checks the recruit dialogue plays with the joining hero's portrait and the party grows to four; `uicheck` checks a new game starts with three.)*

### 25.18 Rested and the paywall hooks (GG-T232)

Owner 2026-11-05: "everything costs gold now but eventually there will need to be a 'monitization' angle that lets you recover your 'Rested' points … make everything cost 1g and take 1second to complete; but these would be scaling pay walls in the future". Clarified the same day: the hooks are **only** summoning, the Diamond lucky craft (GG-T235) and refilling Rested ("until I call it monetize, it's not"); Rested is **party-wide energy**. Every other price is unchanged. **Amended 2026-11-07 (owner, GG-T343):** Diamonds now also buy the Summon Circle's random draw ([§25.10.2](#25102-random-summon-for-diamonds-added-2026-11-07-owner-gg-t343)) — still through the Summon hook's timed job (`PaywallJob.Diamonds` records Diamonds spent instead of gold, refunded if it fizzles); Diamonds are still bought only through the LuckyDiamond hook, and there is still no store. **Amended 2026-11-19 (owner /goal, GG-T427):** the 1 g / 1 s placeholders are retired for real, tunable gold prices, and Rested gets a second, Diamond-priced refill alongside the gold one, so Diamonds can pay for either a hero (the Diamond summon) or skipping the Rested wait, as the owner asked. **Amended 2026-11-20 (owner, GG-T455):** Diamonds get an exchange rate and a store ([§25.18.1](#25181-the-diamond-exchange-gg-t455-owner-2026-11-20)); the LuckyDiamond hook is retired.

- **Paywall hooks (`Meta/Paywall.cs`):** `PaywallKind` { Summon, LuckyDiamond, RestedRefill }. `Paywall.Cost(kind, save)` (gold) and `Paywall.Duration(kind, save)` (seconds) read one tuning table — still the one spot every future rebalance touches, now holding first-pass real numbers instead of placeholders: `Paywall.SummonGoldPrice` **800** (a deliberate, chosen-hero recruit — a permanent roster asset, priced above a blind draw), ~~`Paywall.DiamondGoldPrice` **250** (one Diamond at the forge's "Buy Diamond" button — a real choice against spending the same gold on gear)~~ (retired by GG-T455 for the exchange), `Paywall.RestedRefillGoldPrice` **100** plus `RestedRefillGoldPerStage` **15** per campaign stage cleared (`HighestClearedStage + 1`), so a refill stays about half to one stage's measured pay (≈210 g a stage at Lv 1–6, 400–600 g from Lv 9; the main campaign ≈9,900 g in one pass, from the 2026-11-17 polish records) — 100 g at the start, ≈460 g at the campaign's end; a flat 60 g was under 3% of what the 10 launches it buys earn, which made waiting and the Diamond refill pointless. Every job still takes **1 s** to land (the ceremony, unchanged). ~~`Paywall.Tag` prints e.g. "100g · 1s".~~ **Amended 2026-11-20 (GG-T519):** `Paywall.Tag` prints the price alone, e.g. "100 G"; a bare "· 1s" meant nothing to a new player. A wait of `Paywall.NotableWaitSeconds` (5 s) or more is named, " · ready in 30s" / " · ready in 2 min" / " · ready in 3 h" (`Paywall.WaitSuffix`, also on the Summon Circle's draw button); a shorter one only plays out as the job's progress bar. There is still no real-money store.
- **Timed jobs:** `Paywall.Start(save, kind, arg, now, out job)` takes the gold and records a `PaywallJob` (kind, arg, `StartedUtc`, `Seconds`, `Paid`) in `SaveData.Jobs` — one per kind at a time — so a long future job survives a restart. `Paywall.Progress`/`Ready`, then `Paywall.Finish(c, save, job, now)` lands the kind's effect (Summon → the hero arrives; RestedRefill → the pool fills; ~~LuckyDiamond → one Diamond for the lucky craft~~ LuckyDiamond → retired by GG-T455: `Paywall.Start` refuses it and an old save's pending job lands as a full gold refund of what it paid) and removes the job; it refuses a job still running. `Paywall.FinishReady` lands every job whose time is up — Camp calls it on build unless one is playing on screen.
- **On screen (`App/UI/PaywallUi.cs`):** `TimedJobView.Begin(now => (result, job), label, onDone)` runs the paying start (a refusal toasts why), persists, then `TimedJobView.Run(job, label, onDone)` shows a modal (`paywall-job`) whose bar (`paywall-gauge`) fills over the job's real duration with a countdown, a coin cue (`ui.buy`) as it starts and `ui.confirm` as it lands, then toasts the result.
- **Rested (`Meta/RestedPool.cs`, `SaveData.Rested`):** one party-wide pool, `Max` **10**. Every stage launch — Adventure, Next stage, Retry, the Crucible — spends **1** (`StageLauncher`/`CrucibleLauncher` through `RestedGate`). It comes back **1 point per 10 real minutes** (`RegenEvery`), never above max; a clock that runs backwards restarts the interval. At 0 a launch is refused — "The party is exhausted — no Rested left. Next point in m:ss." — with a **Refill Rested — 100 G** offer (rising 15 g per stage cleared) that fills the pool and then launches. Camp's side column shows a `camp-rested` window (points, meter, "+1 in m:ss", the gold `rested-refill` button, and now a second `rested-refill-diamond` button — ~~**1 Diamond**~~ **25 Diamonds (or 150 G)** since GG-T455, paid through `DiamondExchange.Pay`, for an instant fill with no 1 s job: `RestedPool.DiamondRefillPrice`/`DiamondRefill`, GG-T427; short of both currencies it offers the Diamond Store); Adventure rows add "· 1 Rested" and the Crucible screen "Costs 1 Rested".
- **Exempt:** Test Mode saves (`SaveData.TestMode`), and `-demo`, `-testmode`, `-autobattle` and harness (`-automation`) runs set `RestedPool.Unlimited`, so bots and automation are never blocked (Camp shows "Unlimited"). A harness run opts in with `-rested`, or the dev endpoint `devrested?points=N&limit=1`.
- *(Tests: `PaywallRestedTests` — each hook's own tunable price, spend and regen, refusal at 0 with the wait, Test Mode exemption, the gold refill job (gold up front, not before its time, fills, one at a time), no gold → refused, the Diamond refill (instant, no job, no gold while Diamonds cover it, the shortfall exchanged from gold; too few Diamonds and gold refused; refused when already rested), a running job surviving a save round trip, summoning through the job at the flat price.)*

#### 25.18.1 The Diamond exchange (GG-T455, owner 2026-11-20)

**Amended 2026-11-21 (owner, GG-T568): Diamonds became Aetherite Shards; renamed Mana Shards by GG-T675 (2026-11-22).** Everything below still holds; only the name
the player sees changed: "Diamonds" reads **Mana Shards** (short form **Shards** on buttons and counters, e.g.
"25 Shards (or 150 G)"), the **Diamond Store** is the **Shard Store**, and the store packs' display titles read
"100 Aetherite Shards" … "2,500 Aetherite Shards" (product ids unchanged). Code ids, element names (`diamond-*`,
`camp-diamonds`), the `ui.diamond` cue and the save shape keep the old word. See [§24.9.1](#2491-the-currencies-wiki-entries-gg-t568).

Owner: *"Like guildwars gold and diamonds need an exchange rate so if something costs diamonds, it can be purchased with gold or with real money, right now lets make it 100 diamonds for $1.00 just to keep it low and honest".*

- **The rate (`Meta/DiamondExchange.cs`, the one tuning spot):** `DiamondExchange.GoldPerDiamond` **6** and `DiamondsPerDollar` **100**, so 100 Diamonds = $1.00 = 600 G — exactly the old one-Diamond gold price, so a gold balance buys what it did before the re-denomination. `mat_diamond`'s `MaxStack` is **999,999**.
- **Prices (re-denominated):** the random summon `DiamondSummon.Price` **100** ($1.00 / 600 G), the instant Rested refill `RestedPool.DiamondRefillPrice` **25** ($0.25 / 150 G), a lucky craft `Crafting.LuckyDiamondCost` **50** ($0.50 / 300 G).
- **Paying:** every Diamond sink goes through `DiamondExchange.Pay(save, price)`: Diamonds in the bag first, the shortfall converted from gold at the rate; if gold can't cover it either it fails with "Not enough Diamonds: N more, or M G to exchange" and takes nothing. `Quote` gives the split (Diamonds used, gold used), `CanPay` the check (with gold kept back for the rest of an action, e.g. a lucky craft's recipe gold). Every Diamond-priced button shows both prices (`DiamondExchange.Label`: "25 Diamonds (or 150 G)"); short of both, tapping it offers the Diamond Store (`DiamondStores.OfferStore`).
- **Buying with gold:** `DiamondExchange.Buy` exchanges N×6 G for N Diamonds, instantly (no timed job). It replaces the retired LuckyDiamond paywall job (600 g for one Diamond over 1 s); an old save's pending one is refunded in full.
- **Real money:** `IDiamondStore` (`Status`, `Connected`, `Purchase(content, save, pack, done)`) sells `DiamondStore.Packs` at the flat rate, no bonus tiers: **100 for $1.00, 500 for $5.00, 1,000 for $10.00, 2,500 for $25.00** (`diamonds_100` … `diamonds_2500`). `Purchase` answers with a `PurchaseResult` (`Granted`, `Pending` for an order awaiting a parent's approval or a slow payment, `Cancelled`, `Failed`, `Unavailable`); `Delivered` reports a purchase that finished outside any call (approved later, or left unfinished by a crash), which the App toasts. The App picks the store (`DiamondStores.Current`, connected at boot): editor and development builds use `DevDiamondStore`, a **test stub** that grants the pack at once for free and says so on screen ("Test store (dev build): purchases are free and land at once."). **Release builds on Android and iOS use `UnityIapDiamondStore` (GG-T468, `App/Services`):** Unity IAP 5 (`com.unity.purchasing` 5.4.4) over Google Play Billing and StoreKit, the four packs as consumables under the same product ids on both stores. The flow is the store's: purchase → `OnPurchasePending` → grant the Diamonds and write the save → `ConfirmPurchase` (Google consumes, Apple finishes), so a crash between the grant and the confirm re-delivers the order instead of losing it; unfinished orders are fetched again at every connect. A store that can't connect or fetch the products says "Store unavailable: …" and sells nothing. The adapter is compiled only when the package is present (`GG_IAP`, from `GridGame.App.asmdef`'s `versionDefines`), so Meta stays engine-free and the dotnet build never depends on it. Any other release build (desktop, WebGL) uses `UnconnectedDiamondStore`, which shows the packs but answers every purchase with **"Store not connected yet"**.
- **The purchase ledger (GG-T469, `Meta/PurchaseLedger.cs`, save v11):** `SaveData.Purchases.Granted` maps every store transaction id already granted (Google's purchase token, Apple's transaction id) to its product, Diamonds, time, store and refund state. `PurchaseLedger.Grant` pays a transaction **exactly once** (a re-delivered id answers `AlreadyGranted` and is just finished again); paid Diamonds ignore the bag's cap (the money was taken). The IAP store grants through it, writes the save with `SaveStore.Write` and only confirms the order when the write succeeded; a failed write leaves the order unfinished so it comes back. An order that arrives with no save loaded (the title screen) waits and is granted to the first save loaded; one already granted in another slot of the profile is just finished. **Pending/deferred:** a deferred order (Ask to Buy, Family Link, a cash payment) answers `Pending` and lands through `Delivered` when it clears, even on a later launch; `ExistingPurchasePending` answers `Pending` too. **Failures and cancels** grant nothing and say "Nothing was charged". **Refunds:** `PurchaseLedger.Refund` takes back only the Diamonds still in the bag (never gold, never below zero, idempotent) and marks the record; Apple's revocation callback (product only) refunds that product's latest unrefunded purchase (`RefundLatest`). Google reports refunds of consumed purchases only to a server (Voided Purchases API), so an Android refund after delivery is not clawed back. **Restore:** finished consumables can't be restored on either store; unfinished orders are fetched again at every connect, and on iOS a **Restore purchases** row (`diamond-restore`) asks StoreKit to re-send them. The save itself (with its Diamonds and ledger) comes back on reinstall through the platform's device backup (Android Auto Backup, iCloud backup). A v10 save migrates to an empty ledger; an older build refuses a v11 save rather than drop the ledger.
- **The account wallet (GG-T567, model built 2026-11-23 by GG-T620, `Meta/AccountWallet.cs`):** Diamonds belong to the store login, not the save. `AccountWallet` holds one Diamond balance and one purchase ledger (`Granted`, the same `PurchaseRecord`s) per store login on the device, shared by every profile and slot. `AccountWalletStore` keeps it at `Profiles/.account/wallet-<key>.json` (the dot folder is never listed as a profile); the key is the first 16 hex digits of the login's SHA-256, or `device` until the store names a login, so each phone or login has its own wallet and no account name reaches the disk. Writes are atomic with a rolling `.bak`, and a damaged file loads the backup instead of an empty wallet. `AccountWallets` has the ledger's rules over it: `Grant` once per transaction (paid Diamonds ignore caps), `Add`, `Spend` (all or nothing, never overdraws), `Refund`/`RefundLatest` (only what's left, idempotent) and `TotalBought`. **Moving saves in (GG-T621, `Meta/WalletMigration.cs`):** `WalletMigration.Migrate` carries a save's bag Diamonds and its purchase ledger into the wallet, once. A save is known by its identity, `save-<CreatedUtc ticks in hex>`, which every copy of it shares (another slot or profile, a `.bak`, a cloud copy, an export); the wallet's `Migrated` map keeps, per identity, the most Diamonds any copy held when carried in, so a later copy or an older backup adds only what it holds beyond that max and nothing is paid twice. A moved save gets the flag `wallet-moved:<Diamonds drained>:<wallet key>` (a flag, so no save-version bump) and its bag is emptied of Diamonds; a flagged save is never carried again. Ledger records the wallet lacks are merged in without re-paying (their Diamonds were already in the bag), and a refund the save saw marks the wallet's copy refunded with what was clawed back. The save keeps its own ledger untouched for rollback. Diamonds found in the bag of an already-moved save are stray: reported (`Stray`), never carried, since carrying them could pay a rolled-back copy twice. `WalletMigration.Run` writes the wallet first and the save after; a failed wallet write puts the save back (`Undo`) and it is tried again on the next load, and a crash between the two writes adds nothing on the rerun. **Wired (GG-T702):** at boot, before the real-money store connects (`GameApp.OpenWallet`, then `DiamondStores.Warm`), the device's wallet is bound as `DiamondExchange.Wallet` and every slot of every profile is moved into it (a boot move keeps the save's `UpdatedUtc`, so the most recent slot doesn't change); every load (Continue, Load Game, a chosen cloud copy) and every new game runs it again (`GameApp.MoveToWallet`). From then on a moved save's Diamonds are the wallet's: `DiamondExchange.Have`/`Pay`/`Buy`/`Grant`, `DustEconomy.Refine`, the fizzled-summon refund (`Paywall.Finish`), the Rested refill, the Diamond draw and the lucky craft all read and write the wallet through `DiamondExchange.Add` (no bag cap in the wallet), and the Camp/Hub/Shard Store counters and the automation state show its balance. A save that isn't moved (no wallet bound: tests, sims, the headless/demo/test sessions) keeps using its bag. A wallet change marks it dirty and `GameApp.Persist` writes the wallet before the save, skipping the save write when the wallet's failed. The IAP store grants paid orders into the wallet (`AccountWallets.Grant`, no save needs to be loaded), writes it and only then finishes the order; Apple's revocation takes its refund out of the wallet (`RefundLatest`). Cross-profile refunds against the account ledger: GG-T622. *(Tests: `AccountWalletTests`, `WalletMigrationTests`.)*
- **Screen (`DiamondStoreScreen`, `diamonds`, menu kit):** the balance (`diamond-balance`, `diamond-count`) with the rate; an "Exchange gold" window with an amount picker (`diamond-amount-minus100/-minus10/-plus10/-plus100/-max`, default 100), the gold it costs (`diamond-quote`) and how many the purse can buy; a "Buy Diamonds" list of packs (`diamond-pack-100` … `diamond-pack-2500`) under the store's status line (`diamond-store-status`, GG-T470: one line for where a purchase stands, classed `purchase-state--ready/processing/success/pending/cancelled/failure/unavailable`: "Processing your purchase…" in warning colour while the store's sheet is up, then the answer in good/warn/dim/bad, or the store's own "Store unavailable: …" when it can't sell). Each pack row shows the **store's localized price string** (`IDiamondStore.PriceOf`, e.g. "1,09 €") once the products are fetched, falling back to the US-dollar list price (`DiamondPack.Price`); the note then says prices are set by the store in the player's currency. **No double taps:** while a purchase or restore is in flight (`DiamondStores.InFlight`, static so it holds across leaving and coming back) every pack and Restore row is disabled and the bought row reads "Processing…"; a store that never answers unlocks after two minutes with a "still pending" note. An unavailable store disables the rows. The screen redraws on its own when the store connects, its prices arrive or it drops. A granted purchase or a gold exchange plays `ui.diamond`; a cancel or pending answer `ui.select`; a failure `ui.error`; a footer with the quote and **Exchange** (`diamond-exchange`, confirms first). Reached from Camp's **Diamond Store** row (`camp-diamonds`), the forge's `buy-diamond`, the Summon Circle's `summon-diamond-store` and every "not enough Diamonds" moment. `Features.DiamondStore` (on) hides the Camp row and the packs when off; the exchange and gold payment stay.
- **Odds disclosure (GG-T503, App Store Guideline 3.1.1 / Google Play "Loot boxes"):** every paid random item shows its odds before purchase, computed by the same Meta code the roll uses (no second copy of the rules). **Diamond summon:** `DiamondSummon.Odds(content, save)` lists every hero the next draw can bring (`DrawOdds`: `ClassId`, `OneIn`, `Chance`, `Label` "1 in 23 (4.3%)") — exactly the wanderer pool, each equally likely — and `DiamondSummon.Pick` draws a uniform index over that same list. The Summon Circle's "Call a stranger" window shows `DiamondSummon.OddsLine` ("Each of these 23 heroes: 1 in 23 (4.3%)", `summon-odds`) beside an **Odds** button (`summon-odds-btn`, toggles to "Hide odds") that opens the full list (`summon-odds-list`, one `summon-odds-{classId}` row per hero: name · role and its "1 in N (x%)", then "Every hero listed is equally likely. Heroes you already have never come."); the Draw confirm repeats the odds line. The gold summon is a chosen hero, not random, so it has no odds. **Lucky craft:** `Crafting.MasterworkOdds(content, save)` returns the Masterwork chance without and with the Diamond — `Crafting.MasterworkChance`, the function `Craft` rolls against — and the forge's lucky-craft window shows `Crafting.LuckyOddsLine` ("Masterwork chance: 15% without a Diamond, 55% with one", `lucky-odds`); the lucky-craft button now confirms first ("Pay 50 Diamonds for a lucky craft? Masterwork chance 55% (without the Diamond 15%)."). `Craft` fixes the chance before it pays or grants station XP, so a craft that levels the Blacksmith rolls at the chance that was shown. Percentages read through `OddsText.Percent` (one decimal, culture-free). *(Tests: `SummonTests.The_disclosed_odds_are_the_pool_the_draw_picks_from`, `Every_seed_draws_only_listed_heroes_spread_across_the_pool`, `Odds_read_as_a_culture_free_percentage`; `LuckyCraftTests.The_disclosed_lucky_odds_are_the_chances_the_craft_rolls`, `A_craft_rolls_at_exactly_the_shown_chance_even_when_it_levels_the_forge`.)*
- **Bots** (Test Mode, `CampRoutine`, the soaks) never exchange or spend Diamonds. Automation's `economy` adds `goldPerDiamond`, `diamondsPerDollar` and `luckyDiamonds`; `diamondGold` is one Diamond's gold (6) and `summonDiamonds`/`refill.diamonds` the new prices.
- *(Tests: `DiamondExchangeTests` — the rate and stack size, paying with Diamonds only, a gold shortfall, can't afford takes nothing, gold buys Diamonds instantly, the flat packs and only the dev stub granting; `LuckyCraftTests`, `PaywallRestedTests`, `SummonTests` pin the prices to the constants.)*

### 25.13 Roles (added 2026-10-11, owner direction: "a healer should need to heal, a magician should have ranged spells, a tank should be able to taunt")

Every hero class has a `Role` (validated): **Tank** (9), **Healer** (8), **Mage** (6), **Striker** (31), **Support** (8), **Ranged** (2). The role sets the default bar and the learn list; caster power (§13.1.5) makes the stats matter.

**Amended 2026-10-13 — archetype sub-kits:** each role (Ranged too since 2026-11-02, GG-T18 — see below) splits into 3-5 WoW-style sub-archetypes that play differently within the same role (owner: "every combination of tanking type... find Paladin, Warrior, and Druid do things differently"). Every class keeps slot 0 (the role's shared baseline, e.g. Tank's `taunt`) and slot 2 (`item:x_potion`) in common; slots 1 and 4 carry two bespoke abilities per sub-archetype, and slot 3 is the class's own unique signature (unchanged from the original per-class kit pass). A fourth bespoke ability per cluster ("Core") is learnable but not in the starting bar, so there's room to customize without disturbing the default. Sub-archetypes: Tank — Aegis Guard (pure mitigation + cleanse), Juggernaut (knockback aggression), Warlord (party buffs), Deathguard (drain/poison); Healer — Aegis Wardens (preemptive wards), Hexmenders (damage-for-heal), Skirmish-Menders (mobile support); Support — Primal Pack, Tempo Weavers, Field Engineers; Mage — Spellblades, Field Casters; Striker — Berserkers, Duelists, Skirmishers, Combo Brawlers, Attrition, Shadowsteps, Opportunists, Brawlers. Full id list: `tools/icons.json`'s `ability` map, or `docs/USER_STORIES.md` ticket GG-T3.

**Amended 2026-10-13 — a role-wide invariant, found by live play (GG-T14):** every Tank's default bar must keep at least one damage-dealing ability. A lone tank (party wiped around it) can't pincer by itself, so if every bar slot is non-damage or on cooldown/unaffordable, it can soft-lock against a weak enemy neither side can finish — this actually happened to the Aegis Guard cluster's first cut (fixed by swapping its Capstone for its own damage-dealing Core ability). Asserted by `SynergyTests.Every_hero_has_a_role_and_kits_match_it`.

**Amended 2026-10-13 — Ranged (new 6th role):** a precision-attacker archetype distinct from Striker (melee, `AdjacentOnly`) and Mage (AoE elemental) — no ability in a Ranged hero's bar may be `AdjacentOnly`. Two classes so far, `hexsniper` and `hexwarden`, reusing the portraits of the existing `Ranged`-tagged enemies `hag00`/`hag01` (`AttackRange` 3) rather than new art — see GG-T11.

**Amended 2026-11-02 — Ranged sub-archetypes (GG-T18):** Ranged now splits into the Ranger tree's own three branches (§25.6.1, GG-T74), one class each, on the same slot convention as every other role: slot 0 the role baseline `ranged_quickshot`, slot 1 the branch Opener, slot 2 `item:x_potion`, slot 3 the class signature, slot 4 the branch Capstone (the branch Core stays learnable, off the starting bar). **Marksman** (patient single-target burst) — `hexsniper`: `marksman_deadeye_shot` / `hexsniper_piercingshot` / `marksman_execution_shot`. **Trapper** (slows, roots, silences) — `hexwarden`: `trapper_binding_shot` / `hexwarden_snareshot` / `trapper_snare_net`. **Skirmisher** (lane pressure + falling back) — new class `hexstalker` (Hex Stalker): `skirmisher_covering_volley` / `hexstalker_fadingshot` (*Fading Shot*: 14 Dark, Blinded, caster steps back 2) / `skirmisher_rapid_barrage`; added to the Summon Circle pool. The shared `ranged_calledshot`/`ranged_suppressingfire` stay in every Ranged class's learnable list (and the tree's nucleus). Art: `hexstalker` reuses the portrait/thumb of `hag02`, a common `Kiter`-brain hag mook that appears across many encounters — the same "defector" reuse as `hag00`/`hag01`, which is what this ticket was waiting for; the named miniboss `swampmistress00` is still deliberately not reused.

Universal cross-class pool (added 2026-10-13, GG-T4): `AbilityDef.Universal` — any hero can learn a `Universal`-flagged Skill regardless of role, extending the pre-existing rule that every `Spell` is already learnable by any class (`Abilities.Learnable`).

| Role | Job |
|---|---|
| Tank | draw the enemies, protect casters, at least one damage option always in the bar |
| Healer | mend and cleanse (WIS) |
| Mage | ranged elemental AoE damage (INT) |
| Striker | pincer damage, melee skills (STR) |
| Support | tempo — Quicken allies, Slow foes, rally the party |
| Ranged | precision single-target/line damage that never needs to be adjacent |

The starting trio is a full triangle: Paladin (Tank), Barbarian (Striker), Cleric (Healer). Role badges show on the Camp cards, Party, Status (with a one-line "what it's for") and the battle panel. The Abilities screen can **move** a slot along the bar (`Abilities.Swap`). *(Tests: `Every_hero_has_a_role_and_kits_match_it`, `The_bar_can_be_reordered_within_unlocked_slots`.)*

## 26. Responsive Design & Aspect Ratio

### 26.1 Reference resolution

**1170 × 2532** portrait (`GameApp.DesignWidth/Height`) is the **minimum** stage: every screen must fit in it. The live size is `GameApp.StageWidth/StageHeight` (design pixels), between 1170×2532 and 1424 wide (`MaxStageWidth`, 9:16) or 2990 tall (`MaxStageHeight`, 9:23; GG-T706). Screens lay out vertically from the top in fixed design pixels and horizontally against the live width (left/right insets, or centred on `StageWidth`), so extra width or height is absorbed, never cropped.

### 26.2 Panel scaling

`PanelSettings`: ScaleWithScreenSize, MatchWidthOrHeight; `GameApp.FitStage` sets the reference resolution to the live stage size and `match` to the limiting axis whenever the screen size changes, then raises `GameApp.StageResized`.

### 26.3 Adaptive stage (GG-T169)

**Amended 2026-11-16 (GG-T398; owner 2026-11-05: "every screen must scale to fit the supported aspect ratios (9:23 to 3:4) and resolutions, using the extra space instead of leaving dead margins"):** `GameApp.StageSizeFor(aspect)` grows the stage from the 1170×2532 minimum up to 1899 wide (3:4, `MaxStageWidth`) or 2990 tall (9:23, `MaxStageHeight`), so every shape from 9:23 to 3:4 fills the screen edge to edge; only shapes outside that range clamp to the nearest supported stage inside the `stage-frame` outline. Scaling is always uniform.

- **Content column:** `Fit.Column` is the stage width capped at `Fit.ColumnMax` (1424, the 9:16 width), centred by `Fit.Inset`; menus, windows, results, profiles, character select, credits, crawls, dialogue and merc renewal sit in it, so a tablet's extra width goes to the backdrop. `Fit.C(pct)` sizes within it. **Amended 2026-11-21 (GG-T550; owner: "every screen scales to fill all aspects 9:23–3:4 with consistent gutters, no dead margins"):** on a 3:4 tablet that cap left ~240-design-px empty backdrop bands either side of every page (Adventure, vendors, Items, Heroes, settings, results, dialogue) while the hub and battle filled the width. `Fit.ColumnMax` is now `MaxStageWidth` (1899), so the column is the whole stage on every aspect, `Fit.Inset` is 0, and every page's header and windows sit `Kit.Gutter` from the stage edge like the hub's; the battle's ability bar and Party · Lore · Log tabs span it too. Lines still stop at about 60 characters (`Typography.Paragraphs`), and `Fit.Roomy` grids (party cards, window editor) add a column on the wider stage.
- **Camp hub:** three party cards abreast when `Fit.Roomy` (stage ≥ 1600 wide), two otherwise.
- **Battle:** the board centres, top bar and timeline span the stage, the ability bar, orbs and Party · Lore · Log tabs keep to the content column; the tabs take any extra height.
- **Resize:** the router rebuilds the current screen on `StageResized` (battle re-lays itself out; dialogue, crawl and credits opt out via `RebuildOnResize`).
- **Verification:** `harness.py aspects` runs 9:23 … 3:4 (must fill) plus 9:25 and 1:1 (must be framed).

**Amended 2026-10-21 (owner: "on-device experience is king — fill every phone, frame tablets, never warp"):** `GameApp.StageSizeFor(aspect)` picks the stage: on a screen wider than 19.5:9 the height stays 2532 and the width grows to match, up to 1424 (9:16); on a taller one the width stays 1170 and the height grows, up to 2730 (9:21). Inside 9:21…9:16 — every mainstream phone — the stage fills the screen edge to edge with no bars. Outside it (tablets 3:4/2:3/10:16, ultra-tall shapes) the stage clamps to the nearest supported shape, is centred, and a `stage-frame` outline (the same white rounded frame every window uses) sits 10 px outside it over a near-black bezel, so the bars read as deliberate. Scaling is always uniform.

- **Battle:** the board is centred on the stage width (`BoardLeft = (StageWidth − 1080)/2`, 45 at the minimum), the timeline stretches (`StageWidth − 230`), cut-in anchors mirror against `StageWidth`; the bottom info panel is pinned top and bottom, so it absorbs extra height. `BattleScreen.ApplyStageLayout` re-applies these on `StageResized`.
- **Automation:** design-pixel taps map through the live stage width; `stagebounds` reports `design` (live stage size) and `framed`.
- **Verification:** `python tools/harness.py aspects` launches at 9:23, 9:21, 9:20, 9:19.5, 9:18, 9:16, 2:3 and 3:4, shoots title/hub/battle, runs the layout audit, and fails if the stage drops below the minimum or the frame state doesn't match the rule.

**Amended 2026-11-23 (GG-T706; owner: "don't put ability bars around the border, scale the game to a valid AR and put blue mystic fog in the out of bounds black area"):** the valid range is **9:23 … 9:16** (aspect w/h 0.391 … 0.5625). `MaxStageWidth` is back to **1424** (9:16); `MaxStageHeight` stays **2990** (9:23). Every shape outside the range gets the nearest valid stage, centred, uniformly scaled, inside the `stage-frame` outline, and the out-of-bounds area is the **blue mystic fog** (`App/UI/MysticFog.cs`): a deep night-blue ground (#090D1C) under three soft fog banks from one procedural noise texture, tinted deep indigo, Manalith blue #7590C8 (§11.5) and a pale mana glint, drifting against each other on 29–61 s periods with a 13 s breathing glow; **Reduce Motion** holds them in the rest pose. The fog is hidden and idle while the stage fills the screen. This withdraws GG-T398's "fill to 3:4" and GG-T523's side-column ability bar. `Fit.ColumnMax` follows `MaxStageWidth`, so `Fit.Roomy` (≥ 1600) no longer triggers. GG-T699 and any margin audit measure the letterboxed stage rect (`stagebounds`), not the window.

Why 9:16 … 9:23: the layout is designed at 9:19.5 (1170×2532); the vertical stack (board 1080 wide, HUD rows, info tabs that absorb height) only adapts along height, and extra width beyond ~1424 produced dead bands or the side-bar mode. Every mainstream phone sits inside; a few percent of extra width (10:16, 2:3) is not worth a second layout, and the owner prefers a deliberate bezel.

| Device class | Typical shape (w:h portrait) | Aspect | Result |
|---|---|---|---|
| Older 16:9 phones, iPhone 8/SE | 9:16 | 0.5625 | fills (widest valid) |
| 18:9 / 19.5:9 / 20:9 phones (iPhone X–16, Pixel, Galaxy S) | 9:18 … 9:20 | 0.5 … 0.45 | fills |
| 21:9 phones (Xperia 1/5/10) | 9:21 | 0.429 | fills |
| Foldable cover screens (Galaxy Z Fold 5/6) | ≈ 9:22 … 9:23 | ≈ 0.39–0.41 | fills |
| Fold 3/4 cover, other ultra-tall | ≈ 9:24.5+ | < 0.391 | letterboxed: fog above/below |
| Old 3:5 phones/tablets (WVGA, 1024×600) | 3:5 | 0.6 | letterboxed: thin fog side bands |
| Android tablets 10:16 (16:10) | 10:16 | 0.625 | letterboxed |
| Tablets 2:3 (Surface, some Android), Chromebook tablets | 2:3 | 0.667 | letterboxed |
| iPads 3:4 | 3:4 | 0.75 | letterboxed |
| Foldables opened (Z Fold inner, Pixel Fold) | ≈ 5:6 … 6:5 | ≈ 0.85–1.2 | letterboxed (wide fog bands) |
| Chromebooks / desktop windows (landscape 16:9, 16:10, 3:2) | 16:9 … 3:2 | 1.5–1.78 | letterboxed (fog either side) |

**Amended 2026-11-23 (GG-T724; owner: "some devices are just too small for this game; this game should be targeting flagship models that can actually run games" and "for now lets just focus on iPhones, Samsung Galaxy and Google Pixel releases").** GridGame targets **flagship iPhone, Samsung Galaxy (S and Z) and Google Pixel phones from 2021 on**. The table above still describes what the stage *does* on any shape; this one is what we *design and test for*. Figures are portrait px (w×h) and logical width in pt (iOS) or dp at the default display size (Android), from the makers' spec pages (Apple, Samsung, Google Store) and GSMArena, checked 2026-11-23; a "≈" figure is the panel class, not re-verified for that exact model.

| Family | Model (year) | Physical px | Logical | Aspect w/h | ppi | SoC | Result |
|---|---|---|---|---|---|---|---|
| iPhone | 18 Pro / 18 Pro Max (2026) | 1206×2622 / 1320×2868 | 402 / 440 pt | 0.460 | 460 | A20 Pro | fills |
| iPhone | Duo (foldable, 2026) outer / inner | 1398×2034 / 1878×2670 | ≈466 / ≈626 pt | 0.687 / 0.703 | 460 / 430 | A20 Pro | **letterboxed** on both screens (fog side bands) |
| iPhone | 17 / 17 Pro / 17 Pro Max / Air (2025) | 1206×2622, 1206×2622, 1320×2868, 1260×2736 | 402 / 402 / 440 / 420 pt | 0.46 | 460 | A19 / A19 Pro | fills |
| iPhone | 17e (2026), 16e (2025) | 1170×2532 | 390 pt | 0.462 | 460 | A19 / A18 | fills |
| iPhone | 16 / 16 Plus / 16 Pro / 16 Pro Max (2024) | 1179×2556, 1290×2796, 1206×2622, 1320×2868 | 393–440 pt | 0.46 | 460 | A18 / A18 Pro | fills |
| iPhone | 15 / 15 Plus / 15 Pro / 15 Pro Max (2023) | 1179×2556, 1290×2796 | 393 / 430 pt | 0.461 | 460 | A16 / A17 Pro | fills |
| iPhone | 14 / 14 Plus / 14 Pro / 14 Pro Max (2022) | 1170×2532, 1284×2778, 1179×2556, 1290×2796 | 390–430 pt | 0.46 | 460 | A15 / A16 | fills |
| iPhone | **13 / 13 Pro / 13 Pro Max (2021): minimum** | 1170×2532, 1284×2778 | 390 / 428 pt | 0.462 | 460 | A15 (floor) | fills |
| iPhone | 12 mini / 13 mini; SE 2nd/3rd gen | 1080×2340; 750×1334 | 375 pt | 0.462; 0.562 | 476; 326 | A14 / A15 | **below the minimum screen: not designed for** (below) |
| Galaxy S | S26 / S26+ / S26 Ultra (2026) | 1080×2340, 1440×3120, 1440×3120 | ≈384–412 dp | 0.462 | 411 / ≈513 / 498 | SD 8 Elite Gen 5 / Exynos 2600 | fills |
| Galaxy S | S25, S24, S23 series (2023–25); **S22 series (2022): minimum** | 1080×2340 or 1440×3088–3120 | ≈384–412 dp | 0.462–0.466 | 416–505 | SD 8 Elite … SD 8 Gen 1 / Exynos 2200 (floor) | fills |
| Galaxy Z Fold | Fold8 Ultra (2026), Fold7 (2025) cover | 1080×2520 | ≈411 dp | 0.429 (9:21) | ≈422 | SD 8 Elite (Gen 5) | fills |
| Galaxy Z Fold | Fold8 (2026, the 4:3 "wide" model) cover / inner | 1248×1972 / 1848×2448 | — | 0.633 (≈10:16) / 0.755 | ≈420 / 404 | SD 8 Elite Gen 5 | **letterboxed** (fog side bands) |
| Galaxy Z Fold | Fold6 (2024), Fold5 (2023), Fold4 (2022) cover | 968×2376, 904×2316 | — | 0.407 / 0.390 | ≈402–410 | SD 8 Gen 3 … 8+ Gen 1 | fills (Fold4/5 at the 9:23 edge) |
| Galaxy Z Fold | inner screens, every generation but the Fold8 | ≈1812–1968 × 2160–2184 | — | ≈0.83–0.90 | ≈368–374 | — | **letterboxed** (wide fog bands) |
| Galaxy Z Flip | Flip8 (2026), Flip7 (2025) main | 1080×2520 | ≈411 dp | 0.429 (9:21) | ≈400 | Exynos 2600 / 2500 | fills |
| Galaxy Z Flip | Flip6 / 5 / 4 (2022–24) main | 1080×2640 | — | 0.409 (9:22) | ≈425 | SD 8 Gen 3 … 8+ Gen 1 | fills |
| Galaxy Z Flip | cover screen (FlexWindow, 3.4–4.1", ≈1:1) | e.g. 948×1048 | — | ≈0.9 | — | — | **not supported**: the game is a main-screen app |
| Pixel | 11 / 11 Pro / 11 Pro XL (2026) | ≈1080×2424, ≈1280×2856, 1344×2992 | ≈412 dp | 0.449 (20:9) | ≈422 / ≈495 / 482 | Tensor G6 | fills |
| Pixel | 10 series (2025), 9 series (2024), 8 (2023), 7 (2022) | 1080×2400–2424, 1280×2856, 1344×2992, 1440×3120 | ≈412 dp | 0.445–0.462 | 422–512 | Tensor G5 … G2 | fills |
| Pixel | **6 / 6 Pro (2021): minimum** | 1080×2400, 1440×3120 | ≈412 dp | 0.45 / 0.462 | 411 / 512 | Tensor G1 (floor) | fills |
| Pixel Fold | 11 Pro Fold (2026), 10 Pro Fold (2025), 9 Pro Fold (2024) outer | 1080×2342–2424 | ≈412 dp | 0.446–0.461 | 399–422 | Tensor G6 / G5 / G4 | fills |
| Pixel Fold | the original Pixel Fold (2023) outer | 1080×2092 | — | 0.516 | 408 | Tensor G2 | fills |
| Pixel Fold | inner screens (2076×2152; original Fold 2208×1840) | — | — | ≈0.96 / 1.2 | ≈372–380 | — | **letterboxed** (wide fog bands) |
| Pixel | a-series (6a–10a) | 1080×2400 | ≈412 dp | 0.45 | ≈410–430 | Tensor (mid tier) | not a target; same shape and chip family, so it runs |

**Targets (GG-T724).**
- **Minimum hardware: a 2021 flagship chip.** Apple A15 (iPhone 13), Snapdragon 8 Gen 1 / Exynos 2200 (Galaxy S22, Z Fold4, Z Flip4), Google Tensor G1 (Pixel 6). Everything newer in the three families is supported. That floor brings 4–8 GB RAM, Metal 3 and Vulkan 1.1 with it.
- **Minimum OS: iOS 17.0** (`iOSTargetOSVersionString`, raised from 15.0 in GG-T724). Every target iPhone runs it, and nothing it drops (iPhone 8/X) is above the hardware floor. **Android 12 (API 31)** for the Play release bundle: the Galaxy S22 and Pixel 6 shipped with it. *Applied in GG-T725:* `GridGameBuild.cs` sets `minSdkVersion` to API 31 for the release bundle (the project setting alone would not change the bundle, so the floor lives in that file). The development APK stays at 26 so BlueStacks 5 (Android 9) and the emulator suite still install it (docs/RELEASE_BUILD.md). Target SDK stays "highest installed".
- **Aspect range unchanged: 9:23 … 9:16.** Every target phone, and every fold/flip outer or main screen above, is inside it (iPhones ≈0.46, Galaxy S 0.462, Pixels 0.445–0.462, Fold7/8 Ultra covers and Flip main screens 9:21–9:22, Fold4/5 covers at 9:23). Deliberately letterboxed in the mystic fog: every foldable inner screen, the Galaxy Z Fold8's 10:16 cover and both iPhone Duo screens. No flagship falls just outside the range, so it did not move.
- **Minimum screen: a 6.1-inch-class phone, short side ≥ 390 pt (iOS) or ≈ 384 dp (Android, default display size), ≥ 1080 physical px wide.** At 1080 px the stage draws at ≥ 0.92 screen px per design px (1080 / 1170), so `TypeFloor` never engages on a target device.
- **Below the minimum (iPhone SE 2nd/3rd gen, 12 mini, 13 mini, and any non-flagship Android): "not designed for", not blocked.** They still install and render (the SE's 9:16 even fills), but nobody lays out, tunes or tests for them, and a layout problem that shows only there is not a bug. Blocking is a store setting, not a Unity one (below). *Owner decision, reversible:* SE and mini are out (flagships only). Galaxy A-series and Pixel a-series are not targets either, but are not excluded from the store: they share the shape, and recent ones pass the chip floor.

**Withdrawn design rules** (they existed only for small or odd devices; GG-T724). From the GG-T706 table above, these rows are no longer design targets: "Older 16:9 phones, iPhone 8/SE" (9:16 stays the widest supported *shape*, for split-screen windows and the harness, not a device to design for), "21:9 phones (Xperia 1/5/10)", "Fold 3/4 cover, other ultra-tall", "Old 3:5 phones/tablets (WVGA, 1024×600)", and the tablet, Chromebook and desktop rows beyond "it letterboxes". The "Resolution and DPI" paragraph below keeps its maths, but its small-window thresholds (usable down to a ≈ 640-px-tall window, scale ≈ 0.25) are now a desktop-window fallback, not a phone requirement. `TypeFloor` and GG-T711's shrink-to-fit floor stay as safety nets. No code was removed.

**Store settings (the owner; Unity cannot set these).**
- **Play Console › Release › Device catalog:** the simplest rule for "iPhones, Samsung Galaxy and Google Pixel" is to **include only the manufacturers Samsung and Google**, then exclude the Galaxy A/M/F series if they shouldn't see it, plus a rule *RAM < 6 GB* and *SoC older than Snapdragon 8 Gen 1 / Exynos 2200 / Tensor G1*. Exclude tablets and Chromebooks (form factor) unless a letterboxed phone game is wanted there. The API 31 floor comes from the release bundle (GG-T725).
- **App Store Connect:** iPhone only; minimum iOS comes from the Xcode project (17.0). Apple can't exclude by model, and no hardware capability cleanly separates the SE/mini from the targets, so they stay "not designed for". `ProjectSettings` `targetDevice: 2` is iPhone + iPad; switching the export to iPhone only (0) belongs in the `BuildIOS` entry when it is written (GG-T479, docs/RELEASE_BUILD.md), since iPads letterbox anyway.

**Verification:** `python tools/harness.py aspects devices` runs the target matrix by name (`DEVICE_ASPECTS`: one window per flagship screen shape, plus the letterboxed fold inners); plain `aspects` keeps the generic sweep that `tools/polish.py` uses.

**Resolution and DPI.** Scaling is one uniform factor (screen px per design px = limiting screen axis ÷ stage axis incl. the 36-px frame pad); it is not clamped up or down, so a 4K window or a 460-dpi phone just draws sharper. The minimum usable stage is the design stage itself, 1170×2532 design px; what limits a small or low-dpi screen is text size, which `TypeFloor` (Tokens.cs) clamps: no line drawn below 12 screen px or 9 dp, raising the body/caption sizes in steps (32 … 48 design px). Thresholds: the stage is fully usable down to a window ≈ 640 px tall (scale ≈ 0.25: 132-design-px tap targets ≈ 32 px, the text floor active; a 1366×768 laptop window is ≈ 0.27); below that targets shrink under 32 px and the shape is unsupported (it still renders, never cropped). A phone at 1080 px wide or more draws at ≥ 0.76 with no floor. Very high DPI needs no clamp.

### 26.4 Camera

One UI-only camera (`cullingMask = 0`, black clear); the board is UI Toolkit, so there is no orthographic framing.

### 26.5 Safe area

`GameApp.ApplySafeArea` insets the `screens` container where `Screen.safeArea` (notch, home bar) overlaps the stage — which on a phone that fills the screen is every time. `-safeArea <topPx>,<bottomPx>` simulates one on desktop.

**Amended 2026-11-20 (GG-T483):** content and buttons stay inside the safe area, the art does not. A screen's backdrop layers (each leaf `fill` child of its root: the background picture, a shade) reach out under the insets (`GameScreen.BleedBackdrops`, on every mount, rebuild and inset change), so no screen shows a black band under a notch or gesture bar; a page header's band reaches up to the top edge too (`Ui.Header`: negative margin + padding of `SafeTop`, controls below the inset). A change of insets (upside-down portrait, the endpoint below) is noticed like a stage resize: `GameApp.StageResized` fires and the current screen rebuilds (`Fit.H` reads the new usable height). Faking one: `-safeArea top,bottom` (window px) at launch, or the automation `safearea?top=90&bottom=48&ref=2400` (px quoted at a 2400-px-tall phone, scaled to the window; `clear=1` goes back to the device's; no arguments reports `safeTop`/`safeBottom` in design px). The game is portrait-only, so left/right insets (a landscape punch-hole) are not applied. **Verification:** `python tools/harness.py safeareas` (9:23 and 3:4, a 90-px notch and 48-px gesture bar at 2400 tall: title, Camp, Merchant, results, battle, layout audit, recorded).

### 26.6 Units and overflow (GG-T272, GG-T271; owner 2026-11-05)

Owner: "measure things in agnostic units" and "all overflow should be avoided when possible, when not possible automatic marquee that prints out the rest and then resets back to left".

- **Units.** Design px already scale uniformly to every resolution (§26.2), so type (`TypeScale`), spacing (`Space`, 8-px scale with a 4-px half step) and fixed-size art and controls (`Size`) stay in design px. What design px don't absorb is aspect, so layout widths and vertical bands that should breathe use stage units: `Fit.W(%)` / `Fit.H(%)` (percent of the safe-area stage, `GameApp.SafeTop/SafeBottom`) or flex/`Length.Percent`, and every full-width panel sits `Fit.Edge` (24) from the stage edge. No App or USS margin, padding, size or position is a bare number any more (`tools/polish_lint.py` POL-C1). The battle board's 1080-px grid and its HUD rows stay fixed game geometry, named beside `BoardTop` (`BannerTop`, `InfoPanelTop`, `TopBarHeight`, `TimelineFrameHeight`).
- **One GUI text size (GG-T770, owner 2026-11-23: "calculate the right font size based on aspect ratio, dpi and resolution, then use that for all gui, bold and italic and colors and underlines and strikethrough are allowed but all these different font sizes make it look messy").** Every GUI label (titles, headings, rows, buttons, tabs, captions, hints, badges, banners, cut-in tags, combat floats) draws at **one** size, `TypeScale.Base` (USS `--type-base`). `GuiType` (Tokens.cs) works it out on every stage fit (`GameApp.FitStage` → `GuiType.SetScreen`) and on Settings → Text size (`TextSize.Apply` → `GuiType.SetTextSize`): the design size that draws **14 dp** at the stage's scale (screen px per design px, set by the aspect's limiting axis) and `Screen.dpi` (160 when unknown), clamped to **40–56** design px (a desktop window or tablet never draws smaller than the reference phone, 1170 px at 460 dpi = 40), times Text size (100/115/130 %), raised to the `TypeFloor` legibility floor (12 screen px / 9 dp), capped so a line across the stage still holds 28 characters, and rounded to 2 px. The UI root takes the matching `type-base-40` … `type-base-72` class, so every USS-sized label follows live; sizes set in code read `TypeScale.Base` when their screen builds. **Emphasis is never size:** a title or heading stands out by weight (bold, the Heavy font role) and colour, meta by a dimmer colour; italic, underline and strikethrough are the other tools. The only text not at the base size is the board's fixed geometry — marks inside a tile (GUEST/TAMED tag, cast-chip letter, acted tick, status count, region counter: `BoardView.TileLabelFont`) and tile-filling pictograms (trap skull, region glyph: `TileGlyphFont`) — and icon glyphs sized to their icon (mana pips, the bounty skull). `TextFit` shrink-to-fit (GG-T711) is only an overflow fallback: a label starts at the base and shrinks (down to `TypeFloor.ShrinkFloor`) only when a word does not fit its box. Automation: `stagebounds` reports `typeBase`.
- **Overflow.** Text wraps where it can. A single line that can't (a menu row's value — prices, counts, "Lv 1 · 5 waves" — or a fixed-width name) is a marquee (`Ui.Marquee`, `App/UI/Marquee.cs`): it sits in a clipping box; when the text is wider than the box it holds (`Timing.Cinematic`), scrolls left at 90 px/s with `Ease.InOut` until the end shows, holds, fades out, snaps back to the start, fades in and repeats, on pause-aware Tween time. With Reduce Motion it shows an ellipsis instead. Menu-row values never shrink below their text and never take more than 55% of the row, so the row's label wraps instead (before: prices broke as "30 / G" beside a two-line name). The layout audit counts a marquee label as handled, not as overflow, and measures wrapped text with a 1-px rounding allowance.
- **Amended 2026-11-06 (owner, GG-T305: "perfect text wrapping on all things (hyphenated or word wrap as an option), and one-line things like buttons have a restarting marquee that scrolls the info, flashes 3 times, then resets to left-most and begins again").** Nothing opts in any more: `TextFit` (`App/UI/TextFit.cs`, installed on the panel root by `GameApp`) passes over every TextElement every 120 ms.
  - **Single line** (`white-space: nowrap`) wider than its box — a button, tab, menu-row value, name, chip, badge or header — is a `Marquee` (`App/UI/Marquee.cs`, the one shared component; the party-card name and menu-row values use it like everything else). The element keeps its text and layout (its own glyphs drawn invisibly with a leading `<alpha=#01>`), and an absolute clipping overlay laid over its content box (a sibling: a TextElement with a child loses its text measure) draws the same text in the same font, size, colour and shadow. Cycle: hold at the left 1.2 s → scroll at 90 px/s (`Ease.InOut`, at least 0.7 s) until the end shows → hold 0.3 s → **flash three times** (opacity 1 → 0.2 → 1, 0.16 s each way) → snap back to the left → repeat. Only while the text overflows; text that fits is static and untouched. Pause-aware (`Tween.Dt`) but not sped up by Battle speed. Reduce Motion: an ellipsis. `Ui.Marquee(label)` now only lets a label shrink below its text; a menu-row value's 55% cap is in px from the row's width (a percent max-width on a text node resolved against the wrong base).
  - **Wrapped text** (`white-space: normal`) is laid out by `Meta/TextBreaking.cs` `LineBreaker` against the label's real width and font: greedy like the engine, then no lone word on the last line when the word before it can come down too (never stranding a lone dash or letter above it), no single-letter word ("a", "I", "&", a dash) ending a line, and an overlong word hyphenated at a break point (or split with a hyphen) instead of being cut mid-glyph. When the engine's own wrap is already clean the text is left exactly as given; otherwise the label gets explicit line breaks and a minimum width of its longest line + 2 px (so the engine never re-wraps a line by a rounding hair), re-laid when its width, its parent's width, its font size, the font or the setting changes, or code sets new text (which must hold still for one pass, so a typewriter is never re-flowed mid-line). A GlossaryText word wider than its whole row becomes a wrapped label so it hyphenates too.
  - **Settings → Text wrapping: Word wrap (default) / Hyphenate** (`SettingsSave.Hyphenate`, settings.json `game.hyphenate`), applied live. Hyphenate also breaks a word that would otherwise leave more than 20% of a line empty, at the largest `Hyphenation` point that fits.
  - **Hyphenation** (`Meta/TextBreaking.cs` `Hyphenation.Points/Hyphenate`, `Typography.Hyphenate` for text that lays itself out): deterministic English, words of 6+ letters, at least 2 letters before and 3 after a break, a vowel on both sides. Breaks only where it is sure: before known suffixes (-tion/-sion/-cian/-tial/-cial/-tious/-cious, -ment(s), -ness, -less, -ful, -able/-ible, -ing(s), -thing; a doubled consonant before -ing/-able/-ible splits between the doubles: run-ning, ter-rible, but fall-ing), after un-/dis-/inter- (before a consonant) and over-/under-/counter-/super-, and inside a consonant cluster between vowels at the first split whose right part is a legal onset, never inside ch/ck/gh/ph/sh/th/wh (con-trol, mon-ster, black-smith, lit-tle, sim-ple); a lone consonant between vowels is never split (le-mon vs lem-on is ambiguous). Never a capitalised word (proper nouns), a number, or anything inside a `[keyword]` or a rich-text colour/link/bold span. *(Tests: `TextBreakingTests`.)*
  - **Amended 2026-11-23 (owner, GG-T711: "the sheer quantity of devices is mind boggling, the scaling of text to fit boxes is important it should always try scaling text to fit content before it gives up and wraps").** **Shrink to fit comes first.** Each `TextFit` pass, a text whose words don't fit its content box on one line takes a smaller font (inline `style.fontSize`, from one measurement since width scales with size, then up to 6 corrective 0.5-px steps), down to `TypeFloor.ShrinkFloor` (Tokens.cs): the larger of the badge size (24 design px) and the design size that draws at **12 screen px / 9 dp** on this screen. Only if it doesn't fit even at that floor does it go back to its own size and fall back: wrapped text wraps (and hyphenates as above, so no word is cut); a one-line (`nowrap`) text becomes the marquee, and with Reduce Motion an ellipsis. Nothing clips silently. Re-fitted when its words, its parent's width, the floor (screen shape) or a setting/font changes (the parent's width is read after the shrink lays out, so a parent sized by the label is no feedback loop; a text gives up after 12 re-fits). Code that sets a font size on a shrunk label (a tween, a counter) takes it over for good.
    - **Fit-first:** every one-line text (buttons, tabs, names, chips, badges, headers, menu-row values, captions) and every short wrapped label (menu labels, field values, headings, counts: six words or fewer).
    - **Wraps at its own size (never shrinks):** paragraph text, i.e. `Ui.Para` / `Ui.Note` / `Ui.Empty` (`kit-text`, `kit-note`, `kit-empty`), any wrapped text of more than six words, GlossaryText rows (a sentence built of word labels), and anything with the opt-out class `no-fit` (`TextFit.NoFitClass`; `fit-off` still opts out of fitting altogether). The Mercenaries pitch quote (GG-T700) is `Ui.Para`, so it keeps wrapping. The Abilities bar item label is one line again, `X-Potion ×15`, shrunk to fit its cell (GG-T701 had moved the count to a second line).
  - The layout audits treat a marquee host and its overlay as handled; automation (`clicktext`, `texts`) reads `TextFit.SourceOf`, the words as given; `textinfo?text=` reports how a text element is laid out and fitted; `setting key=hyphenate value=1` flips the mode.

### 26.7 The menu kit (GG-T454; owner 2026-11-20)

**Amended 2026-11-20 (owner, GG-T454: "the alignment and padding and fontsize and whitespace is horrible nothing looks like it fits together every single menu needs to have one standardized look and feel that can be repeated over and over and over and utilizes space as efficiently as possible").** Every menu screen is built from one kit of roles (`Ui.cs`, values in `Tokens.cs` `Kit`, classes `.kit-*` in `Theme.uss`); a screen picks roles, never its own font size, padding or margin.

| Role | Builder | Look |
|---|---|---|
| Page title | `Ui.Header(title, back, …, subtitle)` | heavy title; an optional subtitle (station level, profile) rides under it in the bar in gold instead of a cramped line below |
| Page body | `Ui.PageBody` (HubPage does it) | centred content column; windows sit **24 px** from the stage edge and **16 px** from the header, each other and the bottom |
| Window | `Ui.Panel(title, fill)` | padding **16 / 24**; heavy title; `fill` takes the leftover height |
| Tab strip | `Ui.Tabs` | the window under it sits flush on it automatically |
| List | `Ui.ListPanel` | a window that fills the column and scrolls inside itself; two of them split the height evenly (Equip's slots and bag, GG-T452) |
| List row | `Ui.MenuRow` | label + right-aligned value, body, one **132-px** target tall, 24 px apart |
| Row note | `Ui.Note` | a sentence under a row, body dim, indented to the row label (**72 px**) |
| Section header | `Ui.Section` | heavy gold, flush left |
| Card | `Ui.Card` | portrait **120** (compact cards: 96), heavy name, lines below, its buttons in a row under its text (`Ui.Actions`), same 24-px pitch as rows |
| Fact | `Ui.Field` | a read-only label/value line: dim label in a 220-px column, body value |
| Detail pane | `Ui.DetailPanel` | as tall as its content up to **45%** of the column, then it scrolls; heavy heading + dim meta + body sentences |
| Footer / action bar | `Ui.Footer` | the total on the left, the commit button on the right, at the bottom of the screen (POL-C4) |
| Empty state | `Ui.Empty` | body dim, centred |

**Amended 2026-11-23 (GG-T770):** ~~Type has four sizes (title 60, heading 48, body 40, caption 30).~~ Type has **one** size, `TypeScale.Base` (§26.6 "One GUI text size"); the roles above differ by weight and colour only. **Type has four sizes and one rule:** title 60, heading 48, body 40, caption 30. Anything that wraps is body (`Ui.Para`, POL-A2, the same rule `Typography.Paragraphs` already enforced for long text, now for every sentence); a single line of meta is caption (`Ui.Caption`). The 24-px badge size is gone from menus except the title's version tag. Screens that stay bespoke: the Title, story crawls and credits roll, dialogue, the Level Up and results celebrations and the character select (art-led), the talent graph's node cards and the battle bar's slots (fixed-size cells), the Bestiary grid (`EvenGrid`), and the window editor's live preview (it shows the theme's sample styles).

**Amended 2026-11-23 (owner, GG-T712: "every item or place or selectable must be prefixed with a icon so the user knows what they are pressing").** Every tappable row, card, button and list entry leads with a **kind icon**, so a tap target never reads as plain text (entering a stage used to look like a random line in a wall of text). One call does it: `row.KindIcon(RowKind, Texture2D)` or `row.KindIcon(RowKind, string id)` (`App/UI/RowIcon.cs`, GG-T714). The icon is the kit's `.menu-icon` (class `kind-icon` too), **64 design px** (`Size.Icon`), put right after the row's `.menu-cursor` (first child when it has none), replacing any icon already there. It is **fixed**: `flex-shrink 0`, it never shrinks, and the label shrink-to-fits the width left over (GG-T711). Art goes through `RowIcon.ArtFor(kind, id)`; where a kind has no art the row gets that kind's **emblem** (`KindEmblem`), so a row is never bare. `RowIcon.Badge(icon, RowKind.Cleared | New | Locked)` adds a small corner mark for a stage's state. Emblems are drawn with `Painter2D` as a Manalith-blue (**#7590C8**) disc, pewter rim and pale mark, crisp at any size; **never font glyphs** (GG-LAW-11).

| Kind (`RowKind`) | Icon: art, else emblem |
|---|---|
| Place, Stage | the region's board tile `Tiles/<theme>-00` (`Art.BoardTile`); else place pin (Place) or Enter arrow (Stage) |
| Item | `ItemIcons/<id>-00`; else pouch |
| Ability | `AbilityIcons/<id>-00` (or legacy icon); else bolt |
| Status | `StatusIcons/<id>-00`; else aura ring |
| Hero, Npc, Keeper | the portrait through its Thumbnail framing (swap point `RowIcon.ActorThumb`, GG-T713); else bust |
| Bounty | the bounty skull (`BountySkull`, §22.4) |
| Enter | Enter arrow |
| Back, Close, Confirm | left arrow, cross, check |
| Locked, Cleared, New | padlock, check, four-point sparkle (usually as badges) |
| Menu, Quest, Shop, Station | three bars, exclamation, coin, roof over a door |
| Settings, Save, Quit, Talk | gear, save disk, power, speech bubble |
| Bestiary, NewGamePlus, Crucible, Diamond | open book, loop, chalice, cut gem |

**Entering a stage (owner, GG-T715):** it goes through a confirm step that shows who is going into battle, not straight from the stage row.

**Art to-do (owner):** every kind in the table whose icon is an emblem is a stand-in for real art, to be drawn: place marker, cleared / new / locked badges, Enter arrow, back and close, confirm, quest, station markers, Bestiary, Settings, save and quit, NG+, Crucible, Talk, Diamond Store; plus **own board tiles for the five post-game regions**, which have none of their own yet.

### 26.8 App lifecycle on phones (GG-T480)

- **Leaving the app** (home button, app switcher, an incoming call: `OnApplicationPause(true)`) writes the save first, because Android may kill a backgrounded app without warning. The battle itself is not saved: a battle the OS kills is lost and the save is where it was before the stage (the battle only commits on victory, defeat or retreat).
- **Losing focus** (the notification shade, a system dialog: `OnApplicationFocus(false)`) and leaving both hold the game: the current screen gets `GameScreen.OnAppSuspended`, which opens the battle's pause menu (§GG-T126 pause: the clock, playback and sounds stop), and `AudioListener.pause` holds the music and every ringing effect mid-clip. Coming back brings the sound back; the battle waits on its pause menu for **Resume**.
- Only phones do this. The desktop build keeps running unfocused (the harness drives it in the background) unless it is started with `-pauseOnFocusLoss`.
- **Quitting** writes the save (`OnApplicationQuit`).
- **Android's back button** (and back gesture) arrives as the Escape key and does what Escape does: the screen's `OnBack`, then the router's Back. With nothing to go back to (the Title, the Camp) it asks "Quit the game? Your progress is saved."; Back again cancels the question.

---

## 27. Story crawl (no dialog)

**Amended 2026-11-16 (GG-T386, owner: "after every 5 stages have a story scroll"):** the per-theme crawls are retired. `campaign.json` `Chapters` holds one crawl after every fifth campaign stage (validated: exactly one, in order), played once after that stage's first victory on the way back to Camp, or at the next launch. A save that skipped ahead sees only the latest; a level interlude or a beat crawl due at the same stop waits, so two scrolls never play back to back. Keys `crawl:chapter/<id>`; the beats are outlined in [STORY_OUTLINE.md](STORY_OUTLINE.md). Until the story is written in Prose, the Prologue, the eight chapters, the Ending and every conversation show lorem ipsum (Prose draft batches `chapters-386`, `lorem-387`).

`StoryCrawlScreen`: the theme name (Cinzel) and its `campaign.json` `Crawl` text scroll upward at 95 design-px/s over a dimmed theme backdrop; **Skip ›** or a tap anywhere ends it and replaces it with the battle. It plays on the **first** launch of any stage in a theme — from Adventure or from the results screen's "Next stage ›" (both go through `StageLauncher.Launch`); the theme is recorded in `SaveData.SeenCrawls` (and saved) when the crawl starts. **Amended 2026-10-09** (timing by scroll speed instead of a 26 s duration). **Amended 2026-11-07 (GG-T355):** the crawl's words come from Prose (`crawl:<themeId>`, [§27.2](#272-narrative-sourcing-prose-gg-t355)); `campaign.json` holds no text.

~~No dialog system: no character dialogue, no branching, no cutscenes.~~ **Amended 2026-10-19 (owner, GG-T117):** character dialogue now exists ([§27.1](#271-dialogue-gg-t117)); there are still no cutscenes. **Amended 2026-11-02 (GG-T75):** the caravan crew's rotating one-line barks ([§25.12.1](#25121-the-caravan-crew-and-their-barks-gg-t75-added-2026-11-02-owner-direction-2026-10-16)) are not part of the dialogue system: they are not in `dialogues.json`, have no speakers' portraits, branches, choices, effects or flags, and need no tap. The in-battle "surprise" reinforcement beat ([§14.8](#148-reinforcements-and-eggs-added-2026-10-12-owner-direction)) stays a lightweight one-line announcement card.

### 27.0 The story start to finish (GG-T231, owner 2026-11-05: "make the story from start to finish; title screen, new campaign, text scroll … a story crawl at a certain level … a boss … roll credits")

Owner clarification: a "cutscene" is a conversation ([§27.1](#271-dialogue-gg-t117)) or a text crawl — no new cinematic system. `StoryCrawlScreen` now takes any title, text and backdrop, not only a theme's.
- **Prologue.** `campaign.json` `Prologue` (`CrawlDef`: `Title`, `Crawl`, `Backdrop`) — "Tithe Day": tithe day at Saltmoss and the crew's run with a stolen Concord caravan. Plays once, right after the last lock on character select (`StoryBeats.NewCampaign`), then Camp. Never for automation, Test Mode or `-demo` saves (they don't come through New Game).
- **Level interlude.** Trigger `LevelReached` (`DialogueTrigger.Level`, 2+) fires once any roster hero is at that level. It plays on the way back to Camp from the results screen (`DialogueRunner.PendingInterlude` → `StoryBeats.Camp`), or at the next launch if that comes first (it's an ordinary beat to `PendingBeat`). A story beat may open with its own crawl (`DialogueDef.Crawl`, headed by its `Title`; story beats only, validated). Content: **Lv 10 — "Why We Ran"**, a crawl on how running turned into fighting, then a talk with the Innkeeper where the player names the crew's motive (flags `motive:coin|people|truth|crew`: coin, the holds, the truth about the tithe, each other).
- **Boss introductions.** Trigger `StageStart` (`Id` = stage) plays before that stage's battle, once. Each main-biome finale opens with its boss speaking: Tithe Moors III the Captain (who sold the holds), The Lantern Road III an oni chief on the pass (`oni02`, GG-T345; placeholder draft text until Prose, GG-T355), Shardfall III a shield-walker (`mechaarmor01`) guarding the Concord dig, Arkgrave III the Swamp Mistress (the drowned Ark's pilot), Anchor Wastes III a vat-giant (`cyclops02`) keeping the great Anchor, The Still III the Chronomancer keeping the Still's clocks (GG-T344), Halcyon III a Kiln Guard captain on the spire stair (`ceramicknight06`, GG-T350; placeholder draft text), The Choir's Cathedral III the Hierarch (its own `hierarch` class since GG-T350, speaking with the mannequin placeholder art). Each warns that "the rules don't hold here" in general terms; the mechanics themselves are GG-T234's.
- **Ending.** `campaign.json` `Ending` (`CrawlDef` + `Stage` = `cathedral-03` since GG-T350, was `cityruins-03`; backdrop `cathedral`). The **first victory** over that stage, after LevelUp and PostBattle as always, turns the results screen's Continue into: the ending crawl ("Still Here") → `CreditsScreen` in **rolling mode** (`Resources/Credits.txt` scrolls up over the ruins at 110 px/s, hold anywhere to fast-forward ×4, **Skip ›**, a coda line, `HeroicAge`) → **Title**. "Next expedition ›" is hidden on that one results screen. The save flag `story:ending` (`Story.EndingFlag`) records it; the postgame themes stay open after it and Continue from Title lands in Camp. New Game+ clears the flag, so the ending plays again each cycle. Never on a Test Mode save.
- **Bots** play `LevelReached`/`StageStart` beats headlessly like every other beat (`PlayPendingBeatsHeadless`); they never see the prologue, ending or credits.
- *(Tests: `StoryArcTests`.)*

### 27.1 Dialogue (GG-T117)

- **Content:** `dialogues.json` — a list of `DialogueDef` (id, `Title`, `Trigger`, optional `Conditions`, `Once`, `Backdrop`, `Speakers`, `Start`, `Nodes`). A speaker is a class (its name and portrait), `"$lead"` (the first party member), `"$with"` (the party member a Talk is with), `"$recruit"` (the hero the dialogue's first `JoinHero` brings — fixed when it opens, so the face that speaks is the hero who joins; GG-T118), or a `Name` + `Portrait` (`Resources/Portraits/<id>`). A node is a speaker and a line, then either `Next` (null ends it) or `Choices` (label, `Next`, `Conditions`, `Effects`); a node may carry `Effects` applied on arrival.
- **Vocabulary (the save's, not a script):** conditions `FlagSet`, `FlagNotSet`, `HasItem`, `GoldAtLeast`, `PartyHas`, `RosterHas`, `RosterLacks`, `NpcJoined`; effects `SetFlag`, `ClearFlag`, `GrantGold` (negative = pay, never below 0), `GrantItem`, `RecruitHero` (joins like a Summon Circle recruit, free; no-op if owned), `JoinHero` (GG-T118, story recruits: `Id` and/or `Role`; always brings someone new — `Summon.StoryPick`, [§25.17](#2517-small-start-story-recruits-gg-t118)), `JoinNpc` (sets the flag `npc:<id>`, which `ClearFlag` can't touch; NPCs: blacksmith, alchemist, enchantress, merchant, innkeeper), `GrantSkillPoint` (GG-T133: `Id` a hero class, or `"$with"`/omitted for the Talk partner; `Amount` default 1; paid once per dialogue per hero — the guard flag `sp:<dialogue>:<hero>` is set on the first payout and, like `npc:`, `ClearFlag` can't touch it, so a replayed Talk can't farm it); side quests (GG-T237, [§22.11](#2211-side-quests-added-2026-11-05-owner-direction-gg-t237)) add the conditions `QuestAvailable`, `QuestActive`, `QuestDone` and the effect `StartQuest`. Nothing removes a hero — nobody dies permanently. Flags live in `SaveData.Flags`; finished dialogues in `SaveData.SeenDialogues` (cleared by New Game+, like `SeenCrawls`).
- **Validation (`ContentLoader.Validate`, same severity as a dangling class id):** unknown speakers, classes, items, NPCs, themes, stages or node ids; a missing portrait (the app and dotnet tests pass a portrait lookup); a node unreachable from the start; a node with both `Next` and `Choices`; a choice list with no unconditional choice (so a player can never be stranded); a `JoinHero` with neither `Id` nor `Role`, a non-hero `Id` or an unknown `Role`; a `"$recruit"` speaker in a dialogue with no `JoinHero`.
- **Engine:** `GridGame.Meta.DialogueRunner` walks nodes, offers the choices whose conditions hold, applies effects, and marks the dialogue seen when it ends (dotnet-tested, `DialogueTests`). `DialogueRunner.PlayHeadless` plays one to the end taking the first choice each time, and `PlayPendingBeatsHeadless` plays every beat pending before a stage — how the bots (Test Mode, `BalanceReport`) live through the story they never see (GG-T118).
- **Triggers:** story beats — `ThemeStart` (theme id) and `StageCleared` (stage id) — play once, in file order, through `StageLauncher.Launch`: after the theme's crawl, before the battle. **Amended 2026-10-19 (GG-T119):** every beat pending at a launch plays, one after another (each picked as the last ends), not just the first — a story recruit and a Camp NPC can share a stage, and the app now plays the same set the bots play headlessly. Never on a Test Mode save. **Talk** — Camp's Talk row (`TalkScreen`) lists the party and every joined NPC; each offers its own `Talk` dialogue (`"party"`, a hero class id, or `"npc:<id>"`), unseen ones first and marked **New!**; Talk dialogues repeat unless `Once`.
- **Screen (`DialogueScreen`):** ~~the speakers' portraits overlap across the top — the one talking slides in full-bright and in front, the other dims and shrinks behind; the conversation stacks below as overlapping boxes that slide in from the speaker's side, older ones dimmed, the newest typing out; the choices (or **Next ›**) sit at the bottom.~~ **Amended 2026-11-03 (owner, GG-T225: "one textbox and they keep trading whose talking by lighting up the name of the person talking"):** a conversation is **one shared text box** at the bottom (`dlg-box`); each line replaces the last in it and types out (never a stacked log), and its choices (or **Next ›**) appear inside that same box. The speakers stand on the left and right as their **whole, uncropped portraits** (`Art.Portrait`, contain-fit, feet behind the box — never the §12.1.1 Thumbnail framing), for class speakers and NPC speakers with a `Portrait` alike; a speaker with no art shows only its name plate. Each side has a **name plate** on the box's top edge. Whoever is talking has their plate lit (theme accent fill, bright border, glow) and their portrait full-bright, full-size and nudged toward the middle; the listener's plate greys and their portrait dims and steps back. The swap eases over 0.2 s with a soft slide cue; a speaker stepping on slides in from their edge. The first two speakers take left then right; a third takes the side of whoever is not talking (the one who just spoke stays put), the old occupant stepping off first. A tap finishes the line or advances; **Skip ›** rushes to the next choice — choices are never skipped. Effects toast as they land. `-demo` plays it through on its own. Camp's Talk list shows each NPC's whole portrait; heroes keep their Thumbnail face like every other hero list.
- Sample content: a Lantern Road merchant met at the start of the Tithe Moors (every branch ends with him joining), four story-recruit beats (GG-T118, [§25.17](#2517-small-start-story-recruits-gg-t118)), a Talk with the merchant (hires a sellsword for 300 G) and a generic party chat. **Amended 2026-10-19 (GG-T119):** the beat after Tithe Moors I now meets the Innkeeper, and beats after Tithe Moors II, at the start of the Shardfall and of the Arkgrave meet the Blacksmith, Alchemist and Enchantress (§25.0.1); each NPC has a Camp Talk. **Added 2026-11-02 (GG-T133, owner 2026-10-19 "talk to Heroes, answer right, gain a skill point"):** every hero class has an "Advice" Talk quiz (`talk_quiz_<class>`): the hero voices a worry that fits their faction ([STORY.md](STORY.md) §3; rewritten in GG-T341, each hero with a fresh blind spot), and of three answers only the one that names that hero's own blind spot carries `GrantSkillPoint` (the two wrong ones feed the blind spot or miss it, and earn nothing). Quizzes aren't `Once` — they replay, but the point pays out only the first time it's answered right. An unseen quiz outranks the generic party chat in the Talk row (hero-specific before `"party"`).
- **The story: *Under Morrow* (GG-T341, 2026-11-07; replaces GG-T120's story).** ~~[docs/STORY.md](STORY.md) is the single source for every word of story — crawls, dialogue, quests, caravan barks, mercenary pitches, unit lore, credits.~~ **Amended 2026-11-07 (GG-T355):** Prose is the source ([§27.2](#272-narrative-sourcing-prose-gg-t355)); STORY.md is a pointer, and *Under Morrow* below is retired history. The crew are Saltmoss runaways who fled tithe day in a stolen Concord supply caravan; Morrow, the moon, hangs too low, the Pale Concord tithes every hold's mana to feed the Anchors, and the Hierarch drinks the surplus. The main themes are **The Tithe Moors** (`greenvalley`), **The Lantern Road** (`lanternroad`, added by GG-T345, §22.3.2), **The Shardfall** (`desert`), **The Arkgrave** (`swamp`), **The Anchor Wastes** (`cave`), **The Still** (`still`, added by GG-T344, §22.3.1), **Halcyon** (`cityruins`) and **The Choir's Cathedral** (`cathedral`, the finale, added by GG-T350, §22.3.3); the postgame themes are **The Moors Beyond the Stills**, **Deep Shardfall**, **The Drowned Arks**, **Under the Anchors** and **The Underspire** — the road after the Hierarch, open on purpose. Each `*_party_talk` `ThemeStart` beat carries one piece of the plot (the quota was a lie → the moon is made of what it pulls → the Concord grounded the Arks → the beams carry the surplus to Halcyon → the tanks hold the Hollow, stored → give it back). The Camp NPCs are the Saltmoss Innkeeper, a Free Hold Blacksmith who won't forge for the Captain, a Street splice-cutter Alchemist, a Choir-runaway Enchantress and a Lantern Road Merchant (speaker name "Lantern Merchant"). Ids, triggers, conditions, effects and node graphs are unchanged; only words and NPC speaker names changed. Stage display names, unit `Description`/`Lore` and a separate `hierarch` boss class are the next phase of GG-T341.
- **PPP prose universe seed (GG-T137, 2026-10-20) — retired (GG-T341).** `docs/ppp-universe/` and `docs/story-draft-spider-town.md` belonged to the retired GG-T120 story and are deleted (git keeps them); [STORY.md](STORY.md) replaces both as the prose source.

### 27.2 Narrative sourcing: Prose (GG-T355)

Owner, 2026-11-07: *"all dialog/conversations, descriptions, screen crawls, everything is sourced from prose"*; *"the Game handles gameplay, and Prose handles narrative."*

- **Where the words live.** Every story string a player reads is a text in the Prose universe `gridgame`, applied into `Resources/Content/Text/<kind>.json` (`conversations`, `crawls`, `descriptions`, `quests`, `barks`, `glossary`; schema `gridgame-text/1`, written only by `tools/prose_apply.py` or the one-time seed). Nobody edits those files by hand, and this repo authors no story. Canon (premise, factions, places, characters) is Prose's too; [STORY.md](STORY.md) is only a pointer.
- **Gameplay JSON holds no story text.** Moved out: class `Description`/`Lore`, boss-phase `Announce`, dialogue node `Text` and choice `Label`, every `Crawl` (campaign prologue, ending, themes; a dialogue keeps only `HasCrawl`), quest `Summary`/`TurnIn`, glossary `Text`, item, armor-set, skill-tree and node `Description`, caravan `Barks`, mercenary `Pitch`/`Renewal`, stage `SurpriseLine`. **Stays** (UI and mechanics): names, dialogue `Title`, quest `Giver`, glossary `Term`/`Aliases`, ability descriptions and callouts, class `Card`, item passives, caravan descriptions and level text, the battle tutorial, `Credits.txt`. Triggers, conditions, effects, speakers and choice order stay gameplay.
- **Keys.** A text's key is the game's own id (`class:<id>`, `dialogue:<id>`, `crawl:<theme>`, `item:<id>`, `bark:merc/<class>/pitch`, …; the full scheme is in [PROSE.md](PROSE.md)). A key exists exactly when its record does; the optional ones hang on a marker (`HasCrawl`, `SurpriseSpeaker`, a caravan upgrade's `Crew`).
- **The join.** `ContentLoader.Load` calls `ProseText.Join`, which fills the same model fields every screen already reads; conversations join by node id and choice order. A **missing key** shows `(missing text: <key>)` on screen, is logged as a `[Content]` warning and reported by `Validate`, and never crashes the game. `ContentDb.TryText(key)` exposes each text's Prose id, GUID, version and source (`seed` or `prose`) for debugging and telemetry.
- **Validation.** Every key resolves; no orphan text; no story field left in gameplay JSON (a field put back is reported and Prose's text wins); the conversation's shape matches the dialogue's; the `right-answer` tag marks exactly what the game rewards (the wanderer reply that leads to `heard`, i.e. `Wanderers.RightAnswer`, and the Advice-quiz answer that grants a skill point).
- **The seed.** Until the owner approves, `Content/Text` holds the seed made from the text the JSON held (version 0, `source` `seed`, recorded in `Data/Prose/applied.jsonl`), so the game reads exactly what it did before. Every build pulls published Prose text first (`tools/prose-sync.ps1`) and replaces seeds as texts are published.
- **New content.** A new record's key exists at once (placeholder until its text is approved); its words are drafted in Prose, not written into the JSON ([§20](#20-how-to-add-a-new-enemy--checklist)). Generators seed through `prose_text.upsert_seed` (e.g. `GridGame.Studio/tools/weapons.py`).
- *(Tests: `TextSourcingTests`; `tools/test_prose_apply.py`.)*

### 27.3 The sky: two moons and the staged backdrop (GG-T575 design; GG-T576 builds it)

Owner, 2026-11-21: *"a normal moon to balance things and associate it with the familiar and then an oddly shaped 'weird' moon ... you have perspective when it comes to the scale of things where the Moon is always the same size but this 'weird moon' is getting closer as the game progresses"*; then *"the Somnolith needs to be the same in every picture ... an ever changing Somnolith sucks"*, so backdrops become *"staged"*: background, foreground and the Manalith between, *"a living backdrop (imagine slowly moving shards of Somnolith being dragged across the sky)"*. Canon: Prose `gridgame` sections GG-MANALITH and GG-WHY-NOW (staging copy `Data/Prose/canon/`).

**Status: design only.** Nothing below is built. The art does not exist yet (the owner generates it from the prompts below), and the engine work is GG-T576.

**Amended 2026-11-22 (GG-T679; owner: "replace the picture of the Manalith with an actual slowly tumbling satellite with debris raining off of it"):** the Manalith in the sky is now the 3D **Midnight Geode** model (`Resources/Manalith/manalith-satellite-00.fbx`, decimated to 40k triangles, with its PBR maps), rendered live by `ManalithRig` into the backdrop's Manalith layer. It tumbles slowly on three axes, its rim glows the Manalith's blue (#7590C8), and it sheds dark rock shards, glowing blue specks and now and then a burst of bigger chunks, which drift downward and fade. The flat `Sky/somnolith-00.png` stays as the fallback. Region size and placement are unchanged, but the fixed per-region Rotation no longer applies (it tumbles). Reduce Motion holds its pose and debris still. Every number is in docs/MANALITH_VISUAL_SPEC.md, which SpaceGod's copy of the same satellite (US-T458) shares.

- **The Moon and the Manalith, two rules.** The **Moon** is ordinary: round, pale, the same apparent size and look in every region, the player's fixed yardstick. **The Manalith is not a moon** (owner 2026-11-21, GG-T577: an ancient thing that was stopped long ago and hangs in the sky, orbiting, now coming closer; the prompts below say "moon" only to steer the image generator): one lumpy, potato-shaped, crusted mass (the owner's reference photo, a kidney-stone or desert-rose accretion: bladed crystal clusters, bubbly deep-blue-to-violet lumps flushed with red, glints, pale fibrous tufts; blue like mana, owner art 2026-11-22, GG-T674), with a faint cyan light in the gaps. It is the same object in every scene. Only its apparent size and position change, stepping up with campaign progress.
- **One canonical Manalith asset.** The Manalith is generated once from the owner's reference, cut out to a transparent PNG (`Resources/Sky/somnolith-00.png`, about 2048 px, plus a few shard cut-outs from the same image, `somnolith-shard-00..NN.png`) and composited in-engine. No per-scene repainting, and backdrop art never paints its own Manalith. One cut-out moon sprite (`moon-00.png`) does the same for the Moon.
- **The staged backdrop.** Each battle or region backdrop is drawn back to front:
  1. **Sky plate**: the backdrop's sky only (gradient, clouds, stars), full frame.
  2. **The Moon**: fixed size everywhere, placed per backdrop where the sky is open.
  3. **The Somnolith**: scale and position from the region's sky data (below), with the faint inner glow as an additive pass that can pulse slowly (an effect that plays out over time, Law 5; Reduce Motion holds it still).
  4. **Drifting shards**: a few fragments cut from the same asset, slowly dragged across the sky on long, looping paths, fewer and smaller early on and more of them near the end (the shell breaking, canon 04).
  5. **Foreground plate**: the landscape with the sky made transparent, on top.
  - **Light parallax** between the layers (a few px of drift against the camera or tilt; none under Reduce Motion), so each backdrop is a living scene, not one picture.
  - Interiors (the Still, the Cathedral) show the sky only through their openings: the oculus and the vault window become transparent holes in the foreground plate, and the sky stack draws behind them. Canon says the Still's oculus frames the Manalith.
- **Progress steps.** The Manalith's apparent size rises by chapter in the order of `campaign.json` `Chapters`. Proposed starting values, as the Manalith's height as a fraction of screen height: Tithe Moors (prologue) **0.06**, Lantern Road 0.08, Arkgrave 0.10, Anchor Wastes 0.13, Halcyon 0.16, Bloom 0.20, Underspire 0.25, Shardfall 0.32, **Morrow's Shadow 0.55** (the largest, filling the sky and running off the frame). The Moon stays at **0.07** everywhere, so by chapter 2 the Manalith already outgrows it. Postgame themes use the step of the main region they revisit, or the final step, as tuning decides.
- **Data (tunable, wiki-generatable).** Each `campaign.json` theme gains an optional `Sky` block, e.g. `"Sky": { "Somnolith": { "Scale": 0.13, "X": 0.68, "Y": 0.20, "Rotation": -12 }, "Moon": { "X": 0.22, "Y": 0.14 }, "Shards": 3 }` (X/Y are the sprite centre as fractions of the frame, Y measured from the top; Rotation in degrees, since one odd-shaped asset reads differently turned). A top-level `"Sky": { "MoonScale": 0.07 }` holds the one fixed Moon size. `ContentLoader.Validate` checks the ranges (Scale 0..1, X/Y 0..1, Shards 0..12), and a wiki or bible table can be generated straight from the blocks. A theme with no block falls back to its chapter's step.
- **Existing art must be split.** Every current backdrop is one flat picture, and most of them paint a round moon or planet. GG-T576 splits each one into a sky plate and a foreground plate with transparent sky, and paints out any moon or planet that would fight the two-moon rule. A painted round moon of about the Moon's size can stay as the Moon only if its size and placement agree with the fixed 0.07. The giant spheres in Shardfall and Morrow's Shadow must go, because they are neither moon: they are round and change size.
- **Size budget.** The release APK has about **37 MB** of headroom. One Manalith (about 2048 px), a handful of shards and one Moon sprite cost little. Splitting 81 backdrops into two plates roughly doubles their files, so plates should share sky plates within a region, use compressed textures, keep the foreground at the art's resolution and the sky plate at half (it is soft), and be measured against the budget before they land.

**Backdrop survey (2026-11-21, `Resources/Backgrounds`, 81 images).** "Moon" means a painted round moon or planet; positions are on the portrait frame.

| Set | Images | Painted moon / planet | Where | Note |
|---|---|---|---|---|
| tithe-moors | 00-04 | faint large moon or planet arcs in 00, 01, 02; small pale globes in 03; none in 04 | upper left (00, 02), upper centre (01), centre-right (03) | half-hidden by storm cloud; paint out or keep as cloud |
| lantern-road | 00-03 | full round moon in all four | upper right (00, 03), upper centre-right and large (01), right behind lanterns (02) | about Moon-sized; can become the Moon |
| shardfall | 00-04 | a giant planet-sized sphere in all five | top left (00), centre and filling (01), top centre-right (02), top right (03), right edge (04) | must go: conflicts with the fixed Moon |
| arkgrave | 00-04 | none (a low sun) | sun at centre | sky open behind the masts |
| anchor-wastes | 00-03 | none | | sky only through the arches; small sky area |
| still | 00-05 | full moon in the oculus in all six | top centre | interior: the oculus becomes the sky hole; canon puts the Manalith there |
| halcyon | 00-13 | low pale sun or moon disc at the horizon in 00, 01, 03, 04, 09, 10, 11; a large pale crescent planet in 11 | low centre or centre-left; 11 top right | the disc reads as a setting sun and can stay as a sun; the crescent in 11 must go |
| cathedral | 00-03 | full moon in the vault window in all four | top centre (02: upper middle over the organ) | interior: the vault window becomes the sky hole |
| bloom | 00-07 | large full moon in all eight | upper left to upper centre | larger than 0.07; repaint or replace with the Moon sprite |
| free-holds | 00-06 | full moon in all seven | upper right or centre (00, 02, 03, 04, 06), upper left (01, 05) | about Moon-sized; can become the Moon |
| underspire | 00-08 | small full moon in all nine | mid-left to upper right, varying | about Moon-sized; can become the Moon |
| morrows-shadow | 00-09 | a colossal sphere in all ten | centre to top, filling half the frame or more | must go: the Manalith fills this sky, not a sphere |

**Midjourney prompts (the owner generates them; `--niji 5`, house rule).**

- **(A) The Manalith, isolated, the production asset:**
  `the Somnolith, a grotesque irregular second moon floating alone, lumpy potato shape like Phobos, a giant grown mineral accretion like a kidney stone or desert rose, clusters of flat bladed crystals, bubbly botryoidal deep blue and violet lumps flushed with red, glinting facets, pale white fibrous hair-like tufts, faint cold cyan light glowing from deep cracks between the crystals as if something sleeps inside, unsettling, organic and mineral at once, painted anime fantasy illustration, soft cel shading, painterly texture, whole object centered and fully in frame, isolated on a plain flat white background, no sky, no stars --ar 1:1 --no craters, sphere, round planet, rings, text, background scenery --niji 5`
- **(A2) Image-prompt variant: the owner's reference photo as the image prompt.** Upload `MindAttic.Tickets/data/attachments/GG-T575/2026-11-21-image-4.png` to Midjourney and put its URL first:
  `<reference image URL> this exact mineral accretion scaled up into a colossal moon hanging in space, keep its silhouette, bladed crystal clusters, bubbly deep blue and violet lumps flushed with red and white fibrous tufts, faint cold cyan light glowing from the gaps between the crystals as if something sleeps inside, grotesque and unsettling, painted anime fantasy illustration, soft cel shading, painterly texture, whole object centered and fully in frame, isolated on a plain flat white background --iw 1.5 --ar 1:1 --no craters, sphere, sky, stars, text --niji 5`
- **(B) Mood reference only, not a production asset** (for tone and scale; the game composites the real layers):
  `vertical fantasy landscape at dusk under two moons, a small familiar round pale moon and beside it a much larger grotesque lumpy second moon of crusted deep blue and violet crystal and bubbly mineral lumps flushed with red with faint cyan light in its cracks, small shards of it drifting across the sky, moorland and standing stones below, painted anime fantasy illustration, soft cel shading, atmospheric haze --ar 9:16 --no text --niji 5`

#### 27.3.1 The wake and the Manafall (GG-T670)

Owner, 2026-11-22: *"the Somnolith really needs to be dragging a lot of debris across the sky behind it; that's why it sometimes rains aether dust"*, and *"Aetherain falls"*. Canon: GG-MANALITH ("It drags a wake", "The Aetherain").

- **The wake** (`sky.json` `Debris`, `DebrisDef`): a comet-like trail strung out from the Manalith's centre along `Angle` (192°: up-left, the body coming down and in). Pieces are the shard cut-outs at small sizes plus fine round specks (`SpeckShare`, `SpeckColors`: the Manalith art's own indigo `#2D346C`, blue `#46508E`/`#6D6EA9`, plum `#6B3C6A` and red-violet `#974B72`, each speck picking one; GG-T674). The shard images are cut from the art itself (tools/make_sky_assets.py), so they carry its blue, violet and red. They are densest and largest near the body, shrink from `MaxSize` to `MinSize` and fade with distance, and drift outward over `DriftSeconds` before starting again near the body. The wake follows the body's sway and bob, and sits behind the Manalith and in front of the Moon. Its piece count scales with the region's Manalith `Size` (full `Count` at `FullAtSize` 0.6, never below `MinCount`): 12 pieces at the Tithe Moors, 60 at Morrow's Shadow. A region can carry its own `Debris` block, used as written. Capped at 90 pieces.
- **The Manafall** (`sky.json` `Aetherain`, `AetherainDef`): rolled once per backdrop shown. The chance is `Chance` (0.3) scaled by the Manalith's size, about 5% at the Tithe Moors and 30% at Morrow's Shadow, or a region's own `AetherainChance`. Sparse glints in colours sampled from the Manalith art (`Colors`: mostly its blue `#5D64A3`, with lavender `#A096C7`/`#B2A4D2` and violet-red `#9D6492`/`#974B72` glints, each mote picking one; GG-T674) fall from the wake's band to the bottom of the screen over `FallSeconds`, drifting and twinkling, in front of the landscape but inside the backdrop, so every screen's content stays on top.
- **Cost and motion.** Pieces are sized once and animated with translate, scale, rotate and opacity only (no layout passes, no per-frame allocation). Reduce Motion holds the wake still and skips the Manafall. Validation: `SkyDef.Validate` / `DebrisDef.Validate`.

#### 27.3.2 Magic comes from the Manalith (GG-T671)

Owner, 2026-11-22: *"the Somnolith is what makes magic possible; magic is the concentration of aether dust channeled into phenomenon and physiological changes (like lightning, fire, ice, sleep, poison), so without the Somnolith magic wouldn't exist."* Canon: GG-MANALITH ("It makes magic possible") and GG-ONE-LIGHT ("Magic"). This names an effect of the Manalith's presence, not its purpose: the rule that no text says what the Manalith is for still holds. Aligning the glossary, element and ability texts with it is follow-up work on GG-T671.

#### 27.3.3 The Mana terminology (GG-T675, owner 2026-11-22)

Owner, 2026-11-22: the Somnolith is now the **Manalith**, which rains **Mana Dust**, refined into **Mana Shards**, the
real-money currency; **Mana Orbs** are the mana generated from Mana Dust in combat, and orbs left at the end of a stage
turn back into Mana Dust (GG-T676). Decisions the same day: **aether becomes mana everywhere** (the party panel's
"AETHER" label reads "MANA"), the dust rain is the **Manafall**, and the Camp refiner is the **Manasmith**. Mana Orbs are
old Halcyon tech scavenged from its ruins: batteries that discharge over time.

| Was | Now | Id kept (saves, purchases, Prose keys) |
|---|---|---|
| the Somnolith | **the Manalith** | sky asset `Sky/somnolith-00`, canon file `02-somnolith.md`, `sky.json` `Somnolith` |
| aether | **mana** | ability/glossary ids such as `aether`, `aether_siphon`, `aetherist_aetherpour` |
| Aetherite Dust | **Mana Dust** | item `mat_aetherite_dust` |
| Aetherite Shard(s) | **Mana Shard(s)** | glossary `aetherite_shard`, item `mat_diamond`, products `diamonds_*` |
| the Aetherain | **the Manafall** | glossary `aetherain`, `sky.json` `Aetherain` |
| the Aetherist | **the Manasmith** | NPC id `aetherist`, `npc:aetherist`, portrait `aetherist-00` |

The canon, in [GG-MANALITH](../Data/Prose/canon/02-somnolith.md): Mana Dust is the Manalith breaking up, its debris
burning apart in the atmosphere and settling everywhere as a blue sparkle that thickens region by region as it nears;
the Manafall is when it comes down heavily; Mana Shards are premium because they are mana made stable, which doesn't
dissipate; Mana Orbs capture dust for a fight and can't hold it long. The name: *mana* + *lithos* (stone), "the mana
stone", more literal than anyone knows. Player-facing words only changed; every id above is unchanged. Owner quotes
recorded before 2026-11-22 keep the words the owner used then.

#### 27.3.4 What the Manalith might be (GG-T683, owner 2026-11-22, author-only)

Owner, 2026-11-22: *"even the Manalith could be a chunk of Biomass that was somehow captured by Halcyon and accumulated
rock and dust obscures its true self; but this is never really discussed; the thing about Cosmic Horrors is that there
are always more."* Author-only canon: GridGame shares a universe with GLMZ and SpaceGod, whose Biomass is a living
flesh organism that fell to Earth from space (Prose `docs/universes/GLMZ.md`). The Manalith may be a piece of it that
Halcyon captured, its true self buried under accreted rock and dust. **No text, NPC, item or scene ever says so**, and
the rule that no text says what the Manalith is for still holds. At most, things are left for players to wonder about:
the Manalith tumbles like a body rather than a stone, and "more literal than anyone knows" is allowed to cut both ways.
It is one horror among many. There are always more, so nothing here closes the question.

## 28. No Overworld

There is no world map. Stage navigation is the Adventure list ([§22.3](#223-adventure--the-stage-list)). **Confirmed 2026-10-20 (owner, GG-T115):** permanent, not contingent on GridGameCaravan's traversal prototype picking a demo — the earlier pitch for a navigable overworld and terrain gates (design-draft-overworld-and-dialogue.md §2/§2.1, withdrawn; the draft is deleted in GG-T341, git keeps it) is not coming back. The journey is understood without one: the party is travelling, and the story is told by text crawls ([§27](#27-story-crawl-no-dialog)) and dialogue ([§27.1](#271-dialogue-gg-t117)). GridGameCaravan, the traversal lab that would have fed this, is spun off as its own project ("TownWalker"), not a Gaiden prototype.

## 29. Open Design Questions

Resolve a question → write the answer into the right section → delete it here.

### 29.1 Macro loop / run structure
1. **Permadeath / NG+** — **Answered 2026-10-19 (GG-T114, owner):** never in the normal game — "no one dies permanently, that's lame". Defeat returns to Camp at full HP; fallen heroes recover; a fallen guest returns (§25.11); recruits and dialogue-joined heroes are never removed. **Ironman** is a *planned, opt-in* mode ("leave the door open but not right now") and is **not built**: every "unit is lost" decision already routes through the one rule `Permadeath.Enabled` (`GridGame.Meta`, always `false`; fed to battles as `BattleSetup.Permadeath`), so Ironman only has to switch that on. Still open: what else Ironman changes (fallen heroes too? one save slot?).
2. **Tutorial / onboarding** — answered (GG-T539): every lesson is a blocking popup plus coach marks the player follows (§9 Guided first battle, §25.0.2).

### 29.2 Timeline feel (new with the act-advanced clock)
3. **Frozen length** — should Frozen survive into the next action (Duration 2, or tick at the bearer's own boundary)? Today it ends with the action it lands in (§8.3).
4. **Time bank** — should banked seconds persist until the next enemy turn instead of clearing at control return (§3.1.8)?
5. ~~**ActionTicks**~~ — superseded by the held turn (GG-T188, §2.0): a turn now lasts until the next enemy arrival. Open instead: should a slot use (not just a drag) end the turn, and is `MinTurnSeconds` = 2 s the right floor? (balance pass GG-T191)

### 29.3 Content economy
6. **Inventory cap** — unbounded apart from `MaxStack`; is a total cap wanted?
7. **Durability per instance** — keep the reset-on-unequip simplification (§24.5)?
8. **Item naming** — Hi-Potion, X-Potion and Ether use classic Final Fantasy item names, the same
   copyright concern that prompted renaming Phoenix Down → Revival Charm (2026-10-18); revisit
   whether these should also get original names.

## 30. Performance Budgets

Target: **60 fps** on a 4-year-old mid-tier phone; `GameApp.Awake` pins `targetFrameRate = 60`, `vSyncCount = 0`, and the automation `state` endpoint reports fps.

- **Simulation** costs nothing per frame — it runs only inside `Apply`, one burst per action. `PreviewMove` clones the whole `BattleState` per drag step and `HeroBot` previews every L-path for every hero: fine on cold paths, the first thing to profile on device.
- **Presentation** plays one awaited beat at a time (budget ≤ 3 ms); **UI Toolkit** layout/render ≤ 8 ms — rebuild a screen only on change (`Refresh`), never per frame; keep ≥ 2 ms slack.
- **Hygiene:** all playback runs through `Safe()` with a 20 s watchdog and a final `SyncAll`. **Amended 2026-10-09:** there is no per-battle static state to clear — everything lives on `BattleState`, which dies with the screen.
- **Assets:** `Resources` loading is sanctioned (backdrops at the art's resolution, 1536×3072 JPEG, ~4.5 MB each as textures (GG-T353); thumbs 256², icons 64²; music mp3, SFX ogg/wav).
- ⬜ Not yet built: the on-device profiling pass.

---

## 31. Accessibility

- **Color-blindness:** mana has one color by design (filled vs empty reads by brightness); status pips carry their icon and a letter, never colour alone. ~~**Colorblind palette** (`SettingsSave.ColorblindMode`) remaps buff pip colors.~~ **Amended 2026-11-20 (GG-T495):** the pip remap had lapsed (`BoardView.SetBuffs` ignores the flag; icon + letter carry the meaning). **Colorblind palette** now names the element on an elemental hit's damage number ("86 Ice"), since its burst says it only in colour; physical and armored hits are unchanged. **Amended 2026-11-21 (GG-T525):** it also marks sides with one colourblind-safe **blue/orange pair** instead of blue/red/purple: hero keylines and hero casts sky blue (`Palette.CbHero` 87/181/232), foe keylines, foe turn icons and foe casts vermilion orange (`CbFoe` 245/122/10), a boss's keyline and turn icon a darker burnt orange (`CbBoss` 191/77/5). Drawn in code through `Palette.TeamFoe` / `TeamBoss` / `TeamCast` (timeline icons and the prominence slot, `Palette.Colorblind` set by `GameApp.ApplySettings`), and in USS under a `colorblind` root class (the board keylines); a cast ring on the board shows its caster's side colour instead of its element hue. Tamed, wanderer, guest and neutral marks keep their own colours (their tag says what they are). Status pips stay unremapped by decision: their icon and letter carry the meaning. Timeline icons already on the bar take the new colours when they are next drawn.
- **Motion:** 🟡 **Reduce motion** (`SettingsSave.ReduceMotion`) zeroes lunges, shakes and the death-portrait jitter and dims the Clutch flash (`BattleScreen.Fx`), skips the pincer and caster cut-ins, prefab VFX and particle bursts, and ellipsizes marquees; **Battle speed** ×1/1.5/2/3 shortens every beat. **Added 2026-11-20 (GG-T495):** it also calms the shared tweens everywhere, menus and celebrations included (`Tween.Calm`, set by `GameApp.ApplySettings`): `Tween.Shake` holds still, `Tween.Pop` doesn't swell, `Tween.Move` takes at most `Timing.Quick` with no overshoot.
- **Text size (GG-T495):** Settings → **Text size** 100 / 115 / 130 % (`SettingsSave.TextScale`, settings.json `game.textScale`, anything else reads 100), applied live: `TextSize.Apply` (Tokens.cs) puts `text-115` / `text-130` on the UI root and Theme.uss redefines `--type-badge` … `--type-h2` under it, so every USS-sized label grows with no rebuild; one-line overflow becomes the shared marquee (§26.6). H1, Display and Title stay (already headlines), and so do the battle's fixed-geometry labels set in code (board floats, tile badges). Counters sized to their number (the difficulty level, the Merchant's quantity) take a minimum width instead of a fixed one. Automation: `setting key=textsize value=130`. **Amended 2026-11-23 (GG-T770):** Text size now scales the one GUI size (`GuiType`, §26.6): every label grows together, headlines and code-set labels included; the `text-115` / `text-130` classes and the per-size `--type-*` variables are gone (the root takes one `type-base-N`).
- **Minimum text size (GG-T551, added 2026-11-21):** the stage scales uniformly to the screen, so on a small low-dpi phone (480×800 at 160 dpi, scale 0.32) a 30-px caption drew at ~9 screen px. `TypeFloor` (Tokens.cs) computes, on every stage fit (`GameApp.FitStage`), the smallest design size that lands at **12 screen px or 9 dp** (whichever is larger; `Screen.dpi`, 160 when unknown); when the badge size would fall under it, the UI root takes `type-floor-32/36/40/44/48` and Theme.uss raises `--type-badge` … `--type-body` to at least that many design px (H3 and up never change). Theme.uss pairs each floor with `text-115` / `text-130`, every size the larger of the two, so Text size keeps working on top of the floor. Mainstream phones and tablets (1080×2400, 1170×2532, iPad 1620×2160) clear the floor and look unchanged. Code-set battle labels (board floats, tile badges, mana counter) are fixed geometry and stay as they are. Automation: `stagebounds` reports `typeFloor` and `dpi`. Unity's "Development Build" corner mark seen in emulator shots is dev-only; release builds don't draw it. **Amended 2026-11-23 (GG-T770):** the `type-floor-N` classes are gone; `GuiType` folds the floor into the one GUI size, which is never below it. `TypeFloor` keeps `NeedFor`, `ShrinkFloor` (TextFit's lower bound, at least 24) and `Current` (the floor in design px, reported by `stagebounds`).
- **Contrast (GG-T495):** text tokens are checked against the **lightest** stop of the Classic blue window (#2C46AA) for 4.5:1: `--color-dim` 176 → 200 grey (3.8 → 4.9:1), `--color-bad` 255/97/87 → 255/170/162 (2.8 → 4.5:1), rarities junk 140 → 192 grey, rare 100/170/255 → 150/200/255, epic 200/120/255 → 220/178/255 (all ≥ 4.5:1); Palette mirrors them. White text 7.5:1, gold 6.0, good 5.8, warn 5.2, sub 5.3. A locked menu row greys its name, value and icon (45 %) rather than the whole row, so its hint ("Joins after The Tithe Moors III") reads at full dim contrast (was 1.8:1 at 42 % opacity). ~~Element hues and team colours as text on a window are a follow-up (they live in vfxstyle.json).~~ **Amended 2026-11-21 (GG-T525):** colour that is an art colour first (element hues from vfxstyle.json: dark 1.9:1, fire 3.0:1; team colours: enemy 2.1:1, hero 3.5:1) is lifted for text by `Palette.OnWindow`: the same hue mixed toward white in 8 % steps just until it clears 4.5:1 against #2C46AA (a colour that already does is unchanged), with text variants `Palette.ElementText(type)`, `HeroText`, `EnemyText`. It colours every combat-log line (`AddLogLine`), every coloured line of an InspectCard (`InspectCard.Line`: the unit's side, an ability's "N Fire damage"). The board keeps the art colours (keylines, bursts, damage floats, which sit on art with an outline, not on a window).
- **Touch targets:** slots (≥ 211×196, growing with the stage, GG-T252) and tiles 180² design px clear 44 dp at every supported scale; a dragged hero lifts above the finger. Every menu button and row is at least 132×132 design px (`--size-target`, ≈ 44 dp at 1170 px ≈ 390 dp wide), checked by the layout audit's POL-C2 (`harness.py a11y` prints it per screen). ~~The battle's Party · Lore · Log tabs (60 px tall) are the known exception, a follow-up.~~ **Amended 2026-11-21 (GG-T524):** the battle's info tabs are 132-px hit areas around their 60-px drawn faces ([§9](#9-the-hud)), so every battle target clears it too.
- **Tooltips** (*GG-T252*): hold-to-read InspectCards on every icon, unit, slot, status, timeline icon and item/talent row ([§9](#9-the-hud)); taught by the tutorial's "Hold to read" lesson, and once by a tip for anyone who skipped it (`hint:inspect` flag).
- **Readability:** one GUI text size (40 design px on the reference phone, GG-T770), white on deep-blue windows, HP as gauge + numbers on the panel. **Text wrapping** (*GG-T305*, Settings → Text wrapping: Word wrap / Hyphenate) and the shared one-line marquee (Reduce Motion: an ellipsis) — [§26.6](#266-units-and-overflow-gg-t272-gg-t271-owner-2026-11-05).
- **Audio:** every important event also has text. 🟡 Music/SFX volume cycles in Settings; `MuteMusic`/`MuteSfx` exist in the save and are honored by `Audio.Apply` but have no toggle.
- **Menu themes (added 2026-10-11, owner: "the gradient on all the menus is generated... make multiple Themes... updated in Settings and remembers it"):** `Art.MenuTheme` is two chosen base colors (`ColorA`/`ColorB`); the accent used for every window's border (`Ui.Window`) is never hand-picked — it's the complement of the two base colors' circular-mean hue, pushed toward full saturation/brightness since it frames thin borders, not large fills (`Art.MenuTheme`'s constructor). Presets (`Art.Themes`): **Classic** (the original deep blue, now just the default preset), **Cyberpunk** (magenta → neon cyan, gold-green accent), **Ember** (mindattic.com's coral → teal), **Verdant** (MindAttic.Launcher's green → near-black). `SettingsSave.MenuTheme` persists the pick; "Menu theme" in Settings cycles it (`Art.ApplyTheme`, called from `GameApp.ApplySettings` on every load and every Settings change) and repaints the shared `Art.Window`/`Art.WindowDark` gradient textures **in place** — every already-open window updates with no rebuild, since they all hold the same `Texture2D` reference.
- **Theme USS classes (GG-T185, epic W):** a preset may also name a root USS class (`Art.MenuTheme.UssClass`). `Ui.ApplyMenuTheme` (called from `GameApp.ApplySettings` right after `Art.ApplyTheme`) puts the active theme's class on the UI root and removes every other theme's, and re-tints every open `.window`'s frame; Theme.uss scopes each such theme's frame, tab, button, header and toast rules under that class at the end of the file, so the whole look switches live with no rebuild. A theme with a class owns its window frame in USS (`Ui.Window` sets no inline accent border for it). Adding a theme is one row in `Art.Themes` plus one `.theme-<name>` block in Theme.uss. **Slate** (`theme-slate`) is modeled on Chrono Trigger's default window style: a gray-blue slate gradient body, a light bevelled frame (light top/left edge, darker bottom/right), white text, slate tabs and buttons with the same bevel; primary (gold) and danger (red) buttons keep their colors. The harness sets it with `call setting key=menutheme value=Slate`. **Dark** (`theme-dark`, GG-T186) is the DocFX modern dark docs / mindattic.com look on MindAttic.Components' `web/themes/Dark/dark.css` tokens: flat `#13161b` windows (both gradient stops the same) on a `#0d0f12` stage, `#2a2f3a` hairline frames on windows, tabs, header, buttons and toasts, one `#6ba3e8` accent (selected rows, active tab's top edge, primary buttons with dark text), `#e8eaf0`/`#9aa0b0` text and no text shadows; gold, good/bad and rarity colors stay semantic. `python tools/harness.py sweep RUN Dark` sweeps every menu screen in it.
- **Fonts (added 2026-10-11, owner: "allow a selection of different Fonts to change look and feel"):** `Fonts.Presets` (`Assets/GridGame/App/Services/Fonts.cs`) — **Classic** (Outfit, the original/default), **Lato**, **Roboto**, **Play**, **Montserrat**, **Inter**. Calibri and Aptos were named but are Microsoft-proprietary (not freely bundleable); Inter fills that "clean modern UI sans" role instead. "Attic" isn't a real distributable family; Montserrat fills that slot (its Google Fonts build is a variable font whose default instance renders a bit lighter than Regular — a known, purely cosmetic quirk, not a bug). Only the **body** font is selectable; the Display font (Cinzel, used on ~7 headline/name labels) stays fixed across every preset. `SettingsSave.FontName` persists the pick; "Font" in Settings cycles it the same way "Menu theme" does (`Fonts.ApplyFont`, called from `GameApp.ApplySettings`). Unlike themes, no texture-repaint trick is needed: `unityFontDefinition` is an inherited UI Toolkit style property, so re-applying the body font to the UI root (`Fonts.UseBody`) cascades to every element that doesn't explicitly override it, instantly, with no screen rebuild. Roboto/Montserrat/Inter are Google's current variable-font TTFs (no static cut is published any more); Unity's dynamic SDF pipeline (`FontAsset.CreateFontAsset`) renders their default instance correctly.
- Status: colorblind 🟡 (GG-T525: team pair done; not checked through a CVD simulator) · reduce motion 🟡 · text size ✅ · contrast 🟡 (GG-T525: tokens, element and team text done; other art colours used as text, e.g. role badges, not audited) · tap targets ✅ (GG-T524) · volume 🟡 · menu themes 🟡 · fonts 🟡 · safe area 🟡 (faked on desktop, GG-T483; needs a real notched phone) · tooltips ✅ (GG-T252).

---

## 32. Document Discipline

- Every gameplay-affecting change updates this bible (add, amend or verify); complex mechanics are written here first, then coded. Numbers recorded here follow `BattleConfig` / content changes.
- Removed mechanics are deleted (or marked RETIRED with a pointer, as GG-LAW-3/4). Git is the history.
- A deviation from the GridGame2026 bible gets an inline **Amended YYYY-MM-DD:** note and a bullet in [`AMENDMENTS.md`](AMENDMENTS.md); status changes go to [`USER_STORIES.md`](USER_STORIES.md) with evidence.
- What the game IS lives here; why we deviated lives in AMENDMENTS; what is pending lives in USER_STORIES + §29; what a session debugged lives only in that conversation.
- Readers: the owner (sanity check), future sessions, unattended agents (the spec), new contributors. A section one of them can't read without context is a bug.

## 33. The wiki: one entry per thing (GG-T572; schema GG-T626)

Owner, 2026-11-21: design every system, class, region, item and lore entry so it can be published to a dedicated wiki,
from a single source, with stable ids, one entry per thing and generated pages. The wiki is **built, never written**:
every page comes from the content JSON and the applied Prose texts (§27.2), so a balance change or a newly approved text
changes its page on the next build and nothing has to be copied by hand.

- **The schema** is `tools/wiki_schema.json` (`gridgame-wiki-schema/1`); `tools/wiki_schema.py` builds and validates
  entries from it, and GG-T627's page generator imports that module (`build_all()`, `validate_all()`). Running
  `python tools/wiki_schema.py` builds every entry and reports problems (`-v` lists warnings, `--id <id>` prints one).
- **One entry** (`gridgame-wiki/1`): `id`, `kind`, `gameId`, `title`, `summary`, `body`, `infobox`, `links`, `art`,
  `texts`, `source` {file, BIBLE section}, `version`.
  - **id** is the Prose entity id wherever one exists (`class-<id>`, `item-<id>`, `place-<theme or stage>`,
    `concept-<glossaryId>`, `faction-<id>`, `character-<npc>`), else `<kind>-<slug>` (`ability-`, `armorset-`,
    `skilltree-`, `quest-`, `rule-`, `currency-`, `system-`, `lore-`); the slug is the game id lowercased with `_` → `-`
    (`prose_keys.slug`). Published ids never change, like the game's own ids.
  - **title** is the game-owned name (`Name`, a glossary `Term`, a region rule's Prose `name`).
  - **summary and body hold no words**, only sources: a Prose key and payload field (`class:<id>` short/long,
    `item:<id>` short, `glossary:<id>` definition, `quest:<id>` brief/complete, …), or a game field for the mechanical
    text the game itself owns (an ability's `Description`), or a Prose canon section (`lore`). The existing keys are
    reused (§27.2, PROSE.md); the wiki adds no parallel text.
  - **infobox** copies record fields (role, stats, cost, rarity, stages, …); a field that names another thing becomes a
    **link** to that thing's entry id (a class's abilities, a region's stages and milestone reward, an item's on-use
    ability, a glossary term's `LinkKind`/`LinkId` when it is an ability).
  - **art** is the entry's picture by the asset rule (§12, `AssetNames`): Portraits, ItemIcons, AbilityIcons or a
    region's first backdrop, `.png`.
  - **version** hashes the record and its texts' hashes, so the generator rewrites only pages that changed.
- **Kinds and where they come from:** `class` and `enemy` (classes.json by `Team`), `ability`, `item`, `armorset`,
  `skilltree`, `region` (campaign.json themes), `stage` (non-test stages), `rule` (regionrules.json), `quest`,
  `faction`, `npc` (the Camp NPCs, `prose_keys.NPCS`), `glossary`, `lore` (the canon sections staged in
  `Data/Prose/canon`), and two small registries in the schema itself for things with no content record: `currency`
  (Gold, Mana Shard, Mana Dust; §24.9.1) and `system` (a mechanic: its glossary text and its BIBLE section,
  e.g. pincer §1.2, the timeline §2, Clutch! §13.4.1). A new kind is a new block in the schema, not new code.
- **Validation:** every required field; the id matches its kind's template and is unique; the summary resolves to
  words; every text key exists in Content/Text; every link resolves to an entry; the art is on disk and a `.png`.
  An unresolved summary or text key is an **error** (the page would be empty); a missing long text, art or a `.jpg`
  backdrop is a **warning**.
- *State at GG-T626 (2026-11-22): 2,123 entries over 16 kinds; 2 errors (Gold has no glossary text yet,
  `glossary:gold`) and 12 warnings (6 regions' backdrops are still `.jpg`, 5 Camp NPCs have no portrait file, one
  body text missing). GG-T627 renders pages; GG-T628 fills the gaps and the system/currency registries.*

— end of document —
