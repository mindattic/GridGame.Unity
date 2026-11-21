# Store listing copy (GG-T489, 2026-11-20)

Draft copy for the Google Play and App Store listings. It describes only systems that ship: the **KEEP** rows of
[RELEASE_SCOPE.md](RELEASE_SCOPE.md) "Verdicts". Hidden or disabled systems are never mentioned: the Enchantress, taming,
the menu-theme/font presets and the Inn respec row. Story lines stay inside the premise of
[STORY_OUTLINE.md](STORY_OUTLINE.md) §1 and are marked *(pending the story pass)*, because every player-facing story
word is still being written in Prose (BIBLE §27.2). Region and faction names are current game data, and the writer may
rename them. No other game is named or imitated anywhere in the copy.

Character counts were measured with Python `len()` on the exact strings below. Re-measure after any edit.

## 1. App name

The current product name is **`GridGame Gaiden`** (`ProjectSettings/ProjectSettings.asset`, `productName`, company
`MindAttic`, package `com.mindattic.gridgamegaiden`).

| Candidate | Length | Conflict check (web search, 2026-11-20) | Notes |
|---|---|---|---|
| **GridGame Gaiden** (current) | 15 | No app or game with this exact name found. Close names exist: a "Grid Game" roguelike deckbuilder on Steam (listed for 2026) and a "Grid Game Arcade" puzzle app on iOS | Risk of confusion on the "Grid Game" prefix. "Gaiden" (a side story) is a common word in game titles and reads as a sequel to something players can't find |
| **Pincer Moon** | 11 | No exact match found. The nearest is "Pincer", a 2010 tabletop dice game | Names the core verb and the moon that is over every region. Short and easy to search |
| **Tithe Runners** | 13 | No exact match found | Fits the premise (runaways from the tithe). *Pending the story pass*: "tithe" may be renamed in Prose |
| **Under Morrow** | 12 | No exact match. A "Morrow" survival RPG on Steam and an "Overmorrow" listing exist | It was the retired story's title. Morrow is still current data (Morrow's Shadow), but the name is *pending the story pass* |
| **Pincer Moon: Tactics** | 20 | As Pincer Moon | Spells out the genre where the store shows only the name |

Recommendation: **Pincer Moon** (or **Pincer Moon: Tactics**). The owner should run a trademark search (USPTO/EUIPO)
and an exact-name search in both store consoles before committing. A web search is not a clearance. Changing the name
means changing `productName`. The package id can stay as it is, since it is never shown to players.

## 2. Subtitle (App Store, max 30)

- **`Flank, pincer, outpace the foe`** (30) ← recommended
- `Tactics on a ticking timeline` (29)
- `Pincer tactics under the moon` (29)

## 3. Short description (Google Play, max 80)

- **`Drag heroes to trap foes in pincers, beat the timeline and recruit 69 heroes.`** (77) ← recommended
- `Position is your only weapon: drag, flank and pincer foes on a live timeline.` (77)

## 4. Promotional text (App Store, max 170, editable without review)

`Trap foes between two heroes, push their turns back on the timeline and break rule-bending bosses. 40 stages, 69
heroes, an endless horde mode, no ads.` (151, as one line)

## 5. Full description (max 4000)

> Paste as plain text. Lines in *(italics)* are notes for us and are not part of the copy.

```
POSITION IS YOUR ONLY WEAPON

Moving never deals damage. Drag a hero across the grid one tile at a time and slide anyone in your way aside. Then close the trap: when two of your heroes flank an unbroken line of enemies, everyone on that line takes the hit. Every drop asks one question: does this finish the line?

A TIMELINE THAT WAITS FOR YOU, UNTIL YOU MOVE

Every turn and every spell rides a single timeline. The clock stands still while you look. Pick up a hero and it runs in real time until the next enemy reaches the front. Pincers push your foes' turns back. Hits on a caster can break a charging spell before it lands. Speed decides who acts first, and you decide what to do with it.

ONE SHARED POOL OF MANA

The whole party casts from one visible pool of mana. Pincers, critical hits and interrupted enemy casts refill it. Spend it on fire, ice, lightning, poison, holy, dark and arcane magic, exploit weaknesses, and keep a healer's orbs in reserve when the next wave is a big one.

BOSSES THAT BREAK THE RULES

Each region of the main campaign ends in a boss that breaks one rule of battle. One takes a second turn when nobody stands beside it, one gets back up after it falls, and another shrugs off pincers from the side. Learn how each one is beaten before it beats you.

A WORLD UNDER TWO MOONS
(pending the story pass)

Two moons hang over every land: the old, familiar Moon, and beside it a misshapen second moon that grows larger the further the road goes. A white order owns the world's light. You lead a crew of runaways in a stolen caravan, out of the storm moors and up the lantern road, through the red Shardfall, past grounded ships and colossal stone arches, toward a white spire. A new chapter of the story opens every five stages.

GATHER A CREW OF 69 HEROES

Recruit fighters from six factions: neon street crews, hill-fort knights, lantern-road swordsmen, fen witches and healers, a deserter in red and black, and the wild things of the world itself. Win over wanderers with trust and a game of wits, summon heroes at the Summon Circle, or hire mercenaries on contract. Every hero has a class, talents to spend skill points in, and abilities to learn.

MAKE CAMP

Between battles, the caravan's camp is your base:
- Merchant: buy, sell, and gamble on the bargain shelf
- Blacksmith: repair, forge, upgrade and salvage gear, and chase masterwork rolls
- Alchemist: brew potions from what you loot
- Inn: rest the party between expeditions
- Caravan: build up the forge and alchemy wagons to unlock better materials
- Armour sets, weapon durability, a bestiary and a quest journal

MORE TO PLAY

- 40 hand-built stages across 15 regions, each a five-wave battle, with a full postgame after the credits
- Replay any cleared stage to hunt experience, loot and heroes
- The Crucible: one team, endless waves that keep getting harder, gold banked every wave
- Bounty board: hunt marked, tougher named foes from the clues on their posters
- 33 side quests, from the first stage to the postgame
- New Game+
- A difficulty dial from 1 to 10

PLAY YOUR WAY

Portrait mode, one hand, in short sessions. Adjustable battle speed, an optional drag timer, a colour-blind mode, reduced motion and skippable cut-ins. No account and no ads. Plays offline.

FAIR TO YOUR WALLET

Everything in the game can be earned with gold. Diamonds are an optional shortcut: a random hero draw, a lucky forge craft, or an instant refill of the party's Rested energy. Every Diamond price can also be paid in gold.
```

