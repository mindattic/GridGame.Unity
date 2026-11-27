# GridGame Gaiden: story outline (GG-T1319, was GG-T386)

**What this is.** The beat outline for the writer: the chapters, what each one reveals and who reveals it, what the
Commander gains or loses, and where every crawl falls. It agrees with the canon in `Data/Prose/canon/` (the road table
in `06-arc.md`, the regions in `07-regions.md`); where they ever disagree, the canon wins and this file is fixed. It is
**not** story text: every word a player reads is written and approved in Prose (BIBLE §27.2, [PROSE.md](PROSE.md)).

**The story in one paragraph.** The Commander, a person of Halcyon who died in the Still the night Halcyon fell, is
brought back 1,200 years later when the Choir's tithe runs through the Still's tanks. On tithe day in Saltmoss a
handful of present-day people steal the Choir's white tithe caravan and run, and the Commander goes with them. Near
the Commander, Halcyon tech wakes, so the crew can fight with Mana Orbs and open doors no one has opened in centuries.
The road from the Tithe Moors to the Choir's Cathedral shows them, one piece at a time, what the Manalith is doing,
what Halcyon did to stop it, and that the Hierarch drinks the surplus of the Lullaby. The Hierarch falls, but the
Manalith is still coming; the postgame road runs to Morrow's Shadow, where the player chooses how it ends.

## 1. The rules for the arc

- **No one holds every answer.** Each revelation is carried by a different modern character or Camp keeper. The
  Commander wakes the tech that shows it (a mural, an Ark door, an archive) but does not remember it; someone else reads
  it out. Only the Hierarch knows the whole truth, and tells it too late to matter.
- **The Commander learns what Halcyon did.** Their memory is in pieces: how parts of Halcyon worked, faces, orders. Each
  chapter gives back a piece, and the pieces add up to Halcyon's choice to stop, feed and drink from the Manalith, and
  the Commander's own part in it.
- **Strength and target are the same thing.** Halcyon keyed its machines to its own people's mana. That is why the orbs
  fight for this party and why the Anchors will answer; it is also why the Choir hunts the Commander from the Arkgrave
  on, and why every claimant after the Hierarch wants them.
- **Tone: bittersweet** (GG-ARC, Decisions). Every chapter takes something from someone; warmth lives between the crew.
- **Keep it simple.** One revelation per region at most, one carrier each. No lore the game does not need.

## 2. Chapters and triggers

Chapters follow region boundaries: each one opens one or more whole regions. A chapter crawl plays once, after the
**first victory** on the last stage before its regions, on the way back to Camp (or at the next launch, BIBLE §27).
The main campaign ends at The Choir's Cathedral III with the ending crawl (trigger unchanged: `Ending.Stage` =
`cathedral-03`). The postgame runs to Morrow's Shadow and the player's chosen ending.

| Slot | Title | Plays after (stage, index) | Regions it opens (stages) |
|---|---|---|---|
| Prologue | Tithe Day | the new campaign, after the party pick | The Tithe Moors (1-3) |
| `ch01` | Over the Pass | The Tithe Moors III, `greenvalley-03` (3) | The Lantern Road (4-6), the Shardfall (7-9) |
| `ch02` | The Grounded Arks | The Shardfall III, `desert-03` (9) | The Arkgrave (10-12) |
| `ch03` | Where the Beams Go | The Arkgrave III, `swamp-03` (12) | The Anchor Wastes (13-15), the Still (16-18) |
| `ch04` | The Spire Stair | The Still III, `still-03` (18) | Halcyon (19-21), the Choir's Cathedral (22-24) |
| Ending | Still Here | The Choir's Cathedral III, `cathedral-03` (24) | (credits; the postgame opens) |
| `ch05` | What Came Down | the ending, `cathedral-03` (24): the next arrival at Camp after the credits | The Bloom (25-27) |
| `ch06` | Claimants to the Spire | The Bloom III, `bloom-03` (27) | The Free Holds (28-29), the Underspire (30-31), the Drowned Arks (32-33) |
| `ch07` | The March on the Anchors | The Drowned Arks II, `swamp-05` (33) | Deep Shardfall (34-35), Under the Anchors (36-37) |
| `ch08` | Morrow Holds | Under the Anchors II, `cave-05` (37) | Morrow's Shadow (38-40) |
| Final | (one per ending) | Morrow's Shadow III, `morrowsshadow-03` (40) | (the chosen ending; credits) |

`campaign.json` `Chapters` follows this table (GG-T1480; it fired every fifth stage before). The ids and titles kept
their GG-T386 values, so saves need no SeenCrawls migration: every chapter now plays earlier than it did. `ch05` sits on
the Ending's stage and plays on the next arrival at Camp (or the next stage launch) after the credits. The Final slot
has no data yet: it waits on the ending choice.

