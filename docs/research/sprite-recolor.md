# Organic sprite recolors: Red, Albino and rarer breeds (GG-T1773)

Owner ask: colorize enemy art so a "Red" or "Albino" or rarer breed looks like a real reskin, not a hue/saturation swap. Eyes, claws, teeth and mouths must stay as they are.

## 1. How GridGame draws creatures today

- **UI Toolkit, not sprites.** There is no SpriteRenderer for units. Tokens are `VisualElement`s with `style.backgroundImage` (see `App/Battle/BoardView.cs`, `App/Services/Art.cs`). The only colour control now is `unityBackgroundImageTintColor`, a flat multiply. Multiply alone can only darken or tint, which is why a tint looks cheap.
- **Built-in render pipeline.** `Packages/manifest.json` has no URP/HDRP. Custom shaders already exist in `Resources/Shaders` (Manalith bloom and satellite), and the Manalith already renders to a RenderTexture that UI Toolkit shows. That is a working precedent for "GPU-draw into a texture, hand it to UI Toolkit".
- **Art is painted / AI-generated, not pixel art.** Portraits and creatures are 1024x1024 RGBA PNGs in `Assets/GridGame/Resources/Portraits/` (about 520 files), named `word-word-NN.png` (`slime-00..03`, `bat-00..02`). Bilinear filter, no mipmaps, sRGB, max size 2048. Painted art has soft gradients and anti-aliased edges, so hard palette indexing (classic pixel-art swaps) does not fit.
- **Variants already exist as `-NN` files** (`AssetNames`, variant 00 = default). Breeds are a different axis: same drawing, different colouring. Do not burn `-NN` numbers on them.
- Art authoring scripts live in the sibling GridGame.Studio repo (GG-T579); `tools/` here only checks assets.

## 2. Techniques compared

| Technique | How it works | Good | Bad for us |
|---|---|---|---|
| Hue/sat shift (HSV) | Rotate hue, scale saturation | Trivial | Brightness shifts (yellow looks bright, blue dark); eyes and claws change too; flat result. This is the "cheap swap" the owner dislikes. |
| Luminance-preserving hue shift (OKLab/OKLCH) | Convert to OKLCH, rotate H, scale C, keep L | Lightness stays true, so shading survives. Much better than HSV. | Still global: eyes/claws move unless masked. Out-of-gamut colours need clipping. |
| Palette-index / LUT swap | Source art uses N flat indexed colours, swap the table | Exact, cheap | Needs indexed art. Painted art has thousands of colours. Not usable here. |
| Gradient map on luminance | Compute luma, look up colour in a 1D ramp | Simple, keeps all shading, a ramp can hue-shift shadows (cool dark, warm light) | Throws away original hue detail (belly vs back vs fur patterns) unless combined with masks. |
| Per-region mask + per-region ramp | Companion texture marks regions; each region gets its own ramp or tint | Best control. Eyes/claws/teeth/mouth = "keep" region, untouched. Belly and back can differ. | Needs a mask per creature (section 4 auto-generates them). |
| Mask + tint multiply | Mask picks region, multiply colour | Very cheap | Multiply darkens, cannot make albino (lighter). |

**Verdict:** per-region mask + per-region ramp, driven by the pixel's own lightness in OKLab. Gradient maps give the organic look, masks protect eyes and claws.

## 3. What makes it read as organic

1. **Keep the original shading.** Use the pixel's own lightness as the ramp coordinate, so highlights, shadows and fur detail survive.
2. **Shadow hue-shift.** The ramp is not one colour made darker. Shadows slide toward a cooler, more saturated hue, highlights toward a warmer, lighter one. Three or four stops per ramp is enough.
3. **Two body regions.** "Primary" (back, fur, scales) and "secondary" (belly, underside, fins, frills). A Red breed can have a deep crimson back and a pale rose belly. One flat colour reads as a repaint.
4. **Keep region.** Eyes, claws, teeth, tongue, horns, metal, gems, glow effects pass through unchanged (or get their own accent per breed, such as red eyes on Albino).
5. **Outlines follow the body.** Dark outlines become a darkened version of the new ramp, not a grey drift. Alpha is never touched.
6. **A little pattern.** Optional low-strength stripes or spots make rare breeds feel authored.

## 4. Auto-generating masks for about 500 sprites

