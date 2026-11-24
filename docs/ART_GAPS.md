# Art gap audit (GG-T491, refreshed 2026-11-23 by GG-T753)

Every missing, placeholder or reused asset that ships, ordered by how much a player sees it, with a ready-to-paste
Midjourney prompt for each. The owner generates the art (GG-T492). This file changes no code or content. Where an
asset also needs a small content or code change before it can show, that is called out as a **follow-up**.

## How this was computed

`tools/art_gaps.py` (GG-T752, read-only, not a build gate) loads `Assets/GridGame/Resources/Content/*.json`, lists the
art folders under `Assets/GridGame/Resources` and applies the lookups and fallbacks the game uses at runtime. Every
file name goes through `tools/asset_names.py` (the hyphenated `-00` layout, GG-T561):

| Asset | Lookup | Fallback when missing |
|---|---|---|
| Portrait | `Portraits/<ClassDef.PortraitFile>` (`Portrait` / `MalePortrait` / `PortraitVariant`, else the class id) | `Portraits/mannequin-00` (GG-T351), plus a log warning |
| Backdrop | `Backgrounds/<set>-NN` (`campaign.json` theme `Backgrounds` key, else the theme id) | the base theme (`swamp2` → `swamp`), then `greenvalley` |
| Board tile | `Tiles/<theme>-00` (theme `Tiles` key, else the theme id) | the base theme's tile, then `Tiles/tile-00` |
| Icons | `AbilityIcons/` (else legacy `Icons/<Icon>`), `ItemIcons/`, `StatusIcons/` (+ `stunned`), `UI/` (pickups) | none drawn |

Visibility comes from campaign order: stages 1-24 are the main campaign (it ends at `cathedral-03`) and 25-40 are the
postgame. Each class gets the first stage whose waves field it. Bosses are the rule-break finale enemies and the bounty
targets. Byte-identical portraits come from an MD5 pass.

Rerun: `python -I tools/art_gaps.py [--json out.json]`. It prints a summary; `--json` writes the full report. The app
icon is not in the script: it is set by `GridGameBuild.ApplyAppIcons` (GG-T486).

## Counts