*(3,514 characters, including the "(pending the story pass)" marker line. Before submitting, re-check that the boss rule examples (TwinTurn, Undying, AxisWard) are
still in stage content, and remove the "A world under two moons" block or replace it with Prose-approved text once
the story pass lands.)*

## 6. Feature bullets (for screenshots, the website or press)

1. Pincer combat: flank foes between two heroes, and moving never deals damage.
2. A live speed timeline: push enemy turns back and interrupt spells.
3. One shared mana pool and eight elements of weakness and resistance.
4. Bosses that break the rules of battle.
5. 69 recruitable heroes from six factions: wanderers, summons, mercenaries.
6. A full camp: merchant, blacksmith, alchemist, inn, caravan upgrades.
7. 40 stages, a postgame, New Game+.
8. The Crucible: an endless horde mode.
9. Bounty hunts and 33 side quests.
10. Offline, portrait, one hand, no ads.

## 7. Keywords (App Store, 100 characters, comma-separated, no spaces)

`tactics,tactical,rpg,strategy,grid,pincer,timeline,turn,heroes,anime,fantasy,offline,party,squad` (96)

Leave out words already in the name or subtitle if the name changes (Apple indexes those already). Google Play has no
keyword field. The same terms should appear naturally in the full description, which they do.

**Category:** Games › Role Playing (primary), Strategy (secondary).

## 8. Content-rating and monetisation notes

These match the drafts in [legal/age-ratings.md](legal/age-ratings.md) and
[legal/play-data-safety.md](legal/play-data-safety.md), and those files win where they differ.

- **In-app purchases: yes.** Diamonds are consumables sold in four packs at a flat rate of **100 Diamonds for $1.00**:
  `diamonds_100` $1.00, `diamonds_500` $5.00, `diamonds_1000` $10.00, `diamonds_2500` $25.00 (`DiamondPack`, Unity IAP,
  Google Play Billing / StoreKit). There are no subscriptions and no bonus-tiered "best value" packs.
- **Gold exchange.** Every Diamond price can be paid in gold at 6 G per Diamond (`DiamondExchange`), so nothing is
  locked behind real money.
- **What Diamonds buy:** a random hero draw at the Summon Circle (100 Diamonds), a lucky craft that raises the
  masterwork chance at the forge (50 Diamonds), and an instant Rested refill (25 Diamonds).
- **Random items: yes.** The Diamond summon is a randomised reward that can be paid for with purchased currency. Answer
  the loot-box / "includes random items" questions **yes**. Expect the "In-Game Purchases (Includes Random Items)"
  descriptor. The odds have to be shown before purchase where a store or region requires it (Apple guideline 3.1.1).
  Confirm the Summon Circle shows them.
- **Energy:** the Rested pool (10 points; each stage launch spends 1, and 1 point comes back every 10 minutes) can be
  refilled with gold or Diamonds. If the description mentions energy, describe it plainly.
- **Ads: none.** There is no ad SDK and no advertising id.
- **Online and data:** no account, no chat, no user-generated content and no online play. The only network traffic is
  the store's billing.
- **Violence:** stylised fantasy combat with no blood or gore. Expected rating around PEGI 7 / ESRB E10+ / Apple 9+.
- **Release-state caveat:** RELEASE_SCOPE still says release builds show "Store not connected yet". GG-T468 has since
  wired Unity IAP for Android and iOS release builds. Confirm a release build sells a pack, and that the four product
  ids exist in both consoles, before publishing the IAP lines.
