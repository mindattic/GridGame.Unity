# Art gap audit (GG-T491, 2026-11-20)

Every missing, placeholder or reused asset that ships, ordered by how much a player sees it, with a ready-to-paste
Midjourney prompt for each. The owner generates the art (GG-T492). This file changes no code or content. Where an
asset also needs a small content or code change before it can show, that is called out as a **follow-up**.

## How this was computed

A Python script (Appendix A, read-only) loads `Assets/GridGame/Resources/Content/*.json` and lists the art folders under
`Assets/GridGame/Resources`. It applies the fallbacks the game uses at runtime:

| Asset | Lookup (code) | Fallback when missing |
|---|---|---|
| Portrait | `Portraits/<classId>` (`App/Services/Art.cs` `Portrait`) | `Portraits/mannequin` (GG-T351), plus a log warning |
| Backdrop | the theme's `Backgrounds` set, else its id, `<set>_01..` (`Core/Content/Backdrops.cs` `Resolve`) | the base theme (`swamp2` → `swamp`), then `greenvalley`, then `tithemoors_01` |
| Board tile | `Tiles/<theme>` (`Art.BoardTile`) | the base theme's tile, then `Tiles/tile` |
| Ability icon | `AbilityIcons/<id>`, else legacy `Icons/<Icon>` | none drawn |
| Item icon | `ItemIcons/<id>` | none drawn |
| Status icon | `StatusIcons/<buffId>` (+ `stunned`) | none drawn |
| App icon | `ProjectSettings.asset` `m_BuildTargetIcons` / `m_BuildTargetPlatformIcons` | Unity's default icon |

