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

## Placeholder cues (no file yet)

Called by the game already; `Audio.Sfx` skips a cue with no file, so each also plays the stand-in listed until its
sound exists (`Resources/Audio/Sfx/<group>-<name>-00.ogg`).

| Cue | Wants | Plays on | Stand-in |
|---|---|---|---|
| `slime.vomit` | a wet retching heave and a gloopy launch | a slime lobs its Vomit Glob (GG-T895, §14.23) | `element.water` low |
| `slime.squish` | a juicy squelch underfoot | a hero squishes a slime glob (SquishedEvent, GG-T895) | `battle.slide` low |
| `element.crackle` | a dry crackle of flames catching | a firewall (or fire trail) is painted, and each burn it deals on entering or ending a turn on it (GG-T933, §7) | `element.fire` low |
| `alchemist.slosh` | a glass flask shattering into a thick, glugging slosh of oil | the Alchemist's Oil Flask bursts on its pool (GG-T886, §7) | `element.water` low |
| `battle.clank` | a wooden crate slammed down, iron bands clanking | the Machinist's barricade drops in, takes a hit or comes down (GG-T935, §14.23) | `battle.shatter` high |
| `minotaur.steamtrain` | a steam-train whistle into chugging | a minotaur takes Raging (the RAGE card venting steam) and each step of its raging Bull Rush run (RampagedEvent) (GG-T898, §14.18) | `element.steam` (low hiss on rage, high whistle as the run starts, a soft chuff per step) with `battle.warcry` low on rage and `battle.dash` low per step |

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
| SquishedEvent | `battle.slide`; a slime glob adds `slime.squish` (GG-T895) | |
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
| ReinforcementsArrivedEvent | entrance cues (`battle.trap`, `battle.dash`, `status.debuff`, `battle.shatter`); a slime hatching from its glob `battle.crit` high + `element.water` (GG-T895) | |
| BattleEndedEvent | `battle.victory` / `battle.defeat` / `battle.dash` (escaped) | |
| ComboChangedEvent | `battle.powerup` on a rank-up, pitch rising with the rank (GG-T851) | a rising sting per rank |
| ComboEndedEvent | `combo.rank.d` / `combo.rank.c` / `combo.rank.b` / `combo.rank.a` / `combo.rank.s` / `combo.rank.ss` / `combo.rank.sss` with the rank callout banner (GG-T852), plus `ui.diamond` at S and above | **placeholders, no files yet** (`combo-rank-<rank>-00.ogg`; silent until made): an announcer-style stab per rank, hotter from D up to SSS |
| ComboChangedEvent | `battle.powerup` on a rank-up, pitch rising with the rank (GG-T851) | a rising sting per rank |
| ComboEndedEvent | `combo.rank.d` / `combo.rank.c` / `combo.rank.b` / `combo.rank.a` / `combo.rank.s` / `combo.rank.ss` / `combo.rank.sss` with the rank callout banner (GG-T852), plus `ui.diamond` at S and above | **placeholders, no files yet** (`combo-rank-<rank>-00.ogg`; silent until made): an announcer-style stab per rank, hotter from D up to SSS |

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

## Library choice (GG-T463, research 2026-11-24; OWNER MUST BUY)

**Recommendation: Universal Sound FX by Imphenzia** (Unity Asset Store, package 17256). An agent cannot buy it;
the owner purchases it, then GG-T464 imports it.

| Fact | Finding (Asset Store page, checked 2026-11-24) |
|---|---|
| Price | $24.97 at the current 50% sale (list $49.95), plus tax at checkout |
| Licence | Standard Unity Asset Store EULA, Extension Asset. Royalty-free use in commercial games, mobile included; the sounds may not be redistributed as a standalone sound library. Pick **Single Entity** (individuals and small businesses) |
| Size | 1.4 GB download; import only the groups the mapping list needs, and ship ogg, so the build is not affected |
| Version | 1.6 (2025-02-28), requires Unity 2021.3+ |
| Reviews | ~1,070 ratings, ~5,900 favourites. Recent reviews (v1.6) positive on selection and quality |
| Tags | Fantasy, UI, power-up, sci-fi, 8-bit, voice, loops |

Coverage against the Wants column above: strong for UI, pickups, power-ups, whooshes, casts and magic; fantasy tags
fit the game. Weaker or unconfirmed: weapon-specific hits (sword, axe, bow, claw), per-family death cries, footsteps
by terrain, shell crack and egg squelch. The page states no file count or category list for these, so check the
package contents (Asset Store preview or after purchase) before relying on them. Anything missing stays on the
current synthesized `make_sfx.py` cue, as GG-T464 already plans.

Risks from the reviews: one reviewer reports mono files, abrupt cut-off endings and a muffled sound; others
disagree, and no recent review repeats it. A few note too many whooshes (over 30). Mitigation: audition the
candidates for the Wants rows before committing, trim tails and apply the -16 LUFS normalisation on import, and
refund is available under the Unity refund policy if the pack fails the audition.

Alternatives considered (not researched in depth, cheaper or narrower): generic $20-25 "Universal Sound Effects" and
"Universal Audio FX" packs (different publishers, smaller). Not recommended as the single source.

**Owner action:** buy Universal Sound FX (Single Entity licence) from the Asset Store, then unblock GG-T464.
GG-T463 stays open until the purchase is made.
