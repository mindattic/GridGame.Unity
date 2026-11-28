# Target devices

The phones GridGame is laid out, tuned and tested for, and the numbers each tool should use for them. The data lives
in [`tools/devices.json`](../../tools/devices.json), the one source for device sizes; this page explains it and cites
where every figure came from. The rule it serves is [BIBLE §26.9](../BIBLE.md#269-target-devices-gg-t2025).
Checked 2026-11-28 (GG-T2025).

**Baseline (owner, 2026-11-28, GG-T2116: "ok make that be the baseline", "iphone 15 pro"):** the reference device is
the **iPhone 15 Pro** (2023, A17 Pro, 1179x2556, 393x852 pt, 59 pt top / 34 pt bottom Dynamic Island insets;
`reference` in `tools/devices.json`). **In target** (`inTarget: true`): the iPhone 15 Pro and 15 Pro Max and every
iPhone 16 and 17 model (16, 16 Plus, 16 Pro, 16 Pro Max, 16e, 17, 17 Pro, 17 Pro Max, Air); the Galaxy S23 series and
newer (S23, S23+, S23 Ultra, S23 FE through the S25 family and S25 Edge); the 2023+ Z Flip5/6 main screen and Z Fold5/6
cover screen. **Out of target** (kept in the file, `inTarget: false`, marked *out* below): the iPhone 12, 13 and 14
families, the base iPhone 15 and 15 Plus (A16), the Galaxy S21 and S22 families, and the `pixel9` and `ipad` reference
shapes. Audit and capture tools default to the in-target set.

**Earlier scope (owner, 2026-11-28, GG-T2025; superseded by the baseline above):** the Apple and Samsung phones of the last five years: every iPhone 12 to 17 (mini, Plus,
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

| Shape | Representative | Portrait px | Aspect | In target | Models |
|---|---|---|---|---|---|
| `iphone-mini` | `iphone13mini` | 1080×2340 | 0.462 | none | iPhone 12 mini (out), iPhone 13 mini (out) |
| `iphone-notch-390` | `iphone14` | 1170×2532 | 0.462 | some | iPhone 12 (out), iPhone 12 Pro (out), iPhone 13 (out), iPhone 13 Pro (out), iPhone 14 (out), iPhone 16e |
| `iphone-notch-428` | `iphone14plus` | 1284×2778 | 0.462 | none | iPhone 12 Pro Max (out), iPhone 13 Pro Max (out), iPhone 14 Plus (out) |
| `iphone-island-393` | `iphone16` | 1179×2556 | 0.461 | some | iPhone 14 Pro (out), iPhone 15 (out), iPhone 15 Pro, iPhone 16 |
| `iphone-island-430` | `iphone16plus` | 1290×2796 | 0.461 | some | iPhone 14 Pro Max (out), iPhone 15 Plus (out), iPhone 15 Pro Max, iPhone 16 Plus |
| `iphone-402` | `iphone17pro` | 1206×2622 | 0.460 | all | iPhone 16 Pro, iPhone 17, iPhone 17 Pro |
| `iphone-440` | `iphone17promax` | 1320×2868 | 0.460 | all | iPhone 16 Pro Max, iPhone 17 Pro Max |
| `iphone-air` | `iphoneair` | 1260×2736 | 0.461 | all | iPhone Air |
| `galaxy-20x9` | `galaxys21` | 1080×2400 | 0.450 | none | Galaxy S21 (out), Galaxy S21+ (out), Galaxy S21 Ultra (out) |
| `galaxy-fhd` | `galaxys25` | 1080×2340 | 0.462 | some | Galaxy S22 (out), Galaxy S22+ (out), Galaxy S23, Galaxy S23+, Galaxy S23 FE, Galaxy S24, Galaxy S24 FE, Galaxy S25, Galaxy S25 FE |
| `galaxy-qhd` | `galaxys25ultra` | 1440×3120 | 0.462 | some | Galaxy S22 Ultra (out), Galaxy S23 Ultra, Galaxy S24+, Galaxy S24 Ultra, Galaxy S25+, Galaxy S25 Ultra, Galaxy S25 Edge |
| `galaxy-flip-main` | `galaxyzflip6` | 1080×2640 | 0.409 | all | Galaxy Z Flip5 (main), Galaxy Z Flip6 (main) |
| `galaxy-fold-cover` | `galaxyzfold6cover` | 968×2376 | 0.407 | all | Galaxy Z Fold5 (cover), Galaxy Z Fold6 (cover) |
| `pixel-20x9` | `pixel9` | 1080×2424 | 0.446 | none | Pixel 9 (out) |
| `ipad` | `ipad` | 1640×2360 | 0.695 | none | iPad (10th gen) (out) |

## Every device

| Id | Model | Year | Portrait px | Logical | Scale | ppi | Safe px T/B/L/R | Aspect | Hz | SoC (tier) | Shape | In target |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `iphone12mini` | iPhone 12 mini | 2020 | 1080×2340 | 375×812 pt | 2.88 | 476 | 144/98/0/0 | 0.462 | 60 | A14 (1) | `iphone-mini` | out |
| `iphone12` | iPhone 12 | 2020 | 1170×2532 | 390×844 pt | 3 | 460 | 141/102/0/0 | 0.462 | 60 | A14 (1) | `iphone-notch-390` | out |
| `iphone12pro` | iPhone 12 Pro | 2020 | 1170×2532 | 390×844 pt | 3 | 460 | 141/102/0/0 | 0.462 | 60 | A14 (1) | `iphone-notch-390` | out |
| `iphone12promax` | iPhone 12 Pro Max | 2020 | 1284×2778 | 428×926 pt | 3 | 458 | 141/102/0/0 | 0.462 | 60 | A14 (1) | `iphone-notch-428` | out |
| `iphone13mini` | iPhone 13 mini | 2021 | 1080×2340 | 375×812 pt | 2.88 | 476 | 144/98/0/0 | 0.462 | 60 | A15 (1) | `iphone-mini` | out |
| `iphone13` | iPhone 13 | 2021 | 1170×2532 | 390×844 pt | 3 | 460 | 141/102/0/0 | 0.462 | 60 | A15 (1) | `iphone-notch-390` | out |
| `iphone13pro` | iPhone 13 Pro | 2021 | 1170×2532 | 390×844 pt | 3 | 460 | 141/102/0/0 | 0.462 | 120 | A15 (1) | `iphone-notch-390` | out |
| `iphone13promax` | iPhone 13 Pro Max | 2021 | 1284×2778 | 428×926 pt | 3 | 458 | 141/102/0/0 | 0.462 | 120 | A15 (1) | `iphone-notch-428` | out |
| `iphone14` | iPhone 14 | 2022 | 1170×2532 | 390×844 pt | 3 | 460 | 141/102/0/0 | 0.462 | 60 | A15 (1) | `iphone-notch-390` | out |
| `iphone14plus` | iPhone 14 Plus | 2022 | 1284×2778 | 428×926 pt | 3 | 458 | 141/102/0/0 | 0.462 | 60 | A15 (1) | `iphone-notch-428` | out |
| `iphone14pro` | iPhone 14 Pro | 2022 | 1179×2556 | 393×852 pt | 3 | 460 | 177/102/0/0 | 0.461 | 120 | A16 (2) | `iphone-island-393` | out |
| `iphone14promax` | iPhone 14 Pro Max | 2022 | 1290×2796 | 430×932 pt | 3 | 460 | 177/102/0/0 | 0.461 | 120 | A16 (2) | `iphone-island-430` | out |
| `iphone15` | iPhone 15 | 2023 | 1179×2556 | 393×852 pt | 3 | 460 | 177/102/0/0 | 0.461 | 60 | A16 (2) | `iphone-island-393` | out |
| `iphone15plus` | iPhone 15 Plus | 2023 | 1290×2796 | 430×932 pt | 3 | 460 | 177/102/0/0 | 0.461 | 60 | A16 (2) | `iphone-island-430` | out |
| `iphone15pro` | iPhone 15 Pro | 2023 | 1179×2556 | 393×852 pt | 3 | 460 | 177/102/0/0 | 0.461 | 120 | A17 Pro (2) | `iphone-island-393` | **yes** |
| `iphone15promax` | iPhone 15 Pro Max | 2023 | 1290×2796 | 430×932 pt | 3 | 460 | 177/102/0/0 | 0.461 | 120 | A17 Pro (2) | `iphone-island-430` | **yes** |
| `iphone16` | iPhone 16 | 2024 | 1179×2556 | 393×852 pt | 3 | 460 | 177/102/0/0 | 0.461 | 60 | A18 (2) | `iphone-island-393` | **yes** |
| `iphone16plus` | iPhone 16 Plus | 2024 | 1290×2796 | 430×932 pt | 3 | 460 | 177/102/0/0 | 0.461 | 60 | A18 (2) | `iphone-island-430` | **yes** |
| `iphone16pro` | iPhone 16 Pro | 2024 | 1206×2622 | 402×874 pt | 3 | 460 | 186/102/0/0 | 0.460 | 120 | A18 Pro (3) | `iphone-402` | **yes** |
| `iphone16promax` | iPhone 16 Pro Max | 2024 | 1320×2868 | 440×956 pt | 3 | 460 | 186/102/0/0 | 0.460 | 120 | A18 Pro (3) | `iphone-440` | **yes** |
| `iphone16e` | iPhone 16e | 2025 | 1170×2532 | 390×844 pt | 3 | 460 | 141/102/0/0 | 0.462 | 60 | A18 (2) | `iphone-notch-390` | **yes** |
| `iphone17` | iPhone 17 | 2025 | 1206×2622 | 402×874 pt | 3 | 460 | 186/102/0/0 | 0.460 | 120 | A19 (3) | `iphone-402` | **yes** |
| `iphone17pro` | iPhone 17 Pro | 2025 | 1206×2622 | 402×874 pt | 3 | 460 | 186/102/0/0 | 0.460 | 120 | A19 Pro (3) | `iphone-402` | **yes** |
| `iphone17promax` | iPhone 17 Pro Max | 2025 | 1320×2868 | 440×956 pt | 3 | 460 | 186/102/0/0 | 0.460 | 120 | A19 Pro (3) | `iphone-440` | **yes** |
| `iphoneair` | iPhone Air | 2025 | 1260×2736 | 420×912 pt | 3 | 460 | 204/102/0/0 | 0.461 | 120 | A19 Pro (3) | `iphone-air` | **yes** |
| `galaxys21` | Galaxy S21 | 2021 | 1080×2400 | 360×800 dp | 3 | 421 | 96/72/0/0\* | 0.450 | 120 | Snapdragon 888 / Exynos 2100 (1) | `galaxy-20x9` | out |
| `galaxys21plus` | Galaxy S21+ | 2021 | 1080×2400 | 384×853 dp | 2.8125 | 394 | 90/68/0/0\* | 0.450 | 120 | Snapdragon 888 / Exynos 2100 (1) | `galaxy-20x9` | out |
| `galaxys21ultra` | Galaxy S21 Ultra | 2021 | 1440×3200 (default 1080×2400) | 384×853 dp | 3.75 | 515 | 120/90/0/0\* | 0.450 | 120 | Snapdragon 888 / Exynos 2100 (1) | `galaxy-20x9` | out |
| `galaxys22` | Galaxy S22 | 2022 | 1080×2340 | 360×780 dp | 3 | 425 | 96/72/0/0\* | 0.462 | 120 | Snapdragon 8 Gen 1 / Exynos 2200 (1) | `galaxy-fhd` | out |
| `galaxys22plus` | Galaxy S22+ | 2022 | 1080×2340 | 384×832 dp | 2.8125 | 393 | 90/68/0/0\* | 0.462 | 120 | Snapdragon 8 Gen 1 / Exynos 2200 (1) | `galaxy-fhd` | out |
| `galaxys22ultra` | Galaxy S22 Ultra | 2022 | 1440×3088 (default 1080×2316) | 412×882 dp | 3.5 | 500 | 112/84/0/0\* | 0.466 | 120 | Snapdragon 8 Gen 1 / Exynos 2200 (1) | `galaxy-qhd` | out |
| `galaxys23` | Galaxy S23 | 2023 | 1080×2340 | 360×780 dp | 3 | 425 | 96/72/0/0\* | 0.462 | 120 | Snapdragon 8 Gen 2 (2) | `galaxy-fhd` | **yes** |
| `galaxys23plus` | Galaxy S23+ | 2023 | 1080×2340 | 384×832 dp | 2.8125 | 393 | 90/68/0/0\* | 0.462 | 120 | Snapdragon 8 Gen 2 (2) | `galaxy-fhd` | **yes** |
| `galaxys23ultra` | Galaxy S23 Ultra | 2023 | 1440×3088 (default 1080×2316) | 412×882 dp | 3.5 | 500 | 112/84/0/0\* | 0.466 | 120 | Snapdragon 8 Gen 2 (2) | `galaxy-qhd` | **yes** |
| `galaxys23fe` | Galaxy S23 FE | 2023 | 1080×2340 | 384×832 dp\* | 2.8125\* | 403 | 90/68/0/0\* | 0.462 | 120 | Exynos 2200 / Snapdragon 8 Gen 1 (1) | `galaxy-fhd` | **yes** |
| `galaxys24` | Galaxy S24 | 2024 | 1080×2340 | 360×780 dp | 3 | 416 | 96/72/0/0\* | 0.462 | 120 | Exynos 2400 / Snapdragon 8 Gen 3 (2) | `galaxy-fhd` | **yes** |
| `galaxys24plus` | Galaxy S24+ | 2024 | 1440×3120 (default 1080×2340) | 412×891 dp | 3.5 | 513 | 112/84/0/0\* | 0.462 | 120 | Exynos 2400 / Snapdragon 8 Gen 3 (2) | `galaxy-qhd` | **yes** |
| `galaxys24ultra` | Galaxy S24 Ultra | 2024 | 1440×3120 (default 1080×2340) | 412×891 dp | 3.5 | 505 | 112/84/0/0\* | 0.462 | 120 | Snapdragon 8 Gen 3 (2) | `galaxy-qhd` | **yes** |
| `galaxys24fe` | Galaxy S24 FE | 2024 | 1080×2340 | 384×832 dp\* | 2.8125\* | 385 | 90/68/0/0\* | 0.462 | 120 | Exynos 2400e (2) | `galaxy-fhd` | **yes** |
| `galaxys25` | Galaxy S25 | 2025 | 1080×2340 | 360×780 dp | 3 | 416 | 96/72/0/0\* | 0.462 | 120 | Snapdragon 8 Elite (3) | `galaxy-fhd` | **yes** |
| `galaxys25plus` | Galaxy S25+ | 2025 | 1440×3120 (default 1080×2340) | 412×891 dp | 3.5 | 513 | 112/84/0/0\* | 0.462 | 120 | Snapdragon 8 Elite (3) | `galaxy-qhd` | **yes** |
| `galaxys25ultra` | Galaxy S25 Ultra | 2025 | 1440×3120 (default 1080×2340) | 412×891 dp | 3.5 | 498 | 112/84/0/0\* | 0.462 | 120 | Snapdragon 8 Elite (3) | `galaxy-qhd` | **yes** |
| `galaxys25edge` | Galaxy S25 Edge | 2025 | 1440×3120 (default 1080×2340) | 412×891 dp | 3.5 | 513 | 112/84/0/0\* | 0.462 | 120 | Snapdragon 8 Elite (3) | `galaxy-qhd` | **yes** |
| `galaxys25fe` | Galaxy S25 FE | 2025 | 1080×2340 | 384×832 dp\* | 2.8125\* | 385 | 90/68/0/0\* | 0.462 | 120 | Exynos 2400 (2) | `galaxy-fhd` | **yes** |
| `galaxyzflip5` | Galaxy Z Flip5 (main) | 2023 | 1080×2640 | 393×960 dp\* | 2.75\* | 425 | 88/66/0/0\* | 0.409 | 120 | Snapdragon 8 Gen 2 (2) | `galaxy-flip-main` | **yes** |
| `galaxyzflip6` | Galaxy Z Flip6 (main) | 2024 | 1080×2640 | 393×960 dp | 2.75 | 425 | 88/66/0/0\* | 0.409 | 120 | Snapdragon 8 Gen 3 (2) | `galaxy-flip-main` | **yes** |
| `galaxyzfold5cover` | Galaxy Z Fold5 (cover) | 2023 | 904×2316 | 301×772 dp\* | 3\* | 402 | 96/72/0/0\* | 0.390 | 120 | Snapdragon 8 Gen 2 (2) | `galaxy-fold-cover` | **yes** |
| `galaxyzfold6cover` | Galaxy Z Fold6 (cover) | 2024 | 968×2376 | 323×792 dp | 3 | 410 | 96/72/0/0\* | 0.407 | 120 | Snapdragon 8 Gen 3 (2) | `galaxy-fold-cover` | **yes** |
| `pixel9` | Pixel 9 | 2024 | 1080×2424 | 412×923 dp | 2.625 | 422 | 110/63/0/0\* | 0.446 | 120 | Tensor G4 (2) | `pixel-20x9` | out |
| `ipad` | iPad (10th gen) | 2022 | 1640×2360 | 820×1180 pt | 2 | 264 | 48/40/0/0 | 0.695 | 60 | A14 (1) | `ipad` | out |

## Camera cutouts

GG-T2119: every iPhone row carries its camera cutout (`cutout`) and its display's rounded-corner radius
(`screen_radius`), in device px with the pt figure alongside. Dev builds draw both over the UI when a tool launches
the player as a device (the automation `deviceframe` endpoint, sent by `tools/scenario.py` and `tools/uiaudit.py`), so the owner sees what the island, notch and corners cover.

- **type**: `island` is the Dynamic Island, a pill floating `top` pt below the screen's top edge; `notch` hangs from
  the top edge with its bottom corners rounded by `radius`; `punch` (the Galaxy punch-hole camera) is a circle of
  diameter `w`, its top `top` below the edge. Every cutout is centred horizontally.
- **Display corner radius**: the value iOS reports as the screen's corner radius (`_displayCornerRadius`), as
  tabulated by the ScreenCorners project: 44 pt (12/13 mini), 47.33 (12, 12 Pro, 13, 13 Pro, 14, 16e), 53.33 (12/13
  Pro Max, 14 Plus), 55 (14 Pro through 16 Plus, including 15 Pro / Pro Max), 62 (16 Pro / Pro Max). Apple's real
  corner is a continuous-curvature curve; the overlay draws a circular arc of that radius, which differs by about a
  pixel. The iPhone 17 family and Air take the 16 Pro's 62 pt: *estimated* until read off iOS on those models.
- **Dynamic Island**: Apple publishes no island dimensions. The resting island is about 126 × 37.33 pt (a pill, radius
  18.67) on every model that has one, the figure design kits and simulator measurements use; it sits 11.33 pt below
  the top on the 393/430-pt models. The 402/440-pt models (16 Pro, 17 family) have a 3 pt deeper top inset (62 vs
  59 pt), so their island is put at 14 pt; the Air (68 pt inset) at 20 pt. All island figures are *estimated*.
- **Notch**: about 210 × 30 pt on the iPhone 12 family (the iPhone X notch) and about 162 × 33 pt on the 13, 14 and
  16e (Apple: the 13's notch is 20% narrower), bottom radius about 20 pt. *Estimated*. The 12/13 mini notch is left
  null (the mini's 2.88x native scale makes a pt figure misleading); its corner radius is recorded.
- **Galaxy punch-hole** (GG-T2030): Samsung publishes no hole size. Every in-target Galaxy gets one centred hole about
  18 dp across, its top 11 dp below the edge (about 3.4 mm, inside the 32 dp status bar the safe area assumes), and a
  screen corner of 30 dp (S23-S25, S23 Ultra), 26 dp (the flatter S24/S25 Ultra and S25 Edge), 34 dp (Z Flip main) or
  28 dp (Z Fold cover). Galaxy rows read dp where the table below says pt. All *estimated*.

| Id | Model | Cutout | W×H pt | Top pt | Cutout radius pt | Screen radius pt | Device px (w×h, top, r / screen r) |
|---|---|---|---|---|---|---|---|
| `iphone12mini` | iPhone 12 mini | — | — | — | — | 44 | — / 127 |
| `iphone12` | iPhone 12 | notch\* | 210×30 | 0 | 20 | 47.33 | 630×90, 0, 60 / 142 |
| `iphone12pro` | iPhone 12 Pro | notch\* | 210×30 | 0 | 20 | 47.33 | 630×90, 0, 60 / 142 |
| `iphone12promax` | iPhone 12 Pro Max | notch\* | 210×30 | 0 | 20 | 53.33 | 630×90, 0, 60 / 160 |
| `iphone13mini` | iPhone 13 mini | — | — | — | — | 44 | — / 127 |
| `iphone13` | iPhone 13 | notch\* | 162×33 | 0 | 20 | 47.33 | 486×99, 0, 60 / 142 |
| `iphone13pro` | iPhone 13 Pro | notch\* | 162×33 | 0 | 20 | 47.33 | 486×99, 0, 60 / 142 |
| `iphone13promax` | iPhone 13 Pro Max | notch\* | 162×33 | 0 | 20 | 53.33 | 486×99, 0, 60 / 160 |
| `iphone14` | iPhone 14 | notch\* | 162×33 | 0 | 20 | 47.33 | 486×99, 0, 60 / 142 |
| `iphone14plus` | iPhone 14 Plus | notch\* | 162×33 | 0 | 20 | 53.33 | 486×99, 0, 60 / 160 |
| `iphone14pro` | iPhone 14 Pro | island\* | 126×37.33 | 11.33 | 18.67 | 55 | 378×112, 34, 56 / 165 |
| `iphone14promax` | iPhone 14 Pro Max | island\* | 126×37.33 | 11.33 | 18.67 | 55 | 378×112, 34, 56 / 165 |
| `iphone15` | iPhone 15 | island\* | 126×37.33 | 11.33 | 18.67 | 55 | 378×112, 34, 56 / 165 |
| `iphone15plus` | iPhone 15 Plus | island\* | 126×37.33 | 11.33 | 18.67 | 55 | 378×112, 34, 56 / 165 |
| `iphone15pro` | iPhone 15 Pro | island\* | 126×37.33 | 11.33 | 18.67 | 55 | 378×112, 34, 56 / 165 |
| `iphone15promax` | iPhone 15 Pro Max | island\* | 126×37.33 | 11.33 | 18.67 | 55 | 378×112, 34, 56 / 165 |
| `iphone16` | iPhone 16 | island\* | 126×37.33 | 11.33 | 18.67 | 55 | 378×112, 34, 56 / 165 |
| `iphone16plus` | iPhone 16 Plus | island\* | 126×37.33 | 11.33 | 18.67 | 55 | 378×112, 34, 56 / 165 |
| `iphone16pro` | iPhone 16 Pro | island\* | 126×37.33 | 14 | 18.67 | 62 | 378×112, 42, 56 / 186 |
| `iphone16promax` | iPhone 16 Pro Max | island\* | 126×37.33 | 14 | 18.67 | 62 | 378×112, 42, 56 / 186 |
| `iphone16e` | iPhone 16e | notch\* | 162×33 | 0 | 20 | 47.33 | 486×99, 0, 60 / 142 |
| `iphone17` | iPhone 17 | island\* | 126×37.33 | 14 | 18.67 | 62\* | 378×112, 42, 56 / 186 |
| `iphone17pro` | iPhone 17 Pro | island\* | 126×37.33 | 14 | 18.67 | 62\* | 378×112, 42, 56 / 186 |
| `iphone17promax` | iPhone 17 Pro Max | island\* | 126×37.33 | 14 | 18.67 | 62\* | 378×112, 42, 56 / 186 |
| `iphoneair` | iPhone Air | island\* | 126×37.33 | 20 | 18.67 | 62\* | 378×112, 60, 56 / 186 |
| `galaxys23` | Galaxy S23 | punch\* | 18×18 | 11 | 9 | 30\* | 54×54, 33, 27 / 90 |
| `galaxys23plus` | Galaxy S23+ | punch\* | 18×18 | 11 | 9 | 30\* | 51×51, 31, 25 / 84 |
| `galaxys23ultra` | Galaxy S23 Ultra | punch\* | 18×18 | 11 | 9 | 30\* | 63×63, 38, 32 / 105 |
| `galaxys23fe` | Galaxy S23 FE | punch\* | 18×18 | 11 | 9 | 30\* | 51×51, 31, 25 / 84 |
| `galaxys24` | Galaxy S24 | punch\* | 18×18 | 11 | 9 | 30\* | 54×54, 33, 27 / 90 |
| `galaxys24plus` | Galaxy S24+ | punch\* | 18×18 | 11 | 9 | 30\* | 63×63, 38, 32 / 105 |
| `galaxys24ultra` | Galaxy S24 Ultra | punch\* | 18×18 | 11 | 9 | 26\* | 63×63, 38, 32 / 91 |
| `galaxys24fe` | Galaxy S24 FE | punch\* | 18×18 | 11 | 9 | 30\* | 51×51, 31, 25 / 84 |
| `galaxys25` | Galaxy S25 | punch\* | 18×18 | 11 | 9 | 30\* | 54×54, 33, 27 / 90 |
| `galaxys25plus` | Galaxy S25+ | punch\* | 18×18 | 11 | 9 | 30\* | 63×63, 38, 32 / 105 |
| `galaxys25ultra` | Galaxy S25 Ultra | punch\* | 18×18 | 11 | 9 | 26\* | 63×63, 38, 32 / 91 |
| `galaxys25edge` | Galaxy S25 Edge | punch\* | 18×18 | 11 | 9 | 26\* | 63×63, 38, 32 / 91 |
| `galaxys25fe` | Galaxy S25 FE | punch\* | 18×18 | 11 | 9 | 30\* | 51×51, 31, 25 / 84 |
| `galaxyzflip5` | Galaxy Z Flip5 (main) | punch\* | 18×18 | 11 | 9 | 34\* | 50×50, 30, 25 / 94 |
| `galaxyzflip6` | Galaxy Z Flip6 (main) | punch\* | 18×18 | 11 | 9 | 34\* | 50×50, 30, 25 / 94 |
| `galaxyzfold5cover` | Galaxy Z Fold5 (cover) | punch\* | 18×18 | 11 | 9 | 28\* | 54×54, 33, 27 / 84 |
| `galaxyzfold6cover` | Galaxy Z Fold6 (cover) | punch\* | 18×18 | 11 | 9 | 28\* | 54×54, 33, 27 / 84 |

## What the matrix means for the layout

*Re-checked for the 2023+ baseline (GG-T2116):* the bullets below still list every row; for the in-target set the
narrowest are the **Z Fold5 cover (301 dp, 904 px, scale 0.77, text about 10.3 dp, estimated width)**, the Fold6 cover
(323 dp, 11.0 dp) and the base Galaxy S23/S24/S25 and S23 FE class (360 to 384 dp; 12.3 dp on the 360-dp ones); the
narrowest in-target iPhone is the 16e at 390 pt (13.3 pt). The iPhone minis (375 pt, 12.8 pt) are out of target, so
the "iPhone mini" figure below no longer sets the floor.

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
- ScreenCorners (display corner radius per iPhone, read from iOS's `_displayCornerRadius`):
  <https://github.com/kylebshr/ScreenCorners>.
- Apple, iPhone 13 announcement (notch 20% smaller): <https://www.apple.com/newsroom/2021/09/apple-introduces-iphone-13-and-iphone-13-mini/>.
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

## Text size per device (GG-T2114)

Body and caption text are one size, `TypeScale.Base` (BIBLE R8; `GuiType.Compute`), so one row per device. **Physical dp**
is the size on the glass in 1/160 inch (base px x stage scale x 160 / ppi), which is what `Screen.dpi` lets the game
compute. **pt-dp** is the platform's logical unit (base px x stage scale / the device scale in the Every-device table);
a Galaxy's logical density is lower than its ppi suggests, so its pt-dp reads smaller than its physical dp. Text size 100%.
The stage is 1170 px wide and scales by panel width / 1170 (the Z Fold5 cover now gets a 1170x2998 stage, GG-T2115).

The fix: `GuiType.TargetDp` goes from 14 to 15, and a screen narrower than the iPhone 15 Pro (2.56 in) multiplies the
target by 2.56 / its width, at most 1.2 (`GuiType.ReferenceWidthIn`, `NarrowBoostMax`), so narrow phones get larger text
through the existing `TypeScale`/`TypeFloor` path with no per-screen edits. The base still clamps to 40-56 px and to
28 characters across the stage. Larger text cannot truncate: `TextFit` shrinks an overflowing label toward
`TypeFloor.ShrinkFloor`, then wraps it (one-line text marquees), and the battle HUD rows keep their fixed heights
(BIBLE 9.0). Result: every in-target device draws body text at 14.7 physical dp or more; the narrowest, the Z Fold5
cover, goes from 11.8 to 14.4 pt-dp (14.1 to 17.2 physical).

| Device | Panel px wide | Width (in) | Before: base px | Before: pt-dp / physical dp | After: base px | After: pt-dp / physical dp |
|---|---|---|---|---|---|---|
| iPhone 15 Pro | 1179 | 2.56 | 40 | 13.4 / 14.0 | 42 | 14.1 / 14.7 |
| iPhone 15 Pro Max | 1290 | 2.80 | 40 | 14.7 / 15.3 | 40 | 14.7 / 15.3 |
| iPhone 16 | 1179 | 2.56 | 40 | 13.4 / 14.0 | 42 | 14.1 / 14.7 |
| iPhone 16 Plus | 1290 | 2.80 | 40 | 14.7 / 15.3 | 40 | 14.7 / 15.3 |
| iPhone 16 Pro | 1206 | 2.62 | 40 | 13.7 / 14.3 | 42 | 14.4 / 15.1 |
| iPhone 16 Pro Max | 1320 | 2.87 | 40 | 15.0 / 15.7 | 40 | 15.0 / 15.7 |
| iPhone 16e | 1170 | 2.54 | 40 | 13.3 / 13.9 | 44 | 14.7 / 15.3 |
| iPhone 17 | 1206 | 2.62 | 40 | 13.7 / 14.3 | 42 | 14.4 / 15.1 |
| iPhone 17 Pro | 1206 | 2.62 | 40 | 13.7 / 14.3 | 42 | 14.4 / 15.1 |
| iPhone 17 Pro Max | 1320 | 2.87 | 40 | 15.0 / 15.7 | 40 | 15.0 / 15.7 |
| iPhone Air | 1260 | 2.74 | 40 | 14.4 / 15.0 | 40 | 14.4 / 15.0 |
| Galaxy S23 | 1080 | 2.54 | 40 | 12.3 / 13.9 | 44 | 13.5 / 15.3 |
| Galaxy S23+ | 1080 | 2.75 | 40 | 13.1 / 15.0 | 40 | 13.1 / 15.0 |
| Galaxy S23 Ultra | 1080 | 2.88 | 40 | 14.1 / 15.8 | 40 | 14.1 / 15.8 |
| Galaxy S23 FE | 1080 | 2.68 | 40 | 13.1 / 14.7 | 40 | 13.1 / 14.7 |
| Galaxy S24 | 1080 | 2.60 | 40 | 12.3 / 14.2 | 42 | 12.9 / 14.9 |
| Galaxy S24+ | 1080 | 2.81 | 40 | 14.1 / 15.4 | 40 | 14.1 / 15.4 |
| Galaxy S24 Ultra | 1080 | 2.85 | 40 | 14.1 / 15.6 | 40 | 14.1 / 15.6 |
| Galaxy S24 FE | 1080 | 2.81 | 40 | 13.1 / 15.3 | 40 | 13.1 / 15.3 |
| Galaxy S25 | 1080 | 2.60 | 40 | 12.3 / 14.2 | 42 | 12.9 / 14.9 |
| Galaxy S25+ | 1080 | 2.81 | 40 | 14.1 / 15.4 | 40 | 14.1 / 15.4 |
| Galaxy S25 Ultra | 1080 | 2.89 | 40 | 14.1 / 15.8 | 40 | 14.1 / 15.8 |
| Galaxy S25 Edge | 1080 | 2.81 | 40 | 14.1 / 15.4 | 40 | 14.1 / 15.4 |
| Galaxy S25 FE | 1080 | 2.81 | 40 | 13.1 / 15.3 | 40 | 13.1 / 15.3 |
| Galaxy Z Flip5 (main) | 1080 | 2.54 | 40 | 13.4 / 13.9 | 44 | 14.8 / 15.3 |
| Galaxy Z Flip6 (main) | 1080 | 2.54 | 40 | 13.4 / 13.9 | 44 | 14.8 / 15.3 |
| Galaxy Z Fold5 (cover) | 904 | 2.25 | 46 | 11.8 / 14.1 | 56 | 14.4 / 17.2 |
| Galaxy Z Fold6 (cover) | 968 | 2.36 | 44 | 12.1 / 14.2 | 50 | 13.8 / 16.1 |

