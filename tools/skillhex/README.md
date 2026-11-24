# tools/skillhex

Tools for the skill hexagon board. `Assets/GridGame/Resources/Content/skillhex.json` is the single source: the
game loads it, the page (`docs/design/skill-hexagon/index.html`) draws it through the generated `skillhex.js`,
and these scripts check and regenerate it. Plan: `docs/design/skill-hexagon/PLAN.md`.

| Script | What it does |
|---|---|
| `sync.py` | Writes `skillhex.js` (`window.SKILLHEX = ...`) from `skillhex.json` so the page opens from `file://`. Run after every change to the JSON. |
| `validate.py` | Checks the board against the schema, `abilities.json` and `classes.json`, warns about Signatures shared with other classes or far from their hero's spot, prints a summary and the Open slot gap list. Exits 1 on any error. |
| `regenerate.py` | Rebuilds `skillhex.json` from content with the layout generator (`generate.js`). This throws away hand edits; only Notes and New heroes carry over. `--check` only reports whether the file still matches the generator. |

## Edit, save, sync, validate

1. Open `docs/design/skill-hexagon/index.html` in a browser (straight from disk is fine) and press **Edit board**.
   Planning clicks are off while editing; **Done editing** turns them back on.
2. Edit:
   - **Abilities**: search the list (every hero ability in `data.js`, marked placed or unplaced) and pick one,
     then click an ability or Open slot. The ability's old slot turns Open; if the target held an ability, the
     two swap. Esc cancels. Clicking a placed ability on the board also offers **Move or swap**.
   - **Slots**: click an ability slot to **Make Open** (the slot offers **Restore** afterwards) or to write its
     Note. A Note on an Open slot says what the slot is kept for.
   - **Passives**: click a passive, set its six stat values, **Apply stats**.
   - **Heroes**: choose a hero (or **Add New hero**) and click nodes to add them to or remove them from its Start
     path, which shows in gold. Set its Signature from the abilities on that path, its Recipe (4 points), Status
     (Placed, Cut, New), Name, Class and Note.
   - **Undo edit** (or Ctrl+Z) steps back through every edit, including opening a file.
   - **Checks** re-runs after every edit: `validate.py`'s rules for what the page can see. Click a line to focus
     its node or hero. Abilities and classes come from `data.js` (hero content only), so an id missing there is a
     warning in the page. The page does not check how far a Signature sits from its hero's spot; `validate.py`
     does, and it is the authority.
3. Save: **Save skillhex.json** downloads the file in the same one-node-or-hero-per-line layout `regenerate.py`
   writes, with the same key order, so a diff shows only what changed. Inside the claude.ai artifact viewer
   downloads are blocked; use **Copy JSON** and paste it over the file instead. **Open skillhex.json** loads a
   file from disk to keep editing it.
4. Put the saved file at `Assets/GridGame/Resources/Content/skillhex.json`, then:

   ```
   python tools/skillhex/sync.py
   python tools/skillhex/validate.py
   ```

   `sync.py` refuses a file that breaks the lattice; `validate.py` must end with `0 error(s)` before you commit.
   Review with `git diff Assets/GridGame/Resources/Content/skillhex.json` and commit it together with
   `docs/design/skill-hexagon/skillhex.js`.

Moving a hero's Signature ability off its Start path is an error until the path or the Signature is fixed;
the Checks panel names the hero.
