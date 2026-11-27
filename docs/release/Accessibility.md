# Accessibility and the App Store Accessibility Nutrition Labels (GG-T1553)

What the game supports today, mapped to Apple's Accessibility Nutrition Labels, with an honest answer and the evidence
for each. Checked against the repo on 2026-11-27. The full accessibility spec is [BIBLE section 31](../BIBLE.md); the
store-readiness row is S7 in [StoreReadiness](StoreReadiness.md). The answers are entered by the owner in App Store
Connect (GG-T1539); read Apple's current evaluation criteria for each label there before ticking it, because the
criteria change and this table only says what the game does.

Rule used here: a label is claimed only when the repo shows the feature working across the common tasks of the game
(menus and battle). "In-app setting" is not the same as following the system setting; where the game ignores the
system setting, that is said.

## What Settings > Accessibility offers

Settings (`App/Screens/HubScreens.cs`, section "Accessibility", plus UI scale under Display and the Sound section):

| Setting | Values | Persisted as | Applied by |
|---|---|---|---|
| Text size | 100 / 115 / 130 % | `SettingsSave.TextScale` | `TextSize.Apply` > `GuiType` (`App/UI/Tokens.cs`), live on every screen |
| Colorblind palette | on / off | `SettingsSave.ColorblindMode` | `Palette.Colorblind`, root class `colorblind` (`GameApp.ApplySettings`) |
| Reduce motion | on / off | `SettingsSave.ReduceMotion` | `BattleScreen.ReduceMotion`, `Tween.Calm`, marquee ellipsis |
| UI scale | 80 / 90 / 100 % | `SettingsSave.UiScale` | `GameApp.FitStage` |
| Master / music / sound volume, mute music, mute sound | steps, toggles | `SettingsSave` | `Audio.Apply` |
| Menu theme (Classic, Cyberpunk, Ember, Verdant, Slate, Dark) | cycle | `SettingsSave.MenuTheme` | `Art.ApplyTheme`, `Ui.ApplyMenuTheme` |

Always on, no setting: a 12 screen-px / 9 dp minimum text size (`TypeFloor`), 132 design-px minimum touch targets
(`--size-target`, about 44 dp, checked by the layout audit POL-C2), hold-to-read InspectCards on icons and units, status
pips that carry an icon and a letter as well as a colour, and a text event for every important audio event.

## Label by label

| Apple label | Claim? | Evidence and gap |
|---|---|---|
| VoiceOver | **No** | The UI is Unity UI Toolkit drawn into one surface. No accessibility nodes, labels or roles are exposed to iOS (no `AssistiveSupport` / accessibility hierarchy use anywhere under `Assets/GridGame`, nothing in `Packages/manifest.json`). A drag-and-pincer board game with timeline reading is also not yet reachable by screen reader. Do not claim. |
| Voice Control | **No** | Same reason: Voice Control names come from accessibility elements, and the game exposes none. Controls are touch (tap, drag, hold). Do not claim. |
| Larger Text | **No** | The game has its own Text size (100 / 115 / 130 %) and does not follow iOS Dynamic Type or any system text-size setting. The largest step is 130 %, below the larger sizes Apple's criteria describe. Wrapping and one-line marquees keep labels from clipping (BIBLE 26.6), but that is not Dynamic Type. Do not claim unless the cap is raised and the system size is honoured (not filed). |
| Dark Interface | **Yes, with a caveat** | Every default window is deep blue with white text on a dark stage; the **Dark** menu theme is a flat near-black window set with light text (BIBLE 31, `theme-dark`). The caveat: it does not switch with the iOS appearance setting, it is dark by design. Claim on that basis. |
| Differentiate Without Color Alone | **Not yet** | Strong where it was designed in: status pips carry icon plus letter, mana reads by fill versus empty, the colorblind palette names the element on elemental damage numbers and gives sides a blue/orange pair, tamed/wanderer/guest marks carry a tag. Open gaps (BIBLE 31 status): the palette has not been checked in a colour-vision-deficiency simulator, and combo-rank and keyline colours have no audit that a non-colour cue exists everywhere. Claim after that audit; today say "partial" in copy only. |
| Sufficient Contrast | **Not yet** | Text tokens are checked for 4.5:1 against the lightest Classic window stop (#2C46AA), and element and team colours used as text are lifted by `Palette.OnWindow` (GG-T495, GG-T525). Not audited: other art colours used as text (for example role badges), the other menu themes, and text over art. BIBLE 31 marks contrast as partial. Claim after the audit covers every theme. |
| Reduced Motion | **Yes in-app, system setting not read** | Reduce motion zeroes lunges, shakes and jitter, skips cut-ins, prefab VFX and particle bursts, replaces slams and strobes with fades (combo meter), calms shared tweens everywhere and ellipsizes marquees (BIBLE 31, `BattleScreen.*` `ReduceMotion` branches, `Tween.Calm`). It does not read iOS Reduce Motion. Claim if Apple accepts an in-app switch for the label; the safer route is a small ticket to default the setting from the system value on first run (not filed). Also note S and higher combo ranks strobe white until Reduce motion is on. |
| Captions | **Not applicable** | The game has no spoken dialogue and no voiced audio: `Resources/Audio` holds music and sound effects only; dialogue is on-screen text. No video. Nothing to caption; do not claim a feature that does not exist. State "no audio carries information that is not also on screen" instead (see next row). |
| Audio Descriptions | **Not applicable** | No video or cinematics. Sound is music and effects, and every important event also has text (combat text, combat log, announcements, BIBLE 31 Audio). |

## Other support worth stating

- Sound-independence: music and sound can be muted separately, and the game is fully playable muted (events are
  mirrored as text).
- Touch: 44 dp minimum targets everywhere including the battle info tabs (GG-T524); hold-to-read tooltips (GG-T252).
- Minimum text size on small low-dpi phones (GG-T551).
- No haptics are used, so there is nothing to disable.
- Verification: `python tools/harness.py a11y` prints the touch-target and text-size audit per screen; automation
  `setting key=textsize|colorblind|reducemotion` drives each setting.

## Summary for the App Store Connect answers

Claim now: **Dark Interface**. Defensible but confirm against Apple's criteria: **Reduced Motion**. Not applicable, leave
unclaimed: **Captions**, **Audio Descriptions**. Do not claim: **VoiceOver**, **Voice Control**, **Larger Text**.
Not yet (audit first): **Differentiate Without Color Alone**, **Sufficient Contrast**.

The gaps that would flip a No are separate work, none filed by this ticket: expose UI Toolkit elements to iOS
accessibility (the only route to VoiceOver and Voice Control, and large), honour the system text size beyond 130 %,
default Reduce motion from the system setting, and finish the colour-vision and contrast audits.

## Store copy (accessibility paragraph)

> Built to be comfortable for more people. Choose a larger text size, switch on a colorblind-safe palette that tells
> heroes and foes apart by blue and orange and names the element on every damage number, and turn on Reduce Motion to
> replace shakes, slams and flashes with gentle fades. Status effects always show an icon and a letter, not just a
> colour; every important event also shows as text, music and effects mute separately, and every button is a
> large touch target. Hold any icon to read what it means.

Check before pasting: it promises nothing about VoiceOver, Voice Control or system text size, and it says nothing the
table above does not support.
