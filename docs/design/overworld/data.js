window.Overworld = {
  premise: [
    "The caravan travels a simple overworld map and the Commander decides where it goes. Campaign stages, recruiting, bounties, bosses, treasure and grinding all happen at places on that map. The map is campaign advancement; it replaces the Adventure list and reverses the GG-T115 “No Overworld” ruling.",
    "The Commander is the unseen player: the last sleeper of Halcyon, who woke too late.",
    "Why the caravan follows: the Commander reads the old roads. Halcyon's Mana Waystones light only near the Commander, revealing routes and hidden places no one else can see. Nobody else's maps have them.",
    "Mana Waystones are Halcyon machines that slow the Manalith's descent, long dormant. Waking them is the goal the caravan travels toward."
  ],
  quote: "the overworld is the only thing that makes this thing make sense.",

  mapLook: [
    ["Art", "The owner's painted relief map, one per region (15 regions, one per ThemeDef.Id), shown as-is at native resolution, never upscaled."],
    ["Style", "Shaded relief as in Solasta's travel screen, not contour parchment: green lowlands, ochre and brown uplands, dark gorges, rivers as thin lines in their valleys, settlements as small clusters of roofs."],
    ["Lettering", "Region names in large spaced serif caps across the land; location names in italic serif plates with a pin; road and area names (“Copperhead Road”) as lore on the land, never borders."],
    ["Scale", "No zoom. One readable scale, one large area scrolled with drag and fling (PanZoomView, MinScale = MaxScale = 1), re-centre on the party."],
    ["Edges", "The land fades into fog, haze, mountains or sea at the scroll limits. No border, no “you are leaving” line, no world overview map."],
    ["Between regions", "Roadsigns at the far ends of roads are the only way across. One reads “To the Lantern Road”; taking it moves the caravan to that road's arrival sign on the next map. A locked road shows a sign the Commander can't read yet."],
    ["Graph", "Hand-authored nodes (Stage, Settlement, Waystone, Camp, Bridge, Pass) and spline edges along roads, valleys, passes and bridges. Dijkstra over unlocked edges. The randomness is random encounters: region foes with the odd surprise, possible anywhere outside a safe circle, on a road or off it, more likely as the danger pulse strengthens."],
    ["Crossings", "Rivers only at bridges (no fording); swamp channels by causeways and boardwalks; ranges at passes."]
  ],

  references: [
    { src: "img/solasta-travel-journal.jpg", ref: true, title: "Travel screen and Travel Journal",
      note: "Taken from it: shaded relief, lettered region names, glowing route, party portrait pin, day dial, Interrupt and Navigate, the journal log." },
    { src: "img/solasta-location-card.jpg", ref: true, title: "Location card",
      note: "Taken from it: the side card with art, name, kind, lore lines and a challenge rating; Enter under the name plate when the party is there." }
  ],
  starters: [
    { src: "img/starter-mountains-relief.jpg", title: "Starter map 1: mountains (relief)",
      note: "Brown rocky ranges, a snow peak, green valleys and forest, teal rivers. Routes keep to valley floors; ranges are crossed at passes; rivers at bridges." },
    { src: "img/starter-swamp.jpg", title: "Starter map 2: the swamp (Arkgrave / Drowned Arks)",
      note: "Slate channels and pools through moss, reed beds, tree clumps, low hills. Open water impassable, reeds slow, routes follow dry ground; causeways cross channels." },
    { src: "img/starter-moors.jpg", title: "Starter map 3: the moors (Tithe Moors, region 1)",
      note: "Rolling grassland, one broad river with tributaries, low ridges, copses. Routes shaped by ridges and by bridges at the river's narrow reaches. The working slice is built here." }
  ],

  travel: [
    ["Party marker", "The party's portraits in a small cluster on a pin. No caravan sprite."],
    ["Route line", "A soft glowing line along the valleys. Line and markers are drawn in screen space at a constant size."],
    ["Hunting party", "When a group is out hunting: a second portrait cluster, joined to the caravan by a faint line back to camp."],
    ["Day dial", "At the top. Travel runs until dusk, then the prompt to set up camp. Slower at night."],
    ["Danger gauge", "Beside the dial; pulses on the marker, faster as it fills. Rises by the edge's Danger rate, faster at night and in bad weather. Before it is full a random encounter can fire anywhere outside a safe circle, likelier as it fills; full means the encounter is inevitable, an unavoidable ambush (“Defend the caravan”). Camp or arrival resets it."],
    ["Food", "A small count beside the danger gauge."],
    ["Navigate", "Pick a destination; the route preview appears."],
    ["Interrupt", "Stop now and camp on the spot."],
    ["Travel Journal", "A short log of what heroes do on the road and at camp, by name: forages food, finds ingredients, trains, cooks, tells war stories, meets a stranger. A line can carry an item, gold or a bond tick. Seeded from a per-faction/role table."],
    ["Location card", "Art (the stage backdrop), name, kind (FORTRESS, SETTLEMENT, RUIN, CAMP…), two or three lore lines, Challenge (Easy / Medium / Hard from recommended level vs party level). Enter starts it through StageLauncher.Launch when the party is there."]
  ],

  camp: [
    ["Stop", "Nothing opens while the caravan moves. It travels until it must stop (dusk, Interrupt, arrival), then Set up camp."],
    ["Services", "Camp is today's Camp hub: Forge, Alchemist, Enchantress and the rest open. Camp rest replaces the Inn. The Crucible moves under the Caravan list and draws foes from the current region."],
    ["The circle", "The caravan's zone of influence, drawn in the pixel icon-generator dither style. Forage spots (herbs, ingredients) and hunt spots: safe, familiar regional foes whose level rises slowly with how often the region has been hunted."],
    ["Hunt", "Game animals (deer, buffalo, boar; marsh fowl in the swamp, mountain goats in the ranges). A short battle; they flee, except boar and buffalo, which charge. Yields Food plus hides or horn."],
    ["The Wilds", "Beyond the circle. Seeded icons for a recruitable hero, a stronger boss, treasure or another adventure, only outside every circle; they despawn when camp breaks. Predators (wolves and regional ones) may join a hunt: rarely inside the circle, likely in the Wilds, more at night."],
    ["Eat", "A meal spends Food."],
    ["Sleep", "A full 8 hours; the clock advances to morning. The journal shows who cooked, who kept watch and the night's events."],
    ["Well Rested", "Meal plus full sleep: a buff in every battle until the next nightfall (default +10% HP and one extra starting mana orb; numbers in config). Skipping either gives no buff. No starvation penalty."]
  ],

  periods: [
    { name: "Dawn", color: "#e9a98f", grade: "warm, soft contrast" },
    { name: "Day", color: "#c9d69a", grade: "neutral" },
    { name: "Dusk", color: "#d98a4e", grade: "amber, deeper shadow" },
    { name: "Night", color: "#5a3f9e", grade: "classic jRPG purple, not black" }
  ],
  dayNight: [
    "Each period has an HSV grade (hue, saturation, brightness, contrast) that drives the map, the camp backdrop (LivingBackdrop) and the backdrop of a battle started at that hour.",
    "At night travel is slower, danger rises faster and foes are stronger (a level bonus, more elites), so camping for the night is often the smart move.",
    "Clock and weather are ported as pure C# into Core/World/ from Unity.Rea.Overworld (WorldClock, Clock, time.json, Weather, Clouds and their tests), with the RGB tint replaced by the HSV grade."
  ],
  weather: [
    ["Kinds", "Per region and season: clear, overcast, rain, fog banks, storm, snow (mountains), drifting mist (swamp)."],
    ["Effect", "Slows travel a little and nudges danger. Carries into battle as a backdrop overlay (rain, snow, mist); storm and fog add a light rule such as reduced range."],
    ["Clouds", "Soft cloud shadows drift across the map and never hide the route line or markers."],
    ["Fog of war", "Unexplored land is under fog. Travel reveals a band around the route, camp reveals its circle, a woken waystone its surroundings. Saved as a low-res reveal mask per region. Wild events and locations under fog stay hidden."]
  ],

  waystones: [
    ["Seeing one", "A dormant waystone shows only as a faint glint once the fog near it lifts. Reaching it usually means a guardian battle: beasts drawn to the mana, or Choir/claimant forces."],
    ["Safe circle", "Waking it gives a permanent circle: no ambushes and no danger build-up inside, safe camping."],
    ["Corridors", "Overlapping circles form safe corridors. A chain of woken stones makes a safe road network; the danger gauge rises only outside every circle."],
    ["Fog", "Lifted inside the circle; roads it touches are revealed."],
    ["Mana Dust", "Gathers at the stone over time, collected when the caravan passes or camps there. The existing DustEconomy Mana Dust, refined into Shards at the Aetherist (DustPerShard = 10). No new currency; a per-stone cap keeps Shards in balance with the store."],
    ["Manalith", "Each woken stone lowers that region's sky Manalith a touch (sky.json Size) and counts toward the story's waystone tally."],
    ["Placement", "The natural stopping points between campaign locations; roadsigns between regions sit near one."]
  ],

  library: [
    "No more “Stage N”: captions and the Adventure list give way to location names.",
    "Battles come from a library of hand-crafted battles, each the existing StageDef shape (1 to 5 waves of {Class, Level, X, Y, DelayTicks}, preassembled from the region's common monsters; blocks, background), saved in Resources/Content/battles/<region>/*.json.",
    "Tags: region/biome, kind (ambush, hunt, game animals, Wild boss, waystone guardian, settlement defence, side story) and base tier.",
    "Authoring: EncounterGenerator drafts one from a tag set, the designer hand-tunes it, validation checks every battle (tiles in bounds, classes exist, wave counts). About 100 per starter region to begin.",
    "Story locations keep their authored battles, launched through StageLauncher.Launch (crawls, story beats and chapters unchanged)."
  ],
  factors: [
    ["Region", "Base tier band of the region's foes."],
    ["Distance", "From roads, and being outside every waystone or camp circle."],
    ["Danger gauge", "Its level at the moment the battle fired."],
    ["Night", "Level bonus, more elites."],
    ["Weather", "Nudge up; storm and fog add a light rule."],
    ["Party", "Level and size."],
    ["Hunted", "How often this area has been hunted (slow rise)."],
    ["Story progress", "Where the campaign stands."],
    ["Woken waystones", "Nearby woken stones ease it."]
  ],
  difficultyOut: "One Difficulty function combines the factors into a tier band (which battles the library may pick) and a level offset; foes scale slowly as in the Crucible (Meta/Crucible.cs).",

  onboarding: [
    ["Who you are", "The Commander, the last sleeper of Halcyon who woke too late. The opening crawl, kept short."],
    ["Why they follow you", "The Founding scene: the heroes see Halcyon tech wake near you."],
    ["How to fight", "The five drills, kept."],
    ["How to travel", "The first leg on the map: Navigate, the route line, the day dial, the danger pulse, a scripted ambush, Interrupt and set up camp."],
    ["Camp", "Eat, sleep, wake Well Rested, the circle, a first hunt."],
    ["Why you seek Mana Waystones", "The first dormant stone only the Commander can see, its guardian fight, waking it: the circle appears, the Manalith eases."]
  ],
  onboardingNote: "Meta/Onboarding.cs gains Travel, Camp and Waystone after Crawl, Drill(n) and HubIntro, resumed through StoryBeats.Resume as today.",

  sideStories: [
    "The wanderer “game of wits” quiz is dropped. A hero is met as a Wild hero icon in their own faction's regions (factions.json, Wanderers.Local), found doing their own thing, often a battle the party joins. The in-battle wanderer and Rapport stay, as “they've noticed you”.",
    "Each hero has a short side story: a QuestDef chain ending in RewardHero (the ronin_recruit and forge_iron → lancer pattern). Meeting and resolution lines are small dialogues.",
    "Removed: WitsExchanges, Wanderers.Judge/Threshold/Outlook, the PostBattle talk block, the eight wanderer_* talks, the quiz rules in WandererContent.ValidateDialogue.",
    "Content in batches per faction, Holds first (for the moors)."
  ],
  objectives: [
    ["PayGold", "Pay a sum."],
    ["Defend", "Hold a settlement node's battle."],
    ["SlayBoss", "Defeat a Wild boss."],
    ["Treasure", "Dig at a map node."],
    ["Escort", "Travel to a node with the hero as a guest."]
  ],

  open: [
    ["Stamina", "The stamina pool's player text (Meta/RestedPool.cs, today “Rested”) renamed so “Well Rested” means only the camp buff. Proposed: “Stamina”."],
    ["Mana Shards", "The store's premium currency is already Shards and refined Dust becomes the same Shards; “Mana Shards” is read as those."],
    ["Moors tributaries", "Fordable as stepping stones, or small footbridges only? Default: footbridges (no fording)."]
  ],

  tree: [
    ["GG-T1653", "Overworld design and canon", [
      ["GG-T1654", "BIBLE and AMENDMENTS", [["GG-T1655", "Rewrite BIBLE 28 Overworld"], ["GG-T1656", "Amend Adventure Expeditions Inn"], ["GG-T1657", "AMENDMENTS entry overworld"]]],
      ["GG-T1658", "Overworld design page", [["GG-T1659", "Design page overworld"], ["GG-T1660", "Publish overworld artifact"]]],
      ["GG-T1661", "Overworld canon", [["GG-T1662", "Canon last sleeper"], ["GG-T1663", "Canon waystones roadsigns"]]]
    ]],
    ["GG-T1664", "World data model and map tool", [
      ["GG-T1665", "World data model", [["GG-T1666", "WorldModels and validation"], ["GG-T1667", "World loader and tests"]]],
      ["GG-T1668", "Relief map tool", [["GG-T1669", "Map palette clustering"], ["GG-T1670", "Map cost and water masks"], ["GG-T1671", "Map routes bridges passes"], ["GG-T1672", "Map spot suggestions"], ["GG-T1673", "Map scroll tiles"]]]
    ]],
    ["GG-T1674", "World rules core", [
      ["GG-T1675", "World graph", [["GG-T1676", "WorldGraph Dijkstra"]]],
      ["GG-T1677", "World clock", [["GG-T1678", "Port WorldClock"], ["GG-T1679", "Day night grades"]]],
      ["GG-T1680", "Travel", [["GG-T1681", "Travel along splines"], ["GG-T1682", "Travel stop and roadsigns"]]],
      ["GG-T1683", "Danger", [["GG-T1684", "Danger gauge"], ["GG-T1685", "Safe circles"]]],
      ["GG-T1686", "Fog of war", [["GG-T1687", "Fog reveal mask"]]],
      ["GG-T1688", "Weather and clouds", [["GG-T1689", "Port weather and clouds"], ["GG-T1690", "Weather effects"]]],
      ["GG-T1691", "Mana Waystones", [["GG-T1692", "Waystone wake rule"], ["GG-T1693", "Waystone dust and Manalith"]]],
      ["GG-T1694", "Camp rules", [["GG-T1695", "Camp circle and hunt spots"], ["GG-T1696", "Wild events"], ["GG-T1697", "Food sleep Well Rested"], ["GG-T1698", "Predator encroachment"]]],
      ["GG-T1699", "Battle difficulty", [["GG-T1700", "Difficulty function"], ["GG-T1701", "Battle picker"]]]
    ]],
    ["GG-T1702", "World save", [
      ["GG-T1703", "World save v26", [["GG-T1704", "SaveData World v26"], ["GG-T1705", "Fold expeditions into travel"], ["GG-T1706", "Rested renamed Stamina"]]]
    ]],
    ["GG-T1707", "Battle library", [
      ["GG-T1708", "Battle library format", [["GG-T1709", "Battle file format"], ["GG-T1710", "Battle authoring flow"], ["GG-T1711", "Battle kinds in launcher"]]],
      ["GG-T1712", "Moors battle content", [["GG-T1713", "Moors battles batch 1"], ["GG-T1714", "Moors battles batch 2"], ["GG-T1715", "Night and weather in battle"]]],
      ["GG-T1716", "Game animals", [["GG-T1717", "Game animal classes"], ["GG-T1718", "Game animal art and drops"]]]
    ]],
    ["GG-T1719", "World map screen", [
      ["GG-T1720", "Shared scrolling", [["GG-T1721", "PanZoomView zoom off"], ["GG-T1722", "Screen space route and markers"]]],
      ["GG-T1723", "WorldMapScreen", [["GG-T1724", "Map tiles and edge fog"], ["GG-T1725", "Map nodes and labels"], ["GG-T1726", "Party markers"], ["GG-T1727", "Navigate and route preview"], ["GG-T1728", "Travel tween dial danger"], ["GG-T1729", "Map clouds weather grade"], ["GG-T1730", "Map fog of war"], ["GG-T1731", "Location card"], ["GG-T1732", "Travel Journal"]]],
      ["GG-T1733", "Camp on the map", [["GG-T1734", "Set up camp"], ["GG-T1735", "Camp circle and Wild icons"], ["GG-T1736", "Meal sleep Well Rested UI"], ["GG-T1737", "Crucible under Caravan"]]],
      ["GG-T1738", "Map navigation", [["GG-T1739", "Nav Campaign opens map"], ["GG-T1740", "Retire Adventure list"]]]
    ]],
    ["GG-T1741", "Moors working slice", [
      ["GG-T1742", "Moors region", [["GG-T1743", "Author moors region"], ["GG-T1744", "Moors end to end"]]],
      ["GG-T1745", "Onboarding journey", [["GG-T1746", "Onboarding steps travel camp waystone"], ["GG-T1747", "Onboarding lessons"], ["GG-T1748", "Onboarding text last sleeper"]]],
      ["GG-T1749", "Slice verification", [["GG-T1750", "Harness travel flow"], ["GG-T1751", "Bots travel and camp"], ["GG-T1752", "Moors playtest build"]]]
    ]],
    ["GG-T1753", "Hero side stories", [
      ["GG-T1754", "Side story engine", [["GG-T1755", "Quest objective kinds"], ["GG-T1756", "Wild hero event"], ["GG-T1757", "Remove wits quiz"]]],
      ["GG-T1758", "Side story content", [["GG-T1759", "Holds side stories"], ["GG-T1760", "Street side stories"], ["GG-T1761", "Lantern side stories"], ["GG-T1762", "Fendark Wild Ember side stories"]]]
    ]],
    ["GG-T1763", "Widen the world", [
      ["GG-T1764", "Swamp and mountains regions", [["GG-T1765", "Swamp region"], ["GG-T1766", "Mountains region"]]],
      ["GG-T1767", "Remaining regions", [["GG-T1768", "Remaining regions plan"]]]
    ]]
  ]
};
