# GridGame Gaiden: story outline from the art (GG-T386, for GG-T341)

> **Superseded 2026-11-21 (GG-T526).** The owner adopted the Morrow mythos as canon: the moon is a living thing that slept for ten thousand years inside a shell of debris, and Halcyon's drawing of moonlight is pulling it down. It is staged in `Data/Prose/canon/`, to be pushed to Prose with `push.ps1` once the Prose database resumes. Where this outline disagrees with it, the canon wins.

**What this is.** A beat outline for a writer, read out of what the owner's art actually shows: 211 portraits
(`Resources/Portraits`), 81 landscapes in 12 sets (`Resources/Backgrounds`), `factions.json`, `classes.json`, the
family entries in BIBLE §14.11 to §14.19 and the regions in §22.3.x. It is **not** story text. Every word a player
reads is written and approved in Prose (BIBLE §27.2, [PROSE.md](PROSE.md)); the crawls ship as lorem ipsum until then.

**Why a new outline.** The owner (2026-11-16): the moon story, then the fallen-angel story ("A War in Heaven", v4 in
Prose), "doesn't cover what is actually there". Looking at the art as a whole, almost nothing in it is angelic (two
winged figures: the black-winged Murder, who are street kids, and the Herald, a flower-winged Wild girl). What the art
does show, over and over, is below. The names (the Pale Concord, Morrow, the Anchors, the Still, the regions) are kept
because they are already game data and fit the pictures; the writer may rename anything in Prose.

## 1. What the art says, in one paragraph

A huge pale moon hangs over every landscape, and it gets bigger the further the road goes, until in Morrow's Shadow it
fills the sky and the ground floats. A **white** order owns the world's light: its soldiers are white porcelain, white
power armour, white robes, white ponchos; its halls are white domes full of glowing **cyan** tanks; its city is a white
spire. The people it has used up are white too: hooded crowds with empty faces, pale grey-green husks with chrome in
them, giants of white flesh. Everyone who fights it wears **colour**: neon streetwear, hold steel and green cloaks,
indigo and red on the lantern road, the black and blue of the fen witches. The Ember wears **red and black** and wants
the moon to come down. Where the white order has fallen, **red blossom** grows over its broken pillars.

**The thesis for the writer:** colour is life (the aether, the "tithe"); the Concord drains it, stores it and burns it
to keep the moon up; the crew are runaways in a stolen white caravan who learn what the white is made of.

## 2. The world, region by region (campaign order; what each backdrop set shows)

| # | Region (theme id) | Stages | Art | Reading |
|---|---|---|---|---|
| 1 | The Tithe Moors (`greenvalley`) | 1-3 | grey storm moors, moss, standing stones, small moon | home: the holds and Saltmoss, where the tithe is taken |
| 2 | The Lantern Road (`lanternroad`) | 4-6 | stone stairs up night gorges, torii, paper lanterns, low blue moon | the pilgrim road out of the moors |
| 3 | The Shardfall (`desert`) | 7-9 | red earth, rivers of red, giant curved horns/tusks rising from the ground, a vast pale planet behind | where pieces of the moon came down; the Ember's land |
| 4 | The Arkgrave (`swamp`) | 10-12 | wrecked ship hulls and masts in a gold-hazed marsh | ships built to leave, grounded |
| 5 | The Anchor Wastes (`cave`) | 13-15 | colossal blue stone arches over pink-lit snow | the Anchors: arches that hold the moon off |
| 6 | The Still (`still`) | 16-18 | white dome, cyan tanks, moon in the oculus, catwalks | where the tithe is stored |
| 7 | Halcyon (`cityruins`) | 19-21 | ruined tech city, pink-red sky, towers and cables | the Concord's city, gone to ruin under the spire |
| 8 | The Choir's Cathedral (`cathedral`) | 22-24 | cyan organ of pipes, rose windows, moon in the vault | the top of the spire; the organ is the reservoir |
| 9 | The Bloom (`bloom`) | 25-27 | red blossom and moss over broken white pillars, pale moon | what grows where the stored colour came down |
| 10 | The Free Holds (`greenvalley2`) | 28-29 | walled hill-forts over gold grass, pale moon | the holds, walled up and trading |
| 11 | The Underspire (`cityruins2`) | 30-31 | neon night markets in the rain, the white spire behind cables | the Street's home under the spire |
| 12 | The Drowned Arks (`swamp2`) | 32-33 | Arkgrave art | the Arkgrave flooding |
| 13 | Deep Shardfall (`desert2`) | 34-35 | Shardfall art | the Ember's heartland, shards still falling |
| 14 | Under the Anchors (`cave2`) | 36-37 | Anchor Wastes art | beneath the arches, the chains |
| 15 | Morrow's Shadow (`morrowsshadow`) | 38-40 | a colossal moon over cracked plains, floating rock | where the moon hangs lowest |

