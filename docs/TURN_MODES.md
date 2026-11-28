# Turn modes: how they are compared

> **Stub (GG-T765, 2026-11-23).** The rules live in [BIBLE §2.13](BIBLE.md#213-turn-modes-gg-t762). This file says
> how the four turn modes are measured against each other. The verdict (which mode is most fun, for what and why) is
> written here by GG-T822 once every mode is built and soaked. Raw tables go in
> [`research/TurnModes.md`](research/TurnModes.md) (GG-T778 baseline, GG-T821 full run).

Owner, 2026-11-23: "… tested headlessly and figure out which works better for what and why and report back to me which
is the most fun, I can see all of them being viable game modes."

## The modes

| Mode | `TurnMode` | Rule in one line | Status |
|---|---|---|---|
| Party window | `PartyWindow` | Every able hero acts once per window (until the next enemy reaches the left edge). Default and baseline. | Built (GG-T308) |
| Any hero every turn | `AnyHeroEveryTurn` | One hero acts per party turn, any hero, repeats allowed. | ⬜ GG-T779 |
| Any hero who hasn't moved this round | `UnmovedThisRound` | One hero acts per party turn, each hero once per round. | ⬜ GG-T792 |
| Heroes on the timeline | `HeroTimeline` | Each hero has its own icon on the bar and acts when it reaches the left edge. | ⬜ GG-T805 |

## Method

- **Headless only.** The comparison runs on bot soaks, not game players. `tools/turn_modes.py` (GG-T778) runs the soak
  (`HeldTurnSoakReport` via `dotnet test --filter`) once per mode, with `GRIDGAME_TURN_MODE` set to that mode. It
  gathers the per-mode JSONs (`turn-mode-<mode>-<bot>.json`) into `artifacts/turn-modes/<date>/` and prints one
  markdown table.
- **Equal terms.** Every mode gets the same inputs:
  - the same fixed stage list: every region, early, mid and late campaign, and at least one boss;
  - the same seed set, plus three extra seed sets to measure variance;
  - both bots (smart and casual);
  - difficulty 5 and difficulty 10;
  - the same content and the same general `BattleConfig`.
  The only numbers allowed to differ are a mode's own per-mode `BattleConfig` constants, and each one is listed next to
  the results.
- **Baseline first.** PartyWindow is measured before any other mode lands and must replay identically through the
  `TurnRules` seam. A later mode is judged against that baseline, not against an earlier run of itself.
- **Bots must be fair to each mode.** A mode is only judged once `HeroBot` plays it competently (its "bots and enemy
  AI" story). A result that comes from the bot's weakness, not from the mode, is called out as that, not as the
  mode's verdict.

## Metrics (per stage, per bot, per difficulty)

| Metric | What it tells us |
|---|---|
| Win rate | Is the mode beatable, and is it trivial? Compare against the target band from the balance loop (GG-T276). |
| Battle length (hero actions, enemy turns, seconds) | Pacing against the 2–5 minute battle target (BIBLE §0.3). |
| Decisions per player turn (heroes able to act × their legal actions) | How much choice each turn offers: richness versus paralysis. |
| Hero actions per enemy turn | How much the player gets to do between enemy hits. |
| Downtime (longest run of enemy turns with no hero action; enemy-only seconds) | How long the player sits watching. |
| Pincers and chains | Whether the core verb (pincers) still happens, and how often it is a combo. |
| Guards (forced and chosen) | Whether the player is locked out, or chooses to wait. |
| Ability, spell and item uses | Whether the AbilityBar stays relevant. |
| Mana by source | Whether the party mana pool economy (BIBLE §3.1) holds in the mode. |
| Bot refusals | Rule friction: commands the engine refused. |

## Reading the numbers ("fun" is a judgement, not a metric)

No single number is "fun". The report reads the metrics together and says, for each mode:
- **what it is best at** (for example: a puzzle feel, tempo pressure, planning across a round, reading the timeline);
- **what it costs** (downtime, choice overload, a broken economy, a weak bot);
- **who it suits** (a short session, a careful planner, a Terra Battle or Grandia player).

It ends with one recommendation and the reasons for it.

## Results

⬜ Not yet run. GG-T778 commits the PartyWindow baseline, GG-T821 the four-mode table, and GG-T822 the verdict.
