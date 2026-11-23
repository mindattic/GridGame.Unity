# Attack-type hit sounds (GG-T556)

A landed physical hit sounds like whatever dealt it: a sword slashes, a hammer thuds, a wolf bites, a scorpion
stings. Ten attack types cover every weapon and every foe in the game. This page is the sound brief and the file
list. The data lives in one place, [`Assets/GridGame/Resources/Content/attacktypes.json`](../Assets/GridGame/Resources/Content/attacktypes.json),
which also lists every file name each type wants (its `Sfx` arrays). Grep for `battle-hit-` there to find every slot.

## How a hit picks its sound

For a hit with `DamageType.Physical` (the bible, §12.0):

1. a collision note ("Slam!", "Crash!", "Fell!", from knockbacks and falls) → `slam`;
2. else the resolving skill's own type (`Abilities` map, e.g. `cleave` → `slash`);
3. else the attacker's weapon: its item's `WeaponType` (`Weapons` map);
4. else the attacker's natural attack (`Enemies` map, by class id or by family, the id without its trailing
   digits, so `wolf` covers `wolf00`..`wolf03`); tamed monsters keep their own;
5. else `blunt` (an unarmed hero).

The cue is `battle.hit.<type>`. `Audio` plays a random `battle-hit-<type>-NN.ogg` among those that exist
(`NN` = 00..15, gaps allowed). **A type with no files yet plays the generic `battle-hit-00..06`**, so nothing goes
silent before the assets land. Unchanged: elemental hits keep their element cue (`element.fire` ...), crits keep
`battle.crit`, damage-over-time ticks keep `status.debuff`, misses keep `battle.miss`, and spells keep their casting
sounds.

