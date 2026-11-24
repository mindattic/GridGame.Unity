# skill-hex gaps: the Open slots as the future content and DLC list

The board (`Assets/GridGame/Resources/Content/skillhex.json`) has 271 ability slots: 249 hold an
ability and 22 are `Open`. Every Open slot is on the outer rings (14 to 16), so new content lands at
the edge of a wedge, as late-game reach for the heroes nearby rather than as a starter move.
`python tools/skillhex/validate.py` prints the live list; this file adds what is around each slot
and what should go in it.

Plan and model: [PLAN.md](PLAN.md). Page: [index.html](index.html).

## Where the room is

| Wedge | Stats | Open | Read |
|---|---|---|---|
| Martial | VIT → STR | 5 | Room to grow |
| Brawn | STR → AGI | 5 | Room to grow |
| Shadow | AGI → SPD | 4 | Room to grow |
| Wild | SPD → WIS | 6 | Most room |
| Brains | WIS → INT | 1 | Nearly full |
| Grace | INT → VIT | 1 | Nearly full |

20 of the 22 gaps are in the physical and wild half of the board (Martial, Brawn, Shadow, Wild).
That is where expansions should add kits: melee, weapon, rogue and beast moves. Brains and Grace
(the caster and healer half) are nearly full, so a new caster or healer move there has to replace
something or wait for the board to grow.

## The 22 Open slots

Stat lean is the sum of the six passives around the slot. Nearest abilities are the three closest
placed abilities (lattice distance in brackets). Nearest heroes are the two heroes whose Signature
node is closest.

| Q,R | Wedge | Ring | Stat lean | Nearest abilities | Nearest heroes |
|---|---|---|---|---|---|
| 1,13 | Martial | 14 | STR +12 | Skull Crack (2), Teleport (2), Haymaker (2) | Basher, Bruiser |
| 2,14 | Martial | 16 | STR +8 | Haymaker (2), Rallying Banner (2), Skull Crack (3) | Bruiser, War Chief |
| 5,11 | Martial | 16 | STR +8 | Rallying Banner (2), Last Man Swinging (2), Berserk (2) | War Chief, Raider |
| 11,5 | Martial | 16 | VIT +8 | Marked for the Warband (2), Stand Guard (2), Knight's Oath (2) | Knight, Barbarian |
| 14,2 | Martial | 16 | VIT +8 | Knight's Oath (2), Lantern of the Still (2), Juggernaut Charge (2) | Knight, Tank |
| -10,14 | Brawn | 14 | AGI +12 | Fleche (2), Masterless Cut (2), Rejuvenate (2) | Fencer, Ronin |
| -9,15 | Brawn | 15 | AGI +9, STR +3 | Probing Thrust (2), Perfect Cut (2), War Cry (2) | Sword Master, Fencer |
| -6,15 | Brawn | 15 | STR +9, AGI +3 | Perfect Cut (2), War Cry (2), Second Wind (2) | Sword Master, Pugilist |
| -11,16 | Brawn | 16 | AGI +8 | Fleche (2), Swipe Boon (3), Masterless Cut (3) | Fencer, Ronin |
| -2,16 | Brawn | 16 | STR +8 | Jab-Cross (2), Skull Crack (2), Haymaker (2) | Pugilist, Basher |
| -14,1 | Shadow | 14 | SPD +12 | Afterimage (2), Blitz Rush (2), Finishing Blow (2) | Speedster, Striker |
| -14,10 | Shadow | 14 | AGI +12 | Shadow Swap (2), Redirect Flow (2), Killing Blow (2) | Assassin, Ripper |
| -15,6 | Shadow | 15 | SPD +9, AGI +3 | Inner Peace (2), Vanish (2), Inner Calm (2) | Monk, Striker |
| -16,5 | Shadow | 16 | SPD +8 | Finishing Blow (2), Vanish (2), Afterimage (3) | Striker, Monk |
| -13,-1 | Wild | 14 | SPD +12 | Blitz Rush (2), Night Stalk (2), Steal (2) | Speedster, Night Hunter |
| -10,-4 | Wild | 14 | SPD +12 | Night Stalk (2), Bramble Snare (2), Pack Rally (2) | Night Hunter, Wild Child |
| -6,-9 | Wild | 15 | WIS +9, SPD +3 | Fading Shot (2), Flare Shot (2), Intimidate (2) | Hex Stalker, Blue Lion |
| -14,-2 | Wild | 16 | SPD +8 | Blitz Rush (2), Night Stalk (2), Bramble Snare (3) | Speedster, Night Hunter |
| -11,-5 | Wild | 16 | SPD +8 | Night Stalk (2), Pack Rally (2), Bramble Snare (3) | Night Hunter, Wild Child |
| -2,-14 | Wild | 16 | WIS +8 | Lion's Roar (2), Pitfall Volley (2), Sage's Insight (2) | Blue Lion, Sage |
| 2,-16 | Brains | 16 | WIS +8 | Sage's Insight (2), Arcane Insight (2), Field Triage (2) | Sage, Doctor |
| 16,-2 | Grace | 16 | VIT +8 | Sacred Challenge (2), Lifegift (2), Juggernaut Charge (2) | Herald, Tank |

Basher is a cut hero; its Skull Crack node stays on the board.

## Design briefs per wedge

**Martial (VIT → STR), 5 slots.** Three gaps sit on the STR side among Bruiser, War Chief and
Raider (Haymaker, Rallying Banner, Berserk, Last Man Swinging): heavy single hits and warband
shouts, such as a two-handed finisher that grows with missing HP, or a banner that moves allies
forward on the timeline. Two gaps sit on the VIT side between Knight and Tank (Knight's Oath, Stand
Guard, Juggernaut Charge): protection with an edge, such as a taunt that hits back, a brace that
turns blocked damage into a shove, or a charge that ends in a guard. Unused moves that fit:
Guard Stance, Last Stand, Bolster, Heavy Armor Proficiency, Martial Weapons Proficiency.

