# GridGame UX review: why the owner can't tell what's happening (GG-T1940)

Reviewer: UI/UX (mobile games). Date: 2026-11-28. Build reviewed: `main` at 1231e5e0, the GG-T1857 recapture.

## Inputs and limits

- **Screenshots:** every PNG under `docs/design/ui-audit/captures/iphone17pro/`.
  - The `pixel9/` and `ipad/` folders aren't in the working tree, and this session had no shell to pull them from LFS. Narrow-width and tablet states are therefore reviewed against BIBLE §9.0 and §26.3: every stage from 9:23 to 9:16 uses the same rows, and anything wider is framed at 9:16.
  - Re-check them once those captures land.
- **Captures that show the wrong screen:**
  - `camp.png` is the hub on a fresh save.
  - `overworld.png` is a second copy of the hub, because the overworld (§28) isn't built.
- **Documents:**
  - `docs/BIBLE.md`: §1-3, §2.0-2.6, §2.13, §9.0-9.2, §11.2-11.3, §25.0, §28.
  - The owner-approved plan, `plans/abilities-screen-and-info.md`.
  - `docs/design/ui-audit/findings-hub.json` and `findings-shops.json`.
  - `docs/research/MobileGameAccessibility.md`. Rule ids such as L1, X4 and P1 refer to its checklist.
- **Wireframes:** I had no way to run them through `MindAttic.Helpers.Ascii.AsciiAligner` here. They are drawn left-anchored, with fixed-width cells and no right-hand borders, so no column depends on hand alignment. Run them through the aligner before they go into the BIBLE.
- **Already filed:** issues with an open ticket (GG-T1845 to GG-T1871) are cited by number, not filed again.

---

## 0. The verdict in one paragraph

The battle screen is built around the wrong centre of gravity. GridGame's only weapon is **position**: you trap a foe between two heroes, and the trap does the damage (LAW-1, §2). But the biggest, brightest, most thumb-friendly thing on screen is a row of five ability buttons (`captures/iphone17pro/battle.png`). Every mobile RPG has taught players that this row means "tap to attack", so a new player reads the screen as a menu RPG. They tap icons, nothing much happens, and they never discover the sandwich.

Meanwhile, the screen never answers the three questions every turn-based screen must answer:

1. **Whose turn is it?** The far-left diamond shows an *enemy's* face with a red halo.
2. **How long do I have?** The clock is frozen until you lift a hero, and nothing says so.
3. **What should I try?** The pincer preview only appears *while you are dragging*, which is exactly when the clock is running.

Outside battle, the hub doesn't make its one important action (Continue) look important. It also breaks the owner's corner rule, and the onboarding front-loads 18 lessons before the player gets one free, real fight.

The fix order: make the trap visible before the clock runs, make the turn state explicit, then move the abilities behind one button (the plan the owner already approved).

---

## 1. Core comprehension problems, ranked by impact

### 1.1 The battle loop as a new player actually meets it

Here are the first 60 seconds of `battle.png`, narrated as a first-time player who has skipped or forgotten the drills.

1. **0 s.** My eye lands top-left, the reading start. A frog's face in a red-glowing diamond, with "3.7s" under it. *"It's the frog's turn, and something happens in 3.7 seconds."* I wait. Nothing happens, because the clock is frozen until I lift a hero (§2.0, "looking is free"). I'm already confused.
2. **5 s.** Green text: "3.7s · move or skill". *"Move or skill... what? Is that a choice I have to make?"* It actually means "the gap is long enough for either", and amber would mean "move only".
3. **8 s.** At the bottom are five big glowing squares, two of them identical gold sunbursts. That's where my thumb is, so I tap one. The panel says something; maybe I aim at a frog and Cast. A number pops. *"OK, it's a menu RPG."*
4. **15 s.** I tap another icon. "Already acted" (each hero acts once per window, §2.0). *"Why? I only did one thing."* The token is greyed with a ✓, but I didn't notice.
5. **20 s.** I drag the Knight because the panel says "drag Knight to move". The frog icon starts sliding left, because the clock is now running in real time. I drop next to the frog. No damage, since movement never deals damage (LAW-1). *"Moving is pointless."*
6. **25 s.** The frog acts. Portrait slide, red tiles, a hit. Then a cluster of empty diamonds at the right end of the timeline (the recovery tray) shuffles. *"What are those?"*
7. **30-60 s.** I try GUARD, the button at the top right, outside comfortable thumb reach (L1). The turn passes and "Mana +N" flashes. *"So GUARD is 'end turn'? Then why not say so?"*

**What I never learn:** that standing on *either side* of a foe with a second hero is the attack. The game only tells me in a drill lesson (or the 5-second idle hint) and in the pincer outline that draws *during* a drag. Everything on screen points me at the buttons instead.

### 1.2 What each element on the battle screen is for

The numbers in the ❌ notes refer to the ranked problems in section 1.4.

| Element (`battle.png`) | What it actually is | What a new player thinks | Verdict |
|---|---|---|---|
| `[≡]` top-left | Pause menu (Resume, Battle log, Settings, Retreat) | Menu | ✅ Correct, and it follows the owner rule |
| "The Arkgrave I · Wave 1/1" | Stage and wave | Fine, but where is the goal? | 🟡 No objective (G4) |
| "1550 G" top-right | Gold earned this battle | My gold | ✅ |
| Prominence diamond (frog, red halo, "3.7s") | Next actor and the seconds until it acts | "It's the frog's turn now" | ❌ #2: occupies "whose turn" real estate with the enemy |
| Track with diamonds | Seconds until each icon acts (10 s span) | Decoration or a progress bar | 🟡 Unlabelled |
| Green fill plus "3.7s · move or skill" | The party's window (the gap) | Jargon | ❌ #2: the most important fact on the screen, drawn as the faintest |
| Diamond cluster next to GUARD | Foes recovering, waiting off the bar | Empty, broken icons | ❌ They read as blanks; label or hide them |
| GUARD (top-right) | End the window: Protection for all heroes, unused seconds to mana | Unknown, maybe "defend" | ❌ #8: out of thumb reach, and the meaning isn't stated |
| Red-bordered tokens | Foes | Units | 🟡 Side is told by a 3 px border colour only (C1) |
| Gold-bordered red frog | The next actor's tile | "Selected?" "Special?" | ❌ Gold also means "selected" elsewhere; there is no legend |
| White-bordered Knight | Selected hero | OK | 🟡 |
| Blue-bordered tokens | Heroes | OK | 🟡 Same token shape as the foes |
| Two green bars under each hero | HP bar, plus the gear durability strip (`BoardView.cs` `Gear`, weapon and armor wear) | "Two HP bars?" | ❌ #5: durability doesn't belong on the battle board |
| Five ability squares | The selected hero's kit, icon-only (§9.0) | "Attack buttons" | ❌ #1, #6: they dominate the thumb zone and teach the wrong verb |
| Two identical gold sunbursts | Two different skills (Sacred Challenge and Judgment of Warding, see `abilities.png`) | The same thing twice | ❌ #6 |
| Pouch "5" | Consumable stack (X-Potion) | OK | 🟡 Mixed into the same row as the skills |
| Yellow panel text | Three instructions in one line | A wall of text | ❌ #7: the core verb comes last |
| Panel footer "Knight · TANK · Lv 10" | Selected hero | OK | ✅ |
| "Development Build" | Unity watermark | Noise | Ignore (release builds drop it) |