Do not paint every mask by hand. A Python tool in GridGame.Studio runs once per new sprite and writes `<name>-mask.png` next to the art:

1. **Cluster.** k-means (k = 4 to 6) on the opaque pixels in OKLab. Large smooth clusters are body candidates.
2. **Find "keep" pixels by rule:** small, high-contrast blobs (eyes, teeth), small near-white or near-black blobs inside the body (pupils), small very saturated blobs (gems, glow), and grey/metallic regions that do not match the body hue. Connected components under about 1.5% of the silhouette in the extreme lightness ranges go to keep.
3. **Claws and horns** are extremities with a different hue from the body: "touches the silhouette edge, lighter than body, different cluster" is a good first guess.
4. **Split primary/secondary:** the largest cluster is primary; the next largest becomes secondary when its hue differs enough, otherwise secondary is empty.
5. **Edges.** Dilate keep by 1 px and feather mask edges 1 to 2 px so there is no halo where a recoloured body meets an untouched eye.
6. **Manual touch-up.** The tool also writes a preview sheet (Normal / Red / Albino). A person or agent only fixes masks that look wrong, in any paint tool, painting pure channel colours. Expect most masks to pass as generated and a minority to need a few minutes.
7. **Format:** same size as the art or half size, PNG, **linear (sRGB off)**, no compression, no mipmaps, same filter as the art.

A segmentation model could replace steps 1 to 4 later, but the rule-based path is deterministic and needs no model.

## 5. Albino as a recipe

Albino is a ramp plus a keep-region change, not a plain desaturate:

- **Body (R):** chroma scaled to roughly 8 to 12% of original, hue pushed to warm white (about 70 to 90 degrees in OKLCH), lightness lifted: `L' = lerp(L, 1, 0.55)`, clamped near 0.97 so highlights do not clip to pure white. Shadows keep a faint cool-pink tint, not grey.
- **Secondary (G):** same, a touch pinker (skin showing through), chroma about 18%.
- **Keep region (B):** eyes forced to pink-red (about OKLCH L 0.62, C 0.20, hue 20 degrees), multiplied by the original eye luminance so the highlight dot survives. Claws and teeth stay original or go ivory.
- Outline darkening stays, with a warm-grey ramp, not black.

## 6. How other games do variants

- **Pokemon shinies:** a second hand-picked palette per species on the same sprite, rare by a flat chance.
- **Monster Hunter, Diablo, Path of Exile:** rarity changes tint plus aura and size on the same model. Cheap and readable.
- **Terraria, Stardew, Slay the Spire:** palette swaps and tinted layers over hand-masked regions.
- **Procedural creature games:** colour is per-creature data applied by a shader over masks.
- Common lessons: use a **mask**, keep **eyes** constant, and give each rare tier a **second cue** (aura, name, size), not only colour.

## 7. Cost

- **Draw calls:** none extra if breeds are baked (see recommendation). A live shader would break UI Toolkit batches per distinct material.
- **Memory:** a 1024x1024 RGBA baked variant is 4 MB uncompressed; a 512 mask is 1 MB. Bake only breeds that actually spawn and drop them when the screen exits.
- **Instancing:** on a SpriteRenderer/MeshRenderer path, per-creature values go through `MaterialPropertyBlock` so no material copies. UI Toolkit has no such path, which is another reason to bake.
- **Mobile GPU:** one mask sample, one art sample, one ramp lookup and a small OKLab conversion. Trivial for a one-off bake, fine live too.

## 8. Recommendation

**Bake-on-load, mask-driven, ramp-based recolor in OKLab.**

1. Keep the base art untouched. Add `<stem>-mask.png` (R = body, G = secondary, B = keep-as-is) beside it.
2. Add one shader `GridGame/BreedRecolor` in `Resources/Shaders`. `Art` runs it once per (sprite, breed) with `Graphics.Blit` into a RenderTexture, reads it into a cached `Texture2D`, and returns that. UI Toolkit shows it exactly like today's art: no UI change, no per-frame cost. The Manalith RT path already proves the pattern here.
3. Breeds are **data** (`Content/*.json`), picked by a seeded weighted roll when an enemy spawns. Core stays pure C#: it only stores the breed id string; colour lives in the App layer.
4. Core and rules never see colour. A breed may later carry a stat multiplier, but that is a separate bible decision.

### Shader sketch

