# Release scope (GG-T392, 2026-11-16)

Owner, 2026-11-16: *"we are aiming for a polished, releasable game once the story has been established; enough
development of more and more systems, need to refine and balance everything that exists, cut or at least disable that
which doesn't."*

**The core loop:** make a profile and start with basic heroes → fight 5-wave stages (pincers + the timeline) → earn gold
to repair and buy weapons, learn skills, buy items → a story scroll every 5 stages → wanderers joined by Trust and a game
of wits → Diamond and gold summons → a simple stage select to replay stages and hunt XP and heroes → Camp.
**Paywall hooks that stay:** summon, Diamond lucky craft, Rested refill (party energy), mercenary contracts. **Diamonds
(GG-T455):** 6 G each, 100 for $1.00; every Diamond price is payable in gold; the Diamond Store's real-money packs are not
connected to billing yet (release builds say "Store not connected yet").

## The switch

`Assets/GridGame/Meta/Features.cs` holds one `static bool` per hidden system. A switch that is off hides only the App
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
| Camp tips / onboarding chain (GG-T173–T180) | KEEP | New-player guidance. The bounty tip fires once the board is switched on (GG-T431 rewrote it for hunts) | — |
| Difficulty dial 1–10 | KEEP | One Settings row; 10 is the designed game | — |
| Settings: audio, battle speed, drag timer, colorblind, cut-ins, confirm, reduce motion | KEEP | Player options and accessibility | — |
| Settings: **Menu theme, Font, Text wrapping** | KEEP (on since 2026-11-17, owner, GG-T404) | Look presets (GG-T185, GG-T305), first cut as developer-only. Owner 2026-11-17: the themes and fonts are options to try and compare until the interface is the best it can be, so Settings offers them again. Off, every profile would draw with the default theme (Granite since GG-T404), font (Rounded) and word wrap. Revisit once the look is settled | `Features.LookPresets = true` |
| New Game+ | KEEP | Finished endgame; one row that shows only after the credits | — |
| Taming / domestication (Monster Tamer, Dog Whistle) | DISABLE (owner 2026-11-19, GG-T433) | A fourth way to grow the roster with half-heroes. The Summon Circle doesn't list the Monster Tamer and the Merchant doesn't stock the Dog Whistle (`Features.Offers`), the only ways in | `Features.Taming = true` |
| **The Crucible** | KEEP (owner 2026-11-19, GG-T430) | The game's "toilet play" horde mode: pick one team, endless generated waves that ramp in level, size and elites (`BattleConfig.Crucible*`), gold banked every wave and kept when the team falls, best wave and best haul recorded. Not a replay of cleared stages, so it no longer duplicates the hunt. Ready for the switch | `Features.Crucible = true` |
| **Bounty board** | KEEP (owner 2026-11-19, GG-T431) | Rebuilt as hunts for **marked targets**: 3 posters (seeded, turning over every 3 stage battles) for titled, tougher variants of existing foes (`BattleConfig.Bounty*`: +2 levels, ×2.5 HP, ×1.3 stats but Speed, optional rule-break trait), each with a clue "Seen wandering *stage*". The target spawns in its wave on that stage wearing a ☠ skull, the inspect card names it a bounty target, and its reward (150 G + 50 G per stage index, plus an item or SP) is collected at the board. The kill-N contracts that duplicated the side quests are gone. Bots skip it. Ready for the switch | `Features.Bounties = true` |
| **Caravan upgrades + crew barks** | KEEP (on since 2026-11-19, GG-T432) | A gold sink outside the loop, plus a flavor panel on Camp. Upgrades already bought keep working (Mana Casks, Armory discount, Scouts line, …). **2026-11-19, owner:** the caravan comes back as the loop's gold sink — the Forge and Alchemist **facilities** (Copper → Iron 400 → Steel 1,500 → Mythril 3,000 G; Clay → Glass 300 → Silver 1,200 G) gate material tiers (result rarity). The facility gate is rules, live whatever the switch says, and each facility is also built at its own station (the Blacksmith and Alchemist are KEEP), so the switch only adds the Caravan row | `Features.Caravan = true` |
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
  Frost's `BaseDamage` 10 → 16 (2.3/orb, in line with Meteor's 2.2/orb), then its `Cost` 7 → 10 (GG-T444, /test 2026-11-19: the §3.1.1 mana curve prices a 3×3 freeze at 16 damage at 10 orbs; 1.6/orb plus the area freeze); `fieldeng_overdrive`'s `Cost` 1 → 2 (6/orb,
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
  **GG-T448 playthrough (2026-11-19):** a fresh save played stage by stage through the real UI banked 6,474 g by stage 13
  with nothing to spend it on (starting heroes carry no gear that wears, so repair never came up), and bought the
  Alchemist's whole still ladder by stage 6. Re-priced: Summon 800 → 1,500 g, Diamond 250 → 600 g, Forge
  400/1,500/3,000 → 600/2,500/6,000, Alembic 300/1,200 → 500/3,000. Rested refill unchanged (forgiving by design).
  **GG-T455 (owner 2026-11-20): the Diamond exchange.** Diamonds re-denominated at `DiamondExchange.GoldPerDiamond` 6 G
  and `DiamondsPerDollar` 100, so 100 Diamonds = $1.00 = 600 G (the old one-Diamond price; gold buys exactly what it did).
  Diamond draw 100 ($1.00 / 600 G), instant Rested refill 25 ($0.25 / 150 G), lucky craft 50 ($0.50 / 300 G). Every Diamond
  price is paid through `DiamondExchange.Pay` (Diamonds first, the shortfall in gold), and gold buys Diamonds instantly at
  the Diamond Store; the 600 g LuckyDiamond job is retired (an old pending one is refunded). Real-money packs at a flat
  rate (100/$1, 500/$5, 1,000/$10, 2,500/$25, no bonus tiers) sit behind `IDiamondStore`: a free test stub in editor and
  dev builds, "Store not connected yet" in release until a billing integration (Unity IAP / Google Play / Apple) is wired.
  Not yet felt in real play: the owner's call.