### 1.3 What competes for attention (in order of visual weight in `battle.png`)

1. **The ability bar:** five saturated pixel icons on bright green frames, the largest coloured mass on screen.
2. **The background art:** a high-contrast wreck silhouette and dead trees running through the board. The 6 x 8 grid lines are nearly invisible against it, so the board reads as "art with stickers" rather than "a grid where position matters".
3. **The prominence diamond's red halo,** the hottest colour on screen, attached to an enemy.
4. **The yellow panel text.**
5. **The units themselves.** They are small portrait squares, and they come *fifth*. In a positional game they should come first.

The empty top three board rows are fine (they are drag space). But the art is busiest exactly there, which pulls the eye away from where the fight is.

### 1.4 The ranked problems

Impact means how much of "I don't get what's happening" each one causes. The evidence and the fix are given per problem.

**#1. The screen teaches the wrong verb. (Critical)**
- **Evidence:**
  - The five slots fill the thumb zone (`battle.png`, `battle-boss.png`).
  - Damage comes only from pincers (LAW-1, §1.2).
  - The pincer outline appears only *during* a drag (§9, "the drag preview shows the same outline"), and a drag runs the real-time clock (§2.0).
  - So the one preview that explains the game is shown only under time pressure.
- **Fix:**
  - **Pincer spots.** When a hero is selected (and the clock is frozen), mark every tile that hero can reach this window where dropping would trap a foe with an ally. Draw a gold crossed-swords marker on the tile and a faint outline round the foe it would catch. This is the BIBLE's idle hint (§9.2.1, `HeroBot`), shown at once and for *every* option, not after 5 s and only for the bot's pick.
  - Tapping a spot previews the outcome in the panel (who strikes, roughly how much damage, the pushback seconds). This is E5.
  - Then move the abilities behind the single **Abilities** button (plan, slice C).

**#2. "Whose turn is it, and is the clock running?" has no answer on screen. (Critical)**
- **Evidence:**
  - The far-left diamond is the *next enemy*.
  - Under PartyWindow, the default mode (§2.13.0), heroes have no timeline icons at all.
  - The party's window is a thin green fill plus "move or skill".
  - The frozen clock ("looking is free") has no visual state.
  - "N heroes can still act · 2.3s left" exists only as a transient banner (§2.0 "Seen").
- **Fix:**
  - **A turn strip:** one line between the top bar and the timeline. It reads `YOUR TURN · Knight, Cleric, Red Mage can act · the clock waits until you lift a hero`. It turns into `ENEMY TURN · Red Frog` during playback, and into `TOO LITTLE TIME · press GUARD` on a forced Guard.
  - **A window lane:** redraw the gap as a labelled "YOU" block running from the left edge to the next foe's icon, with a small face chip for each hero who can still act. A chip greys out once that hero has acted.
  - Show the next foe where it actually sits, at the *end* of the lane, not in the far-left "whose turn" slot. The far-left slot shows "YOU" during the party window and the enemy only while the enemy is acting ("NOW").

**#3. No goal, no stakes, no boss. (High)**
- **Evidence:**
  - "Wave 1/1" is the only objective.
  - `battle-boss.png` ("Tithe Moors III") has no boss marker, boss bar or name. The gold-ringed armoured unit could be anything.
  - Wave counts change between stages (1 to 5), and there is no foe count.
- **Fix:**
  - Put the goal in the top bar's centre: `Wave 2/3 · Defeat every foe`, or a stage-specific objective.
  - For a boss: a thin named HP bar under the top bar, a purple base plate (§2.6 already reserves purple for boss icons) and a "BOSS" tag on its diamond.
  - When a wave begins, show the billboard line "Wave 2 of 3: 3 foes".

**#4. Enemy intent is invisible until the blow lands. (High)**
- **Evidence:** a foe's turn plays intent, then wind-up, then the act (§9.1, GG-T839), all *during* its turn. During the party's window you can't see who the next foe will hit, so there is nothing to plan against. The BIBLE names "telegraphing a physical attack's target" as a candidate follow-up (§2).
- **Fix:** during the party window, draw a thin red arrow from the next actor to the tile it will strike, plus a small damage badge. Show intent for the next two actors at most. Intent is the single biggest legibility tool in Into the Breach and Slay the Spire, and here it also explains why a pincer's pushback matters ("push it back and its attack lands after your Cleric acts").

**#5. Hero, foe, selected and next-actor are told apart only by thin border colours. (High)**
- **Evidence:**
  - Every token is the same rounded portrait square.
  - Side is a 3 px red or blue border.
  - Gold means "next actor" on the board but "selected" in menus (`abilities.png` roster).
  - There are two green bars under each hero: HP, plus gear durability.
  - Grey rivet plates hang under armoured foes (`battle-boss.png`).
  - This fails C1 (colour alone) and costs a constant second of decoding on every glance.
- **Fix:**
  - Give each side a **base plate**: heroes stand on a blue rounded plinth, foes on a red one with a notched corner. That is shape plus colour.
  - Selected: a lifted token with a white ring and a soft shadow, the depressed or raised state the owner asked for.
  - Next actor: a pulsing ring in its timeline colour, never gold.
  - Remove the durability strip from the board entirely. Durability belongs in Equip and on the results screen. Keep only the last-point "cracked" badge.
  - Make the armour plate a single steel chevron on the token's armoured side, not a separate bar.

**#6. The ability bar can't be read. (High, and mostly covered by the approved plan)**
- **Evidence:**
  - Icon-only slots.
  - Two identical sunburst icons for different skills (`battle.png` slots 2 and 5; the same icons with names in `abilities.png`).
  - The consumable sits among the skills.
  - No mana is visible anywhere on the battle screen.
  - Readiness marks (sweep, fill bar) aren't visible in either capture.
- **Fix:**
  - Use the plan's `[Abilities] [Info] [GUARD]` row, the full-screen sheet and the thin party mana bar.
  - Add one rule the plan doesn't state: **every ability gets a unique icon.** Add a content validation check that no two abilities in one hero's kit share an icon.

