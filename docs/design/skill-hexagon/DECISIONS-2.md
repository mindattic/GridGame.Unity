# skill-hex owner decisions, part 2 (GG-T1054)

This brief lets the owner settle three questions in one pass. GG-T1025 already settled the SP economy, Luck, stat
growth, the healing stat and the cut classes; its description holds those answers. The model is in
[PLAN.md](PLAN.md), the gap list in [GAPS.md](GAPS.md) and the last signature pass in [SIGNATURES.md](SIGNATURES.md).
The numbers below come from `Assets/GridGame/Resources/Content/skillhex.json`, `abilities.json` and `classes.json`
on main at `bd4f896e`. The hero spot is placed the way `tools/skillhex/validate.py` (`hero_spots`) places it.
"d" is the lattice distance from a hero's spot, and the validator warns above d1.

Nothing here is decided. Each part ends with the question to answer. GG-T1038 (New Wild hero) waits on question 1.

## 1. The new Wild hero (WIS 2, STR 1, SPD 1)

**Where it sits.** The spot is **(-2, -8)**, ring 10, in the Wild wedge but on its WIS side, close to Brains. The
passives within 2 steps add up to WIS +23 and SPD +1. The nearby heroes are trappers and tempo casters: Hex Warden
(-2, -11), White Ninja (-3, -6), Monster Tamer (0, -6) and Courier (2, -10). The nearest heavy STR melee move is
Cleave, at d15. The "brawler" part must therefore come from the signature and the kit. The
board around the spot does not supply it.

**Where the signature goes.** Every point next to (-2, -8) is a passive, so the only slot within d1 is (-2, -8)
itself. Today Stumbling Ground (`tempoweave_stumble`) sits there; it is a kit move for Courier, Dervish and Tactician,
and no hero's signature. Proposal: the signature takes (-2, -8), and Stumbling Ground moves to the Open Wild slot
(-6, -9) (WIS +9, SPD +3, d5). That uses 1 of the 22 Open slots (see question 2).

### Name options (the style is a plain noun or a two-word title: Wild Child, Blue Lion, Panda Keeper, Hex Stalker)

| # | Name | Class id | One-line fantasy |
|---|---|---|---|
| a | **Thornfist** | `thornfist` | A grove-sworn brawler who winds bramble round bare knuckles and hits with the patience of old trees. |
| b | **Grove Keeper** | `grovekeeper` | The keeper of a sacred grove meets trespassers fist-first and lets the roots finish them. It pairs with Panda Keeper and Monster Tamer. |
| c | **Moonbear** | `moonbear` | A druid who borrows the bear's strength under the moon: slow to anger and impossible to move. It pairs with Blue Lion. It shares "moon" with White Ninja's Moonlit Recovery. |

None of the three names appears anywhere in content, BIBLE.md or USER_STORIES.md today.

### Proposed starting kit (4 existing moves near the spot)

| Move | Node | d | Why | Shared with |
|---|---|---|---|---|
| Alpha's Challenge (`primalpack_challenge`) | (-4, -7) | 2 | It taunts every foe in reach, which makes the hero a front-line brawler from turn one | Blue Lion, Wild Child, Monster Tamer |
| Snare Net (`trapper_snare_net`) | (-3, -9) | 2 | 6 damage and [Silenced]; it fouls footing and sets up the signature | Hex Warden (only lister) |
| Stumbling Ground (`tempoweave_stumble`) | (-2, -8), moving to (-6, -9) | 0, then 5 | It slows ground and sets up the signature | Courier, Dervish, Tactician |
| Feral Howl (`wildchild_howl`) | (-4, -4) | 4 | 6 damage to every foe nearby and [Blinded]; it is the druid's roar | Wild Child, Monster Tamer |

Bench options in case one of these is unwanted: Shortcut (`tempoweave_shortcut`, d2, hands an ally its turn early),
Vengeance (`vengeance`, d4, a reaction that turns the hero [Empowered] when an ally falls), and Stampede
(`stampede_rush`, off the board, could fill a Wild Open slot).

