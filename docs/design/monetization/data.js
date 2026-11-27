// Monetization design data (GG-T1444). Numbers marked "today" are read from the code named beside them; prices marked
// placeholder are proposals until the owner sets them (GG-T1445). StoreCatalog (GG-T1447) is built from the product list.
window.MONETIZATION = {
  rates: [
    ["Shards per US dollar", "100", "DiamondExchange.DiamondsPerDollar", "today"],
    ["Gold per Shard", "6 G", "DiamondExchange.GoldPerDiamond", "today"],
    ["Mana Dust per Shard (refine)", "10", "DustEconomy.DustPerShard", "today"],
    ["Gold per Mana Dust (Manasmith)", "2 G", "DustEconomy.GoldPerDust", "today"],
    ["Mana Dust per victory", "1, +4 on a first clear, +1 per Mana Orb left", "DustEconomy.DustPerVictory / FirstClearDustBonus / DustPerOrbLeft", "today"],
    ["Summon Circle opens", "after campaign stage 5", "CampUnlocks.SummonAfter", "today"]
  ],

  placements: [
    {
      id: "shop",
      name: "Shop tab",
      when: "Bottom-nav Shop tab, once the Merchant has joined (the tab is dimmed with his join hint before that).",
      what: [
        ["Merchant", "Today's ShopScreen body: gold goods only. No real money here."],
        ["Shards", "Today's Shard Store (DiamondStoreScreen): the four Shard packs at the flat rate, the exchange line (1 Shard = 6 G), Restore purchases. Opens with the Summon Circle."],
        ["Offers", "Cards with both prices where one exists (real money, or Shards), the account limit left, and what is inside. Empty until the starter offer unlocks."],
        ["Cosmetics", "Unit frames, premium window themes, hub backdrops (GG-T1458). Each card names its earned source as well as its price."]
      ],
      rules: "Opening the Shop never pops anything. Tabs that have nothing to show say why and when, in one line.",
      refs: "GG-T1451, GG-T1458, BIBLE 25.0, docs/design/hub-navigation/"
    },
    {
      id: "summon",
      name: "Summon",
      when: "The Summon tab and Camp tile, once campaign stage 5 is cleared (CampUnlocks.SummonOpen).",
      what: [
        ["Draw x1 / x10", "Each button names what it will spend, in this order: Summon Tickets, then Shards, then gold at the exchange (100 Shards or 600 G a draw today, DiamondSummon.Price)."],
        ["Recruit", "The chosen-hero gold recruit stays (1,500 G, Paywall.SummonGoldPrice): the one draw-free path to a hero."],
        ["Short of the price", "The button stays enabled; a tap says what is missing and offers one way to the Shard Store (today's summon-diamond-store button). No auto-open."]
      ],
      rules: "Rates are one tap away on every banner. Tickets are always spent before Shards so earned currency is never stranded.",
      refs: "GG-T1413, GG-T1422, GG-T1428, BIBLE 25.10"
    },
    {
      id: "rested",
      name: "Out-of-Rested prompt",
      when: "Launching a stage or the Crucible with no Rested left (RestedPool, LaunchCost 1).",
      what: [
        ["Wait", "First and default: the pool refills on its own. The prompt shows when the next point is back."],
        ["Refill with gold", "100 G, a short timed job (Paywall.RestedRefillGoldPrice)."],
        ["Refill now", "25 Shards (or 150 G at the exchange), instant (RestedPool.DiamondRefillPrice)."],
        ["Short of both", "One line and one button to the Shard Store (PaywallUi via DiamondStores.OfferStore). No offer cards here."]
      ],
      rules: "The prompt only appears when the player tries to launch; Camp never nags about Rested.",
      refs: "BIBLE 25.18, PaywallUi.cs, RestedPool.cs"
    },
    {
      id: "starter",
      name: "Starter offer",
      when: "Once per account, the first time Camp opens after the Summon Circle has opened (stage 5 cleared).",
      what: [
        ["How it shows", "One card in Shop > Offers plus a badge on the Shop tab. Never a modal, never in the middle of a run or its results."],
        ["How long", "No countdown. It stays in Offers until bought or dismissed; a dismissed card can be found again under Offers."],
        ["What it is", "starter_pack (see the product list): the single bonus-rate product in the catalogue."]
      ],
      rules: "Exactly one offer is ever pushed. Every other offer waits quietly in the Offers tab.",
      refs: "GG-T1450"
    }
  ],

  principles: [
    {
      title: "Every paid thing has an earned path",
      body: "Nothing is sold that play cannot also reach. Shards: Mana Dust earned in battle (1 a victory, +4 on a first clear, +1 per Mana Orb left) refines at the Manasmith, 10 Dust to 1 Shard (DustEconomy, HubScreens AetheristScreen). Summon Tickets: campaign milestones, Crucible best-wave milestones, some quest rewards and a daily first win (GG-T1422). Anything priced in Shards also takes gold at 6 G a Shard. Cosmetics: each has a free-track, Shard or quest source as well as its pack."
    },
    {
      title: "No story behind money",
      body: "Every stage, story beat, Camp keeper and hero the story needs is free. Summoning and recruiting are game systems with no story meaning (canon 05-factions). Paid things are convenience (a draw, a refill), cosmetics, or a head start; never a chapter, an ending or a better ending."
    },
    {
      title: "No ads",
      body: "No ad SDK, no rewarded video, no interstitials, no cross-promotion. A free player's time is never sold."
    },
    {
      title: "No pressure in the first session",
      body: "Nothing real-money is offered before the Summon Circle opens: no Shard Store row, no offer, no badge. The first session (the drills and stages 1 to 5) never shows a price in real money."
    },
    {
      title: "Low and honest prices",
      body: "Shard packs keep the flat rate (owner: \"low and honest\"): every pack costs the same per Shard. Prices show in the player's currency from the store. No fake timers, no 'best value' flags, no stacked discounts. The starter pack is the only bonus rate."
    },
    {
      title: "One catalogue, granted once",
      body: "Every product lives in StoreCatalog with its id, type and contents and is granted exactly once per transaction (GG-T1446). Non-consumables restore on a new device (iOS Restore row, Android owned purchases at connect)."
    }
  ],

  // type: Consumable or NonConsumable, as the stores define them. limit: per account; 0 = none.
  products: [
    { id: "diamonds_100", name: "100 Mana Shards", type: "Consumable", limit: 0, price: "$1.00", status: "live", contents: "100 Shards", earned: "1,000 Mana Dust refined, or spend 600 G where gold is taken" },
    { id: "diamonds_500", name: "500 Mana Shards", type: "Consumable", limit: 0, price: "$5.00", status: "live", contents: "500 Shards", earned: "As above" },
    { id: "diamonds_1000", name: "1,000 Mana Shards", type: "Consumable", limit: 0, price: "$10.00", status: "live", contents: "1,000 Shards", earned: "As above" },
    { id: "diamonds_2500", name: "2,500 Mana Shards", type: "Consumable", limit: 0, price: "$25.00", status: "live", contents: "2,500 Shards", earned: "As above" },
    { id: "starter_pack", name: "Starter pack", type: "Consumable", limit: 1, price: "$2.99", status: "new", contents: "300 Shards, 5 Summon Tickets, one unit frame", earned: "Shards and Tickets as above; the frame is also on the season pass free track" },
    { id: "season_pass_s1", name: "Season pass premium, season 1", type: "NonConsumable", limit: 1, price: "$4.99", status: "new", contents: "The premium track of season 1 (cosmetics, Shards, Tickets)", earned: "The free track fills by playing; premium cosmetics come back later for Shards" },
    { id: "cosmetic_frames_1", name: "Unit frames bundle", type: "NonConsumable", limit: 1, price: "$1.99", status: "new", contents: "Three unit frames", earned: "Each frame sells alone for Shards in Shop > Cosmetics" },
    { id: "cosmetic_themes_1", name: "Window themes bundle", type: "NonConsumable", limit: 1, price: "$2.99", status: "new", contents: "Two premium window themes", earned: "Each theme sells alone for Shards; free presets stay free" },
    { id: "cosmetic_backdrops_1", name: "Hub backdrops bundle", type: "NonConsumable", limit: 1, price: "$1.99", status: "new", contents: "Two procedural hub backdrops", earned: "Each backdrop sells alone for Shards" }
  ],

  // In-game prices paid in Shards (or gold at the exchange). Not store products; listed so the whole price sheet is in one place.
  shardPrices: [
    ["Summon draw x1", "100 Shards (or 600 G)", "DiamondSummon.Price"],
    ["Summon draw x10", "1,000 Shards (or 6,000 G), proposal: no discount", "GG-T1428"],
    ["Instant Rested refill", "25 Shards (or 150 G)", "RestedPool.DiamondRefillPrice"],
    ["Single cosmetic", "placeholder 200 to 400 Shards", "GG-T1456"]
  ],

  questions: [
    "Real prices for every product (GG-T1445). Keep the flat $1.00 = 100 Shards, or move the packs to $0.99 / $4.99 / $9.99 / $24.99 store tiers?",
    "Starter pack contents and price: 300 Shards + 5 Tickets + a frame for $2.99 is a placeholder.",
    "Season pass: one product id per season (season_pass_s1, s2 ...) as a NonConsumable, or one Consumable that the game ties to the current season?",
    "Should the premium pass also be buyable with Shards (an earned path for the pass itself), and at what price?",
    "Should x10 draws carry a discount, or stay ten times x1 (the flat-rate rule)?"
  ]
};