**#7. The panel says three things at once and gets clipped. (Medium)**
- **Evidence:**
  - "Knight: tap a skill to aim it · hold one to read it · drag Knight to move" (`battle.png`).
  - In `battle-boss.png` an extra tip line pushes it under the scroll edge, so the instruction is cut off.
  - The core verb, drag, comes last.
- **Fix:** give one instruction, verb first, matched to state:

  | State | Panel line |
  |---|---|
  | No hero selected | "Tap a hero." |
  | Hero selected, spots exist | "Drag Knight to a ⚔ tile to trap a foe." |
  | Hero selected, no spots | "No trap this turn. Move to set one up, or open Abilities." |
  | Aiming | "Choose a target." |
  | Enemy turn | "Red Frog attacks Knight." |

  Move tips ("tap or hold anything to read it") to the Info button, which is the point of the plan's Info button.

**#8. GUARD is out of reach and doesn't say what it does. (Medium; the plan moves it)**
- **Evidence:**
  - GUARD sits at y about 150-333 of 2532, the top 13% of the screen (accessibility checklist L1, marked ❌).
  - It does three things: ends the window, protects every hero, and banks the seconds left into mana (§2.5).
  - The label says one word.
- **Fix:**
  - Move it to the action row (plan).
  - Label it `GUARD` with a smaller second line, `end turn`.
  - On press, the billboard says "Guard: +12 mana, heroes protected".
  - When some heroes haven't acted yet, ask once per battle: "Cleric and Red Mage haven't acted. End the turn anyway?" Offer a "don't ask again" box.

**#9. Too many systems reach the player too early. (High, structural)**
- **Evidence:**
  - The drills teach 18 lessons across 5 drills before the first real stage (§9, GG-T1315). They cover moving, tapping to read, pincers, shoving, chains, supporters, mana, spells, buffs, the hold timer, the timeline, Guard, interrupting, taunt, pickups, armor plates and more.
  - Each lesson opens with a blocking popup.
  - On top of that the player meets cast times, stagger, Clutch, combo ranks, Rally, forced Guard, region rules, hazards, tactical tiles, heavy foes, four turn modes and a difficulty scale of 1 to 10.
  - **Settings puts "Turn mode" as the very first gameplay row** (`settings.png`).
- **Fix:** see section 4. Three short drills, seven lessons, and everything else taught just in time in real stages. Turn mode leaves the player-facing Settings until the owner has picked a winner (GG-T821).

**#10. Outside battle, nothing says "do this next". (Medium)**
- **Evidence:**
  - On the hub, the Continue button looks like every other panel (`hub.png`).
  - Gold is buried in "5,000 G gold · 0:00:10 · Cleared 11/40" (GG-T1845).
  - "On the road · no mana carried", "Rested", "Unlimited" and "Make camp" are unexplained jargon.
  - The page scrolls and clips its last row (GG-T1846).
  - Seven currencies appear across the headers: G, Diamonds, Tickets, Summon Tickets, Shards, Mana Shards and Rested. The Summon header says "0 Tickets" while its body says "Summon Tickets: 10" (`summon.png`).
- **Fix:** see the hub verdict (section 2.2) and the IA proposal (section 3).

### 1.5 The loop in one sentence, for the game to say out loud

> **"Your turn lasts until the next enemy reaches the left. Every hero acts once. Trap a foe between two heroes to hit it, and the trap pushes it back, which buys your team more time."**

Put this line on the first drill's first card. Put it at the top of Settings › How to play. Show it in the Info button's empty state. Every element on the battle screen should map to a word in it:

| Word in the sentence | Element on screen |
|---|---|
| turn | Turn strip |
| next enemy reaches the left | Timeline |
| every hero acts once | Face chips in the lane |
| trap | Pincer spots |
| pushes it back | Lane growing after a pincer |

---

## 2. Per-screen verdicts

Verdict key:
- **Keep:** fine.
- **Fix:** local fixes.
- **Rework:** the arrangement itself changes.

### 2.1 Battle: Rework

Capture: `captures/iphone17pro/battle.png`, `battle-boss.png`.

**Recommended arrangement, top to bottom.** It extends the approved plan; it doesn't replace it.

1. **Top bar** (unchanged): `[≡]`, stage and goal, gold.
2. **Turn strip** (new, one line, about 60 px): turn state and who can act.
3. **Timeline:** the far-left slot is "YOU" or "NOW", plus a labelled window lane with hero face chips. The next foe sits at the lane's end with its intent icon. The recovery tray is collapsed to a small "+2 resting" chip. GUARD leaves this row.
4. **Board, 6 x 8:**
   - base plates by side;
   - pincer spots for the selected hero;
   - an intent arrow for the next one or two foes;
   - the art behind the board dimmed by about 35%, with grid lines raised to 3:1 (X6).
5. **Panel:** the selected hero's thumbnail, name, role and HP, then one instruction line. While aiming, the five-question ability card and the confirm card take the panel (§9.0, unchanged).
6. **Party mana bar:** thin, full width (plan, GG-T1915).
7. **Action row**, in the thumb zone: `[ Abilities ]  [ Info ]  [ GUARD / end turn ]` (plan).

Proposed wireframe (iPhone 17 Pro, mid-turn, Knight selected):

```
| [≡]   Arkgrave I · Wave 2/3 · Defeat every foe          1,550 G
| YOUR TURN · Knight, Cleric, Red Mage can act · clock waits
| [YOU]==== K C M ==== 3.7s ==>[Frog: hits Knight]--◇----◇---- +2 resting
|
|  .   .   .   .   .   .
|  .   .   .   .   .   .
|  .  <B>  .   .   .   .
| (F) (P) [!]  .   .   .
|  .   ⚔   .   .   .   .
|  .   .   .   .   .   .
|  .  <K>  .   .   .  <M>
|  .   .  <C>  .   .   .
|
| [Knight] TANK · 307/307
| Drag Knight to ⚔ to trap the Plant with Barbarian.
| ======== party mana 240 / 300 ========
| [ Abilities ]      [ Info ]      [ GUARD  end turn ]
```

Key:
- `<X>` is a hero on a blue plinth. B, K, C and M are Barbarian, Knight, Cleric and Red Mage.
- `(X)` is a foe on a red plinth. F is a frog, P is the plant.
- `[!]` is the next actor, with its intent arrow drawn on the board.
- `⚔` is a pincer spot for the selected hero.

**States:**

