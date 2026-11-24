# Signature review (GG-T1039)

Every placed hero's Signature was checked against `abilities.json` and `classes.json`: all 65 are unique to
their class, and every signature node now sits on or next to its hero's spot (lattice distance 0 or 1, the
spot computed from the Recipe as `app.js` does it). Four auto-picked signatures were changed. Where the new
signature was more than one step from the spot, it swapped nodes with the old one; each changed hero's
Start path was rebuilt the way `startPath` in `app.js` builds it. No other hero's Start changed.

| Hero | Old signature | New signature | Why |
|---|---|---|---|
| Black Ninja | Smoke Vanish (`blackninja_smoke_vanish`) | Shadow Fang (`blackninja_shadowfang`) | Every other colour ninja signs with its Fang (Frost, Flame, Venom, Shock, Prism); Shadow Fang is in its loadout and already one step from its spot. |
| Drifter | Frost Nova (`drifter_frostnova`) | Float (`drifter_float`) | Frost Nova is a generic ice burst; Float (lift any unit so it can be slid into a pincer) is the move only a Drifter has and names it. Swapped nodes with Frost Nova: (4, -8) ↔ (6, -9). |
| Hex Sniper | Deadeye Shot (`marksman_deadeye_shot`) | Piercing Shot (`hexsniper_piercingshot`) | The class card is "a piercing shot nothing else in the party can match"; Deadeye was a legacy Marksman move. Swapped nodes with Deadeye: (-4, 5) ↔ (-6, 6). |
| Hex Warden | Binding Shot (`trapper_binding_shot`) | Snare Shot (`hexwarden_snareshot`) | The hero's own thorn-hex shot over a legacy Trapper bolt. Swapped nodes with Binding Shot: (-1, -10) ↔ (-2, -11). |

`tools/skillhex/validate.py` now warns when a Signature is also in another class's Abilities or
DefaultLoadout, or sits more than `--max-sig-distance` lattice steps (default 1) from its hero's spot.