Region names checked against their art: all fit. One fix made: `greenvalley2` was still "The Moors Beyond the Stills"
while its stages and art are the Free Holds; it is now **The Free Holds**. Every campaign stage is named for its region
plus a numeral ("The Tithe Moors I", "Deep Shardfall II").

## 3. Who is who, as drawn

### The crew's side: six hero factions (factions.json)

| Faction | Look | Role in the story |
|---|---|---|
| **The Street** (24 heroes) | oversized jackets and hoodies in orange, pink, neon green and yellow; cargo pants, platform sneakers, chrome limbs, big blades and bats | the young of the Underspire; couriers, fighters, thieves, engineers; loud colour as defiance |
| **The Free Holds** (11) | plate and kite shields, greens and whites, furs, warhammers, axes | the hill-fort folk the Concord tithes; knights and barbarians |
| **The Lantern Road** (15) | indigo and black, red sashes, masks, katana and odachi, a straw kasa, a monk, a sage with a starry staff | pilgrims and road-wardens of the mountain road |
| **The Fen-dark** (14) | black and deep blue robes, witch hats, plague-doctor and nun habits, a lab coat, a white-ice aetherist | the marsh's witches, healers and hex-hunters; the ones who know what aether is |
| **The Ember** (1) | black with a red sword and red cape | a deserter from the Ember (the Dark Templar) |
| **The Wild** (4) | a blue lion, a flower-winged girl (the Herald), a beast-tamer | the living world itself, siding with colour |

### The powers they fight (enemy families, grouped by what the art shares)

- **The Pale Concord (all white).** The Kiln Guard (`ceramicknight`, porcelain knights in white capes), the Red Seal
  (`templar`, white robes with red crosses), the Bulwark (`heavytrooper`, white power armour, siege rifles, an aether pack
  each), the Choir (`cultist`, faceless white-and-violet ponchos printed with glyphs), shield-walkers (`mechaarmor`, white
  tower shields), vat-giants (`cyclops`, white flesh, one red eye), the Chronomancer (white, clock wheels), the Hierarch
  (no portrait yet). The **Hollow** (`undead`, white-hooded crowds with empty faces) and the **Chromed Hollow**
  (`cyberzombie`, grey-green husks with chrome) are what is left of people the Concord has drained.
- **What leaks from the Anchors.** The Anchor Watch (`wraithknight`, grey plate, cloaks of old fire, kept alive by the
  arches) and the Void Sentinel (smoke around a cyan heart).
- **The Ember (red and black).** Infernalists (black armour, long red capes), Hellspawn (red scale, manes of blood-light),
  the Demon Lord. They drink the Shardfall's red rivers and want the moon home.
- **The Street's crews.** Redline (`ganger`, masked, red hoodies, swords), the Murder (`harpy`, neon street kids with
  black wings), the Herd (`minotaur`, bull-spliced kids in hoodies and sneakers), and the Street's dead: the Ghost, the
  Phantom and the Reaper, three hooded girls.
- **The marsh.** Hags (dark blue, veiled), marsh shamblers, lurkers, frogs, the naga, the Swamp Mistress (green tendrils),
  the Lich (purple, crowned; the last captain of a grounded Ark).
- **The road.** Oni (masked, kimono, crouched) and Steppin Razors (veiled women with claw-blade hands).
- **The Shardfall dig.** Purple prototypes (purple bio-mechanical bodies) and tech gremlins (red-hooded, goggled): things
  made, not born. With them the Captain's hold militia (`soldier`, helmets and green cloaks).
- **The Wild under the moon.** Wolves, a werewolf, bats, vultures, a flying monkey, scorpions, the sand maw, the yeti,
  the ice mauler, the mountain troll; moss-golems (`treegolem`, skeletons grown over with moss) in the marsh and the Bloom.
- **The Captain.** Dark plate under a white tabard: a hold officer in Concord colours. He is the one recurring human
  villain (Tithe Moors I and III, the Shardfall, the Free Holds, Deep Shardfall).
