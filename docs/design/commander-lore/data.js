window.CommanderLore = {
  moon: 0.085,
  premise: [
    "An ancient thing hangs in the sky beside the Moon. Long ago it was stopped and left there, orbiting the world, and now it is coming closer. Everyone calls it the Manalith, and its mana is also the life in every person.",
    "The Choir keeps it asleep with the Lullaby, a rite that takes people's mana to quiet it and keeps a share, the skim, for its leader, the Hierarch.",
    "The player is the Commander, a person of Halcyon who died in the Still the night Halcyon fell and was brought back 1,200 years later when the tithe ran through the Still's tanks. The Commander leads but never fights. Halcyon keyed its machines to its own people's mana, so the Mana Orbs, old doors and archives wake only near the Commander. That is why present-day heroes follow them, and why the Choir hunts them.",
    "No one holds every answer. The Commander's memory is in pieces; each truth is read out by someone else. The tone is bittersweet, and the player chooses the ending."
  ],
  decisions: [
    { id: "D1", title: "\"Manalift\" means the Manalith", def: "The owner's brief says \"Manalift\"; canon reads it as the Manalith everywhere.", alt: "Keep \"Manalift\" as a separate thing or a new name.", src: "06-arc.md open decision 2; 07-regions.md naming flag" },
    { id: "D2", title: "The Warden stays as an ordinary Tank", def: "The warden class stays as a present-day recruitable Tank, kit unchanged, from the Lantern Road faction. Its lore is rewritten.", alt: "Cut the class, or give it a different faction.", src: "06-arc.md open decisions 1 and 5" },
    { id: "D3", title: "The Commander's look", def: "The player picks a female or a male portrait, like every hero. Story text about the Commander stays gender-neutral.", alt: "One fixed look, or a hidden face.", src: "06-arc.md open decision 3" },
    { id: "D4", title: "Default Commander name", def: "The first-run wizard prefills \"Commander\" (1-16 characters, changeable in Settings). Story text says \"the Commander\" and uses {commander} only in direct address.", alt: "Start with an empty field the player must fill, or a named default.", src: "BIBLE first-run profile (GG-T1550); docs/design/front-door" },
    { id: "D5", title: "The Commander in battle", def: "Gives advice and a few orders; never fights or takes a tile.", alt: "A Commander unit on the board.", src: "06-arc.md open decision 4" },
    { id: "D6", title: "Bond change per dialogue choice", def: "Design says -10 to +10 per choice (BIBLE 25.20), but the dialogue effects AddTrust and AddCohesion accept -20 to 20. Suggested: tighten the effects to the design's ±10.", alt: "Keep -20..20 so a rare story beat can move a bond further.", src: "BIBLE 25.20 sources; dialogue vocabulary (GG-T1293)" },
    { id: "D7", title: "\"Echo\" for summon duplicates", def: "Duplicates are called Echoes, but Echo is also a Support wanderer and the Mocking Echo foe. Suggested: rename duplicates to \"Resonance\".", alt: "Keep \"Echo\" for duplicates.", src: "BIBLE 25.10.3 summon banners" },
    { id: "D8", title: "Resolve and damage over time", def: "Spec: at Trust tier 5 a hero survives one lethal blow per battle at 1 HP. The built check does not require an attacker, so a burn or poison tick can also trigger it.", alt: "Blows only (ticks can still fell a hero), or any lethal damage (as built). Pick one.", src: "BIBLE 25.20 payoffs; BattleRunner Resolve check (GG-T1581)" }
  ],
  regions: [
    ["The Tithe Moors", "1-3", 0.10, "slimetrail (stage II)", "slimes, wolves, frogs, goblin thugs, soldiers, the Captain", "The Manalith is a speck; the dangers are hungry creatures and the Captain's men."],
    ["The Lantern Road", "4-6", 0.12, "none", "oni, bats, wolves, razor gangers, flying monkeys", "Foes arrive faster and from the side; the danger is distance, not mana."],
    ["The Shardfall", "7-9", 0.14, "firetrail (stage III)", "scorpions, vultures, sandmaws, mecha armour, tech gremlins, infernalists", "The first shell pieces fell here; burning ground punishes standing still."],
    ["The Arkgrave", "10-12", 0.16, "none", "lurkers, marsh shamblers, frogs, toads, tree golems, a lich", "Halcyon's grounded Arks rot in the fen; attrition in the wet."],
    ["The Anchor Wastes", "13-15", 0.18, "icetrail (II), crackingfloor (III)", "goblin thugs, skelepedes, cyclopes, trolls, yetis, ice maulers, wraith knights, a void sentinel", "The arches are taps; ice slips and the floor runs out."],
    ["The Still", "16-18", 0.20, "landmines (stage III)", "the Hollow, Bulwark heavy troopers, cyber-zombies, cultists", "The Choir's tithe vault; the Bulwark fights with ordnance."],
    ["Halcyon", "19-21", 0.23, "none", "the Hollow, ghosts, phantoms, mocking echoes, Templars, cyber-zombies, minotaurs", "Mana is thinnest here; mocking echoes turn the party's moves back on it."],
    ["The Choir's Cathedral", "22-24", 0.26, "crackingfloor (stage II)", "cultists, cyber-zombies, heavy troopers, Kiln Guard, Templars", "The organ is the reservoir; the Choir's whole force on a floor that gives way."],
    ["The Bloom", "25-27", 0.30, "none", "gangers, heavy troopers, tree golems, Templars", "The Manalith's substance took root; foes in the numbers of a place left alone."],
    ["The Free Holds", "28-29", 0.34, "holds-barricades", "hold wrights, marksmen, rams, soldiers, the Captain", "A siege: barricades split pincer lines until broken under fire."],
    ["The Underspire", "30-31", 0.38, "crackingfloor", "spire burrowers, pit lurkers, spire shovers, gangers, minotaurs, cultists, harpies", "Still drawing on the taps; foes dig and shove on a failing floor."],
    ["The Drowned Arks", "32-33", 0.42, "rising-tide", "storm callers, tide striders, bloat drowners, hags, ironbark golems, lurkers", "Three rows flood on the timeline; the board shrinks every wave."],
    ["Deep Shardfall", "34-35", 0.46, "shardfall-strikes", "shard callers, crystal brutes, shard skitterers, the Captain, scorpions, sandmaws", "40-damage strikes land on friend and foe alike."],
    ["Under the Anchors", "36-37", 0.50, "anchor-pull", "chain wardens, hook throwers, anchor hulks, ice maulers, the Watch", "A heave drags every unit within three tiles one tile in."],
    ["Morrow's Shadow", "38-40", 0.78, "morrows-dark", "void sentinels, dusk stalkers, lamp snuffers, gloom menders, dusk mimics, hellspawn, werewolves", "The Manalith fills the sky; foes beyond two tiles are hidden."]
  ],
  chapters: [
    ["Prologue", "Tithe Day", "new campaign", "The Tithe Moors", "Why does Halcyon's tech wake for this stranger?", "the Merchant, the Innkeeper", "Gains a crew and the orbs; has nothing else."],
    ["ch01", "Over the Pass", "Tithe Moors III (3)", "Lantern Road, Shardfall", "The oldest art has no Manalith; some of what fell is its body.", "a Lantern Road sage, the Alchemist, the Blacksmith, an Ember deserter", "Learns their mana is not like anyone else's."],
    ["ch02", "The Grounded Arks", "Shardfall III (9)", "Arkgrave", "Halcyon's Arks were thrown inland; Halcyon tried to run.", "the Enchantress, the Fen-dark", "An Ark door opens for them; the Choir starts hunting the Commander."],
    ["ch03", "Where the Beams Go", "Arkgrave III (12)", "Anchor Wastes, the Still", "The arches are taps; the Still's tanks hold more tithe than the Lullaby needs.", "the Watch, a Street engineer, the Innkeeper", "Their life was paid for with other people's."],
    ["ch04", "The Spire Stair", "the Still III (18)", "Halcyon, Cathedral", "The mana in everyone is the Manalith's; the Hierarch drinks the skim.", "a Fen-dark witch, a Choir runaway, the Hierarch", "Sees their own face among the commanders who fed the Lullaby."],
    ["Ending", "Still Here", "Cathedral III (24)", "(postgame opens)", "The Hierarch falls, but the Manalith is still coming.", "the whole Camp", "Has broken what their people built, and kept the crew."],
    ["ch05", "What Came Down", "the ending (24)", "The Bloom", "Where its substance took root, things grow.", "the Herald, the Wild", "Finds a place where their being here costs no one."],
    ["ch06", "Claimants to the Spire", "Bloom III (27)", "Free Holds, Underspire, Drowned Arks", "Everyone wants the empty spire.", "a Free Holds knight, a Street runner, the Merchant", "Learns they are now the prize."],
    ["ch07", "The March on the Anchors", "Drowned Arks II (33)", "Deep Shardfall, Under the Anchors", "The Ember march to open every tap.", "the Ember deserter, the Watch's last captain", "Gains the Anchors; every ending runs through them."],
    ["ch08", "Morrow Holds", "Under the Anchors II (37)", "Morrow's Shadow", "The Anchors hold, and the choice is the player's.", "the Camp keepers", "Has every piece they will get, and chooses."],
    ["Final", "(one per ending)", "Morrow's Shadow III (40)", "the chosen ending", "Keep it asleep, cut the lines, or let it come.", "the Commander", "The last of Halcyon's people to decide for the world."]
  ]
};