- **Facility prices (GG-T432, first pass, not measured).** Iron Forge 400 G, Steel 1,500 G, Mythril 3,000 G; Glass
  Alembic 300 G, Silver 1,200 G: 6,400 G for the whole ladder against ≈9,900 G of main-campaign pay, competing with
  summons (800 G) and training (~300 G a skill). Watch whether Rare gear (Iron Forge) arrives too late for the bots;
  `CampRoutine` builds a level once it costs half its gold. **Re-priced 2026-11-19 (GG-T448)** to 600/2,500/6,000 and
  500/3,000 against the measured pay above — see the Paywall entry.
- **Repair pricing vs. facilities, a genuine tension (GG-T448, 2026-11-19).** A camp bot that forges gear and keeps it
  repaired every visit spends 97-98% of a whole campaign's gold on repairs — at `Repair.RepairRate` 0.30 (17,246 g
  earned, 16,758 g repaired per run) and still at 0.15 after halving it (18,152 earned, 17,865 repaired). Cheaper
  repairs don't free up gold; they just buy more repairs before a piece crosses `IsUneconomical`, because the camp
  routine repairs every worn piece back to full every visit by design, and facilities/the Summon Circle are
  deliberately left to repair's leftovers. Halved anyway (0.30 → 0.15) for the modest room it does give facilities/gear
  (150 → 275, 3,937 → 4,981 g/run), but a player who keeps every piece of gear repaired will likely still feel starved
  for facility/summon gold. The owner's fresh-save playthrough above never hit this at all (no gear wears without
  forging), so the two playstyles feel very different — worth a played-through pass that actually forges and repairs
  gear before trusting either number.
  **Resolved 2026-11-21 (GG-T540, BIBLE §25.0.11):** the cause was the routine's scratch repairs after every battle, each
  compounding the 1.6× growth. With `CampRoutine.RepairBelowFraction` = 0.6, repairs went from 68%/70% to 28%/19%
  (greedy/casual) of main-campaign spend; measured with `tools/polish.py --balance` (POL-G8). Postgame not measured.