Visibility comes from campaign order: `campaign.json` `Themes[].Stages` gives stage numbers 1-40. Stages 1-24 are the
main campaign (it ends at `Ending.Stage` = `cathedral-03`) and 25-40 are the postgame. Each class gets the first stage
whose waves field it (including wave `Group`s from `encounters.json`, guests, recruitables and surprise speakers).
Class ids named anywhere else (factions, summon pool, mercenaries, bounties, quests, dialogues) are recorded as well.
Bosses are the rule-break enemies in stage waves (`RuleBreak`, BIBLE §14.9), the classes tagged `Boss`, and the
bounty targets. Byte-identical portrait files were found with an MD5 pass, which is how the two Echo portraits (the hero's since cut, GG-T531) turned
out to be copies of the mannequin.

Rerun: `python -I <path>/art_gaps.py D:\Projects\MindAttic\Unity.GridGame`. It prints JSON.

## Counts

| Kind | Gaps | Notes |
|---|---|---|
| App icon | 0 | Set from the Manalith sky art (GG-T486); a painted icon is optional |
| Portraits missing, so the mannequin shows | 20 | The Hierarch, plus 19 region-rule foes in the postgame |
| Portraits that are byte copies of the mannequin | 1 | `mockingecho` (foe). The `echo` hero copy went with the class (GG-T531) |
| Other duplicated portraits that ship | 2 pairs | `templar03` = `templar04`, `naga00` = `nagaegg` |
| Rule-break finale bosses on a plain foe's art | 7 | Stages 3-21. The 8th, the Hierarch, is counted under missing |
| Bounty targets on their base class's art | 11 | Plus `duskstalker`'s bounty, which has no art at all (counted under missing) |
| Regions with no backdrop set of their own | 3 | `swamp2`, `desert2`, `cave2` (stages 32-37) |
| Themes with no board tile of their own | 5 | Every postgame `…2` theme borrows its base theme's tile |
| Ability / item / status / pickup / caravan icons missing | 0 | 324 / 1,287 / 16 / 4 / 9, all covered (`tools/check_assets.py` already fails on these) |
| Content follow-ups (not art) | 2 | The Hierarch's dialogue pins `"Portrait": "mannequin"`. Six postgame stages carry the base theme id |

On disk today: 213 portraits for 233 classes, 81 backdrops in 12 sets, 11 tiles, 325 ability icons, 1,287 item icons
and 17 status icons.

## House style for the prompts

- **Characters** match the existing portraits (for example `ceramicknight00.png`, `captain.png`, `oni00.png`). They are
  full-body painterly anime concept art with loose ink linework, soft flat shading and a muted palette with one strong
  accent colour. Each figure stands alone on a plain light background, which `GridGame.Studio/tools/remove_bg.py` makes transparent,
  and `GridGame.Studio/tools/import_art.py` puts on the 1024×1024 transparent canvas (POL-H1). Faction colours follow STORY_OUTLINE §3.
  The Pale Concord is white with cyan, the Ember is red and black, the Street wears neon streetwear, the Free Holds wear
  greens and whites, plate and furs, the Lantern Road wears indigo and black with red sashes, and the marsh is dark blue.
  **Every character prompt ends `--niji 5`** (never `--v 7`).
- **Backdrops** match the existing sets (for example `arkgrave_01.jpg`). They are vertical cinematic matte paintings,
  near-photoreal, hazy and backlit, no people and no text. Each set has 4-8 images,
  delivered at 1536×3072 (1:2, POL-H3, mean luminance 0.25-0.50) and named `<set>_01.jpg` onward. The backdrop prompts
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

## 1. Everywhere: the app icon

| Asset id | Where it shows | Now | Prompt |
|---|---|---|---|
| App icon (iOS, Android legacy and adaptive foreground/background) | Home screen, store listing, task switcher, notifications | Done for now (GG-T486): the Manalith with the Moon behind it on a night sky, made from the game's own sky art by `GridGame.Studio/tools/make_app_icon.py` into `Assets/GridGame/AppIcon/` (1024 master, 512 Play listing icon, Android legacy/round/adaptive at every density); `GridGameBuild.ApplyAppIcons` sets them on every build. The prompt is for a painted replacement if the owner wants one | `square app icon, a stylized white porcelain knight's helm split by a crimson pincer of two blades, huge pale moon behind, bold readable silhouette, painterly anime style, deep indigo background, centered, no text --ar 1:1 --niji 5` |

Generate at 1024×1024 or larger. The Android adaptive foreground should keep the subject in the centre 66%. Use the
same prompt with "no helm, background only" for the background layer.

## 2. Main campaign (stages 1-24)

### 2.1 Placeholder portraits (mannequin)

| # | Asset id | Where it shows | Now | Prompt |
|---|---|---|---|---|
| 1 | `hierarch` (The Hierarch, `Boss`) | **The final boss**: stage 24, The Choir's Cathedral III, wave finale (`Undying`). Also the boss-intro dialogue `boss_intro_cathedral`, cut-ins, inspect card, Bestiary | No file, so the mannequin shows. **Follow-up:** `dialogues.json` speaker `boss` has `"Portrait": "mannequin"` pinned, which must become `"hierarch"` (or be dropped) once the art lands | `the Hierarch, ancient high priest of a white order, towering white ceremonial robes and tall white mitre fused with glowing cyan organ pipes and glass tubes of liquid light, pale ageless face, hands open as if drawing colour out of the air, faint cyan veins, serene and terrible, {…}` |
| 2 | ~~`echo`~~ | **Cut (GG-T531):** the Echo hero class was removed and `echo.png` deleted. No art needed | — | — |
| 2a | `warden` (Warden, the fixed lead, GG-T533) | Every campaign from the first battle: character select, party, battle, cut-ins | The owner's female Warden portrait ships (`Portraits/warden.png`). **Still to come:** the male variant (GG-T532, every class in female and male versions; the player picks one when a hero joins). Male candidates are staged in the git-ignored `_male_variants/` and reach `Portraits/` only via GridGame.Studio's `male_variants_gallery.py --import-picks` after the owner picks (GG-T612; layout in GridGame.Studio's README) | — |
| 3 | `mockingecho` (Mocking Echo, foe) | Stages 19-20, Halcyon I-II. Inspect card, Bestiary | `mockingecho.png` is a **byte copy of the mannequin** | `Mocking Echo, Echo's pale twin, a faceless woman in white and violet glyph-printed poncho of the Choir, cracked mirror mask grinning, mimicking a fighting stance mockingly, faint cyan light, {…}` |

### 2.2 Bosses on reused art

Each rule-break finale boss is the same class, and so the same picture, as a foe the player fights as filler
elsewhere. Only the rule-break glow tells them apart.
**Follow-up for all seven:** a portrait is looked up by class id, so a boss look needs either its own class id or a
portrait-override field (code or content, not in scope here).

| # | Asset id | Where it shows | Now | Prompt |
|---|---|---|---|---|
| 4 | `oni02` boss | Stage 6, Lantern Road III finale (`Undying`) | Same art as the `oni02` filler in stages 4-5 | `oni warlord who will not die, towering masked demon in torn crimson and black kimono, cracked red oni mask, paper prayer charms burning on his arms, crouched with a great odachi, lantern light, {…}` |
| 5 | `mechaarmor01` boss | Stage 9, Shardfall III finale (`AxisWard`) | Same art as a filler in stage 35 | `shield-walker commander, white power armour with a tower shield on each arm locked into a wall, cyan aether core in the chest, red earth dust, Concord insignia, {…}` |
| 6 | `swampmistress00` boss | Stage 12, Arkgrave III finale (`MireSwap`) | Same art as a filler in stage 33 | `Swamp Mistress queen, veiled marsh witch rising from black water, crown of green tendrils and lotus, dark blue gown dripping, glowing green eyes, {…}` |
| 7 | `cyclops02` boss | Stage 15, Anchor Wastes III finale (`GravityStamp`) | Same art as a filler in stages 14 and 37 (also `Boss`-tagged) | `vat-giant patriarch, colossal one-eyed giant of white flesh with a single red eye, steel vat clamps and cyan tubes in its back, stamping the ground, pink snow, {…}` |
| 8 | `chronomancer` boss | Stage 18, The Still III finale (`TwinTurn`) | Same art as a filler in stage 14 | `Chronomancer ascendant, white-robed time mage surrounded by floating brass clock wheels and cyan dials, two overlapping afterimages of himself, {…}` |
| 9 | `ceramicknight06` boss | Stage 21, Halcyon III finale (`AxisWard`) | Same art as a filler in stage 24 | `Kiln Guard champion, porcelain knight in a long white cape with red glyphs, cracked golden kintsugi seams across its armour, great porcelain kite shield and longsword, {…}` |
| 10 | `captain` boss | Stage 3, Tithe Moors III finale (`TwinTurn`) | Same art as a foe in stages 1, 9, 29, 34, 35. This is **partly by design**, since the Captain is the recurring villain, so it's low priority | `the Captain, hold officer in dark plate under a white Concord tabard, scarred, cloak torn by moor wind, drawn sword and raised gauntlet giving an order, {…}` |

### 2.3 Duplicated portraits

| # | Asset id | Where it shows | Now | Prompt |
|---|---|---|---|---|
| 11 | `nagaegg` (Naga Egg) | Laid in battle by `naga00`, stage 11, Arkgrave II. Inspect card | `nagaegg.png` is a **byte copy of `naga00.png`**, so the egg draws as a whole naga | `a large naga egg, leathery teal shell with dark blue scale patterns, glowing veins and a shadow coiling inside, nested in marsh reeds and gold haze, game creature portrait, {…}` |
| 12 | `templar04` (Templar) | Stage 27 (Bloom III); `templar03` is in stages 24 and 26. Crucible Red Seal waves | `templar04.png` is a **byte copy of `templar03.png`** | `Red Seal templar zealot, white robes with red cross tabard, chain hood, censer flail and tower shield, stern eyes, {…}` |

### 2.4 Bounty targets on base art (main campaign)

The bounty board ships (`Features.Bounties = true`). A target is a titled, tougher variant of a normal foe and wears a
☠ marker. Today it draws with its base class's portrait. That is acceptable by design but generic. **Follow-up:**
a bounty-portrait field would be needed for unique art.

| # | Bounty (target class, stage) | Prompt |
|---|---|---|
| 13 | Grimjaw the Mauler (`wolf02`, greenvalley-02) | `Grimjaw, huge scarred grey moor wolf, torn ear, iron trap still clamped on one leg, bared broken fangs, {…}` |
| 14 | Mother Gloam (`slime02`, greenvalley-03) | `Mother Gloam, enormous murky green slime with smaller slimes and swallowed bones floating inside, {…}` |
| 15 | Kurogane the Lantern-Breaker (`oni00`, lanternroad-01) | `Kurogane, iron-skinned oni brute dragging a smashed stone lantern on a chain, black kimono, {…}` |
| 16 | Ashpelt the Moonless (`werewolf00`, lanternroad-02) | `Ashpelt, ash-grey werewolf with a burned pelt and blind white eyes, howling, {…}` |
| 17 | Thirstmouth (`sandmaw`, desert-02) | `Thirstmouth, gigantic sand maw worm bursting from red earth, ring of crystal teeth, {…}` |
| 18 | Rotbark the Unfelled (`treegolem02`, swamp-02) | `Rotbark, moss golem of a skeleton grown through with a rotting black tree, axes still stuck in it, {…}` |
| 19 | Hollowe the Vat-Giant (`cyclops00`, cave-01) | `Hollowe, hunched white-flesh cyclops with a hollow cyan-lit chest cavity, chains on wrists, {…}` |
| 20 | Saint Shroud, the Unburied (`undead01`, still-01) | `Saint Shroud, tall hooded Hollow wrapped in white burial shroud and halo of candles, empty face, {…}` |
| 21 | The Pale Bellringer (`ghost`, cityruins-02) | `the Pale Bellringer, hooded ghost girl swinging a cracked bronze bell, translucent, pink-red city glow, {…}` |
| 22 | Brother Vesper, Last of the Choir (`templar01`, cathedral-02) | `Brother Vesper, old templar in torn white and red robes holding a guttering cyan lamp, {…}` |
| 23 | Sergeant Iron Tithe (`heavytrooper03`, bloom-02) | `Sergeant Iron Tithe, white power-armour trooper with tally marks painted in red, siege rifle, aether pack, red blossom on armour, {…}` |

## 3. Postgame (stages 25-40)

### 3.1 Regions with no backdrop set

The battle backdrop, results, level-up and region card all borrow the base region's set.
**Follow-up:** battles read `stages.json` `Theme`, which for these six stages is the *base* theme (`swamp-04/05`:
`swamp`, `desert-04/05`: `desert`, `cave-04/05`: `cave`). Even with new art and a `Backgrounds` entry on the campaign
theme, the battles would keep the old set until those stages' `Theme` becomes `swamp2` / `desert2` / `cave2`, which
is how `greenvalley-04` and `cityruins-04` already work.

| # | Asset id (proposed set) | Where it shows | Now | Prompt (generate 4-6) |
|---|---|---|---|---|
| 24 | `drownedarks_01..` for theme `swamp2` (The Drowned Arks) | Stages 32-33, region card, results, level-up | Borrows `arkgrave` (5 images) | `flooded graveyard of colossal wrecked ships half-sunk in dark rising water, broken masts and tangled rigging, gold haze over the flood, rain on the surface, [ … ]` |
| 25 | `deepshardfall_01..` for theme `desert2` (Deep Shardfall) | Stages 34-35 | Borrows `shardfall` (5 images) | `deep red canyon where giant curved bone-white horns and crystal shards jut from the earth, rivers of glowing red, shards still falling as streaks of light, vast pale planet filling half the sky, [ … ]` |
| 26 | `undertheanchors_01..` for theme `cave2` (Under the Anchors) | Stages 36-37 | Borrows `anchorwastes` (4 images) | `beneath colossal blue stone arches, enormous rusted chains as thick as towers hanging into pink-lit snow caverns, frost, faint moonlight through the arch gaps, [ … ]` |

### 3.2 Board tiles borrowed from the base theme

Tiles are generated, not painted (`GridGame.Studio/tools/make_tiles.py [theme …]` re-fills the original bevelled tile with a
per-biome procedural pattern). No Midjourney prompt is needed. Run the script for these themes once a pattern is chosen.

| # | Theme | Shows now |
|---|---|---|
| 27 | `greenvalley2` (The Free Holds) | `greenvalley` |
| 28 | `cityruins2` (The Underspire) | `cityruins` |
| 29 | `swamp2` (The Drowned Arks) | `swamp` (also gated by the stage `Theme` follow-up above) |
| 30 | `desert2` (Deep Shardfall) | `desert` (same) |
| 31 | `cave2` (Under the Anchors) | `cave` (same) |

### 3.3 Region-rule foes with no portrait (mannequin)

All of these are foes in stage waves, and they also join Crucible waves once met. None has a file, so each draws as the
mannequin in battle, on the inspect card, in the Bestiary and on the death dissolve.

| # | Asset id (name) | First stage | Prompt |
|---|---|---|---|
| 32 | `holdwright` (Holdwright) | 28 The Free Holds I | `Holdwright, sturdy hold carpenter woman with nails in her teeth, green cloak and leather apron, warhammer and a timber beam on her shoulder, wooden palisade behind, {…}` |
| 33 | `holdram` (Hold Ram) | 28 The Free Holds I | `Hold Ram, huge fur-cloaked hold warrior carrying an iron-capped battering-ram log, dented helm, greens and whites, {…}` |
| 34 | `holdmarksman` (Hold Marksman) | 28 The Free Holds I | `Hold Marksman, patient archer in green hood and white tabard, heavy longbow with a piercing bodkin arrow drawn, quiver of fletched arrows, {…}` |
| 35 | `spireburrower` (Spire Burrower) | 30 The Underspire I | `Spire Burrower, mole-like tunnelling beast with chrome digging claws and a drill snout, wet fur, neon reflections, {…}` |
| 36 | `pitlurker` (Pit Lurker) | 30 The Underspire I | `Pit Lurker, long-limbed pale cave beast with many glowing eyes clinging to a broken floor edge, crouched to pounce, {…}` |
| 37 | `spireshover` (Floorbreaker) | 30 The Underspire I | `Floorbreaker, hulking demolition worker in grey coveralls under the white spire, hydraulic sledge gauntlets cracking concrete, neon rain, {…}` |
| 38 | `tidestrider` (Tidestrider) | 32 The Drowned Arks I | `Tidestrider, lean amphibious hunter beast on stilt-like legs, eel-skinned, crackling with static, water streaming off it, {…}` |
| 39 | `stormcaller` (Stormcaller) | 32 The Drowned Arks I | `Stormcaller, scaled sea-shaman in a drowned sailor's coat, harpoon staff crackling with lightning, barnacles, {…}` |
| 40 | `bloatdrowner` (Bloated Drowner) | 32 The Drowned Arks I | `Bloated Drowner, swollen drowned ship's hand, pale bloated skin, seaweed and barnacles, water leaking from its mouth, {…}` |
| 41 | `shardcaller` (Shardcaller) | 34 Deep Shardfall I | `Shardcaller, Ember zealot in black robes and long red cape, arms raised calling down glowing red moon shards that orbit above, {…}` |
| 42 | `crystalbrute` (Crystal Brute) | 34 Deep Shardfall I | `Crystal Brute, hulking construct of pale moon crystal with red veins, crystal growths sprouting where it steps, {…}` |
| 43 | `shardskitter` (Shard Skitter) | 34 Deep Shardfall I | `Shard Skitter, quick insect beast with a carapace of jagged pale crystal shards, many thin legs, red earth dust, {…}` |
| 44 | `chainwarden` (Chain Warden) | 36 Under the Anchors I | `Chain Warden, grey-plated warden with a cloak of old fire, hauling a great anchor chain over one shoulder, frost on armour, {…}` |
| 45 | `anchorhulk` (Anchor Hulk) | 36 Under the Anchors I | `Anchor Hulk, immovable construct of blue arch stone bound in rusted chains, a ship's anchor as one arm, {…}` |
| 46 | `hookthrower` (Hookthrower) | 36 Under the Anchors I | `Hookthrower, wiry hooded figure swinging a barbed hook on a long chain, ragged grey wraps, pink snow, {…}` |
| 47 | `lampsnuffer` (Lamp Snuffer) | 38 Morrow's Shadow I | `Lamp Snuffer, sly hooded figure with a long brass candle-snuffer pole, trailing smoke, eyes hidden, {…}` |
| 48 | `duskstalker` (Dusk Stalker), also the bounty **The Lamp-Eater** | 38 Morrow's Shadow I | `Dusk Stalker, sleek black predator beast with glowing amber eyes and lantern-like spots, low hunting crouch, {…}` (Lamp-Eater variant: `…with swallowed lanterns glowing through its ribs`) |
| 49 | `gloommender` (Gloom Mender) | 38 Morrow's Shadow I | `Gloom Mender, veiled healer in grey-violet robes holding a dim violet lantern, half dissolved into shadow, {…}` |
| 50 | `duskmimic` (Dusk Mimic) | 38 Morrow's Shadow I | `Dusk Mimic, shadow creature shaped like a young adventurer, featureless dark face with a borrowed smile, colours flickering like a stolen outfit, {…}` |

## 4. Not shipping, recorded for completeness

- `monstertamer.png` is a byte copy of `wildchild.png`. The Monster Tamer is hidden with Taming (`Features.Taming =
  false`), so only the Wild Child shows. Give it its own art if Taming returns.
- `mannequin` is a real enemy class with four encounter groups (`mannequin-*`) but appears in no stage, so neither the
  campaign nor the Crucible fields it. It is the dev/training dummy and the fallback art.
- No icon gaps: every ability, item, buff (+ `stunned`), pickup and caravan upgrade has its pixel icon (icons are
  generated by `GridGame.Studio/tools/make_icons.py`, not Midjourney).
- **The premium currency's icon is still a literal diamond (GG-T568).** `mat_diamond` is now the **Mana Shard**
  (BIBLE §24.9.1), but its pixel icon (`tools/icons.json`: `lorc/cut-diamond`, ice tint) is a cut gem. New art wanted:
  a jagged shard of pale blue-violet crystal with amber flecks, broken off the Manalith's crust (a crystal-shard
  game-icon such as `lorc/crystal-shard`, or an owner-drawn icon), used on the Shard Store, the counters, the summon
  draw and the lucky craft. Not blocking: the diamond reads as "premium gem" until it lands.

