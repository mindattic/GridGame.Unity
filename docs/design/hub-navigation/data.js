// Hub navigation model (GG-T1328). Plain global so the page opens from file:// with no fetch.
window.HUB_NAV = {
  tabs: [
    { id: "home", label: "Home", target: null },
    { id: "campaign", label: "Campaign", target: "stageselect" },
    { id: "heroes", label: "Heroes", target: "hero" },
    { id: "summon", label: "Summon", target: "summon" },
    { id: "shop", label: "Shop", target: "shop" }
  ],

  pillars: [
    {
      id: "campaign", name: "Campaign",
      body: "The story. One big Continue button launches the next uncleared stage in one tap (the same launch Stage select uses: its story crawl on first entry, then the battle). A smaller Stage select button opens the region list for replays, hunts and expeditions. New Game+ sits here once the campaign is beaten.",
      items: ["Continue: Stage N", "Stage select", "New Game+ (after the ending)"]
    },
    {
      id: "crucible", name: "Crucible",
      body: "Endless waves for XP and loot (BIBLE 22.10). One tap opens the Crucible screen with its record. Locked with its milestone hint until it opens; closed on an expedition, saying why. Hidden while Features.Crucible is off.",
      items: ["Crucible (record)"]
    },
    {
      id: "camp", name: "Camp",
      body: "Everything between battles, as six tiles. Heroes, Summon and Shop open their screen directly (they are also bottom-nav tabs); Craft and Journal open a short group menu; Settings opens Settings. A tile whose stations are all closed is dimmed with the earliest join hint; a tile with something waiting (skill points, a bounty to collect, a quest to hand in, unread Codex pages) carries a badge.",
      items: ["Heroes", "Summon", "Shop", "Craft", "Journal", "Settings"]
    }
  ],

  // group: where the screen lives. nav: whether the bottom nav shows. back: what Back does there.
  // path: the stack it sits at (Hub = root). links: what the page simulator can open from it.
  screens: [
    { id: "title", cls: "TitleScreen", name: "Title", group: "Front door", nav: false, path: "Title", back: "Hardware back exits the app (unchanged)", links: ["profile", "saveslot", "settings-title", "credits"] },
    { id: "profile", cls: "ProfileSelectScreen", name: "Profile select", group: "Front door", nav: false, path: "Title > Profile", back: "Pops to Title", links: ["profilename"] },
    { id: "profilename", cls: "ProfileNameScreen", name: "Profile name", group: "Front door", nav: false, path: "Title > Profile > Name", back: "Pops one", links: [] },
    { id: "saveslot", cls: "SaveSlotScreen", name: "Save slot", group: "Front door", nav: false, path: "Title > SaveSlot", back: "Pops to Title", links: ["charselect", "hub"] },
    { id: "charselect", cls: "CharacterSelectScreen", name: "Character select", group: "Front door", nav: false, path: "Title > SaveSlot > CharacterSelect", back: "Pops to SaveSlot", links: ["prologue"] },
    { id: "prologue", cls: "StoryCrawlScreen", name: "Story crawl (Prologue)", group: "Front door", nav: false, path: "Replaces the stack", back: "Swallowed (Skip is on screen); ends in ResetTo(Hub)", links: ["hub"] },
    { id: "settings-title", cls: "SettingsScreen", name: "Settings (from Title)", group: "Front door", nav: false, path: "Title > Settings", back: "Pops to Title. No save is loaded, so no nav", links: [] },
    { id: "credits", cls: "CreditsScreen", name: "Credits", group: "Front door", nav: false, path: "Title > Credits, or after the Ending", back: "Pops to Title", links: [] },

    { id: "hub", cls: "HubScreen", name: "Hub (Home)", group: "Home", nav: true, path: "Hub", back: "Root: asks \"Return to the title?\"; Yes saves and ResetTo(Title)", links: ["continue", "stageselect", "crucible", "hero", "summon", "shop", "craft", "journal", "settings"] },

    { id: "stageselect", cls: "StageSelectScreen", name: "Stage select", group: "Campaign", nav: true, path: "Hub > StageSelect", back: "Pops to Hub", links: ["stageconfirm"] },
    { id: "stageconfirm", cls: "StageConfirmScreen", name: "Stage confirm", group: "Campaign", nav: true, path: "Hub > StageSelect > StageConfirm", back: "Pops to StageSelect", links: ["storycrawl", "battle"] },
    { id: "continue", cls: "(Campaign Continue)", name: "Continue (one tap)", group: "Campaign", nav: true, path: "Action on the hub root", back: "-", links: ["storycrawl", "battle"], action: true },
    { id: "storycrawl", cls: "StoryCrawlScreen", name: "Story crawl", group: "Run", nav: false, path: "Replaces everything above Hub", back: "Swallowed (Skip is on screen)", links: ["battle"] },
    { id: "dialogue", cls: "DialogueScreen", name: "Dialogue", group: "Run", nav: false, path: "Pushed over its caller", back: "Unchanged (OnBack as today)", links: [] },
    { id: "battle", cls: "BattleScreen", name: "Battle", group: "Run", nav: false, path: "Replaces everything above Hub", back: "Cancels targeting, then opens Pause (unchanged)", links: ["levelup", "postbattle"] },
    { id: "levelup", cls: "LevelUpScreen", name: "Level up", group: "Results", nav: false, path: "Over Battle", back: "Same as its Continue", links: ["postbattle"] },
    { id: "postbattle", cls: "PostBattleScreen", name: "Post battle", group: "Results", nav: false, path: "Over Battle", back: "Same as Continue: ResetTo(Hub)", links: ["hub", "battle"] },
    { id: "mercrenew", cls: "MercenaryRenewalScreen", name: "Mercenary renewal", group: "Results", nav: false, path: "After results", back: "Same as its Done", links: [] },

    { id: "crucible", cls: "CrucibleScreen", name: "Crucible", group: "Crucible", nav: true, path: "Hub > Crucible", back: "Pops to Hub", links: ["hero-party", "battle"] },
    { id: "hero-party", cls: "PartyScreen (HeroScreen, Party tab)", name: "Change team", group: "Crucible", nav: true, path: "Hub > Crucible > Hero(Party)", back: "Pops to Crucible", links: [] },

    { id: "hero", cls: "HeroScreen (+ Status, Equip, Abilities, SkillTree, Party wrappers)", name: "Heroes", group: "Camp: Heroes", nav: true, path: "Hub > Hero", back: "Pops to Hub", links: ["respec"] },
    { id: "respec", cls: "RespecScreen", name: "Inn: Respec", group: "Camp: Heroes", nav: true, path: "Hub > Hero > Respec", back: "Pops to Hero (only while Features.InnRespecRow is on)", links: [] },

    { id: "summon", cls: "SummonScreen", name: "Summon Circle", group: "Camp: Summon", nav: true, path: "Hub > Summon", back: "Pops to Hub. The reveal overlay hides the nav until it ends", links: ["merc"] },
    { id: "merc", cls: "MercenaryScreen", name: "Mercenaries", group: "Camp: Summon", nav: true, path: "Hub > Summon > Mercenaries", back: "Pops to Summon", links: [] },

    { id: "shop", cls: "ShopScreen", name: "Merchant", group: "Camp: Shop", nav: true, path: "Hub > Shop", back: "Pops to Hub", links: ["diamonds"] },
    { id: "diamonds", cls: "DiamondStoreScreen", name: "Diamond store", group: "Camp: Shop", nav: true, path: "Hub > Shop > Diamonds", back: "Pops to Shop", links: [] },

    { id: "craft", cls: "(Craft group menu, GG-T1346)", name: "Craft", group: "Camp: Craft", nav: true, path: "Hub > Craft", back: "Pops to Hub", links: ["items", "blacksmith", "alchemist", "enchantress", "aetherist", "caravan"] },
    { id: "items", cls: "ItemsScreen", name: "Items", group: "Camp: Craft", nav: true, path: "Hub > Craft > Items", back: "Pops to Craft", links: [] },
    { id: "blacksmith", cls: "BlacksmithScreen", name: "Blacksmith", group: "Camp: Craft", nav: true, path: "Hub > Craft > Blacksmith", back: "Pops to Craft", links: [] },
    { id: "alchemist", cls: "AlchemistScreen", name: "Alchemist", group: "Camp: Craft", nav: true, path: "Hub > Craft > Alchemist", back: "Pops to Craft", links: [] },
    { id: "enchantress", cls: "EnchantressScreen", name: "Enchantress", group: "Camp: Craft", nav: true, path: "Hub > Craft > Enchantress", back: "Pops to Craft", links: [] },
    { id: "aetherist", cls: "AetheristScreen", name: "Manasmith", group: "Camp: Craft", nav: true, path: "Hub > Craft > Manasmith", back: "Pops to Craft", links: [] },
    { id: "caravan", cls: "CaravanScreen", name: "Caravan", group: "Camp: Craft", nav: true, path: "Hub > Craft > Caravan", back: "Pops to Craft", links: [] },

    { id: "journal", cls: "(Journal group menu, GG-T1346)", name: "Journal", group: "Camp: Journal", nav: true, path: "Hub > Journal", back: "Pops to Hub", links: ["talk", "quests", "bounties", "bestiary", "codex"] },
    { id: "talk", cls: "TalkScreen", name: "Talk", group: "Camp: Journal", nav: true, path: "Hub > Journal > Talk", back: "Pops to Journal", links: ["dialogue"] },
    { id: "quests", cls: "QuestScreen", name: "Quests", group: "Camp: Journal", nav: true, path: "Hub > Journal > Quests", back: "Pops to Journal", links: [] },
    { id: "bounties", cls: "BountyScreen", name: "Bounties", group: "Camp: Journal", nav: true, path: "Hub > Journal > Bounties", back: "Pops to Journal", links: [] },
    { id: "bestiary", cls: "BestiaryScreen", name: "Bestiary", group: "Camp: Journal", nav: true, path: "Hub > Journal > Bestiary", back: "Pops to Journal", links: [] },
    { id: "codex", cls: "CodexScreen", name: "Codex", group: "Camp: Journal", nav: true, path: "Hub > Journal > Codex", back: "Pops to Journal", links: [] },

    { id: "settings", cls: "SettingsScreen", name: "Settings", group: "Camp: Settings", nav: true, path: "Hub > Settings", back: "Pops to Hub. Save & Quit lives here now", links: ["export", "import", "windoweditor"] },
    { id: "export", cls: "SaveExportScreen", name: "Export save", group: "Camp: Settings", nav: true, path: "Hub > Settings > Export", back: "Pops to Settings", links: [] },
    { id: "import", cls: "SaveImportScreen", name: "Import save", group: "Camp: Settings", nav: true, path: "Hub > Settings > Import", back: "Pops to Settings", links: [] },
    { id: "windoweditor", cls: "WindowEditorScreen", name: "Window editor (dev)", group: "Camp: Settings", nav: false, path: "Hub > Settings > WindowEditor", back: "Pops to Settings. A dev tool: full screen, no nav", links: [] }
  ],

  // Which tab lights for a screen group; anything else lights Home.
  tabFor: { "Campaign": "campaign", "Camp: Heroes": "heroes", "Camp: Summon": "summon", "Camp: Shop": "shop" },

  currentGraph: [
    "Title --> SaveSlot (New / Load) --> Camp (HubScreen) <------------------------------+",
    "  | Continue --------------------->   | menu rows push sub-screens; Back pops      |",
    "  +- Credits                          +- Adventure -> [StoryCrawl, first entry] -> Battle",
    "                                      +- Crucible                                  |    |",
    "                                      +- Items . Heroes . Talk                     |    v",
    "                                      +- Merchant . Inn . Blacksmith . Alchemist   | PostBattle",
    "                                      +- Enchantress . Manasmith                   | (Continue -> Camp,",
    "                                      +- Bounties . Quests . Summon Circle         |  Retry / Next stage",
    "                                      +- Diamonds . Mercenaries . Caravan          +-- -> Battle)",
    "                                      +- Bestiary . Codex . Settings",
    "                                      +- New Game+ . Save & Quit -> Title",
    "",
    "One scrolling list of up to 24 rows. Depth is unbounded: every row and every link inside a page Pushes."
  ],

  newGraph: [
    "Title --> SaveSlot --> [first run: CharacterSelect -> Prologue] --> Hub (root)",
    "",
    "Hub (root) ---------------------------------------------------------------+",
    "  Campaign pillar:  [Continue: Stage N] ==> StoryCrawl / Battle           |",
    "                    [Stage select] -> StageSelect -> StageConfirm ==> Run |",
    "  Crucible pillar:  [Crucible] -> Crucible -> Change team                 |",
    "  Camp pillar:      Heroes | Summon | Shop | Craft | Journal | Settings   |",
    "                      Craft   -> Items . Blacksmith . Alchemist .         |",
    "                                 Enchantress . Manasmith . Caravan        |",
    "                      Journal -> Talk . Quests . Bounties . Bestiary .    |",
    "                                 Codex                                    |",
    "                                                                          |",
    "Bottom nav on every HubPage:  Home | Campaign | Heroes | Summon | Shop     |",
    "  tap X  = ResetTo(Hub) + Push(X)         -> stack is [Hub, X]            |",
    "  Home   = ResetTo(Hub)                   -> stack is [Hub]               |",
    "  Back   = pop one; at the root, ask \"Return to the title?\"               |",
    "",
    "Run (StoryCrawl, Battle, LevelUp, PostBattle): no nav. ==> = Replace above Hub.",
    "PostBattle Continue = ResetTo(Hub) ---------------------------------------+",
    "Final victory (first time, 27.0): PostBattle -> StoryCrawl (Ending) -> Credits -> Title."
  ],

  rules: [
    ["Stack ceiling", "The stack never holds more than Hub + 2 pages. A push of a page that would make a fourth entry Replaces the top instead, so Back from it goes to the group page, never deeper. Only a Dialogue (a scene, not a page) may sit above that, and it pops back to the page that opened it."],
    ["Bottom nav", "Shown on HubScreen and every HubPage while a save is loaded. Five tabs: Home, Campaign, Heroes, Summon, Shop. The tab of the page's group is lit; Crucible, Craft, Journal and Settings light Home. Tapping any tab, the lit one too, is ResetTo(Hub) then Push(its screen): one fade, and the tab always lands on its own top page."],
    ["Locked tabs", "Summon and Shop follow their station's unlock (CampUnlocks.SummonOpen, the Merchant joining). Before that the tab is dimmed with a padlock; a tap toasts the join or milestone hint and does not navigate."],
    ["Back", "Pops one page (Router.Back). A screen may still swallow it (OnBack), as Battle does for targeting and pause."],
    ["Home", "ResetTo(HubScreen). The header's Home button (GG-T1335) and the Home tab do the same thing."],
    ["Back at the root", "Hardware back or Escape on the hub asks \"Return to the title?\" (Confirm). Yes saves (App.Persist) and ResetTo(TitleScreen): the old Save & Quit row. No stays."],
    ["Runs", "Continue, StageConfirm and the Crucible launch a run by Replacing everything above Hub, so a run's stack is [Hub, StoryCrawl or Battle]. Results end with ResetTo(Hub); Retry and Next stage Replace the Battle."],
    ["No nav here", "Battle, Dialogue, StoryCrawl, LevelUp, PostBattle and MercenaryRenewal (the run and its results), the Summon reveal overlay, Confirm, the dev Window editor, and the front door (Title, Profile, SaveSlot, CharacterSelect, Credits, and Settings opened from Title)."],
    ["Front door", "The first-run hand-off from the Prologue into the hub and the coach-mark lessons that teach the pillars and tabs belong to GG-T1258. This model only says the Prologue ends in ResetTo(Hub)."],
    ["Party strip", "The hub root keeps a party strip above the pillars (portraits, level, HP; tap opens Heroes on that hero). The Commander replacing Warden there belongs to GG-T1270."]
  ],

  // GG-T1614: every Push / Replace / ResetTo in App/Screens, PostBattle, LevelUp and the battle pause menu, as audited
  // for GG-T1341. [from, call, to, note]. Calls are Push (P), Replace (R), ResetTo (RT), PushOrReturn (PoR), Home.
  routes: [
    ["Splash", "RT", "Title", "after the studio logo"],
    ["Title", "P", "SaveSlot / Profile / Settings / Credits", "Load Game, New Game, Profile, Settings, Credits rows"],
    ["Title", "RT", "Hub", "Continue; GameApp.cs boot and slot load"],
    ["SaveSlot", "R", "SaveSlot", "Turn mode row; Import restore"],
    ["SaveSlot", "P", "CharacterSelect", "new game"],
    ["SaveSlot", "RT", "Hub", "load; new game after the opening beats (StoryBeats.NewCampaign / WithChapter)"],
    ["Profile select", "P", "ProfileName", "Rename, + New profile"],
    ["Profile name", "RT", "Title", "done"],
    ["Settings", "P", "WindowEditor / SaveExport / SaveImport / About", "rows; About pushes ThirdPartyNotices"],
    ["Hub", "P", "Status / Respec / StageSelect / Crucible", "party card, Respec, Adventure and Continue fallback, Crucible card"],
    ["Hub", "PoR", "every CampMenu destination", "tile grid via Ui.NavDestination; a tile never stacks a page already on the stack"],
    ["Hub", "RT", "Title", "Save & Quit confirm (ToTitle)"],
    ["Bottom nav tab", "Home + P", "its page", "Ui.NavGo: Home() then Push(dest); stack becomes [Hub, page]"],
    ["Header Home", "Home", "Hub", "shown only on the hub trail and from page depth 2 up"],
    ["HubPage header", "PoR", "DiamondStore", "currency strip Shards; pops back to a Shard Store already on the stack, no-op when it is the current page"],
    ["StageSelect", "P", "StageConfirm", "Launch(stage)"],
    ["StageConfirm / StageLauncher", "P or R", "StoryCrawl / Dialogue / Battle", "R when launched from the page itself (replaceCurrent), P otherwise"],
    ["Battle pause menu", "P", "Settings (How to play)", "Close() then Push; awayOnSettings returns to the pause menu; the Settings page is not a HubPage child of the hub, so no nav"],
    ["Battle", "R", "LevelUp or PostBattle", "results replace the battle"],
    ["LevelUp", "R", "PostBattle", "Continue and the last page"],
    ["LevelUp", "P", "HeroScreen.Learn", "Learn now, over the results (PostBattle replaced under it)"],
    ["PostBattle", "P", "HeroScreen.Learn", "results-learn-<class>; over the results, no nav, no Home"],
    ["PostBattle", "P", "Dialogue (ForRunner)", "a scene after the fight"],
    ["PostBattle", "RT", "Hub", "Continue, Retreat early, demo auto-return"],
    ["PostBattle", "R", "Battle", "Retry, Next stage (StageLauncher replaceCurrent)"],
    ["Mercenary camp", "P", "MercenaryRenewal", "after results, once per pending renewal"],
    ["Dialogue", "R", "its then screen", "finish: replaces itself with `then`"],
    ["Dialogue", "Back, else RT", "caller, else Hub", "finish with nothing under it goes to the Hub, never strands"],
    ["Talk / Journal", "P", "Dialogue", "a topic or NPC scene over the page"],
    ["Crucible", "P", "Party (Change team)", "crucible-team-change"],
    ["StoryCrawl", "RT", "Hub / next beat / Battle", "ends in ResetTo of its next screen"],
    ["Ending", "RT", "Credits then Title", "first final victory"]
  ],

  // GG-T1341 / GG-T1614: the two router rules behind the nav.
  routerRules: [
    ["PushOrReturn", "ScreenRouter.PushOrReturn(s): when a screen of the same type and Id is already on the stack, pop back to it in one fade (nothing when it is the current screen); otherwise Push. Used for the Shard Store from every currency strip, DiamondStoreScreen.Open and the hub tiles, so the stack never holds the same page twice."],
    ["OnHubTrail", "ScreenRouter.OnHubTrail: true only when every screen above the HubScreen is a HubPage. HubPage shows the bottom nav only on the trail, and the header Home only on the trail from two pages up (PagesAboveHub > 1). A page opened over a battle, its results or the pause menu's Settings (HeroScreen.Learn, Settings from Pause) is off the trail: no nav-*, no Home, so it cannot abandon the run."],
    ["Dialogue finish", "A finished Dialogue Replaces itself with its next screen; with none, it Backs; with nothing to go back to, ResetTo(Hub)."]
  ],

  bible: [
    {
      sec: "11.2.1 Screen transition graph",
      text: "Amended 2026-11-27 (GG-T1328): the hub root replaces Camp's 24-row list. Title > SaveSlot > (first run: CharacterSelect > Prologue) > Hub. The hub root shows the party strip and three pillars: Campaign (Continue: Stage N launches the next uncleared stage in one tap; Stage select), Crucible, and Camp (tiles Heroes, Summon, Shop, Craft, Journal, Settings; Craft and Journal open a group menu). A run (StoryCrawl, Battle, LevelUp, PostBattle) Replaces everything above Hub and ends in ResetTo(Hub). Final victory is unchanged. The full map of every screen to its pillar and route back is docs/design/hub-navigation/."
    },
    {
      sec: "11.3 Screen navigation",
      text: "Amended 2026-11-27 (GG-T1328): every HubPage and the hub root carry a bottom nav, Home | Campaign | Heroes | Summon | Shop, while a save is loaded. A tab tap is ResetTo(Hub) then Push(its screen); Home and the header's Home button are ResetTo(Hub); Back pops one. The stack never exceeds Hub + 2: a push that would make a fourth entry Replaces the top. Back at the hub root asks \"Return to the title?\" and, on Yes, saves and ResetTo(Title). No nav on the run and its results, Dialogue, the Summon reveal, Confirm, or the front door."
    },
    {
      sec: "25.0 Camp navigation",
      text: "Amended 2026-11-27 (GG-T1328): the menu rows become the Camp pillar's six tiles. Heroes (Heroes screen; Inn rest and Respec ride on it), Summon (Summon Circle, Mercenaries), Shop (Merchant, Diamond store), Craft (Items, Blacksmith, Alchemist, Enchantress, Manasmith, Caravan), Journal (Talk, Quests, Bounties, Bestiary, Codex) and Settings (Settings, save export and import, Save & Quit). Adventure becomes the Campaign pillar, Crucible its own pillar, New Game+ moves to the Campaign pillar. Station unlocks, join hints and row badges carry over to the tiles and group menus."
    }
  ],

  stories: [
    ["Hub root with three pillars: Campaign (one-tap Continue, Stage select), Crucible, Camp tiles", "11.2.1, 25.0"],
    ["Campaign Continue launches the next uncleared stage in one tap", "11.2.1"],
    ["Bottom nav Home | Campaign | Heroes | Summon | Shop on the hub and every HubPage; tab = ResetTo(Hub) + Push", "11.3"],
    ["The router stack never exceeds Hub + 2", "11.3"],
    ["Home (tab and header button) is ResetTo(Hub); Back pops one", "11.3"],
    ["Back at the hub root asks to return to the title, saving first", "11.3"],
    ["No bottom nav on the run, results, Dialogue, Summon reveal or front-door screens", "11.3"],
    ["Camp tiles group every station: Heroes, Summon, Shop, Craft, Journal, Settings", "25.0"]
  ]
};
