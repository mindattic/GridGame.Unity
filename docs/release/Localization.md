# Localization sizing (GG-T1551)

What localising GridGame would take, measured on `main` on 2026-11-27. The counts are regex heuristics (a string with a
space and three letters in a row), so read them as +/- 15%; they are a sizing, not an inventory. Glyph coverage was read
straight from the font cmaps (the same chain `tools/check_glyphs.py` uses).

## Verdict

- There is **no localisation layer today**: no string table, no `Loc.T(key)`, no locale setting. English is baked into
  `.cs` literals, content JSON and the Prose-sourced `Content/Text` files. Number formatting touches the current culture
  in only about 40 places.
- **Recommended launch set: English plus Spanish, French, German and Portuguese (Brazil).** All are fully covered by the
  bundled font chain, need no font or shaping work, and are the usual first wave for a mobile RPG. Add Russian and
  Polish second (covered by the body fonts; only the Cinzel title font lacks Cyrillic). **Defer Japanese, Korean and
  Chinese**: they need new fonts and an atlas strategy, so plan them as a second release. **Do not plan Arabic or
  Hebrew** (right-to-left needs UI mirroring and shaping that the UI stack does not give us for free).
- Effort: **about 4 weeks of engineering to make the game localisable, then 3-5 days of QA per Latin language plus
  translation cost** (below). The translation volume (about 68,000 words, 80% of it story and rules prose a translator
  must read in order) is the real driver.

## 1. Player-visible strings

| Source | Strings | Words | Notes |
|---|---|---|---|
| `Assets/GridGame/App/**/*.cs` (114 files) | ~470 | ~1,400 | Screens, buttons, tutorial, toasts; 145 are interpolated (`{}`) |
| `Assets/GridGame/Meta/**/*.cs` (46 files) | ~110 | ~410 | Wallet, shop, camp, bonds messages; 59 interpolated |
| `Assets/GridGame/Core/**/*.cs` (105 files) | ~225 | ~810 | Rules text (`SupportText`, difficulty, battle log); 164 interpolated, the hardest to translate |
| `Resources/Content/Text/*.json` (Prose story text) | ~3,360 | ~44,500 | `descriptions` 1,925 strings / 29.5k words, `conversations` 1,005 / 8.9k, `glossary` 210 / 3.1k, `quests` 97 / 1.3k, `crawls` 46 / 1.2k, `barks` 76 / 0.7k |
| `Resources/Content/*.json` (gameplay data) | ~3,550 | ~20,300 | `abilities` 545 / 6.5k, `items` 1,667 / 6.1k, `classes` 513 / 3.0k, `skillhex` 156 / 2.2k, plus `encounters`, `dialogues`, `stages`, `vfx` labels |
| **Total** | **~7,700** | **~68,000** | Single-word names (heroes, items, places) are not counted: probably another 1,500-2,500 short strings |

Notes:

- Story text already lives apart from gameplay JSON (BIBLE section 27.2, [PROSE.md](../PROSE.md)), so that half is keyed
  and can be translated per key. The gameplay-JSON half mixes ids, flags and display text in the same objects, so each
  file needs its display fields identified and overlaid.
- Names and glossary terms need a translator glossary first; the lore uses coined words (Manalith, Hollow, the tithe)
  that must be fixed once and reused.
- The ~800 `.cs` literals are small beside the content, but their interpolation makes them the long pole for engineering.

## 2. Font glyph coverage

`tools/check_glyphs.py` currently crashes in this checkout with a `UnicodeDecodeError` (a content file has a non-UTF-8
byte at offset 21449), so it reports nothing; fix the read (or open files with `errors="replace"`) before using it as the
gate for a new language. Coverage below is from the font cmaps, using the chain the game uses (Noto Sans, then Noto Sans
Symbols 2, then Outfit; Cinzel for titles; "Rounded" is the M PLUS Rounded preset):

| Script | Needed for | Body chain | Cinzel (titles) | M PLUS Rounded |
|---|---|---|---|---|
| Latin-1 + Latin Extended-A | es fr de pt it pl tr | 100% | 86% | 100% |
| Vietnamese stacked marks | vi | 100% | 2% | 100% |
| Cyrillic | ru uk | 100% | **0%** | 100% |
| Greek | el | 98% | 7% | 98% |
| Hiragana / Katakana | ja | **0%** | 0% | 98% |
| CJK Unified (about 21,000 code points) | ja zh | **0%** | 0% | **23%** (not enough for Chinese or full Japanese) |
| Hangul syllables | ko | **0%** | 0% | **0%** |
| Arabic | ar | **0%** | 0% | 0% |
| Thai | th | **0%** | 0% | 1% |