| Kind | Gaps | Notes |
|---|---|---|
| App icon | 0 | Done (GG-T486), made from the Manalith sky art; a painted icon is optional (section 4) |
| Classes on the mannequin | 21 | The Hierarch (final boss), `mockingecho` (its file is a byte copy) and 19 postgame region foes |
| Other shared portraits | 2 | `templar-04` is a byte copy of `templar-03`; `nagaegg` draws `naga-00` |
| Rule-break finale bosses on a plain foe's art | 7 | Stages 3-21. The 8th, the Hierarch, is counted under the mannequin |
| Bounty targets on their base class's art | 11 | Plus the Lamp-Eater (`duskstalker`), which is on the mannequin |
| Themes with no backdrop set of their own | 3 | `swamp2`, `desert2`, `cave2` (stages 32-37) |
| Themes with no board tile of their own | 5 | Every postgame `…2` theme borrows its base theme's tile |
| Ability / item / status / pickup / caravan icons missing | 0 | 329 / 1,289 / 17 / 4 / 9, all covered |
| Male portrait variants missing | 0 | Every class with a `MalePortrait` has its file (the Warden's included) |

Done since the first audit: the app icon (GG-T486), the postgame stages now name their own `…2` themes (GG-T502), the
Warden's male portrait, and the Echo hero was cut (GG-T531).

On disk today: 260 portraits for 232 classes, 81 backdrops in 12 sets, 11 tiles, 330 ability icons, 1,289 item icons
and 18 status icons.

## House style for the prompts

- **Characters** match the existing portraits (for example `ceramic-knight-00.png`, `captain-00.png`, `oni-00.png`). They are
  full-body painterly anime concept art with loose ink linework, soft flat shading and a muted palette with one strong
  accent colour. Each figure stands alone on a plain light background, which `GridGame.Studio/tools/remove_bg.py` makes transparent,
  and `GridGame.Studio/tools/import_art.py` puts on the 1024×1024 transparent canvas (POL-H1). Faction colours follow STORY_OUTLINE §3.
  The Pale Concord is white with cyan, the Ember is red and black, the Street wears neon streetwear, the Free Holds wear
  greens and whites, plate and furs, the Lantern Road wears indigo and black with red sashes, and the marsh is dark blue.
  **Every character prompt ends `--niji 5`** (never `--v 7`).
- **Backdrops** match the existing sets (for example `arkgrave-00.jpg`). They are vertical cinematic matte paintings,
  near-photoreal, hazy and backlit, no people and no text. Each set has 4-8 images,
  delivered at 1536×3072 (1:2, POL-H3, mean luminance 0.25-0.50) and named `<set>-00.jpg` onward. The backdrop prompts
  carry no model flag. They are not characters, so `--niji` doesn't apply.
  **Two moons (GG-T575, BIBLE §27.3):** a new backdrop paints at most the ordinary **Moon**: one small, round, pale
  moon, about 7% of the frame's height, high in open sky. It never paints the **Manalith** (the lumpy second moon) or
  any large moon, planet or giant sphere. The Manalith is one canonical sprite composited in-engine at a size set by
  chapter, so leave a generous stretch of clear, uncluttered sky (upper third) for it to sit in. Interiors keep a
  window, oculus or opening that shows that sky.

Shared character suffix (the `{…}` in each prompt below):
`full body character concept art, standing three-quarter view, painterly anime style, loose ink linework, soft flat
shading, muted palette, isolated on plain off-white background, no text, no frame --ar 1:1 --niji 5`

Shared backdrop suffix (the `[…]` below):
`vertical cinematic matte painting, near-photoreal concept art, atmospheric haze, volumetric backlight, one small round
pale moon high in the sky, wide clear open sky in the upper third, no people, no text --ar 1:2 --style raw --no large
moon, planet, giant sphere, second moon`

---

## 1. Main campaign (stages 1-24)

### 1.1 Placeholder portraits (mannequin)

| # | Asset id | Where it shows | Now | Prompt |
|---|---|---|---|---|
| 1 | `hierarch` (The Hierarch, `Boss`) | **The final boss**: stage 24, The Choir's Cathedral III, wave finale (`Undying`). Also the boss-intro dialogue `boss_intro_cathedral`, cut-ins, inspect card, Bestiary | `classes.json` pins `"Portrait": "mannequin"`. **Follow-up:** once `hierarch-00.png` lands, drop that pin, and change the `dialogues.json` speaker `boss` (`"Portrait": "mannequin"`) to `"hierarch"` or drop it | `the Hierarch, ancient high priest of a white order, towering white ceremonial robes and tall white mitre fused with glowing cyan organ pipes and glass tubes of liquid light, pale ageless face, hands open as if drawing colour out of the air, faint cyan veins, serene and terrible, {…}` |
| 2 | `mockingecho` (Mocking Echo, foe) | Stages 19-20, Halcyon I-II. Inspect card, Bestiary | `mocking-echo-00.png` is a **byte copy of `mannequin-00.png`**; replace the file | `Mocking Echo, a pale faceless woman in white and violet glyph-printed poncho of the Choir, cracked mirror mask grinning, mimicking a fighting stance mockingly, faint cyan light, {…}` |

### 1.2 Bosses on reused art

Each rule-break finale boss is the same class, and so the same picture, as a foe the player fights as filler
elsewhere. Only the rule-break glow tells them apart. **Follow-up for all seven:** a portrait is looked up by class,
so a boss look needs either its own class id or a portrait-override field (code or content, not in scope here).

| # | Asset id | Where it shows | Now | Prompt |
|---|---|---|---|---|
| 3 | `oni02` boss | Stage 6, Lantern Road III finale (`Undying`) | Same art as the `oni02` filler in stages 4-5 | `oni warlord who will not die, towering masked demon in torn crimson and black kimono, cracked red oni mask, paper prayer charms burning on his arms, crouched with a great odachi, lantern light, {…}` |
| 4 | `mechaarmor01` boss | Stage 9, Shardfall III finale (`AxisWard`) | Same art as a filler in stage 35 | `shield-walker commander, white power armour with a tower shield on each arm locked into a wall, cyan aether core in the chest, red earth dust, Concord insignia, {…}` |
| 5 | `swampmistress00` boss | Stage 12, Arkgrave III finale (`MireSwap`) | Same art as a filler in stage 33 | `Swamp Mistress queen, veiled marsh witch rising from black water, crown of green tendrils and lotus, dark blue gown dripping, glowing green eyes, {…}` |
| 6 | `cyclops02` boss | Stage 15, Anchor Wastes III finale (`GravityStamp`) | Same art as a filler in stages 14 and 37 | `vat-giant patriarch, colossal one-eyed giant of white flesh with a single red eye, steel vat clamps and cyan tubes in its back, stamping the ground, pink snow, {…}` |
| 7 | `chronomancer` boss | Stage 18, The Still III finale (`TwinTurn`) | Same art as a filler in stage 14 | `Chronomancer ascendant, white-robed time mage surrounded by floating brass clock wheels and cyan dials, two overlapping afterimages of himself, {…}` |
| 8 | `ceramicknight06` boss | Stage 21, Halcyon III finale (`AxisWard`) | Same art as a filler in stage 24 | `Kiln Guard champion, porcelain knight in a long white cape with red glyphs, cracked golden kintsugi seams across its armour, great porcelain kite shield and longsword, {…}` |
| 9 | `captain` boss | Stage 3, Tithe Moors III finale (`TwinTurn`) | Same art as a foe in stages 1, 9, 29, 34, 35. **Partly by design** (the recurring villain), so low priority | `the Captain, hold officer in dark plate under a white Concord tabard, scarred, cloak torn by moor wind, drawn sword and raised gauntlet giving an order, {…}` |

### 1.3 Shared portraits

| # | Asset id | Where it shows | Now | Prompt |
|---|---|---|---|---|
| 10 | `nagaegg` (Naga Egg) | Laid in battle by `naga00`, stage 11, Arkgrave II. Inspect card | `classes.json` gives it `"Portrait": "naga"`, so the egg draws as a whole naga. **Follow-up:** point it at its own file (`naga-egg-00.png`) once the art lands | `a large naga egg, leathery teal shell with dark blue scale patterns, glowing veins and a shadow coiling inside, nested in marsh reeds and gold haze, game creature portrait, {…}` |
| 11 | `templar04` (Templar) | Stage 27 (Bloom III); `templar03` is in stages 24 and 26. Crucible Red Seal waves | `templar-04.png` is a **byte copy of `templar-03.png`**; replace the file | `Red Seal templar zealot, white robes with red cross tabard, chain hood, censer flail and tower shield, stern eyes, {…}` |

### 1.4 Bounty targets on base art

A bounty target is a titled, tougher variant of a normal foe with a ☠ marker, drawn with its base class's portrait:
acceptable but generic. **Follow-up:** a bounty-portrait field would be needed for unique art. The 12th bounty, the
Lamp-Eater, is in section 2.3 (`duskstalker`).

| # | Bounty (target class, stage) | Prompt |
|---|---|---|
| 12 | Grimjaw the Mauler (`wolf02`, greenvalley-02) | `Grimjaw, huge scarred grey moor wolf, torn ear, iron trap still clamped on one leg, bared broken fangs, {…}` |
| 13 | Mother Gloam (`slime02`, greenvalley-03) | `Mother Gloam, enormous murky green slime with smaller slimes and swallowed bones floating inside, {…}` |
| 14 | Kurogane the Lantern-Breaker (`oni00`, lanternroad-01) | `Kurogane, iron-skinned oni brute dragging a smashed stone lantern on a chain, black kimono, {…}` |
| 15 | Ashpelt the Moonless (`werewolf00`, lanternroad-02) | `Ashpelt, ash-grey werewolf with a burned pelt and blind white eyes, howling, {…}` |
| 16 | Thirstmouth (`sandmaw`, desert-02) | `Thirstmouth, gigantic sand maw worm bursting from red earth, ring of crystal teeth, {…}` |
| 17 | Rotbark the Unfelled (`treegolem02`, swamp-02) | `Rotbark, moss golem of a skeleton grown through with a rotting black tree, axes still stuck in it, {…}` |
| 18 | Hollowe the Vat-Giant (`cyclops00`, cave-01) | `Hollowe, hunched white-flesh cyclops with a hollow cyan-lit chest cavity, chains on wrists, {…}` |
| 19 | Saint Shroud, the Unburied (`undead01`, still-01) | `Saint Shroud, tall hooded Hollow wrapped in white burial shroud and halo of candles, empty face, {…}` |
| 20 | The Pale Bellringer (`ghost`, cityruins-02) | `the Pale Bellringer, hooded ghost girl swinging a cracked bronze bell, translucent, pink-red city glow, {…}` |
| 21 | Brother Vesper, Last of the Choir (`templar01`, cathedral-02) | `Brother Vesper, old templar in torn white and red robes holding a guttering cyan lamp, {…}` |
| 22 | Sergeant Iron Tithe (`heavytrooper03`, bloom-02) | `Sergeant Iron Tithe, white power-armour trooper with tally marks painted in red, siege rifle, aether pack, red blossom on armour, {…}` |

## 2. Postgame (stages 25-40)

### 2.1 Regions with no backdrop set

Battle backdrop, results, level-up and region card borrow the base region's set. The stages already name their own
theme (GG-T502), so only the art is missing. **Follow-up:** add the set name as the theme's `Backgrounds` key in
`campaign.json` (as `free-holds` and `underspire` do), or name the files after the theme id (`swamp2-00.jpg`).

| # | Asset id (proposed set) | Where it shows | Now | Prompt (generate 4-6) |
|---|---|---|---|---|
| 23 | `drowned-arks-00..` for theme `swamp2` (The Drowned Arks) | Stages 32-33, region card, results, level-up | Borrows `arkgrave` (5 images) | `flooded graveyard of colossal wrecked ships half-sunk in dark rising water, broken masts and tangled rigging, gold haze over the flood, rain on the surface, [ … ]` |
| 24 | `deep-shardfall-00..` for theme `desert2` (Deep Shardfall) | Stages 34-35 | Borrows `shardfall` (5 images) | `deep red canyon where giant curved bone-white horns and crystal shards jut from the earth, rivers of glowing red, shards still falling as streaks of light, [ … ]` |
| 25 | `under-the-anchors-00..` for theme `cave2` (Under the Anchors) | Stages 36-37 | Borrows `anchor-wastes` (4 images) | `beneath colossal blue stone arches, enormous rusted chains as thick as towers hanging into pink-lit snow caverns, frost, faint moonlight through the arch gaps, [ … ]` |

### 2.2 Board tiles borrowed from the base theme

Tiles are generated, not painted (`GridGame.Studio/tools/make_tiles.py [theme …]`), so no Midjourney prompt is needed.
Run it for these themes once a pattern is chosen; each writes `Tiles/<theme>-00.png`.

| # | Theme | Shows now |
|---|---|---|
| 26 | `greenvalley2` (The Free Holds) | `greenvalley-00` |
| 27 | `cityruins2` (The Underspire) | `cityruins-00` |
| 28 | `swamp2` (The Drowned Arks) | `swamp-00` |
| 29 | `desert2` (Deep Shardfall) | `desert-00` |
| 30 | `cave2` (Under the Anchors) | `cave-00` |

### 2.3 Region-rule foes on the mannequin

All are foes in stage waves and join Crucible waves once met. Each pins `"Portrait": "mannequin"` in `classes.json`,
so it draws as the mannequin in battle, on the inspect card, in the Bestiary and on the death dissolve.
**Follow-up:** drop each pin when its `<name>-00.png` lands.

| # | Asset id (name) | First stage | Prompt |
|---|---|---|---|
| 31 | `holdwright` (Holdwright) | 28 The Free Holds I | `Holdwright, sturdy hold carpenter woman with nails in her teeth, green cloak and leather apron, warhammer and a timber beam on her shoulder, wooden palisade behind, {…}` |
| 32 | `holdram` (Hold Ram) | 28 The Free Holds I | `Hold Ram, huge fur-cloaked hold warrior carrying an iron-capped battering-ram log, dented helm, greens and whites, {…}` |
| 33 | `holdmarksman` (Hold Marksman) | 28 The Free Holds I | `Hold Marksman, patient archer in green hood and white tabard, heavy longbow with a piercing bodkin arrow drawn, quiver of fletched arrows, {…}` |
| 34 | `spireburrower` (Spire Burrower) | 30 The Underspire I | `Spire Burrower, mole-like tunnelling beast with chrome digging claws and a drill snout, wet fur, neon reflections, {…}` |
| 35 | `pitlurker` (Pit Lurker) | 30 The Underspire I | `Pit Lurker, long-limbed pale cave beast with many glowing eyes clinging to a broken floor edge, crouched to pounce, {…}` |
| 36 | `spireshover` (Floorbreaker) | 30 The Underspire I | `Floorbreaker, hulking demolition worker in grey coveralls under the white spire, hydraulic sledge gauntlets cracking concrete, neon rain, {…}` |
| 37 | `tidestrider` (Tidestrider) | 32 The Drowned Arks I | `Tidestrider, lean amphibious hunter beast on stilt-like legs, eel-skinned, crackling with static, water streaming off it, {…}` |
| 38 | `stormcaller` (Stormcaller) | 32 The Drowned Arks I | `Stormcaller, scaled sea-shaman in a drowned sailor's coat, harpoon staff crackling with lightning, barnacles, {…}` |
| 39 | `bloatdrowner` (Bloated Drowner) | 32 The Drowned Arks I | `Bloated Drowner, swollen drowned ship's hand, pale bloated skin, seaweed and barnacles, water leaking from its mouth, {…}` |
| 40 | `shardcaller` (Shardcaller) | 34 Deep Shardfall I | `Shardcaller, Ember zealot in black robes and long red cape, arms raised calling down glowing red moon shards that orbit above, {…}` |
| 41 | `crystalbrute` (Crystal Brute) | 34 Deep Shardfall I | `Crystal Brute, hulking construct of pale moon crystal with red veins, crystal growths sprouting where it steps, {…}` |
| 42 | `shardskitter` (Shard Skitter) | 34 Deep Shardfall I | `Shard Skitter, quick insect beast with a carapace of jagged pale crystal shards, many thin legs, red earth dust, {…}` |
| 43 | `chainwarden` (Chain Warden) | 36 Under the Anchors I | `Chain Warden, grey-plated warden with a cloak of old fire, hauling a great anchor chain over one shoulder, frost on armour, {…}` |
| 44 | `anchorhulk` (Anchor Hulk) | 36 Under the Anchors I | `Anchor Hulk, immovable construct of blue arch stone bound in rusted chains, a ship's anchor as one arm, {…}` |
| 45 | `hookthrower` (Hookthrower) | 36 Under the Anchors I | `Hookthrower, wiry hooded figure swinging a barbed hook on a long chain, ragged grey wraps, pink snow, {…}` |
| 46 | `lampsnuffer` (Lamp Snuffer) | 38 Morrow's Shadow I | `Lamp Snuffer, sly hooded figure with a long brass candle-snuffer pole, trailing smoke, eyes hidden, {…}` |
| 47 | `duskstalker` (Dusk Stalker), also the bounty **The Lamp-Eater** (morrowsshadow-01) | 38 Morrow's Shadow I | `Dusk Stalker, sleek black predator beast with glowing amber eyes and lantern-like spots, low hunting crouch, {…}` (Lamp-Eater variant: `…with swallowed lanterns glowing through its ribs`) |
| 48 | `gloommender` (Gloom Mender) | 38 Morrow's Shadow I | `Gloom Mender, veiled healer in grey-violet robes holding a dim violet lantern, half dissolved into shadow, {…}` |
| 49 | `duskmimic` (Dusk Mimic) | 38 Morrow's Shadow I | `Dusk Mimic, shadow creature shaped like a young adventurer, featureless dark face with a borrowed smile, colours flickering like a stolen outfit, {…}` |

## 3. Not shipping, recorded for completeness

- The Monster Tamer's female look draws `wild-child-female-00.png`; its male look has its own file. It is hidden with
  Taming, so only the Wild Child shows. Give it its own art if Taming returns.
- `aetherist-00.png` and `aetherist-male-00.png` are on disk but no class draws them.
- `mannequin` is the dev/training dummy and the fallback art; it appears in no stage.
- No icon gaps (icons are generated by `GridGame.Studio/tools/make_icons.py`, not Midjourney).
- The premium currency (Aetherite to players, GG-T568; item `mat_diamond`) still uses the `lorc/cut-diamond` icon in
  `tools/icons.json`. Nice to have, not blocking: a jagged pale blue-violet crystal shard (such as `lorc/crystal-shard`).

## 4. Optional: a painted app icon

The shipped icon (GG-T486) is the Manalith and the Moon on a night sky, made by `GridGame.Studio/tools/make_app_icon.py`
into `Assets/GridGame/AppIcon/` and set on every build by `GridGameBuild.ApplyAppIcons`. For a painted replacement,
generate at 1024×1024 or larger with the subject in the centre 66% (Android adaptive foreground):
`square app icon, a stylized white porcelain knight's helm split by a crimson pincer of two blades, huge pale moon
behind, bold readable silhouette, painterly anime style, deep indigo background, centered, no text --ar 1:1 --niji 5`

