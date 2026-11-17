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
| Items, Status, Equip, Party | KEEP | Basic roster management | — |
| Abilities (Learn, kit trainer, Locked tab, Passives, Tomes and Chests) | KEEP | "Learn skills" | — |
| Talents (skill trees) with its per-hero Respec button | KEEP | Spends the skill points level-ups give; removing it would strand them | — |
| Camp row **Inn: Respec** | SIMPLIFY (hidden) | The Talents screen's Respec button already does the same job with the same price and the same Inn rule | `Features.InnRespecRow = true` |
| Merchant (Buy, Sell, Bargain shelf) | KEEP | "Buy items / weapons"; the bargain shelf is the early cheap-weapon gamble | — |
| Blacksmith (Repair, Forge, Upgrade, Salvage, masterwork, recipe discovery) | KEEP | "Repair and buy weapons" | — |
| Lucky craft (Diamond) | KEEP | Paywall hook | — |
| Alchemist | KEEP | Cheap potions from drops; onboarding step 3 | — |
| Enchantress (armor enchants, weapon enchants), armor sets | KEEP | Onboarding step 6 opens Training (step 7), so hiding her would stall the tutorial chain. See the cut questions | — |
| Inn rest | KEEP | Healing between expeditions | — |
| Rested pool (party energy) and its refill | KEEP | Paywall hook | — |
| Summon Circle (gold summon + Diamond draw) | KEEP | Loop step + paywall hook | — |
| Mercenaries | KEEP | Paywall hook. It still opens at the bounty milestone (`Mercenaries.Open` reads `CampUnlocks.BountiesOpen`, which is unchanged) | — |
| Talk (Camp conversations) | KEEP | Delivers story recruits and the side-quest offers | — |
| Side quests (Quests journal) | KEEP, flagged | Finished and optional. Its offers are Talk scenes whose text is lorem ipsum until the new story lands (GG-T387) | — |
| Bestiary | KEEP | Cheap, finished, and the same knowledge tier drives the battle inspect card | — |
| Camp tips / onboarding chain (GG-T173–T180) | KEEP | New-player guidance. The bounty tip can no longer fire (its screen is hidden) | — |
| Difficulty dial 1–10 | KEEP | One Settings row; 10 is the designed game | — |
| Settings: audio, battle speed, drag timer, colorblind, cut-ins, confirm, reduce motion | KEEP | Player options and accessibility | — |
| Settings: **Menu theme, Font, Text wrapping** | DISABLE | Developer look presets (GG-T185, GG-T305). Off, every profile draws with the default theme (Chrono), font (Rounded) and word wrap | `Features.LookPresets = true` |
| New Game+ | KEEP | Finished endgame; one row that shows only after the credits | — |
| Taming / domestication (Monster Tamer, Dog Whistle) | KEEP, flagged | Finished and reachable only by choice (one summon, one shop item). See the cut questions | — |
| **The Crucible** | DISABLE | Duplicates the loop's hunt (replaying cleared stages for XP and loot). A second grind mode is another Camp row to parse | `Features.Crucible = true` |
| **Bounty board** | DISABLE | Its kill-N contracts duplicate the side quests' Defeat objectives, and it adds a Camp row, an Adventure strip and a results row. Kills still count in the rules | `Features.Bounties = true` |
| **Caravan upgrades + crew barks** | DISABLE | A gold sink outside the loop, plus a flavor panel on Camp. Upgrades already bought keep working (Mana Casks, Armory discount, Scouts line, …) | `Features.Caravan = true` |
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
- **Spells near the 12-orb bank.** Meteor costs 10 orbs for 22 damage (2.2 a orb, and it empties the bank). Frost costs 7
  for 10 (1.4 a orb, the worst in the game). Both are hero spells. Fireball pays 6 a orb, and `fieldeng_overdrive` pays
  12 a orb for 1 orb (the best single-target rate, twice Fireball's). Spellblade Overchannel pays 9 a orb.
- **Training prices.** A skill is a flat 300 G whatever it does. A spell is 120 + 80 × its cost, so Meteor is 920 G and
  Frost 680 G for the two worst damage-per-orb spells. The kit trainer charges 100 G.
- **Rewards are ×1.5 at the default difficulty 10.** "Intended" also pays the most, so every gold and XP number in the
  game is effectively 1.5× its content value. Make sure prices were tuned with that in mind.
- **Drop rates.** A Wooden Chest is 1.5% on almost every drop table, about one every 4–5 stages. Signature jewelry
  (Titan, Runelord, Wyrmheart, Dragon earring) is 0.6% and Dragon Scale 0.5–1%. If any of these is a recipe input, that
  recipe is effectively unreachable. 98 drop entries sit under 2%.
- **Enchant recipes.** 600 weapon-enchant recipes, every one at tier 3 and a flat 150 G. That is a very long list for one
  tab, and with no price spread it gives no sense of progression.
- **Armor enchants** are 40 G (tier 1) and 150 G (tier 3), cheap next to a mid weapon (~200–335 G).
- **Mercenaries** charge 45–70 G per battle up front for 3/5/10 battles, about a third of an early stage's pay per battle.
  The 10-battle term at 10% cheaper per step is fine, but check it against stage pay at Lv 10–20.
- **Rested:** 10 max, 1 per launch, so a refill covers 10 stages. Hunts also cost 1, so a hunting player refills often.
  That is the hook working, but it also taxes the grind the XP curve above demands.
- **Paywall placeholders:** summon, lucky craft and Rested refill are all still 1 g / 1 s, and the Diamond draw is 1 Diamond.
  These are placeholder prices, not tuned ones.
- **Consumables** run from 20 G (Antidote) to 500 G (X-Potion); the Tent is 300 G, the Revival Charm 450 G. The Healing
  Potion (25 G, 50 HP) vs Hi-Potion (75 G, 150 HP) step is linear, so there is no reason to buy small ones late.
- **Wanderer join rule:** low trust (0) can't join even with a faction-mate and three right answers (0 + 1 + 3 = 4 is the
  bare threshold). In practice that means half trust or better, which is fine, but say so in the talk's outlook line.

## Questions for the owner: cut outright?

These are recommendations only. Nothing was deleted.

1. **Crucible, bounty board, caravan upgrades.** They are hidden now. Should we delete them (code, content, save fields,
   tests, harness runs) or keep them switchable for a later update?
2. **Taming / domestication.** It overlaps with wanderers and summons as a fourth way to grow the roster, and
   domesticated monsters are half-heroes (no gear, no skills, Lv cap 30). Keep it, hide it (pull the Monster Tamer from
   the summon pool and the Dog Whistle from the shop), or cut it?
3. **Side quests vs the new story.** Every quest giver line is placeholder until the Prose story lands. Keep the journal
   and rewrite the ten quests into the new story, or hide it until then?
4. **Weapon enchants.** Cut the 600-recipe weapon tab down to a handful, or drop it and keep armor enchants only?
5. **Talents + Abilities + Tomes.** That is three ways to learn things. Fold Talents into the Abilities screen, or keep
   it as its own row?
6. **Status / Equip / Abilities / Party.** Four Camp rows for one hero. Merge them into a single hero screen with tabs?
7. **Gambits (GG-T299) and monster meat (GG-T304).** Close both tickets as out of scope for release?
