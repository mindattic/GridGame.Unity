# Phone game accessibility and UI placement: the guides, and a GridGame checklist

*GG-T1835, 2026-11-27.* Owner: "need to go through the UI on every screen and review it for efficiency and correct
placement; not sure if WCAG 2.2 AA can help here or not but there must be some guides out there for accessibility in
video games on the phone."

**Short answer.** Yes, there are game-specific guides, and they fit better than WCAG. Use them in this order:
1. **Xbox Accessibility Guidelines (XAGs)** for measurable numbers. They include a mobile touch section with millimetre
   target sizes and DPI-scaled text sizes.
2. **Game Accessibility Guidelines (GAG)** for what to build first (Basic, then Intermediate, then Advanced).
3. **Apple HIG and Material** for phone norms: 44 pt and 48 dp targets, safe areas, Dynamic Type, system settings.
4. **WCAG 2.2 AA** only for the parts that carry over to a game UI: contrast, target size, dragging, timing, resize text.

The checklist in §2 turns these into pass rules for GridGame. Each rule cites its source and maps to a Polish Standard
rule ([`docs/POLISH.md`](../POLISH.md)) where one already exists. §3 explains how to audit a screen. §4 lists what
GridGame's Settings are missing.

---

## 0. Unit conversions used in this doc

GridGame lays out on a 1170-px-wide design stage that scales uniformly (BIBLE §26). On the reference devices:

| Device | Screen px | Density | Points / dp | 1 design px ≈ | 132 design px (`--size-target`) | 180-px board tile |
|---|---|---|---|---|---|---|
| iPhone 17 Pro | 1206 × 2622 | 460 ppi, @3x | 402 × 874 pt | 0.34 pt | ≈ 45 pt, 7.6 mm | ≈ 62 pt, 10.3 mm |
| Pixel 9 | 1080 × 2424 | ~422 ppi, 2.625x | ~411 × 923 dp | 0.35 dp | ≈ 46 dp, 7.4 mm | ≈ 63 dp, 10.1 mm |
| iPad (A16) | 1640 × 2360 | 264 ppi, @2x | 820 × 1180 pt | stage is wider (up to 1899 px), height-limited | ≥ 44 pt | ≥ 60 pt |

1 pt on an iPhone is about 0.166 mm (163 pt per inch). The `tools/scenario.py` presets `iphone17pro`, `pixel9` and
`ipad` already use these pixel sizes and their safe-area insets.

---

## 1. The guides: what each one is and whether it fits

