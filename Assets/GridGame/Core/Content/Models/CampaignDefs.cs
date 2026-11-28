using System.Collections.Generic;

namespace GridGame.Core.Content
{
    /// <summary>A caravan upgrade (§25.12): bought with gold, one value per level.</summary>
    public sealed class CaravanUpgradeDef
    {
        public string Id;
        public string Name;
        public string Description;
        public string Icon;
        public List<CaravanLevel> Levels = new List<CaravanLevel>();
        /// <summary>The crew member who runs this wagon (GG-T75), e.g. "Brannoc".</summary>
        public string Crew;
        /// <summary>Their job on the caravan, e.g. "cask-keeper".</summary>
        public string CrewRole;
        /// <summary>One-line flavor barks shown on Camp and the Caravan screen — never dialogue: no branching, no choices (§27).</summary>
        public List<string> Barks = new List<string>();

        // ── Facility upgrades (§25.12.2, GG-T432) ──────────────────────────────────────────────
        // A crafting station's facility is a caravan upgrade whose levels gate material tiers: each level lets the
        // station make a rarer result. Only the ids below may be facilities (ContentLoader.Validate).

        /// <summary>The Blacksmith's forge (Copper → Iron → Steel → Mythril).</summary>
        public const string ForgeFacilityId = "forge";
        /// <summary>The Alchemist's still (Clay Still → Glass Alembic → Silver Alembic).</summary>
        public const string AlchemyFacilityId = "alembic";

        /// <summary>The facility id of a crafting station, or null when the station has none (the Enchant station).</summary>
        public static string FacilityId(CraftStation station) =>
            station == CraftStation.Forge ? ForgeFacilityId : station == CraftStation.Alchemy ? AlchemyFacilityId : null;

        /// <summary>A facility's name before any level is bought ("Copper Forge").</summary>
        public string BaseName;
        /// <summary>The rarest result the unbought facility can make; set only on a facility.</summary>
        public ItemRarity? BaseRarity;

        public bool IsFacility => BaseRarity.HasValue;
    }

    public sealed class CaravanLevel
    {
        public int Cost;
        public float Value;
        public string Text;
        /// <summary>A facility level's name ("Iron Forge", GG-T432); null on an ordinary upgrade.</summary>
        public string Name;
        /// <summary>The rarest result a facility at this level can make (GG-T432); null on an ordinary upgrade.</summary>
        public ItemRarity? Rarity;
    }

    public sealed class ThemeDef
    {
        public string Id;
        public string Name;
        public List<string> Stages = new List<string>();
        /// <summary>Campaign milestone (GG-T301): an item granted once, the first time the theme's last stage is won
        /// (a Tome, so a story beat can teach a passive). Null = none.</summary>
        public string MilestoneReward;
        /// <summary>GG-T677: the theme's first full clear also paid one owned Mana Orb; since GG-T1811 (orbs retired) it pays
        /// <c>ManaOrbs.MilestoneShards</c> Mana Shards instead.</summary>
        public bool MilestoneOrb;
        /// <summary>GG-T677: a Halcyon ruin. Each of its stages holds one find, made on a victory with this chance (0 = none)
        /// until found; every stage's find is collected at most once. It paid a Mana Orb; since GG-T1811 it pays
        /// <c>ManaOrbs.HalcyonFindDust</c> Mana Dust.</summary>
        public float OrbFindChance;
        /// <summary>GG-T344: the theme's background set, <c>Backgrounds/&lt;set&gt;-00..NN</c> (owner rule: named after the
        /// story region, its words hyphenated, numbered from 00, GG-T561). Null = the theme id (<see cref="Backdrops.SetOf"/>).</summary>
        public string Backgrounds;
        /// <summary>Region rule ids (regionrules.json, GG-T359) every stage of this theme plays under.</summary>
        public List<string> Rules = new List<string>();
        /// <summary>GG-T456: the region's own battle tracks (Resources/Audio/Music/&lt;name&gt;), drawn one per stage from a
        /// shuffle bag so none repeats until the whole playlist has played.</summary>
        public List<string> Music = new List<string>();
        /// <summary>GG-T456: the closest-sounding tracks from other regions, added to the bag while the region has fewer
        /// than <c>MusicPlaylist.MinSize</c> tracks of its own. Drop them as the region gets its own music.</summary>
        public List<string> MusicBorrow = new List<string>();
        /// <summary>GG-T1478: the region has an entry crawl, Prose key <see cref="CrawlKey"/>, played once on the first
        /// arrival (merged with a chapter crawl due at the same stop).</summary>
        public bool HasCrawl;
        /// <summary>The region-entry crawl's words, joined from Prose at load (never in gameplay JSON).</summary>
        public string Crawl;

