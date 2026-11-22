# Sound effects: coverage audit (GG-T465)

Bible §12.0: every event gets a sound. This is the map from every event the player sees to the cue it plays, made
2026-11-20 by reading every `Audio.Sfx` call (battle playback, camp screens, widgets). The SFX library import
(GG-T464) uses it as its mapping list: the **Wants** column says what a library sound should replace, where the cue
today is a borrowed or synthesized stand-in.

How cues work: game code calls `Audio.Sfx("<group>.<name>")`; the file is the key's words joined by hyphens plus the
variant `-00`, the asset naming rule (GG-T561/GG-T562, `Core/Content/AssetNames`): `camp.repair` →
`Resources/Audio/Sfx/camp-repair-00.ogg`, `battle.crucible_wave` → `battle-crucible-wave-00.ogg`. `battle.move` /
`battle.hit` pick one of 6 / 7 separate sounds at random, numbered as variants (`battle-move-00..05`,
`battle-hit-00..06`); `ui.back` shares `ui-click-00`. Every file is synthesized by `GridGame.Studio/tools/make_sfx.py` (MindAttic's own, normalised to
−16 LUFS, ≤ −1 dBTP). Groups: `ui.*`, `battle.*`, `element.*`, `status.*`, `story.*`, and since GG-T465 `camp.*`.

## New cues (GG-T465)

| Cue | Sound | Plays on |
|---|---|---|
| `ui.equip` | buckle-and-plate "shink" | equipping or removing gear (was the coin) |
| `camp.repair` | three hammer taps on the anvil | a repair at the Blacksmith (was the coin) |
| `camp.caravan` | wooden knocks, a bell, a rising pair | buying a Caravan upgrade (was the coin) |
| `camp.summon` | shimmering swell into a held chord, sparkles | a summoned hero steps out of the circle (was `battle.powerup`) |
| `ui.diamond` | quick crystalline run | **not wired yet**: for the Diamond Store / exchange (Payments epic owns `DiamondStoreScreen`, which plays `ui.confirm` today) |
| `battle.fizzle` | sputtering downward sweep | a queued spell interrupted or fizzling (was silent) |
| `element.steam` | soft hiss | a status washed off as steam (was silent) |
| `battle.wave` | two war drums and a short horn | a campaign wave arriving (was banner only; the Crucible keeps `battle.crucible_wave`) |
| `status.sleep` | two soft falling tones | a turn skipped asleep (was silent; other skips play a soft `ui.cancel`) |

Also given a cue from the existing set: **Respite** (the pincer-pair spin) plays `battle.whoosh`.

## Battle events (`Core/Battle` events → `App/Battle/BattleScreen.*`)

| Event | Cue(s) | Wants (GG-T464) |
|---|---|---|
| SteppedEvent | `battle.move` (6 variants) | footsteps by terrain |
| DisplacedEvent | `battle.slide` | |
| TeleportedEvent | `element.arcane` | blink / warp |
| PincerFormedEvent | `battle.pincer`, `battle.dash` (cut-ins) | |
| RespiteEvent | `battle.whoosh` (GG-T465) | |
| AttackEvent | `element.arcane` (spell bolt) + the hit's damage cue | |
| SupportActionEvent | per effect (`SupportSfx`), `battle.pickup` card pop | |
| DamagedEvent | the ability style's cue / `battle.crit` / `status.debuff` (DoT) / `battle.miss` | weapon-specific hits (sword, axe, bow, claw) |
| HealedEvent, ClutchEvent, RevivedEvent | `battle.heal` | |
| DiedEvent | `battle.death` | per-family death cries |
| LootEvent, WaveClearedEvent | `ui.buy` (coin burst) | coins scatter |
| RemainsLeftEvent | `battle.shatter` | bones / debris |
| GuestJoinedEvent, RecruitableAppearedEvent, EggLaidEvent | `battle.pickup` | egg squelch |
| RecruitOfferedEvent | `ui.click` | |
| RecruitJoinedEvent | `battle.victory` | short "joins" jingle |
| TamedEvent | `battle.powerup` | |
| TameFailedEvent | `battle.miss` | |
| BondGrewEvent, TrustGrewEvent | `ui.select`, `battle.warcry` when full | |
| TameEndedEvent, RoseAgainEvent | `status.debuff` | undead rise |
| WandererJoinedEvent | `battle.pickup` | |
| AmplifierSpawnedEvent, ManaChargedEvent | `battle.orb` | |
| AmplifierMovedEvent, PickupMovedEvent, PushedBackEvent, CastStaggeredEvent | `battle.slide` | |
| AmplifierUsedEvent, EggHatchedEvent | `battle.crit` | shell crack |
| OrbMintedEvent | `battle.orb` / `battle.pickup` / `battle.miss` (lost) | |
| OrbsSpentEvent, AbilityResolvedEvent | `battle.cast` + the ability's VFX cues | per-element casts |
| TimelineAdvancedEvent | `battle.whoosh` (slot change), `battle.channel` (last seconds) | |
| HastenedEvent | `battle.cast` (haste burst) | |
| TempoChangedEvent | `battle.powerup` / `status.debuff` | |
| TurnStartedEvent | `battle.turn` | |
| TurnSkippedEvent | `status.sleep` asleep, else `ui.cancel` (GG-T465) | |
| CastQueuedEvent | `battle.cast`, `battle.channel` | |
| CastCancelledEvent | `battle.fizzle` (GG-T465) | |
| AbilityRefusedEvent | `ui.error` | |
| ItemUsedEvent | the item's VFX: `battle.pickup` / `battle.glug` / `battle.whoosh` / `battle.glass` | |
| PlateHitEvent | `battle.clang` | |
| PlateBrokenEvent, WeaponShatteredEvent, ArmorBrokenEvent | `battle.shatter` | metal snap for weapons |
| PickupDroppedEvent, PickupCollectedEvent | `battle.powerup` | |
| PickupExpiredEvent | `battle.miss` | |
| BuffAppliedEvent | the status's cue / `battle.cast` / `status.debuff` / `battle.taunt` | per-status cues |
| BuffRemovedEvent | `element.steam` when steam (GG-T465); otherwise silent (the bar icon fades) | |
| TriggeredEvent | the passive's cue | |
| DodgedEvent | `battle.whoosh` | |
| TrapPlacedEvent, TrapTriggeredEvent, TrapClearedEvent | `battle.trap` | |
| RegionRuleEvent | `battle.whoosh` | |
| RegionTilesEvent | `battle.slide` / `battle.trap` | |
| RegionAnchorEvent | `battle.dash` | |
| RegionFlagEvent, HazardQueuedEvent | `battle.channel` | |
| HazardRetimedEvent | `battle.slide` | |
| HazardLandedEvent | `battle.shatter` | per-hazard impacts |
| FellEvent | `battle.slide`, `battle.shatter`, `battle.dash` | falling wail |
| WeaponSwappedEvent | `battle.dash` | weapon draw |
| BossPhaseEvent | `battle.explosion` | |
| RuleBrokenEvent | `battle.warcry` arrival, then per rule (`battle.dash`, `battle.clang`/`battle.shatter`, `battle.whoosh`, `battle.explosion`, `battle.powerup`) | |
| BountyTargetEvent | `battle.warcry`, `battle.pickup` | |
| WaveStartedEvent | `battle.wave` (GG-T465); Crucible `battle.crucible_wave` | |
| ReinforcementsArrivedEvent | entrance cues (`battle.trap`, `battle.dash`, `status.debuff`, `battle.shatter`) | |
| BattleEndedEvent | `battle.victory` / `battle.defeat` / `battle.dash` (escaped) | |

