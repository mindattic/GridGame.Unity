# GridGame design system (GG-T2044)

The one place a screen's look and behaviour is decided. It is written down from what already ships (`App/UI/Tokens.cs`, `Resources/UI/Theme.uss`, `App/UI/Ui.cs`) and from the owner's decisions in [BIBLE §9.0](BIBLE.md#90-battle-hud-rows-gg-t1808-owner-2026-11-27), [§11.5](BIBLE.md#115-the-visual-language), [§11.6](BIBLE.md#116-vector-shapes--the-shapes-standard-gg-t735-gg-t736-gg-t737), [§26.6](BIBLE.md#266-units-and-overflow-gg-t272-gg-t271-owner-2026-11-05) and [§26.7](BIBLE.md#267-the-menu-kit-gg-t454-owner-2026-11-20). The visual reference, with live states and the device table, is [docs/design/design-system/index.html](design/design-system/index.html) (it opens from disk). Input: the UX review, [docs/design/ux-review/REVIEW.md](design/ux-review/REVIEW.md) (GG-T1940).

Sibling specs, written in parallel:
- **Words**: [COPY_STYLE.md](COPY_STYLE.md) and `docs/design/copy-style/` (GG-T2045). Voice, casing, button verbs, one name per concept, label length per role.
- **Icons**: [ICONS.md](ICONS.md) and `docs/design/icon-set/` (GG-T2046). One icon style, sizes per role (inline 32, button 48, row 64, nav 72) and the inventory.

This file decides layout, tokens, components and states. Where it names a label it defers to the copy guide, and where it names an icon it defers to the icon spec. Making the tokens the only source in code (the lint) is GG-T2047.

The theme is **Manapunk** (§11.5): smoked-glass panes, pewter rails, one mana-blue accent. The values below are Manapunk's. The other window themes keep the same sizes and change only colour.

## 1. The owner's rules (non-negotiable)

| # | Rule | Decided in | Built by |
|---|---|---|---|
| R1 | **Hamburger top-left, gold top-right.** The hub and the battle carry the hamburger in the top-left corner and the gold count in the top-right corner. | §9.0 (GG-T1808, GG-T1904) | `RowIcon.HamburgerButton` (HubScreen, BattleScreen); `Ui.Currencies`, `.header-gold` right-aligned |
| R2 | **Sub-pages keep Back top-left** and have no hamburger. Home (the house) sits after Back only when the page is more than one page above the hub. | §9.0 (GG-T1904), §11.3 (GG-T1335) | `Ui.Header(title, onBack, …, onHome)` |
| R3 | **Select, then confirm, shown pressed in.** A first tap selects (`ui.select`); a tap on the selected thing runs it (`ui.confirm`). The selected state is a darker inset well, a bright 3-px frame with a dark top edge and a 3-px sink at 98.5 %, never colour alone. Nothing starts selected on touch. | §11.5 (GG-T1923, GG-T1942) | `Ui.MenuRow`, `Ui.ConfirmTap`, `Ui.Tappable(…, confirmTap: true)`, `Ui.Button(…, confirmTap: true)`; `.menu-row--selected`, `.tap--selected` |
| R4 | **No hand cursor on mobile.** The pointing hand is for PC builds and the editor only. On a phone no row, note or label keeps a gutter for it. | §11.5 (GG-T1923) | `Ui.ShowsCursor`, `.no-cursor`, `Kit.Indent` (8 px on touch, 72 on PC) |
| R5 | **No truncation.** Text shrinks to fit (down to the 30-px floor), then wraps, hyphenating cleanly, or for a single line becomes a marquee. No ellipsis except under Reduce Motion, no clipped glyph, no word broken by hand. | §26.6 (GG-T305, GG-T711) | `TextFit`, `Marquee`, `LineBreaker` (automatic on every label) |
| R6 | **Every arrow is a filled, outlined shape**: a Bezier curve or an orthogonal path (right angles, rounded corners), with the head part of the same outline. No stroked lines, no double strokes, no font arrows. | §11.6 (GG-T735 to GG-T737), §9.0 (GG-T2118) | `Shapes.Arrow`, `Shapes.BezierArrow`, `Shapes.OrthoArrow`, `Shapes.OrthoPathArrow`, `Shapes.DashedArrow` |
| R7 | **Touch targets are at least 132 × 132 design px** (44 pt on the reference phone), with at least 24 px between neighbours. | POLISH POL-C2, POL-C3; §26.7 | `Size.Target`, `--size-target`, `Kit.Row`, `Space.TargetHalfGap` |
| R8 | **30-px text floor.** No text is drawn below 30 design px, and the live floor rises further on small or low-dpi screens (12 screen px or 9 dp). All GUI text is one size, `TypeScale.Base` (40 to 56 px). Emphasis is weight and colour, never size. | §26.6 (GG-T770, GG-T1878), POL-A2 | `TypeFloor.ShrinkMin` = 30, `TypeFloor.ShrinkFloor`, `GuiType` |
| R9 | **Hero thumbnails; full art only on tap.** Every small actor image is the baked 320-px thumbnail. The full 1024 portrait appears only in story scenes, reveals, and when the player taps a thumbnail. | §12.1 (GG-T713, GG-T1804) | `Ui.Thumb`, `Ui.PortraitThumb`, `Art.Thumb`, `ArtPreview.Open` |

## 2. Tokens

Code reads `Tokens.cs`; USS reads `Theme.uss` `:root`. A literal colour, size, spacing or duration in a screen is a bug (POL-A1, POL-B1, POL-C1, POL-E1). `tools/polish_lint.py` counts `new Color(…)`, `Color.white` and the like, a hex colour string, a `px` length in a C# string, a literal font size, and a literal duration passed to `Tween` or `WaitForSeconds`. Only dev-only screens may keep literals, each listed with its reason in the lint's `DEV_ALLOW` (WindowEditorScreen, BreedGalleryScreen, DevOptions).

### 2.1 Colour roles (Manapunk)

| Role | Value | Token | Use |
|---|---|---|---|
| Stage | #0C0C16 | `.theme-manapunk .stage` | behind everything (the living backdrop paints over it) |
| Pane | #22203A to #100F1C, 0.94 | theme gradient (`WindowThemes`) | every window, sheet and dialog |
| Pane flat | #1A1930 at 0.94 | `.window--plain`, `.tab` | plain windows, inactive tabs |
| Raised | #2A2840 | `.btn`, `.tab--active` | a button's face, the active tab |
| Rail | #60646F (dark #34363F, light #8C909C) | `Palette.Pewter`, `PewterDark`, `PewterLight` | frames, dividers, gauge sleeves, button rims |
| Accent (contained mana) | #7590C8 (light #ACA5D3, deep #41386F) | `Palette.OrbLit`, `OrbLitHighlight` | the primary button, a window's top edge, the active tab and nav tab, the pressed tint. Used sparingly: it means "this matters" |
| Text | #E6E6EE | `--color-text` (#F4F4F6 outside Manapunk) | all body text, above 6:1 on every pane |
| Text dim | #A4A3BC | `.dim` | meta, captions, inactive labels; floor 3:1 (`WindowTone.CaptionContrast`) |
| Gold | #FFD959 | `--color-gold`, `Palette.Gold` | currency, and the header subtitle |
| Good, Bad, Warn, Info | #73F294, #FFAAA2, #FFC45C, #DBE0FF | `--color-good`, `-bad`, `-warn`, `-info` | outcome text, never alone (POL-B5) |
| Term | #9CC7FF | `Palette.Term` | a tappable glossary [term] |
| Danger | #3C1A26 face, #963659 rim, #FFB0B8 text | `.btn--danger` | destructive buttons only |
| Hero, Foe, Boss | #78ABFF, #EB454A, #DB66FF | `--color-hero`, `-enemy`, `-boss` | team keylines and timeline marks, always with a shape |
| Veil | #05050C at 0.72 | `.veil` | behind every modal and sheet |

Elements (`Palette.Element`), rarity (`--color-rarity-*`) and gauges (`--color-gauge-*`) are gameplay colours and stay the same in every theme. Each hue family has one meaning (POL-B4). The reference page lists every USS colour token.

**Base-theme surfaces (GG-T2135).** Every colour a Theme.uss rule outside `:root` uses is a `--color-*` token: the stage and letterbox (`--color-stage`, `--color-letterbox`, `--color-black`, `--color-clear`), the selected well (`--color-well`, `--color-well-edge`), hover and press tints (`--color-hover`, `--color-press`, `--color-card`, `--color-card-selected`, `--color-mana-tint`, `--color-mana-faint`), the button family (`--color-btn`, `-lip`, `-hover`, `-pressed`, and the same for `-primary` and `-danger`, plus `--color-btn-primary-rim`), chrome (`--color-chrome`, `--color-toast`, `--color-badge`, `--color-tab`, `--color-tab-active`, `--color-label`, `--color-alert`, `--color-scrollbar`, `--color-thumb`, `--color-gauge-rim`, `--color-pip-rim`, `--color-title`, `--color-mana-core`, `--color-continue`), and the team keylines that had no variable (`--color-tamed`, `--color-wanderer`, `--color-cb-hero`, `-foe`, `-boss`). USS cannot add alpha to a variable, so each translucent tint is its own token. A `.theme-*` block still overrides by selector.

Palette additions for code: `Palette.Black`, `Palette.Gray` (the fallback when a content colour does not parse), `Palette.Missing` (magenta, for a colour that must never be missing), `Palette.Overbright` (3, 3, 3: an image flashes overbright and eases back to White), `Palette.FoeTint` (a foe's cut-in portrait), `Palette.PlateRim` (a dialogue nameplate whose speaker is not talking) and `Palette.ManalithRock`.

Grouped code colours (GG-T2139), each at the exact value its call sites drew before: `Palette.Vfx.*` holds the battle effects (selection and intent glows such as `SelectGlow`, `Harmful`, `Helpful`, `Threat`; element and status hues such as `Fire`, `Burning`, `Frozen`; sparks, rings, marks, cut-in bands, the board's rock, plate and amplifier, combat text and its outlines; and the vfxstyle fallback hex strings), `Palette.Combo.D` to `SSS` the combo rank badge, `Palette.Backdrop.*` the tints over a screen's background art, `Palette.Scene.*` generated textures and rigs (placeholder art, the card back, the moon's albedo, the fog banks, the coach shade), and `Palette.SkillHex.*` the skill hexagon's own palette. `Palette.Clear` is fully transparent and `Palette.Grey(v, a)` a computed grey. A colour whose alpha is computed at runtime keeps that computation at the call site and tokenizes only the base: `Palette.Vfx.Spark.WithA(1f - k)`, `c.WithA(0.5f)`. TestMode's pacing waits are `Timing.TestModeSettle` and `Timing.TestModeRead`.

### 2.2 Type

- **One size**: `TypeScale.Base` (`--type-base`). `GuiType` computes it per screen to draw 14 dp, clamped to 40 to 56 design px, then multiplies by Settings → Text size (100, 115 or 130 %). Nominal 40.
- **Floor**: 30 design px (`TypeFloor.ShrinkMin`), raised live to 12 screen px or 9 dp (`TypeFloor.ShrinkFloor`). Shrink-to-fit never goes under it.
- **Roles by weight and colour**: Body (M PLUS Rounded 1c Bold); Heavy (ExtraBold: titles, headings, button and tab labels, names); Display (Cinzel: the wordmark and celebration titles only). A screen uses at most these three (POL-A4), and never faux bold (POL-A3).
- **Builders**: `Ui.Heading`, `Ui.Name`, `Ui.Para` (anything that wraps), `Ui.Caption` (one line of meta), `Ui.Note`, `Ui.Display`, `Ui.Section`.
- **Measure**: wrapped text is at most 720 px wide (`--measure-text`, about 48 characters, POL-A5).
- **Depth**: Manapunk draws a 1-px outline and no drop shadow.

### 2.3 Space and size

- **Space** (`Space`, `--space-*`): 4, 8, 12, 16, 24, 32, 48, 64, 96. 12 exists only as half of the 24-px gap between targets. `--space-16-framed` (13) is a selected card's padding: 16 less its 3-px frame, so selecting it does not move its content.
- **Kit** (`Kit`): page gutter 24 (`Fit.Edge`), gap 16, window padding 16 / 24, row 132, header 150, nav 132, indent 8 (touch) or 72 (PC), card portrait 120, detail pane at most 45 % of the column.
- **Size** (`Size`): target 132, icon roles per [ICONS.md](ICONS.md) (inline `IconInline` 32, button `IconButton` 48, row `Icon` 64, nav `IconNav` 72; USS `--size-icon-inline`, `--size-icon-button`, `--size-icon`, `--size-icon-nav`), icon M 56, icon S 40, chip 50, thumb 120, avatar 96, party slot 180, medallion 140, slot 196, cell 170, card 300, portrait 360 × 400, art 420 × 620. Lines: underline 2, hairline 3, track 6, bar 8, gauge 14, HP bar 18.

### 2.4 Radii

| Thing | Manapunk | Base theme |
|---|---|---|
| Window, toast | 10 | 10 |
| Stage frame | 12 | 10 |
| Button, tab top corners, card, thumbnail | 8 | 16 (button) |
| Menu row, role badge, scrollbar | 6 | 6, 10 |
| Lab clamp, conduit | square | none |

Radii are tokens (GG-T2047): Theme.uss `--radius-N` and Tokens.cs `Radius.RN`, steps 3, 6, 8, 10, 12, 14, 16, 17, 18, 22; POL-C1 counts a literal `border-*-radius` in Theme.uss as a failure.

The page's token data (`docs/design/design-system/tokens.js`: colours, space, size, timing, radii and device shapes) is generated from the tokens by `python tools/design_tokens.py`; `tools/test.ps1` runs it with `--check`, which fails when the page is stale or Tokens.cs and Theme.uss disagree on the radii. Rerun it after changing a token.

### 2.5 Elevation

UI Toolkit has no box shadow, so elevation is **layer order plus edge light**, not blur.

| Level | Layer | What lives there | Edge |
|---|---|---|---|
| 0 | Stage | backdrop, board | none |
| 1 | Page | windows, lists, cards | rail rim, lit mana top edge |
| 2 | Chrome | header, bottom nav, footer, battle action row | a 2-px rail or mana border toward the content |
| 3 | Raised control | buttons | the dark lip it stands on (bottom border 7 px; small 5 px) |
| 4 | Overlay | toast, Inspect card | rail rim, mana top edge |
| 5 | Modal | veil, then sheet, Confirm or Art preview | the 0.72 veil behind; always the topmost Overlay child |

Pressing lowers a control by one level: the lip shrinks to 2 px and the face moves down by the difference (`translate 0 5px`).

### 2.6 Motion

- **Durations** (`Timing`): instant 0.08, quick 0.15, standard 0.25, emphasis 0.4, dramatic 0.7, cinematic 1.2 s. No random durations (POL-E3).
- **Easing** (`Ease`): Out, InOut, Back (overshoot on release), Snap (fast settle), Arc (0 to 1 to 0). No linear UI motion (POL-E2).
- **Press**: sink in 0.06 s ease-out, release in 0.14 s ease-out-back (`.pressable`). The first visible response lands inside 100 ms (POL-D3).
- **Screen change**: a 0.2 s fade through black (`Timing.ScreenFade`, `ScreenRouter`).
- **Reduce Motion** removes shakes, the thumbnail wobble (`ThumbWobble`), the conduit flicker and the marquee scroll (an ellipsis instead). Never more than 3 flashes a second (POL-E7).

## 3. Components and their states

Every tappable goes through `Ui.Pressable`, which sinks it and plays the `ui.press` tick on touch. The four states, in Manapunk:

- **Default**: at rest.
- **Pressed** (`.is-pressed`, while the finger is down): the control sinks. Buttons drop onto their lip; rows and cards scale to 95 to 98.5 % and darken.
- **Selected-pressed** (`.menu-row--selected`, `.tap--selected`; GG-T1923, GG-T1942): after the first tap of a select-then-confirm control. A darker inset well (black at 0.38), a 3-px frame in the frame colour with a dark top edge, a 3-px sink at 98.5 %. A button stays sunk on its lip with a dark top edge. One thing is selected per screen.
- **Disabled** (`.menu-row--disabled`, `.btn--disabled`): text and icon at 0.6 opacity (still 4.5:1). It does not sink, and a tap toasts the reason when there is one.

| Component | Builder | Classes | Select-then-confirm | Notes |
|---|---|---|---|---|
| Header (sub-page) | `Ui.Header(title, onBack, parent, out currencies, subtitle, onHome)` | `.header`, `.header-title`, `.header-sub`, `.header-gold` | no (navigation) | Back top-left, title, optional gold subtitle, currencies top-right (R1, R2) |
| Hub and battle top bar | `RowIcon.HamburgerButton` and `Ui.Currencies` | `.header-currencies` | no | hamburger top-left, gold top-right (R1) |
| Bottom nav | `Ui.NavBar(parent, current)` | `.kit-navbar`, `.kit-navtab`, `.kit-navtab--active` | no | Home, Campaign, Heroes, Summon, Shop. A locked tab is dimmed with a padlock and toasts its hint |
| Page body | `Ui.PageBody` | `#page-body` | n/a | content column, 24-px gutter, 16-px gaps |
| Window | `Ui.Panel(parent, title, fill)`, `Ui.Window` | `.window`, `.window-title` | n/a | padding 16 / 24; `fill` takes the leftover height |
| List | `Ui.ListPanel`, `Ui.List` | `.list`, `.list--fill` | n/a | scrolls inside itself; two lists split the height |
| Menu row | `Ui.MenuRow(label, value, onClick, parent, icon, enabled)` | `.menu-row`, `--selected`, `--disabled`, `--compact` | **yes**, always | kind icon first (`row.KindIcon`), label, value on the right (at most 55 %); 132 tall, 24 apart |
| Row note | `Ui.Note` | `.kit-note` | n/a | a dim sentence under a row, indented by `Kit.Indent` |
| Section | `Ui.Section` | `.kit-section` | n/a | heavy gold, flush left |
| Card | `Ui.Card(parent, classId, name, out col)` | `.card`, `.card-tap`, `.tap--selected` | when its tap runs an action (`Ui.Tappable(…, confirmTap: true)`) | thumbnail 120 (R9), heavy name, its buttons under its text (`Ui.Actions`) |
| Button | `Ui.Button(text, onClick, classes, parent, confirmTap)` | `.btn`, `--primary`, `--danger`, `--small`, `--icon`, `--disabled` | **yes** for an action with no confirm of its own (every `RowKind.Confirm` icon button). **No** for a button that opens its own Confirm, a dialog's answer, the battle commit button, navigation and steppers | the primary holds the mana fill; danger is wine red; one primary per screen (POL-C7) |
| Icon button | `RowIcon.IconButton(kind, …, tex)` | `.btn--icon` | Confirm kind: yes | 132 square; a 48 icon (`Size.IconButton`): the kind's set icon (`RowIcon.IconId`, `Art.UiIcon`) unless `tex` names another, per [ICONS.md](ICONS.md) |
| Tabs | `Ui.Tabs(names, active, onPick, parent, locked)` | `.tab`, `.tab--active` | no | the window below sits flush; the active tab has a 4-px mana top |
| Stepper | `Ui.Stepper(parent, value, onMinus, onPlus, …)` | `.btn--small` | no | minus, value, plus; each button a full target |
| Field | `Ui.Field(label, value, parent)` | `.kit-field` | n/a | read-only: dim label, body value |
| Detail pane | `Ui.DetailPanel` | `.window--detail` | n/a | grows to 45 % of the column, then scrolls |
| Footer | `Ui.Footer` | `.window`, `.kit-footer` | its button: yes, unless it confirms itself | total on the left, commit on the right, at the bottom, pinned above the nav |
| Empty state | `Ui.EmptyState(list, icon, text)`, `Ui.Empty` | `.kit-empty` | n/a | icon, one sentence, the next step (copy per COPY_STYLE) |
| Gauge | `Ui.Gauge`, `Ui.SetGauge` | `.gauge` | n/a | a glass tube in a pewter sleeve; never colour alone |
| Thumbnail | `Ui.Thumb(id, parent, size, thumbOnly)`, `Ui.PortraitThumb` | `.thumb` | a tap opens `ArtPreview` | the baked 320-px thumb up to 200 px; full art only on tap (R9) |
| Role badge | `Ui.RoleBadge` | `.role-badge` | n/a | role-colour rim plus a letter, never colour alone |
| Marquee | `Ui.Marquee` (automatic through `TextFit`) | `.marquee` | n/a | the one-line overflow fallback (R5) |

## 4. Sheets and modals

All of them sit on `GameApp.I.Overlay` above a `.veil` (#05050C at 0.72) that swallows taps, kept the topmost Overlay child. Back or Escape closes the top one.

| Surface | Builder | Shape | Dismiss | Confirm rule |
|---|---|---|---|---|
| Confirm dialog | `Confirm.Show(question, yes, onYes, onNo)` | a centred window 76 % wide: the question as a heading, actions right-aligned (Cancel, then Yes) | Cancel, Back, a second Back | single tap: the dialog is the confirm |
| Bottom sheet | the Abilities sheet (`BattleScreen.AbilitySheet`, GG-T1914, GG-T1920) | full width, rising from the thumb zone. Header with the hero thumbnail, name, HP and Close; tabs; a breadcrumb (`Abilities › Ninjutsu › Smokebomb`). The board stays visible behind it | Close, Back, a tap on the veil | rows are select-then-confirm; the commit button (Perform, Cast, Use) is single |
| Inspect card | `InspectCard.Attach(e, build)`, `InspectCard.Tap` | a floating card beside its anchor | lift the finger, tap elsewhere | press and hold inspects anything (POL-D1) |
| Art preview | `ArtPreview.Open(classId)` | the full 1024 portrait over the veil | a tap anywhere, Back | n/a (R9) |
| Toast | `GameApp.Toast(text, seconds)` | a small pane above the nav, never over content | times out (1.8 s) | n/a |
| Pause menu | hamburger, then `OpenMenu` | a sheet of menu rows | Resume, Back | rows are select-then-confirm |

A sheet rises from the bottom (`Timing.Standard`, `Ease.Out`) and drops away faster (`Timing.Quick`). A dialog fades in and scales up from 96 % (`Timing.Quick`, `Ease.Back`).

## 5. Layout grid and thumb zone

The stage is in design px and scales uniformly: at least 1170 × 2532 (`GameApp.DesignWidth`, `DesignHeight`), up to 1424 wide (9:16) or 2990 tall (9:23). The safe areas come off the top and bottom.

- **Columns**: one content column, `Fit.Column`, with a 24-px gutter (`Fit.Edge`) on each side. Inside it, a 4-column grid with 24-px gutters is the default for card grids, and the battle action row's three cells share the width evenly. Grids keep even gutters (POL-C8).
- **Vertical bands, top to bottom**: safe top, header or top bar (150), body (flex), footer (132, optional), nav (132), safe bottom.
- **Thumb zone**: primary actions sit in the **bottom 45 %** of the safe stage (POL-C4). Lists anchor to the lower half (GG-T1853). The commit button, the footer, the nav, the battle action row and the timeline (GG-T2118) live there. The top-left corner (hamburger, Back) is the hard-to-reach corner on purpose: those taps are deliberate, not frequent.

Per device shape, from `tools/devices.json` (the representative device of each shape; the reference device, `iphone15pro`, is in `iphone-island-393`). "132 px is" and "30 px is" convert design px into the device's own pt or dp.

| Shape | In target | Stage | Safe top / bottom | Thumb zone from y | 132 px is | 30 px is |
|---|---|---|---|---|---|---|
| iphone-notch-390 | yes | 1170 × 2532 | 141 / 102 | 1400 | 44.0 pt | 10.0 pt |
| iphone-island-393 (reference) | yes | 1170 × 2536 | 176 / 101 | 1418 | 44.3 pt | 10.1 pt |
| iphone-island-430 | yes | 1170 × 2536 | 161 / 93 | 1416 | 48.5 pt | 11.0 pt |
| iphone-402 | yes | 1170 × 2544 | 180 / 99 | 1426 | 45.3 pt | 10.3 pt |
| iphone-440 | yes | 1170 × 2542 | 165 / 90 | 1423 | 49.6 pt | 11.3 pt |
| iphone-air | yes | 1170 × 2541 | 189 / 95 | 1430 | 47.4 pt | 10.8 pt |
| galaxy-fhd | yes | 1170 × 2535 | 104 / 78 | 1398 | 40.6 dp | 9.2 dp |
| galaxy-qhd | yes | 1170 × 2535 | 91 / 68 | 1398 | 46.4 dp | 10.5 dp |
| galaxy-flip-main | yes | 1170 × 2860 | 95 / 72 | 1576 | 44.3 dp | 10.1 dp |
| galaxy-fold-cover | yes | 1170 × 2872 | 116 / 87 | 1584 | 36.4 dp | 8.3 dp |
| iphone-mini | no | 1170 × 2535 | 156 / 106 | 1406 | 42.3 pt | 9.6 pt |
| iphone-notch-428 | no | 1170 × 2532 | 129 / 93 | 1400 | 48.3 pt | 11.0 pt |
| galaxy-20x9 | no | 1170 × 2600 | 104 / 78 | 1434 | 40.6 dp | 9.2 dp |
| pixel-20x9 | no | 1170 × 2626 | 119 / 68 | 1460 | 46.4 dp | 10.5 dp |
| ipad | no | 1424 × 2532 (framed) | 51 / 43 | 1392 | 61.5 pt | 14.0 pt |

**Open question for the owner:** 132 design px is exactly 44 pt only on 390-pt-wide screens. On the 360-dp Galaxy S base models it is 40.6 dp, and on the Z Fold cover 36.4 dp: under Apple's 44 pt and Material's 48 dp. Holding 44 on every screen would need a target that grows on narrow screens (about 160 px on the Fold cover), the way `TypeFloor` already raises text. Until that is decided, 132 px stays the token and the narrow Galaxy shapes are the known exception. The text floor has the same shape, and `TypeFloor` already handles it live.

## 6. Do and do not

| Do | Do not | Rule |
|---|---|---|
| Put the hamburger top-left and gold top-right on the hub and in battle | Move the menu into the thumb's corner, or put gold on the left | R1 |
| Give every sub-page Back top-left | Add a hamburger to a sub-page | R2 |
| Select on the first tap and run on the second; show the selection pressed in | Run an action on the first tap of a list row; mark the selection by colour or glow alone | R3 |
| Lead each row with its kind icon at the left edge | Leave a cursor gutter on a phone, or show the pointing hand | R4 |
| Let text shrink, then wrap, then marquee | Cut text with an ellipsis, clip a value at the edge, or break a word by hand | R5 |
| Draw arrows with `Shapes`: filled, outlined, Bezier or orthogonal | Stroke a line twice, use a font arrow, or draw a bare stroked line | R6 |
| Make every tappable 132 × 132 with 24 px between | Ship a 20-px talent node or a text link as a tap target | R7 |
| Use `TypeScale.Base` and change weight or colour for emphasis | Use a smaller size for meta, or anything under 30 px | R8 |
| Show heroes as their thumbnail and open the full art on tap | Show a shrunken 1024 portrait in a list or on a card | R9 |
| Keep one primary button per screen, in the thumb zone | Stack three price buttons on every row; float the commit button at the top | POL-C4, POL-C7 |
| Take every colour, space and duration from a token | Write `new Color(…)`, `12px` or `0.3f` in a screen | POL-A1, B1, C1, E1 |
| Pin the footer and toasts above the nav | Let a footer or toast sit under the nav or over a row | REVIEW §2.3 |

## 7. What the UX review asks of the kit

From [REVIEW.md](design/ux-review/REVIEW.md), the findings that belong to the kit rather than to one screen:

- **The hand cursor on seven screens**: removed by R4 (built, GG-T1923). The review proposed a filled highlight with a 4-px left accent bar; the owner chose the pressed-in state instead, so the pressed-in state is the standard.
- **Hero, foe, selected and next actor told apart only by thin border colours** (REVIEW #5): every team and state mark also needs a shape (POL-B5). The battle token rework is its own story.
- **Full art shown without a tap** on Heroes · Status: R9.
- **Words broken mid-syllable** on Abilities, **gold clipped** on Summon history: R5.
- **A footer under the nav** on the Shard Store, **toasts over content**: §4 and §6.
- **One name per concept** (Diamonds, Shards, Mana Shards): the copy guide (GG-T2045).
