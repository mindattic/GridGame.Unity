**Why the road gets harder.** The campaign has two dials that turn together. The first is **distance**: every stage is one level past the one before, and each region adds something the party has not met. The second is **the Manalith**: its apparent size rises region by region while the Moon stays the same, and what lives under it changes with it. This entry gives each of the 15 regions its reason for the danger it holds, tied to its region rule, its sky step and its monsters.

*Naming flag (GG-T1307, for the owner canon approval GG-T1340):* the owner's brief says "Manalift". Canon has **the Manalith**, and this entry reads it that way (the open decision in GG-ARC, "Manalift is read as the Manalith").

## Three reasons, applied everywhere

1. **Closer means thinner, and thinner means stranger.** The nearer the Manalith hangs, the harder it draws mana home (the blanking, GG-WHY-NOW 5). Near its first spoor the foes are ordinary creatures under strain; further in they are the Hollow, then what fell with the shell, then things that thrive in the dark.
2. **Further means older damage.** The road runs from the Choir's tithe moors, through Halcyon's ruins and the Anchors, to the last place the Anchors hold. Each stage is closer to what Halcyon built, overdrew and lost, so the machinery is bigger and angrier.
3. **The board learns the sky.** From the Free Holds on, the floor itself obeys the region: barricades, cracks, flood, shard strikes, anchor heave, darkness. Danger stops being only "more foes" and becomes "read the timeline or lose a tile".

## The 15 regions, in the order of the road

