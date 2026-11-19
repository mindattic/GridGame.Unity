# Release scope (GG-T392, 2026-11-16)

Owner, 2026-11-16: *"we are aiming for a polished, releasable game once the story has been established; enough
development of more and more systems, need to refine and balance everything that exists, cut or at least disable that
which doesn't."*

**The core loop:** make a profile and start with basic heroes → fight 5-wave stages (pincers + the timeline) → earn gold
to repair and buy weapons, learn skills, buy items → a story scroll every 5 stages → wanderers joined by Trust and a game
of wits → Diamond and gold summons → a simple stage select to replay stages and hunt XP and heroes → Camp.
**Paywall hooks that stay:** summon, Diamond lucky craft, Rested refill (party energy), mercenary contracts.

## The switch

`Assets/Gaiden/Meta/Features.cs` holds one `static bool` per hidden system. A switch that is off hides only the App
entry points (Camp row, side panel, Settings row, Adventure strip, results row). The rules engine, content, save data,
the bots (`CampRoutine`, Test Mode, soaks) and the automation `goto` routes don't change. A save that already used a
hidden system keeps everything it bought. To bring a system back, set its switch to `true`. To turn every switch on for
one run, launch with `-allfeatures`. The harness's `fullloop`, `crucible` and themed `sweep` runs do that, so they still
cover the hidden systems.

## Verdicts

| System / screen | Verdict | Why | How to re-enable |
|---|---|---|---|
| Profiles, New Game, character select, save slots, Continue/Load | KEEP | Loop step 1 | — |
| Adventure: region → stage select, expeditions, hunts on cleared stages | KEEP | The loop's stage select and XP/hero hunt; an expedition is just a region played in order | — |
| Battle, level-up fanfare, results, retry / next battle | KEEP | The game | — |
| Story crawls (one every 5 stages), story beats, credits | KEEP | Loop step; text is Prose-sourced | — |
| Wanderers (Trust + game of wits), guests, talk-recruitables | KEEP | Loop step | — |
| Items | KEEP | Basic roster management | — |
| Heroes: Status, Equip, Abilities, Party as the tabs of one Hero screen (GG-T435) | KEEP (merged) | Basic roster management; one Camp row instead of four | — |
| Abilities tab (Learn, kit trainer, Locked tab, Passives, Tomes and Chests) | KEEP | "Learn skills" | — |
| Talents (skill trees) with its per-hero Respec button, now the Abilities tab's Talents sub-tab (GG-T435) | KEEP (merged) | Spends the skill points level-ups give; removing it would strand them | — |
| Camp row **Inn: Respec** | SIMPLIFY (hidden) | The Talents sub-tab's Respec button already does the same job with the same price and the same Inn rule | `Features.InnRespecRow = true` |
| Merchant (Buy, Sell, Bargain shelf) | KEEP | "Buy items / weapons"; the bargain shelf is the early cheap-weapon gamble | — |
| Blacksmith (Repair, Forge, Upgrade, Salvage, masterwork, recipe discovery) | KEEP | "Repair and buy weapons" | — |
| Lucky craft (Diamond) | KEEP | Paywall hook | — |
| Alchemist | KEEP | Cheap potions from drops; onboarding step 3 | — |
| Enchantress (armor enchants, weapon enchants) | DISABLE (owner 2026-11-19, GG-T436) | "Enough things going on without slotting gems and enchantments on day one." Her Camp row is hidden; the onboarding chain skips the enchant step (Training, step 7, follows the first armor). The 600 recipes stay. Plan: docs/plans/enchanting-and-gems.md | `Features.Enchanter = true` |
| Armor sets | KEEP | Forged at the Blacksmith | — |
| Inn rest | KEEP | Healing between expeditions | — |
| Rested pool (party energy) and its refill | KEEP | Paywall hook | — |
| Summon Circle (gold summon + Diamond draw) | KEEP | Loop step + paywall hook | — |
| Mercenaries | KEEP | Paywall hook. It still opens at the bounty milestone (`Mercenaries.Open` reads `CampUnlocks.BountiesOpen`, which is unchanged) | — |
| Talk (Camp conversations) | KEEP | Delivers story recruits and the side-quest offers | — |
| Side quests (Quests journal) | KEEP, shipped | Finished and optional: 33 quests in five chains and singles, from the first stage to the postgame (fetch, kill a class or family, recruit, reach a level, learn a skill; GG-T434). Offers and journal text are lorem ipsum until the new story lands (GG-T387) | — |
| Bestiary | KEEP | Cheap, finished, and the same knowledge tier drives the battle inspect card | — |
| Camp tips / onboarding chain (GG-T173–T180) | KEEP | New-player guidance. The bounty tip can no longer fire (its screen is hidden) | — |
| Difficulty dial 1–10 | KEEP | One Settings row; 10 is the designed game | — |
| Settings: audio, battle speed, drag timer, colorblind, cut-ins, confirm, reduce motion | KEEP | Player options and accessibility | — |
| Settings: **Menu theme, Font, Text wrapping** | DISABLE | Developer look presets (GG-T185, GG-T305). Off, every profile draws with the default theme (Chrono), font (Rounded) and word wrap | `Features.LookPresets = true` |
| New Game+ | KEEP | Finished endgame; one row that shows only after the credits | — |
| Taming / domestication (Monster Tamer, Dog Whistle) | DISABLE (owner 2026-11-19, GG-T433) | A fourth way to grow the roster with half-heroes. The Summon Circle doesn't list the Monster Tamer and the Merchant doesn't stock the Dog Whistle (`Features.Offers`), the only ways in | `Features.Taming = true` |
| **The Crucible** | KEEP (owner 2026-11-19, GG-T430) | The game's "toilet play" horde mode: pick one team, endless generated waves that ramp in level, size and elites (`BattleConfig.Crucible*`), gold banked every wave and kept when the team falls, best wave and best haul recorded. Not a replay of cleared stages, so it no longer duplicates the hunt. Ready for the switch | `Features.Crucible = true` |
| **Bounty board** | DISABLE | Its kill-N contracts duplicate the side quests' Defeat objectives, and it adds a Camp row, an Adventure strip and a results row. Kills still count in the rules | `Features.Bounties = true` |
| **Caravan upgrades + crew barks** | DISABLE → ready to KEEP (GG-T432) | A gold sink outside the loop, plus a flavor panel on Camp. Upgrades already bought keep working (Mana Casks, Armory discount, Scouts line, …). **2026-11-19, owner:** the caravan comes back as the loop's gold sink — the Forge and Alchemist **facilities** (Copper → Iron 400 → Steel 1,500 → Mythril 3,000 G; Clay → Glass 300 → Silver 1,200 G) gate material tiers (result rarity). The facility gate is rules, live whatever the switch says, and each facility is also built at its own station (the Blacksmith and Alchemist are KEEP), so the switch only adds the Caravan row | `Features.Caravan = true` |
| Gambits (GG-T299, on hold) | not built | Nothing reaches the player | — |
| Monster meat (GG-T304) | not built | Nothing reaches the player | — |
| Endless mode (`-demo -endless`), Test Mode (`-testmode`), automation server, `-start` screens, test stages | dev only | Command-line flags or development builds only; test stages are in no campaign region | — |