- **Consumables** run from 20 G (Antidote) to 500 G (X-Potion); the Tent is 300 G, the Revival Charm 450 G. The Healing
  Potion (25 G, 50 HP) vs Hi-Potion (75 G, 150 HP) step is linear, so there is no reason to buy small ones late.
  **GG-T448 second playthrough (2026-11-19, a fresh save through the real UI, this time forging and repairing gear):**
  keeping the party's healing stock topped at the best affordable potion recurs every camp visit by its nature
  (battles consume stock), and once X-Potion (500 G) was the cheapest "under 15% of the purse" tier left to restock,
  3 of them cost 1,500 G a visit — more gold per visit than repair (6–10 G for one hero's forged weapon+gloves,
  nowhere near the 97% BalanceReport measured for a fully-geared 10-hero bot) or either facility built that run (Iron
  Forge 600 G, Glass Alembic 500 G, each bought once). Potions, not repair, were this run's actual gold sink — the
  opposite of the no-gear-wear first playthrough, where nothing but facilities ate gold. Both are real playstyles;
  neither alone tells the whole story. Worth the owner's own feel for X-Potion's price before trusting it.
  **GG-T448 third playthrough (2026-11-20, 21 of 40 stages, 22/23 battles won through the real UI):** a real
  automation gap was found and fixed first — the `goto` endpoint had no case for "caravan" at all (added since
  GG-T432, never wired in), so no automated run had ever been able to open the Caravan screen, let alone buy a
  wagon. With it fixed, this run bought one of every Caravan upgrade (Scouts, Armory ×2, Mana Casks ×2,
  Quartermaster's Ledger ×2, Strongbox, Medic Wagon) and ran the Forge ladder up to Steel Forge (2,500 G) — the
  fullest the buy/repair/upgrade loop has been exercised yet, zero tap failures. It stopped on its own at a real
  difficulty wall (Cathedral II, Lv 18, 13-hero roster, 3 failed Retry attempts), not a crash or a script bug — the
  second genuine wall found this session (the first was Cave II, Lv 13–14, in an earlier run). Both are worth the
  owner's own eyes before calling them intended or a cliff to soften.
  **Correction, GG-T448/GG-T452 (2026-11-20): both of those walls were the test, not the game.** The Equip tab
  overflowed a phone screen (its bag list sat below the stage), so nothing could be equipped through the UI at all, and
  five of six heroes fought bare-handed. With the layout fixed (GG-T452) and the driver gearing the whole party, run 8
  **finished the main campaign** (Ending at Cathedral III, `story:ending` set): 24 of 24 main stages won on the first
  try, Cave II and Cathedral II included, plus the Bloom I-III, all through the real UI. Over the run it equipped 45
  pieces, repaired 56 times, built the Glass Alembic and Iron Forge, bought 9 Caravan upgrades, and never ran Rested dry
  (real time between stages regenerated it); gold climbed steadily to about 3,000.
  **The postgame wall is real** and matches the nightly measurement below: Free Holds I (enemy Lv 28) beat a Lv 20 party
  four times running. Hunting the Bloom III (the game's intended answer) paid about one level per three hunts, so the
  gap is roughly 20+ hunts; each costs a Rested point, and the gold refill had grown to 505 G. That is the grind the
  Diamond/Rested hooks sit on; whether it is the right length is the owner's call (options in the section below).
  **GG-T476 fourth playthrough (2026-11-23, partial: fresh save, first 5 of 40 stages, 5/5 battles won first try, no
  retries, through the real UI, after the repair-threshold GG-T540 / Dust GG-T569 / GG-T276-T215 balance changes):**
  ledger (gold at the camp before each stage; Rested left): start 200 g, Rested 10; Green Valley I won, 375 g (party Lv 4,
  3 heroes); II won, 494 g (Lv 5, Redmage joined); III won, 1,070 g; Lantern Road I won, 991 g after 300 g of facility/gear
  spend; II won, 789 g. Rested drained 10 -> 6 over the five camps. Gold income (~120-575 g per stage) comfortably outpaces
  the early spend and nothing wore enough to need repair yet. The run did not go further: the harness lost the player
  mid-run (socket TimeoutError) and the idle-time reaper killed an earlier unchunked run, so the rest of the campaign
  (stages 6-40, where the economy and postgame changes actually bite) is not yet measured; see the follow-up tickets.
  **GG-T704 resumed that save (2026-11-23, 3-battle `--resume --keep` chunks; stages 6-10, 5/5 won first try, no retries):**
  gold at the camp before each stage / Rested left / roster: Lantern Road III 789 g / 6 / 4 heroes Lv 8; Desert I 811 g / 5 /
  4 (Lv 9); II 578 g / 4 / 5 (Engineer joined); III 970 g / 4 / 6 (Dervish, party Lv 8-10); Swamp I 1,559 g / 3 / 7
  (Sellsword, Lv 9-11); Swamp II 1,498 g / 2. Rested drains about one point a stage (10 -> 2 after 14 camps) and the gold
  refill price climbs 15 g a camp (100 -> 250 g), so a player who never rests will meet the refill gate around stage 15;
  gold income per stage (~400-1,000 g) still outruns spend, ending the chunk near 1,500 g. Barbarian's weapon is the
  most worn (133/180) and Redmage/Cleric drop to low HP at stage end but nobody was KO'd out of the battle.
  **The player crashed (native UnityPlayer access violation, no managed exception) at the start of Swamp II's battle**
  (stage 11 of 40; crash report in R:/Temp/MindAttic/GridGame/Crashes, log not kept); the run stopped there on the owner's
  instruction not to launch more players. Stages 11-40 remain unmeasured; see GG-T704's split tickets.
  **GG-T710 resumed again (2026-11-23, one player, 3-battle `--resume --keep` chunks, `-rested` build of main):** the Swamp II
  crash did NOT reproduce: Swamp II (stage 11) won first try (camp 1,048 g after a 250 g Rested refill, Rested 2 -> 10), Swamp III
  (12) won (1,323 g, Rested 9), Cave I (13) won (1,904 g, Rested 8), all 0 retries; gold at the camp before each stage 1,048 /
  1,323 / 1,904, ending the chunk near 2,355 g. The next chunk lost the player at the start of **Cave II (stage 14)**: native
  UnityPlayer access violation inside `Resources.Load` called from `Audio.Clip` <- `Audio.Music` <- `ScreenRouter.MountTop`
  (Audio.cs:74/90, ScreenRouter.cs:177) while the battle screen mounted; the harness then hit its socket TimeoutError. Same
  signature as the Swamp II crash (native, at battle start), so it is an intermittent music-clip load crash, not a Swamp II
  stage bug. The crash is filed as its own ticket (GG-T741, since fixed).
  **GG-T742 resumed again (2026-11-24, one player run from a scratch copy of a fresh `-SkipProse` build of main that includes the
  GG-T741 music preload, 3-battle `--resume --keep` chunks, port 28906; Cave II's music-load crash did NOT reproduce):** stages
  14-27 won, every one first try with 0 retries (Cave II/III, Still I-III, City Ruins I-III, Cathedral I-III, Bloom I-III; 14/14).
  Battle seconds 127-256 at x3 speed, rising with depth (Cave II 127 s, Bloom III 256 s). Party level (warden) 13 at Cave II, 19
  at Bloom III; Rested pool ran 10 -> 1 over the stretch (it only refills via the paid gate). Gold at the camp before each stage:
  1,755 / 2,590 / 2,925 / 2,655 / 3,218 / 4,262 / 3,373 / 3,867 / 4,912 / 3,871 / 2,923 / 4,393 / 3,377 / 2,632, ending at 3,176
  before stage 28; gold stays in the 2.6-4.9k band, so the economy is not what gates this stretch. **Stage 28 (`greenvalley-04`,
  the next stage after Bloom III in play order) is a difficulty wall for this autoplay party:** lost 4 times (first try plus 3 retries) with the party
  at level 19 and Rested 1, consistent with the postgame level wall the nightly measured (see "the postgame level wall" below).
  Stages 29-40 remain unmeasured because the bot cannot clear stage 28; that needs the owner's pick on the wall options
  (GG-T474), not another chunk run. Full per-battle log: `artifacts/playthrough2/play.jsonl` (git-ignored).
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

### Worked out: the wall per option (GG-T757, `tools/xp_wall.py`)

Numbers for GG-T474, computed from content, not a soak; no balance value changed. `python tools/xp_wall.py` plays the
campaign straight through on paper (every hero earns the battle's full XP, every foe killed once at Normal, party from
level 1) and prints each cell below as **party level arriving / foe level (replays of the previous stage needed to
reach the foe level)**. Options: **now** = current curve (option 4, keep it); **1a** = kill XP's level term
+ 2*(L-24)^2 past 24 (option 1's example); **1b** = kill XP x XpToNext(L)/XpToNext(24) past 24; **2a** = per-region
levels, Bloom 25 then +2 a region, Morrow's Shadow III kept at 40; **2b** = +1 a region, Morrow's Shadow III 34;
**3** = XpToNext linear past 24 (+480 a level); **5a** = flat postgame bonus, kill XP x2 past 24; **5b** = catch-up,
+50% kill XP per level the hero is behind the foe (max x4) past 24.

| Stage | now | 1a | 1b | 2a | 2b | 3 | 5a | 5b |
|---|---|---|---|---|---|---|---|---|
| The Bloom I | 15/25 (47) | 15/25 (47) | 15/25 (47) | 15/25 (47) | 15/25 (47) | 15/25 (47) | 15/25 (47) | 15/25 (47) |
| The Bloom II | 16/26 (61) | 16/26 (59) | 16/26 (56) | 16/25 (52) | 16/25 (52) | 16/26 (61) | 16/26 (30) | 16/26 (27) |
| The Bloom III | 16/27 (67) | 16/27 (60) | 16/27 (57) | 16/25 (51) | 16/25 (51) | 16/27 (67) | 16/27 (33) | 17/27 (29) |
| The Free Holds I | 16/28 (67) | 16/28 (54) | 16/28 (53) | 16/27 (62) | 16/26 (53) | 16/28 (67) | 17/28 (32) | 18/28 (28) |
| The Free Holds II | 16/29 (81) | 17/29 (57) | 17/29 (59) | 16/27 (63) | 16/26 (56) | 16/29 (81) | 18/29 (39) | 19/29 (32) |
| The Underspire I | 17/30 (80) | 17/30 (49) | 17/30 (54) | 17/29 (75) | 17/27 (58) | 17/30 (79) | 18/30 (38) | 20/30 (31) |
| The Underspire II | 17/31 (94) | 18/31 (50) | 18/31 (59) | 17/29 (75) | 17/27 (60) | 17/31 (93) | 19/31 (44) | 21/31 (36) |
| The Drowned Arks I | 17/32 (92) | 18/32 (42) | 18/32 (54) | 17/31 (86) | 17/28 (62) | 17/32 (90) | 19/32 (43) | 22/32 (34) |
| The Drowned Arks II | 18/33 (112) | 19/33 (44) | 19/33 (62) | 18/31 (92) | 18/28 (67) | 18/33 (110) | 20/33 (53) | 23/33 (40) |
| Deep Shardfall I | 18/34 (109) | 20/34 (37) | 19/34 (56) | 18/33 (104) | 18/29 (69) | 18/34 (106) | 20/34 (51) | 23/34 (39) |
| Deep Shardfall II | 18/35 (129) | 20/35 (38) | 20/35 (62) | 18/33 (107) | 18/29 (73) | 18/35 (125) | 20/35 (60) | 24/35 (45) |
| Under the Anchors I | 19/36 (125) | 21/36 (32) | 20/36 (57) | 18/35 (119) | 18/30 (74) | 19/36 (120) | 21/36 (58) | 25/36 (43) |
| Under the Anchors II | 19/37 (145) | 22/37 (33) | 21/37 (62) | 19/35 (122) | 19/30 (78) | 19/37 (138) | 21/37 (67) | 25/37 (49) |
| Morrow's Shadow I | 19/38 (142) | 23/38 (29) | 21/38 (57) | 19/37 (136) | 19/31 (81) | 19/38 (134) | 22/38 (66) | 26/38 (47) |
| Morrow's Shadow II | 19/39 (162) | 24/39 (30) | 22/39 (62) | 19/37 (138) | 19/31 (83) | 19/39 (153) | 22/39 (75) | 26/39 (53) |
| Morrow's Shadow III | 20/40 (171) | 25/40 (28) | 22/40 (61) | 20/40 (179) | 19/34 (117) | 20/40 (159) | 23/40 (79) | 27/40 (56) |
| **Total hunts, close every gap** | **202** | **109** | **130** | **199** | **122** | **189** | **117** | **202** |
| Straight-through level after Morrow's Shadow III | 20 | 26 | 23 | 20 | 20 | 20 | 23 | 28 |

What the numbers say:

- **The wall starts in the main campaign, not the postgame.** A straight-through party falls behind at the Swamp
  (level 9 against 10) and reaches The Bloom I at **15 against 25**; every option above only touches levels past 24,
  so all of them start the Bloom 10 levels down and 47 hunts short. The soak's falloff lines up with the gap: 80-100%
  wins up to a gap of about 10 (Cathedral, the Bloom), 50% at 12 (Free Holds I), 0-12% at 15-16 (Drowned Arks II,
  Deep Shardfall I).
- **Now:** the gap grows a level every ~1.5 stages, 10 to 20; closing every gap costs **202** replays.
- **Only 5b holds the gap** (10-13, the Bloom's own) for a straight-through party; 1a holds it at 14-15 and roughly
  halves the hunting (109). 1b and 5a slow the growth (to 18 and 17). 2a is the current climb in steps and changes
  almost nothing; 2b flattens the ramp (gap 10-12 until the finale's 34) but ends 6 levels lower. 3 barely helps:
  at 25-40 the bent curve is only 0-16% cheaper a level than the quadratic.
- Catch-up (5b) gives nothing to a party that is already level, so its close-every-gap total stays 202; it rewards the
  straight-through player rather than the hunter.

### Decision brief for the owner (GG-T474, picked: 5b, applied 2026-11-26)

**Owner pick 2026-11-25: 5b, catch-up XP on hunts. Applied (GG-T474):** past foe level 24, a hero below the strongest foe earns +50% XP per level behind, capped at x4 (`BattleConfig.CatchUp*`, `Formulas.CatchUpXp`; BIBLE section 13). The start level was not specified, so the brief's 24 stands; starting earlier is unmeasured. Not soaked: GG-T919 measures stages 29-40.

The question: Free Holds I (foe level 28) needs about 67 replays of the previous stage for a straight-through party
(level 16), and the gap keeps growing. Pick one; nothing below is applied.

| Pick | What it is | Gap, Bloom I to the finale | Hunts to close every gap | Cost |
|---|---|---|---|---|
| Keep (now) | Grind the postgame by hunting | 10 growing to 20 | 202 | Free; the soak says 0-12% bot wins at Drowned Arks II and Deep Shardfall I |
| 1a | Late kill XP scales up past level 24 | 10 growing to 15 | 109 | Keeps both pinned curves; gap still passes the ~10 the bots can win |
| 5b | Catch-up: +50% kill XP per level behind, max x4, past 24 | 10-13 (held) | 202 (no help to a party already level) | Keeps both pinned curves; rewards falling behind, so it must be tuned so it cannot be farmed |
| 5a | Flat x2 kill XP past 24 | 10 growing to 17 | 117 | Simplest; slows the wall, does not hold it |
| 2b | Postgame levels step +1 a region, finale 34 | 10-12 | 122 | Breaks GG-T357 (one level a stage); finale is 6 levels lower |
| 1b, 2a, 3 | Scaled kill XP, steps to 40, bent curve | 17-20 | 130 / 199 / 189 | Little or no gain; 3 also breaks GridGame2026 parity |

Recommendation: **5b**, with 1a as the second choice. It is the only option that holds the gap in the range the bots
already win (10-13) without touching the two pinned curves, and a player who hunts is not penalised (hunting only
closes the gap faster, same as today). Two caveats to decide with it: (a) the gap opens in the main campaign (15
against 25 at Bloom I), so a catch-up that starts at level 24 begins 10 levels down; starting it earlier (for
example from level 10) is a question for you and is not yet measured; (b) 5b alone leaves the 202-hunt total, so
pair it with 1a if you also want less grinding for the player who falls behind anyway. If you prefer the least change,
choose Keep and accept the soak's 0% stages as the intended wall.


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