| State | What the screen shows |
|---|---|
| Battle start | Billboard "Wave 1 of 3: 3 foes". Turn strip `YOUR TURN · all 4 heroes can act`. No hero selected; the panel says "Tap a hero." |
| Hero selected | Pincer spots appear, the token lifts, the panel shows the instruction. |
| No pincer available | Spots are absent. The panel says "No trap this turn. Move to set one up, or open Abilities." The one best setup tile shows as a hollow marker. |
| Dragging | The turn strip turns amber: `CLOCK RUNNING · 2.1s`. The lane drains in real time. The pincer outline previews on hover (as built). |
| Aiming an ability | The board dims except legal targets (plan). The panel shows the prompt, then the confirm card. The action row becomes `[Cancel] [Cast]`, select then confirm, with the Cast button depressed on press. |
| Enemy turn (playback) | The turn strip reads `ENEMY TURN · Red Frog`, the far-left slot `NOW`. The action row is disabled but visible, at 50% and not hidden, so nothing jumps. |
| Too little time (forced Guard) | The turn strip goes red: `TOO LITTLE TIME · press GUARD (0.4s gap, a move needs 0.85s)`. GUARD pulses. The lane shows the sliver in red. |
| A hero has acted | Their chip in the lane greys out and their token shows ✓ (as built). The strip's name list updates. |
| Boss | A named boss bar under the top bar, a purple plinth, "BOSS" on its diamond. |
| Too much data (6+ foes, casts in flight) | The lane keeps at most four diamonds visible. Overflow collapses to "+3". Intent arrows are limited to the next two actors. Cast diamonds keep their teal or amber borders (§2.6). |
| Victory or defeat | A full-width billboard, then the results screen. Defeat offers Retry and lists *what killed you*, read from the last three Log lines. |
| Error (refused command) | The refusal line sits atop the panel (as built, GG-T1824) and is mirrored in the turn strip for 1.6 s. |
| Narrow (9:23) and tall | The turn strip and the action row are fixed. The panel absorbs any extra height (as §9.0). On the shortest stage the panel keeps at least two lines. |
| Tablet (2:3, 3:4) | Framed at 9:16 per §26.3, so the layout is unchanged. |
| Reduce Motion | Spots and arrows are static. The lane doesn't animate while frozen. |

**Contrast:**
- The yellow panel text on the dark panel passes.
- The green "move or skill" text over board art fails X7: there's no plate under it. The turn strip fixes this by sitting on a solid band.
- Grid lines on `battle.png` are well under 3:1 (X6).
- Every theme (Manapunk default, Classic, Dark and the rest) must keep the plinth colours at 3:1 against the board.

### 2.2 Hub (Camp root): Rework

Capture: `captures/iphone17pro/hub.png`, `camp.png` (fresh save), `overworld.png` (duplicate).

**What's wrong:**
- **Too low:** the party window takes the top 40% of the screen and is informational. The primary action (Continue) sits mid-screen in the same flat style as every card.
- **No header:** there's no hamburger and no gold in the corners (GG-T1845). The page scrolls and clips Settings and Save & Quit to bare icons (GG-T1846).
- **Icons:** they repeat (GG-T1847).
- **Status jargon:** "Rest" and "Make camp" sit on one row. One is an icon and a word, the other is plain right-aligned text that doesn't look tappable. "On the road · no mana carried", "Rested ... Unlimited" with a full green bar, and "Crucible: Closed on the road" all describe one hidden road-or-camp mode without ever naming it.
- **Fresh-save contrast:** on a fresh save (`camp.png`), the locked tiles' hint text overlaps the next tile and fails contrast ("Opens after The Lantern Road II" runs into "Merchant Joins after The Tithe Moors I").
- **Duplication:** the hub root shows Heroes, Summon Circle, Merchant and Shard Store as tiles, but the bottom nav on every sub-page already has Heroes, Summon and Shop.

**Recommended arrangement:**

```
| [≡]   Camp · The Arkgrave                         5,000 G
| [Kn 307] [Ba 197] [Cl 187] [RM 187]       (thumbnails, HP rings)
| ON THE ROAD · battle 2 of 3 · wounds carry   [ Make camp ]
|
| NEXT  The Arkgrave III · Recommended Lv 12
| [ region art strip: swamp, frogs, the Hag ]
| [            Fight  >            ]   (primary, filled)
| Stage select >                Crucible (opens after Shardfall I)
|
| [ Craft ]   [ Journal  2 new ]   [ Heroes  new skill ]
|
| Home | Journey | Heroes | Recruit | Shop
```

**Rules:**
- **Header:** `[≡]` top-left opens Settings, Profile, How to play, About and Save & Quit. Gold sits top-right. Zero-value currencies are hidden.
- **Party strip:** a single row of four thumbnails with HP rings. Tapping a thumbnail opens Status. Full art appears only on tap, per the owner rule.
- **Mode banner:** one line that names the mode in plain words and carries its one action. On the road it reads "battle 2 of 3 · wounds carry" with a `[Make camp]` button. Once camped it reads "Camped · Inn and Respec open" and the action becomes `[Rest]`. Drop the "Rested · Unlimited" bar from the hub. Its only meaning is a stamina gate, so show it on the Fight button's caption when it applies ("Costs 1 Stamina").
- **Primary card:** it sits in the lower-middle, in the thumb zone. It is the only filled button on the screen, and it says what you'll fight.
- **Tiles:** only the things the nav doesn't have (Craft, Journal, plus Heroes with its badge as a nudge). Shard Store leaves the hub root and moves into Shop.
- **Bottom nav:** it shows on the hub root too, with Home lit (the BIBLE says so, §11.3; the capture shows no nav on the root).

**States:**

| State | Hub shows |
|---|---|
| Fresh save | One hero row, the NEXT card for Tithe Moors I and two tiles. Locked items appear as one line, "More of the camp opens as you travel", not three ghost tiles. |
| Wounded party | Low-HP rings turn amber or red. The mode banner reads "2 heroes wounded · Make camp to heal". |
| Everything open | At most 3 tiles plus the nav. Nothing scrolls on a 9:19.5 phone. |
| Badges overflow | One dot per tile, never stacked text ("New ability ready" overflows today, GG-T1860). |
| Campaign finished | The NEXT card becomes "New Game+ · Level 1" with Fight. |

### 2.3 Every other screen

