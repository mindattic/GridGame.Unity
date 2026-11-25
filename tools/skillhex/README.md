# tools/skillhex: the skill hexagon edit loop

The skill hexagon board lives in one file, `Assets/GridGame/Resources/Content/skillhex.json`. The game
loads it, and the editor page at `docs/design/skill-hexagon/index.html` draws it from `skillhex.js`, a
copy of the same JSON that `sync.py` writes so the page opens from `file://` without a fetch. The model
is described in `docs/design/skill-hexagon/PLAN.md`, and the empty Open slots are listed in
`docs/design/skill-hexagon/GAPS.md`.

## The loop

1. **Edit.** Open `docs/design/skill-hexagon/index.html` in Chrome or Edge. Pick a mode:
   - *Plan a build*: click abilities to plan a path for a hero. This changes nothing in the file.
   - *Edit board*: click an ability slot to place, move, swap or clear an ability (a cleared slot is
     Open) and give it a note, or click a passive to change its stats.
   - *Edit hero path*: pick a hero, then click nodes to change its Start path or make an ability its
     Signature. Recipe, Status and other hero fields are edited in the JSON.

   The Validation panel updates after every edit. *Undo edit* steps back one edit.
2. **Save.** *Save* writes the board back in the file's one-line-per-entry layout, so diffs stay small.
   - From `file://` in Chrome or Edge, the first Save asks where to write. Choose
     `Assets/GridGame/Resources/Content/skillhex.json`. Later saves write to the same file. Use *Open…*
     to load the file first if you want Save to go straight back to it.
   - In browsers without the file picker, Save downloads `skillhex.json`. Move it over the content file.
   - In the claude.ai artifact viewer, downloads and file pickers are blocked. Use *Copy JSON* and paste
     the text over the whole content file.
3. **Sync.** `python tools/skillhex/sync.py` runs quick checks (lattice, slot kinds, duplicate
   abilities, Start nodes that don't exist) and rewrites `docs/design/skill-hexagon/skillhex.js`. Run it
   after every change to `skillhex.json`, or the page keeps drawing the old board.
4. **Validate.** `python tools/skillhex/validate.py` runs the full check: the schema (when `jsonschema` is
   installed), ability ids against `abilities.json`, hero classes against `classes.json`, Recipes,
   Signatures and connected Start paths. It warns about shared Signatures and Signatures far from their
   hero, prints the Open slot list by wedge, and exits 1 on any error. Fix errors and repeat from step 1.

Commit `skillhex.json` and `skillhex.js` together.

## The tools

| Script | What it does |
|---|---|
| `sync.py` | Checks `skillhex.json` and writes `skillhex.js` for the page. Standard library only. |
| `validate.py` | The full check against the schema and game content. `--abilities`, `--classes`, `--schema` and `--max-sig-distance` override the defaults; a path to another `skillhex.json` checks that file instead. |
| `regenerate.py` | Rebuilds the whole board from `data.js` with `generate.js` in a headless browser (needs Playwright), then runs `sync.py`. **This throws away hand edits** except notes and heroes with Status New. `--check` only reports whether the generator still matches the file. |

Don't edit `skillhex.js` by hand. `sync.py` overwrites it.
