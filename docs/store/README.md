# Store assets

Every image the Google Play and App Store listings need, where it lives and how to rebuild it. The listing copy (name,
descriptions, keywords, category) is in [../STORE_LISTING.md](../STORE_LISTING.md); the owner enters both into the
consoles (GG-T1530). The feature graphic and icons carry no title text, so they stay valid when GG-T573 picks a name.

| Asset | Store | Spec | File | Made by |
|---|---|---|---|---|
| Feature graphic | Google Play | 1024x500, 24-bit PNG, no alpha | `feature-graphic-1024x500.png` | `python tools/store_feature_graphic.py` |
| Hi-res icon | Google Play | 512x512, 32-bit PNG | `Assets/GridGame/AppIcon/app-icon-store-00.png` | `GridGame.Studio/tools/make_app_icon.py` (GG-T486) |
| App icon | App Store | 1024x1024, PNG, no alpha | `Assets/GridGame/AppIcon/app-icon-00.png` | `GridGame.Studio/tools/make_app_icon.py` (GG-T486) |
| Phone screenshots | Google Play | 1080x1920 (9:16), 8 shots | `screenshots/1080x1920/` | automation harness, `screenshot super=2` |
| Phone screenshots | App Store, 6.5-inch | 1284x2778, 8 shots | `screenshots/1284x2778/` | automation harness, `screenshot super=2` |
| Phone screenshots | App Store, 6.9-inch | 1320x2868 | not made yet (GG-T1522) | automation harness |
| Trailer | both | 45 s, see [../TRAILER.md](../TRAILER.md) | not made yet | MindAttic.Media.Capture |

`app-icon-00.png` is fully opaque, which is what the App Store needs; the store icon on Play may keep alpha.

## Feature graphic

`tools/store_feature_graphic.py` builds it from art the game already ships: a 500 px band of a battle backdrop
(`Assets/GridGame/Resources/Backgrounds`, default `shardfall-01.jpg`, the orange world with the horn spires) and five
full-body hero portraits (`Assets/GridGame/Resources/Portraits`) stood in a line, the centre one largest and the
flanks smaller and dimmer, each over a soft shadow, with the bottom of the backdrop faded dark. There is no text on it:
Play shows the app name next to the graphic, and a baked-in title would go stale with the rename.

Every choice is a flag, so a new look needs no code change:

    python tools/store_feature_graphic.py --backdrop still-00.jpg --band 0.4
    python tools/store_feature_graphic.py --heroes ronin-male-00,knight-female-00,red-mage-female-00
    python tools/store_feature_graphic.py --help     # --dim, --lead-height, --step, --spread, --ground, --out

Rerun it whenever the backdrop or one of those portraits is repainted, and commit the PNG.

## Screenshots

Eight portrait scenes, the same in every size: `01-battle-pincer`, `02-camp`, `03-summon-circle`,
`04-adventure-regions`, `05-heroes-equip`, `06-blacksmith-forge`, `07-boss-battle`, `08-diamond-store`. They were shot
from a development player through the automation harness (`tools/harness.py`) on the GG-T494 fresh-player save with
battle cut-ins off; the `screenshot` endpoint's `super=2` renders at half size times two, which keeps them crisp and
free of the Development Build watermark. Recapture them (GG-T1522) after a UI change that shows in any scene, one game
player at a time.