Silent by design (each rides on a neighbour that has its own cue, or has nothing on screen): ActionBeganEvent (the
action's own cues follow), StepBlockedEvent, FacingChangedEvent (part of a turn), BuffTickedEvent (its
DamagedEvent sounds), WeaponWornEvent (nothing shown; the break is `battle.shatter`), AnnounceEvent (a banner over
an event with its own cue), PlayerControlEvent (input reopens), TileConflictEvent (dev-only error).

Battle input: press `ui.click`, drag start `battle.pickup`, path steps `battle.move` / `battle.dash`, preview
`battle.slide`, out of time `ui.error`, select `ui.select`, confirm `ui.confirm`, cancel `ui.cancel`.

## Camp, menus and meta

| Event | Cue | Wants (GG-T464) |
|---|---|---|
| Any button press / tappable | `ui.press` / `ui.click` | |
| Tabs, rows, inspect cards, camp tips | `ui.select` | |
| A refused action anywhere | `ui.error` | |
| A purchase / sale / generic camp action (`HubPage.Done`) | `ui.buy` | coin pouch |
| Equip / remove gear | `ui.equip` (GG-T465) | armour rustle |
| Repair | `camp.repair` (GG-T465) | real anvil |
| Caravan upgrade | `camp.caravan` (GG-T465) | cart and cheer |
| Facility built at a station | `ui.buy` + `battle.clang` (Forge) / `battle.glug` (others) | construction |
| Crafting | `ui.buy` + station cue; lucky craft `element.arcane` then `battle.powerup` (masterwork) / `battle.clang` | |
| Summon reveal | `camp.summon` (GG-T465), then `battle.victory` | |
| Diamond Store / exchange / packs | `ui.confirm` / `ui.error` (`ui.diamond` ready, see above) | gem chime |
| Paywall (Rested refill, skip) | `ui.buy`, `ui.confirm` / `ui.error` | |
| Tome read (a skill learned) | `battle.powerup` | page turn |
| Chest opened | `battle.pickup` | lid creak |
| Rest at the inn | `battle.heal` | |
| Quest accepted / handed in | `story.quest_accept` / `story.quest_complete` | |
| Mercenary haggle | `ui.press`, then `ui.buy` / `ui.select` / `ui.error` | |
| Dialogue: line advance, speaker change, choice, standing change | `ui.select`, `battle.slide`, `ui.confirm`, `battle.powerup` / `status.debuff` | |
| Level-up screen | `battle.crit` per level, `battle.orb`, `battle.shatter` | |
| Results: rewards, retry, Crucible best | `battle.victory`, `ui.confirm`, `battle.crucible_best` | |
| Crucible entered | `battle.crucible_enter` | |
| Claimed timed job (purse) | `battle.pickup` | |
| Window theme editor save | `ui.confirm` / `ui.error` | |