| Screen | Capture | Verdict | What's wrong | Recommended arrangement |
|---|---|---|---|---|
| Splash | `splash.png` | Keep | Mid-fade capture only | Tap to skip (as built) |
| Create Profile | `createprofile.png` | Fix | A 3-step wizard before the player has seen the game. The disabled Next is 1.44:1 (GG-T1852). | One step: the Commander's name, prefilled with a suggestion. Ask the look and pronouns later, at the Founding scene. |
| Main Menu | `title.png` | Fix | **Hand cursor** beside the focused row (owner rule: none on mobile). The wordmark is thin and small. "Profile ... Scenario" means nothing to a player. | Drop the hand. Continue card on top when a save exists (as specced). Profile row reads "Profile: Scenario". The menu sits in the lower half, in thumb reach. |
| Profiles | `profiles.png` | Fix | Hand cursor. Rename and Delete float apart from the row. "+ New profile" has two pluses (GG-T1851). | As GG-T1851. |
| Start Campaign (slots) | `newgame.png` | Keep | Slots now sit low (GG-T1853 done) | "More options" should say what's inside: "Turn mode, difficulty". |
| Load | `load.png` | Keep | Fine | Add a last-played line per slot when one is used. |
| Stage select | `stages.png` | Fix | Hand cursor. Rows about 180 px apart for 4 regions, so most of the panel is empty. No art and no recommended level. "28 more stages ahead…" uses an ellipsis (GG-T1850). | Region cards with an art strip, "2/3 cleared", "Rec. Lv 12" and the next stage's name. The current region is expanded to show its stages. When the overworld lands, this screen becomes the map (§28). |
| Results | `results.png`, `results-mid.png` | Rework | "VICTORY" is a small thin word in the top-left, so the win has no moment. The reward rows are tiny type, but the "Teach to" buttons are huge and interrupt the list. Labels drift between screens ("Continue" here, "To Camp" there). The important rule, "Wounds carry to the next battle", is buried mid-list. | A big centred VICTORY with a short fanfare. One summary row: gold, XP, best combo. A "Carried to next battle" box (HP, mana). Loot as a grid of icons. A Tome becomes a single "Teach Counter Attack to..." card with hero thumbnails. Footer: `[To Camp]` on the left, the primary `[Next battle]` on the right. |
| Heroes · Status | `status.png` | Fix | The 4-hero roster takes 2 rows (about 22% of the screen) on every Heroes tab. The stat table overflows (GG-T1861). Full art shows without a tap. | Roster as one row of thumbnails. Show the art as a thumbnail and open it full screen on tap (owner rule). Table columns are sized to their content. |
| Heroes · Equip | `equip.png` | Fix | Hand cursor. The slot list window shows only 2 slots, so most slots need scrolling. The Knight holding "Hunter's Bow" looks like a fixture error. | Slots as a 2 x 4 grid of icons (paper-doll style). Tapping a slot opens the bag sheet. Add an "Auto-equip best" button (checklist E2). |
| Heroes · Abilities | `abilities.png` | Rework | Five levels of nesting: header, hero tabs, roster, battle bar, then Known/Items/Passives/Locked/Board sub-tabs, which leaves about one visible row of content ("Clear slot"). Words break mid-syllable ("Chal-lenge", "Judg-ment of Ward-ing", "Knight-'s Oath"), which is the owner's no-truncation rule in another form. The cells are mixed sizes. | Wait for the plan: the battle bar goes away with slice C, and this tab becomes "Skills": the hero's `ActionTree` (pins, categories) as a list with full names, plus a Learn section. Talents (Board) gets its own tab. |
| Heroes · Talents (Skill Hex) | `skill-hex.png`, `talents.png` | Fix | The hex is clipped at the bottom, showing only the top half. Its nodes are about 20 px, far under the 132 px tap floor (T1). The copy says "Hover or tap", but there is no hover on a phone. The tab is named "Board" here and "Talents" elsewhere. The Respec page says Knight has "No board nodes bought" while this page says Knight owns 16 nodes. | Full-height pan view (PanZoomView, as on the map). Tap a node to select it, then Confirm to buy (select then confirm, depressed state). One name everywhere: "Talents". Fix the data mismatch. |
| Heroes · Party | `party.png` | Rework | Order is backwards: unreadable hex thumbnails first, then a 4 x 4 cohesion matrix of zeros, and the real task ("Active party 4/6, tap to swap") last and scrolled. | The active party (4 slots) first, then the reserve list, using select then confirm to swap. Cohesion becomes a single line per pair ("Knight + Cleric: Trust 2"), shown only once any pair is above 0. Drop the hex thumbnails. |
| Items | `items.png` | Fix | Hand cursor. Rows about 190 px apart, so 3 items fill the screen. The tabs read "All / Items" inside a screen called Items. It lights the Home tab. | 132 px rows. Tabs: All, Consumables, Gear, Materials. The detail and Use-on-hero area is fine. |
| Shop (Merchant) | `shop.png` | Fix | Two tab rows (Buy/Sell/Bargain over 5 categories). The header is crowded (GG-T1858). The grey stock note fails contrast (GG-T1864). | Category tabs scroll sideways. Buy/Sell becomes a segmented toggle in the header band. The steppers and the Pay/Buy footer are good (select then confirm). |
| Shard Store | `diamonds.png` | Fix | Named "Diamonds", "Shards" and "Mana Shards" across three screens. Its footer button sits **under the bottom nav** (a half-hidden "...hards ... G" behind Home and Campaign). The stepper wraps (GG-T1869). | One name. Pin the footer above the nav. Move it into Shop as a tab. |
| Summon | `summon.png` | Fix | Big empty bands. The header says "0 Tickets" while the body says "Summon Tickets: 10". There is a pity bar ("Within 10 pulls") although BIBLE §3 says "still no pity system", so ask the owner which is right. | Hero art or a circle visual at the top. Rates and History as small links. The x1 and x10 buttons as a footer pair. One ticket count, in the header. |
| Summon history | `summon-history.png` | Fix | **Gold clipped at the right edge** ("5,000 G" cut off), which is truncation. The empty state is fine. | Shared header fix (GG-T1858). |
| Mercenaries | `mercenaries.png` | Fix | Every hire shows 3 to 4 price buttons stacked, so the list is very long. | One row per mercenary: thumbnail, role, a short line. Tapping it opens a sheet with the 3 durations, Haggle and a Hire confirm. |
| Blacksmith | `blacksmith.png` | Fix | Opens on Repair even when nothing needs it, leaving a giant empty panel. The header XP line overflows (GG-T1859). | Open on Forge unless something is worn. Put the rank line in the body. |
| Alchemist | `alchemist.png` | Fix | You can't tell brewable from unbrewable recipes at a glance (all grey). Green fails contrast (GG-T1863). | Brewable recipes first with a "Brew" chip, the rest under "Need ingredients". |
| Enchantress | `enchantress.png` | Fix | Hand cursor. Three stacked scroll boxes on one page. | Two steps: pick the gear, then pick the enchant (a sheet), with the Needs list and the Enchant button. |
| Caravan | `caravan.png` | Fix | Lore printed inline makes the page 2.5 screens long (GG-T1868). The Codex toast covers a row. | As GG-T1868. Toasts sit above the nav, never over content. |
| Talk | `talk.png` | Fix | Hand cursor. Small grey locked rows (Trust 1/3/5 needed) dominate. | One row per person with a "New" dot. Their locked talks go inside their card. |
| Quests | `quests.png` | Fix | **Dead ends:** every row says "Talk to them" with no button. | Each quest row gets a `[Talk]` action that opens that NPC's dialogue. |
| Bounties | `bounties.png` | Keep | Good structure. Skull glyph size (GG-T1854). | Add "Go to stage" next to "Take". |
| Bestiary | `bestiary.png` | Fix | A wall of 167 black silhouettes, "Seen 0/167". It's demotivating, and it says 0 on a save that has fought many foes (check the fixture). | Group by region with "3/12 met" headers. Show unmet foes as a "?" count, not silhouettes. |
| Crucible | `crucible.png` | Fix | The foe list is a 9-line paragraph. "Closed on the road" doesn't say how to open it. | Put the rules in an Info card. Footer: "Make camp to enter" plus the button. |
| Inn · Respec | `respec.png` | Fix | Body text at 3.08:1 (GG-T1862). Data disagrees with the Talents page. | As GG-T1862. Show the refund per hero. |
| Settings | `settings.png` | Fix | **Turn mode is the first gameplay row**, an experimental system shown to every player. No accessibility section first. | Order: Accessibility (text size, Reduce Motion, colorblind, untimed turns), then Gameplay (difficulty, battle speed, confirm before acting), Audio, Graphics. Turn mode moves under an "Experimental" fold until GG-T821 decides. |
| Windows | `windows.png` | Rework | A developer theme editor ("Sample text: the quick brown fox") reachable from the player's menus. | Dev builds only. Players get a single "Menu theme" picker in Settings. |
| Credits, About | (captures) | Keep | | |
| Overworld | `overworld.png` (shows the hub) | n/a | Not built (§28, proposed) | See section 3. |