Levels are the recommended level (a stage's place on the road plus one; the postgame regions fix theirs). Sky is the Manalith's apparent height as a fraction of the screen (`sky.json`); the Moon is 0.085 everywhere. A region rule marked "stage" applies only to that stage; the rest apply to the whole region.

| Region | Levels | Sky | Region rule | Main monsters | Why it is this dangerous |
|---|---|---|---|---|---|
| **The Tithe Moors** | 1-3 | 0.10 | `slimetrail` (stage II): slimes lay a slowing trail | slimes, wolves, frogs, goblin thugs, soldiers, the Captain | The Manalith is a speck, so the dangers are ordinary: hungry creatures and the Captain's men. The one rule teaches that a monster can change the floor, and that fire answers it. |
| **The Lantern Road** | 4-6 | 0.12 | none | oni, bats, wolves, steppin razor gangers, flying monkeys | The first climb out of the moors. Foes arrive faster and from the side (bats, monkeys, razors), so the party learns to read the timeline before it is asked to read the floor. The sky has barely moved, so the danger is distance, not mana. |
| **The Shardfall** | 7-9 | 0.14 | `firetrail` (stage III): Ember hellspawn leave burning tiles | scorpions, vultures, sandmaws, mecha armour, tech gremlins, infernalists | The first great pieces of the shell came down here, so things fell with them (sandmaws) and the Ember came to feed the fire. Burning ground is the first hazard that punishes standing still. |
| **The Arkgrave** | 10-12 | 0.16 | none | lurkers, marsh shamblers, frogs, toads, tree golems, a lich | Halcyon's ships, thrown inland, rot in the fen. The foes are the fen's own and the dead it has kept, and the danger is attrition in the wet, until the postgame floods it. |
| **The Anchor Wastes** | 13-15 | 0.18 | `icetrail` (stage II), `crackingfloor` (stage III) | goblin thugs, skelepedes, cyclopes, mountain trolls, yetis, ice maulers, the Watch's wraith knights, a void sentinel | The arches are taps, and the draw is the pull. The Watch still holds its posts and the cold breeds its own. Ice slips, and the cracking floor teaches that the ground is a resource that runs out. |
| **The Still** | 16-18 | 0.20 | `landmines` (stage III): Bulwark gunners bury mines | the Hollow, Bulwark heavy troopers, cyber-zombies, cultists | The Choir's tithe vault under the oculus that frames the Manalith. The foes are the Hollow and Halcyon's failures, and the Bulwark fights with ordnance, so a step can be the wrong step. |
| **Halcyon** | 19-21 | 0.23 | none | the Hollow, ghosts, phantoms, mocking echoes, Templars, cyber-zombies, tech gremlins, minotaurs | The dead city under a pink sky. Mana is thinnest here, so the monsters have the least of their own, and mocking echoes turn the party's own moves back on it. |
| **The Choir's Cathedral** | 22-24 | 0.26 | `crackingfloor` (stage II) | cultists, cyber-zombies, heavy troopers, Kiln Guard ceramic knights, Templars | The organ is the reservoir and the Hierarch drinks from it. The Choir's whole force fights here, on a floor that gives way. |
| **The Bloom** | 25-27 | 0.30 | none | gangers, heavy troopers, tree golems, Templars | Where the Manalith's substance took root. Growth, not blankness: the Choir's armour and the Street caught in ground that is alive. The sky is a third larger than at Halcyon, and the foes arrive in the numbers of a place that has been left alone. |
| **The Free Holds** | 28-29 | 0.34 | `holds-barricades`: every wave throws up four walls | hold wrights, marksmen, rams, soldiers, the Captain | The Holds are besieged. Barricades split pincer lines, so the party's best tool is taken from it and must be won back by breaking walls under fire. |
| **The Underspire** | 30-31 | 0.38 | `crackingfloor` | spire burrowers, pit lurkers, spire shovers, gangers, minotaurs, cultists, harpies | The neon city still draws on Halcyon's taps, so it keeps the Manalith coming. Its foes dig and shove, and the floor they work on gives out. |
| **The Drowned Arks** | 32-33 | 0.42 | `rising-tide`: three rows flood on the timeline | storm callers, tide striders, bloat drowners, hags, ironbark golems, lurkers | The Arkgrave again, with the water coming up. The danger is the clock: wet units are slowed and shocked, and the board shrinks every wave. |
| **Deep Shardfall** | 34-35 | 0.46 | `shardfall-strikes`: a 40-damage strike lands on the timeline | shard callers, crystal brutes, shard skitterers, the Captain, scorpions, sandmaws | The shell is breaking here. Pieces fall on friend and foe alike, so danger is something the party must read and can also use. |
| **Under the Anchors** | 36-37 | 0.50 | `anchor-pull`: a heave drags every unit within three tiles one tile in | chain wardens, hook throwers, anchor hulks, ice maulers, the Watch | The Ember march on the Anchors to open every tap. The field itself moves the pieces, so pincers close or break by the heave, not by the heroes. |
| **Morrow's Shadow** | 38-40 | 0.78 | `morrows-dark`: foes more than two tiles from a hero are hidden | void sentinels, dusk stalkers, lamp snuffers, gloom menders, dusk mimics, hellspawn, infernalists, werewolves | The Manalith fills the sky and its crystals can be counted. Every earlier danger is at its peak and nothing can be seen coming. |

## How the three lines meet

- **Rules climb with the sky.** The first region rules are one monster's trick (slime, fire, ice, mines). The postgame rules belong to the region, not the foe: siege, crack, tide, strike, heave, dark. The last has the biggest sky and the biggest change to what the party can see.
- **Monster families climb with the sky.** Natural creatures dominate until the Arkgrave, the Hollow and Halcyon's failures from the Still to the Cathedral, then the shell's fallen and the Ember at their most numerous, ending in the things that live in the dark.
- **The sky never lies about it.** A player who looks up sees the same Moon and a bigger Manalith, so the danger is one they were shown, not one that was sprung. The Anchors hold in Morrow's Shadow, and the final choice belongs to the player (GG-ARC).

## Mana Waystones

*(GG-T1663, owner 2026-11-27, epic GG-T1652; BIBLE §28.8.)* Halcyon laid its roads with Mana Waystones: machines that slow the Manalith's descent, keyed, like everything Halcyon built, to its own people's mana. They have been dormant since Halcyon fell. **Only the Commander sees them and wakes them** (GG-ARC); to everyone else there is nothing there, and nobody else's maps have them. A dormant stone shows only as a faint glint the Commander notices once the fog near it lifts.

- **Guarded by what the mana drew.** A dormant stone still leaks a little mana, so something has always settled around it: beasts drawn to it, or the Choir's and the claimants' forces holding the ground. Reaching one means clearing them first.
- **A circle of safety.** A woken stone casts a protective circle: no ambush and no danger inside it, and a camp there is safe. Where circles overlap they make a safe road, so a chain of woken stones becomes a safe road network through a region. Danger rises only outside every circle.
- **Mana Dust.** Mana Dust gathers at a woken stone over time, and the caravan collects it when it passes or camps there.
- **The Manalith eases.** Each woken stone slows the Manalith a little: over that region it hangs a touch smaller in the sky than the Sky column above, which is its size with every stone asleep. It never stops coming (GG-WHY-NOW); the stones buy time, not an ending.
- **Roadsigns.** A region's roads leave it only by roadsigns, each standing near a waystone at the far end of a road. A sign says where its road leads. A road the story has not opened yet shows a sign the Commander cannot read: a dormant stone.

## Roads and waystones, region by region

The roadsigns lead on in story order, and every road also leads back the way the caravan came.

| Region | Where its roads lead | What guards its waystones |
|---|---|---|
| **The Tithe Moors** | Out of the moors by the old pilgrim road, up to the Lantern Road. | Wolves and slimes drawn to the leak, and the Captain's men camped on the high ground. |
| **The Lantern Road** | Over the pass and down into the Shardfall. | Bats and flying monkeys roosting on the warm stones, and steppin razor gangers who toll the road. |
| **The Shardfall** | Inland into the fen, to the Arkgrave. | Sandmaws that came down with the shell, and Ember infernalists who feed the stones' heat to their fires. |
| **The Arkgrave** | Out of the wrecks along the beams, to the Anchor Wastes. | Lurkers and marsh shamblers in the pools around the stones, and a lich among the wrecks. |
| **The Anchor Wastes** | Under the arches and down to the Still. | Yetis and ice maulers that den in the stones' warmth, and the Watch's wraith knights at their posts. |
| **The Still** | Out from under the oculus to Halcyon. | The Hollow, which gathers where mana pools, and Bulwark troopers who have mined the ground. |
| **Halcyon** | Up the spire stair to the Choir's Cathedral. | Ghosts and mocking echoes that cling to the city's last mana, and Templars on watch. |
| **The Choir's Cathedral** | Down from the spire and out to the Bloom, when the postgame opens. | The Kiln Guard's ceramic knights and the Choir's cultists, guarding what feeds the organ. |
| **The Bloom** | Out of the growth to the Free Holds. | Tree golems rooted around the stones, and Choir heavy troopers caught in the living ground. |
| **The Free Holds** | Past the siege lines and down to the Underspire. | The Holds' own wrights and marksmen, who wall off any ground they mean to keep, and the Captain. |
| **The Underspire** | Down the old drains to the Drowned Arks. | Spire burrowers that dig towards the leak, and Street gangers who tap the stones for light. |
| **The Drowned Arks** | Back into the Shardfall, to Deep Shardfall. | Storm callers and tide striders in the rising water, with bloat drowners under it. |
| **Deep Shardfall** | Down to Under the Anchors. | Crystal brutes and shard callers drawn to the falling shell, and the Captain, still collecting. |
| **Under the Anchors** | Out from under the last Anchors into Morrow's Shadow. | The Ember's chain wardens and anchor hulks, who mean to open every tap, and the Watch. |
| **Morrow's Shadow** | Nowhere further. The last roadsign is the end of the road. | Things that live in the dark (dusk stalkers, lamp snuffers, void sentinels) around stones that hold the last light. |
