// Every number the Summon design uses, as the named constant it would get in Meta (SummonConfig).
// The owner sets the final values (GG-T1414); these are the proposed defaults.
window.SUMMON = {
  config: [
    { name: "PullTickets", value: 1, unit: "Ticket", group: "Price", note: "One pull costs one Summon Ticket." },
    { name: "PullShards", value: 100, unit: "Shards", group: "Price", note: "Or 100 Mana Shards ($1.00, or 600 G through the exchange, Â§25.18.1): the price the Diamond draw already has." },
    { name: "MultiCount", value: 10, unit: "pulls", group: "Price", note: "A x10 is ten pulls in one tap." },
    { name: "MultiPriceFactor", value: 10, unit: "x", group: "Price", note: "A x10 costs exactly ten pulls: no discount, no bonus pull, so x1 is never the worse deal." },
    { name: "FeaturedShare", value: 0.5, unit: "share", group: "Rates", note: "On a Featured banner, half the pulls bring the featured hero; the rest are a Standard pull." },
    { name: "FeaturedCount", value: 1, unit: "hero", group: "Rates", note: "One featured hero per banner." },
    { name: "FeaturedPity", value: 5, unit: "pulls", group: "Pity", note: "Hard pity: the 5th Featured pull without the featured hero brings them." },
    { name: "NewHeroPity", value: 10, unit: "pulls", group: "Pity", note: "Hard pity: the 10th pull in a row without a new hero brings an unowned one, while any remain on that banner." },
    { name: "SparkPulls", value: 25, unit: "pulls", group: "Pity", note: "Every 25 pulls on a banner kind earn a Spark: pick any hero on that banner outright." },
    { name: "FeaturedDays", value: 14, unit: "days", group: "Banners", note: "A Featured banner runs 14 days, then its hero joins the Standard banner for good." },
    { name: "EchoSp", value: 1, unit: "SP", group: "Duplicates", note: "A duplicate is an Echo: +1 skill point for that hero." },
    { name: "EchoCap", value: 5, unit: "Echoes", group: "Duplicates", note: "At most 5 Echoes (5 SP) per hero, about 5 levels of board nodes." },
    { name: "FragmentsPerSpareEcho", value: 2, unit: "fragments", group: "Duplicates", note: "A duplicate past the cap gives 2 Ticket fragments instead." },
    { name: "FragmentsPerTicket", value: 10, unit: "fragments", group: "Duplicates", note: "10 fragments make a Summon Ticket, automatically." },
    { name: "StarterTickets", value: 10, unit: "Tickets", group: "Earning", note: "Granted on the first Summon Circle visit: one free x10." },
    { name: "TicketsPerFirstClear", value: 1, unit: "Ticket", group: "Earning", note: "First clear of each campaign stage (40 stages)." },
    { name: "TicketsPerChapter", value: 5, unit: "Tickets", group: "Earning", note: "Finishing each campaign chapter (8 chapters)." },
    { name: "WinsPerTicket", value: 5, unit: "wins", group: "Earning", note: "Every 5 battles won, any mode, the Crucible included." },
    { name: "HistoryKeep", value: 500, unit: "pulls", group: "History", note: "The save keeps the last 500 pulls; older ones roll off." },
    { name: "RevealSeconds", value: 1, unit: "s", group: "Ceremony", note: "The summoning job's length (the Summon paywall job, Â§25.18); a x10 reveals ten cards in one sequence." }
  ],

  campaign: { stages: 40, chapters: 8, winsEstimate: 60 },

  sources: [
    { source: "Story recruit", how: "Joins on a story beat (Â§25.17).", owner: "BIBLE Â§25.17", paid: false },
    { source: "Trust recruitment / wanderer", how: "Fights beside the party, then talks its way in (Â§25.11.2).", owner: "GG-T1257", paid: false },
    { source: "Guest or talk-recruitable", how: "An authored ally freed or talked round in battle (Â§25.11, Â§25.11.1).", owner: "BIBLE Â§25.11", paid: false },
    { source: "Summon Circle, chosen", how: "Pick a listed hero for gold (1,500 G, Â§25.10). Not random.", owner: "BIBLE Â§25.10", paid: false },
    { source: "Mercenary", how: "Hired for a number of battles, then leaves (Â§25.10.1). Never owned.", owner: "BIBLE Â§25.10.1", paid: false },
    { source: "Standard banner", how: "Random pull from every summonable hero. Tickets or Shards.", owner: "this page", paid: true },
    { source: "Featured banner", how: "Random pull, half of it the featured hero, for 14 days.", owner: "this page", paid: true }
  ],

  // Sample names only: hero names and lore belong to GG-T1270.
  standardPool: ["Hexsniper", "Hexwarden", "Hexstalker", "Monster Tamer", "Ronin", "Pugilist", "Vampire", "Thief"],
  featured: "Summon-only A",
  summonOnly: ["Summon-only A", "Summon-only B", "Summon-only C", "Summon-only D", "Summon-only E", "Summon-only F"]
};
