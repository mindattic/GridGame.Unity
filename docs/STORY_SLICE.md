# The Story Slice: Prologue Beat Sheet and Slice Arc

GG-T2065, under GG-T2010 (Prologue story and script) and epic GG-T1941. Status: **planner defaults, owner-approved 2026-11-28** ("approve all do all"); the owner may still change any of them, and the rows marked **(owner default)** are the ones most likely to move. The click-through is [`docs/design/prologue/`](design/prologue/index.html) (its `data.js` is this file's twin; change both together). The BIBLE's [§27.0](BIBLE.md) and [§28.10](BIBLE.md#2810-new-game-onboarding-order) point here. Built on the Commander canon (`Data/Prose/canon`, GG-T1305) and the overworld design (BIBLE §28).

Design only. It is built by GG-T2066 (dialogue seeds), GG-T2069 (unyielding plates), GG-T2071 (element-locked foes), GG-T2072 (Sunder), GG-T2073 (the Tithe Collector), GG-T2074 (the Ward-Engine and the Tome of Bolt), GG-T2075 (the prologue battles) and GG-T2077 (the onboarding steps).

## Premise

The Commander, the last sleeper of Halcyon, wakes too late in a Halcyon vault under the Tithe Moors. Moorfolk scavengers prying at its door (the Knight, the Barbarian and the Cleric) are fighting slimes drawn to the leaking mana when the vault's lights, and their own dead Mana Orbs, wake around the Commander.

They take the Commander to the hamlet of Tithe, which the Captain's Tithe Collector bleeds dry and where he holds a Red Mage prisoner at the toll bridge. The first clash fails: his plate faces the party, rocks guard his flanks and every blow clangs. They fall back, camp, and the Barbarian learns Sunder. In the rematch Sunder shatters the plate. The Red Mage joins, the Collector's chest holds a Tome of Bolt, and his white tithe wagon becomes the caravan.

On the road only the Commander sees a waystone glint. Its Halcyon Ward-Engine can be hurt only by Lightning. The Red Mage casts Bolt, the stone wakes, its circle rolls out over the moor and the Manalith eases a touch. That ends the prologue.

## The bar

- **Believable.** Every lesson is something these people would do anyway: buy a potion before a fight, run from one they can't win, train, try again.
- **Scripted.** The order is fixed and seeded. Random encounters stay off until the stone wakes, so nothing can derail it.
- **Not a chore.** At most one blocking card a minute ([REVIEW.md §4.2](design/ux-review/REVIEW.md), principle 2), every beat skippable, no grinding, and the one failure is a scripted retreat that costs nothing.
- **It teaches** travel (route, day dial, pulse, camp), what to look for (the plate, the element lock, the glint) and how to progress (skill points, tomes, waystones).

## Length and the card budget

Played straight through the prologue runs **33:45**, inside the 25 to 35 minute target, with **24 blocking cards and none closer than 60 seconds** to the one before. A card is anything that stops play until tapped: a coach box, a conversation, a crawl, the name field. Pointers, cut-ins and the screen itself are not cards. The page recomputes both numbers from `data.js` and marks any card that breaks the gap in red. Each beat's Skip collapses it to a few seconds.

## The beats

Times are from the first tap. Node ids are proposals for `world.json`'s moors region (`greenvalley`, still empty; GG-T1652 authors it).

| Time | Beat | Map node | Teaches | Battle | Reward | Skip |
|---|---|---|---|---|---|---|
| 0:00 | **Cold Open.** A heartbeat, frost cracking on glass, "Cold. Then light. Then voices." The name field. | `greenvalley.vault` (Stage: The Sleeper's Vault) | The player is the Commander | None | The name, said through `{commander}` | Tap skips; the name keeps its suggestion |
| 0:45 | **Drill 1: The Trap.** Scavengers at the half-open door, slimes after the mana; the Commander's tank cracks and the dead orbs light. Untimed. | `greenvalley.vault` | Pincers (do, then name), Chains, the timeline | `tutorial-01`, as built (GG-T1960); vault backdrop, two barks | The first VICTORY | Drill counts cleared; lessons fall back to just in time (GG-T1398) |
| 2:45 | **Opening crawl and the Founding.** Three pages (`crawl:opening/1-3`); the Knight saw the lights come on when the Commander moved: "Whatever you are, our orbs work near you. Come with us." | `greenvalley.vault` | Who you are; why they follow you | None | The party's reason to follow | Skip ends the crawl; the Founding is one more tap, and opens by asking your look and pronouns (GG-T2127) |
| 4:00 | **Drill 2: The Clock.** Slimes and a bat on the vault stair. | `greenvalley.vault` (the stair) | The hold timer; Guard | `tutorial-02`, as built | The moor at the top | As drill 1 |
| 6:00 | **Drill 3: Abilities.** A glob slime at the vault mouth; the Cleric is hurt. | `greenvalley.vault` (the mouth) | Abilities, Spells, Heal, Cast; Info | `tutorial-03`, as built | Out under the sky | As drill 1 |
| 7:45 | **The Road to Tithe.** The map opens; halfway the pulse fills and wolves come out of the heather. | edge `greenvalley.vault` > `greenvalley.tithe` | Navigate and the route line, the day dial, the danger pulse, an ambush | `battles/greenvalley/ambush-prologue`: wolves, one wave, fixed seed; the pulse is scripted to fill on this edge | A little gold, a wolf pelt | Caravan placed at Tithe, no ambush, gold granted |
| 10:45 | **The Hamlet of Tithe.** The Collector takes the colour every month now and holds a Red Mage at the bridge. The Merchant has one Healing Potion left. | `greenvalley.tithe` (Settlement) | Quests; buying; equipping a consumable | None | Quest 1, The Tithe Collector; a Healing Potion | Potion bought and equipped on the Knight |
| 13:45 | **The Toll Bridge.** Wave 1, his men: a heavy swing leaves the Knight low. Wave 2, the Collector: plate to the front, rocks on his flanks, every blow clangs for 0. After his second round the Knight calls the retreat. | `greenvalley.bridge` (Bridge) | Using a potion; reading a plated foe; falling back is allowed | `prologue-bridge-1`: `soldier00`, goblin thugs, then the Tithe Collector (captain variant, unyielding front plate, GG-T2069). Ends by script as **Withdraw, not Defeat**: no hero lost, wounds carry | Knowing what the problem is | Retreat line plays; the potion counts as spent |
| 17:45 | **Camp at the Ford.** Dusk stops the caravan; Set up camp is the only button. A meal, eight hours, Well Rested. The Barbarian: "I don't need to go round it. I need to go through it." The skill board opens on Sunder. | `greenvalley.camp-ford` (Camp: Reedford) | Dusk and Set up camp; Well Rested; skill points; the camp circle and hunt spots | Optional `battles/greenvalley/hunt-deer` | Sunder (story-granted skill point), Well Rested, Food if hunted | Sunder learned, Well Rested, morning |
| 22:15 | **The Rematch.** Sunder glows on the Barbarian's chip (a pointer). The plate shatters with a cut-in; a pincer from the front finishes him. The Red Mage: "You took his plate off with an axe. I like you already." | `greenvalley.bridge` | Do, then name (PLATE SHATTERED); what you learn at camp beats the fight you lost | `prologue-bridge-2`: the Collector and two guards; Sunder carries ShattersPlate (GG-T2072, GG-T2073) | The Red Mage joins; the chest: Tome of Bolt, a Storm Flask, gold; the white tithe wagon becomes the caravan | Won with every reward |
| 25:45 | **The Glint.** The Red Mage reads the tome. Only the Commander sees a glint on a bare hilltop: "There's nothing there." "There is for them." | edge `greenvalley.bridge` > `greenvalley.waystone-1` | A tome teaches a spell; only the Commander sees waystones; fog lifts with travel | None | Bolt on the Red Mage | Caravan below the stone, Bolt learned |
| 28:45 | **The Sleeping Stone.** A Halcyon Ward-Engine unfolds. Swords, Sunder and fire read 0; its Info shows a lightning mark; Bolt hits for real. | `greenvalley.waystone-1` (Waystone) | Info shows what hurts a foe; element locks; the Storm Flask fallback | `prologue-ward`: the Ward-Engine (tinted mecha, every resistance 0 except Lightning x1.5; GG-T2071, GG-T2074) | The stone, ready to wake | Fight won |
| 32:15 | **The Stone Wakes.** Its circle rolls out, fog lifts off the roads it touches, the Manalith eases a hair. Region card: The Tithe Moors. | `greenvalley.waystone-1` | Why you seek waystones | None | Waystone tally 1, its circle, Mana Dust gathering; the Tithe Moors opens | Region card |

### Blocking cards

0:06 your name · 1:45 the timeline · 2:45 the opening crawl · 3:45 the Founding · 4:51 the clock runs only while you hold · 5:54 Guard · 7:00 Abilities, Spells, Heal, Cast · 8:03 Navigate · 9:09 the pulse and ambushes · 11:03 quest: The Tithe Collector · 12:03 buy the potion · 13:03 equip it · 14:57 use the potion · 16:21 the unyielding plate · 17:21 "Fall back!" · 18:27 eat and sleep · 19:45 learn Sunder · 20:57 hunt spots (optional) · 25:09 the Red Mage joins · 26:15 the Tome of Bolt · 27:15 the glint · 29:33 only Lightning, check its Info · 31:45 waking the stone · 32:45 the Tithe Moors.

### Skip all

"New to GridGame? No", or Skip prologue at any beat, lands in the state finishing it gives: the Knight, the Barbarian, the Cleric and the Red Mage; Sunder and Bolt learned; a Healing Potion equipped on the Knight and the Storm Flask in the bag; the first waystone woken; the caravan camped in its circle; onboarding Done.

### Onboarding steps (owner default)

`Meta/Onboarding.cs` today has `Crawl`, `Drill`, `HubIntro`, `Done`. The prologue needs `Wake`, `Drill(1)`, `Crawl`, `Drill(2)`, `Drill(3)`, `Travel`, `Tithe`, `Bridge`, `Camp`, `Rematch`, `Glint`, `Waystone`, `Done`, each resumed through `StoryBeats.Resume`; `HubIntro` folds into `Camp` (GG-T2077). The first drill moves ahead of the crawl, as in REVIEW.md §4.3's first five minutes.

## The slice arc

1. **Prologue** (25 to 35 min): the beats above, `greenvalley.vault` to `greenvalley.waystone-1`.
2. **The Tithe Moors I** (`greenvalley-01`, 10 to 12 min): the first free stretch. The Innkeeper at Tithe gives `q_moor_slimes` (Slime in the Cellar). Random encounters switch on outside the stone's circle.
3. **The Tithe Moors II** (`greenvalley-02`, 15 to 20 min): `q_moor_harpies` and `q_moor_werewolf` (The Beast of the Moors), and the one bounty, Grimjaw the Mauler (`bounty_grimjaw`). Slimetrail teaches that a foe can change the floor and fire answers it.
4. **The Tithe Moors III** (`greenvalley-03`, 8 to 10 min): the boss is the Captain, who sold the holds' tithe and sent the Collector, with his TwinTurn rule break; his boss introduction plays before the battle.
5. **Slice ending and credits** (3 min): on the first victory over `greenvalley-03`, a short slice ending crawl, rolling credits, then Title. The Lantern Road roadsign stays dormant and regions past the moors are closed in the slice.

The whole slice runs about 60 to 80 minutes. The 25 to 35 minute target is the prologue's.

## Against the canon

| Topic | Canon says | The slice does |
|---|---|---|
| Where the Commander wakes **(owner default)** | `06-arc`: the Commander wakes in the Still, at the foot of the spire, when the tithe runs through its tanks. | A Halcyon vault under the Tithe Moors: one of the Still's outlying tank-houses on Halcyon's old road, woken by the tithe flowing through it on its way to the Still. The Still stays where canon puts it. |
| Saltmoss and Tithe Day **(owner default)** | `06-arc`, Prologue "Tithe Day": the Commander walks into Saltmoss on tithe day and present-day people steal the Choir's white tithe caravan. | The hamlet of Tithe takes Saltmoss's place. The caravan is still a stolen white tithe wagon: the Collector's, taken after the rematch. |
| Who the Collector serves **(owner default)** | `05-factions`: the Captain sold the holds' tithe to the Choir and hunts the runaways who stole its caravan. | The Tithe Collector is the Captain's man, a captain variant, and the Captain is the slice boss, so the slice has one villain from first beat to last. |
| The heroes | `05-factions`: every hero is a present-day person; the Free Holds are moor folk who pay the tithe and hate it. | The Knight, the Barbarian and the Cleric are Moorfolk scavengers of the Free Holds; the Red Mage is a prisoner who refused to pay. |
| Placeholder names **(owner default)** | GG-T1941, decision 10. | Tithe Collector, Ward-Engine, Sunder and Tithe (the hamlet) are placeholders until the owner renames them. |

The Prose canon itself (`06-arc`'s Prologue row) is amended in a follow-up once the owner confirms these rows; this file does not edit Prose.