## Appendix A: the enumeration script

<details><summary>art_gaps.py (read-only; prints JSON)</summary>

```python
import glob, hashlib, json, os, sys, collections
ROOT = sys.argv[1] if len(sys.argv) > 1 else "."
RES = os.path.join(ROOT, "Assets", "GridGame", "Resources"); C = os.path.join(RES, "Content")
L = lambda n: json.load(open(os.path.join(C, n + ".json"), encoding="utf-8-sig"))
def files(folder):
    d = os.path.join(RES, folder)
    return {os.path.splitext(f)[0] for f in os.listdir(d) if f.lower().endswith((".png", ".jpg"))} if os.path.isdir(d) else set()
portraits, backs, tiles = files("Portraits"), files("Backgrounds"), files("Tiles")
aicons, iicons, sicons, icons, ui = files("AbilityIcons"), files("ItemIcons"), files("StatusIcons"), files("Icons"), files("UI")
classes = {c["Id"]: c for c in L("classes")}; camp, stages = L("campaign"), {s["Id"]: s for s in L("stages")}
enc = {e["Id"]: e for e in L("encounters")}
def strings(x):
    if isinstance(x, str): yield x
    elif isinstance(x, dict):
        for v in x.values(): yield from strings(v)
    elif isinstance(x, list):
        for v in x: yield from strings(v)
order, stage_theme = [], {}
for t in camp["Themes"]:
    for sid in t["Stages"]: order.append(sid); stage_theme[sid] = t
def stage_classes(s):
    found = collections.OrderedDict()
    for w in s["Waves"]:
        for o in [w] + ([enc[w["Group"]]] if w.get("Group") in enc else []):
            for e in o.get("Enemies", []):
                found.setdefault(e["Class"], []).append("boss:" + e["RuleBreak"] if e.get("RuleBreak") else "foe")
            for k in ("Guests", "Recruitables", "SurpriseSpeaker"):
                for v in strings(o.get(k)):
                    if v in classes: found.setdefault(v, []).append(k)
    return found
first, fielded = {}, collections.defaultdict(set)
for i, sid in enumerate(order, 1):
    for cid, rs in stage_classes(stages[sid]).items():
        first.setdefault(cid, i)
        for r in rs: fielded[cid].add((r.split(":")[0], i))
refs = collections.defaultdict(set)
for f in glob.glob(os.path.join(C, "*.json")):
    if not f.endswith("classes.json"):
        for v in strings(json.load(open(f, encoding="utf-8-sig"))):
            if v in classes: refs[v].add(os.path.basename(f))
out = {"main_stages": order.index(camp["Ending"]["Stage"]) + 1}
out["portraits_missing"] = sorted(((first.get(c, 999), c, sorted(refs[c])) for c in classes if c not in portraits))
h = collections.defaultdict(list)
for p in portraits:
    fn = os.path.join(RES, "Portraits", p + ".png")
    if os.path.exists(fn): h[hashlib.md5(open(fn, "rb").read()).hexdigest()].append(p)
out["portraits_identical"] = [sorted(v) for v in h.values() if len(v) > 1]
out["finale_bosses"] = [(i, cid, r[5:], cid in portraits, sorted(k for kind, k in fielded[cid] if kind == "foe"))
    for i, sid in enumerate(order, 1) for cid, rs in stage_classes(stages[sid]).items() for r in rs if r.startswith("boss:")]
out["bounties"] = [(b["Id"], b["TargetClass"], b["Stage"], b["TargetClass"] in portraits) for b in L("bounties")]
themes = {t["Id"]: t for t in camp["Themes"]}
set_of = lambda t: (themes.get(t) or {}).get("Backgrounds") or t
count = lambda s: next(n for n in range(33) if f"{s}_{n+1:02d}" not in backs)
out["backgrounds"] = [(t, set_of(t), count(set_of(t))) for t in themes]
out["stage_theme_mismatch"] = [(sid, stages[sid]["Theme"], stage_theme[sid]["Id"]) for sid in order if stages[sid]["Theme"] != stage_theme[sid]["Id"]]
out["tiles_borrowed"] = [t for t in themes if t not in tiles]
out["ability_icons_missing"] = [a["Id"] for a in L("abilities") if a["Id"] not in aicons and not (a.get("Icon") in icons)]
out["item_icons_missing"] = [i["Id"] for i in L("items") if i["Id"] not in iicons]
out["status_icons_missing"] = [b["Id"] for b in L("buffs") if b["Id"] not in sicons] + ([] if "stunned" in sicons else ["stunned"])
out["pickup_icons_missing"] = [p["Id"] for p in L("pickups") if (p.get("Icon") or p["Id"]) not in ui]
out["caravan_icons_missing"] = [c["Id"] for c in L("caravan") if c.get("Icon") and c["Icon"] not in iicons]
print(json.dumps(out, indent=1))
```

</details>

The app icon is checked by hand: `ProjectSettings/ProjectSettings.asset` has `m_BuildTargetIcons: []` and every
platform's `m_Textures: []`.
