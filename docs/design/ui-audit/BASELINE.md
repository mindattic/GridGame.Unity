# iPhone baseline (GG-T2029)

Every screen `tools/uiaudit.py` walks (41: the StartScreens ids, the two results screens and the seven scenario spots),
captured on one device per in-target iPhone shape from `tools/devices.json`, one player at a time. The Windows dev
player was built from origin/main at `81023c2b`, so it has the bottom battle timeline (GG-T2118) and the camera cutout and
rounded screen corners painted over all UI by the `deviceframe` endpoint (GG-T2119). iPhone 15 Pro is the reference.

| device | shape | cutout |
|---|---|---|
| iphone15pro (reference) | iphone-island-393 (14 Pro / 15 / 15 Pro / 16) | island |
| iphone16e | iphone-notch-390 (the only in-target notch phone) | notch |
| iphone15promax | iphone-island-430 (15 Plus / 15 Pro Max / 16 Plus) | island |
| iphone17pro | iphone-402 (16 Pro / 17 / 17 Pro) | island |
| iphone17promax | iphone-440 (16 Pro Max / 17 Pro Max) | island |
| iphoneair | iphone-air (68 pt top inset) | island |

The island-393 and island-430 shapes are captured on the 15 Pro and 15 Pro Max rather than the 16 and 16 Plus that
`--device shapes` picks: same shape, and the oldest in-target phone of each.

Per device: `captures/<device>/<screen>.png`, `facts.json` (uiaudit.py's raw measures) and `findings.json` (written by
`python tools/uiaudit_baseline.py <device> ...`, which also prints the table below).

## Summary

Each cell is findings (screens with any). No screen errored and none landed somewhere unexpected, except `splash`,
which lands on create-profile in this build, and `overworld`, which lands on the hub until an overworld map screen exists.

| device | shape | screens | errors | overflow | clipped | overlap | offStage | underCutout | topInset | homeIndicator | targetsUnder44pt | contrastFails | contrastUnderCutout |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| iphone15pro | iphone-island-393 | 41 | 0 | 0 (0) | 0 (0) | 0 (0) | 0 (0) | 0 (0) | 0 (0) | 52 (19) | 7 (7) | 26 (16) | 0 (0) |
| iphone16e | iphone-notch-390 | 41 | 0 | 0 (0) | 0 (0) | 0 (0) | 0 (0) | 0 (0) | 0 (0) | 53 (18) | 4 (4) | 22 (11) | 0 (0) |
| iphone15promax | iphone-island-430 | 41 | 0 | 0 (0) | 0 (0) | 0 (0) | 0 (0) | 0 (0) | 0 (0) | 54 (19) | 0 (0) | 25 (14) | 0 (0) |
| iphone17pro | iphone-402 | 41 | 0 | 0 (0) | 0 (0) | 0 (0) | 0 (0) | 0 (0) | 0 (0) | 54 (19) | 0 (0) | 25 (16) | 0 (0) |
| iphone17promax | iphone-440 | 41 | 0 | 0 (0) | 0 (0) | 0 (0) | 0 (0) | 0 (0) | 0 (0) | 52 (19) | 0 (0) | 25 (15) | 0 (0) |
| iphoneair | iphone-air | 41 | 0 | 0 (0) | 0 (0) | 0 (0) | 0 (0) | 0 (0) | 0 (0) | 52 (19) | 0 (0) | 28 (16) | 0 (0) |

Columns: **overflow** text that overflows or wraps past its box; **clipped** an element clipped by a parent;
**overlap** sibling elements overlapping; **offStage** a label outside the stage; **underCutout** on-screen text whose
rect meets the Dynamic Island or notch; **topInset** other text above the safe top; **homeIndicator** text more than
8 device px into the bottom safe inset; **targetsUnder44pt** tap targets under 44 pt on the short side;
**contrastFails** text under WCAG AA (4.5, or 3 for large text) against the screenshot behind it;
**contrastUnderCutout** the same for samples under the cutout.

## What it says

- **Nothing sits under the cutout or in the top inset on any shape**, including the notch (16e) and the Air's taller
  inset. The header row clears the island everywhere. With the cutout painted black, a contrast sample under the island
  would read black; there were none, so `contrastUnderCutout` is 0 throughout.
- **No overflow, clipping, overlap or off-stage text** on any device in this build.
- **homeIndicator is scroll content, not fixed UI.** Every hit is a row of a scroll view that runs on past its
  viewport (the battle panel's log and stats, the hub's quote, list rows on the shop, settings, quests and so on), which
  the uiaudit facts do not mark as scrolled. The fixed controls (tab bar, battle Abilities/Info/Guard row and panel)
  stay above the band in every capture.
- **Tap targets under 44 pt only on the 390 and 393 pt phones**, and only just: the full-width Continue, Start
  Campaign, X-Potion, Weapon and Scenario rows are 132 design px tall, which is 43.7 pt on the 15 Pro and 43.0 pt on the
  16e. The wider shapes scale them past 44 pt. One more design px of height fixes all of them.
- **Contrast**: the same handful everywhere. Disabled-looking primary buttons (Buy, Enchant, Create, Next, Enter the
  Crucible, "Nothing needs repair") at about 2.2; the battle Info button (3.7-3.8) and boss-fight panel words (TANK,
  HEALTH, gold) over the brighter moor background; the hub's Summon Circle label (about 4.1-4.5); and the camp scenario,
  where the locked station tiles (Heroes, Summon Circle, Merchant, Mana Shard Store, their "Opens after" lines) read
  1.2-2.0 over the camp art.