- **Placeholders.** The region-rule foes (holdwrights, rams and marksmen; spire burrowers, pit lurkers, floorbreakers;
  tidestriders, stormcallers, drowners; shard skitters, shardcallers, crystal brutes; chain wardens, hookthrowers,
  anchor hulks; dusk stalkers, lamp snuffers, gloom menders, the dusk mimic) and the Hierarch still use the mannequin.
  Their art, when it comes, may change these beats.

## 4. The crawl slots and their beats

A crawl plays at the **prologue**, after **every fifth campaign stage** (`campaign.json` `Chapters`, GG-T386) and at the
**ending** (after the Hierarch, stage 24). Each scroll closes the five stages just played and opens the next five. Keys
are `crawl:prologue`, `crawl:chapter/<id>`, `crawl:ending`; titles are in `campaign.json` and the seed.

| Slot | Plays | Title | Opens stages |
|---|---|---|---|
| Prologue | after the party pick | Tithe Day | 1-5: Tithe Moors I-III, Lantern Road I-II |
| `ch01` | after Lantern Road II (5) | Over the Pass | 6-10: Lantern Road III, Shardfall I-III, Arkgrave I |
| `ch02` | after Arkgrave I (10) | The Grounded Arks | 11-15: Arkgrave II-III, Anchor Wastes I-III |
| `ch03` | after Anchor Wastes III (15) | Where the Beams Go | 16-20: The Still I-III, Halcyon I-II |
| `ch04` | after Halcyon II (20) | The Spire Stair | 21-24: Halcyon III, Cathedral I-III (then the ending) |
| Ending | after Cathedral III (24) | Still Here | (credits; the postgame opens) |
| `ch05` | after Bloom I (25) | What Came Down | 26-30: Bloom II-III, Free Holds I-II, Underspire I |
| `ch06` | after Underspire I (30) | Claimants to the Spire | 31-35: Underspire II, Drowned Arks I-II, Deep Shardfall I-II |
| `ch07` | after Deep Shardfall II (35) | The March on the Anchors | 36-40: Under the Anchors I-II, Morrow's Shadow I-III |
| `ch08` | after Morrow's Shadow III (40) | Morrow Holds | (the epilogue) |

### Prologue: Tithe Day
- **Where:** Saltmoss, a hold on the Tithe Moors, on tithe day. Small moon, grey storm.
- **Who:** the white collectors arrive with the Captain's hold militia; the crew are young people from three or four
  factions who were in town for the tithe.
