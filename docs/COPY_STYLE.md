# Copy and label style (GG-T2045)

How GridGame's interface words are written: who speaks, how they are cased, which verb a button uses, which one
name each concept has, how numbers print, how long a label may be on the narrowest screen, and what empty and error
states say. The page version, with a label-fit checker, is
[docs/design/copy-style/index.html](design/copy-style/index.html) (opens from disk).

Scope: UI text, the words that live in code (titles, tabs, buttons, rows, toasts, confirms, empty states, lesson
captions). Story text (dialogue, crawls, descriptions, barks, glossary definitions) lives in Prose and follows its
own canon ([PROSE.md](PROSE.md), [BIBLE §27.2](BIBLE.md#272-narrative-sourcing-prose-gg-t355)); the naming and
number rules below still apply to it.

Sibling guides under GG-T1999 (Design system written down):

- **GG-T2044, design system:** [DESIGN_SYSTEM.md](DESIGN_SYSTEM.md) and
  [design/design-system/](design/design-system/index.html). Tokens, components and their states. This guide takes
  its type size and widths from the same tokens.
- **GG-T2046, icons:** [ICONS.md](ICONS.md) and [design/icon-set/](design/icon-set/index.html). Every label in this
  guide that sits beside an icon (nav tabs, footer buttons, battle actions) is paired with an icon there.

Both are being written alongside this one; the links resolve once they land.

## 1. Owner rules (not negotiable)

1. **No `...` and no truncation.** No literal ellipsis in any string, no text cut off at a box edge, no word broken
   mid-syllable to squeeze a box (GG-T1850, UX review §3).
2. **Labels must fit.** Every label fits its box at its own size on the narrowest stage (section 7). Shrink to fit
   and marquee are safety nets for the player's Text size setting, not a plan for a label that is too long.
3. **The commit button names the move type:** **Perform** (a skill or a weapon move), **Cast** (a spell), **Use**
   (an item). Never "Go", "OK" or "Confirm".
4. **Tabs:** **Recruit** (was Summon) and **Journey** (was Campaign). Approved, GG-T1964.
5. **The currency is "Mana Shards"** (singular "Mana Shard"), GG-T1974. Never Diamonds, Shards on its own, or
   Aetherite.
6. **Mana is the party mana bar:** the one thin bar the whole party shares (BIBLE GG-LAW-6, §3.1.0). "Mana" means
   that bar and its points, nothing else. Grants are worded in points ("+200 mana"), never orbs.

## 2. Voice: the Commander, in the second person

The player is the Commander (BIBLE §9.2, §25.11.2.2). The interface talks to them as **you**.

- Second person, present tense: "You need 200 more Mana Shards." "Your party rests until morning."
- Never "the player", never "we", never "the user". Heroes are named ("Mira is down"), not "unit" or "character".
- The Commander's own lines (advice, orders, story beats) are spoken by the Commander to the party: imperative,
  short, naming the hero ("Mira, flank the frog."). They live in Prose barks (`bark:commander/...`), not in code.
- Plain words over jargon. A game term the player must learn is a `[glossary]` link the first time a screen uses it
  (section 10).
- One idea per line. Say what happened and what to do next, in that order.
- No exclamation marks in interface text. Combat floats and callouts ("Resolve!", "CLUTCH!") are the exception.

## 3. Casing

| Text | Case | Examples |
| --- | --- | --- |
| Screen titles, nav tabs, sub-tabs, section headers | Title Case | Mana Shard Store, Journey, Summon History |
| Destination buttons (name a place) | Title Case, "To" + place | To Camp, To Journey |
| Action buttons | Sentence case, verb first | Set up camp, Next battle, Replay tutorials |
| Body, rows, captions, toasts, confirms, empty states | Sentence case | Wounds carry to the next battle. |
| Proper names (heroes, classes, places, items, abilities) | As named | Tithe Moors, Counter Attack |
| Stat abbreviations | Capitals | HP, ATK, EVA |

Title Case keeps short words lower case unless first: a, an, and, at, by, for, in, of, on, or, the, to (except the
"To" that starts a destination button).

## 4. Verb vocabulary

One verb per job. A button starts with its verb; the exits are fixed words.

| Verb | Means | Where |
| --- | --- | --- |
| **Perform** | Commit a skill or weapon move | Battle ability sheet and confirm row |
| **Cast** | Commit a spell (spends from the party mana bar) | Same |
| **Use** | Use an item from the bar or the bag | Same; Items |
| **GUARD** | The battle Guard action | Battle action row (kept as built, see section 11) |
| **To Camp** | Leave results for camp; on the road this sets up camp | Results footer |
| **Navigate** | Pick a destination on the overworld map | Journey map (BIBLE §28) |
| **Set up camp** | Stop on the road and make camp | Hub mode banner, road prompts |
| **Rest** | Sleep in camp until morning | Camp |
| **Recruit** | Hire a mercenary; the tab that holds summons and mercenaries | Recruit tab |
| **Summon** | Pull on a banner | Recruit tab |
| **Ask** | Ask the Commander for advice | Battle advice card |
| **Buy / Sell / Equip / Learn / Teach / Craft** | What they say | Shops, Heroes, stations |
| **Back** | Return to the previous screen | Header, footer left |
| **Close** | Dismiss a sheet or a card | Sheets, modals |
| **Cancel** | Abandon a choice in progress | Confirm dialogs, aiming |

- A confirm dialog's yes button repeats the action's verb ("Buy", "Summon", "Set up camp"); its no button is
  "Cancel". Never OK, Yes or No.
- Cancel or Back sits on the left, the action on the right.
- One word per action across screens: "To Camp" everywhere results lead to camp, not "Continue" on one screen and
  "To Camp" on another.

## 5. One name per concept

| Concept | Use | Never | Status |
| --- | --- | --- | --- |
| Premium currency | Mana Shards (1 Mana Shard) | Diamonds, Shards, Aetherite | Decided, GG-T1974 (`ShardText`) |
| Its store | Mana Shard Store | Diamond Store, Shard Store | Decided, GG-T1974 |
| Soft currency | gold, printed "1,250 G" | coins, money | As built (`Ui.Gold`) |
| Summon currency | Summon Tickets (1 Summon Ticket) | Tickets on its own | Default, section 11 |
| Battle resource | mana: the party mana bar, in points | orbs, MP, per-hero mana | Decided, GG-LAW-6 |
| Road energy pool | Rested (unchanged for now) | Energy | **Pending owner, GG-T1706** proposes Stamina |
| Camp buff | Well Rested | Rested on its own | Follows GG-T1706 |
| Hero progression board | Talents | Board, Skill Board, Skill Hex | Default, section 11 |
| A hero's moves | Skills (moves on cooldown), Spells (cost mana), Items | Abilities, as a player word | Default, section 11 |
| Map and stages tab | Journey | Campaign, Adventure | Decided, GG-T1964 |
| Gacha and mercenaries tab | Recruit | Summon, Summon Circle | Decided, GG-T1964 |
| The player | the Commander, "you" | player, user | Decided, section 2 |

GG-T1706 (Rested renamed Stamina) waits on the owner. Until it lands, keep "Rested" in the game and in new copy,
and do not rename it here or in code.

Internal ids keep old words (`mat_diamond`, `aetherite_shard`, `CampMenu.Campaign`, `Diamond*` classes); only
player-facing text follows this table.

## 6. Numbers and units

- **Digits always** in UI text ("3 heroes", not "three heroes"). Spelled-out numbers belong in story text only.
- **Thousands commas:** "1,250", the same on every device (`Ui.Count`, `Ui.Gold`); never the phone's culture.
- **Gold:** number, space, G: "5,000 G". **Mana Shards and tickets:** number then the full name: "40 Mana Shards",
  "1 Summon Ticket" (`ShardText.Of`). Singular at exactly 1.
- **Mana:** points, lower-case word: "Costs 300 mana", "+200 mana". The bar holds 1000.
- **Seconds:** number, space, s: "3.7 s", at most one decimal in the HUD (as `SupportText.Sec` prints). Minutes and
  longer: "2 min", "1 h 20 min".
- **Percent:** no space: "30%". Chances in descriptions use the band words ([slight], [small], [fair], [good],
  [likely]) instead of a number (BIBLE §9, GG-T300).
- **Signed changes:** "+30% Crit", "-2 ATK" with the sign stuck to the number.
- **Stacks:** "×15" (multiplication sign, no space): "X-Potion ×15".
- **Levels:** "Lv 12". **Progress:** "2/3 cleared", "battle 2 of 3".
- **Separators:** a middle dot with spaces, " · ", between facts on one line ("Lv 1 · 5 waves").

## 7. Label length per role, on the narrowest shape

Every in-target shape lays out on a stage at least **1170 design px** wide (`GameApp.DesignWidth`; narrower aspects
such as the Galaxy Fold cover grow the stage taller, never narrower, BIBLE §26.3), so 1170 is the width every budget
uses. GUI text is one size, `TypeScale.Base`, at most **56 px** at Text size 100% (`GuiType.MaxDesign`), and a glyph
averages half an em (`GuiType.GlyphEm`), so one character takes about 28 px. Budget = floor(box width / 28). The hard
cap is the same box at the 30 px shrink floor (`TypeFloor.ShrinkMin`).

| Role | Box width (design px) | Budget (chars) | Hard cap (chars) | Example |
| --- | --- | --- | --- | --- |
| Bottom nav tab | 234 (1170 / 5) | 8 | 15 | Journey (7) |
| Sub-tab, 4 across | 248 | 8 | 16 | Passives (8) |
| Footer button, 2 across, with icon | 425 | 15 | 28 | Set up camp (11) |
| Footer button, 3 across, with icon | 235 | 8 | 15 | Perform (7) |
| Battle commit button | 425 | 15 | 28 | Perform (7) |
| Page title (back and action buttons beside it) | 826 | 29 | 55 | Mana Shard Store (16) |
| Menu row label beside a value | 447 | 15 a line, 2 lines | 29 | Replay tutorials (16, 2 lines) |
| Menu row value (at most 55% of the row) | 546 | 19 | 36 | 1,000 Mana Shards (17) |
| Chip or badge | 150 | 5 | 10 | ×15 |
| Combat float or callout | 420 | 15 | 28 | Resolve! |
| Toast, confirm body, empty state | 1122, wraps | 40 a line, 2 lines (80) | 74 a line | No quests yet. Talk to people in town to find one. (51) |

Box widths come from the kit tokens: 24 px gutters (`Fit.Edge`), 16 px gaps (`Kit.Gap`), 24 px button padding
(`Kit.PadX`), 64 px icons (`Size.Icon`), 132 px header buttons (`Size.Target`). Paragraphs never run wider than
60 characters a line (`Typography.ParagraphChars`), whatever the box.

- The budget is the target: a label within it fits on every in-target device at Text size 100% without shrinking.
- Beyond the budget, `TextFit` shrinks it, then marquees it; both count as a miss to fix in the copy.
- At Text size 115% and 130%, shrink to fit is expected and fine.
- Price buttons are the tightest case: "1,000 Mana Shards" (17) goes in a row value or on two lines, not on a
  3-across button.

## 8. Fitting without truncation

1. Shorten the words first: pick the shorter verb from section 4, drop articles, move detail into the row's caption
   or the InspectCard.
2. Then let the label wrap to a second line (rows, two-line buttons).
3. Never add `...`, never cut, never hyphenate a label under 12 characters to make it fit.
4. Long names (heroes, items, abilities) wrap on word boundaries; code never abbreviates them.
5. Placeholders the code fills (`{hero}`, `{ability}`, prices) are measured at their longest real value.

## 9. Empty and error states

- **Empty state:** one sentence that says what will be here and how to get it, then the action button if there is
  one. "No quests yet. Talk to people in town to find one." with `[To Journey]`. Never just "Empty" or "None".
- **Locked:** say what unlocks it: "Unlocks after The Tithe Moors III." Never "Locked" on its own.
- **Not enough:** name the shortfall and the fix: "You need 200 more Mana Shards." with `[Mana Shard Store]`.
- **Error:** what happened, in plain words, and what to do: "Couldn't save. Free some space and try again." No
  codes or exception text in player-facing copy (log them instead), no blame, no "Oops".
- **Disabled button:** the reason shows beside it or in a toast on tap ("No mana", "Recharging: 2 turns"), never a
  silent grey button.

## 10. `[glossary]` links

Square brackets mark a tap term (BIBLE §9, GG-T300): the brackets are hidden and the word is underlined in
`Palette.Term`; a tap opens its definition card.

- Every bracketed term must resolve to an `Id`, `Term` or `Aliases` entry in `glossary.json`; `ContentLoader.Validate`
  fails on one that doesn't.
- Link a term the **first time** it appears in a block of text, not every time.
- Bracket game terms the player has to learn (statuses, keywords, timeline words, chance bands, currencies), not
  ordinary English words.
- Bracket the word as it reads in the sentence; add an alias to the glossary entry rather than rewording the
  sentence to fit the headword.
- No brackets in buttons, tabs, titles or chips: those are taps already. Brackets belong in descriptions, cards,
  lesson popups and help text.
- A renamed concept keeps its old name as an alias (so "Diamonds" and "Rested" in older text still resolve) until
  every text is updated.

## 11. Open naming questions (with the default used until the owner decides)

| Question | Default | Why |
| --- | --- | --- |
| Rested renamed Stamina (GG-T1706) | Keep **Rested** | Waiting on the owner; no change until it lands |
| Talents, Board, Skill Board or Skill Hex | **Talents** | UX review §3; one name on Status, Abilities and Respec |
| "Abilities" as a player word, or Skills / Spells / Items | **Skills, Spells, Items**; "Abilities" stays internal | Matches the Perform / Cast / Use split |
| The pull button once the tab is Recruit | **Summon** | The act is still a summon; Recruit names the tab and hiring a mercenary |
| Summon Tickets once the tab is Recruit | **Summon Tickets** | Same reason; never "Tickets" alone |
| GUARD in capitals or Guard | **GUARD** on the button, "Guard" in sentences | As built and taught by the lesson |
| "Make camp" or "Set up camp" | **Set up camp** | The owner's vocabulary; "Make camp" is drift |
| "Continue" on results when not on the road | **Continue** only when the next screen is not camp | "To Camp" wherever camp is next (UX review item 17) |
| Ellipsis under Reduce Motion (BIBLE §26.6) | **No ellipsis**: shrink to the floor, then wrap | Owner rule 1 overrides the old marquee fallback |

## 12. Known drift in the game today

Places that break this guide now; each needs its own fix ticket. GG-T2045 changes none of them.

- `BattleScreen.cs` confirm row and `BattleScreen.Tutorial.cs` coach step use **"Go"** for a non-spell move; the
  rule is **Perform** (the ability sheet already says Perform).
- Bottom nav and screen titles still read **Summon** and **Campaign** until GG-T1964 lands.
- `CampMenu` shows **"Make camp"** on the Rest row; the vocabulary is **Set up camp**.
- Heroes › Abilities sub-tab, the Status column and the party board button read **"Board"**; the default is
  **Talents**.
- `Marquee` shows an **ellipsis under Reduce Motion** (BIBLE §26.6), against owner rule 1.

## 13. Checklist before a string ships

- [ ] Second person, sentence case body, Title Case title, verb-first button.
- [ ] The verb is from section 4 and the noun from section 5.
- [ ] Numbers follow section 6.
- [ ] Fits the role's budget in section 7 at 56 px; no `...`, nothing cut.
- [ ] Empty, locked and error states say what and what next.
- [ ] Every `[term]` resolves in the glossary, linked once per block.