**Hand cursor:** it appears on `title.png`, `stages.png`, `equip.png`, `items.png`, `talk.png`, `profiles.png` and `enchantress.png`. One kit-level fix removes it everywhere: replace it with a filled row highlight and a 4 px left accent bar.

---

## 3. Navigation and information architecture

### 3.1 Current (as captured)

```
Splash
 └ Create Profile (3 steps, first launch) ─ Main Menu
     ├ Continue ─ Hub
     ├ Start Campaign ─ slots ─ team pick ─ "New to GridGame?" ─ crawl ─ Founding ─ Drill 1..5 ─ Hub
     ├ Load ─ slots ─ Hub
     └ Profile · Settings · About · Credits

Hub root (no header, no nav in capture, scrolls)
 ├ Party window (4 cards) ─ Status
 │   ├ Rest
 │   └ Make camp
 ├ Continue ─ Battle ─ Results ─ Next battle | To Camp
 ├ Adventure ─ Stage select
 ├ Crucible
 ├ Tiles
 │   ├ Heroes ─ Hero screen
 │   │   ├ Status
 │   │   ├ Equip
 │   │   ├ Abilities
 │   │   │   └ Known | Items | Passives | Locked | Board
 │   │   └ Party
 │   ├ Summon Circle
 │   │   ├ Standard
 │   │   └ Recruit
 │   ├ Merchant ─ Buy | Sell | Bargain
 │   ├ Shard Store
 │   ├ Craft ─ Items · Blacksmith · Alchemist · Enchantress · Manasmith · Caravan
 │   ├ Journal ─ Talk · Quests · Bounties · Mercenaries · Codex · Bestiary
 │   ├ Settings (includes Windows)
 │   └ Save & Quit
 └ (Inn ─ Respec, inside the party window when the Inn has joined)

Bottom nav on sub-pages: Home | Campaign | Heroes | Summon | Shop
```

**Problems in the current IA:**
1. **Three routes to the same place:**
   - Heroes, Summon and Shop are each reachable from a hub tile, the bottom nav, and sometimes a header link.
   - Respec lives in two places: the Inn page and the per-hero Talents.
   - Talk appears three times: the Journal tile, the Talk button in every station header, and Quests' "talk to them" text.
2. **Groups that don't match the player's goal:**
   - Mercenaries sits in Journal but lights Summon.
   - Items sits in Craft but lights Home.
   - Shard Store is a hub tile but lights Shop.
3. **A hidden mode:** "on the road" against "camped" changes what's open (Inn, Respec, Crucible), but no screen names it as a mode.
4. **Too deep:** Heroes, then Abilities, then Board is 3 tab levels on one screen.
5. **Developer and experimental items in player menus:** Windows, Turn mode.

### 3.2 Proposed

```
Splash ─ (first launch) Name ─ Drill 1 ─ Main Menu ...      (see section 4)

Main Menu
 ├ Continue ─ Home
 ├ Start Campaign
 ├ Load
 └ [≡] Profile · Settings · About · Credits

Bottom nav (every hub page, the root included):
 Home        Camp: party strip, mode banner (road/camped), NEXT card, Craft, Journal
 Journey     Stage select today; the overworld map when §28 lands; Crucible lives here
 Heroes      roster strip, then Status | Gear | Skills | Talents | Party
 Recruit     Summon (Standard, Recruit), Mercenaries
 Shop        Merchant (Buy/Sell), Shard Store, Bargain

Header everywhere:
 tab roots:     [≡] top-left · title · gold top-right
 pushed pages:  [<] Back top-left · title · gold top-right   ([≡] one tap away via Home)

[≡] menu: Settings · How to play · Profile · About · Save & Quit
Craft group:   Items · Blacksmith · Alchemist · Enchantress · Manasmith · Caravan
Journal group: Quests (each row has Talk) · Bounties · Talk · Codex · Bestiary
Inn and Respec: on Home's mode banner when camped; Respec also per hero in Talents
```

**Decisions for the owner:**
1. **Hamburger or Back in the top-left on pushed pages.** I recommend Back, the platform convention, with `[≡]` on tab roots. The alternative is `[≡]` always top-left and Back as a left-edge pill under the header.
2. **"Journey" against "Campaign" as the tab name.** Journey survives the overworld.
3. **Summon tab renamed "Recruit".** It also covers Mercenaries, and it softens the gacha read.

**Tap counts after the change (checklist E1 and E2):**

| Task | Taps |
|---|---|
| Cold start to battle | 3: Continue, Fight, (confirm) |
| Equip best gear | 3: Heroes, Gear, Auto-equip |
| Learn a skill | 2 from the Heroes badge |
| Rest | 2: Make camp, Rest |
| Retry the last stage | 1, from results |

---

## 4. Onboarding: what the first 5 minutes should teach

### 4.1 What happens now

Splash, then a 3-step Create Profile, then Main Menu, then Start Campaign, then the team pick, then "New to GridGame?", then the opening crawl, then the Founding dialogue. Only then Drill 1 (moving, tapping to read, pincers), and Drills 2-5 carry the other 15 lessons, each with a blocking popup and coach marks. After that comes the graduation card and Camp.