## Balance watch-list (from reading content; nothing measured)

None of these were run (balance tools are test-gated). Each is a number to look at in the next owner `/test` balance pass.

- **XP curve vs the one-level-a-stage campaign.** `XpToNext(L) = 50 + 10·L²` is quadratic, but kill XP is roughly
  linear (`XpReward + 2·Lv + stats/10`, ×1.5 at difficulty 10). Early on a party out-levels the curve: Lv 1→2 needs 60 XP,
  and Tithe Moors I's ~20 kills pay several hundred. Late on it falls behind: Lv 39→40 needs 16,050 XP against
  roughly 1.5–2k from a two-foe-a-wave stage at Lv 40, so it takes ~8 replays per level. The crossover looks like it sits
  around Lv 12–15. Hunts are meant to fill the gap, but check that the grind they ask for late is not punishing.
- ~~**Spells near the 12-orb bank.**~~ **Fixed 2026-11-17 (GG-T276).** Frost was 10 dmg for 7 orbs (1.4/orb, the worst
  in the game) and `fieldeng_overdrive` was 12 dmg for 1 orb (12/orb, the best single-target rate, twice Fireball's 6).
  Frost's `BaseDamage` 10 → 16 (2.3/orb, in line with Meteor's 2.2/orb); `fieldeng_overdrive`'s `Cost` 1 → 2 (6/orb,
  matching Fireball). See BIBLE §25.6.