## 3. The beats, chapter by chapter

Each chapter lists its beats, the revelation, who carries it, and what the Commander gains or loses. Boss
introductions are the existing `StageStart` beats (BIBLE §27.0); the Camp keepers' join beats are BIBLE §25.0.1.1.

### Prologue: Tithe Day (the Tithe Moors)
- **Beats:** the Commander wakes in a cold tank in the Still, walks out past sleeping collectors and is in Saltmoss by
  dawn on tithe day. The white collectors and the Captain's hold militia take the colour; the Hollow walk behind the
  caravan. A handful of present-day people steal the Choir's white caravan and run, and the Commander goes with them. A
  dead lantern on the caravan lights in the Commander's hand. The Captain gives chase and is beaten at The Tithe
  Moors III.
- **Revelation:** none yet, only the question: why does Halcyon's tech wake for this stranger?
- **Who carries it:** the Merchant (joins at The Tithe Moors I), who sees the lantern relight; the Innkeeper (II), whose
  village was tithed empty; the Blacksmith (III), the Captain's forced smith.
- **The Commander:** gains a crew and the orbs. Has nothing else: no name anyone knows, no home that still stands.

### ch01: Over the Pass (the Lantern Road, the Shardfall)
- **Beats:** the pilgrim road climbs out of the moors; oni and Steppin Razors on the pass; the oni chief at The Lantern
  Road III. A shrine mural, dark for centuries, lights when the Commander touches it. Over the pass the road drops into
  red country: horns of fallen shell in red earth, the Choir's dig guarded by the Captain's shield-walker, Ember zealots
  drinking the red rivers.
- **Revelation:** the oldest art shows the Moon alone, with no Manalith; then, in the Shardfall, the shell is falling
  and some of what fell is the Manalith's body. The Choir mines it; the Ember want the rest of it down.
- **Who carries it:** a Lantern Road sage reads the mural; the Alchemist (joins at The Lantern Road I) notices the
  Commander's mana is whole where everyone else's is thin; the Blacksmith says the shard-glass of the Crucible is not
  stone; the Ember deserter (the Dark Templar) names the Ember's creed and may join here.
- **The Commander:** recognises the sky on the mural without knowing why. Learns their mana is not like anyone else's.

### ch02: The Grounded Arks (the Arkgrave)
- **Beats:** wrecked hulls in a gold-hazed fen; the Fen-dark among the wrecks; the Lich, an Ark's last captain who
  would not let his ship die. An Ark door opens for the Commander, the first door the crew could not force. The Swamp
  Mistress, the drowned Ark's pilot, at The Arkgrave III.
- **Revelation:** Halcyon's Arks, thrown inland by the tides the night Halcyon fell. Halcyon tried to run.
- **Who carries it:** the Enchantress (joins at The Arkgrave I), a binder taught from Halcyon scraps, reads the Ark's
  manifest; the Fen-dark, who know who sleeps in the wrecks.
- **The Commander:** remembers boarding orders, and that they were not on a ship. The opened door is noticed: from here
  the Choir hunts the Commander, not only the caravan.

### ch03: Where the Beams Go (the Anchor Wastes, the Still)
- **Beats:** colossal arches over pink-lit snow; the Watch's wraith knights, still at their posts, lower their blades to
  the Commander and fight the crew anyway; a vat-giant keeps the great Anchor. Then back to where it started: the
  Still, its queues of Hollow, the Bulwark guarding the tanks, the Chronomancer keeping its clocks (The Still III).
- **Revelation:** the arches are taps, not supports; the draw is the pull. The Still's tanks hold the tithe, and more of
  it than the Lullaby needs.
- **Who carries it:** the Watch, who salute; a Street engineer who reads the arch the Commander woke; the Innkeeper, who
  finds her village's colour in a tank.
- **The Commander:** learns where they died, and that the tithe running through the tanks is what brought them back.
  Their life was paid for with other people's.

### ch04: The Spire Stair (Halcyon, the Choir's Cathedral)
- **Beats:** the dead city under a pink sky; the Street's ghosts; a dead archive wakes for a moment near the Commander.
  The Kiln Guard captain on the spire stair (Halcyon III), with the Demon Lord's surprise arrival in its fourth wave
  as a foreshadow. The Choir sings the tithe up into the organ; the Hierarch at Cathedral III, Undying while the Choir
  stands.
- **Revelation:** the archive shows the stopping, the feeding and the fall: the mana in everyone is the Manalith's. At
  the top of the spire the organ is the reservoir, and the Hierarch drinks the skim.