By my estimate the player reaches their first **unscripted** battle 12-20 minutes after launch, having read 18 popups. That is two to three times the patience budget for a mobile session (BIBLE §0.3 sizes a whole session at 20-30 minutes).

### 4.2 Principles

1. **Do, then name.** The player makes the pincer first; the word "Pincer" appears in the cut-in afterwards. No popup comes before the first action.
2. **One concept per minute.** No more than one blocking card per 60 seconds of play.
3. **No clock until the trap is understood.** Drills 1 and 2 run untimed (the clock waits even while you hold). The real-time window is introduced on its own, once the player already knows what they're spending time on. This also serves accessibility rule P1.
4. **The screen teaches itself.** The turn strip, the lane chips, pincer spots and intent arrows carry the teaching. Lessons point at them instead of describing them.
5. **Everything else is taught just in time,** the first time it matters in a real stage. The BIBLE already does this for Guard, Potions, Reinforcements, Heavy foes, Tactical tiles and Hidden mines; extend it.

### 4.3 The first 5 minutes

| Time | Beat | The player does | The screen teaches |
|---|---|---|---|
| 0:00 | Splash (tap skips) | | |
| 0:05 | **Name** (one field, suggestion prefilled) | Types or accepts | |
| 0:20 | **Drill 1, "The Trap"**: 2 heroes, 1 slime, untimed | Taps the Knight. A ⚔ spot glows. Drags there. | Pincer spots. The trap cut-in names it: "PINCER!" |
| 0:45 | Same drill, wave 2: 2 slimes in a row | Traps both with one drop | A chain. The billboard says "Chain x2". |
| 1:15 | Wave 3: a slime walks toward the Cleric | Watches the intent arrow, traps it first | Intent arrows. "Enemies act when they reach the left." The timeline lane is visible, still frozen. |
| 1:45 | **First real win.** Big VICTORY, simple results. | Taps Next | Sets the tone: winning feels good |
| 2:00 | Opening crawl (short, skippable) plus the Founding beat | | Who you are |
| 2:45 | **Drill 2, "The Clock"**: 4 heroes, 3 slimes, now timed | Lifts a hero, sees the turn strip go amber and the lane drain | "The clock runs only while you hold a hero." Each hero acts once (the chips grey out). |
| 3:30 | Gap too short | Presses GUARD (end turn) | GUARD: "end the turn, protect everyone, bank mana". The mana bar fills. |
| 4:00 | **Drill 3, "Abilities"**: the Cleric is hurt | Abilities, then Spells, then Heal, then Cast, then taps the Knight | The sheet, a cost against the mana bar, aiming, confirm |
| 4:40 | Info button on a foe | Taps Info, then a foe | Info explains anything |
| 5:00 | **Camp**, first visit: one boxed button, Fight | | "The next stage is here." Nothing else is pointed at yet. |

### 4.4 What leaves the drills (taught just in time instead)

| Concept | When it is taught |
|---|---|
| Supporters | First time a third hero stands in line with a pincer: one tap-to-continue box |
| Shoving | First time the player drops onto an occupied tile |
| Buffs and debuffs | First status that lands |
| Taunt and roles | First stage where a foe targets the Cleric |
| Pickups | First drop on the board |
| Interrupting | First foe cast diamond on the timeline |
| Armor plates | First plated foe |
| Combo | After the first B rank (as built, `battle-combo`) |
| Cast times | First spell with a cast time |
| Region rules | On entering the region: one card, then the rule's icon in the top bar |

**Result:** 3 drills and 7 lessons instead of 5 drills and 18 lessons. The Settings › How to play drill picker stays, for players who want the whole curriculum.

**Camp onboarding:** keep the BIBLE's one-new-station-per-stage pacing (§25.0.1.1). It's good. Make sure each station's first visit points at exactly one control.

---

## 5. Quick wins against structural changes

### 5.1 Quick wins (each one task, low risk, no rule change)

