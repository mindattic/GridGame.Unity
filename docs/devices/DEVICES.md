# Target devices

The phones GridGame is laid out, tuned and tested for, and the numbers each tool should use for them. The data lives
in [`tools/devices.json`](../../tools/devices.json), the one source for device sizes; this page explains it and cites
where every figure came from. The rule it serves is [BIBLE §26.9](../BIBLE.md#269-target-devices-gg-t2025).
Checked 2026-11-28 (GG-T2025).

**Scope (owner, 2026-11-28):** the Apple and Samsung phones of the last five years: every iPhone 12 to 17 (mini, Plus,
Pro, Pro Max, 16e, Air) and every Galaxy S21 to S25 (base, +, Ultra, FE, S25 Edge), plus the Galaxy Z Flip5/6 main
screen and Z Fold5/6 cover screen as a *should*. Out by default: the iPhone SE and the Flip cover screen (FlexWindow).
`pixel9` and `ipad` stay as reference shapes because the tools and older captures already use them.

## Fields

- **px**: the physical panel, portrait. **default_px** (Galaxy QHD+ only): Samsung ships its 1440-wide panels set to
  FHD+, so that is what the app sees until the player picks QHD+ in Settings. Same aspect either way.
- **unit / scale / logical**: px per pt (iOS) or per dp (Android at the default display size) and the logical size.
  The iPhone 12/13 mini render at 3x and are downsampled to the panel, a native scale of 2.88.
- **ppi**: the panel's published pixel density.
- **safe_px**: the portrait safe area in device px: top is the notch, Dynamic Island or status bar over the punch-hole,
  bottom the home indicator or gesture bar. Left and right are 0 on every phone in portrait.
- **aspect**: w/h. The game fills 9:23 to 9:16 (0.391 to 0.5625, [§26.3](../BIBLE.md#263-adaptive-stage-gg-t169)).
- **hz**: the panel's top refresh rate. **soc / soc_tier**: the chip and a rough tier: 1 = A14, A15, Snapdragon 888,
  8 Gen 1, Exynos 2100/2200; 2 = A16, A17 Pro, A18, 8 Gen 2/3, Exynos 2400/2400e; 3 = A18 Pro, A19, A19 Pro, 8 Elite.
- **estimated**: fields that are not a published figure (marked \* below). See "How sure each figure is".

## Shapes

Fifteen shapes: thirteen from the owner's matrix plus the two reference shapes. Each has a representative device id,
which is what a tool runs when it wants one window per shape. Models in a shape share the panel size (or, for
`galaxy-20x9` and `galaxy-fold-cover`, the aspect and safe area).

| Shape | Representative | Portrait px | Aspect | Models |
|---|---|---|---|---|
| `iphone-mini` | `iphone13mini` | 1080×2340 | 0.462 | iPhone 12 mini, iPhone 13 mini |
| `iphone-notch-390` | `iphone14` | 1170×2532 | 0.462 | iPhone 12, iPhone 12 Pro, iPhone 13, iPhone 13 Pro, iPhone 14, iPhone 16e |
| `iphone-notch-428` | `iphone14plus` | 1284×2778 | 0.462 | iPhone 12 Pro Max, iPhone 13 Pro Max, iPhone 14 Plus |
| `iphone-island-393` | `iphone16` | 1179×2556 | 0.461 | iPhone 14 Pro, iPhone 15, iPhone 15 Pro, iPhone 16 |
| `iphone-island-430` | `iphone16plus` | 1290×2796 | 0.461 | iPhone 14 Pro Max, iPhone 15 Plus, iPhone 15 Pro Max, iPhone 16 Plus |
| `iphone-402` | `iphone17pro` | 1206×2622 | 0.460 | iPhone 16 Pro, iPhone 17, iPhone 17 Pro |
| `iphone-440` | `iphone17promax` | 1320×2868 | 0.460 | iPhone 16 Pro Max, iPhone 17 Pro Max |
| `iphone-air` | `iphoneair` | 1260×2736 | 0.461 | iPhone Air |
| `galaxy-20x9` | `galaxys21` | 1080×2400 | 0.450 | Galaxy S21, Galaxy S21+, Galaxy S21 Ultra |
| `galaxy-fhd` | `galaxys25` | 1080×2340 | 0.462 | Galaxy S22, Galaxy S22+, Galaxy S23, Galaxy S23+, Galaxy S23 FE, Galaxy S24, Galaxy S24 FE, Galaxy S25, Galaxy S25 FE |
| `galaxy-qhd` | `galaxys25ultra` | 1440×3120 | 0.462 | Galaxy S22 Ultra, Galaxy S23 Ultra, Galaxy S24+, Galaxy S24 Ultra, Galaxy S25+, Galaxy S25 Ultra, Galaxy S25 Edge |
| `galaxy-flip-main` | `galaxyzflip6` | 1080×2640 | 0.409 | Galaxy Z Flip5 (main), Galaxy Z Flip6 (main) |
| `galaxy-fold-cover` | `galaxyzfold6cover` | 968×2376 | 0.407 | Galaxy Z Fold5 (cover), Galaxy Z Fold6 (cover) |
| `pixel-20x9` | `pixel9` | 1080×2424 | 0.446 | Pixel 9 |
| `ipad` | `ipad` | 1640×2360 | 0.695 | iPad (10th gen) |

## Every device

| Id | Model | Year | Portrait px | Logical | Scale | ppi | Safe px T/B/L/R | Aspect | Hz | SoC (tier) | Shape |
|---|---|---|---|---|---|---|---|---|---|---|---|
| `iphone12mini` | iPhone 12 mini | 2020 | 1080×2340 | 375×812 pt | 2.88 | 476 | 144/98/0/0 | 0.462 | 60 | A14 (1) | `iphone-mini` |
| `iphone12` | iPhone 12 | 2020 | 1170×2532 | 390×844 pt | 3 | 460 | 141/102/0/0 | 0.462 | 60 | A14 (1) | `iphone-notch-390` |
| `iphone12pro` | iPhone 12 Pro | 2020 | 1170×2532 | 390×844 pt | 3 | 460 | 141/102/0/0 | 0.462 | 60 | A14 (1) | `iphone-notch-390` |
| `iphone12promax` | iPhone 12 Pro Max | 2020 | 1284×2778 | 428×926 pt | 3 | 458 | 141/102/0/0 | 0.462 | 60 | A14 (1) | `iphone-notch-428` |
| `iphone13mini` | iPhone 13 mini | 2021 | 1080×2340 | 375×812 pt | 2.88 | 476 | 144/98/0/0 | 0.462 | 60 | A15 (1) | `iphone-mini` |
| `iphone13` | iPhone 13 | 2021 | 1170×2532 | 390×844 pt | 3 | 460 | 141/102/0/0 | 0.462 | 60 | A15 (1) | `iphone-notch-390` |
| `iphone13pro` | iPhone 13 Pro | 2021 | 1170×2532 | 390×844 pt | 3 | 460 | 141/102/0/0 | 0.462 | 120 | A15 (1) | `iphone-notch-390` |
| `iphone13promax` | iPhone 13 Pro Max | 2021 | 1284×2778 | 428×926 pt | 3 | 458 | 141/102/0/0 | 0.462 | 120 | A15 (1) | `iphone-notch-428` |
| `iphone14` | iPhone 14 | 2022 | 1170×2532 | 390×844 pt | 3 | 460 | 141/102/0/0 | 0.462 | 60 | A15 (1) | `iphone-notch-390` |
| `iphone14plus` | iPhone 14 Plus | 2022 | 1284×2778 | 428×926 pt | 3 | 458 | 141/102/0/0 | 0.462 | 60 | A15 (1) | `iphone-notch-428` |
| `iphone14pro` | iPhone 14 Pro | 2022 | 1179×2556 | 393×852 pt | 3 | 460 | 177/102/0/0 | 0.461 | 120 | A16 (2) | `iphone-island-393` |
| `iphone14promax` | iPhone 14 Pro Max | 2022 | 1290×2796 | 430×932 pt | 3 | 460 | 177/102/0/0 | 0.461 | 120 | A16 (2) | `iphone-island-430` |
| `iphone15` | iPhone 15 | 2023 | 1179×2556 | 393×852 pt | 3 | 460 | 177/102/0/0 | 0.461 | 60 | A16 (2) | `iphone-island-393` |
| `iphone15plus` | iPhone 15 Plus | 2023 | 1290×2796 | 430×932 pt | 3 | 460 | 177/102/0/0 | 0.461 | 60 | A16 (2) | `iphone-island-430` |
| `iphone15pro` | iPhone 15 Pro | 2023 | 1179×2556 | 393×852 pt | 3 | 460 | 177/102/0/0 | 0.461 | 120 | A17 Pro (2) | `iphone-island-393` |
| `iphone15promax` | iPhone 15 Pro Max | 2023 | 1290×2796 | 430×932 pt | 3 | 460 | 177/102/0/0 | 0.461 | 120 | A17 Pro (2) | `iphone-island-430` |
| `iphone16` | iPhone 16 | 2024 | 1179×2556 | 393×852 pt | 3 | 460 | 177/102/0/0 | 0.461 | 60 | A18 (2) | `iphone-island-393` |
| `iphone16plus` | iPhone 16 Plus | 2024 | 1290×2796 | 430×932 pt | 3 | 460 | 177/102/0/0 | 0.461 | 60 | A18 (2) | `iphone-island-430` |
| `iphone16pro` | iPhone 16 Pro | 2024 | 1206×2622 | 402×874 pt | 3 | 460 | 186/102/0/0 | 0.460 | 120 | A18 Pro (3) | `iphone-402` |
| `iphone16promax` | iPhone 16 Pro Max | 2024 | 1320×2868 | 440×956 pt | 3 | 460 | 186/102/0/0 | 0.460 | 120 | A18 Pro (3) | `iphone-440` |
| `iphone16e` | iPhone 16e | 2025 | 1170×2532 | 390×844 pt | 3 | 460 | 141/102/0/0 | 0.462 | 60 | A18 (2) | `iphone-notch-390` |
| `iphone17` | iPhone 17 | 2025 | 1206×2622 | 402×874 pt | 3 | 460 | 186/102/0/0 | 0.460 | 120 | A19 (3) | `iphone-402` |
| `iphone17pro` | iPhone 17 Pro | 2025 | 1206×2622 | 402×874 pt | 3 | 460 | 186/102/0/0 | 0.460 | 120 | A19 Pro (3) | `iphone-402` |
| `iphone17promax` | iPhone 17 Pro Max | 2025 | 1320×2868 | 440×956 pt | 3 | 460 | 186/102/0/0 | 0.460 | 120 | A19 Pro (3) | `iphone-440` |
| `iphoneair` | iPhone Air | 2025 | 1260×2736 | 420×912 pt | 3 | 460 | 204/102/0/0 | 0.461 | 120 | A19 Pro (3) | `iphone-air` |
| `galaxys21` | Galaxy S21 | 2021 | 1080×2400 | 360×800 dp | 3 | 421 | 96/72/0/0\* | 0.450 | 120 | Snapdragon 888 / Exynos 2100 (1) | `galaxy-20x9` |
| `galaxys21plus` | Galaxy S21+ | 2021 | 1080×2400 | 384×853 dp | 2.8125 | 394 | 90/68/0/0\* | 0.450 | 120 | Snapdragon 888 / Exynos 2100 (1) | `galaxy-20x9` |
| `galaxys21ultra` | Galaxy S21 Ultra | 2021 | 1440×3200 (default 1080×2400) | 384×853 dp | 3.75 | 515 | 120/90/0/0\* | 0.450 | 120 | Snapdragon 888 / Exynos 2100 (1) | `galaxy-20x9` |
| `galaxys22` | Galaxy S22 | 2022 | 1080×2340 | 360×780 dp | 3 | 425 | 96/72/0/0\* | 0.462 | 120 | Snapdragon 8 Gen 1 / Exynos 2200 (1) | `galaxy-fhd` |
| `galaxys22plus` | Galaxy S22+ | 2022 | 1080×2340 | 384×832 dp | 2.8125 | 393 | 90/68/0/0\* | 0.462 | 120 | Snapdragon 8 Gen 1 / Exynos 2200 (1) | `galaxy-fhd` |
| `galaxys22ultra` | Galaxy S22 Ultra | 2022 | 1440×3088 (default 1080×2316) | 412×882 dp | 3.5 | 500 | 112/84/0/0\* | 0.466 | 120 | Snapdragon 8 Gen 1 / Exynos 2200 (1) | `galaxy-qhd` |
| `galaxys23` | Galaxy S23 | 2023 | 1080×2340 | 360×780 dp | 3 | 425 | 96/72/0/0\* | 0.462 | 120 | Snapdragon 8 Gen 2 (2) | `galaxy-fhd` |
| `galaxys23plus` | Galaxy S23+ | 2023 | 1080×2340 | 384×832 dp | 2.8125 | 393 | 90/68/0/0\* | 0.462 | 120 | Snapdragon 8 Gen 2 (2) | `galaxy-fhd` |
| `galaxys23ultra` | Galaxy S23 Ultra | 2023 | 1440×3088 (default 1080×2316) | 412×882 dp | 3.5 | 500 | 112/84/0/0\* | 0.466 | 120 | Snapdragon 8 Gen 2 (2) | `galaxy-qhd` |
| `galaxys23fe` | Galaxy S23 FE | 2023 | 1080×2340 | 384×832 dp\* | 2.8125\* | 403 | 90/68/0/0\* | 0.462 | 120 | Exynos 2200 / Snapdragon 8 Gen 1 (1) | `galaxy-fhd` |
| `galaxys24` | Galaxy S24 | 2024 | 1080×2340 | 360×780 dp | 3 | 416 | 96/72/0/0\* | 0.462 | 120 | Exynos 2400 / Snapdragon 8 Gen 3 (2) | `galaxy-fhd` |
| `galaxys24plus` | Galaxy S24+ | 2024 | 1440×3120 (default 1080×2340) | 412×891 dp | 3.5 | 513 | 112/84/0/0\* | 0.462 | 120 | Exynos 2400 / Snapdragon 8 Gen 3 (2) | `galaxy-qhd` |
| `galaxys24ultra` | Galaxy S24 Ultra | 2024 | 1440×3120 (default 1080×2340) | 412×891 dp | 3.5 | 505 | 112/84/0/0\* | 0.462 | 120 | Snapdragon 8 Gen 3 (2) | `galaxy-qhd` |
| `galaxys24fe` | Galaxy S24 FE | 2024 | 1080×2340 | 384×832 dp\* | 2.8125\* | 385 | 90/68/0/0\* | 0.462 | 120 | Exynos 2400e (2) | `galaxy-fhd` |
| `galaxys25` | Galaxy S25 | 2025 | 1080×2340 | 360×780 dp | 3 | 416 | 96/72/0/0\* | 0.462 | 120 | Snapdragon 8 Elite (3) | `galaxy-fhd` |
| `galaxys25plus` | Galaxy S25+ | 2025 | 1440×3120 (default 1080×2340) | 412×891 dp | 3.5 | 513 | 112/84/0/0\* | 0.462 | 120 | Snapdragon 8 Elite (3) | `galaxy-qhd` |
| `galaxys25ultra` | Galaxy S25 Ultra | 2025 | 1440×3120 (default 1080×2340) | 412×891 dp | 3.5 | 498 | 112/84/0/0\* | 0.462 | 120 | Snapdragon 8 Elite (3) | `galaxy-qhd` |
| `galaxys25edge` | Galaxy S25 Edge | 2025 | 1440×3120 (default 1080×2340) | 412×891 dp | 3.5 | 513 | 112/84/0/0\* | 0.462 | 120 | Snapdragon 8 Elite (3) | `galaxy-qhd` |
| `galaxys25fe` | Galaxy S25 FE | 2025 | 1080×2340 | 384×832 dp\* | 2.8125\* | 385 | 90/68/0/0\* | 0.462 | 120 | Exynos 2400 (2) | `galaxy-fhd` |
| `galaxyzflip5` | Galaxy Z Flip5 (main) | 2023 | 1080×2640 | 393×960 dp\* | 2.75\* | 425 | 88/66/0/0\* | 0.409 | 120 | Snapdragon 8 Gen 2 (2) | `galaxy-flip-main` |
| `galaxyzflip6` | Galaxy Z Flip6 (main) | 2024 | 1080×2640 | 393×960 dp | 2.75 | 425 | 88/66/0/0\* | 0.409 | 120 | Snapdragon 8 Gen 3 (2) | `galaxy-flip-main` |
| `galaxyzfold5cover` | Galaxy Z Fold5 (cover) | 2023 | 904×2316 | 301×772 dp\* | 3\* | 402 | 96/72/0/0\* | 0.390 | 120 | Snapdragon 8 Gen 2 (2) | `galaxy-fold-cover` |
| `galaxyzfold6cover` | Galaxy Z Fold6 (cover) | 2024 | 968×2376 | 323×792 dp | 3 | 410 | 96/72/0/0\* | 0.407 | 120 | Snapdragon 8 Gen 3 (2) | `galaxy-fold-cover` |
| `pixel9` | Pixel 9 | 2024 | 1080×2424 | 412×923 dp | 2.625 | 422 | 110/63/0/0\* | 0.446 | 120 | Tensor G4 (2) | `pixel-20x9` |
| `ipad` | iPad (10th gen) | 2022 | 1640×2360 | 820×1180 pt | 2 | 264 | 48/40/0/0 | 0.695 | 60 | A14 (1) | `ipad` |

## What the matrix means for the layout

- **Every phone fills the screen.** All of them sit inside 9:23 to 9:16 except the Galaxy Z Fold5 cover, 904×2316 =
  0.3903, a hair under 9:23 (0.3913): it gets a sliver of fog above and below (well under 1% of the height).
- **The smallest drawing scale** is the Fold5 cover at 904 / 1170 = 0.77 screen px per design px (Fold6 cover 0.83);
  every other phone is 1080 px wide or more, so 0.92 or above. `TypeFloor` does not engage on any of them.
- **The smallest physical text** is on the narrow logical widths: the one 40-design-px GUI size
  ([§26.6](../BIBLE.md#266-units-and-overflow-gg-t272-gg-t271-owner-2026-11-05)) is about 40 × 375 / 1170 = 12.8 pt on
  the iPhone mini, 12.3 dp on a 360-dp Galaxy S base model, 11.0 dp on the Fold6 cover and 10.3 dp on the Fold5 cover,
  against 13.3 to 15 pt on the other iPhones.
- **Safe areas** range from 47 pt (notch iPhones) through 59 and 62 pt (Dynamic Island) to 68 pt (iPhone Air) on top,
  and 34 pt at the bottom of every iPhone; the Galaxy figures are estimates until GG-T2030's captures or a real device
  confirm them.
- **Chips:** the floor moves down to tier 1's oldest, the A14 (iPhone 12, 2020) and Snapdragon 888 / Exynos 2100
  (Galaxy S21, 2021), one year below GG-T724's A15 / 8 Gen 1 floor.

## How sure each figure is

- **iPhone**: px, points, scale, ppi, refresh and chip are Apple's published specs; the safe-area insets are what the
  iOS simulator reports, as tabulated by Use Your Loaf (12/13 family from the 13 page, 14 family from the 14 page, 16
  family from the 16 page, 17 family and Air from the 17 page; 12 mini = 13 mini, 12 = 13, 15 = 14 Pro and 16, 16e =
  14 notch). Solid.
- **Galaxy panel figures** (px, size, ppi, refresh, chip, release) are Samsung's spec pages, cross-checked with
  GSMArena. Solid. The default FHD+ setting on the QHD+ models is Samsung's out-of-the-box Screen resolution.
- **Galaxy dp widths** come from browser viewport tables (CSS px = dp at the default display size): 360 dp for the
  S21-S25 base models, 384 for the S21+, S21 Ultra, S22+ and S23+, 412 for the S22-S25 Ultra and S24+/S25+/S25 Edge,
  393 for the Z Flip6 and 323 for the Z Fold6 cover. Those tables are third-party and say so; a player who changes
  Screen zoom changes them. The S23/S24/S25 FE (assumed 384, like the other big FHD+ models), Z Flip5 (assumed the
  Flip6's 393) and Z Fold5 cover (assumed the Fold6 cover's density, 301 dp) are not in any table: marked estimated.
- **Galaxy safe areas** are an estimate for every model: 32 dp on top (status bar over the punch-hole) and 24 dp at the
  bottom (the gesture bar, Samsung's default navigation), converted to px with the model's scale. Samsung publishes no
  inset figures; read the real ones from `Screen.safeArea` on a device (the `safearea` automation endpoint reports
  them) and replace these.
- **pixel9** keeps the 110 / 63 px insets the tools already used; **ipad** now uses iPadOS's 24 pt top / 20 pt bottom
  (48 / 40 px; `tools/scenario.py` had 72 / 60).

## Corrections to the estimates in the tools

GG-T2030 added five Galaxy entries to `tools/scenario.py` `DEVICES` and `tools/uiaudit.py` `METRICS` by estimate.
Against this matrix:

| Id | Was (scenario px T/B; uiaudit ppi, scale) | Matrix | Change |
|---|---|---|---|
| `galaxys21` | 90/68; 421, 2.8125 | 96/72\*; 421, 3.0 (360 dp) | scale, insets |
| `galaxys25` | 90/68; 416, 2.8125 | 96/72\*; 416, 3.0 (360 dp) | scale, insets |
| `galaxys25ultra` | 120/90; 498, 3.75 | 112/84\*; 498, 3.5 (412 dp) | scale, insets |
| `galaxyzflip6` | 84/63; 425, 2.625 | 88/66\*; 425, 2.75 (393 dp) | scale, insets |
| `galaxyzfold6cover` | 84/63; 410, 2.625 | 96/72\*; 410, 3.0 (323 dp) | scale, insets |
| `ipad` | 72/60; 264, 2.0 | 48/40; 264, 2.0 | insets |

Pixel sizes and ppi were right. GG-T2026 makes `scenario.py`, `uiaudit.py` and `harness.py` read `tools/devices.json`
instead of their own dicts, which applies these.

## Sources

- Apple, iPhone tech specs (per model) and compare: <https://support.apple.com/specs/iphone>,
  <https://www.apple.com/iphone/compare/>.
- Apple, Human Interface Guidelines, Layout (screen sizes in points and scale):
  <https://developer.apple.com/design/human-interface-guidelines/layout>.
- Use Your Loaf, safe-area insets per model: <https://useyourloaf.com/blog/iphone-13-screen-sizes/>,
  <https://useyourloaf.com/blog/iphone-14-screen-sizes/>, <https://useyourloaf.com/blog/iphone-16-screen-sizes/>,
  <https://useyourloaf.com/blog/iphone-17-screen-sizes/>.
- Samsung, Galaxy S and Z specs and compare: <https://www.samsung.com/global/galaxy/>,
  <https://www.samsung.com/uk/smartphones/galaxy-s25/compare/>.
- GSMArena, per-model spec pages (resolution, size, ppi, refresh, chipset): <https://www.gsmarena.com/samsung-phones-9.php>.
- Browser viewport tables (dp widths; unverified by their own account): <https://screensizechecker.com/devices/android-viewport-sizes>,
  <https://yesviz.com/viewport/>.
- Google Store, Pixel 9 tech specs: <https://store.google.com/product/pixel_9_specs>.
- Android developers, window insets: <https://developer.android.com/develop/ui/views/layout/insets>.