| Guide | What it is | Fit for GridGame | Use it for |
|---|---|---|---|
| **[Game Accessibility Guidelines](https://gameaccessibilityguidelines.com/full-list/)** (GAG) | Developer-written list of game guidelines, tiered Basic / Intermediate / Advanced by cost and reach, grouped Motor, Cognitive, Vision, Hearing, Speech, General | **High.** Written for games, cheap tier first | What to build first. Most Basic items are low effort |
| **[Xbox Accessibility Guidelines](https://learn.microsoft.com/en-us/gaming/accessibility/guidelines)** (XAG v3.2) | Microsoft's game guidelines with goals, scoping questions and numbered implementation rules. Explicitly not a compliance checklist | **High.** Has a **mobile** section with numbers: [XAG 101 text](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/101), [102 contrast](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/102), [107 input and touch](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/107), [112 UI navigation](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/112), [117 motion and distraction](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/117) | Measurable pass rules (mm targets, DPI text sizes, 4.5:1 / 3:1 / 7:1 contrast) |
| **[AbleGamers APX](https://ablegamers.org/our-programs/accessibility-unlocked/apx/)** | 22 design patterns: 12 [Access](https://ablegamers.org/our-programs/accessibility-unlocked/apx/access-patterns/) (Clear Text, Second Channel, Distinguish This From That, Personal Interface, Do More With Less, …) and 10 [Challenge](https://ablegamers.org/our-programs/accessibility-unlocked/apx/challenge-patterns/) (Slow It Down, Undo Redo, Training Ground, Total Recall, Helping Hand, …) | **Medium-high.** Vocabulary for design reviews, not numbers | Naming a fix ("this needs Slow It Down") |
| **[Apple HIG: Accessibility](https://developer.apple.com/design/human-interface-guidelines/accessibility)**, [Layout](https://developer.apple.com/design/human-interface-guidelines/layout), [Typography](https://developer.apple.com/design/human-interface-guidelines/typography), [Designing for games](https://developer.apple.com/design/human-interface-guidelines/designing-for-games) | Platform norms: **44 × 44 pt** minimum hit target, 17 pt default body and 11 pt minimum text, Dynamic Type (7 standard + 5 accessibility sizes), safe areas, honor Reduce Motion / Bold Text / Increase Contrast | **High** for layout and touch. Dynamic Type has no direct hook in Unity UI Toolkit | Phone norms, safe areas, system settings |
| **[App Store Accessibility Nutrition Labels](https://developer.apple.com/help/app-store-connect/manage-app-accessibility/overview-of-accessibility-nutrition-labels)** | Per-device claims on the product page (iOS 26+): VoiceOver, Voice Control, **Larger Text (200%+)**, Dark Interface, Differentiate Without Color Alone, Sufficient Contrast, Reduced Motion, Captions, Audio Descriptions. Voluntary for now; Apple says it will become required for submissions | **High.** A public scorecard. A feature may be claimed only if **every common task** (first launch, main gameplay, purchase, settings) can be done with it | The release target: which labels GridGame can honestly claim (§4) |
| **[Android accessibility](https://developer.android.com/guide/topics/ui/accessibility/apps)** / **[Material 3 accessibility](https://m3.material.io/foundations/designing/structure)** | **48 × 48 dp** touch targets with ~8 dp between them; text contrast 4.5:1 (3:1 for large text); TalkBack; system font scale (up to 200% on Android 14+); "Remove animations" | **High** for targets and contrast. TalkBack only through Unity's Accessibility module | Target and spacing numbers, Android behaviour |
| **[WCAG 2.2](https://www.w3.org/TR/WCAG22/)** AA (applied to non-web software via **[WCAG2ICT](https://www.w3.org/TR/wcag2ict-22/)**) | Web content standard. EU and US law reference it for websites and apps, not for gameplay | **Partial.** See the table below | Contrast, target size, dragging, timing, resize text |
| **CVAA (US)** | 21st Century Communications and Video Accessibility Act. Covers **in-game communication** (text, voice and video chat), not gameplay ([overview](https://www.gamedeveloper.com/business/demystifying-cvaa)) | **Not applicable today.** GridGame has no chat. Applies if chat is ever added | Nothing for now |
| **EAA (EU), Directive 2019/882**, in force **28 June 2025** | Covers e-commerce and electronic communication services. Games are not named, but in-game **purchases** and chat are in scope ([IGDA GA-SIG explainer](https://igda-gasig.org/what-and-why/demystifying-eaa-gpsr/), [Taylor Wessing](https://www.taylorwessing.com/en/insights-and-events/insights/2025/03/accessibility-in-the-gaming-industry)) | **Low to medium.** The Shard Store and other IAP flows count as e-commerce. Microenterprises (fewer than 10 staff and under €2M turnover) are exempt from the service obligations. Check MindAttic LLC's status before an EU launch with IAP | Make the purchase flow meet WCAG AA: labels, contrast, no colour-only cues, and an accessibility statement |

### WCAG 2.2: what carries over to a phone tactics game

| SC | Level | Applies? | GridGame reading |
|---|---|---|---|
| [1.4.3 Contrast (Minimum)](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html) | AA | **Yes** | Text 4.5:1, large text 3:1 (POL-B2) |
| [1.4.11 Non-text Contrast](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html) | AA | **Yes** | Gauges, mana meters, slot frames, keylines, timeline diamonds 3:1 (POL-B3) |
| [1.4.1 Use of Color](https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html) | A | **Yes** | Sides, elements, rarities, status (POL-B5) |
| [1.4.4 Resize Text](https://www.w3.org/WAI/WCAG22/Understanding/resize-text.html) | AA | **Yes** | 200% without losing content. GridGame stops at 130% |
| [1.4.10 Reflow](https://www.w3.org/WAI/WCAG22/Understanding/reflow.html) | AA | **In spirit only.** The 320 CSS px test is about browser zoom | The XAG 112 form fits better: scaled text never needs scrolling in **two** directions |
| [1.4.12 Text Spacing](https://www.w3.org/WAI/WCAG22/Understanding/text-spacing.html) | AA | **Partial.** Users can't inject CSS into a game | Use as a design target: line height ≥ 1.5 in paragraphs (XAG 101) |
| [1.3.4 Orientation](https://www.w3.org/WAI/WCAG22/Understanding/orientation.html) | AA | **Exception claimed.** The portrait lock is a product law (GG-LAW-8); a 6×8 board in portrait is arguably "essential" | Record it as a known gap (GAG Advanced and XAG 107 both ask for both orientations, for players with a mounted device) |
| [2.2.1 Timing Adjustable](https://www.w3.org/WAI/WCAG22/Understanding/timing-adjustable.html) | A | **Yes, for the turn window.** The real-time and essential exceptions are weak for a single-player game | Offer turn off, extend, or ≥ 10× longer |
| [2.3.1 Three Flashes or Below Threshold](https://www.w3.org/WAI/WCAG22/Understanding/three-flashes-or-below-threshold.html) | A | **Yes** | Clutch flash, crit bursts, marquee flashes (POL-E7) |
| [2.3.3 Animation from Interactions](https://www.w3.org/WAI/WCAG22/Understanding/animation-from-interactions.html) | AAA | **Yes, as a target** even though it is AAA | Reduce Motion already does most of it |
| [2.4.11 Focus Not Obscured (Minimum)](https://www.w3.org/WAI/WCAG22/Understanding/focus-not-obscured-minimum.html) / 2.4.7 Focus Visible | AA | **Only with keyboard, controller or switch input** | Matters once controller and switch support exist (desktop and Steam builds) |
| [2.5.1 Pointer Gestures](https://www.w3.org/WAI/WCAG22/Understanding/pointer-gestures.html) | A | **Yes** | Pinch and two-finger gestures (ArtPreview zoom) need a single-pointer alternative. ArtPreview has +/- buttons |
| [2.5.2 Pointer Cancellation](https://www.w3.org/WAI/WCAG22/Understanding/pointer-cancellation.html) | A | **Yes** | Act on release. Releasing off the board cancels (POL-D5 ✅) |
| [2.5.7 Dragging Movements](https://www.w3.org/WAI/WCAG22/Understanding/dragging-movements.html) | AA | **Yes. The biggest one.** | Moving a hero is drag-only. It needs a tap-based path |
| [2.5.8 Target Size (Minimum)](https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html) | AA | **Yes, but weak.** 24 × 24 CSS px is below every game guide | Use Apple and Material (44 pt / 48 dp) as the floor and XAG 15 mm as the stretch |
| 3.3.x Input assistance, 4.1.2 Name/Role/Value | A/AA | **Menus only**, through a screen reader | Unity Accessibility module (§2.10) |
| 2.4.2 Page Titled, 2.4.5 Multiple Ways, 3.2.3 Consistent Navigation | A/AA | **In spirit** | Every page has a header title. Navigation is consistent (bottom nav, Back top-left) |
| 1.2.x Captions / audio description | A/AA | **Mostly no.** GridGame has no voiced dialogue or video | Re-check if voice or cutscene video is added |

---

## 2. GridGame checklist

Each row is **rule → pass test → source**. "Status" is a first read from BIBLE and code (2026-11-27). ✅ likely passes,
🟡 partial or unmeasured, ❌ likely fails, ❓ needs the audit in §3. Design px are on the 1170-wide stage (§0).

### 2.1 Layout and placement

| # | Rule | Pass test | Source | Status |
|---|---|---|---|---|
| L1 | **Frequent actions sit in the thumb zone** | Every control used once or more per turn (move, ability slots, Cast/Cancel, Confirm, **Guard**, End/Skip) has its centre in the **bottom 45%** of the safe-area stage | [Hoober 2013, how users hold phones](https://www.uxmatters.com/mt/archives/2013/02/how-do-users-really-hold-mobile-devices.php); POL-C4 | ❌ **GUARD** sits at the right end of the timeline row (y ≈ 150–333 of 2532, the **top 13%**). Ability bar, tabs, Cast/Cancel ✅ |
| L2 | **Rare controls go to the top corners, by convention** | Pause/menu **top-left**, currency/status **top-right**, Back **top-left** in the header, nothing destructive in a corner the thumb sweeps through | [Apple HIG Layout](https://developer.apple.com/design/human-interface-guidelines/layout); XAG 112 (consistent placement) | ✅ pause top-left, coins top-right, header Back top-left |
| L3 | **Persistent Home/Back on every sub-page** | Every page above the Hub has Back, and pages 2+ deep also have Home or the bottom nav. Android hardware back works everywhere | XAG 112 ("persistent links back to the main menu") | ✅ GG-T1328/GG-T1335 bottom nav and header Home |
| L4 | **Edge and corner targets are bigger** | Targets touching the top or bottom 10% of the screen are ≥ **11 mm** (≈ 66 pt ≈ 195 design px) in the direction of the edge. Centre targets ≥ 7 mm | [Hoober 2017, Design for Fingers, Touch, and People](https://www.uxmatters.com/mt/archives/2017/03/design-for-fingers-touch-and-people-part-1.php) | ❓ the pause button and header Back are 132 px (7.6 mm) |
| L5 | **Safe areas** | No content or control inside the notch, Dynamic Island or home-indicator inset on any device in §3. Backdrops bleed under them | [Apple HIG Layout](https://developer.apple.com/design/human-interface-guidelines/layout); BIBLE §26.5; POL-C5 | 🟡 faked on desktop (GG-T483). Never checked on a real notched phone |
| L6 | **Gesture-zone clearance** | No drag target or control within **~20 dp of the bottom edge** or the left/right edges where system back and home gestures start | [Android gesture navigation](https://developer.android.com/develop/ui/views/touch-and-input/gestures/gesturenav) | ❓ check the bottom row of the board and the tab strip on Pixel 9 |
| L7 | **Row discipline** | Each screen is fixed horizontal bands: header, content, footer/action bar. The commit button is **bottom-right** in the footer, the total bottom-left. One primary button per screen | POL-C4, POL-C7; BIBLE §26.7 menu kit | ✅ kit roles exist; ❓ per screen |
| L8 | **One-handed portrait play** | A full battle turn (pick a hero, move, cast, confirm) is possible with the right thumb alone on iPhone 17 Pro, with the phone held at the bottom third. Same with the left hand when the thumb margin is set | GAG Basic "large and well spaced"; [Hoober](https://www.uxmatters.com/mt/archives/2013/02/how-do-users-really-hold-mobile-devices.php) | ❓ the top rows of the board (y < 30%) are out of one-thumb reach by design. Allowed for **drag targets**, but not for buttons |
| L9 | **Stationary targets** | Nothing you must tap moves while you aim at it (no drifting buttons, no marquee on a button's hit box) | GAG Intermediate "make interactive elements that require accuracy stationary" | ✅ marquee moves only the text; ❓ timeline diamonds (tap-to-read) move |
| L10 | **No dead bands, no clipping** | Side margins ≤ 10% of width, no empty band > 15% of height, nothing clipped, on 9:23 through 3:4 | POL-C5, POL-C9 | 🟡 `harness.py aspects` checks it |

### 2.2 Efficiency (taps and information)

| # | Rule | Pass test | Source | Status |
|---|---|---|---|---|
| E1 | **Launch to battle in ≤ 3 taps** for a returning player | Count taps from cold start: Splash (tap skips) → Main Menu **Continue** → Hub **Continue: Stage N** → stage confirm. Splash skip not counted | [GAG Basic: start without multiple levels of menus](https://gameaccessibilityguidelines.com/allow-the-game-to-be-started-without-the-need-to-navigate-through-multiple-levels-of-menus/) | ✅ 3 by design (GG-T1328, GG-T715); confirm by harness |
| E2 | **Common camp tasks in ≤ 3 taps from the Hub** | Equip best gear for a hero, learn the next ability, heal or rest, sell junk, summon once, retry the last stage: each ≤ 3 taps. Each has a one-tap shortcut where the game can decide (auto-equip, "Learn" deep link) | XAG 112; Nielsen flexibility and efficiency | 🟡 "Learn" deep link exists (GG-T1404); ❓ auto-equip, sell junk |
| E3 | **≤ 7 choices per visible group** | Menu groups and tile grids ≤ 7 items, otherwise grouped or nested | POL-C7 (Hick–Hyman) | 🟡 Hub has 6 tiles ✅; ❓ Settings sections |
| E4 | **Progressive disclosure** | Primary numbers show by default (HP, cost, cooldown). Full rules are one tap away (tap-to-read card), never more than two | GAG Intermediate "contextual help"; POL-D1 | ✅ tap/hold InspectCard and [term] glossary links |
| E5 | **Outcome preview before commit** | Aim shows damage/heal range, kills, status chance and mana effect before Cast | POL-D2; APX "Helping Hand" | 🟡 |
| E6 | **No duplicate pages, no lost runs** | Opening a page already on the stack returns to it, and nav can't abandon a battle | XAG 112 | ✅ GG-T1341/GG-T1614 |
| E7 | **Battle screen density** | Each band carries one job (status, timeline, banner, board, bar, tabs). Nothing from the HUD draws over the board for longer than 0.4 s | POL-E8; GAG "simple clear formatting" | ✅ by layout; ❓ combo meter and billboard over the board's top rows |

### 2.3 Touch targets

| # | Rule | Pass test | Source | Status |
|---|---|---|---|---|
| T1 | **Floor: 44 pt / 48 dp** | Every tappable element's hit box ≥ 132 × 132 design px (≈ 45 pt / 46 dp on the reference phones) | [Apple HIG](https://developer.apple.com/design/human-interface-guidelines/accessibility) 44 pt; [Android](https://developer.android.com/guide/topics/ui/accessibility/apps) 48 dp; POL-C2 | ✅ `harness.py a11y` POL-C2, GG-T524 |
| T2 | **Spacing** | ≥ 24 design px (≈ 8 dp) of dead space between adjacent targets, or hit boxes that don't overlap | [Material 3](https://m3.material.io/foundations/designing/structure) 8 dp; XAG 107 "inactive space"; POL-C3 | ❌/❓ POL-C3 is ⬜ (unchecked). Board tiles are edge to edge (acceptable for a grid that is a drag surface, not for buttons) |
| T3 | **Stretch: XAG phone size** | Primary battle targets (ability slots, Guard, Cast/Cancel) ≥ **15 mm** (≈ 90 pt ≈ 262 design px) on phones and ≥ 24 mm on tablets | [XAG 107 mobile](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/107) | ❌ slots ≈ 211 × 196 (≈ 12 × 11 mm), tiles 10 mm. Aspirational: record it, don't block on it |
| T4 | **Act on release, cancel by sliding off** | Buttons fire on pointer-up. Sliding off before release cancels. Drags can be dropped back or cancelled | [WCAG 2.5.2](https://www.w3.org/WAI/WCAG22/Understanding/pointer-cancellation.html); XAG 107 | ✅ POL-D5 |
| T5 | **No required multi-finger gestures** | Pinch and two-finger actions all have a one-finger or button alternative | [WCAG 2.5.1](https://www.w3.org/WAI/WCAG22/Understanding/pointer-gestures.html); XAG 107 | ✅ ArtPreview has +/- and double tap |
| T6 | **Holds have a tap alternative** | Anything opened by holding (InspectCard at 0.45 s, Party on long-press) also opens by a tap | [GAG Intermediate: alternatives to holding](https://gameaccessibilityguidelines.com/avoid-provide-alternatives-to-requiring-buttons-to-be-held-down/); XAG 107 | ✅ tap-to-read (GG-T450) |
| T7 | **Resizable / repositionable controls** | A setting enlarges battle controls (UI scale **above** 100%) or moves them (left-hand / thumb-margin) | XAG 107; GAG Intermediate "allow interfaces to be resized / rearranged" | 🟡 Thumb margin exists; UI scale only goes **down** (80–100%) |

### 2.4 Text and contrast

| # | Rule | Pass test | Source | Status |
|---|---|---|---|---|
| X1 | **Readable default size** | Body ≥ **14 dp/pt** at 100% (Material body-medium); nothing informative < **11 pt** (Apple minimum). Captions are the same size as body (GG-T770) | [Apple HIG Typography](https://developer.apple.com/design/human-interface-guidelines/typography); GAG Basic "easily readable default font size"; POL-A2 | ✅ `GuiType` targets 14 dp, floor 12 px / 9 dp |
| X2 | **Scales to 200%** | Text size reaches ≥ **200%** with no lost content and scrolling in one direction at most. Battle labels (floats, tile badges, counters) included | [WCAG 1.4.4](https://www.w3.org/WAI/WCAG22/Understanding/resize-text.html); [XAG 101](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/101); Apple "Larger Text" label = 200%+ | ❌ max 130% |
| X3 | **Follows the system text size** | First launch reads iOS Dynamic Type / Android font scale and sets Text size to match (clamped), unless the player has picked one | [Apple HIG](https://developer.apple.com/design/human-interface-guidelines/typography); XAG 112 (read platform settings); Unity `AccessibilitySettings` | ❌ not read |
| X4 | **Text contrast** | Text ≥ **4.5:1**, large text (≥ 54 px, or ≥ 42 px Heavy) ≥ **3:1**, on **every** menu theme, measured against the **lightest** stop of a gradient | [WCAG 1.4.3](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html); [XAG 102](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/102); POL-B2 | 🟡 checked on **Classic** (GG-T495). The default is now **Manapunk** (`SettingsSave.DefaultMenuTheme`), plus Cyberpunk, Ember, Verdant, Slate, Dark and custom themes, all unaudited |
| X5 | **Disabled text** | Locked or inactive labels ≥ **3:1** | XAG 102 | ✅ locked rows grey only name, value and icon (GG-T495); ❓ per theme |
| X6 | **Non-text contrast** | Gauges, meter fills vs empty, slot frames, keylines, timeline diamonds ≥ **3:1** against what is behind them | [WCAG 1.4.11](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html); POL-B3 | ❌/❓ POL-B3 is ⬜ |
| X7 | **Text over art has a plate** | Any text drawn over board art, a backdrop or VFX has an outline or a backing plate that keeps 4.5:1 against the worst pixel | XAG 102 (non-solid backgrounds) | 🟡 floats are outlined; ❓ banner and billboard over bright regions (Bloom, desert) |
| X8 | **High-contrast option** | A high-contrast theme where every UI element is ≥ **7:1** | XAG 102; GAG Intermediate "option to adjust contrast" | ❌ none (Dark is a style, not high contrast) |
| X9 | **Paragraph form** | Paragraphs ≤ 60 characters a line (POL-A5), line height ≥ 1.5, sentence case, left-aligned | XAG 101 text spacing; POL-A5 | ❓ POL-A5 is ⬜ |
| X10 | **Clear font available** | At least one plain sans option (no display face) for body text. Display face only on headlines | XAG 101; APX "Clear Text" | ✅ Font presets (Inter, Roboto, Lato) |

### 2.5 Color independence

| # | Rule | Pass test | Source | Status |
|---|---|---|---|---|
| C1 | **Never colour alone, by default** | With Colorblind **off**, every colour-coded fact also has a shape, letter, icon or word: side (hero/foe/boss), element of a hit, rarity, status, ready/not-ready slot, meter full | [WCAG 1.4.1](https://www.w3.org/WAI/WCAG22/Understanding/use-of-color.html); GAG Basic; Apple "Differentiate Without Color Alone"; POL-B5 | 🟡 status pips have icon + letter ✅; the element of a hit is named only with Colorblind on; ❓ rarity |
| C2 | **CVD simulation** | Screenshots of the battle, Summon, Shop and results pass under deuteranopia, protanopia and tritanopia simulation (ΔE₀₀ ≥ 15 between element pairs) | POL-B6; [Color Oracle](https://colororacle.org/) | ❌ "not checked through a CVD simulator" (BIBLE §31) |
| C3 | **One meaning per hue** | Element hues never reused for UI states | POL-B4 | 🟡 |

### 2.6 Motion and effects

| # | Rule | Pass test | Source | Status |
|---|---|---|---|---|
| M1 | **Reduce Motion covers everything** | With it on: no shake, lunge, zoom, cut-in sweep, parallax or background drift; marquees become ellipses; transitions are fades ≤ 0.2 s | [WCAG 2.3.3](https://www.w3.org/WAI/WCAG22/Understanding/animation-from-interactions.html); [XAG 117](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/117); Apple "Reduced Motion" label | 🟡 extensive (GG-T495). ❓ board twists/zoom, sky backdrops, combo meter |
| M2 | **Follows the system setting** | iOS Reduce Motion / Android "Remove animations" turns Reduce Motion on at first launch | XAG 112; [Apple HIG](https://developer.apple.com/design/human-interface-guidelines/accessibility) | ❌ not read |
| M3 | **No more than 3 flashes per second** | Clutch flash, crit bursts, Shatter, summon reveals, marquee flashes ≤ 3 per second, and no large saturated-red flash | [WCAG 2.3.1](https://www.w3.org/WAI/WCAG22/Understanding/three-flashes-or-below-threshold.html); GAG Basic "avoid flickering"; POL-E7 | 🟡 marquee flash is 0.32 s a cycle (≈ 3 Hz, small area); ❓ VFX |
| M4 | **Separate sliders** | Screen shake and effects amount each have their own 0–100% control | XAG 117 | ✅ Screen shake, Effects |
| M5 | **Effects never hide information** | No burst covers numbers or the active unit for longer than 0.4 s | POL-E8 | 🟡 |

### 2.7 Timing and pacing

| # | Rule | Pass test | Source | Status |
|---|---|---|---|---|
| P1 | **The turn window can be turned off or extended** | One setting (or turn mode) that has no real-time limit on the player's turn, or lets it be ≥ **10×** longer. Available from the first battle, with no penalty beyond scoring | [WCAG 2.2.1](https://www.w3.org/WAI/WCAG22/Understanding/timing-adjustable.html); XAG 107 (speed); [GAG Advanced "precise timing not essential"](https://gameaccessibilityguidelines.com/full-list/); APX "Slow It Down" | ❌ picking up a hero starts the real-time window and a forced drop ends it (BIBLE §2.0). No untimed option exists |
| P2 | **Game speed goes slower, not only faster** | Battle speed includes ×0.5 (or slower) | [GAG Basic: adjust game speed](https://gameaccessibilityguidelines.com/include-an-option-to-adjust-the-game-speed/) | ❌ ×1 / 1.5 / 2 / 3 |
| P3 | **Drag timer stays opt-in** | The 4 s drag timer is off by default and labelled with its time | GAG Basic | ✅ off by default (§2.9) |
| P4 | **Text at the player's pace** | Dialogue, story crawls, tutorial cards and level-up stats never auto-advance before the player taps, or auto-advance can be turned off. Tap shows the whole line first | [GAG Basic: progress text at own pace](https://gameaccessibilityguidelines.com/allow-players-to-progress-through-text-prompts-at-their-own-pace/) | 🟡 Level Up auto-advances after a hold; ❓ crawl speed control |
| P5 | **Pause anywhere** | Pause stops every clock (turn window, drag timer, tweens) in battle and in timed menus | XAG 117; BIBLE §11.3 | ✅ GG-T126 |
| P6 | **Banners last long enough** | Announcements that carry information stay ≥ **1 s per 6 words** at ×1 and can be re-read in the Log | XAG 117 (auto-updating content); GAG "allow reminder" | 🟡 billboard lines fade after 1.6 s ÷ speed; ✅ Log keeps them |

### 2.8 Audio and haptics

| # | Rule | Pass test | Source | Status |
|---|---|---|---|---|
| A1 | **No information by sound alone** | Every event with a cue also has a visual (BIBLE §31 "every important event also has text") | GAG Basic; XAG 103; APX "Second Channel" | ✅ by law; ❓ timer-running-out cue |
| A2 | **Separate volumes and mutes** | Master, music and SFX volumes, plus mute toggles | GAG Basic | ✅ (Settings has Master, Music, Sound and both mutes; BIBLE §31's "no toggle" is stale) |
| A3 | **Haptics: off by default, with a toggle** (owner) | If any vibration is added: a Settings toggle, **default off**, ideally an intensity slider; never the only channel for anything | [GAG Basic: toggle/slider for haptics](https://gameaccessibilityguidelines.com/include-toggle-slider-for-any-haptics/); owner | ✅ no haptics in code today. The rule applies when they are added |
| A4 | **Mono audio** | Nothing is panned to one side only. Honour system mono, or add a toggle if stereo panning is ever used | GAG Intermediate "stereo/mono toggle" | ❓ |

### 2.9 Cognitive load

| # | Rule | Pass test | Source | Status |
|---|---|---|---|---|
| G1 | **Interactive tutorial, skippable and replayable** | Drills teach by doing; can be skipped; any lesson can be replayed from Settings | GAG Basic "interactive tutorials"; GAG Advanced "replay instructions"; APX "Training Ground" | ✅ drills, Skip to Camp, Replay tutorials, Play a drill |
| G2 | **Skills fully explained** (owner) | Every ability's text states: **shape** (which tiles), **mode/filter** (who it hits), **numbers** (damage/heal formula or range at the hero's stats), **cost and recharge**, **status, its chance and duration**, and timeline effect (pushback/haste seconds). No term left undefined: each [keyword] links to its glossary entry | APX "Total Recall"; GAG "simple clear language"; GG-LAW-5 triad; POL-D1/D7 | ❓ audit every ability card against this list (catalog-driven, so it can be a test) |
| G3 | **Plain language** | Descriptions at roughly a grade-8 reading level, ≤ 20 words a sentence, active voice | [GAG Basic: simple clear language](https://gameaccessibilityguidelines.com/use-simple-clear-language/) | ❓ (`tools/` could score Prose texts) |
| G4 | **Objective reminder** | The current goal (stage objective, wave n/m, quest step) is visible or one tap away during play | [GAG Intermediate: reminder of objectives](https://gameaccessibilityguidelines.com/indicate-allow-reminder-of-current-objectives-during-gameplay/) | 🟡 wave n/m in the top bar; ❓ region rules |
| G5 | **Controls reminder** | How-to-play reachable from the pause menu mid-battle | GAG Intermediate | ✅ Settings › How to play (drill play hidden mid-battle) |
| G6 | **Interactive things look interactive** | Every tappable row leads with a kind icon. Nothing styled as tappable is dead | GAG Intermediate; POL-D6 | ✅ GG-T712 kind icons |
| G7 | **Practice without failure** | A no-loss practice mode exists | GAG Intermediate; APX "Training Ground" | ✅ Play a drill (practice) |
| G8 | **Difficulty can be changed at any time** | Difficulty options reachable mid-campaign, with no lockout | GAG Basic/Intermediate; APX "House Rules" | 🟡 §22.9 scaffolding |
| G9 | **Undo** | The last move can be undone before it resolves (Confirm before acting covers abilities) | APX "Undo Redo"; Into the Breach | 🟡 Confirm before acting ✅ (default on); ❓ move undo |
| G10 | **Settings saved and shown in game** | All settings persist; accessibility features are listed in-game and on the store page | GAG Basic (×2) | ✅ settings.json; ❌ no store/website list yet |

### 2.10 Input alternatives

| # | Rule | Pass test | Source | Status |
|---|---|---|---|---|
| I1 | **Single-pointer move** | A hero can be moved without dragging: **tap hero → tap destination (path preview) → tap again or Confirm**. Same rules and pincers as a drag | [WCAG 2.5.7 Dragging Movements (AA)](https://www.w3.org/WAI/WCAG22/Understanding/dragging-movements.html); XAG 107 (avoid path-based gestures); APX "Do More With Less" | ❌ movement is drag-only (Terra Battle core). Diagonal slip (§25.16) stays a drag-only bonus, which is allowed if not required |
| I2 | **Same input everywhere** | Every menu and battle action is reachable by touch alone (no hardware button required) | GAG Basic | ✅ |
| I3 | **Controller / keyboard / switch** | Menus and battle work with a game controller and keyboard (desktop builds, iPad with controller), with a visible focus ring that is never hidden under other UI | XAG 107, XAG 112; WCAG 2.4.7/2.4.11 | ❓ |
| I4 | **Screen reader for menus** | VoiceOver/TalkBack can read and activate every menu row, button and the InspectCard text, through Unity's Accessibility module (`AccessibilityHierarchy`, UI Toolkit supported, in Unity 6000.4) | [GAG Intermediate: screenreader support for mobile](https://gameaccessibilityguidelines.com/ensure-screenreader-support-for-mobile-devices/); [Unity screen reader docs](https://docs.unity3d.com/Manual/accessibility/screen-readers-get-started.html); Apple "VoiceOver" label | ❌ module is in the manifest, unused |
| I5 | **No speech or motion input required** | Never needed | GAG Basic | ✅ |

### 2.11 Settings that should exist (summary; gaps in §4)

Text size up to 200% · follow system text size · high-contrast theme · colorblind (exists) · Reduce Motion that
follows the system (exists, not system-aware) · screen shake and effects sliders (exist) · battle speed including
slower than ×1 · **untimed turns / turn-time multiplier** · drag timer (exists, off) · confirm before acting (exists) ·
tap-to-move · UI scale above 100% · thumb margin / left-handed layout (thumb margin exists) · haptics toggle (when
haptics land, default off) · mute and volumes (exist) · auto-advance text on/off · hold time for InspectCard ·
**accessibility step on first launch** (text size, Reduce Motion, colorblind, timer off) · settings reset to defaults.

---

## 3. How to audit a screen

**Order.** Battle → Hub → Heroes (Status, Equip, Abilities/Skill hex) → Shop/Craft → Summon → Stage select → Dialogue
and crawl → Results/Level Up → Settings → front door (Splash, Create Profile, Main Menu). That is the order of
playtime. One game instance at a time (`--replace`).

**Devices.**

| Device preset | Why | Must pass |
|---|---|---|
| `iphone17pro` (1206 × 2622, safe 186/102) | Reference phone, Dynamic Island, tall 9:19.5 | Everything |
| `pixel9` (1080 × 2424, safe 110/63) | Android gesture bar, slightly narrower dp | Everything; L6 gesture zones |
| `ipad` (1640 × 2360, safe 72/60) | Wide stage, two-column ability bar (GG-T523) | Layout, T3 tablet size (24 mm), no dead bands |
| (extra) 480 × 800 at 160 dpi | Worst-case `TypeFloor` | X1 floor only |

**Per screen, capture:**
1. **Screenshot at Text size 100% and 130%** (and 200% once it exists): `python tools/scenario.py <preset> --device <d>`,
   then `call setting key=textsize value=130`. Look for clipping, two-way scrolling, marquee on essential text.
2. **Layout audit:** the automation `layout` endpoint (POL-C2 sizes, POL-C5 safe area, overflow). Scripted versions:
   `python tools/harness.py a11y` (Settings, Camp, Merchant, results, battle), `aspects` (every ratio),
   `safeareas` (notch + gesture bar).
3. **Thumb map:** overlay the bottom 45% line and the top/bottom 10% edge bands on the screenshot. Mark every control
   with how often it is used (per turn / per visit / rare). Any per-turn control above the line fails L1; any edge
   control under 11 mm fails L4.
4. **Contrast:** `tools/polish.py` measures POL-B2 from the screenshot plus `PolishAudit` text colours. Run it per
   **menu theme** (`setting key=menutheme value=<name>`), not only the default. Sample the lightest stop behind each
   label. Non-text items (meters, frames, keylines) need a manual 3:1 sample until POL-B3 is automated.
5. **Colour-blind pass:** run the screenshot through a deuteranopia/protanopia/tritanopia simulator (Color Oracle, or a
   script in `tools/`) with Colorblind off, then on. Every colour-coded fact must still be readable (C1, C2).
6. **Reduce Motion pass:** `setting key=reducemotion value=1`, record video (`-capture video`). Look for anything that
   still shakes, sweeps or zooms, and count flashes per second on bursts (M1, M3).
7. **Tap count:** script the screen's top 3 tasks with `clicktext`/`tap` and count the taps (E1, E2).
8. **Target spacing:** from the layout dump, the gap between neighbouring hit boxes (T2). This is POL-C3, worth
   adding to the `layout` endpoint.
9. **One-handed run** (on a real phone, by a person): play one battle turn and one camp task with only the right thumb,
   then only the left with the thumb margin set. Note every regrip.
10. **Record** the result per screen and rule ID (L1…I5) in a table under `artifacts/a11y/<date>/`, with the screenshot
    path, so a regression shows up as a changed cell.

---

## 4. Gaps in GridGame's current settings vs the guides

Current Settings (Settings/SettingsScreen.cs, 2026-11-27): Gameplay (battle speed ×1–3, drag timer, confirm before acting, pincer
close-ups, turn mode), Graphics (quality, frame cap, VSync, screen shake, effects), Display (window mode, resolution,
thumb margin on mobile, UI scale 80–100%), Sound (master, music, SFX, both mutes, mute when unfocused), Accessibility
(text size 100/115/130, colorblind, reduce motion, windows theme, font, text wrapping), How to play (replay tutorials,
play a drill).

| Gap | Guides asking for it | Effort | Unlocks Apple label |
|---|---|---|---|
| **No untimed / longer turn option**; battle speed has nothing below ×1 | WCAG 2.2.1, GAG Basic (speed), GAG Advanced (timing), XAG 107, APX Slow It Down | Low–medium: a turn-time multiplier (×1/×2/×4/∞) on `TurnWindowSeconds`, plus ×0.5 speed | — |
| **Drag-only movement** | WCAG 2.5.7 (AA), XAG 107, GAG Basic (simpler controls) | Medium: a tap-tap-confirm path into the same `MoveCommand` | Voice Control (needs tappable targets) |
| **Text size caps at 130%** | WCAG 1.4.4, XAG 101 (200%), GAG Advanced | Medium: add 150/175/200 to `GuiType`. The 28-characters-a-line cap and the battle's fixed bands need a reflow rule | **Larger Text** |
| **System settings ignored** (font scale, Bold Text, Reduce Motion, Increase Contrast) | XAG 112, XAG 102, Apple HIG | Low–medium: Unity `AccessibilitySettings` (font scale, bold text) on iOS/Android; Reduce Motion needs a small native call | Larger Text, Reduced Motion |
| **Contrast audited on one theme**; no high-contrast theme | WCAG 1.4.3/1.4.11, XAG 102 (7:1 mode), GAG Intermediate | Low to audit, medium for a High Contrast theme preset | **Sufficient Contrast** |
| **Element of a hit is colour-only unless Colorblind is on**; no CVD simulation done | WCAG 1.4.1, GAG Basic, POL-B5/B6 | Low: always print the element word (or icon) on elemental hits | **Differentiate Without Color Alone** |
| **UI scale only shrinks** (80–100%) | XAG 107 (resize targets), GAG Intermediate | Low–medium: allow 110–125% on battle controls | — |
| **No screen reader** | GAG Intermediate (mobile), XAG 106, Apple label | High for battle, medium for menus (Unity Accessibility module, UI Toolkit supported) | **VoiceOver** (only if every common task works, which means battle too) |
| **No first-launch accessibility step** | XAG 112 (accessibility settings before play) | Low: one page in Create Profile with Text size, Reduce Motion, Colorblind and turn timer | — |
| **No haptics toggle** | GAG Basic | None today; required the moment haptics land (default off, owner) | — |
| **Guard button outside the thumb zone** | POL-C4, Hoober | Low–medium: move Guard into the ability row or the bottom band | — |
| **Auto-advance on Level Up / text** can't be turned off | GAG Basic (own pace) | Low | — |
| **No accessibility list on store or website** | GAG Basic, Apple Nutrition Labels, EAA (if IAP in the EU and not a microenterprise) | Low | All labels need it |

**Apple labels GridGame could claim today, after a check:** Dark Interface (most themes are dark), Reduced Motion
(once M1 is verified on every common task, ideally system-aware). **After cheap work:** Differentiate Without Color
Alone (element words always on), Sufficient Contrast (audit all themes or ship a high-contrast one). **Larger Text**
needs 200%. **VoiceOver / Voice Control** need the screen reader and tap-to-move work.

---

## 5. Mobile games worth copying

- **Into the Breach** (mobile, Netflix): grid tactics, every enemy attack shown before it happens, one free turn
  reset per battle, no timers. Model for G9 undo and E5 previews.
- **Fire Emblem Heroes**: portrait grid tactics played one-handed. Units move by drag *or* tap-then-tap. Model for I1.
- **Marvel Snap**: portrait, all play in the bottom half, hold or tap a card for full rules text. Model for L1 and E4.
- **Slay the Spire** (mobile): no timers, every keyword expandable in place. Model for G2 and P1.
- **Call of Duty: Mobile / Diablo Immortal**: cited in XAG 107 for resizable and repositionable touch controls, layout
  presets and slide-to-cancel. Model for T7.
- **Terra Battle** (GridGame's ancestor): the 4 s drag timer was its core pressure. GridGame keeps it opt-in (P3);
  the held-turn window is the remaining timing gap (P1).

---

## Sources

- Game Accessibility Guidelines, full list: https://gameaccessibilityguidelines.com/full-list/
- Xbox Accessibility Guidelines v3.2: https://learn.microsoft.com/en-us/gaming/accessibility/guidelines (XAG
  [101](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/101),
  [102](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/102),
  [107](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/107),
  [112](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/112),
  [117](https://learn.microsoft.com/en-us/gaming/accessibility/xbox-accessibility-guidelines/117))
- AbleGamers APX: https://ablegamers.org/our-programs/accessibility-unlocked/apx/
- Apple HIG: [Accessibility](https://developer.apple.com/design/human-interface-guidelines/accessibility),
  [Layout](https://developer.apple.com/design/human-interface-guidelines/layout),
  [Typography](https://developer.apple.com/design/human-interface-guidelines/typography),
  [Designing for games](https://developer.apple.com/design/human-interface-guidelines/designing-for-games)
- Apple Accessibility Nutrition Labels: https://developer.apple.com/help/app-store-connect/manage-app-accessibility/overview-of-accessibility-nutrition-labels
- Android accessibility: https://developer.android.com/guide/topics/ui/accessibility/apps ; gesture navigation:
  https://developer.android.com/develop/ui/views/touch-and-input/gestures/gesturenav ; Material 3:
  https://m3.material.io/foundations/designing/structure
- WCAG 2.2: https://www.w3.org/TR/WCAG22/ ; WCAG2ICT: https://www.w3.org/TR/wcag2ict-22/
- CVAA: https://www.gamedeveloper.com/business/demystifying-cvaa
- EAA: https://igda-gasig.org/what-and-why/demystifying-eaa-gpsr/ ;
  https://www.taylorwessing.com/en/insights-and-events/insights/2025/03/accessibility-in-the-gaming-industry
- Thumb reach: Hoober, https://www.uxmatters.com/mt/archives/2013/02/how-do-users-really-hold-mobile-devices.php and
  https://www.uxmatters.com/mt/archives/2017/03/design-for-fingers-touch-and-people-part-1.php
- Unity Accessibility module (screen readers): https://docs.unity3d.com/Manual/accessibility/screen-readers-get-started.html
- Colour-blind simulation: https://colororacle.org/
- Internal: BIBLE §2.0, §2.9, §9, §11, §26.5–26.7, §31; docs/POLISH.md; tools/harness.py (`a11y`, `aspects`,
  `safeareas`); tools/scenario.py devices.

*Notes on confidence: the XAG, GAG, APX, Apple Nutrition Label and Unity facts were read from the linked pages on
2026-11-27. Apple HIG pages render client-side and were not readable by the fetcher, so the 44 pt / 17 pt / 11 pt
figures are Apple's long-standing published values. Check them against the live page before quoting externally. The
game examples in §5 describe well-known designs; confirm the specifics before using them in a pitch.*
