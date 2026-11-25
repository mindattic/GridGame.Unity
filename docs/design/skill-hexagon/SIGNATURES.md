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

## Slice-1 signature repair (GG-T1131)

Merge slice 1 (GG-T974) folded thirteen heroes' own signatures into shared moves and left their spot
slots Open, so `validate.py` reported 25 warnings: 13 signatures shared with another class and 12 more
than one step from the hero's spot. Each affected hero now signs with a move from its own kit placed
on the Open slot its old signature left on its spot (distance 0). Only that ability node moved; its old
slot is now Open. Where the moved move was the GG-T974 signature, the Start suffix GG-T974 added to
reach it was dropped, which restores the pre-merge Start (it already held the spot). No moved ability
sat in, or landed in, another hero's Start, and no other hero's Signature or Start changed. Warnings
went from 25 to 11, errors stay 0. Owner signature review (GG-T1054) may override any of these.

| Hero | Old signature | New signature | Nodes moved | Start |
|---|---|---|---|---|
| Lancer | Running Riot (`skirmisher_runningriot`) | Pole Vault (`lancer_polevault`) | Pole Vault (-5, 1) → (-6, 0) | unchanged |
| Basher (Cut) | Skull Rattle (`berserker_skullrattle`) | Rattle (`rattle`) | none (stays at the centre) | extended through passives to (0, 0): (-2, 6) (-2, 5) (-1, 4) (-1, 3) (0, 2) (0, 1) (0, 0) |
| Tank | Overrun (`juggernaut_overrun`) | Unstoppable Momentum (`juggernaut_unstoppable_momentum`) | Momentum (14, -4) → (15, 0) | unchanged |
| Myrmidon | Grappling Hook (`grappling_hook`) | Grappling Hook (kept) | (-1, 5) → (6, 0) | GG-T974 suffix to (-1, 5) dropped |
| Bruiser | Blood Rage (`blood_rage`) | Blood Rage (kept) | (-3, 6) → (0, 15) | GG-T974 suffix dropped |
| Fighter | One-Two Flurry (`combobrawler_onetwoflurry`) | One-Two Flurry (kept) | (1, 7) → (-2, 10) | GG-T974 suffix dropped |
| Prizefighter | Knockout Combo (`combobrawler_knockoutcombo`) | Grudge (`grudge`) | Grudge (6, 6) → (8, 2) | unchanged |
| Odachi | Guard Break (`brawler_guard_break`) | Grim Resolve (`grim_resolve`) | Grim Resolve (-6, 12) → (-2, 13) | unchanged |
| Slasher | Wide Cleave (`brawler_wide_cleave`) | Wide Cleave (kept) | (-4, 14) → (-6, 9) | GG-T974 suffix dropped |
| Swordmaster | Decisive Cut (`duelist_decisivecut`) | Guard Stance (`duelist_guardstance`) | Guard Stance (-5, 10) → (-8, 16) | unchanged |
| Ronin | Hit and Run (`skirmisher_hitandrun`) | Hit and Run (kept) | (-12, 9) → (-11, 13) | GG-T974 suffix dropped |
| Streetfighter | Dirty Fighting (`dirty_fighting`) | Dirty Fighting (kept) | (-7, 8) → (-8, 10) | GG-T974 suffix dropped |
| Warchief | Rally Point (`warlord_rally_point`) | Rally Point (kept) | (7, 4) → (3, 12) | GG-T974 suffix dropped |

Why the swaps: Lancer and Basher are the only two with a move no other class lists, so their sharing
warnings clear. Where the merged signature sat in other heroes' Starts (Overrun in Myrmidon's, Knockout
Combo in Fighter's, Martial Artist's and Pugilist's, Guard Break in Slasher's, Decisive Cut in Duelist's,
Fencer's, Ronin's and Sellsword's), moving it would have taken it out of their Starts, so the hero signs
with the kit move that is shared by the fewest classes and sits in no other Start instead.

**Still needing a new signature move (owner or design).** These eleven heroes have no move that only
their class lists after GG-T974, so each keeps one sharing warning: Bruiser (Blood Rage), Fighter
(One-Two Flurry), Myrmidon (Grappling Hook), Odachi (Grim Resolve, also Ronin), Prizefighter (Grudge),
Ronin (Hit and Run), Slasher (Wide Cleave, also Odachi), Streetfighter (Dirty Fighting, also the
goblinthug00 monster), Swordmaster (Guard Stance), Tank (Unstoppable Momentum, also Myrmidon) and
Warchief (Rally Point, also Shieldmaiden). Each spot slot already holds the stand-in, so a new own move
can take that slot.
