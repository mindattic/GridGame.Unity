// Trust and cohesion (BIBLE 25.20, GG-T1290). Every number here is a BondConfig setting in the proposal.
window.TrustCohesion = {
  scale: { min: 0, max: 100 },
  tiers: [
    { tier: 0, floor: 0, trust: "No bonus", cohesion: "No bonus" },
    { tier: 1, floor: 10, trust: "+2% crit, +2% evasion; Trust scene 1", cohesion: "+3% pincer damage" },
    { tier: 2, floor: 25, trust: "+4% crit, +4% evasion", cohesion: "+6% pincer damage; pair scene 1" },
    { tier: 3, floor: 45, trust: "+6% crit, +6% evasion; Trust scene 2", cohesion: "+9% pincer damage; supporter reach +1 toward this partner" },
    { tier: 4, floor: 70, trust: "+8% crit, +8% evasion", cohesion: "+12% pincer damage; reach +1; support magnitude +10%; pair scene 2" },
    { tier: 5, floor: 100, trust: "+10% crit, +10% evasion; Resolve; Trust scene 3", cohesion: "+15% pincer damage; reach +1; support +10%; Duo support" }
  ],
  floorRule: "A bond never drops below the floor of the highest tier it has reached, so no unlock is ever lost.",
  sources: [
    { bond: "Trust", source: "Dialogue choice", amount: "-10 to +10", note: "Set per choice; pays once per save" },
    { bond: "Trust", source: "Joins as a story recruit", amount: "10", note: "Seed on joining (tier 1)" },
    { bond: "Trust", source: "Wanderer won at full Rapport", amount: "15", note: "Seed on joining" },
    { bond: "Trust", source: "Summon, guest or talk-recruitable", amount: "5", note: "Seed on joining" },
    { bond: "Trust", source: "Starting hero", amount: "0", note: "" },
    { bond: "Trust", source: "Victory", amount: "+1", note: "Each hero who fought, downed or not" },
    { bond: "Cohesion", source: "Pair-scene dialogue choice", amount: "-10 to +10", note: "Pays once per save" },
    { bond: "Cohesion", source: "Pincer together in a won battle", amount: "+1 each", note: "Two ends, or an end and a supporter" },
    { bond: "Cohesion", source: "Heal, shield or buff the partner", amount: "+1 each", note: "In a won battle" },
    { bond: "Cohesion", source: "Battle cap", amount: "5", note: "Most one pair earns per battle" }
  ],
  payoffs: [
    { bond: "Trust", what: "Crit chance and evasion", value: "+2% each per tier" },
    { bond: "Trust", what: "Resolve (tier 5)", value: "Once per battle, survive a lethal blow at 1 HP; a Clutch! kind" },
    { bond: "Cohesion", what: "Bonded pincer partners", value: "+3% damage per tier" },
    { bond: "Cohesion", what: "Supporter reach (tier 3+)", value: "+1 toward the bonded attacker" },
    { bond: "Cohesion", what: "Support magnitude (tier 4+)", value: "+10% on heals, shields and buffs to the partner" },
    { bond: "Cohesion", what: "Duo support (tier 5)", value: "An authored support for that pair" },
    { bond: "Story", what: "Trust scenes", value: "At Trust tiers 1, 3 and 5" },
    { bond: "Story", what: "Pair scenes", value: "At cohesion tiers 2 and 4" },
    { bond: "Story", what: "Chapter voice", value: "The most trusted hero in the party voices the chapter beats" },
    { bond: "Story", what: "New Game+", value: "Keeps every bond" }
  ],
  whoHasTrust: [
    { who: "Heroes on the roster", trust: true },
    { who: "The lead Warden (while it exists)", trust: false },
    { who: "Domesticated monsters", trust: false },
    { who: "Mercenaries", trust: false }
  ],
  coreCast: ["Barbarian", "Cleric", "Red Mage", "Engineer", "Knight", "Sage"],
  naming: {
    trust: "Trust: the lasting bond one hero has with the Commander (0-100, tiers 0-5).",
    rapport: "Rapport: the battle gauge a wanderer fills (code name Unit.Trust, BIBLE 25.11.2). Only its player text changes."
  }
};