| Property | Meaning |
|---|---|
| `_MainTex` | The art (RGBA, sRGB) |
| `_MaskTex` | Mask (RGBA, linear). R = body, G = secondary, B = keep-as-is |
| `_RampBody` | 1D ramp texture, 64x1, dark to light, for the body region |
| `_RampSecondary` | 1D ramp for the secondary region |
| `_KeepTint` | Colour for the keep region (white = unchanged) |
| `_KeepMix` | 0 = keep original, 1 = replace with `_KeepTint` shaded by original luminance (albino eyes) |
| `_LightLift`, `_ChromaScale` | Scalars applied before the ramp lookup |
| `_Pattern`, `_PatternStrength` | Optional stripes/spots texture, multiplied inside the body region |

Fragment logic:

1. `c = tex2D(_MainTex, uv)`, `m = tex2D(_MaskTex, uv)`. Convert `c.rgb` to OKLab; `L` is lightness.
2. `L2 = saturate(lerp(L, 1, _LightLift))`.
3. `body = tex2D(_RampBody, float2(L2, 0.5)).rgb`; same for secondary.
4. `rgb = c.rgb; rgb = lerp(rgb, body, m.r); rgb = lerp(rgb, sec, m.g);`
5. Keep region: `rgb = lerp(rgb, c.rgb * _KeepTint.rgb, m.b * _KeepMix)`. Authoring rule: B overrides R and G where they overlap, so the shader zeroes R and G where B is set.
6. Output `float4(rgb, c.a)`. Alpha is untouched.

### Breed table (data, weights are relative)

Put this in `Content/breeds.json`. Shape: `id`, `weight`, `bodyRamp`, `secondaryRamp`, `lightLift`, `chromaScale`, `keepTint`, `keepMix`, `aura`.

| Id | Weight | Body ramp (dark to light) | Secondary | Keep region | Notes |
|---|---|---|---|---|---|
| `normal` | 800 | original art, no bake | | | the default; no work |
| `red` | 120 | deep maroon, crimson, coral, warm cream highlight | dusty rose | original | shadows lean toward purple |
| `albino` | 50 | cool pink-grey, warm ivory, near-white | blush pink | pink-red eyes (`keepMix` 1) | recipe in section 5 |
| `shadow` | 20 | near-black indigo, slate violet, grey-lilac | deep plum | eyes shifted to cyan glow | negative `lightLift` |
| `gilded` | 8 | brown-bronze, rich gold, pale yellow | amber | original | small shimmer; best on armoured or scaled bodies |
| `spectral` | 2 | translucent teal-blue, aqua, white | pale cyan | eyes brightened | alpha about 0.8, soft glow aura |

Weights are relative, so adding a breed never forces a re-balance. Only enemies with a mask are eligible; everything else rolls `normal`.

## 9. Step-by-step authoring (artist or agent)

**Once (tooling, separate ticket):**
1. Write the mask generator in GridGame.Studio (section 4).
2. Add the `GridGame/BreedRecolor` shader and the `Art` bake-and-cache path.
3. Add `breeds.json` and a `ContentLoader.Validate` rule (every ramp exists, weights positive).
4. Add a `tools/` check that lists enemies without masks.

**Per new creature:**
1. Place `name-NN.png` (RGBA, transparent background) in `Resources/Portraits`.
2. Run the mask generator. It writes `name-NN-mask.png` and a preview sheet (Normal / Red / Albino).
3. Look at the preview. Fix any bad region in a paint tool: R body, G secondary, B keep. B wins overlaps. Eyes, claws, teeth, tongue, horns, gems go in B. Save as PNG, linear.
4. Import settings: mask has sRGB **off**, no mipmaps, same filter as the art.
5. Run the asset check; commit art and mask together.

**Per new breed:**
1. Add a row in `breeds.json` with two to four colour stops per region (the loader builds the 64x1 ramp texture).
2. Check the preview sheet on five different creatures: a furred one, a scaled one, an armoured one, a slime, a flyer. If the shadows go muddy, change the middle stop, not the weight.
3. Set the weight last.

## 10. Open questions for the owner

- Should a breed change stats (a Gilded is tougher) or only looks? Looks only is the safe default.
- Does a rare breed get a name prefix and a drop bonus ("Albino Wolf")? Probably yes, as the second cue.
- Heroes: breeds on enemies first; hero colour variants already use the `-NN` costume system.