1. Remove the hand cursor from the menu kit. It shows on 7 captures.
2. Shared header: gold pinned top-right, zero-value currencies hidden (GG-T1858, GG-T1849). This fixes the clipped gold on `summon-history.png`.
3. Give the hub `[≡]` top-left and gold top-right, and drop gold from the Continue card (GG-T1845).
4. Style Continue (or Fight) on the hub as the single filled primary button.
5. Remove the gear durability strip from battle tokens. Keep only the cracked badge.
6. Content check: unique icons within each hero's kit. Fix Sacred Challenge against Judgment of Warding.
7. Panel copy: one verb-first instruction per state (table in #7).
8. Add a goal line to the top bar ("Defeat every foe") and a BOSS tag, purple plinth and named bar on bosses.
9. Copy fixes:

   | Screen | Change |
   |---|---|
   | Battle | "move or skill" becomes "time to move or use a skill"; "move only" becomes "only time to move" |
   | Skill Hex | "Hover or tap" becomes "Tap" |
   | Stage select | "28 more stages ahead…" becomes "28 more stages to unlock" (GG-T1850) |

10. GUARD gets a second line, "end turn", before the plan's slice C moves it.
11. Summon: one ticket count. Fix the header against body contradiction.
12. Shard Store: pin the footer above the bottom nav.
13. Quests: a Talk button on every row.
14. Skill Hex: show the whole board, no clipping. Fix the Talents against Respec node-count mismatch.
15. Items and Stage select: row spacing down to the 132 px kit row.
16. Settings: move Turn mode under an "Experimental" fold. Show Windows in dev builds only.
17. Results: a big centred VICTORY. Rename "Continue" to "To Camp" everywhere.
18. Bestiary: group by region and replace the silhouette wall with "?" counts.

### 5.2 Structural changes (stories of several tasks, in this order)

1. **Battle legibility pass** (new story under GG-T1805, alongside the plan's slices):
   1. Turn strip.
   2. Window lane with hero chips, and the far-left "YOU"/"NOW" slot.
   3. Pincer spots plus the outcome preview on tap.
   4. Enemy intent arrows for the next two actors.
   5. Base plates by side, a selected-lift state and the next-actor ring.
   6. Board art dim and grid contrast.
2. **The approved battle-actions plan**, slices A-C plus Info, the mana bar and the coach retarget (GG-T1914, GG-T1915 and the new slices). Do the BIBLE §9.0 rewrite first (the plan says so) and fold item 1's rows into the same rewrite, so one spec covers both.
3. **Onboarding rebuild:**
   1. One-step profile.
   2. Drill 1 before the crawl.
   3. Untimed drills 1-2.
   4. 3 drills and 7 lessons.
   5. Just-in-time lessons for the rest.
4. **Hub relayout and IA de-duplication** (sections 2.2 and 3.2):
   1. Nav on the hub root.
   2. Tiles cut to Craft and Journal.
   3. Mode banner.
   4. Shard Store into Shop.
   5. Recruit tab.
5. **Heroes screen flatten:** roster as a single strip; tabs Status, Gear, Skills, Talents, Party; the Party tab reordered.
6. **Tap-to-move with confirm** (checklist I1): tap a hero, tap a tile, see the path and pincer preview, then Confirm. It's an accessibility must-have, and it gives "select then confirm" to the core verb. The drag stays, as the fast path.
7. **Untimed-turn accessibility option** (P1) and battle speed x0.5 (P2).
8. **Currency consolidation:** gold in the header, everything else in context. One name per currency (Diamonds or Shards, and "Rested" renamed "Stamina" per §28.6).
9. **Overworld (§28):** sequence it *after* items 1-3. It answers "where am I going", but it adds day and night, food, danger, fog and waystones. Each is one more system to teach. Ship it only once a new player can already explain a battle in one sentence.

---

## 6. Proposed tickets

One line each: summary, then impact. They are grouped by the story they belong under. The parent files them into the tree.

**Battle legibility (new feature under GG-T1805)**
1. Turn strip under the top bar: YOUR TURN / CLOCK RUNNING / ENEMY TURN / TOO LITTLE TIME, plus the heroes who can still act. **Impact: Critical.** It answers "whose turn is it".
2. Window lane: a labelled YOU block to the next foe, with face chips for heroes who can still act; the far-left slot shows YOU or NOW instead of the next enemy. **Impact: Critical.**
3. Pincer spots: when a hero is selected and the clock is frozen, mark every reachable tile that would trap a foe, and outline the foe it catches. **Impact: Critical.** It makes the core verb visible.
4. Pincer outcome preview: tapping a spot shows strikers, the damage range and the pushback seconds in the panel. **Impact: High.**
5. Enemy intent: a red arrow and damage badge from the next one or two actors to their target during the party window. **Impact: High.**
6. Token base plates: a blue hero plinth and a red notched foe plinth; selected lifts with a white ring; next actor gets its own ring colour, never gold. **Impact: High.**
7. Remove the gear durability strip from battle tokens and keep only the cracked badge. **Impact: Medium.** It ends the "two HP bars" confusion.
8. Board readability: dim the art behind the board about 35% and raise grid lines to 3:1 on every theme (X6). **Impact: Medium.**
9. Top bar goal line ("Defeat every foe") and a wave-start billboard with the foe count. **Impact: Medium.**
10. Boss presentation: a named boss HP bar, a purple plinth and a BOSS tag on its diamond. **Impact: Medium.**
11. Panel instruction rewrite: one verb-first line per state; tips move to Info. **Impact: Medium.**
12. Collapse the recovery tray into a "+N resting" chip so it stops reading as empty diamonds. **Impact: Low.**
13. GUARD sublabel "end turn", plus a once-per-battle confirm when heroes haven't acted. **Impact: Medium.** Fold it into plan slice C.
14. Content check: no two abilities in one hero's kit share an icon; fix Sacred Challenge and Judgment of Warding. **Impact: Medium.**
15. Defeat screen: list the last three hits that ended the battle, next to Retry. **Impact: Low.**

**Approved plan (existing; just confirming the scope)**
16. Slice C BIBLE §9.0 rewrite also specifies tickets 1-2's rows, so the battle has one layout spec. **Impact: High.** It prevents two competing redesigns.

**Onboarding (new feature)**
17. Profile creation cut to one step (name with a suggestion); look and pronouns move to the Founding scene. **Impact: Medium.**
18. Drill 1 "The Trap" plays before the opening crawl: untimed, 2 heroes, pincer spots, chain, intent. **Impact: High.**
19. Drills untimed until the Clock drill: the clock waits even while holding. **Impact: High.**
20. Cut the drills from 5 drills and 18 lessons to 3 drills and 7 lessons; turn the rest into first-time-in-a-real-stage lessons. **Impact: High.**
21. Put the one-sentence loop line on the first drill card, at the top of How to play and in Info's empty state. **Impact: Medium.**

**Hub and IA (new feature)**
22. Hub header with `[≡]` top-left and gold top-right; it fits one screen with no scroll (covers GG-T1845 and GG-T1846). **Impact: High.**
23. Hub relayout: a single-row party strip, a mode banner (road or camped, with its one action) and a NEXT card with one filled Fight button in the thumb zone. **Impact: High.**
24. Bottom nav on the hub root too; hub tiles cut to Craft and Journal; Shard Store moves into Shop. **Impact: Medium.**
25. Rename the Summon tab "Recruit" and move Mercenaries under it; rename "Campaign" to "Journey" (owner decision). **Impact: Low.**
26. Quests: a Talk button on every quest row. **Impact: Medium.** It removes dead ends.
27. Settings reorder: Accessibility first; Turn mode under Experimental; Windows hidden outside dev builds. **Impact: Medium.**

**Menu kit and screens**
28. Remove the hand cursor from the kit everywhere; the focus is a filled row and a left accent bar. **Impact: Medium.** It's an owner rule, broken on 7 screens.
29. Results rework: a big VICTORY, a summary row, a "carried to next battle" box, a loot grid, one Teach card, consistent "To Camp". **Impact: Medium.**
30. Heroes screen: roster as a single thumbnail row; the Party tab reordered so the active party comes first and the cohesion matrix is replaced by pair lines. **Impact: Medium.**
31. Skill Hex: show the whole board, select-then-confirm node purchase, "Tap" copy, one name ("Talents"), and fix the Respec node-count mismatch. **Impact: Medium.**
32. Equip: a slot grid plus an Auto-equip best button. **Impact: Medium.**
33. Stage select: region cards with an art strip, the recommended level and the next stage's name; tighter rows. **Impact: Low.**
34. Shard Store: footer pinned above the bottom nav; one currency name across Diamonds, Shards and Mana Shards. **Impact: Medium.**
35. Summon: one ticket count (fix the header-against-body contradiction), and ask the owner whether the pity bar stays, since BIBLE §3 says there isn't one. **Impact: Low.**
36. Bestiary: group by region with "?" counts instead of the silhouette wall; check the "Seen 0" fixture. **Impact: Low.**
37. Small list screens (Items, Talk, Alchemist, Enchantress, Mercenaries): 132 px rows, actionable rows first, detail in a sheet. **Impact: Low.**

**Accessibility (new feature)**
38. Tap-to-move with path and pincer preview and Confirm, as an alternative to dragging (I1). **Impact: High.**
39. An untimed-turns option and battle speed x0.5 (P1, P2). **Impact: Medium.**

**Review follow-up**
40. Recapture pixel9 and ipad and re-check this review's battle and hub verdicts at 9:23 and 3:4 (the folders weren't in the working tree). **Impact: Low.**
