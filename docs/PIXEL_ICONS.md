# Pixel art: every game icon

The enemy pixel-art timeline sprites (`Resources/Pixel`, `tools/make_pixel_icons.py` and its eye-annotation
tools) were deleted on 2026-11-03 (GG-T224): since GG-T221 every timeline turn icon is the unit's portrait framed
by its class `Thumbnail` (BIBLE §12.1.1). What remains here is the icon pipeline.

## Statuses, abilities, items, cursor (`GridGame.Studio/tools/make_icons.py`)

Owner, 2026-10-10:

> "instead of letter placeholders go out and find the images, and then use the same sprite art techniques to turn them into classically pixelated icons"
>
> "a continuous effort of building up graphics that match the aesthetic"

- **Sources.** Silhouettes come from **game-icons.net** (CC BY 3.0). The tool downloads each SVG once into `tools/icon_src/` and records every one, with its author, in `tools/icon_src/SOURCES.md`; the Credits screen carries the attribution.
- **Manifest.** `tools/icons.json` maps each game id to `[author/icon, palette, options…]`:
  - `status`: buff ids and `stunned`.
  - `ability`: every ability id.
  - `item`: overrides for consumables and materials.
  - `ui`: the field pickups (`pk_*`, in `Resources/UI`) and every interface concept in docs/ICONS.md (GG-T2050): nav tabs, hub tiles, currencies, stats, elements, attack types, rarity, item categories, stations, map markers, battle actions, RowKind chrome, settings rows and board cells, named `<section>_<concept>` (`chrome_back`, `set_mute_music`) and written to `Resources/UI/Icons/<section>-<concept>-00.png` with point-filter metas. Two options beyond `flop`: `rot:<deg>` turns the source, `strike` draws a 1 px light diagonal over a 3 px outline (the mute rows). Studio's `main()` doesn't know the `UI/Icons` folder or `strike` yet (GGS-T47), so GG-T2050 built them through `make_icons.build()` directly. The menu `cursor` is no longer generated (GG-T772): `Resources/UI/cursor-00.png` is "Hand cursor" by andreyin (CC0, https://opengameart.org/node/89568), a 32×32 Chrono Trigger-style hand stored ×4 nearest-neighbour at 128×128 and credited on the Credits screen. Don't add a `cursor` entry back, or a full rebuild overwrites it.

  Equipment needs no manifest entry. A rule picks the silhouette from the weapon type or the name (ring, amulet, helm, boots, robe…), and the palette from the material (rusty, iron, steel, bronze, gold…) or the enchant suffix (Flame, Frost, Spark, Shadow).
- **Pipeline.**
  1. Rasterise the white SVG at 64 px.
  2. Paint it with a three-stop vertical gradient from the palette.
  3. Shrink to 20 px with a hard alpha edge.
  4. Reduce to 8 colours, no dithering.
  5. Add a 1 px dark outline, centre it on 22×22, then scale ×4 nearest-neighbour.
  6. Write a point-filter `.meta`.
- **Output.** `Resources/StatusIcons`, `AbilityIcons`, `ItemIcons` and `UI`. `Art.StatusIcon`, `Art.AbilityIcon`, `Art.ItemIcon` and `Art.UiIcon` load them (`Art.Cursor` loads the hand above); `Ui.StatusPip` draws a status icon on a dark disc ringed in the status colour.
- **Gates.**
  - `python ../GridGame.Studio/tools/make_icons.py --check` fails if any ability, item or status has no mapping.
  - `tools/check_assets.py` fails if any icon PNG is missing.
- **Adding content** means adding a manifest line (or letting the item rule cover it) and re-running the tool.