        /// <summary>A region-entry crawl's Prose key (GG-T1478).</summary>
        public static string CrawlKey(string id) => "crawl:region/" + id;

        /// <summary>What <c>SaveData.SeenCrawls</c> records once the region-entry crawl has played (GG-T1478).</summary>
        public string SeenMark => "region:" + Id;
    }

    public sealed class CampaignDef
    {
        /// <summary>GG-T456: the Camp's peaceful tracks, one per return from the field.</summary>
        public List<string> HubMusic = new List<string>();
        /// <summary>GG-T458: playlists for the non-battle moments, keyed by <c>MusicPlaylist.MomentKeys</c> (title, story,
        /// victory, defeat, levelup, crucible, boss, finalboss, charselect, credits). Each draws from its own shuffle bag;
        /// an empty or missing list keeps the screen's fallback.</summary>
        public Dictionary<string, List<string>> MomentMusic = new Dictionary<string, List<string>>();
        public List<ThemeDef> Themes = new List<ThemeDef>();
        /// <summary>GG-T1374: the tutorial drill stages, in play order. Never in a theme, so campaign order,
        /// HighestClearedStage, CampUnlocks, chapter crawls and the Codex do not see them.</summary>
        public List<string> Tutorial = new List<string>();
        /// <summary>GG-T2075: the prologue battles a new game plays in place of <see cref="Tutorial"/> (drills like it, never in a
        /// theme); the Tutorial drills stay in Play a drill. Empty = a new game plays the Tutorial drills.</summary>
        public List<string> Prologue = new List<string>();
        /// <summary>GG-T231: the crawl that starts a new campaign (GG-T1363: the Opening, paged; was the Prologue), and
        /// the one after the final boss.</summary>
        public CrawlDef Opening;
        public CrawlDef Ending;
        /// <summary>GG-T386: the chapter crawls, in campaign order, one after every fifth campaign stage (each names the
        /// stage it follows). They replace the per-theme crawls, so the player never sees two scrolls back to back.</summary>
        public List<CrawlDef> Chapters = new List<CrawlDef>();
    }

    /// <summary>A bounty (§22.4, GG-T431): a marked, tougher variant of an existing enemy class, with its own title, that
    /// roams one campaign stage. Taken at the board, it joins <see cref="Wave"/> of <see cref="Stage"/>; slain, its reward
    /// is collected back at the board. Gold scales with the stage (<c>Bounties.RewardGold</c>).</summary>
    public sealed class BountyDef
    {
        public string Id;
        /// <summary>The target's title, its name in battle: "Grimjaw the Mauler".</summary>
        public string Name;
        /// <summary>The enemy class it is a variant of (no new art).</summary>
        public string TargetClass;
        /// <summary>The campaign stage it roams; the clue names it ("Seen wandering …").</summary>
        public string Stage;
        /// <summary>0-based wave it joins (the next wave with room if that one is full).</summary>
        public int Wave;
        /// <summary>An optional extra trait: a rule-breaking boss's rule (§14.9). None = only the stat buff.</summary>
        public RuleBreak RuleBreak;
        public string RewardItem;
        /// <summary>Optional skill-point payout (GG-T134): on Claim every hero in the active party gains this
        /// many skill points. 0 (the default) pays none.</summary>
        public int RewardSkillPoints;
    }

    /// <summary>An offer in the Shop's Offers tab (offers.json, GG-T1450): sold either for real money as a store product
    /// (<see cref="ProductId"/>, a <c>StoreCatalog</c> id whose contents these must match) or for Shards
    /// (<see cref="ShardPrice"/>), never both. Rules in <c>Meta/Offers</c>.</summary>
    public sealed class OfferDef
    {
        public string Id;
        public string Name;
        /// <summary>The real-money product it sells, or null for a Shard offer.</summary>
        public string ProductId;
        /// <summary>The Shards it costs; 0 for a real-money offer.</summary>
        public int ShardPrice;
        /// <summary>Shards paid into the account wallet.</summary>
        public int Shards;
        /// <summary>Summon Tickets paid into the account wallet.</summary>
        public int Tickets;
        /// <summary>Cosmetic ids the account owns once it is claimed.</summary>
        public List<string> Cosmetics = new List<string>();
        /// <summary>How many times one account may claim it; 0 is no limit.</summary>
        public int AccountLimit;
        /// <summary>Campaign stages the save must have cleared before it shows (0 shows from the start).</summary>
        public int ShowsAfter;
        /// <summary>When it leaves the Shop (UTC); null never ends.</summary>
        public System.DateTime? EndUtc;
    }
}