`ContentLoader.Validate` fails the content if a weapon type or an enemy class has no attack type, a map points at an
unknown type, ability or class, or a file is not named `battle-hit-<type>-NN`. `tools/check_assets.py` warns (it
doesn't fail) while slots are still empty.

## Asset spec

- Folder: `Assets/GridGame/Resources/Audio/Sfx/`, Ogg Vorbis, mono is fine.
- Length: 0.15-0.6 s, the transient in the first 30 ms (the hit lands on the damage number).
- Loudness: **−16 LUFS ± 2, ≤ −1 dBTP** (POLISH.md §F; `tools/loudness.py` measures it).
- Names follow the asset rule (GG-T561/GG-T562, `word-word-00`): `battle-hit-<type>-00`, `-01`, ... The variants
  are separate sounds picked at random, so the number is the pick; `-00` always exists.
- Variants: the counts below are the recommendation. More is fine: name them on in sequence and add them to the
  type's `Sfx` list.
- The impact only: no whoosh-in longer than about 80 ms (the lunge has its own timing), no reverb tail over 300 ms.

## The types

| Type | What it sounds like | Weapons | Enemies | Skills | Files |
|---|---|---|---|---|---|
| **slash** | A blade whoosh ending in a clean cut through cloth or flesh; a short metal ring on armoured targets. | Axe, Chakram, Glaive, Greatsword, Halberd, Hatchet, Katana, Knife, Scythe, Sickle, Sword | Captain, Ceramic Knight, Ganger, Infernalist Executioner, Mecha Armor, Reaper, Soldier, Templar, Watch Reaver, Watch Sentry, Watch Vigilant | Cleave, Iaijutsu, Rend, Flurry, Masterless Cut, Perfect Cut, Decisive Cut, Vanishing Cut, Wide Cleave | `battle-hit-slash-00` .. `-03` (4) |
| **pierce** | A fast thrust: a tight air hiss and a sharp, wet punch-through; a point skidding on mail when armoured. | Dagger, Pitchfork, Rapier, Spear, Trident | Herd Gorer (horns), Hookthrower, Tidestrider | Fleche, Lance Charge, Phalanx Hook, Probing Thrust, Riposte Thrust, Grappling Hook, Hook Line | `battle-hit-pierce-00` .. `-03` (4) |
| **blunt** | A held weapon or fist landing: a solid wooden or metal thud with a bony crack under it, no blade ring. | Club, Flail, Hammer, Knuckles, Lantern, Mace, Morningstar, Quarterstaff, Sling | Bulwark Shieldbearer, Goblin Thug, Herd Boss, Herd Bouncer, Holdwright, Mannequin, Raider; unarmed heroes | Shield Bash, Skull Crack, Haymaker, Knockout Punch, Jab-Cross, Flowing Palm, Combo Strike, Skull Rattle, Opening Jab, One-Two Flurry, Knockout Combo, Bamboo Slam, Guard Break | `battle-hit-blunt-00` .. `-03` (4) |
| **slam** | A huge body landing: a deep, low boom with a ground-shake tail (stone, timber or meat), heavier and slower than blunt. | (none) | Anchor Hulk, Bloated Drowner, Crystal Brute, Cyclops, Floorbreaker, Herd Breaker, Hold Ram, Marsh Shambler, Mocking Echo, Mountain Troll, Oni, Slime, Tree Golem, Yeti; remains and eggs (Gravestone, Bone Pile, Corpse, Naga Egg) | Juggernaut Charge, War Stomp, Overrun, Bull Rush, Battering Charge, Floor Breaker; every knockback "Slam!"/"Crash!" and fall | `battle-hit-slam-00` .. `-02` (3) |
| **claw** | Raking talons: a quick triple scrape-and-tear, ripping cloth and hide. | (none) | Blue Lion, Cyber Zombie, Demon Lord, Dusk Mimic, Dusk Stalker, Flying Monkey, Hag (melee), Harpy Raider, Harpy Shrieker, Hellspawn Ravager / Emberheart / Scaleback, Ice Mauler, Infernalist Zealot, Lurker, Night Hunter, Purple Prototype, Shard Skitter, Spire Burrower, Steppin Razor, Tech Gremlin, Undead, Vulture, Werewolf | (none) | `battle-hit-claw-00` .. `-03` (4) |
| **bite** | Jaws snapping shut: a wet crunch with a short snarl or hiss in front of it. | (none) | Bat, Hellspawn Drinker, Naga, Pit Lurker, Sand Maw, Wolf | (none) | `battle-hit-bite-00` .. `-02` (3) |
| **sting** | A stinger jab: a dry chitin click, a tiny needle punch and a faint venom squirt. | (none) | Scorpion, Skelepede; owner rule (GG-T560): **every wasp, hornet and scorpion type** stings, so any such enemy added later gets `sting` in attacktypes.json | (none) | `battle-hit-sting-00` .. `-02` (3) |
| **lash** | Whip, chain, cord, tongue or tentacle: a whistling swing ending in a sharp crack or a wet slap. | Bolas, Whip | Chain Warden, Frog, Swamp Mistress, Toad | (none) | `battle-hit-lash-00` .. `-02` (3) |
| **projectile** | An arrow, bolt, dart, thrown spear or round arriving: a short incoming zip and a hard thunk into the target. | Bow, Crossbow, Dart, Javelin | Bulwark Rifleman, Bulwark Siege Gunner, Harpy Spotter, Hex Sniper, Hex Stalker, Hold Marksman | Quickshot, Called Shot, Suppressing Fire, Deadeye Shot, Piercing Volley, Execution Shot, Binding Shot, Snare Net, Covering Volley, Retreat Shot, Rapid Barrage, Pinning Shot, Turret Barrage | `battle-hit-projectile-00` .. `-03` (4) |
| **magic** | A focus (orb, wand, tome, staff) striking: a bright arcane zap-pop with a shimmering tail, no physical impact. | Orb, Rod, Scepter, Skullstaff, Staff, Tome, Totem, Wand | Bulwark Siphoner, Choir Cantor / Psalmist / Confessor, Chronomancer, Ghost, Gloom Mender, Hag (ranged), Hex Warden, Infernalist Pyromancer, Infernalist Bloodbinder, Lamp Snuffer, Lich, Phantom, Shardcaller, Stormcaller, The Hierarch, Void Sentinel | (none) | `battle-hit-magic-00` .. `-03` (4) |

**36 files in all.** Coverage: 39 of 39 weapon types (slash 11, blunt 9, magic 8, pierce 5, projectile 4, lash 2);
169 of 169 enemy classes (claw 44, slam 35, slash 33, magic 19, bite 11, blunt 7, lash 7, projectile 6, sting 4,
pierce 3, counting every numbered variant); 48 skills carry their own type, and every other physical skill sounds
like its user's weapon.

## Every file name

```
battle-hit-slash-00.ogg        battle-hit-slash-01.ogg        battle-hit-slash-02.ogg        battle-hit-slash-03.ogg
battle-hit-pierce-00.ogg       battle-hit-pierce-01.ogg       battle-hit-pierce-02.ogg       battle-hit-pierce-03.ogg
battle-hit-blunt-00.ogg        battle-hit-blunt-01.ogg        battle-hit-blunt-02.ogg        battle-hit-blunt-03.ogg
battle-hit-slam-00.ogg         battle-hit-slam-01.ogg         battle-hit-slam-02.ogg
battle-hit-claw-00.ogg         battle-hit-claw-01.ogg         battle-hit-claw-02.ogg         battle-hit-claw-03.ogg
battle-hit-bite-00.ogg         battle-hit-bite-01.ogg         battle-hit-bite-02.ogg
battle-hit-sting-00.ogg        battle-hit-sting-01.ogg        battle-hit-sting-02.ogg
battle-hit-lash-00.ogg         battle-hit-lash-01.ogg         battle-hit-lash-02.ogg
battle-hit-projectile-00.ogg   battle-hit-projectile-01.ogg   battle-hit-projectile-02.ogg   battle-hit-projectile-03.ogg
battle-hit-magic-00.ogg        battle-hit-magic-01.ogg        battle-hit-magic-02.ogg        battle-hit-magic-03.ogg
```

## Changing the classification

Edit `attacktypes.json` only; no code changes. Adding a type means adding it to `Types` with its brief and `Sfx`
list, then pointing weapons, enemies or skills at it. A new weapon type or enemy class must get an entry (or, for an
enemy, share a family key that already has one), or content validation fails.