- ~~**Training prices.**~~ **Partly fixed 2026-11-17 (GG-T215).** A skill was a flat 300 G whatever it did; skills now
  price at `BattleConfig.SkillTrainBasePrice` (150) + `SkillTrainPricePerCooldown` (50) × `CooldownTurns`, so a 1-turn
  jab is 200 G and a 6-turn revive/ultimate is 450 G (weighted average over the 146 skills is unchanged, ~300 G, so
  this is a spread not a net economy shift). Spells are unchanged at 120 + 80 × cost (Meteor still 920 G, but Frost's
  cost-efficiency fix above is the real lever there — its price stays 680 G). The kit trainer still charges 100 G.
- **Rewards are ×1.5 at the default difficulty 10.** "Intended" also pays the most, so every gold and XP number in the
  game is effectively 1.5× its content value. Make sure prices were tuned with that in mind.
- **Drop rates.** A Wooden Chest is 1.5% on almost every drop table, about one every 4–5 stages. Signature jewelry
  (Titan, Runelord, Wyrmheart, Dragon earring) is 0.6% and Dragon Scale 0.5–1%. If any of these is a recipe input, that
  recipe is effectively unreachable. 98 drop entries sit under 2%.
- ~~**Enchant recipes.**~~ **Fixed 2026-11-17 (GG-T215).** All 600 weapon-enchant recipes (100 base weapons × 6
  elements) were tier 3 and a flat 150 G regardless of the base weapon, gating every one behind a maxed Alchemist
  station and giving no sense of progression. Re-tiered/re-priced by the base weapon's own `Rarity`: Common/Uncommon
  → Tier 1 (60/90 G), Rare → Tier 2 (130 G), Epic/Legendary → Tier 3 (180/250 G). A starter sword now enchants at
  Apprentice; only the best 25 weapons still need Master. Still a long list for one tab — not addressed here.
- **Armor enchants** are 40 G (tier 1) and 150 G (tier 3), cheap next to a mid weapon (~200–335 G).
- **Mercenaries** charge 45–70 G per battle up front for 3/5/10 battles, about a third of an early stage's pay per battle.
  The 10-battle term at 10% cheaper per step is fine, but check it against stage pay at Lv 10–20.
- **Rested:** 10 max, 1 per launch, so a refill covers 10 stages. Hunts also cost 1, so a hunting player refills often.
  That is the hook working, but it also taxes the grind the XP curve above demands.
- ~~**Paywall placeholders.**~~ **Tuned 2026-11-19 (owner /goal, GG-T427).** Summon, lucky craft and Rested refill were all
  still 1 g / 1 s. First-pass real prices now: deliberate gold Summon 800 g, one Diamond (LuckyDiamond) 250 g, Rested's
  gold refill 100 g + 15 g per stage cleared (a flat 60 g, tried first, was under 3% of what its 10 launches earn). The Diamond draw stays 1 Diamond (unchanged, deliberately cheaper per attempt than the 800 g
  guaranteed pick) — still a measurement-pending number, not re-derived here. Rested also gained a second, Diamond-priced
  refill (1 Diamond, instant, no job) alongside the gold one, so Diamonds can pay for either a hero pull or skipping the
  wait, per the owner's request. Checked against measured stage pay (2026-11-17 polish records, winning runs): ≈210–270 g a stage at Lv 1–6,
  400–600 g from Lv 9, ≈9,900 g for one pass of the main campaign. Not yet felt in real play: the owner's call.
- **Facility prices (GG-T432, first pass, not measured).** Iron Forge 400 G, Steel 1,500 G, Mythril 3,000 G; Glass
  Alembic 300 G, Silver 1,200 G: 6,400 G for the whole ladder against ≈9,900 G of main-campaign pay, competing with
  summons (800 G) and training (~300 G a skill). Watch whether Rare gear (Iron Forge) arrives too late for the bots;
  `CampRoutine` builds a level once it costs half its gold.
- **Consumables** run from 20 G (Antidote) to 500 G (X-Potion); the Tent is 300 G, the Revival Charm 450 G. The Healing
  Potion (25 G, 50 HP) vs Hi-Potion (75 G, 150 HP) step is linear, so there is no reason to buy small ones late.
- **Wanderer join rule:** low trust (0) can't join even with a faction-mate and three right answers (0 + 1 + 3 = 4 is the
  bare threshold). In practice that means half trust or better, which is fine, but say so in the talk's outlook line.

## Measured: the postgame level wall (nightly 2026-11-17)