Findings:

- **CJK is missing, as expected.** Kana exists only in the optional Rounded preset and its kanji set is partial; Korean,
  Chinese, Arabic and Thai are in no bundled font. Each needs a new font (CJK Noto fonts run several MB each, more than
  the current font set), loaded as a fallback and ideally a dynamic atlas, plus an APK-size decision.
- **Cinzel lacks Cyrillic, Greek and Vietnamese**, so those titles fall back to Noto Sans and lose the display look: a
  design call, or a second display font for Russian.
- Latin languages need nothing new. German compounds and French word length are a layout matter, not a glyph one.

## 3. Layout and construction risks

Measured in `Assets/GridGame/**`:

- **Sentences built by concatenation and interpolation.** About 441 `"..." + value` concatenations and about 2,000 uses
  of `$"..."` / `string.Format` across App, Meta and Core. Word order, gender and agreement cannot be translated inside
  such strings; each becomes a key with named placeholders (`"{hero} learns {ability}"`). This is the bulk of the
  engineering, and the rules text in `Core/Rules` is worst because it assembles tooltips clause by clause.
- **Plurals.** About 49 hand-rolled `n == 1 ? ... : ...s` sites. Fine for English, wrong for Russian and Polish (three
  forms) and French (zero/one); they need a plural-category lookup per language.
- **Fixed widths.** About 21 literal pixel `style.width/height` sets and about 194 width/min/max assignments in screen
  code, built for English lengths. German and Spanish run 20-35% longer than English; expect clipped buttons and tab
  labels on the hub, shop and battle HUD. Truncation rules (`text-overflow`) appear in only about 5 places in the themes.
- **Case changes.** 50 `ToUpper` / `ToLower` calls are culture-sensitive (Turkish dotless i) and some drive UI labels;
  use invariant or pass the locale.
- **Numbers and dates** are mostly invariant today; localised separators need one `Format` helper.
- **Text baked into images** (title art, `Resources/Tutorial`, store assets) is not measured here and must be checked.
- **Save data and the Prose key scheme** are id-based, not text-based, so switching language would not touch saves.

## 4. Recommended approach

1. A `Loc` service: key -> string per locale (JSON in `Resources/Content/Locale/<lang>/`), English fallback, locale in
   Settings and the save, `{name}` placeholders, CLDR plural categories and a font-fallback hook.
2. Overlay the story text: `Content/Text` already has keys; add per-locale overlay files (produced from Prose or a
   translation export of the same files). Gameplay JSON display fields get an overlay of the same shape.
3. Extract the ~800 App/Meta/Core literals into keys, one screen at a time, converting concatenations to placeholder
   strings, with English output kept identical and pinned by a test.
4. Fix layouts: replace fixed widths on text containers with min-width and flex, and test with a pseudo-locale (every
   string +40% and accented) before any translation arrives.
5. Add a glyph gate per shipped language and a font fallback for any CJK later.

## 5. Estimate

| Work | Effort |
|---|---|
| `Loc` service, locale setting, fallback, plural rules, tests | 4-6 days |
| Extract ~800 code literals, convert concatenations (Core rules text is about half) | 8-10 days |
| Overlay loading for Content/Text and gameplay JSON display fields, export/import tooling | 4-5 days |
| Pseudo-locale and layout fixes on every screen | 4-5 days |
| Fonts, glyph gate (fix the `check_glyphs.py` read error), Cinzel fallback | 1-2 days |
| **Engineering total (one-off)** | **about 4 weeks** |
| QA per Latin language (read-through of every screen, layout) | 3-5 days each |
| Translation per language (~68k words plus names) | External cost: at a typical 0.08-0.15 USD/word, roughly 5,500-10,000 USD per language, so 22,000-40,000 USD for es/fr/de/pt-BR. Machine translation plus a native editor could cut the rules text, not the story. |
| CJK later (ja, ko, zh-Hans) | Add 1-2 weeks for fonts, atlas and size, at a higher translation rate |

If localisation is approved, the first ticket should be the pseudo-locale pass (step 4): it is cheap and shows most
layout damage before any money goes on translation.