## Reference shots (iPhone 15 Pro)

- Battle: `captures/iphone15pro/battle.png`
- Hub: `captures/iphone15pro/hub.png`
- Battle Abilities sheet (opened from the battle's Abilities button, outside uiaudit.py's walk):
  `captures/iphone15pro/battle-abilities.png`
- Heroes > Abilities: `captures/iphone15pro/abilities.png`

# Galaxy baseline (GG-T2030)

The same 41-screen walk, one device per in-target Galaxy shape from `tools/devices.json`, one player at a time
(`python tools/uiaudit.py --device <id>`, then `python tools/uiaudit_baseline.py <ids>`). The Windows dev player was built
from origin/main at `133a873b`, so the battle screen has the newer turn strip and timeline than the iPhone shots above.
The `deviceframe` overlay draws each phone's punch-hole and screen corners; Samsung publishes neither, so this ticket
recorded estimates for every in-target Galaxy in `tools/gen_devices.py` (an 18 dp hole 11 dp below the edge; corners
26-34 dp; see `docs/devices/DEVICES.md`, "Camera cutouts").

| device | shape | cutout |
|---|---|---|
| galaxys25 | galaxy-fhd (S23-S25 base, S23+, the FEs; 1080x2340, 360-384 dp) | punch |
| galaxys25ultra | galaxy-qhd (S23-S25 Ultra, S24+/S25+, S25 Edge; 1440 wide, 412 dp) | punch |
| galaxyzflip6 | galaxy-flip-main (Z Flip5/6 main, 22:9) | punch |
| galaxyzfold6cover | galaxy-fold-cover (Z Fold5/6 cover, about 9:23) | punch |

Skipped: `galaxy-20x9` (S21, S21+, S21 Ultra) is `inTarget: false`, and so are the S22 rows inside the fhd and qhd shapes.
The Fold5 cover (904x2316, narrower) shares the fold-cover shape but was not captured; its fog bars are GG-T2115.

## Summary

| device | shape | screens | errors | overflow | clipped | overlap | offStage | underCutout | topInset | homeIndicator | targetsUnder44pt | contrastFails | contrastUnderCutout |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| galaxys25 | galaxy-fhd | 41 | 0 | 0 (0) | 0 (0) | 0 (0) | 0 (0) | 0 (0) | 0 (0) | 61 (13) | 8 (7) | 27 (10) | 0 (0) |
| galaxys25ultra | galaxy-qhd | 41 | 0 | 0 (0) | 0 (0) | 0 (0) | 0 (0) | 0 (0) | 0 (0) | 51 (14) | 0 (0) | 25 (9) | 0 (0) |
| galaxyzflip6 | galaxy-flip-main | 41 | 0 | 0 (0) | 0 (0) | 0 (0) | 0 (0) | 0 (0) | 0 (0) | 21 (10) | 4 (4) | 18 (10) | 0 (0) |
| galaxyzfold6cover | galaxy-fold-cover | 41 | 0 | 0 (0) | 0 (0) | 0 (0) | 0 (0) | 0 (0) | 0 (0) | 29 (12) | 3 (3) | 16 (10) | 0 (0) |

Columns as above. `targetsUnder44pt` counts targets under 44 of the device's unit, which is dp on Galaxy; Android's own
minimum is 48 dp, so the Galaxy counts understate the problem.

## What it says

- **The punch-hole and the top inset are clear on every shape.** Nothing sits under the hole or above the 32 dp status
  bar; the header row clears it everywhere.
- **No overflow, clipping, overlap or off-stage text** by the measures, on any Galaxy shape.
- **homeIndicator** is the same scroll-content effect as on iPhone (rows of scroll views running past their
  viewport), and lower on the taller Flip and Fold cover.
- **Tap targets are the real Galaxy problem.** The full-width 132-design-px rows (Continue, Start Campaign, X-Potion,
  Regeneration, Scenario) come out at 40 dp on the 360 dp S25, 43.3 dp on the Flip6 and 35.6 dp on the Fold6 cover;
  only the 412 dp Ultra clears 44. All are under Android's 48 dp. Filed as GG-T2144.
- **Contrast** fails are the iPhone set (disabled-looking Buy / Enchant / Create / Enter the Crucible near 2.2, boss
  panel words, Windows delete) plus the camp station labels (Craft, Journal, Recruit near 2.2) on the narrow Flip and
  Fold cover.
- **Seen in the shots, not in the measures:** the battle turn-timer hint ("3.7s - move or skill") is half hidden under
  the Abilities / Info / Guard row and crossed by the turn-progress bar on all four (GG-T2142), and the YOUR TURN strip
  overlaps the grid's top edge, worst on the Flip and Fold cover (GG-T2143). Both come from the newer battle layout, so
  they likely show on iPhone too once those shots are retaken on this build.
- **Fold6 cover fills the screen**: no fog bars at 968x2376.

## Reference shots

- Battle: `captures/galaxys25/battle.png`, `captures/galaxyzfold6cover/battle.png`
- Hub: `captures/galaxys25/hub.png`, `captures/galaxyzfold6cover/hub.png`
