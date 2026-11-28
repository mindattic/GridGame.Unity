/* The slice acceptance checklist (GG-T2106): every owner ask in GG-T1941, the beat that delivers it (docs/STORY_SLICE.md) and the proof.
   Twin of docs/release/SLICE_ACCEPTANCE.md; change both together.
   status: done = landed on main and seen in a capture; built = landed on main, not yet captured on screen; missing = not on main, open ticket named.
   proof: "cap:<screen>" a capture in docs/design/ui-audit/captures/<device>/; "pre:<name>" a preset in tools/scenarios/; "run:<target>" a scenario target; "play:<stage>" a prologue battle, reached only by playing a new game; "file:<path>" a repo file.
   landed: closed tickets whose commits are on main. open: the open tickets that still owe the row. */
window.SLICE = {
  checked: "2026-11-28",
  main: "7318fff7",
  device: "iphone15pro",
  deviceName: "iPhone 15 Pro",
  captureDir: "../ui-audit/captures/",
  captureCommit: "160dc4b4 (GG-T2029, 2026-11-28 02:42)",
  prologue: ["prologue-vault", "prologue-ambush", "prologue-bridge-skirmish", "prologue-bridge-a", "prologue-bridge-b", "prologue-ward"],
  ask: "all of this has to include a prologue where it teaches me what to do, and how to travel and what to look for and how to progress, and tutorials about buying consumable, equiping them, using them in battle; learning an ability and using it to shatter armor of a guy that won't die because you can't damage a guy from the direction he is wearing armor, then learn a spell and cast that to overcome an enemy that is immune to all damage types except Lightning ... to see the entire game working with quests, and bounties, and travel, and battle, and crafting, and healing, and equipping and repairing and all of this needs to be perfectly sized with perfect labels and icons and the best code possible",
  statuses: {
    done: "Done, with proof",
    built: "Built, not yet seen on screen",
    missing: "Missing"
  },
  asks: [
    {
      id: "prologue", title: "Prologue lessons", quote: "a prologue where it teaches me what to do",
      parts: [
        { what: "Name, look and pronouns at the Founding", beat: "0:00 Cold Open; 2:45 the Founding", status: "built",
          proof: ["cap:createprofile", "cap:newprofile"], landed: ["GG-T1957", "GG-T2127"], open: [],
          note: "The captures show the one-step name field. They were taken before GG-T2127 landed, so the look (commander / commander_male) and pronoun picker has not been captured." },
        { what: "The prologue battles teach pincers, chains, the timeline, the clock, GUARD, abilities and Heal", beat: "0:45 The Trap; 4:00 The Clock; 6:00 Abilities; 13:45 The Toll Bridge", status: "built",
          proof: ["play:prologue-vault", "play:prologue-ambush", "play:prologue-bridge-skirmish"], landed: ["GG-T1958", "GG-T1959", "GG-T1960", "GG-T1961", "GG-T2075", "GG-T2138"], open: [],
          note: "campaign.json Prologue replaces Tutorial for new games, in this order: prologue-vault, prologue-ambush, prologue-bridge-skirmish, prologue-bridge-a, prologue-bridge-b, prologue-ward. The three drills stay under Play a drill." },
        { what: "Prologue conversations and barks", beat: "every beat", status: "built",
          proof: ["file:Assets/GridGame/Resources/Content/dialogues.json"], landed: ["GG-T2066", "GG-T2067"], open: ["GG-T2068", "GG-T1748"],
          note: "14 conversations and barks compiled from Data/Dialogue. The owner still has to publish the Prose texts (GG-T2068)." },
        { what: "The onboarding steps that string the beats together and resume them", beat: "Wake to Waystone", status: "missing",
          proof: [], landed: [], open: ["GG-T2077", "GG-T1746", "GG-T2083"],
          note: "Meta/Onboarding.cs still has only Crawl, Drill, HubIntro and Done. The map, hamlet and camp beats between the battles need these steps." },
        { what: "Skip the prologue", beat: "Skip all", status: "missing", proof: [], landed: [], open: ["GG-T2082"], note: "" }
      ]
    },
    {
      id: "travel", title: "Travel", quote: "how to travel",
      parts: [
        { what: "The overworld map screen and the travel rules (on the road, carried orbs, make camp)", beat: "7:45 The Road to Tithe", status: "done",
          proof: ["cap:overworld", "pre:overworld"], landed: ["GG-T1785", "GG-T1786", "GG-T1790"], open: [], note: "" },
        { what: "The road to Tithe on the moors map: route line, day dial, danger pulse, the ambush on the edge", beat: "7:45 The Road to Tithe", status: "missing",
          proof: ["play:prologue-ambush"], landed: ["GG-T2075"], open: ["GG-T1743", "GG-T1728", "GG-T1724", "GG-T1726", "GG-T1739", "GG-T1744"],
          note: "The ambush battle is built as prologue-ambush. The moors region in world.json and the travel tween with the dial and pulse are not." },
        { what: "Camp at the Ford: Set up camp, the meal, eight hours, Well Rested", beat: "17:45 Camp at the Ford", status: "missing",
          proof: ["cap:camp"], landed: [], open: ["GG-T1734", "GG-T1736"],
          note: "The capture is today's Camp screen, not the dusk Set up camp flow." }
      ]
    },
    {
      id: "look", title: "What to look for", quote: "what to look for",
      parts: [
        { what: "A plated foe: blows from the front clang for 0", beat: "13:45 The Toll Bridge", status: "built",
          proof: ["play:prologue-bridge-a"], landed: ["GG-T2069", "GG-T2073"], open: [], note: "Covered in full under Sunder." },
        { what: "Info shows what hurts a foe (the element lock)", beat: "28:45 The Sleeping Stone", status: "built",
          proof: ["play:prologue-ward"], landed: ["GG-T2071"], open: [], note: "RevealResistances shows a story foe's immunities whatever the Bestiary tier. IMMUNE reads as one word." },
        { what: "The glint only the Commander sees; fog lifts with travel", beat: "25:45 The Glint", status: "missing",
          proof: [], landed: [], open: ["GG-T1730", "GG-T2053", "GG-T1746"], note: "" },
        { what: "An always-visible objective", beat: "every beat", status: "missing", proof: [], landed: [], open: ["GG-T2078"], note: "" }
      ]
    },
    {
      id: "progress", title: "Progression", quote: "how to progress",
      parts: [
        { what: "The skill board and skill points", beat: "17:45 Camp at the Ford", status: "done",
          proof: ["cap:skill-hex", "cap:talents", "pre:skill-hex"], landed: ["GG-T2072"], open: [],
          note: "Sunder sits at node [6, 9] on the Barbarian's Start frontier. The capture is not of the Barbarian, so Sunder's node itself is not yet seen." },
        { what: "A Tome teaches a spell; the results show one Teach card per Tome", beat: "25:45 The Glint", status: "built",
          proof: ["cap:results", "play:prologue-bridge-b"], landed: ["GG-T1969", "GG-T2074"], open: [],
          note: "results.png shows the reworked results screen, not a Tome of Bolt Teach card." },
        { what: "Waking a waystone: the tally, its circle, the region card", beat: "32:15 The Stone Wakes", status: "missing",
          proof: [], landed: [], open: ["GG-T1746", "GG-T2077"], note: "" }
      ]
    },
    {
      id: "consumable", title: "Buying, equipping and using a consumable", quote: "tutorials about buying consumable, equiping them, using them in battle",
      parts: [
        { what: "Buy: the shop", beat: "10:45 The Hamlet of Tithe", status: "done",
          proof: ["cap:shop"], landed: [], open: ["GG-T2077"],
          note: "The shop screen is seen. The Tithe Merchant with one Healing Potion left needs the hamlet beat (GG-T2077's Shop step)." },
        { what: "Equip: a Healing Potion on a hero's battle bar", beat: "10:45 The Hamlet of Tithe", status: "built",
          proof: ["pre:potion-equipped"], landed: ["GG-T2076"], open: [],
          note: "Heroes > Abilities > Items is the equip surface. The preset opens a battle with the potion under the Consumables tab." },
        { what: "Use: a hero wounded in the bridge skirmish, healed with the potion", beat: "13:45 The Toll Bridge", status: "built",
          proof: ["play:prologue-bridge-skirmish"], landed: ["GG-T2075"], open: [], note: "The Collector's men wound a hero; a potion pickup and Heal." },
        { what: "The coach for buy, equip and use", beat: "10:45 to 14:57", status: "missing", proof: [], landed: [], open: ["GG-T2079"], note: "" }
      ]
    },
    {
      id: "sunder", title: "Sunder on the plated Tithe Collector", quote: "learning an ability and using it to shatter armor of a guy that won't die",
      parts: [
        { what: "The first clash: plate to the front, rocks on his flanks, a scripted withdrawal after three clangs", beat: "13:45 The Toll Bridge", status: "built",
          proof: ["play:prologue-bridge-a"], landed: ["GG-T2069", "GG-T2070", "GG-T2073"], open: [],
          note: "Ends as BattlePhase.Withdrawn: FALL BACK banner, no defeat, no costs, the stage counts as played." },
        { what: "Learn Sunder at camp", beat: "17:45 Camp at the Ford", status: "built",
          proof: ["pre:skill-hex"], landed: ["GG-T2072"], open: ["GG-T2080", "GG-T1734"],
          note: "The skill exists (adjacent Strike, 12 Physical, CD 3, ShattersPlate). The story-granted point at camp waits on the camp beat and the coach." },
        { what: "The rematch: Sunder shatters the plate (SHATTERED), a pincer from the front finishes him; Chain Mail and the Collector's Chest drop", beat: "22:15 The Rematch", status: "built",
          proof: ["play:prologue-bridge-b", "pre:new-gear", "pre:new-gear-compare"], landed: ["GG-T2069", "GG-T2073", "GG-T2134", "GG-T2061"], open: [],
          note: "The new-gear presets show the bag-upgrade badge and the compare for Chain Mail." },
        { what: "The coach for learning and using Sunder", beat: "19:45 learn Sunder; 22:15 the pointer", status: "missing", proof: [], landed: [], open: ["GG-T2080"], note: "" },
        { what: "The Red Mage joins after the rematch", beat: "22:15 The Rematch", status: "missing", proof: [], landed: ["GG-T2066"], open: ["GG-T2077", "GG-T2082"],
          note: "Her lines are in the dialogue seeds, but no stage or campaign content grants the recruit yet." }
      ]
    },
    {
      id: "bolt", title: "Bolt on the Lightning-only Ward-Engine", quote: "learn a spell and cast that to overcome an enemy that is immune to all damage types except Lightning",
      parts: [
        { what: "The Tome of Bolt in the Collector's chest teaches the Red Mage", beat: "22:15 The Rematch; 25:45 The Glint", status: "built",
          proof: ["play:prologue-bridge-b"], landed: ["GG-T2074", "GG-T2134"], open: [], note: "tome_bolt is in chest_tithe; a Tome may teach a Spell." },
        { what: "The Ward-Engine: every resistance 0 except Lightning x1.5; the Storm Flask fallback", beat: "28:45 The Sleeping Stone", status: "built",
          proof: ["play:prologue-ward"], landed: ["GG-T2071", "GG-T2074", "GG-T2138"], open: [],
          note: "ward_engine has cold-tinted mecha art. The stage's Lessons cover reading Info and casting the spell. flask_storm stays the item that always works." },
        { what: "The coach for learning and casting Bolt", beat: "26:15 to 29:33", status: "missing", proof: [], landed: [], open: ["GG-T2081"], note: "" }
      ]
    },
    {
      id: "loop", title: "Quests, bounties, battle, crafting, healing, equipping, repairing", quote: "the entire game working with quests, and bounties, and travel, and battle, and crafting, and healing, and equipping and repairing",
      parts: [
        { what: "Quests: the quest screen and the q_moor quests", beat: "Slice arc 2 and 3", status: "done",
          proof: ["cap:quests"], landed: [], open: ["GG-T2055", "GG-T2057"],
          note: "q_moor_slimes, q_moor_harpies and q_moor_werewolf are in quests.json. Givers and targets on the map, and the two prologue quests, are still open." },
        { what: "Bounties: the bounty board and Grimjaw the Mauler", beat: "Slice arc 3", status: "done",
          proof: ["cap:bounties"], landed: [], open: ["GG-T2056"], note: "bounty_grimjaw is in bounties.json. A roaming bounty on the moors map is still open." },
        { what: "Battle", beat: "every battle", status: "done",
          proof: ["cap:battle", "cap:battle-abilities", "pre:greenvalley-start"], landed: ["GG-T2118"], open: ["GG-T2014"], note: "GG-T2014 (the battle reads at a glance) is polish on a battle that already works." },
        { what: "Crafting: Crucible, Alchemist, Blacksmith, Enchantress", beat: "Slice arc 2", status: "done",
          proof: ["cap:crucible", "cap:alchemist", "cap:blacksmith", "cap:enchantress"], landed: ["GG-T2021"], open: ["GG-T2059"],
          note: "The stations work. Opening them at the hamlet beats, and a moors brew and forge recipe in the first hour, are still open." },
        { what: "Healing: camp rest, the Medic Wagon after a win on the road", beat: "17:45 Camp; slice arc 2 to 4", status: "done",
          proof: ["cap:camp", "cap:results"], landed: ["GG-T1785", "GG-T1790", "GG-T1969"], open: ["GG-T2058"],
          note: "Wounds carrying between road battles, and the prologue granting the Medic Wagon, are still open." },
        { what: "Equipping", beat: "22:15 The Rematch (Chain Mail)", status: "done",
          proof: ["cap:equip", "pre:new-gear", "pre:new-gear-compare"], landed: ["GG-T2061", "GG-T2134"], open: [], note: "" },
        { what: "Repairing: the Blacksmith repair list", beat: "after the Collector fights", status: "done",
          proof: ["cap:blacksmith"], landed: ["GG-T1865", "GG-T1883"], open: ["GG-T2060"], note: "The repair lesson at the hamlet or camp after the Collector fights is still open." }
      ]
    },
    {
      id: "boss", title: "The boss (the Captain) and the ending", quote: "a complete experience, even if abbreviated",
      parts: [
        { what: "The Captain at The Tithe Moors III: boss intro, phases, rally at half HP, TwinTurn, defeat scene", beat: "Slice arc 4", status: "built",
          proof: ["pre:greenvalley-boss", "cap:battle-boss"], landed: ["GG-T1952", "GG-T2062"], open: ["GG-T2064"],
          note: "battle-boss.png shows the boss presentation (GG-T1952), but it was captured before the Captain landed (0e2f3c46), so the Captain himself is not yet seen. Balance by calculation is GG-T2064." },
        { what: "The slice ending crawl, credits, back to the map with the road dormant", beat: "Slice arc 5", status: "missing",
          proof: ["cap:credits"], landed: [], open: ["GG-T2063"], note: "The credits screen exists (title > Credits). The slice ending that leads to it does not." }
      ]
    },
    {
      id: "sizing", title: "Sizing", quote: "perfectly sized ... the latest models of Apple and Samsung phones",
      parts: [
        { what: "The device matrix and tools know every target phone", beat: "all", status: "done",
          proof: ["file:tools/devices.json", "file:docs/devices/DEVICES.md"], landed: ["GG-T1993", "GG-T2116", "GG-T2119"], open: [], note: "Reference: iPhone 15 Pro. In target: iPhone 15 Pro and newer, Galaxy S23 and newer." },
        { what: "Baseline captures on every in-target iPhone shape", beat: "all", status: "done",
          proof: ["file:docs/design/ui-audit/BASELINE.md", "file:docs/design/ui-audit/captures/iphone15pro/findings.json"], landed: ["GG-T2029"], open: [],
          note: "On the 15 Pro: 0 overflow, 0 clipped, 0 overlap, 0 under the cutout. 7 screens have tap targets under 44 pt; 26 contrast fails; 52 home-indicator hits." },
        { what: "Every screen fits every shape and holds its performance budget", beat: "all", status: "missing",
          proof: [], landed: [], open: ["GG-T2122", "GG-T2114", "GG-T2098", "GG-T2099", "GG-T2100", "GG-T2115", "GG-T2101"], note: "" }
      ]
    },
    {
      id: "labels", title: "Labels and icons", quote: "perfect labels and icons",
      parts: [
        { what: "No two abilities in a hero kit share an icon", beat: "all", status: "done",
          proof: ["file:tools/check_kit_icons.py"], landed: ["GG-T1955", "GG-T1983", "GG-T1980", "GG-T1981"], open: [], note: "" },
        { what: "The label and icon audit, its fixes, one complete icon set and the sign-off", beat: "all", status: "missing",
          proof: [], landed: [], open: ["GG-T2094", "GG-T2095", "GG-T2096", "GG-T2123", "GG-T2124", "GG-T2001", "GG-T2097"], note: "" }
      ]
    },
    {
      id: "code", title: "Code health", quote: "the best code possible",
      parts: [
        { what: "Screens one per file; battle, content and automation split; the size guard", beat: "none", status: "done",
          proof: ["file:tools/size_baseline.json", "file:tools/polish_lint.py", "file:docs/CODE_STYLE.md"], landed: ["GG-T1995", "GG-T1997", "GG-T1998", "GG-T2024", "GG-T2035", "GG-T2036"], open: [],
          note: "polish_lint flags .cs files over 1200 lines and files that hold two screens." },
        { what: "MetaServices split by domain: heroes", beat: "none", status: "missing", proof: [], landed: [], open: ["GG-T2037"], note: "" }
      ]
    }
  ],
  next: [
    ["GG-T2107", "Harness slice flow: drive the whole slice end to end"],
    ["GG-T2108", "Acceptance run on the reference device (the iPhone 15 Pro, per the GG-T1992 note)"],
    ["GG-T2109", "Acceptance run on the Galaxy S25"],
    ["GG-T2110", "OWNER: play the slice"]
  ]
};