**Brawn (STR → AGI), 5 slots.** The AGI side (Fencer, Ronin, Sword Master: Fleche, Masterless
Cut, Probing Thrust, Perfect Cut) wants precise blade work: a parry-riposte, a lunge that skips a
tile, or a cut that hits harder on a foe that has not acted yet. The STR side (Pugilist, Basher:
Jab-Cross, Skull Crack, Haymaker) wants fist combos: a body blow that slows, or a combo ender
that pays off earlier jabs. Unused moves that fit: Feint, Adrenaline Rush, Shake It Off, Thorns.

**Shadow (AGI → SPD), 4 slots.** Three gaps lean SPD (Speedster, Striker, Monk: Afterimage,
Blitz Rush, Finishing Blow, Vanish, Inner Peace): tempo tricks, such as a dash that strikes twice
if it acts first, or a calm stance that pushes the next foe back on the timeline. One gap leans
AGI beside Assassin and Ripper (Shadow Swap, Killing Blow, Redirect Flow): a backstab or bleed
that pays off position. Unused moves that fit: Sprint, Disengage, Opportunist, Pressure Point,
Focus Breath.

**Wild (SPD → WIS), 6 slots, the most room.** Four gaps lean SPD between Speedster, Night Hunter
and Wild Child (Night Stalk, Pack Rally, Bramble Snare, Steal): hunting and pack moves, such as a
pounce from stealth, a beast call, a trap that roots, or a scent mark that the pack hits harder.
Two gaps lean WIS beside Hex Stalker, Blue Lion and Sage (Fading Shot, Flare Shot, Lion's Roar,
Pitfall Volley): nature magic and marking shots, such as a curse arrow or a roar that silences.
The new Wild druid-brawler (WIS 2, STR 1, SPD 1) is the natural owner of several of these. Unused
moves that fit: Stampede (`stampede_rush`), Dog Whistle (as a learned tame call), Drench,
Undertow.

**Brains (WIS → INT), 1 slot.** Ring 16 at the Wild edge, between Sage and Doctor (Sage's Insight,
Arcane Insight, Field Triage). A WIS-leaning support spell: a party-wide foresight or a triage that
spends mana for tempo. Candidates: Haste Field, Stasis.

**Grace (INT → VIT), 1 slot.** Ring 16 at the Martial edge, between Herald and Tank (Sacred
Challenge, Lifegift, Juggernaut Charge). A VIT-leaning holy guard: a ward that taunts, or a heal
that also shields. Candidates: Barrier, Spellshield, Rune Ward (the three `warden_*` spells).

## Abilities in abilities.json that are not on the board (110)

No hero class in `classes.json` lists any of these, so none is a hero kit move that was dropped.
They split into moves only enemy classes list (72) and moves no class lists at all (38). The
second group is the hero-side pool to draw from first when filling the gaps above.

### Enemy only (72): listed by enemy classes, no hero class

- Charge spells (34): charge_absolution, charge_aegis_field, charge_aether_siphon,
  charge_blood_rite, charge_bull_rush, charge_call_shards, charge_conduct, charge_drag_anchor,
  charge_ember_vigil, charge_feed_the_herd, charge_floor_shove, charge_gale_shriek,
  charge_hellfire, charge_hook_line, charge_howl, charge_ice, charge_mend, charge_miasma,
  charge_mockingecho_mock, charge_psalm_of_certainty, charge_raise_barricade, charge_rally,
  charge_ram_smash, charge_red_tide, charge_siege_shot, charge_slime_glob, charge_smite,
  charge_snuff_lights, charge_soulfire, charge_temporal_drag, charge_thunder,
  charge_tithe_draught, charge_tithe_hymn, charge_void_beam.
- Passives (15): bloodscent, brass_knuckles, cinder_claws, double_attack, ethereal,
  executioners_verdict, frostborn, infernal_hide, jammer_darts, lifesteal, regenerate,
  rotting_bite, suppressing_fire, talon_rake, tough_hide.
- Reactions (15): carrion_feast, counter_attack, gelatinous, herd_rage, herd_rage_kin,
  herd_stampede, mirror_plating, oathbound, rime_riposte, searing_scales, serpents_retort,
  spiteful_hex, time_slip, toxic_skin, void_core.
- Skills and spells (8): choir_rune, chrono_rewind, enrage, gremlin_sabotage,
  hierarch_open_reservoir, lich_soul_harvest, sapper_buried_charge, scorpion_burrow.

### No class lists them (38): hero-side moves left off

- Skills (13): adrenaline_rush, bolster, disengage, feint, focus_breath, guard_stance,
  last_stand, opportunist, pressure_point, second_wind, shake_it_off, sprint, stampede_rush.
- Spells (9): drain_life, drench, flame_lance, haste_field, stasis, undertow, warden_barrier,
  warden_runeward, warden_spellshield.
- Passives and reactions (5): arcane_focus_proficiency, heavy_armor_proficiency,
  martial_weapons_proficiency, mana_siphon, thorns.
- Items (11): disarm_kit_use, dog_whistle_call, flask_arcane_burst, flask_fire_burst,
  flask_frost_burst, flask_holy_burst, flask_shadow_burst, flask_storm_burst, flask_venom_burst,
  grenade_blast, purifying_salts_use. These belong on the 5-slot bar as consumables, not on the
  board.