The first nightly bot soak (`BalanceReport.Campaign_pacing_table`, greedy and casual bots, campaign played straight
through) measured what the watch-list above only guessed. The main campaign is at or near 100% bot wins on every
stage, but **no bot run finished the campaign (0/4)**: wins fall off as soon as the postgame's one-level-a-stage ramp
outruns the party's XP.

| Stages | Enemy level | Bot win % |
|---|---|---|
| Main campaign (Tithe Moors to Cathedral) | 1-24 | 80-100 |
| The Bloom I-III | 25-27 | 80-100 |
| The Free Holds I / II | 28 / 29 | 50-57 / 20-44 |
| The Underspire I / II | 30 / 31 | 66 / 40-57 |
| The Drowned Arks I / II | 32 / 33 | 37-50 / **0-12** |
| Deep Shardfall I | 34 | **0** |

Cause: `XpToNext(L) = 50 + 10*L^2` is quadratic while kill XP is linear, and the postgame adds a level per stage. Both
are pinned deliberately (ParityTests keep the GridGame2026 curve; GG-T357 set one level a stage, Morrow's Shadow III
at 40), so the fix is a design call:

1. **Late kill XP scales up** (for example the level term of kill XP grows with the square of the enemy level past 24):
   keeps both pinned curves, makes the postgame pay for itself.
2. **Postgame levels step per region, not per stage** (two stages share a level; Morrow's Shadow III still ends high):
   breaks GG-T357's "one level a stage".
3. **The XP curve bends past mid levels**: breaks parity with GridGame2026.
4. **Keep it**: the postgame is a grind you hunt your way through (bots don't hunt; players will), and only the 0%
   stages get a closer look in case a region rule is unwinnable rather than hard.

The bots don't replay stages to hunt, so real players will sit higher than these numbers; the 0% stages (Deep Shardfall
I, Drowned Arks II) are the ones to look at first either way. Floodwater had no counter until GG-T410, which may account
for part of the Drowned Arks number.

## Owner answers (2026-11-19)

1. **Crucible, bounty board, caravan upgrades:** keep, and build them out. The Crucible is the "toilet play" horde
   mode (one team, endlessly harder generated waves, gold until the party falls; GG-T430). The bounty board hunts
   especially dangerous named variants, with a clue ("Seen wandering the Ashlands II"), a skull marker in battle and
   the reward claimed at the board (GG-T431). The caravan upgrades become the necessary gold sink: facility levels that
   unlock better material tiers (copper, iron, steel) at the Blacksmith, Alchemist and other stations (GG-T432).
2. **Taming:** hidden (GG-T433).
3. **Side quests:** ship them, plus more quests and quest chains (fetch materials, kill monsters, recruit people, reach a
   level, unlock a skill; GG-T434). The text stays lorem until the story is written "by examining the shape of things".
4. **Weapon enchants:** the Enchanter is left out of release; the 600 recipes stay; enchants and gems are a plan
   (docs/plans/enchanting-and-gems.md; GG-T436).
5–6. **Hero screens:** merge Status / Equip / Abilities / Party into one tabbed hero screen, Talents folded into
   Abilities (GG-T435).
7. **Gambits (GG-T299) and monster meat (GG-T304):** closed as out of scope.

## The original questions (answered above)

These are recommendations only. Nothing was deleted.

1. **Crucible, bounty board, caravan upgrades.** They are hidden now. Should we delete them (code, content, save fields,
   tests, harness runs) or keep them switchable for a later update?
2. **Taming / domestication.** It overlaps with wanderers and summons as a fourth way to grow the roster, and
   domesticated monsters are half-heroes (no gear, no skills, Lv cap 30). Keep it, hide it (pull the Monster Tamer from
   the summon pool and the Dog Whistle from the shop), or cut it?
3. **Side quests vs the new story.** Every quest giver line is placeholder until the Prose story lands. Kept and shipped
   (GG-T434): the quests are written to be rewritten; their descriptive names and objectives give the story its shape.
4. **Weapon enchants.** Cut the 600-recipe weapon tab down to a handful, or drop it and keep armor enchants only?
5. **Talents + Abilities + Tomes.** That is three ways to learn things. Fold Talents into the Abilities screen, or keep
   it as its own row?
6. **Status / Equip / Abilities / Party.** Four Camp rows for one hero. Merge them into a single hero screen with tabs?
7. **Gambits (GG-T299) and monster meat (GG-T304).** Close both tickets as out of scope for release?