- **Who carries it:** a Fen-dark witch reads the archive; a Choir runaway in the crew names the skim; the Hierarch, the
  only one who knows the whole truth, tells the rest in the boss introduction and wants the Commander as Halcyon's key.
- **The Commander:** sees their own face among the commanders who chose to feed the Lullaby. Learns what Halcyon did and
  that they helped do it.

### Ending: Still Here
- **Beat:** the Hierarch falls, the organ breaks and the skim ends. The Manalith does not fall (the Anchors still hold),
  but it hangs as low as ever and is still coming. The crew are still here. Credits; the postgame opens.
- **The Commander:** has broken what their people built, and kept the crew.

### ch05: What Came Down (the Bloom)
- **Beats:** red blossom and moss over the Choir's broken white pillars; Redline harvesting it, the Red Seal's last
  wardens and Bulwark holdouts burning it, moss-golems guarding it; the Red Seal's last warden at The Bloom III.
- **Revelation:** where the Manalith's substance took root, things grow. It is not only death.
- **Who carries it:** the Herald and the Wild, who live in it.
- **The Commander:** finds something of the Manalith that is not hungry, and the first place where their being alive
  costs no one.

### ch06: Claimants to the Spire (the Free Holds, the Underspire, the Drowned Arks)
- **Beats:** the holds walled up and trading in the Bloom, the Captain's last stand (The Free Holds II); the Underspire's
  neon markets on a breaking floor, three claimants to the empty spire (the Herd Boss, what is left of the Choir,
  Redline) and the Herd Boss at The Underspire II; the Arkgrave flooding, the Swamp Mistress again on the rising tide.
- **Revelation:** everyone wants the empty spire. The Underspire is still drawing on Halcyon's taps, and the Captain is
  still collecting.
- **Who carries it:** a Free Holds knight, whose oath the Captain sold; a Street runner, whose lights run on the taps;
  the Merchant, who hears every claimant ask the price of the Commander.
- **The Commander:** learns they are now the prize. Every claimant wants the key the Choir wanted.

### ch07: The March on the Anchors (Deep Shardfall, Under the Anchors)
- **Beats:** shard strikes on the old dig and the Captain's militia still at it; the Ember host marching; under the
  arches, chain wardens, hookthrowers and anchor hulks hauling the chains.
- **Revelation:** the Ember march on the Anchors to open every tap and drag the Manalith down.
- **Who carries it:** the Ember deserter, whose own people lead the march; the Watch's last captain, who gives the
  Anchors back to a commander of Halcyon.
- **The Commander:** gains the Anchors. Only Halcyon's mana can open or close them, so every ending now runs through the
  Commander.

### ch08: Morrow Holds (Morrow's Shadow)
- **Beats:** the Manalith fills the sky, its crystals close enough to count, the Moon a small, steady disc beside it;
  floating rock over cracked plains; dusk stalkers, lamp snuffers, gloom menders and the dusk mimic in the dark; the
  Demon Lord at Morrow's Shadow III, falling under the Manalith he called.
- **Revelation:** the Anchors hold, and the choice is the player's.
- **Who carries it:** the Camp keepers, each speaking for what they would keep.
- **The Commander:** has every piece they will get, and chooses.

### Final: the chosen ending
After the first victory on Morrow's Shadow III the player picks one ending (GG-ARC, Endings), each with its own crawl:
**keep it asleep** (the Lullaby without the skim), **cut the lines** (close every tap; the Manalith drifts out and
everyone's mana dims), or **let it come** (stop the Lullaby; what follows stays a mystery). Every ending costs everyone,
and the Commander is the last of Halcyon's people to decide for the world.

## 4. The crawl plan

| Crawl | Count | Key | Where it falls |
|---|---|---|---|
| Opening | 5 pages | `crawl:opening/<n>` (1 to 5) | after the party pick on a new campaign; where it sits in the front door is GG-T1258's |
| Chapter | 8 | `crawl:chapter/<id>` | after the first victory named in §2, on the way back to Camp |
| Region entry | 15 | `crawl:region/<themeId>` (engine GG-T1478) | once, when a region first unlocks: on the way back to Camp after the first victory on the previous region's last stage (the Tithe Moors' with the prologue) |
| Ending | 1 | `crawl:ending` | after the first victory on The Choir's Cathedral III, then the credits |
| Final choice | 3 | one per ending | after the first victory on Morrow's Shadow III and the player's pick |

A region-entry crawl is short: where the crew are, what the sky shows, why it is dangerous (`07-regions.md`). The
regions and the stop each one's crawl shares:

