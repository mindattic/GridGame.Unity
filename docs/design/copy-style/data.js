// Copy and label style guide (GG-T2045). The words here mirror docs/COPY_STYLE.md; edit both together.
window.CopyStyle = {
 "links": [
  { "ticket": "GG-T2044", "name": "Design system", "href": "../design-system/index.html", "md": "../../DESIGN_SYSTEM.md" },
  { "ticket": "GG-T2046", "name": "Icon style and inventory", "href": "../icon-set/index.html", "md": "../../ICONS.md" },
  { "ticket": "GG-T2045", "name": "This guide, as markdown", "href": "../../COPY_STYLE.md" }
 ],
 "ownerRules": [
  ["No ... and no truncation", "No literal ellipsis in any string, no text cut off at a box edge, no word broken mid-syllable to squeeze a box (GG-T1850)."],
  ["Labels must fit", "Every label fits its box at its own size on the narrowest stage. Shrink to fit and marquee are safety nets for Text size, not a plan."],
  ["Perform, Cast or Use", "The commit button names the move type: Perform (skill or weapon move), Cast (spell), Use (item). Never Go, OK or Confirm."],
  ["Recruit and Journey", "The Summon tab becomes Recruit and Campaign becomes Journey (GG-T1964, approved)."],
  ["Mana Shards", "The currency is Mana Shards, singular Mana Shard (GG-T1974). Never Diamonds, Shards alone or Aetherite."],
  ["Mana is the party mana bar", "The one thin bar the whole party shares (GG-LAW-6). Grants are worded in points (+200 mana), never orbs."]
 ],
 "voice": [
  { "do": "You need 200 more Mana Shards.", "dont": "The player needs more Diamonds." },
  { "do": "Your party rests until morning.", "dont": "We rest until morning." },
  { "do": "Mira is down.", "dont": "Unit 3 has been defeated!" },
  { "do": "Mira, flank the frog.", "dont": "Hint: move character to tile (4,2)." },
  { "do": "Wounds carry to the next battle. Set up camp to rest.", "dont": "HP persistence is enabled..." }
 ],
 "voiceRules": [
  "The player is the Commander; the interface calls them you. Second person, present tense.",
  "Never the player, the user or we. Heroes are named, not unit or character.",
  "Commander lines (advice, orders, story beats) are imperative and name the hero; they live in Prose barks, not code.",
  "Plain words over jargon. A term the player must learn is a [glossary] link the first time.",
  "One idea per line: what happened, then what to do next.",
  "No exclamation marks in interface text; combat floats and callouts (Resolve!, CLUTCH!) are the exception."
 ],
 "casing": [
  ["Screen titles, nav tabs, sub-tabs, section headers", "Title Case", "Mana Shard Store, Journey, Summon History"],
  ["Destination buttons (name a place)", "Title Case, To + place", "To Camp, To Journey"],
  ["Action buttons", "Sentence case, verb first", "Set up camp, Next battle, Replay tutorials"],
  ["Body, rows, captions, toasts, confirms, empty states", "Sentence case", "Wounds carry to the next battle."],
  ["Proper names", "As named", "Tithe Moors, Counter Attack"],
  ["Stat abbreviations", "Capitals", "HP, ATK, EVA"]
 ],
 "verbs": [
  ["Perform", "Commit a skill or weapon move", "Battle ability sheet and confirm row"],
  ["Cast", "Commit a spell (spends from the party mana bar)", "Same"],
  ["Use", "Use an item from the bar or the bag", "Same; Items"],
  ["GUARD", "The battle Guard action", "Battle action row (kept as built)"],
  ["To Camp", "Leave results for camp; on the road this sets up camp", "Results footer"],
  ["Navigate", "Pick a destination on the overworld map", "Journey map (BIBLE §28)"],
  ["Set up camp", "Stop on the road and make camp", "Hub mode banner, road prompts"],
  ["Rest", "Sleep in camp until morning", "Camp"],
  ["Recruit", "Hire a mercenary; the tab that holds summons and mercenaries", "Recruit tab"],
  ["Summon", "Pull on a banner", "Recruit tab"],
  ["Ask", "Ask the Commander for advice", "Battle advice card"],
  ["Buy, Sell, Equip, Learn, Teach, Craft", "What they say", "Shops, Heroes, stations"],
  ["Back", "Return to the previous screen", "Header, footer left"],
  ["Close", "Dismiss a sheet or a card", "Sheets, modals"],
  ["Cancel", "Abandon a choice in progress", "Confirm dialogs, aiming"]
 ],
 "verbNotes": [
  "A confirm dialog's yes button repeats the action's verb (Buy, Summon, Set up camp); its no button is Cancel. Never OK, Yes or No.",
  "Cancel or Back sits on the left, the action on the right.",
  "One word per action across screens: To Camp everywhere results lead to camp."
 ],
 "names": [
  ["Premium currency", "Mana Shards (1 Mana Shard)", "Diamonds, Shards, Aetherite", "decided", "GG-T1974"],
  ["Its store", "Mana Shard Store", "Diamond Store, Shard Store", "decided", "GG-T1974"],
  ["Soft currency", "gold, printed 1,250 G", "coins, money", "decided", "as built"],
  ["Summon currency", "Summon Tickets (1 Summon Ticket)", "Tickets on its own", "default", ""],
  ["Battle resource", "mana: the party mana bar, in points", "orbs, MP, per-hero mana", "decided", "GG-LAW-6"],
  ["Road energy pool", "Rested (unchanged for now)", "Energy", "pending", "GG-T1706 proposes Stamina"],
  ["Camp buff", "Well Rested", "Rested on its own", "pending", "follows GG-T1706"],
  ["Hero progression board", "Talents", "Board, Skill Board, Skill Hex", "default", ""],
  ["A hero's moves", "Skills, Spells, Items", "Abilities, as a player word", "default", ""],
  ["Map and stages tab", "Journey", "Campaign, Adventure", "decided", "GG-T1964"],
  ["Gacha and mercenaries tab", "Recruit", "Summon, Summon Circle", "decided", "GG-T1964"],
  ["The player", "the Commander, you", "player, user", "decided", ""]
 ],
 "namesNote": "GG-T1706 (Rested renamed Stamina) waits on the owner. Keep Rested in the game and in new copy until it lands. Internal ids keep their old words (mat_diamond, aetherite_shard, CampMenu.Campaign); only player-facing text follows this table.",
 "numbers": [
  ["Counts", "Digits always", "3 heroes"],
  ["Thousands", "Commas, same on every device (Ui.Count)", "1,250"],
  ["Gold", "Number, space, G", "5,000 G"],
  ["Mana Shards, tickets", "Number then the full name; singular at 1 (ShardText.Of)", "40 Mana Shards, 1 Summon Ticket"],
  ["Mana", "Points, lower-case word", "Costs 300 mana, +200 mana"],
  ["Seconds", "Number, space, s; one decimal in the HUD", "3.7 s, 2 min, 1 h 20 min"],
  ["Percent", "No space; descriptions use chance band words", "30%, [fair]"],
  ["Signed changes", "Sign stuck to the number", "+30% Crit, -2 ATK"],
  ["Stacks", "Multiplication sign, no space", "X-Potion ×15"],
  ["Levels and progress", "Lv, slash or of", "Lv 12, 2/3 cleared, battle 2 of 3"],
  ["Separators", "Middle dot with spaces", "Lv 1 · 5 waves"]
 ],
 "measure": {
  "stage": 1170, "base": 56, "floor": 30, "glyphEm": 0.5, "paragraph": 60,
  "about": "Every in-target shape lays out on a stage at least 1170 design px wide (GameApp.DesignWidth); narrower aspects such as the Galaxy Fold cover grow the stage taller, never narrower. GUI text is one size, at most 56 px at Text size 100% (GuiType.MaxDesign), and a glyph averages half an em, so a character takes about 28 px. Budget = floor(width / 28); the hard cap is the same box at the 30 px shrink floor."
 },
 "roles": [
  { "id": "nav", "name": "Bottom nav tab", "width": 234, "lines": 1, "how": "1170 / 5", "example": "Journey" },
  { "id": "subtab", "name": "Sub-tab, 4 across", "width": 248, "lines": 1, "how": "(1122 - 3 x 16) / 4, minus padding", "example": "Passives" },
  { "id": "foot2", "name": "Footer button, 2 across, with icon", "width": 425, "lines": 1, "how": "(1122 - 16) / 2 - 64 icon - 16 gap - 2 x 24 padding", "example": "Set up camp" },
  { "id": "foot3", "name": "Footer button, 3 across, with icon", "width": 235, "lines": 1, "how": "(1122 - 32) / 3 - 128", "example": "Perform" },
  { "id": "commit", "name": "Battle commit button", "width": 425, "lines": 1, "how": "as a 2-across footer button", "example": "Perform" },
  { "id": "title", "name": "Page title", "width": 826, "lines": 1, "how": "1122 - 2 x 132 header buttons - 2 x 16", "example": "Mana Shard Store" },
  { "id": "rowlabel", "name": "Menu row label beside a value", "width": 447, "lines": 2, "how": "45% of the row after icon and padding", "example": "Replay tutorials" },
  { "id": "rowvalue", "name": "Menu row value", "width": 546, "lines": 1, "how": "at most 55% of the row", "example": "1,000 Mana Shards" },
  { "id": "chip", "name": "Chip or badge", "width": 150, "lines": 1, "how": "Size.Chip height, short pill", "example": "×15" },
  { "id": "float", "name": "Combat float or callout", "width": 420, "lines": 1, "how": "over the board", "example": "Resolve!" },
  { "id": "para", "name": "Toast, confirm body, empty state", "width": 1122, "lines": 2, "how": "full content width, 2 lines; never wider than 60 chars a line (Typography.ParagraphChars)", "example": "No quests yet. Talk to people in town to find one." }
 ],
 "fitRules": [
  "Shorten the words first: a shorter verb, no articles, detail moved into the caption or the InspectCard.",
  "Then let it wrap to a second line (rows, two-line buttons).",
  "Never add ..., never cut, never hyphenate a label under 12 characters.",
  "Long names wrap on word boundaries; code never abbreviates them.",
  "Placeholders ({hero}, {ability}, prices) are measured at their longest real value.",
  "At Text size 115% and 130%, shrink to fit is expected and fine."
 ],
 "states": [
  ["Empty", "What will be here and how to get it, then the action.", "No quests yet. Talk to people in town to find one. [To Journey]", "Empty"],
  ["Locked", "What unlocks it.", "Unlocks after The Tithe Moors III.", "Locked"],
  ["Not enough", "The shortfall and the fix.", "You need 200 more Mana Shards. [Mana Shard Store]", "Insufficient funds!"],
  ["Error", "What happened, in plain words, and what to do. No codes, no blame.", "Couldn't save. Free some space and try again.", "Oops! IOException 0x80070070"],
  ["Disabled", "The reason beside it or in a toast on tap.", "No mana · Recharging: 2 turns", "(a silent grey button)"]
 ],
 "glossary": [
  "Square brackets mark a tap term: brackets hidden, word underlined in Palette.Term, a tap opens its card (GG-T300).",
  "Every [term] resolves to an Id, Term or Aliases entry in glossary.json; ContentLoader.Validate fails otherwise.",
  "Link a term the first time it appears in a block of text, not every time.",
  "Bracket game terms the player has to learn (statuses, keywords, timeline words, chance bands, currencies), not ordinary words.",
  "Bracket the word as it reads; add an alias to the entry rather than rewording the sentence.",
  "No brackets in buttons, tabs, titles or chips; they are taps already.",
  "A renamed concept keeps its old name as an alias until every text is updated."
 ],
 "open": [
  ["Rested renamed Stamina (GG-T1706)", "Keep Rested", "Waiting on the owner; no change until it lands", true],
  ["Talents, Board, Skill Board or Skill Hex", "Talents", "UX review; one name on Status, Abilities and Respec", false],
  ["Abilities as a player word, or Skills / Spells / Items", "Skills, Spells, Items", "Matches the Perform / Cast / Use split", false],
  ["The pull button once the tab is Recruit", "Summon", "The act is still a summon; Recruit names the tab and hiring a mercenary", false],
  ["Summon Tickets once the tab is Recruit", "Summon Tickets", "Never Tickets alone", false],
  ["GUARD in capitals or Guard", "GUARD on the button, Guard in sentences", "As built and taught by the lesson", false],
  ["Make camp or Set up camp", "Set up camp", "The owner's vocabulary; Make camp is drift", false],
  ["Continue on results when not on the road", "Continue only when the next screen is not camp", "To Camp wherever camp is next", false],
  ["Ellipsis under Reduce Motion (BIBLE §26.6)", "No ellipsis: shrink to the floor, then wrap", "Owner rule overrides the old marquee fallback", false]
 ],
 "drift": [
  ["BattleScreen.cs confirm row, BattleScreen.Tutorial.cs coach step", "Go", "Perform"],
  ["Bottom nav and screen titles", "Summon, Campaign", "Recruit, Journey (GG-T1964)"],
  ["CampMenu Rest row", "Make camp", "Set up camp"],
  ["Heroes Abilities sub-tab, Status column, party board button", "Board", "Talents"],
  ["Marquee under Reduce Motion", "ellipsis", "shrink, then wrap"]
 ],
 "checklist": [
  "Second person, sentence case body, Title Case title, verb-first button.",
  "The verb is from the vocabulary and the noun from the names table.",
  "Numbers follow the numbers table.",
  "Fits the role's budget at 56 px; no ..., nothing cut.",
  "Empty, locked and error states say what and what next.",
  "Every [term] resolves in the glossary, linked once per block."
 ]
};
