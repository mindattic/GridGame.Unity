# Icons: one style, every concept

GG-T2046 (part of GG-T1999, "Design system written down"). GG-T2050 generates the set from this inventory. The
design page is `docs/design/icon-set/index.html` (open it from disk). It shows every row below with its preview,
made by the same ImageMagick recipe as `make_icons.py`.

## The style

Owner-approved default: **game-icons.net silhouettes (CC BY 3.0), run through the existing pixel pipeline
(`GridGame.Studio/tools/make_icons.py`, docs/PIXEL_ICONS.md), in the Manapunk palette, UI chrome included.** So the
code-drawn `KindEmblem` discs and the Unicode glyphs used as icons both go. Every icon in the game, from the
status pips to the nav tabs, comes out of one recipe:

1. Rasterise the white SVG at 64 px (rotate or mirror it first if the row's options ask).
2. Fill it with the palette's three-stop vertical gradient (light, base, dark).
3. Box-shrink it to 20 px with a hard alpha edge, then reduce to 8 colours with no dithering.
4. Add a 1 px `#120a14` outline and centre it on a 22 x 22 grid.
5. Scale it x4 nearest-neighbour to 88 px and write the `.meta`.

Rules:

- **One concept, one icon.** The same concept uses the same silhouette everywhere. Quests on the hub tile and quest
  markers on the map share `lorc/scroll-unfurled`, and Bounties share the skull. Two different concepts never share a
  silhouette and palette in one group. `tools/check_kit_icons.py` already enforces this for hero kits.
- **The palette carries the meaning.** Chrome and navigation use `manapunk`, the Manalith blue of BIBLE 11.5. Locked,
  disabled, muted and spent states use `pewter`. Danger and leaving use `wine`. Content icons keep their element or
  material palette (`fire`, `steel`, `arcane`...). Rarity has one silhouette, and the tier is its palette.
- **Real art wins over an icon.** A hero thumbnail, a keeper portrait or a region tile stays the picture wherever it
  is today. The icon is the fallback, and the picture on screens that have no art.
- **Badges** (Locked, New, Cleared, a count) sit on the icon's top-right corner at inline size, as `RowIcon.Badge`
  places them today.

## Sizes per role

Sizes are design px (the 1080-wide kit). Every icon has one master on the 22-px grid (the 88-px PNG). The engine draws
it at the size of its role, with the bilinear filter `GaidenImport` forces on Resources. Status, ability and item icons
already work this way at row size. Keeping one grid keeps the pixel density the same across the set.

| Role | Size | Where | Kit token (GG-T2050 adds the new ones) |
|---|---|---|---|
| inline | 32 | in text, prices, the wallet, stat and element chips, badges, battle feed, board cells | `Size.IconInline`, `--size-icon-inline` |
| button | 48 | header buttons (Hamburger, Back, Home), battle action cells, Continue, dialog buttons | `Size.IconButton`, `--size-icon-button` |
| row | 64 | leading icon of every menu row, hub tile, settings row, list entry, map marker | `Size.Icon` (exists, 64) |
| nav | 72 | bottom nav tabs | `Size.IconNav`, `--size-icon-nav` |

The tokens that already exist (`IconS` 40, `IconM` 56, `IconL` 76) keep their present uses until GG-T2050 moves each
icon onto its role.

## Palettes

Every palette in `tools/icons.json` stays. GG-T2050 adds three for chrome:

| Palette | Light | Base | Dark | Use |
|---|---|---|---|---|
| `manapunk` | `#ACA5D3` | `#7590C8` | `#41386F` | navigation, chrome, settings rows (the Manalith blue, `Palette.OrbLit`) |
| `pewter` | `#8C909C` | `#60646F` | `#34363F` | locked, disabled, muted, acted, developer (`Palette.Pewter*`) |
| `wine` | `#E8A0BC` | `#963659` | `#3E1022` | quit and leave (the danger button rim) |

## What GG-T2050 has to change in the pipeline

- `make_icons.py` still writes to `Assets/Gaiden/Resources` (its `RES`), a path from before the rename. Point it at
  `Assets/GridGame/Resources`.
- Add a manifest group for the kit icons (say `kit`, written to `Resources/KitIcons/<id>-00.png` and loaded by an
  `Art.KitIcon(id)`). It covers every row below that is not already a status, ability, item or pickup icon.
- Add two options. `rot<deg>` rotates the source (the recipe already takes `rot`): Back is `plain-arrow` turned 90
  degrees and Enter is the same arrow turned 270. `strike` draws a 1 px light diagonal over a 3 px outline, for the
  mute rows.
- `RowIcon.KindIcon` falls back to the kind's set icon (`RowIcon.IconId`, `Art.UiIcon`); `KindEmblem` is gone
  (GG-T2051).
- Every new source is downloaded into `tools/icon_src/` and credited in `SOURCES.md` and on the Credits screen, as now.

Every source named below was checked against game-icons.net on 2026-11-28, or is already in `tools/icon_src/`.

## Inventory
### Nav tabs

Role: nav 72. Where: Ui.NavBar (NavKind), NavBadges.Tabs.

| Concept | Code id | Today | Source (game-icons.net) | Palette |
|---|---|---|---|---|
| Home | `nav-home` | emblem Home | `delapouite/house` | manapunk |
| Campaign | `nav-campaign` | emblem Place | `lorc/crossed-swords` | manapunk |
| Heroes | `nav-heroes` | emblem Hero | `lorc/visored-helm` | manapunk |
| Summon | `nav-summon` | emblem New (sparkle) | `lorc/magic-portal` | manapunk |
| Shop | `nav-shop` | emblem Shop (coin) | `delapouite/shop` | manapunk |

### Hub tiles and Camp destinations

Role: row 64 (tile), button 48 (Continue, Stage select). Where: HubScreen tiles, CampMenu ids, CampGroupScreen rows.

| Concept | Code id | Today | Source (game-icons.net) | Palette |
|---|---|---|---|---|
| Continue | `continue` | emblem Enter | `delapouite/level-end-flag` | manapunk |
| Stage select | `stageselect` | emblem Place | `lorc/compass` | manapunk |
| New Game+ | `newgameplus` | emblem NewGamePlus | `lorc/cycle` | arcane |
| Crucible | `crucible` | emblem Crucible | `lorc/holy-grail` | fire |
| Heroes | `heroes` | emblem Hero | `lorc/visored-helm` | steel |
| Summon | `summon` | emblem Summon | `lorc/magic-portal` | arcane |
| Shop | `shop` | emblem Shop, Merchant portrait once joined | `delapouite/shop` | gold |
| Craft (group) | `craft` | emblem Craft | `lorc/anvil` | iron |
| Journal (group) | `journal` | emblem Codex | `lorc/open-book` | leather |
| Settings | `settings` | emblem Settings | `lorc/cog` | manapunk |
| Items | `items` | emblem Item | `lorc/swap-bag` | leather |
| Inn / Rest | `inn` | emblem Station | `delapouite/bed` | cloth |
| Respec | `respec` | emblem Station | `delapouite/anticlockwise-rotation` | teal |
| Mercenaries | `mercenaries` | emblem Hero | `lorc/swords-emblem` | crimson |
| Diamond Store | `diamonds` | emblem Diamond | `lorc/diamond-hard` | mana |
| Talk | `talk` | emblem Talk | `lorc/conversation` | manapunk |
| Quests | `quests` | emblem Quest | `lorc/scroll-unfurled` | sand |
| Bounties | `bounties` | BountySkull art | `lorc/skull-crossed-bones` | bone |
| Bestiary | `bestiary` | emblem Bestiary | `lorc/wolf-head` | earth |
| Codex | `codex` | emblem Codex | `delapouite/notebook` | arcane |
| Quit to title | `quit` | emblem Quit | `delapouite/exit-door` | wine |

### Currencies and counters

Role: inline 32. Where: header wallet, prices, rewards, badges.

| Concept | Code id | Today | Source (game-icons.net) | Palette |
|---|---|---|---|---|
| Gold | `Gold` | text only | `delapouite/two-coins` | gold |
| Shards (Diamonds) | `Diamonds` | emblem Diamond | `lorc/diamond-hard` | mana |
| Summon Tickets | `Tickets` | text only | `delapouite/ticket` | sun |
| Food | `World.Food` | text only | `lorc/meat` | rust |
| Waystone Dust | `WaystoneDust` | text only | `lorc/powder` | ghost |
| XP | `Xp` | text only | `delapouite/progression` | jade |
| Skill points | `SkillPoints` | badge number | `delapouite/skills` | holy |
| Masterwork | `Mastery > 1` | glyph U+2605 | `delapouite/round-star` | gold |

### Stats and vitals

Role: inline 32. Where: Stats, stat hexagon, Status tab, requirement chips.

| Concept | Code id | Today | Source (game-icons.net) | Palette |
|---|---|---|---|---|
| Strength (STR) | `Strength` | text only | `lorc/muscle-up` | crimson |
| Vitality (VIT) | `Vitality` | text only | `delapouite/heart-beats` | heal |
| Agility (AGI) | `Agility` | text only | `lorc/run` | jade |
| Speed (SPD) | `Speed` | text only | `lorc/wingfoot` | teal |
| Intelligence (INT) | `Intelligence` | text only | `lorc/brain` | arcane |
| Wisdom (WIS) | `Wisdom` | text only | `lorc/third-eye` | moon |
| Luck (LCK) | `Luck` | text only | `lorc/clover` | green |
| HP | `Hp` | bar only | `lorc/heart-organ` | crimson |
| Mana (MP, orbs) | `Mana` | orb only | `lorc/crystal-growth` | water |
| Level | `Level` | text only | `delapouite/upgrade` | gold |
| Move | `Move` | text only | `lorc/footprint` | earth |

### Elements (DamageType)

Role: inline 32. Where: DamageType, elements.json.

| Concept | Code id | Today | Source (game-icons.net) | Palette |
|---|---|---|---|---|
| Physical | `Physical` | text only | `lorc/fist` | iron |
| Fire | `Fire` | text only | `lorc/small-fire` | fire |
| Ice | `Ice` | text only | `lorc/frozen-orb` | ice |
| Lightning | `Lightning` | text only | `lorc/lightning-helix` | spark |
| Poison | `Poison` | text only | `lorc/poison-gas` | poison |
| Holy | `Holy` | text only | `lorc/holy-symbol` | holy |
| Dark | `Dark` | text only | `lorc/evil-moon` | shadow |
| Arcane | `Arcane` | text only | `lorc/star-swirl` | arcane |

### Damage (attack) types

Role: inline 32. Where: attacktypes.json.

| Concept | Code id | Today | Source (game-icons.net) | Palette |
|---|---|---|---|---|
| Slash | `slash` | none | `lorc/sword-wound` | steel |
| Pierce | `pierce` | none | `lorc/arrowhead` | steel |
| Blunt | `blunt` | none | `lorc/hammer-drop` | iron |
| Slam | `slam` | none | `lorc/earth-crack` | earth |
| Claw | `claw` | none | `lorc/claw-slashes` | bone |
| Bite | `bite` | none | `lorc/carnivore-mouth` | bone |
| Sting | `sting` | none | `lorc/scorpion-tail` | poison |
| Lash | `lash` | none | `delapouite/lasso` | leather |
| Projectile | `projectile` | none | `lorc/arrow-flights` | wood |
| Magic | `magic` | none | `lorc/magic-palm` | arcane |

### Rarity

Role: inline 32. Where: ItemRarity, Palette.Rarity (one silhouette, the tier is the palette).

| Concept | Code id | Today | Source (game-icons.net) | Palette |
|---|---|---|---|---|
| Junk | `Junk` | name colour | `lorc/gems` | ash |
| Common | `Common` | name colour | `lorc/gems` | gray |
| Uncommon | `Uncommon` | name colour | `lorc/gems` | green |
| Rare | `Rare` | name colour | `lorc/gems` | water |
| Epic | `Epic` | name colour | `lorc/gems` | arcane |
| Legendary | `Legendary` | name colour | `lorc/gems` | gold |

### Item categories

Role: inline 32. Where: ItemsScreen category glyphs, item lists.

| Concept | Code id | Today | Source (game-icons.net) | Palette |
|---|---|---|---|---|
| Weapon | `EquipmentSlot.Weapon` | glyph U+2020 | `lorc/sword-slice` | steel |
| Armor | `ArmorSlots` | glyph U+25C6 | `lorc/mailed-fist` | iron |
| Accessory | `other slots` | glyph U+2727 | `lorc/gem-pendant` | gold |
| Consumable | `ItemType.Consumable` | glyph U+271A | `lorc/standing-potion` | crimson |
| Material | `ItemType.CraftingMaterial` | glyph U+25C8 | `lorc/crystal-cluster` | teal |
| Recipe / Tome | `RecipeScroll, Tome` | glyph U+2756 | `delapouite/wax-tablet` | sand |
| Enchanted | `enchant suffix` | glyph U+2726 | `lorc/magic-swirl` | arcane |

### Stations (keeper trades)

Role: row 64. Where: CampMenu stations; once a keeper has joined, the keeper portrait stays the tile picture.

| Concept | Code id | Today | Source (game-icons.net) | Palette |
|---|---|---|---|---|
| Blacksmith (Forge) | `blacksmith` | keeper portrait / emblem Station | `lorc/anvil-impact` | iron |
| Alchemist (Lab) | `alchemist` | keeper portrait / emblem Station | `lorc/round-bottom-flask` | poison |
| Enchantress | `enchantress` | keeper portrait / emblem Station | `lorc/magic-swirl` | arcane |
| Aetherist | `aetherist` | keeper portrait / emblem Station | `lorc/crystal-bars` | mana |
| Caravan | `caravan` | keeper portrait / emblem Station | `delapouite/caravan` | wood |
| Merchant (Shop) | `shop` | keeper portrait / emblem Shop | `delapouite/shop` | gold |
| Summon Circle | `summon` | emblem Summon | `lorc/magic-portal` | arcane |
| Inn | `inn` | emblem Station | `delapouite/bed` | cloth |

### Map markers (overworld)

Role: row 64 on the map, inline 32 in lists. Where: NodeKind, overworld markers.

| Concept | Code id | Today | Source (game-icons.net) | Palette |
|---|---|---|---|---|
| Quest | `quest marker` | emblem Quest | `lorc/scroll-unfurled` | sand |
| Bounty | `bounty marker` | glyph U+2620 / BountySkull | `lorc/skull-crossed-bones` | bone |
| Settlement | `NodeKind.Settlement` | none | `delapouite/village` | wood |
| Camp | `NodeKind.Camp` | none | `lorc/campfire` | fire |
| Waystone | `NodeKind.Waystone` | none | `delapouite/obelisk` | ghost |
| Roadsign | `roadsign` | none | `delapouite/direction-signs` | wood |
| Danger | `Danger` | glyph U+26A0 | `lorc/hazard-sign` | crimson |
| Stage (battle) | `NodeKind.Stage` | region tile / emblem Enter | `lorc/crossed-sabres` | steel |
| Bridge | `NodeKind.Bridge` | none | `delapouite/stone-bridge` | iron |
| Pass | `NodeKind.Pass` | none | `delapouite/mountain-road` | earth |
| Ruin | `NodeKind.Ruin` | none | `delapouite/castle-ruins` | ash |
| Forage spot | `forage` | none | `delapouite/herbs-bundle` | moss |

### Battle actions and feed

Role: button 48 (action cells), inline 32 (feed and badges). Where: BattleScreen action cells, ability sheet, battle feed.

| Concept | Code id | Today | Source (game-icons.net) | Palette |
|---|---|---|---|---|
| Abilities | `Abilities cell` | hero thumb + word | `lorc/sword-spin` | manapunk |
| Info | `Info cell` | emblem Eye | `delapouite/info` | manapunk |
| GUARD | `GUARD cell` | StatusIcon protection (kept) | `lorc/checked-shield` | green |
| Perform (skill) | `verb Perform / Go` | text only | `lorc/sword-clash` | steel |
| Cast (spell) | `verb Cast` | text only | `lorc/fairy-wand` | arcane |
| Use (item) | `verb Use` | text only | `lorc/standing-potion` | crimson |
| Weapon swap | `WeaponSwappedEvent` | glyph U+2B7E | `lorc/back-forth` | steel |
| Turn start | `turn feed line` | glyph U+25B6 | `lorc/hourglass` | manapunk |
| Acted | `acted badge` | glyph U+2713 | `delapouite/check-mark` | pewter |
| Heal | `heal feed line` | glyph U+271A | `zeromancer/heart-plus` | heal |
| Defeated | `defeat feed line` | glyph U+2605 | `lorc/tombstone` | pewter |
| Support | `support feed line` | glyph U+2726 | `delapouite/shaking-hands` | teal |
| Countdown | `hazard countdown` | glyph U+23F2 | `delapouite/alarm-clock` | crimson |
| Region target cell | `BoardView anchor cell` | glyph U+25CE | `lorc/target-arrows` | gold |

### UI chrome (RowKind emblems)

Role: button 48 in headers, inline 32 as badges, row 64 as row icons. Where: RowKind / KindEmblem.

| Concept | Code id | Today | Source (game-icons.net) | Palette |
|---|---|---|---|---|
| Hamburger | `RowKind.Hamburger` | emblem | `delapouite/hamburger-menu` | manapunk |
| Menu (in-list) | `RowKind.Menu` | emblem | `delapouite/stack` | manapunk |
| Back | `RowKind.Back` | emblem | `delapouite/plain-arrow` (rot90) | manapunk |
| Enter / Go | `RowKind.Enter` | emblem | `delapouite/plain-arrow` (rot270) | manapunk |
| Home | `RowKind.Home` | emblem | `delapouite/house` | manapunk |
| Close / cancel | `RowKind.Close` | emblem | `lorc/cross-mark` | manapunk |
| Confirm | `RowKind.Confirm` | emblem | `delapouite/check-mark` | manapunk |
| Locked | `RowKind.Locked` | emblem, glyph U+1F512 | `lorc/padlock` | pewter |
| Cleared | `RowKind.Cleared` | emblem, glyph U+2714 | `delapouite/check-mark` | jade |
| New / unseen | `RowKind.New` | emblem | `delapouite/sparkles` | gold |
| Save | `RowKind.Save` | emblem | `delapouite/save` | manapunk |
| Document | `RowKind.Document` | emblem | `lorc/papers` | cloth |
| Place | `RowKind.Place` | emblem (fallback after region tile) | `delapouite/position-marker` | manapunk |
| Stage | `RowKind.Stage` | emblem (fallback after region tile) | `lorc/crossed-sabres` | steel |
| Item (fallback) | `RowKind.Item` | emblem | `lorc/swap-bag` | leather |
| Ability (fallback) | `RowKind.Ability` | emblem | `lorc/spark-spirit` | arcane |
| Status (fallback) | `RowKind.Status` | emblem | `lorc/aura` | teal |
| Hero / NPC / Keeper (fallback) | `RowKind.Hero, Npc, Keeper` | emblem | `delapouite/person` | manapunk |
| Station (generic) | `RowKind.Station` | emblem | `delapouite/castle` | manapunk |
| Lore | `RowKind.Lore` | emblem | `lorc/quill-ink` | sand |
| Faction | `RowKind.Faction` | emblem | `lorc/flying-flag` | crimson |

### Settings rows

Role: row 64. Where: SettingsScreen (Settings/SettingsScreen.cs).

| Concept | Code id | Today | Source (game-icons.net) | Palette |
|---|---|---|---|---|
| Battle speed | `RowKind.Speed` | emblem | `delapouite/fast-forward-button` | manapunk |
| Drag timer, Untimed turns | `RowKind.Timer` | emblem | `delapouite/alarm-clock` | manapunk |
| Pincer close-ups | `RowKind.Focus` | emblem | `delapouite/crosshair` | manapunk |
| Confirm before acting | `RowKind.Confirm` | emblem | `delapouite/check-mark` | manapunk |
| Quality | `Quality row (RowKind.Diamond today)` | emblem Diamond | `lorc/gems` | manapunk |
| Frame rate cap | `RowKind.Gauge` | emblem | `delapouite/speedometer` | manapunk |
| Effects | `Effects row (RowKind.New today)` | emblem New | `delapouite/sparkles` | manapunk |
| VSync | `RowKind.Sync` | emblem | `delapouite/clockwise-rotation` | manapunk |
| Screen shake | `RowKind.Shake` | emblem | `delapouite/vibrating-smartphone` | manapunk |
| Window mode, Windows | `RowKind.Window` | emblem | `delapouite/tv` | manapunk |
| Resolution | `RowKind.Frame` | emblem | `delapouite/expand` | manapunk |
| Thumb margin | `RowKind.Hand` | emblem | `delapouite/thumb-up` | manapunk |
| UI scale | `RowKind.Scale` | emblem | `delapouite/resize` | manapunk |
| Master volume | `RowKind.Speaker` | emblem | `delapouite/speaker` | manapunk |
| Music volume | `RowKind.Note` | emblem | `delapouite/musical-score` | manapunk |
| Sound volume | `RowKind.Wave` | emblem | `delapouite/sound-on` | manapunk |
| Mute music | `RowKind.NoteOff` | emblem | `delapouite/musical-score` (strike) | pewter |
| Mute sound | `RowKind.SpeakerOff` | emblem | `delapouite/speaker-off` | pewter |
| Mute when unfocused | `RowKind.WindowOff` | emblem | `delapouite/tv` (strike) | pewter |
| Text size | `RowKind.Text` | emblem | `lorc/magnifying-glass` | manapunk |
| Font | `RowKind.Type` | emblem | `delapouite/scroll-quill` | manapunk |
| Text wrapping | `Wrap row (RowKind.Lore today)` | emblem Lore | `lorc/quill-ink` | manapunk |
| Colorblind palette | `RowKind.Eye` | emblem | `lorc/semi-closed-eye` | manapunk |
| Reduce motion | `RowKind.Pause` | emblem | `lorc/sundial` | manapunk |
| Transfer / Import save | `RowKind.Save` | emblem | `delapouite/save` | manapunk |
| About, Legal | `RowKind.Document` | emblem | `lorc/papers` | manapunk |
| Developer rows | `RowKind.Settings` | emblem | `lorc/cog` | pewter |

### Statuses

Role: inline 32 (pips), row 64 (lists). Where: buffs.json + stunned; already generated (Resources/StatusIcons), kept as is.

| Concept | Code id | Today | Source (game-icons.net) | Palette |
|---|---|---|---|---|
| burning | `burning` | StatusIcons art | `carl-olsen/flame` | fire |
| frozen | `frozen` | StatusIcons art | `lorc/snowflake-2` | ice |
| wet | `wet` | StatusIcons art | `lorc/drop` | water |
| warm | `warm` | StatusIcons art | `lorc/sun` | sun |
| sleep | `sleep` | StatusIcons art | `delapouite/night-sleep` | sleep |
| poisoned | `poisoned` | StatusIcons art | `lorc/poison-bottle` | poison |
| slowed | `slowed` | StatusIcons art | `lorc/snail` | earth |
| silenced | `silenced` | StatusIcons art | `lorc/silence` | pink |
| blinded | `blinded` | StatusIcons art | `delapouite/eyelashes` | gray |
| protection | `protection` | StatusIcons art | `lorc/checked-shield` | green |
| ward | `ward` | StatusIcons art | `lorc/shield-reflect` | water |
| regen | `regen` | StatusIcons art | `zeromancer/heart-plus` | heal |
| stunned | `stunned` | StatusIcons art | `delapouite/stars-stack` | gold |
| taunting | `taunting` | StatusIcons art | `lorc/shouting` | crimson |
| empowered | `empowered` | StatusIcons art | `delapouite/biceps` | gold |
| blurred | `blurred` | StatusIcons art | `lorc/sprint` | silver |
| guarded | `guarded` | StatusIcons art | `lorc/magic-shield` | teal |
| linked | `linked` | StatusIcons art | `lorc/linked-rings` | steel |
| shredded | `shredded` | StatusIcons art | `lorc/shatter` | rust |
| rooted | `rooted` | StatusIcons art | `lorc/thorny-vine` | green |
| frightened | `frightened` | StatusIcons art | `lorc/screaming` | shadow |
| raging | `raging` | StatusIcons art | `lorc/bull-horns` | crimson |
| floating | `floating` | StatusIcons art | `lorc/feather` | silver |
| anchored | `anchored` | StatusIcons art | `lorc/anchor` | iron |

### KindEmblem kinds to migrate

Every `RowKind` that draws a code emblem today, and the inventory row whose icon replaces it.

| RowKind | Becomes |
|---|---|
| `Place` | chrome/Place |
| `Stage` | chrome/Stage |
| `Enter` | chrome/Enter / Go |
| `Item` | chrome/Item (fallback) |
| `Ability` | chrome/Ability (fallback) |
| `Status` | chrome/Status (fallback) |
| `Hero` | chrome/Hero / NPC / Keeper (fallback) |
| `Npc` | chrome/Hero / NPC / Keeper (fallback) |
| `Keeper` | chrome/Hero / NPC / Keeper (fallback) |
| `Bounty` | hub/Bounties |
| `Menu` | chrome/Menu (in-list) |
| `Quest` | hub/Quests |
| `Shop` | hub/Shop |
| `Station` | chrome/Station (generic) |
| `Back` | chrome/Back |
| `Close` | chrome/Close / cancel |
| `Confirm` | chrome/Confirm |
| `Locked` | chrome/Locked |
| `Cleared` | chrome/Cleared |
| `New` | chrome/New / unseen |
| `Settings` | hub/Settings |
| `Document` | chrome/Document |
| `Save` | chrome/Save |
| `Quit` | hub/Quit to title |
| `Talk` | hub/Talk |
| `Bestiary` | hub/Bestiary |
| `NewGamePlus` | hub/New Game+ |
| `Crucible` | hub/Crucible |
| `Diamond` | hub/Diamond Store |
| `Codex` | hub/Codex |
| `Lore` | chrome/Lore |
| `Faction` | chrome/Faction |
| `Home` | chrome/Home |
| `Summon` | hub/Summon |
| `Craft` | hub/Craft (group) |
| `Speed` | settings/Battle speed |
| `Timer` | settings/Drag timer, Untimed turns |
| `Focus` | settings/Pincer close-ups |
| `Gauge` | settings/Frame rate cap |
| `Shake` | settings/Screen shake |
| `Sync` | settings/VSync |
| `Window` | settings/Window mode, Windows |
| `Frame` | settings/Resolution |
| `Hand` | settings/Thumb margin |
| `Scale` | settings/UI scale |
| `Text` | settings/Text size |
| `Type` | settings/Font |
| `Eye` | settings/Colorblind palette |
| `Pause` | settings/Reduce motion |
| `Speaker` | settings/Master volume |
| `Note` | settings/Music volume |
| `Wave` | settings/Sound volume |
| `SpeakerOff` | settings/Mute sound |
| `NoteOff` | settings/Mute music |
| `WindowOff` | settings/Mute when unfocused |
| `Hamburger` | chrome/Hamburger |

### Unicode glyph icons to migrate

| Glyph | Used for | Where (Assets/GridGame/App, Meta) | Becomes |
|---|---|---|---|
| ★ U+2605 | Masterwork prefix; defeat feed line | Camp/BlacksmithScreen.cs 291, 389; EquipTab.cs 40; StatusTab.cs 95; ItemsScreen.cs 76; LuckyCraftUi.cs 48, 101; BattleScreen.Playback.cs 921 | currency/Masterwork; battle/Defeated |
| ✦ U+2726 | enchant / armor passive; support feed line; other item | Camp/EnchantressScreen.cs 76; EquipTab.cs 40, 74; StatusTab.cs 99; ItemsScreen.cs 41; BattleScreen.Playback.cs 693 | itemcat/Enchanted; battle/Support |
| 🔒 U+1F512 | locked tier or station label | Camp/BlacksmithScreen.cs 105, 177, 181, 201, 262, 267; Camp/EnchantressScreen.cs 185, 213 | chrome/Locked |
| ☠ U+2620 | bounty mark | BattleScreen.Bounty.cs 15, 22; BoardView.cs 844; StageSelectScreen.cs 78, 122; BountySkull.cs 25; CampMenu.cs 120 | map/Bounty |
| ⭾ U+2B7E | weapon swap | BattleScreen.Playback.cs 259; AbilitiesTab.cs 315 | battle/Weapon swap |
| ✚ U+271A | heal feed line; consumable | BattleScreen.Playback.cs 907; ItemsScreen.cs 38 | battle/Heal; itemcat/Consumable |
| ⚠ U+26A0 | hazard cell default | BattleScreen.Region.cs 324; BoardView.cs 977 | map/Danger |
| ♨ U+2668 | burn tick default | BattleScreen.Playback.cs 879 | status/burning |
| ▶ U+25B6 | turn start feed line | BattleScreen.Playback.cs 1817 | battle/Turn start |
| ⏲ U+23F2 | hazard countdown | BattleScreen.Region.cs 174 | battle/Countdown |
| ✓ U+2713 | acted badge | BoardView.cs 285 | battle/Acted |
| ✔ U+2714 | proficiency line | StatusTab.cs 145 | chrome/Cleared |
| ◎ U+25CE | region target cell | BoardView.cs 986 | battle/Region target cell |
| † ◆ ✧ ◈ ❖ U+2020 U+25C6 U+2727 U+25C8 U+2756 | item category marks | ItemsScreen.cs 37-40 | itemcat/Weapon, Armor, Accessory, Material, Recipe / Tome |

Kept as type, not icons: U+25B2 U+25BC (stat up / down inside a number (CombatText.cs 228, Ui.cs 957)); U+2192 (arrows inside prose (16 lines)); U+2212 (the minus sign in numbers (9 lines)).

### Board-cell glyphs (regionrules.json, vfxstyle.json `Glyph`)

| Rule ids | Glyph | Source | Palette |
|---|---|---|---|
| cracked | `⁂` | `lorc/earth-crack` | earth |
| crumbling | `✸` | `lorc/earth-crack` | rust |
| hole | `◌` | `delapouite/mountain-cave` | ash |
| cavein | `☄` | `delapouite/stone-pile` | earth |
| slime | `◍` | `lorc/vile-fluid` | green |
| slime_glob | `●` | `lorc/vile-fluid` | moss |
| choir_rune | `◇` | `lorc/rune-stone` | holy |
| burrow_mark | `◌` | `lorc/footprint` | earth |
| burrow_ambush | `⚠` | `lorc/hazard-sign` | crimson |
| substitution_log | `▬` | `delapouite/barrier` | wood |
| fire | `♨` | `lorc/small-fire` | fire |
| firewall | `♨` | `lorc/wildfires` | fire |
| oil | `~` | `lorc/drop` | ash |
| ice | `❄` | `lorc/snowflake-1` | ice |
| mine, buriedmine | `◈` | `lorc/circle-sparks` | iron |
| armedmine | `☢` | `lorc/circle-sparks` | crimson |
| mineblast | `✱` | `lorc/thunder-struck` | fire |
| timebomb | `⏲` | `delapouite/alarm-clock` | crimson |
| barricade | `#` | `delapouite/barrier` | iron |
| rubble | `:` | `delapouite/stone-pile` | ash |
| machinist_barricade | `▦` | `delapouite/barrier` | steel |
| shard-crystal | `^` | `lorc/crystal-cluster` | teal |
| shard-strike | `*` | `lorc/lightning-tear` | spark |
| anchor-post, anchor-heave | `@` | `lorc/anchor` | iron |
| floodwater, tide-1..3 | `~` | `lorc/drop` | water |
| brazier-light | `o` | `lorc/fire-bowl` | sun |
| holy-light | `+` | `lorc/sunbeams` | holy |
| flare-light | `*` | `lorc/sun-radiations` | sun |
| smoke | `~` | `lorc/tornado` | ash |
| sleep (vfxstyle) | `z` | `delapouite/night-sleep` | sleep |