- **Conflict:** the tithe takes the colour out of people (the Hollow walk behind the collectors' caravan). The crew steal
  the Concord's own white caravan and run.
- **Ahead:** the moors' wild things, Redline and their hired Herd and Murder on the roads, the Captain's militia; the
  Captain himself at Tithe Moors III; then the Lantern Road.

### ch01: Over the Pass
- **Closes:** the moors behind them; the Captain beaten once but not finished; the Street crews they fought on the moor
  roads were hired to bring the caravan back.
- **Who:** Lantern Road pilgrims and road-wardens (ninjas, ronin, the monk, the sage); oni raiders and Steppin Razors on
  the pass; the moon-changed wolves.
- **Conflict:** the oni chief holds the top of the pass; beyond it the road drops into red country.
- **Boss:** the oni chief (Lantern Road III, Undying). Then the first sight of the Shardfall: horns of fallen moon in red
  earth.
- **Ahead:** the Shardfall's Ember zealots and hellspawn, the Concord's dig (shield-walkers, purple prototypes, gremlins)
  guarded by the Captain again (shield-walker boss, Shardfall III); the Lich at the Arkgrave's edge.

### ch02: The Grounded Arks
- **Closes:** what the Shardfall showed: the moon is shedding, the Concord mines what falls, and the Ember drinks the red
  rivers and welcomes the fall. The Dark Templar may desert to the crew here.
- **Who:** the Fen-dark witches and healers of the marsh; hags, shamblers, lurkers, the naga; the Lich (the Ark captain
  who would not let his ship die).
- **Conflict:** the Arks were built to leave and were grounded. Who grounded them, and why the Concord wants no one to go.
- **Boss:** the Swamp Mistress, the drowned Ark's pilot (Arkgrave III). Then the arches on the horizon.
- **Ahead:** the Anchor Wastes: the Anchor Watch, the Kiln Guard, vat-giants, the Void Sentinel where an Anchor leaks, a
  first look at the Chronomancer; the vat-giant keeping the great Anchor (Anchor Wastes III).

### ch03: Where the Beams Go
- **Closes:** the Anchors hold the moon off, and they run on the tithe; the beams carry it somewhere.
- **Who:** the Still's queues of Hollow, the Bulwark guarding the tanks, the Choir singing the aether out of people.
- **Conflict:** the tanks hold stored people (the crew finds a whole hold in one). The Still keeps more than the
  Anchors need.
- **Boss:** the Chronomancer (The Still III, TwinTurn). Then Halcyon: the white city in ruins under the spire.
- **Ahead:** Halcyon's Street ghosts, the Herd for hire, the Bulwark squads.

### ch04: The Spire Stair
- **Closes:** the surplus climbs the spire; someone at the top drinks it.
- **Who:** Halcyon's ruins and the Street's dead (the Ghost, the Phantom, the Reaper); the Kiln Guard on the stair; the
  Choir and the Red Seal in the cathedral.
- **Conflict:** the Hierarch's organ is the reservoir; the Choir sings the tithe up into it. A foreshadow on the stair:
  the Demon Lord arrives in Halcyon III's fourth wave (a surprise reinforcement), so the Ember's master has come for the
  same reservoir.
- **Boss:** the Kiln Guard captain (Halcyon III), then the Hierarch (Cathedral III, Undying while the Choir stands).

### Ending: Still Here
- **Beat:** the Hierarch falls and the organ breaks; the stored colour comes down over the world. The moon does not fall
  (the Anchors still hold), but it hangs as low as ever. The crew are still here. Credits; the postgame opens.

### ch05: What Came Down
- **Closes:** the Bloom: red blossom over the Concord's broken pillars, where the reservoir came down.
- **Who:** Redline harvesting the Bloom, the Red Seal's last wardens and Bulwark holdouts burning it, moss-golems
  guarding it; the Free Holds walled up and trading in it.
- **Conflict:** with the Hierarch gone, everyone wants what came down. The holds are selling it; the Captain is behind it.
- **Boss:** the Red Seal's last warden (Bloom III); the Captain's last stand at the holds (Free Holds II).
- **Ahead:** the Underspire's markets.

### ch06: Claimants to the Spire
- **Closes:** the holds, freed of the Captain.
- **Who:** the Street at home: Redline, the Murder, the Herd, what is left of the Choir; the floor of the Underspire
  breaking under them.
- **Conflict:** three claimants to the empty spire (the Herd Boss, the Choir, Redline). Then the wider question: the
  moon. Leave on the Arks, or stand?
- **Boss:** the Herd Boss (Underspire II). Then the Drowned Arks (the Swamp Mistress again, the rising tide) and Deep
  Shardfall (shard strikes; the Captain's militia still at the dig).

### ch07: The March on the Anchors
- **Closes:** the Arks will not fly; the Ember is marching on the Anchors to cut them.
- **Who:** under the arches: chain wardens, hookthrowers, anchor hulks hauling the chains; the Anchor Watch; the Ember's
  host.
- **Conflict:** if the Anchors go, the moon comes home. The crew go under the Anchors, then out to where the moon hangs
  lowest.
- **Boss:** the Demon Lord in Morrow's Shadow III (dusk stalkers, lamp snuffers, gloom menders and the dusk mimic in the
  dark before him).

### ch08: Morrow Holds (epilogue)
- **Beat:** the Ember's master falls under the moon he called; the Anchors hold; the crew choose what the stolen white
  caravan becomes. Morrow stays in the sky. (Whether the answer is "keep the Anchors" or "find another way" is the
  writer's and owner's call.)

## 5. Notes for the writer and the owner

- **Colour is the through-line** the art supports best; the Bloom (red over white) is its payoff image.
- **Not in the art:** angels, a war in heaven, a celestial host. If the "War in Heaven" premise stays, it needs art; if
  not, it can be retired in Prose like "Under Morrow".
- **Open questions:** what the moon is (the Shardfall's horns suggest it is a body, or the bones of one); who the Herald
  is (the one winged figure on the crew's side); where the purple prototypes are made; whether the Captain survives the
  Free Holds (he still appears in Deep Shardfall).
- **Chronomancer early:** it appears in Anchor Wastes II (wave 2) before its own finale in the Still; the outline treats
  that as a first sighting.
- The per-theme crawls are retired (one scroll per five stages instead), so a region's arrival is carried by the
  chapter scroll and the region's `*_party_talk` beat.