| Region | Its crawl shares the stop with |
|---|---|
| The Tithe Moors | the prologue |
| The Lantern Road | `ch01` |
| The Shardfall | (alone, after The Lantern Road III) |
| The Arkgrave | `ch02` |
| The Anchor Wastes | `ch03` |
| The Still | (alone, after The Anchor Wastes III) |
| Halcyon | `ch04` |
| The Choir's Cathedral | (alone, after Halcyon III) |
| The Bloom | `ch05` (after the ending) |
| The Free Holds | `ch06` |
| The Underspire | (alone, after The Free Holds II) |
| The Drowned Arks | (alone, after The Underspire II) |
| Deep Shardfall | `ch07` |
| Under the Anchors | (alone, after Deep Shardfall II) |
| Morrow's Shadow | `ch08` |

**When two crawls fall at one stop, they chain into one scroll**: the chapter (or prologue) first under its own title,
then the region's crawl under the region's name, in one `StoryCrawlScreen` with one Skip. Two scrolls never play back to
back (BIBLE §27). The ending is the exception: it keeps its stop to itself and goes straight to the credits, so `ch05`
and the Bloom's crawl wait for the next arrival at Camp. A save that skipped ahead sees only the latest chapter and the
latest region's crawl, chained.

## 5. What the art says (kept from GG-T386)

The art is read in one line: **white is hollowed, colour is alive.** The Choir's soldiers are white porcelain, white
power armour, white robes and ponchos; its halls are white domes of glowing cyan tanks; the people it has used up are
white too. Everyone who fights it wears colour: neon streetwear, hold steel and green cloaks, indigo and red on the
lantern road, the black and blue of the fen witches. The Ember wears red and black. Where the white has fallen, red
blossom grows over its pillars. The Manalith beside the Moon grows region by region until it fills the sky.

| # | Region (theme id) | Stages | Art | Reading |
|---|---|---|---|---|
| 1 | The Tithe Moors (`greenvalley`) | 1-3 | grey storm moors, moss, standing stones | home: the holds and Saltmoss, where the tithe is taken |
| 2 | The Lantern Road (`lanternroad`) | 4-6 | stone stairs up night gorges, torii, paper lanterns | the pilgrim road out of the moors |
| 3 | The Shardfall (`desert`) | 7-9 | red earth, rivers of red, giant curved horns rising from the ground | where the shell came down; the Ember's land |
| 4 | The Arkgrave (`swamp`) | 10-12 | wrecked ship hulls and masts in a gold-hazed marsh | Halcyon's Arks, grounded |
| 5 | The Anchor Wastes (`cave`) | 13-15 | colossal blue stone arches over pink-lit snow | the Anchors, Halcyon's taps |
| 6 | The Still (`still`) | 16-18 | white dome, cyan tanks, the sky in the oculus, catwalks | the vault the Commander came back from; where the tithe is stored |
| 7 | Halcyon (`cityruins`) | 19-21 | ruined tech city, pink-red sky, towers and cables | Halcyon's dead city under the spire |
| 8 | The Choir's Cathedral (`cathedral`) | 22-24 | cyan organ of pipes, rose windows | the top of the spire; the organ is the reservoir |
| 9 | The Bloom (`bloom`) | 25-27 | red blossom and moss over broken white pillars | where the Manalith's substance took root |
| 10 | The Free Holds (`greenvalley2`) | 28-29 | walled hill-forts over gold grass | the holds, walled up and trading |
| 11 | The Underspire (`cityruins2`) | 30-31 | neon night markets in the rain, the white spire behind cables | the Street's home under the spire |
| 12 | The Drowned Arks (`swamp2`) | 32-33 | Arkgrave art | the Arkgrave flooding |
| 13 | Deep Shardfall (`desert2`) | 34-35 | Shardfall art | the shell breaking, shards still falling |
| 14 | Under the Anchors (`cave2`) | 36-37 | Anchor Wastes art | beneath the arches, the chains |
| 15 | Morrow's Shadow (`morrowsshadow`) | 38-40 | the Manalith filling the sky over cracked plains, floating rock | where the Anchors hold last |

Who is who, as drawn, is in canon (`05-factions.md`) and BIBLE §14.11 to §14.19; the region dangers are in
`07-regions.md`.

## 6. Open points

- **Camp join beats still speak of sleepers.** Several join scenes in BIBLE §25.0.1.1 ("the sleepers who broke the
  levy", the Warden finding a thaw-sigil) predate the Commander; their rewrite belongs with the region beat tasks
  (GG-T1503 main regions, GG-T1507 postgame).
- **The Choir runaway and the Street engineer** are carriers by faction, not new characters: the writer picks the
  recruited hero of that faction (or a Camp keeper) who speaks the line.