### New signature concept: **Rootbreaker** (`<classid>_rootbreaker`)

- **What it does.** An adjacent strike, about 14 Physical. The target is [Rooted] for 1 turn. If the target was
  already [Slowed] or [Rooted], the hit deals x1.5 damage and pushes it back 1 s on the timeline.
- **How it plays.** The hero sets a snare (Snare Net, Stumbling Ground, or Wild Child's Bramble Snare), then lands the
  slam. That gives the Wild hero a melee payoff no other hero has. Bramble Snare roots but does no damage, and Bamboo
  Slam taunts.
- **Owner sub-question.** Should it scale with STR (the brawler reading) or WIS (the hero's main stat and the
  wedge it lives in)?
- **Alternative concept.** *Wild Shape*: a 3-turn stance in which the hero gains [Protection] and its hits taunt. It is
  heavier to build, because it adds a new form state.

### Art

- **Temporary reuse.**
  - Panda Keeper's art (`panda-keeper-female` / `panda-keeper-male`) is the closest silhouette: a beast-side brawler.
  - Martial Artist's art (`martial-artist-*`) fits a bare-handed fighter.
  - The `mannequin` placeholder (ContentModels: "placeholders → mannequin") avoids two identical faces when Panda
    Keeper is in the same party.
  - Do not reuse Wild Child's art. Monster Tamer already draws `wild-child-female`, so that face would appear three
    times.
  - Recommendation: `mannequin` for shipped builds and Panda Keeper art for playtests.
  - For the ability icon, reuse `wildchild-howl` or the `thorns` icon until a new one exists.
- **Owner-only (new art):**
  - the female portrait (`Resources/Portraits/<stem>-female-00.png`)
  - the male portrait (`<stem>-male-00.png`, the class's `MalePortrait`)
  - the matching `Resources/Thumbs` images
  - the signature's ability icon (`Resources/AbilityIcons/<id>-00.png`)
  - the GridGame2026 `ArtSource` key, if the art comes through `import_art.py`

  Once the art lands, an agent can set the crop, `Thumbnail`, `MaleThumbnail` and `Figure` framing. Lore and Codex
  text can be drafted for the owner's approval.

**Question 1.** Choose a name (a, b, c or your own). Approve or change the 4-move kit and the Rootbreaker signature,
and say whether it scales with STR or WIS. Approve moving Stumbling Ground to (-6, -9). Choose the placeholder art.
New portraits and the icon stay with the owner.

## 2. What the 22 Open slots are for

**Today.** All 22 are on rings 14 to 16: Martial 5, Brawn 5, Shadow 4, Wild 6, Brains 1, Grace 1.
- At runtime (`SkillHex.cs`) an Open slot is a hole. No hero can take it, and routes go around it.
- The validator only prints the gap list, and no test asserts the count.
- The page and `SkillHexView` draw a faint ring. No Open node carries a `Note` yet.
- GAPS.md lists 36 hero-side moves that no class lists. Eleven of them are items that belong on the 5-slot bar,
  which leaves **25 candidates for the 22 slots**.

| Option | What the slots are | Validator | Page / game |
|---|---|---|---|
| **A. DLC packs by wedge** | Keep them Open and tag each one with a `Note` such as `DLC: Wild pack`. Packs follow the GAPS wedge briefs (Martial+Brawn, Shadow+Wild, Brains+Grace). | Add a check that every Open slot has a pack `Note`, and print the gap list grouped by pack. | The rings show the pack name on hover. In game, 22 holes stay on the outer rings until a pack ships. |
| **B. Fill now from the 25 unlisted hero-side moves** (recommended) | Place 20 of them by wedge (list below), move Stumbling Ground into one (question 1), and keep **1 Wild slot Open** as reserve with a `Note`. | The "not on the board" count drops from 108 to 88 (72 enemy-only, 11 items, 5 leftovers). Add a check that each remaining Open slot carries a `Note`. | Almost no faint rings. Late-game reach grows in the physical half. GAPS.md becomes history. A balance soak covers the new nodes. |
| **C. Reserve for future hero kits** | Keep them Open, and give each a `Note` naming the planned hero or ticket that will fill it (for example, the 6 Wild slots for the new Wild hero's late kit). | Add a check that a reserved `Note` names a real ticket or hero, with a warning when one goes stale. | Same as A, but labelled by hero. Nothing new is playable until those heroes exist. |

**The fill list for B** follows the GAPS.md wedge briefs. The implementer picks exact slots in the page editor.
- Martial (5): Guard Stance, Last Stand, Bolster, Heavy Armor Proficiency, Martial Weapons Proficiency.
- Brawn (5): Feint, Adrenaline Rush, Shake It Off, Thorns, Second Wind. Check `second_wind` against the placed
  `brawler_second_wind`; it may be a merge alias (GG-T973) and not a node of its own.
- Shadow (4): Sprint, Disengage, Opportunist, Pressure Point.
- Wild (6): Stampede, Drench, Undertow, Focus Breath, Stumbling Ground (moved there), and 1 Open reserve.
- Brains (1): Stasis (Haste Field became Courier's signature in GG-T1138).
- Grace (1): Barrier (`warden_barrier`).
- Left off the board (5): Rune Ward, Flame Lance, Drain Life, Mana Siphon and Arcane Focus (Spellshield became Sage's
  signature in GG-T1138). They
  can be DLC, or replace a weaker move later.

**Why B is recommended.**
- The moves already exist with data and descriptions, and the holes have no use in play today.
- The caster and healer half is already full, so a DLC pack (A) would only ever add physical and Wild moves anyway.
- Option C ties board space to heroes that do not exist yet.
- B still keeps one tagged reserve slot for the Wild hero or a first DLC pack.

**Question 2.** Choose A, B or C, or a mix (for example B, with a different number of slots kept in reserve). For B,
confirm the fill list, or strike items from it.

## 3. Signatures: the 65 placed heroes

All 65 are unique to their class, and every one is at d0 or d1, so the validator gives no warnings. The table flags
the ones that still look weak.

Flag codes:
- **P**: a plain hit. The data has no status or rider, so the move is damage only.
- **D**: it duplicates another hero's signature in effect.
- **S**: self-sustain only.
- **L**: the hero does not start with it on its `DefaultLoadout` bar.
- **T**: the text promises more than the data does.
- **I**: only the id is odd.

Moves marked "Swap" are the hero's own unique, unplaced-as-signature moves near its spot. Moves at d2 need a node
swap with the old signature, as GG-T1039 did. "Tune" keeps the move and gives it the rider its name implies.

**Summary.**
- 41 heroes: keep, no flag.
- 7 swaps: Assassin, Barbarian, Machinist, Monk, Thief and Speedster, plus Dervish, which needs a changed effect.
- 6 tunes: Chroma, Red and Yellow Ninja, Odachi, Striker and Sword Master.
- 2 loadout fixes: Drifter and Wild Child.
- 1 text fix: Hex Sniper.
- 8 keep despite a flag (the other half of a D pair, or a cosmetic or text note).

| Hero | Recipe | Signature | Effect (from abilities.json) | d | Flag | Proposal |
|---|---|---|---|---|---|---|
| Acolyte | INT 2 VIT 1 SPD 1 | Benediction (`sister_benediction`) | Skill, AllAllies, [regen] | 1 | - | Keep |
| Alchemist | WIS 3 AGI 1 | Transmute Pain (`alchemist_transmute`) | Skill, [protection], cleanse | 0 | - | Keep |
| Assassin | AGI 3 SPD 1 | Killing Blow (`assassain_killingblow`) | Skill, 16 Physical, adjacent | 1 | P | Swap to Shadow Swap (d1) |
| Barbarian | VIT 2 STR 2 | Reckless Swing (`barbarian_reckless`) | Skill, 24 Physical, adjacent | 1 | P | Swap to Earthsplitter (d1), or give Reckless Swing a drawback |
| Black Ninja | AGI 2 VIT 1 SPD 1 | Shadow Fang (`blackninja_shadowfang`) | Skill, 14 Dark, adjacent, [blinded] | 1 | - | Keep |
| Blue Lion | WIS 3 SPD 1 | Lion's Roar (`bluelion_roar`) | Skill, Self, [empowered] | 1 | - | Keep |
| Blue Ninja | AGI 2 SPD 1 INT 1 | Frost Fang (`blueninja_frostfang`) | Skill, 10 Ice, adjacent, [slowed] | 0 | - | Keep |
| Bruiser | STR 4 | Haymaker (`bruiser_haymaker`) | Skill, 14 Physical, adjacent, knockback 1, pushback 0.5 | 1 | - | Keep |
| Chroma Ninja | SPD 3 INT 1 | Prism Fang (`chromaninja_prismfang`) | Skill, 20 Physical, adjacent | 0 | P | Tune: give Prism Fang a rider (the Fang series pattern) |
| Cleric | INT 3 VIT 1 | Divine Grace (`cleric_grace`) | Skill, [ward] | 1 | - | Keep |
| Courier | WIS 2 STR 1 INT 1 | Haste Field (`haste_field`) | Spell, AllAllies, hasten 0.12 | 0 | - | Keep (GG-T1138 merged Swift Relay into Shortcut and re-signed Courier) |
| Dark Templar | VIT 2 AGI 1 INT 1 | Dark Rite (`darktemplar_rite`) | Skill, 8 Dark, [blinded] | 1 | - | Keep |
| Defender | VIT 2 STR 1 WIS 1 | Shield Wall (`defender_wall`) | Skill, Cross 1, [ward] | 1 | D | Keep (Dervish changes) |
| Dervish | SPD 2 STR 1 WIS 1 | Whirling Ward (`dervish_whirl`) | Skill, Cross 1, [ward] | 1 | D | Change: same shape and effect as Shield Wall (Cross 1, Ward) |
| Doctor | WIS 3 INT 1 | Field Triage (`doctor_triage`) | Skill, heal 18, cleanse | 1 | - | Keep |
| Drifter | INT 2 STR 1 WIS 1 | Float (`drifter_float`) | Spell, [floating] | 1 | L | Keep; put Float on its DefaultLoadout |
| Duelist | STR 2 AGI 1 INT 1 | Riposte (`duelist_riposte`) | Skill, 16 Physical, adjacent, pushback 1.0 | 1 | - | Keep |
| Engineer | INT 2 AGI 1 WIS 1 | Overclock (`engineer_overclock`) | Skill, [empowered] | 0 | - | Keep |
| Fencer | AGI 3 STR 1 | Fleche (`fencer_fleche`) | Skill, 16 Physical, adjacent, swaps places | 1 | - | Keep |
| Fighter | STR 2 AGI 1 WIS 1 | Combo Strike (`fighter_combo`) | Skill, 14 Physical, adjacent, pushback 1.0 | 0 | - | Keep |
| Green Ninja | SPD 2 VIT 1 WIS 1 | Venom Fang (`greenninja_venomfang`) | Skill, 8 Poison, adjacent, [poisoned] | 0 | - | Keep |
| Harbinger | INT 4 | Omen of Doom (`harbinger_omen`) | Spell, 10 Arcane, [slowed] | 1 | - | Keep |
| Herald | VIT 3 INT 1 | Lifegift (`herald_lifegift`) | Skill, heal 30, costs HP | 1 | - | Keep |
| Hex Sniper | AGI 3 INT 1 | Piercing Shot (`hexsniper_piercingshot`) | Skill, 26 Dark | 0 | P, T | Keep; rewrite the text (it reads as a hag curse) |
| Hex Stalker | SPD 2 WIS 2 | Fading Shot (`hexstalker_fadingshot`) | Skill, 14 Dark, [blinded], stepback 2 | 1 | - | Keep |
| Hex Warden | WIS 3 VIT 1 | Snare Shot (`hexwarden_snareshot`) | Skill, 12 Dark, [slowed] | 0 | - | Keep |
| Hexer | WIS 2 AGI 1 INT 1 | Shadow Bolt (`blackwitch_shadowbolt`) | Spell, 14 Dark, [blinded] | 1 | - | Keep |
| Jade Knight | INT 2 VIT 1 AGI 1 | Jade Resolve (`jadeknight_resolve`) | Skill, Self, [regen] | 0 | D, S | Keep (Monk changes) |
| Knight | VIT 3 STR 1 | Knight's Oath (`knight_oath`) | Skill, [ward], swaps places | 1 | - | Keep |
| Lancer | SPD 3 VIT 1 | Lance Charge (`lancer_chargeline`) | Skill, 12 Physical, Row | 0 | - | Keep |
| Lightweaver | INT 3 SPD 1 | Arcane Ward (`whitewitch_ward`) | Skill, heal 10, [ward] | 0 | - | Keep |
| Machinist | INT 3 STR 1 | Turret Barrage (`machinist_turret`) | Skill, 16 Physical | 0 | P | Swap to Barricade (d2, needs a node swap) |
| Martial Artist | STR 3 WIS 1 | Flowing Palm (`martialartist_palm`) | Skill, 8 Physical, adjacent, [slowed] | 0 | - | Keep |
| Monk | AGI 2 SPD 2 | Inner Peace (`monk_peace`) | Skill, Self, [regen] | 1 | D, S | Swap to Redirect Flow (d1) |
| Monster Tamer | WIS 3 STR 1 | Tame (`tame`) | Skill, tame | 0 | I | Keep (id is generic `tame`) |
| Myrmidon | VIT 3 SPD 1 | Phalanx Hook (`myrmidon_phalanx`) | Skill, 10 Physical, pushback 0.5, pull 2 | 0 | - | Keep |
| Night Hunter | SPD 3 WIS 1 | Night Stalk (`nighthunter_nightstalk`) | Skill, 14 Dark, adjacent, [blurred] | 1 | - | Keep |
| Odachi | STR 3 INT 1 | Iaijutsu (`odachi_iaijutsu`) | Skill, 26 Physical, adjacent | 0 | P | Tune: a bonus when Odachi acts before the target |
| Paladin | VIT 2 INT 2 | Holy Bulwark (`paladin_bulwark`) | Skill, AllAllies, [ward] | 1 | - | Keep |
| Panda Keeper | STR 2 VIT 1 WIS 1 | Bamboo Slam (`pandagirl_slam`) | Skill, 12 Physical, adjacent, [taunting] | 0 | - | Keep |
| Prize Fighter | VIT 2 STR 1 SPD 1 | Knockout Punch (`prizefighter_knockout`) | Skill, 16 Physical, adjacent, [sleep] 50% | 0 | - | Keep |
| Pugilist | STR 3 AGI 1 | Jab-Cross (`pugilist_jabcross`) | Skill, 22 Physical, adjacent | 1 | P | Keep (Counterpunch, d1, is a reaction) |
| Raider | STR 3 SPD 1 | Plunder Strike (`raider_plunder`) | Skill, 14 Physical, adjacent, steals mana | 0 | D | Keep (Thief changes) |
| Red Mage | WIS 2 INT 2 | Elemental Fury (`redmage_fury`) | Spell, 20 Fire, Cross 1 | 1 | - | Keep |
| Red Ninja | SPD 2 VIT 1 AGI 1 | Flame Fang (`redninja_flamefang`) | Skill, 18 Fire, adjacent | 0 | P, T | Tune: add [Burning] (text says "cinder") |
| Ripper | AGI 3 VIT 1 | Rend (`ripper_rend`) | Skill, 10 Physical, adjacent, [poisoned] | 0 | T | Keep; "bleeding" is [Poisoned] in data |
| Ritualist | VIT 2 SPD 1 INT 1 | Blood Rite (`ritualist_bloodrite`) | Spell, 12 Arcane, [poisoned] | 0 | - | Keep |
| Ronin | AGI 3 WIS 1 | Masterless Cut (`ronin_masterless`) | Skill, 14 Physical, adjacent, stepback 2 | 0 | - | Keep |
| Sage | WIS 4 | Spellshield (`warden_spellshield`) | Spell, [ward], cleanse | 1 | - | Keep (GG-T1138 merged Sage's Insight into Disrupt and re-signed Sage) |
| Sellsword | STR 2 VIT 1 SPD 1 | Contract Kill (`sellsword_contract`) | Skill, 20 Physical, adjacent, x1.3 vs boss | 1 | - | Keep |
| Shieldguard | VIT 3 AGI 1 | Valkyrie's Call (`shieldmaiden_valkyrie`) | Skill, heal 30, Self, revives | 0 | - | Keep |
| Slasher | AGI 2 STR 1 WIS 1 | Flurry (`slasher_flurry`) | Skill, 10 Physical, Cross 1 | 1 | - | Keep |
| Speedster | SPD 4 | Blitz Rush (`speedster_blitz`) | Skill, Self, [empowered], hasten 0.2 | 1 | - | Swap to Afterimage (d2, needs a node swap), or keep |
| Street Fighter | AGI 2 STR 1 INT 1 | Dirty Trick (`streetfighter_dirtytrick`) | Skill, 12 Physical, adjacent, [blinded] | 0 | - | Keep |
| Striker | SPD 3 AGI 1 | Finishing Blow (`striker_finishingblow`) | Skill, 20 Physical, adjacent | 1 | P | Tune: a bonus against a hurt foe |
| Sword Master | STR 2 AGI 2 | Perfect Cut (`swordmaster_perfectcut`) | Skill, 24 Physical, adjacent | 1 | P | Tune, or keep as the pure-damage hero |
| Tactician | INT 3 WIS 1 | Countermarch (`tactician_countermarch`) | Skill, swaps turns | 1 | - | Keep |
| Tank | VIT 4 | Juggernaut Charge (`tank_juggernaut`) | Skill, 12 Physical, adjacent, knockback 2, pushback 0.75 | 1 | - | Keep |
| Thief | AGI 4 | Snatch (`thief_snatch`) | Skill, 12 Physical, adjacent, steals mana | 1 | D | Swap to Swipe Boon (d2, needs a node swap) |
| Vampire | INT 3 AGI 1 | Bloodletting Bite (`attrition_bloodletting`) | Skill, 12 Dark, heal 6, adjacent, [poisoned] | 0 | I | Keep (id prefix only) |
| War Chief | STR 3 VIT 1 | Rallying Banner (`warchief_banner`) | Skill, AllAllies, [empowered] | 1 | - | Keep |
| Warden | VIT 3 WIS 1 | Reveille (`warden_reveille`) | Skill, hasten 0.15, cleanse | 0 | - | Keep |
| White Ninja | WIS 2 VIT 1 SPD 1 | Moonlit Recovery (`whiteninja_moonlit`) | Skill, heal 22, Self | 1 | S | Keep, or swap to Substitution (d1, a reaction) |
| Wild Child | SPD 3 STR 1 | Bramble Snare (`wildchild_bramblesnare`) | Spell, [rooted] | 0 | L | Keep; put Bramble Snare on its DefaultLoadout |
| Yellow Ninja | SPD 2 AGI 1 INT 1 | Shock Fang (`yellowninja_shockfang`) | Skill, 18 Lightning, adjacent | 1 | P | Tune: add a shock rider ([Slowed] or [Blinded]) |

**Question 3.** Approve the Proposal column as a whole, or name the rows to change. The key calls are these:
- Dervish needs a new effect. Should it be a ward that also hastens, or a different move?
- Monk becomes Redirect Flow.
- Thief becomes Swipe Boon. Raider keeps Plunder Strike.
- Speedster: keep Blitz Rush, or switch to Afterimage.
- The six plain-hit tunes: tune them, or keep a few heroes as pure damage.
