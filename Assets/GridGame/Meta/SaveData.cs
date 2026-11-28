using System;
using System.Collections.Generic;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Meta
{
    /// <summary>
    /// One save slot (bible §15). Plain data, JSON-serialized by the app's save store.
    /// Bump <see cref="CurrentVersion"/> and add a step to <see cref="SaveMigrations"/> when the shape changes.
    /// </summary>
    /// <summary>Legacy shape (GG-T1790): an expedition in progress in an older save, drained into travel on load
    /// (<see cref="SaveMigrations.DrainExpedition"/>).</summary>
    public sealed class ExpeditionSave
    {
        public string ThemeId;
        /// <summary>Index (into the theme's stages) of the next battle.</summary>
        public int Next;
        /// <summary>Orbs left in the bank at the end of the last battle; they start the next one.</summary>
        public int CarriedOrbs;
        /// <summary>Coins earned so far on this expedition (the completion bonus is a share of it).</summary>
        public int Coins;
    }

    public sealed class SaveData
    {
        public const int CurrentVersion = 28;

        public int Version = CurrentVersion;
        public string Name = "Lightbearer";
        /// <summary>The player's own name (GG-T1342): what <see cref="Commander.Name"/> and the {commander} text token say.</summary>
        public string CommanderName;
        public DateTime CreatedUtc = DateTime.UtcNow;
        public DateTime UpdatedUtc = DateTime.UtcNow;
        public double PlaySeconds;

        public int Gold;
        public int LifetimeCoins;
        /// <summary>Campaign index of the furthest cleared stage (-1 = none).</summary>
        public int HighestClearedStage = -1;
        public HashSet<string> ClearedStages = new HashSet<string>();
        public HashSet<string> SeenCrawls = new HashSet<string>();
        /// <summary>Story flags set by dialogue (§27.1, GG-T117) — the same idea as SeenCrawls. A joined Camp NPC
        /// is the flag "npc:&lt;id&gt;" (JoinNpc), which ClearFlag can't touch.</summary>
        public HashSet<string> Flags = new HashSet<string>();
        /// <summary>Dialogue ids played to the end: story beats never replay; Talk dialogues marked Once don't either.</summary>
        public HashSet<string> SeenDialogues = new HashSet<string>();
        public string LastStage;

        public List<HeroSave> Roster = new List<HeroSave>();
        public List<string> Party = new List<string>();
        public Dictionary<string, int> Inventory = new Dictionary<string, int>();
        public int Recruits;
        /// <summary>Caravan upgrade id → level bought (§25.12).</summary>
        public Dictionary<string, int> Caravan = new Dictionary<string, int>();
        /// <summary>Crafting station id ("blacksmith"/"alchemist") → accumulated XP (§24.6.1, GG-T35).</summary>
        public Dictionary<string, int> StationXp = new Dictionary<string, int>();
        /// <summary>Guest allies lost for good (§25.11) — only ever recorded or honored with <see cref="Permadeath"/> on
        /// (a future Ironman mode); in the normal game it stays empty, and entries from older saves are ignored (GG-T114).</summary>
        public HashSet<string> LostGuests = new HashSet<string>();
        /// <summary>Recipe ids discovered early, before the crafting station reached their tier — by reading a
        /// recipe scroll or first-ever pickup of the recipe's rarest input (§24.6.2, GG-T36). A recipe whose
        /// tier the station has already reached is always discovered regardless of this set.</summary>
        public HashSet<string> DiscoveredRecipes = new HashSet<string>();
        /// <summary>GG-T538: Merchant items this save has bought. One stays on the shelf whatever its stock tier
        /// (<see cref="VendorTiers.OnShelf"/>), so nothing a player once bought goes away.</summary>
        public HashSet<string> ShopBought = new HashSet<string>();
        /// <summary>GG-T538: Forge and Alchemy recipes this save has made. One stays in its station's list whatever its
        /// progress tier (<see cref="VendorTiers.RecipeOpen"/>).</summary>
        public HashSet<string> CraftedRecipes = new HashSet<string>();
        /// <summary>Spare equipment that has been used: item id → its worn copies (per-instance durability, US-246).</summary>
        public Dictionary<string, List<WornGear>> Worn = new Dictionary<string, List<WornGear>>();
        /// <summary>Shattered/broken gear kept as salvage fodder instead of lost outright: item id → count of
        /// Broken copies. Broken pieces can't be equipped or repaired — only scrapped at the Blacksmith (GG-T41).</summary>
        public Dictionary<string, int> Broken = new Dictionary<string, int>();
        /// <summary>Advances once per finished battle; seeds the Merchant's "Used / Bargain" shelf so it
        /// turns over on every return to camp but replays identically from the same save (GG-T38).</summary>
        public int BargainRotation;
        /// <summary>Chests opened so far (GG-T301): seeds each chest's roll, so a save replays its chests identically.</summary>
        public int ChestsOpened;
        /// <summary>Themes whose campaign milestone reward has been paid (GG-T301, <see cref="ThemeDef.MilestoneReward"/>).</summary>
        public HashSet<string> Milestones = new HashSet<string>();
        /// <summary>GG-T677: Mana Orbs owned while orbs were progression. Retired by GG-T1811: read only by the v28 save
        /// migration, which pays each owned orb out once (<see cref="ManaOrbs.RetirePayout"/>).</summary>
        public int OwnedOrbs;
        /// <summary>GG-T677: Halcyon-ruin stages whose find has been made (each holds one; <see cref="ManaOrbs.AfterVictory"/>).</summary>
        public HashSet<string> OrbFinds = new HashSet<string>();
        /// <summary>GG-T677: Broken Mana Orbs that dropped while they could (they stopped dropping with GG-T1811).</summary>
        public int BrokenOrbsDropped;
        /// <summary>GG-T677: orb rolls made so far: seeds each find/drop roll, so a save replays its rolls identically.</summary>
        public int OrbRolls;
        public bool TutorialDone;
        /// <summary>Camp stations whose first-use tip has been shown (GG-T173, <see cref="CampTips"/>).</summary>
        public HashSet<string> SeenCampTips = new HashSet<string>();
        /// <summary>A `-testmode` throwaway save (GG-T93): enemy levels follow the party (Progression.StageRecommendedLevel).</summary>
        public bool TestMode;
        /// <summary>New Game+ cycle (0 = first run). Unlocked by clearing the last campaign stage (US-332).</summary>
        public int NewGamePlus;
        /// <summary>The turn mode this save is locked to (§2.13, GG-T773), chosen at New Game; null = unlocked, every battle
        /// plays the profile's <see cref="SettingsSave.TurnMode"/>. <see cref="TurnModeSetting.Resolve"/> picks between them.
        /// Save v15: an older save loads unlocked (and every profile defaults to PartyWindow, the only rule it knew).</summary>
        public TurnMode? TurnModeLock;

        /// <summary>Legacy (§22.5, retired by travel in GG-T1790): an expedition an older save was on. Only read by
        /// <see cref="SaveMigrations.Migrate"/>, which drains it into <see cref="World"/> and clears it.</summary>
        public ExpeditionSave Expedition;
        public BountySave Bounty = new BountySave();
        /// <summary>Optional side quests (§22.11, GG-T237): under way (with progress) and handed in.</summary>
        public QuestSave Quests = new QuestSave();
        /// <summary>Crucible record (§22.10): best waves survived and runs fought on this save.</summary>
        public CrucibleSave Crucible = new CrucibleSave();
        /// <summary>Mercenary contracts and the hiring board (GG-T233, <see cref="Mercenaries"/>).</summary>
        public MercenarySave Mercenaries = new MercenarySave();
        /// <summary>Party-wide energy spent per stage launch (§25.18, <see cref="RestedPool"/>).</summary>
        public RestedSave Rested = new RestedSave();
        /// <summary>Paid, timed paywall jobs still running (§25.18, <see cref="Paywall"/>).</summary>
        public List<PaywallJob> Jobs = new List<PaywallJob>();
        /// <summary>Real-money purchases already granted, by store transaction id (§25.18.1, GG-T469, <see cref="PurchaseLedger"/>).
        /// Save v11: an older build refuses this save rather than drop the ledger and pay a re-delivered order twice.</summary>
        public PurchaseSave Purchases = new PurchaseSave();
        public Dictionary<string, BestiaryEntry> Bestiary = new Dictionary<string, BestiaryEntry>();
        /// <summary>GG-T923: Codex entries unlocked, "&lt;kind&gt;:&lt;id&gt;" (<see cref="GridGame.Meta.Codex"/>). Save v16: an
        /// older save back-fills everything it had already reached, taken as read.</summary>
        public HashSet<string> Codex = new HashSet<string>();
        /// <summary>GG-T923: Codex entries the player has opened; unlocked and not in here is "new".</summary>
        public HashSet<string> CodexRead = new HashSet<string>();
        /// <summary>GG-T1292: each hero's Trust in the Commander, 0-100, by class id (§25.20, <see cref="Bonds"/>). Save v20: an
        /// older save starts every hero at 0.</summary>
        public Dictionary<string, int> Trust = new Dictionary<string, int>();
        /// <summary>GG-T1292: cohesion between two heroes, 0-100, by <see cref="Bonds.PairKey"/> ("a|b", sorted).</summary>
        public Dictionary<string, int> Cohesion = new Dictionary<string, int>();
        /// <summary>GG-T1292: the highest Trust tier each hero has reached; Trust never drops below its floor.</summary>
        public Dictionary<string, int> TrustTierReached = new Dictionary<string, int>();
        /// <summary>GG-T1292: the highest cohesion tier each pair has reached; cohesion never drops below its floor.</summary>
        public Dictionary<string, int> CohesionTierReached = new Dictionary<string, int>();
        /// <summary>GG-T1292: bond-paying dialogue choices already paid, "dialogueId/choiceId". Kept through New Game+.</summary>
        public HashSet<string> BondChoices = new HashSet<string>();
        /// <summary>GG-T1592: heroes (class id, lower case) whose meeting the story has told: the starting party and story
        /// recruits (<see cref="Bonds.Joined"/>). Save v22: an older save counts its whole roster as met.</summary>
        public HashSet<string> MetHeroes = new HashSet<string>();
        /// <summary>GG-T1419: summon pity per banner kind and the pull history (<see cref="Gacha"/>). Save v23: an older save
        /// starts with no pulls.</summary>
        public SummonState Summon = new SummonState();
        /// <summary>GG-T1626: tutorial drills won (<see cref="Drills"/>). Save v24: an older save that finished the tutorial
        /// counts every drill cleared.</summary>
        public HashSet<string> ClearedDrills = new HashSet<string>();
        /// <summary>GG-T1392: the onboarding step this campaign is on (<see cref="GridGame.Meta.Onboarding"/>). Save v25: an
        /// older save is Done.</summary>
        public OnboardingSave Onboarding = new OnboardingSave();
        /// <summary>GG-T1778: the overworld (<see cref="WorldSave"/>, BIBLE §28.13). Save v26: an older save starts camped at
        /// its furthest cleared stage's node.</summary>
        public WorldSave World = new WorldSave();
        // Settings no longer live in a save slot (GG-T99): they belong to the player profile's settings.json
        // (PlayerProfiles.cs). An older slot's "Settings" block is ignored on load; LegacySaveMigration reads it once.

        public HeroSave Hero(string classId) => Roster.Find(h => string.Equals(h.ClassId, classId, StringComparison.OrdinalIgnoreCase));

        public int Count(string itemId) => itemId != null && Inventory.TryGetValue(itemId, out var n) ? n : 0;

        public void AddItem(string itemId, int n)
        {
            if (itemId == null || n == 0) return;
            int v = Count(itemId) + n;
            if (v <= 0) Inventory.Remove(itemId); else Inventory[itemId] = v;
            if (n > 0) GridGame.Meta.Codex.Unlock(this, CodexKinds.Item, itemId); // GG-T923: a gained item unlocks its Codex entry
            // Worn copies can't outnumber owned copies; when copies leave the bag the most-worn go first (an enchanted
            // copy, GG-T179, last).
            if (Worn.TryGetValue(itemId, out var worn))
            {
                worn.Sort((a, b) => { int e = (b.Enchant != null).CompareTo(a.Enchant != null); return e != 0 ? e : b.Durability.CompareTo(a.Durability); });
                while (worn.Count > Count(itemId)) worn.RemoveAt(worn.Count - 1);
                if (worn.Count == 0) Worn.Remove(itemId);
            }
        }

        public int BrokenCount(string itemId) => itemId != null && Broken.TryGetValue(itemId, out var n) ? n : 0;

        public void AddBroken(string itemId, int n)
        {
            if (itemId == null || n == 0) return;
            int v = BrokenCount(itemId) + n;
            if (v <= 0) Broken.Remove(itemId); else Broken[itemId] = v;
        }
    }

    public sealed class HeroSave
    {
        public string ClassId;
        public int TotalXp;
        /// <summary>Current HP; 0 = full (wounds carry between battles, §15.1).</summary>
        public int Hp;
        public List<string> Known = new List<string>();
        /// <summary>Five ability-bar entries: ability id, "item:id", or null.</summary>
        public List<string> Bar = new List<string> { null, null, null, null, null };
        /// <summary>Unspent skill points (§15.1, GG-T49), earned per level plus milestone bonuses
        /// (<see cref="Formulas.SkillPointsForLevelUp"/>). Spent on skill-tree nodes (GG-T50).</summary>
        public int SkillPoints;
        /// <summary>The ability id whose "New ability ready" badge the player has seen by opening this hero's Board
        /// (GG-T1407, <see cref="NextAbility.MarkSeen"/>). An older save simply has none.</summary>
        public string AbilityReadySeen;
        /// <summary>Duplicate summons of this hero (§25.10.3, GG-T1421, <see cref="Gacha.Echo"/>), each worth
        /// <see cref="Gacha.EchoSp"/> SP, up to <see cref="Gacha.EchoCap"/>. An older save simply has none.</summary>
        public int Echoes;
        /// <summary>Skill-tree nodes bought with points, as "treeId/nodeId" (§25.6.3, GG-T53, the retired talent trees);
        /// the Inn's Respec clears it and refunds their cost. An older save simply has none.</summary>
        public List<string> TreeNodes = new List<string>();
        /// <summary>Skill points actually paid for each bought node, keyed like <see cref="TreeNodes"/> (premium included), so
        /// Respec refunds what was paid even if the off-tree premium has since changed. A node bought before this was recorded
        /// (an older save) has no entry and refunds its node's cost. Null-safe: may be missing.</summary>
        public Dictionary<string, int> TreeNodeCosts;
        /// <summary>Skill hexagon nodes this hero owns, as "q,r" (GG-T1031, <see cref="SkillHexProgress"/>), bought with
        /// <see cref="SkillPoints"/>. An older save simply has none. Null-safe: may be missing.</summary>
        public List<string> HexNodes = new List<string>();
        /// <summary>Level-up stat points put into each stat (GG-T591, <see cref="StatPoints"/>), keyed by a
        /// <see cref="StatGrantDef.Names"/> entry: how many points, not how much stat. Earned points are derived from the
        /// level, so only the picks are saved. Null-safe: an older save has none (all its points are simply unspent).</summary>
        public Dictionary<string, int> StatPicks;
        /// <summary>Learned passives the player has switched on (GG-T301, <see cref="PassiveLoadout"/>), read only once
        /// <see cref="PassivesChosen"/> is set; until then the first <see cref="PassiveLoadout.Slots"/> learned are on.</summary>
        public List<string> ActivePassives = new List<string>();
        /// <summary>The player has picked which learned passives are on (GG-T301). An older save has not.</summary>
        public bool PassivesChosen;
        /// <summary>A domesticated monster (GG-T303, <see cref="Domestication"/>): <see cref="ClassId"/> is an enemy class. It
        /// fights on its own AI, wears no gear, has no skill tree, and grows slowly up to <see cref="Domestication.LevelCap"/>.</summary>
        public bool Domesticated;
        /// <summary>GG-T532: which version of the class this hero is: the female art (the default) or the male art
        /// (the class's MalePortrait, GG-T557). Looks only: stats, kit and story are the same either way.</summary>
        public HeroVariant Variant;
        /// <summary>GG-T532: the player hasn't picked <see cref="Variant"/> yet. Every new hero joins with it set
        /// (<see cref="Progression.NewHero"/>); the game asks at its next quiet moment and clears it, or clears it silently
        /// (Female) when the class has no male art yet. A save from before v14 has it clear.</summary>
        public bool VariantPending;

        public string WeaponId;
        public int WeaponDurability;
        public int WeaponRepairs;
        /// <summary>Masterwork stat multiplier (§24.6.2, GG-T46); 1 = normal gear.</summary>
        public float WeaponMastery = 1f;
        /// <summary>Armor equipped in Head/Torso/Legs/Gloves/Boots, each with its own durability/repairs/mastery —
        /// the same per-piece tracking Weapon already had (§24.2, GG-T89).</summary>
        public Dictionary<EquipmentSlot, EquippedArmor> Armor = new Dictionary<EquipmentSlot, EquippedArmor>();
        /// <summary>Jewelry equipped in Necklace/Earring1/Earring2/Ring1/Ring2 — plain item ids, no durability
        /// (the same no-wear rule the old Relic slots had, GG-T89).</summary>
        public Dictionary<EquipmentSlot, string> Jewelry = new Dictionary<EquipmentSlot, string>();

        // ── v2-and-earlier save shape, kept only so SaveMigrations can drain them into Armor/Jewelry above.
        // Never read anywhere else — a v3+ save always has these empty/default. ──
        public string ArmorId;
        public int ArmorDurability;
        public int ArmorRepairs;
        public float ArmorMastery = 1f;
        public List<string> Relics = new List<string> { null, null, null };
    }

    /// <summary>GG-T532: a hero's version, picked when they join (owner 2026-11-21: "when they finally join your party you
    /// get to choose the gender ... no change to story or mechanics makes everything equal").</summary>
    public enum HeroVariant
    {
        Female = 0,
        Male = 1,
    }

    /// <summary>GG-T532: the rules of hero variants that need no art: the portrait id a variant reads, the heroes still
    /// waiting for the player's pick, and picking. Whether the art exists is the app's question (Art.HasMaleArt).</summary>
    public static class HeroVariants
    {
        /// <summary>The Portraits/ file <paramref name="def"/> draws as <paramref name="v"/> (GG-T557,
        /// <see cref="GridGame.Core.Content.ClassDef.PortraitFile"/>): its male art when male and it has one, else its
        /// default art; <paramref name="variant"/> picks a numbered alternate (a costume), default the class's own.</summary>
        public static string PortraitFile(GridGame.Core.Content.ClassDef def, HeroVariant v, int? variant = null) =>
            def?.PortraitFile(v == HeroVariant.Male, variant);

        /// <summary>The owned hero's variant; Female for a class the save doesn't hold.</summary>
        public static HeroVariant Of(SaveData s, string classId) =>
            s?.Roster == null || string.IsNullOrEmpty(classId) ? HeroVariant.Female : s.Hero(classId)?.Variant ?? HeroVariant.Female;

        /// <summary>Roster heroes still waiting for the player's pick, in join order.</summary>
        public static List<HeroSave> Pending(SaveData s) =>
            s?.Roster == null ? new List<HeroSave>() : s.Roster.FindAll(h => h != null && h.VariantPending && !h.Domesticated);

        /// <summary>Sets the hero's variant and ends the wait. Picking again later (the hero screen) works the same way.</summary>
        public static void Choose(HeroSave h, HeroVariant v)
        {
            if (h == null) return;
            h.Variant = v == HeroVariant.Male ? HeroVariant.Male : HeroVariant.Female;
            h.VariantPending = false;
        }

        /// <summary>A wanderer's look on the board (either, evenly), from a seed so a battle replays the same.</summary>
        public static HeroVariant Roll(int seed) => new Random(seed).Next(2) == 0 ? HeroVariant.Female : HeroVariant.Male;
    }

    /// <summary>One equipped armor piece (§24.2, GG-T89): mirrors <see cref="WornGear"/>'s shape plus the item id,
    /// since (unlike the bag's Worn list, keyed by item id already) a hero's Armor dict is keyed by slot.</summary>
    public sealed class EquippedArmor
    {
        public string ItemId;
        public int Durability;
        public int Repairs;
        public float Mastery = 1f;
        /// <summary>Armor enchant id (armorenchants.json, GG-T179) bound into this piece by the Enchantress; null = none.
        /// It grants its passive while the piece is worn and intact, and is lost if the piece breaks.</summary>
        public string Enchant;
    }

    public sealed class WornGear
    {
        public int Durability;
        public int Repairs;
        /// <summary>Masterwork stat multiplier this specific copy rolled at craft time (§24.6.2, GG-T46); 1 = normal.</summary>
        public float Mastery = 1f;
        /// <summary>Armor enchant this copy carries (GG-T179), kept while it sits in the bag; null = none.</summary>
        public string Enchant;
    }

    /// <summary>The bounty board (§22.4, GG-T431): marked targets taken, slain and awaiting their reward, and paid.</summary>
    public sealed class BountySave
    {
        /// <summary>Bounties taken and still roaming their stage.</summary>
        public List<string> Taken = new List<string>();
        /// <summary>Targets killed whose reward waits at the board.</summary>
        public List<string> Slain = new List<string>();
        /// <summary>Bounties paid out; they never post again.</summary>
        public HashSet<string> Claimed = new HashSet<string>();
        /// <summary>How many times the board's open offers have turned over (seeds them).</summary>
        public int Refresh;
        /// <summary>Stage battles fought since the last turnover.</summary>
        public int Battles;
        /// <summary>The board has been used (a bounty taken, or an old kill contract held), so its Camp row never locks again.</summary>
        public bool Used;
        /// <summary>Retired pre-GG-T431 kill contract fields: read from old saves, then cleared by the migration.</summary>
        public string ActiveId;
        public int Progress;
    }

    public sealed class QuestSave
    {
        /// <summary>Quest id → progress (Defeat kills so far; ClearStage 1 once met).</summary>
        public Dictionary<string, int> Active = new Dictionary<string, int>();
        public HashSet<string> Done = new HashSet<string>();
    }

    /// <summary>The save's Crucible record (§22.10). <see cref="Runs"/> also seeds the next run, so a save replays
    /// the same gauntlet until it fights it.</summary>
    public sealed class CrucibleSave
    {
        public int Best;
        public int Runs;
        public int TotalWaves;
        /// <summary>The most gold one run has banked (wave pay plus drops, GG-T430); a worse run never lowers it.</summary>
        public int BestGold;
        /// <summary>GG-T945 (§2.13.1, §22.10): the records of every mode other than <see cref="TurnMode.PartyWindow"/>,
        /// whose record stays <see cref="Best"/>/<see cref="BestGold"/> so an older save loads and reads unchanged. Null
        /// (and left out of the JSON) until a run in another mode ends; the JSON keys it by mode name.
        /// <see cref="Runs"/> and <see cref="TotalWaves"/> stay shared: Runs seeds the next run whatever its mode.</summary>
        public Dictionary<TurnMode, CrucibleRecord> ByMode;

        /// <summary>The best waves survived in <paramref name="mode"/>'s runs (0 before the first).</summary>
        public int BestFor(TurnMode mode) =>
            mode == TurnMode.PartyWindow ? Best : ByMode != null && ByMode.TryGetValue(mode, out var r) ? r.Best : 0;

        /// <summary>The richest haul of <paramref name="mode"/>'s runs (0 before the first).</summary>
        public int BestGoldFor(TurnMode mode) =>
            mode == TurnMode.PartyWindow ? BestGold : ByMode != null && ByMode.TryGetValue(mode, out var r) ? r.BestGold : 0;

        /// <summary>Records one finished run of <paramref name="mode"/>: counts it, and raises that mode's best waves and
        /// best haul (never lowers them). True when the waves are a new best for the mode.</summary>
        public bool Record(TurnMode mode, int waves, int gold)
        {
            Runs++;
            TotalWaves += waves;
            bool newBest = waves > BestFor(mode);
            if (mode == TurnMode.PartyWindow)
            {
                if (newBest) Best = waves;
                if (gold > BestGold) BestGold = gold;
                return newBest;
            }
            ByMode ??= new Dictionary<TurnMode, CrucibleRecord>();
            if (!ByMode.TryGetValue(mode, out var r)) ByMode[mode] = r = new CrucibleRecord();
            if (newBest) r.Best = waves;
            if (gold > r.BestGold) r.BestGold = gold;
            return newBest;
        }
    }

    /// <summary>One turn mode's Crucible record (GG-T945): its best waves survived and its richest haul.</summary>
    public sealed class CrucibleRecord
    {
        public int Best;
        public int BestGold;
    }

    /// <summary>What the save knows about one enemy class (§25.11). How much the Bestiary and the battle inspect
    /// reveal is <see cref="BestiaryKnowledge.Tier"/>, earned by <see cref="Defeated"/>.</summary>
    public sealed class BestiaryEntry
    {
        public bool Seen;
        /// <summary>Retired with the Scan ability (GG-T290). Never set any more; an older save that scanned this enemy
        /// keeps at least the stats tier through <see cref="BestiaryKnowledge.Tier"/>.</summary>
        public bool Scanned;
        public int Defeated;
    }

    /// <summary>The Bestiary's knowledge tiers (§25.11, GG-T290): the more of an enemy the party defeats, the more it
    /// learns. Seen gives its name and description; then Speed, then HP and every base stat, then its elemental
    /// weaknesses and resistances, and last its abilities and how it picks targets.</summary>
    public static class BestiaryKnowledge
    {
        public const int None = 0, Description = 1, Speed = 2, Stats = 3, Elements = 4, Abilities = 5;
        public const int DefeatsForSpeed = 1, DefeatsForStats = 3, DefeatsForElements = 6, DefeatsForAbilities = 10;

        /// <summary>The highest tier <paramref name="e"/> has reached. A pre-GG-T290 Scanned entry counts as at least Stats.</summary>
        public static int Tier(BestiaryEntry e)
        {
            if (e == null) return None;
            int d = Math.Max(0, e.Defeated);
            int t = d >= DefeatsForAbilities ? Abilities : d >= DefeatsForElements ? Elements : d >= DefeatsForStats ? Stats
                : d >= DefeatsForSpeed ? Speed : e.Seen ? Description : None;
            if (e.Scanned && t < Stats) t = Stats;
            return t;
        }

        public static int Tier(SaveData s, string classId) =>
            s?.Bestiary != null && classId != null && s.Bestiary.TryGetValue(classId, out var e) ? Tier(e) : None;

        public static bool Knows(BestiaryEntry e, int tier) => Tier(e) >= tier;
        public static bool Knows(SaveData s, string classId, int tier) => Tier(s, classId) >= tier;

        /// <summary>The defeat count that unlocks <paramref name="tier"/> (0 for Description and below).</summary>
        public static int DefeatsFor(int tier) =>
            tier >= Abilities ? DefeatsForAbilities : tier == Elements ? DefeatsForElements : tier == Stats ? DefeatsForStats : tier == Speed ? DefeatsForSpeed : 0;

        /// <summary>The next tier still to learn, or -1 when everything is known.</summary>
        public static int NextTier(BestiaryEntry e)
        {
            int t = Tier(e);
            return t >= Abilities ? -1 : Math.Max(Speed, t + 1);
        }

        /// <summary>Defeats still needed for the next tier (0 when everything is known).</summary>
        public static int DefeatsToNext(BestiaryEntry e)
        {
            int next = NextTier(e);
            return next < 0 ? 0 : Math.Max(0, DefeatsFor(next) - Math.Max(0, e?.Defeated ?? 0));
        }

        /// <summary>What a tier reveals, as the Bestiary's "Defeat N more to learn …" phrase.</summary>
        public static string Label(int tier) => tier switch
        {
            Speed => "its speed",
            Stats => "its HP and stats",
            Elements => "its weaknesses",
            Abilities => "its abilities",
            _ => "about it",
        };

        /// <summary>Its elemental weaknesses and resistances from the class's multipliers, e.g. "Immune to Poison ·
        /// weak to Fire (×1.5)", or "No elemental weakness or resistance".</summary>
        public static string ElementLine(ClassDef d)
        {
            var parts = new List<string>();
            if (d?.Resistances != null)
                foreach (var kv in d.Resistances)
                {
                    if (Math.Abs(kv.Value - 1f) < 0.001f) continue;
                    parts.Add(kv.Value <= 0f ? $"immune to {kv.Key}"
                        : kv.Value > 1f ? $"weak to {kv.Key} (×{kv.Value:0.##})" : $"resists {kv.Key} (×{kv.Value:0.##})");
                }
            if (parts.Count == 0) return "No elemental weakness or resistance";
            var s = string.Join(" · ", parts);
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        /// <summary>"Defeat 2 more to learn its HP and stats", or null when everything is known.</summary>
        public static string Hint(BestiaryEntry e)
        {
            int next = NextTier(e);
            if (next < 0) return null;
            int n = DefeatsToNext(e);
            return $"Defeat {n} more to learn {Label(next)}.";
        }
    }

    /// <summary>The player's settings — one set per profile, stored in Profiles/&lt;Name&gt;/settings.json through
    /// <see cref="GameSettings"/> (GG-T99), not in a save slot.</summary>
    public sealed class SettingsSave
    {
        public float MusicVolume = 0.6f;
        public float SfxVolume = 0.7f;
        public bool MuteMusic;
        public bool MuteSfx;
        public bool ColorblindMode;
        public bool ReduceMotion;
        /// <summary>Terra Battle-style drag timer (§2, act-advanced amendment). Off by default.</summary>
        public bool DragTimer;
        /// <summary>Battle animation speed multiplier (1 = normal).</summary>
        public float BattleSpeed = 1f;
        /// <summary>Close-up portrait cut-in on pincers (ported from GridGame2026).</summary>
        public bool PincerCutIns = true;
        /// <summary>Pick a target, then confirm (owner direction 2026-10-11). Off = commit on the pick, for veterans.</summary>
        public bool ConfirmActions = true;
        /// <summary>Menu window theme by name: a preset from Resources/UI/themes.json or one of <see cref="CustomThemes"/>
        /// (GG-T407); unknown/missing names fall back to the default: Manapunk since 2026-11-22 (GG-T678), Dark before
        /// (see <see cref="DefaultMenuTheme"/>).</summary>
        public string MenuTheme = DefaultMenuTheme;
        /// <summary>The player's own window themes from Settings → Windows (GG-T407), at most
        /// <see cref="WindowThemeCatalog.MaxCustom"/>, each named apart from every preset.</summary>
        public List<WindowThemeDef> CustomThemes = new List<WindowThemeDef>();
        /// <summary>The player's Brightness/Contrast/Gamma for each preset theme, by theme name (GG-T563): kept here, on the
        /// device, so themes.json stays pristine and an update never wipes the tuning. A preset not listed is at 0.5/0.5/0.5;
        /// a custom theme carries its own values.</summary>
        public Dictionary<string, WindowToneDef> ThemeTones = new Dictionary<string, WindowToneDef>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Body font by name (App.Fonts.Presets); unknown/missing names fall back to Rounded (GG-T385).</summary>
        public string FontName = DefaultFontName;
        // Owner 2026-11-21: "we tried FF, then Chrono, then back to FF - none of these where right; but the colors
        // in all these apps is better" (GridGame.Gallery's own look, see Theme.uss's .theme-dark retint) - "Dark" is
        // the base default now, not "Classic" (the Final Fantasy blue it was reverted to under GG-T453).
        // GG-T678 (owner 2026-11-22): "Manapunk", between Dark and the FF blue, is the default; Dark stays selectable.
        public const string DefaultMenuTheme = "Manapunk", DefaultFontName = "Rounded";
        /// <summary>Settings → Text wrapping (GG-T305): false = Word wrap (default), true = Hyphenate (ragged lines and
        /// overlong words break at <see cref="Hyphenation"/> points).</summary>
        public bool Hyphenate;
        /// <summary>Settings → Text size (GG-T495): 100, 115 or 130 percent of the menu type scale; anything else reads as 100.</summary>
        public int TextScale = 100;
        /// <summary>The difficulty dial 1..10 (§22.9, GG-T309), 10 by default; applies from the next battle.</summary>
        public DifficultySettings Difficulty = new DifficultySettings();
        /// <summary>The turn mode (§2.13, GG-T773): <c>turnMode</c> in settings.json, <see cref="TurnMode.PartyWindow"/> by
        /// default. A save's <see cref="SaveData.TurnModeLock"/> overrides it (<see cref="TurnModeSetting.Resolve"/>).</summary>
        public TurnMode TurnMode = TurnMode.PartyWindow;
        /// <summary>Graphics → Quality (GG-T1221): a QualitySettings level index; -1 (default) leaves the project's own level.</summary>
        public int QualityLevel = -1;
        /// <summary>Graphics → Frame rate cap (GG-T1221): 30, 60 (default) or -1 for uncapped; anything else reads as 60.</summary>
        public int FrameRateCap = 60;
        /// <summary>Graphics → VSync (GG-T1221): off by default; on, it paces frames and the cap is not used.</summary>
        public bool VSync;
        /// <summary>Graphics → Screen shake (GG-T1221): 0..1 scale on every shake, 1 = full (default), 0 = off.</summary>
        public float ScreenShake = 1f;
        /// <summary>Graphics → Effects amount (GG-T1221): 0..1 scale on particle emission, 1 = full (default).</summary>
        public float VfxAmount = 1f;
        /// <summary>Sound → Master volume (GG-T1221): 0..1 over music and SFX, 1 by default.</summary>
        public float MasterVolume = 1f;
        /// <summary>Sound → Mute when unfocused (GG-T1221): silence everything while the window lacks focus. Off by default.</summary>
        public bool MuteWhenUnfocused;
        /// <summary>Display → Window mode (GG-T1222, desktop only): -1 (default) leaves the window as launched, 0 windowed,
        /// 1 fullscreen.</summary>
        public int FullScreen = -1;
        /// <summary>Display → Resolution (GG-T1222, desktop only): window size in px; 0 (default) leaves it as launched.</summary>
        public int ResolutionWidth, ResolutionHeight;
        /// <summary>Display → Thumb margin (GG-T1222, phones only): extra design px kept clear above the safe area's
        /// bottom edge, 0 (default) to <see cref="GameSettings.MaxThumbMargin"/>.</summary>
        public int ThumbMargin;
        /// <summary>Accessibility → UI scale (GG-T1222): 80, 90 or 100 (default) percent of the screen the stage fills.</summary>
        public int UiScale = 100;
        /// <summary>GG-T1618: the first-run commander-name prompt has been answered (or never applies) for this profile.</summary>
        public bool FirstRunDone;
        /// <summary>GG-T1645: the last drill's To Camp was pressed on some slot of this profile; Start Campaign's
        /// New to GridGame? question then defaults to Skip to Camp.</summary>
        public bool DrillsFinished;
        /// <summary>GG-T1456: the equipped cosmetic per <see cref="CosmeticKind"/> name (Frame, Theme, Backdrop); none = the
        /// plain look. Ownership is on the account wallet; <see cref="Cosmetics.Reconcile"/> drops what it no longer owns.</summary>
        public Dictionary<string, string> Cosmetics = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// The one place a battle's turn mode is picked (§2.13, GG-T773): the save's lock if it has one, else the profile's
    /// setting. <see cref="BattleFactory.Build"/> (campaign, stage, Crucible, bounty, wanderer, test-mode and tutorial
    /// battles), <see cref="EndlessEncounter.Build"/> and the app's battle screen all set <see cref="BattleSetup.TurnMode"/>
    /// from it; no battle picks its mode any other way.
    /// </summary>
    public static class TurnModeSetting
    {
        /// <summary>The `-turnMode` launch flag's mode (GG-T1237): wins over the profile but not a save's lock, and is never
        /// written to settings.json, so a later Settings save cannot persist it.</summary>
        public static TurnMode? LaunchOverride;

        /// <summary>The mode a battle on <paramref name="save"/> plays: its lock, else <paramref name="profile"/>.
        /// A value the enum doesn't know (a hand edit) reads as <see cref="TurnMode.PartyWindow"/>.</summary>
        public static TurnMode Resolve(SaveData save, TurnMode profile)
        {
            var mode = save?.TurnModeLock ?? LaunchOverride ?? profile;
            return Enum.IsDefined(typeof(TurnMode), mode) ? mode : TurnMode.PartyWindow;
        }

        /// <summary>The mode a battle on <paramref name="save"/> plays under <paramref name="settings"/> (null = the defaults).</summary>
        public static TurnMode Resolve(SaveData save, SettingsSave settings) =>
            Resolve(save, settings?.TurnMode ?? TurnMode.PartyWindow);

        /// <summary>New Game's choice (GG-T773): locks <paramref name="save"/> to <paramref name="mode"/>, or unlocks it
        /// (null) so it follows the profile setting.</summary>
        public static void Lock(SaveData save, TurnMode? mode)
        {
            if (save == null) return;
            save.TurnModeLock = mode is TurnMode m && Enum.IsDefined(typeof(TurnMode), m) ? m : (TurnMode?)null;
        }

        /// <summary>The mode a command-line flag or harness call names (GG-T775): any <see cref="TurnMode"/> name, any case.
        /// False with a one-line <paramref name="error"/> when the name is unknown or the mode isn't built yet
        /// (<see cref="TurnModes.IsBuilt"/>), so a typo never silently plays PartyWindow.</summary>
        public static bool TryParseBuilt(string name, out TurnMode mode, out string error)
        {
            mode = TurnMode.PartyWindow;
            error = null;
            if (string.IsNullOrWhiteSpace(name) || int.TryParse(name, out _) || !Enum.TryParse(name.Trim(), true, out TurnMode m) || !Enum.IsDefined(typeof(TurnMode), m))
            {
                error = $"unknown turn mode '{name}' (modes: {string.Join(", ", Enum.GetNames(typeof(TurnMode)))}; built: {BuiltNames()})";
                return false;
            }
            if (!TurnModes.IsBuilt(m))
            {
                error = $"turn mode {m} is not built yet (built: {BuiltNames()})";
                return false;
            }
            mode = m;
            return true;
        }

        private static string BuiltNames()
        {
            var names = new List<string>();
            foreach (var m in TurnModes.Available) names.Add(m.ToString());
            return string.Join(", ", names);
        }

        /// <summary>The mode a settings.json value names; missing or unknown reads as <see cref="TurnMode.PartyWindow"/>.</summary>
        public static TurnMode Parse(string name) =>
            !string.IsNullOrEmpty(name) && !int.TryParse(name, out _) && Enum.TryParse(name, false, out TurnMode m) && Enum.IsDefined(typeof(TurnMode), m)
                ? m : TurnMode.PartyWindow;
    }

    /// <summary>
    /// The difficulty dial (owner, GG-T309: "On a 1-10 scale you would start on 10 and let players adjust the game if
    /// they feel it is too easy or hard … the higher the difficulty the greater the rewards"). One number replaces the
    /// §22.9 scaffold's Easy/Normal/Hard/Custom preset and its six inert multipliers; what each level does is
    /// <see cref="DifficultyTable"/>. It is a profile setting (settings.json), so every save slot of the profile plays it.
    /// </summary>
    public sealed class DifficultySettings
    {
        public int Level = DifficultyTable.Default;

        /// <summary>The level an old §22.9 preset becomes. Its multipliers were pinned at 1.0 (inert), so every player
        /// actually fought the designed game — level 10 — except one who chose a lower setting on purpose: Easy → 5, and
        /// a Custom whose enemy HP/damage averaged below ×1 → the level whose enemy side is nearest that average.
        /// Normal, Hard, a Custom at ×1 or above, and anything unknown → 10.</summary>
        public static int FromLegacyPreset(string preset, float enemyHp = 1f, float enemyDamage = 1f)
        {
            switch ((preset ?? "").Trim().ToLowerInvariant())
            {
                case "easy": return 5;
                case "custom":
                    float want = (enemyHp + enemyDamage) / 2f;
                    if (!(want < 1f)) return DifficultyTable.Default;
                    int best = DifficultyTable.Min;
                    float bestGap = float.MaxValue;
                    foreach (var r in DifficultyTable.Rows)
                    {
                        float gap = Math.Abs((r.EnemyHp + r.EnemyDamage) / 2f - want);
                        if (gap < bestGap) { bestGap = gap; best = r.Level; }
                    }
                    return best;
                default: return DifficultyTable.Default;
            }
        }
    }

    public static class SaveMigrations
    {
        /// <summary>GG-T1790: travel replaced expeditions. A save loaded mid-expedition is on the road with the orbs it
        /// carried (wounds stay as saved); the legacy <see cref="SaveData.Expedition"/> is then cleared. Any version.</summary>
        public static void DrainExpedition(SaveData s)
        {
            if (s.Expedition == null) return;
            s.World ??= new WorldSave();
            s.World.CarriedOrbs = Math.Max(0, s.Expedition.CarriedOrbs);
            s.World.Camped = false;
            s.Expedition = null;
        }

        /// <summary>GG-T344: where The Still went into the campaign order, and how many stages it brought (save v5).</summary>
        public const int StillInsertedAt = 12, StillStageCount = 3;

        /// <summary>GG-T345: where The Lantern Road went into the campaign order (between the Tithe Moors and the Shardfall),
        /// and how many stages it brought (save v6).</summary>
        public const int LanternRoadInsertedAt = 3, LanternRoadStageCount = 3;

        /// <summary>GG-T350: where The Choir's Cathedral went into the campaign order (after Halcyon, the new finale), and how
        /// many stages it brought (save v7).</summary>
        public const int CathedralInsertedAt = 21, CathedralStageCount = 3;

        /// <summary>GG-T347: where The Bloom went into the campaign order (after the Cathedral, the first postgame chapter),
        /// and how many stages it brought (save v8).</summary>
        public const int BloomInsertedAt = 24, BloomStageCount = 3;

        /// <summary>GG-T357: the ten stages of the five older postgame chapters were reordered in place (save v9). Old order
        /// from <see cref="PostgameReorderedFrom"/>: the Free Holds, Deep Shardfall, the Drowned Arks, Under the Anchors, the
        /// Underspire; new order: the Free Holds, the Underspire, the Drowned Arks, Deep Shardfall, Under the Anchors.
        /// <see cref="PostgameReorderMap"/>[i] is the new campaign index of the stage that sat at old index
        /// <c>PostgameReorderedFrom + i</c>. The Bloom before them and Morrow's Shadow after them did not move.</summary>
        public const int PostgameReorderedFrom = 27;
        public static readonly int[] PostgameReorderMap = { 27, 28, 33, 34, 31, 32, 35, 36, 29, 30 };

        /// <summary>The v9 frontier: the furthest new index among every stage at or below the old
        /// <paramref name="highestCleared"/>, so each save keeps its furthest clear and nothing it cleared locks again.</summary>
        public static int ReorderedPostgameFrontier(int highestCleared)
        {
            int frontier = highestCleared;
            for (int i = 0; i < PostgameReorderMap.Length && PostgameReorderedFrom + i <= highestCleared; i++)
                frontier = Math.Max(frontier, PostgameReorderMap[i]);
            return frontier;
        }

        /// <summary>The save version current when GG-T52's gear-proficiency gate shipped; the grandfather step runs only up to it.</summary>
        public const int GearGateShippedAtVersion = 4;

        /// <summary>Ability ids removed from content with nothing to become; a save still holding one drops it on load. An
        /// ability merged into another (GG-T973) is not retired: it is an abilities.json alias (<see cref="ContentDb.AbilityAliases"/>)
        /// and <see cref="ResolveAbilityAliases"/> rewrites it to the survivor instead.</summary>
        public static readonly HashSet<string> RetiredAbilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "scan" };

        /// <summary>GG-T973: rewrites every merged ability id a save still holds to the ability it was merged into
        /// (<paramref name="aliases"/>, alias → survivor; abilities.json's <see cref="AbilityDef.MergedInto"/> rows). Per hero:
        /// <see cref="HeroSave.Known"/> and <see cref="HeroSave.ActivePassives"/> are rewritten in place and deduped (the first
        /// occurrence keeps its position); on <see cref="HeroSave.Bar"/> the alias's slot keeps the survivor, unless the survivor
        /// is already on the bar, when that slot empties ("item:" entries are untouched). Codex "ability:" entries move to the
        /// survivor, read state kept. Inventory holds item ids only: a Tome's taught ability is content (Validate keeps it off
        /// aliases). Idempotent; runs on every load so a merge needs no save-version bump. Returns how many ids it rewrote.</summary>
        public static int ResolveAbilityAliases(SaveData s, IReadOnlyDictionary<string, string> aliases)
        {
            if (s == null || aliases == null || aliases.Count == 0) return 0;
            int rewrites = 0;
            string To(string id) => id != null && aliases.TryGetValue(id, out var to) && !string.IsNullOrEmpty(to) ? to : null;
            void RewriteList(List<string> ids)
            {
                if (ids == null) return;
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < ids.Count; i++)
                {
                    var to = To(ids[i]);
                    if (to != null) { ids[i] = to; rewrites++; }
                    if (ids[i] != null && !seen.Add(ids[i])) ids.RemoveAt(i--);
                }
            }
            foreach (var h in s.Roster)
            {
                if (h == null) continue;
                RewriteList(h.Known);
                RewriteList(h.ActivePassives);
                if (h.Bar == null) continue;
                for (int i = 0; i < h.Bar.Count; i++)
                {
                    var to = To(h.Bar[i]);
                    if (to == null) continue;
                    rewrites++;
                    bool onBar = false;
                    for (int j = 0; j < h.Bar.Count; j++)
                        if (j != i && string.Equals(h.Bar[j], to, StringComparison.OrdinalIgnoreCase)) onBar = true;
                    h.Bar[i] = onBar ? null : to;
                }
            }
            foreach (var kv in aliases)
            {
                if (string.IsNullOrEmpty(kv.Value)) continue;
                string from = CodexKinds.Key(CodexKinds.Ability, kv.Key), into = CodexKinds.Key(CodexKinds.Ability, kv.Value);
                if (s.Codex != null && s.Codex.Remove(from)) { s.Codex.Add(into); rewrites++; }
                if (s.CodexRead != null && s.CodexRead.Remove(from)) s.CodexRead.Add(into);
            }
            return rewrites;
        }

        /// <summary>GG-T1245: a save written by a newer build (a downgrade). Not corruption: SaveStore leaves its files alone.</summary>
        public sealed class SaveTooNewException : InvalidOperationException
        {
            public SaveTooNewException(int version) : base($"Save version {version} is newer than this build") { }
        }

        /// <summary>Upgrades an older save in place. Unknown future versions are rejected. <paramref name="c"/>
        /// is optional (version-shape fixups below don't need content) but drives the GG-T52 proficiency
        /// grandfather clause at the end: without it that step is simply skipped. <paramref name="profileName"/> names the
        /// Commander of a save from before v21.</summary>
        public static SaveData Migrate(SaveData s, ContentDb c = null, string profileName = null)
        {
            if (s == null) return null;
            if (s.Version > SaveData.CurrentVersion) throw new SaveTooNewException(s.Version);
            // GG-T52's gear gate shipped while saves were v4: only a save that old can hold gear its hero was never gated on.
            bool beforeGearGate = s.Version <= GearGateShippedAtVersion;
            // v1 is the first shape.
            if (s.Version < 2)
            {
                // GG-T49: skill points didn't exist before v2, so a hero already past level 1 is
                // backfilled once with the points they'd have earned leveling up to here — otherwise
                // every existing save would start the new economy at a permanent deficit.
                foreach (var h in s.Roster)
                    h.SkillPoints += Formulas.SkillPointsForLevelUp(1, Formulas.LevelFromTotalXp(h.TotalXp).level);
                s.Version = 2;
            }
            s.Bounty ??= new BountySave();
            s.Bounty.Taken ??= new List<string>();
            s.Bounty.Slain ??= new List<string>();
            s.Bounty.Claimed ??= new HashSet<string>();
            if (s.Bounty.ActiveId != null)
            {
                // GG-T431: the kill-N contracts are retired. An old save's active contract is dropped (its kills meant
                // nothing in the new board); the board stays open for it.
                s.Bounty.ActiveId = null;
                s.Bounty.Progress = 0;
                s.Bounty.Used = true;
            }
            s.Quests ??= new QuestSave();
            s.Quests.Active ??= new Dictionary<string, int>();
            s.Quests.Done ??= new HashSet<string>();
            s.Crucible ??= new CrucibleSave();
            s.Mercenaries ??= new MercenarySave();
            s.Worn ??= new Dictionary<string, List<WornGear>>();
            s.Broken ??= new Dictionary<string, int>();
            s.StationXp ??= new Dictionary<string, int>();
            s.DiscoveredRecipes ??= new HashSet<string>();
            s.Flags ??= new HashSet<string>();
            s.SeenDialogues ??= new HashSet<string>();
            s.SeenCampTips ??= new HashSet<string>();
            s.Rested ??= new RestedSave();
            s.Jobs ??= new List<PaywallJob>();
            foreach (var h in s.Roster) while (h.Bar.Count < 5) h.Bar.Add(null);
            // GG-T290: the Scan ability was retired (the Bestiary now learns from defeats), so it leaves every
            // hero's known list and ability bar. Idempotent.
            foreach (var h in s.Roster)
            {
                h.Known?.RemoveAll(id => RetiredAbilities.Contains(id));
                if (h.Bar != null)
                    for (int i = 0; i < h.Bar.Count; i++)
                        if (h.Bar[i] != null && RetiredAbilities.Contains(h.Bar[i])) h.Bar[i] = null;
            }
            if (s.Version < 3)
            {
                // GG-T89: one Armor slot split into five (Head/Torso/Legs/Gloves/Boots) and Relic1-3 became
                // named jewelry (Necklace/Earring1/Earring2). Best-effort carry-over so nobody's gear vanishes:
                // the old Armor piece becomes Torso (the closest match), and relics fill in order — a relic's
                // own category may not match the slot it lands in, but it still contributes its stats there.
                var jewelrySlots = new[] { EquipmentSlot.Necklace, EquipmentSlot.Earring1, EquipmentSlot.Earring2 };
                foreach (var h in s.Roster)
                {
                    h.Armor ??= new Dictionary<EquipmentSlot, EquippedArmor>();
                    h.Jewelry ??= new Dictionary<EquipmentSlot, string>();
                    if (h.ArmorId != null)
                        h.Armor[EquipmentSlot.Torso] = new EquippedArmor { ItemId = h.ArmorId, Durability = h.ArmorDurability, Repairs = h.ArmorRepairs, Mastery = h.ArmorMastery };
                    for (int i = 0; i < jewelrySlots.Length && i < (h.Relics?.Count ?? 0); i++)
                        if (h.Relics[i] != null) h.Jewelry[jewelrySlots[i]] = h.Relics[i];
                    h.ArmorId = null; h.ArmorDurability = 0; h.ArmorRepairs = 0; h.ArmorMastery = 1f;
                    h.Relics = new List<string>();
                }
                s.Version = 3;
            }
            if (s.Version < 4)
            {
                // GG-T119: Camp NPCs are now met on the journey and their services stay hidden until they join.
                // A save from before that has been using every service all along, so everyone has already joined.
                CampNpcs.JoinAll(s);
                s.Version = 4;
            }
            if (s.Version < 5)
            {
                // GG-T344: The Still (three stages) was inserted between the Anchor Wastes and Halcyon, at campaign index
                // StillInsertedAt. HighestClearedStage is a campaign index, so a save already past the Anchor Wastes moves
                // up by the Still's stage count and keeps the same frontier (its Halcyon/postgame clears stay cleared;
                // the Still opens behind it, replayable like any earlier stage).
                if (s.HighestClearedStage >= StillInsertedAt) s.HighestClearedStage += StillStageCount;
                s.Version = 5;
            }
            if (s.Version < 6)
            {
                // GG-T345: The Lantern Road (three stages) was inserted between the Tithe Moors and the Shardfall, at campaign
                // index LanternRoadInsertedAt. Same rule as v5: a save already past the Tithe Moors moves up by its stage
                // count and keeps the same frontier; the Lantern Road opens behind it, replayable like any earlier stage.
                if (s.HighestClearedStage >= LanternRoadInsertedAt) s.HighestClearedStage += LanternRoadStageCount;
                s.Version = 6;
            }
            if (s.Version < 7)
            {
                // GG-T350: The Choir's Cathedral (three stages) was inserted after Halcyon as the new finale, at campaign
                // index CathedralInsertedAt. Same rule as v5/v6: a save already into the postgame moves up by its stage
                // count and keeps the same frontier. A save whose furthest clear is Halcyon III finds the cathedral next.
                if (s.HighestClearedStage >= CathedralInsertedAt) s.HighestClearedStage += CathedralStageCount;
                s.Version = 7;
            }
            if (s.Version < 8)
            {
                // GG-T347: The Bloom (three stages) was inserted after the Cathedral as the first postgame chapter, at campaign
                // index BloomInsertedAt. Same rule as v5-v7: a save already into the old postgame moves up by its stage count
                // and keeps the same frontier. A save whose furthest clear is the Cathedral III finds the Bloom next.
                if (s.HighestClearedStage >= BloomInsertedAt) s.HighestClearedStage += BloomStageCount;
                s.Version = 8;
            }
            if (s.Version < 9)
            {
                // GG-T357: the five older postgame chapters were put in story order and onto a rising level curve, which
                // moved six of their ten stages (no stage was added or removed). HighestClearedStage is a single frontier,
                // so it becomes the furthest new index among everything the save had cleared: its furthest clear is
                // still cleared, and any chapter that moved behind it opens there, replayable like any earlier stage.
                s.HighestClearedStage = ReorderedPostgameFrontier(s.HighestClearedStage);
                s.Version = 9;
            }
            if (s.Version < 10)
            {
                // GG-T432: crafting stations gained gold-bought facility levels that gate material tiers (§25.12.2). A save
                // from before keeps roughly what its station skill had reached: Journeyman -> the first level, Master -> the second.
                s.Caravan ??= new Dictionary<string, int>();
                s.StationXp ??= new Dictionary<string, int>();
                Facilities.Grandfather(s);
                s.Version = 10;
            }
            if (s.Version < 11)
            {
                // GG-T469: the purchase ledger. No older save bought anything with real money (release builds had no store
                // before GG-T468), so it starts empty.
                s.Purchases = new PurchaseSave();
                s.Version = 11;
            }
            // GG-T537: Camp vendors now join on stage clears. A save from before (v11 or older) already past a vendor's stage
            // takes that beat as told (below, once content is at hand), so it doesn't get a run of join scenes at once.
            bool beforeStoryVendors = s.Version < 12;
            if (s.Version < 12) s.Version = 12;
            // GG-T538: vendor stock and recipe lists grow with progress and station rank. A save from before keeps what it
            // owns on the shelf and in the lists (VendorTiers.Grandfather, below, once content is at hand).
            bool beforeVendorTiers = s.Version < 13;
            if (s.Version < 13) s.Version = 13;
            if (s.Version < 14)
            {
                // GG-T532: heroes have a female or male version now. Everyone already in an older save is the female art they
                // have always been, and nobody is asked again (a hero who joins from here on is asked when they join).
                foreach (var h in s.Roster)
                {
                    if (h == null) continue;
                    h.Variant = HeroVariant.Female;
                    h.VariantPending = false;
                }
                s.Version = 14;
            }
            if (s.Version < 15)
            {
                // GG-T773: saves can lock a turn mode (§2.13). Every older save was played under PartyWindow, the only rule
                // there was, and stays unlocked: it follows the profile setting, whose default is PartyWindow.
                s.TurnModeLock = null;
                s.Version = 15;
            }
            // GG-T923: the Codex. A save from before back-fills every entry it had already reached (below, after Notes.Clear),
            // taken as read so a returning player isn't handed a wall of "new" badges.
            bool beforeCodex = s.Version < 16;
            if (s.Version < 16) s.Version = 16;
            s.Codex ??= new HashSet<string>();
            s.CodexRead ??= new HashSet<string>();
            // GG-T973: merged abilities (abilities.json MergedInto aliases) became their survivors. v17 marks a save written
            // once aliases existed; the rewrite itself runs on every load (below, once content is at hand), since each later
            // merge adds an alias without a version bump.
            if (s.Version < 17) s.Version = 17;
            if (s.Version < 18)
            {
                // GG-T1065: Stamina is gone (no Stats field, no StatGrantDef name). Points a hero put into it come back as
                // unspent: StatPoints.Spent sums every key, so dropping the key refunds them.
                foreach (var h in s.Roster)
                    h?.StatPicks?.Remove("Stamina");
                s.Version = 18;
            }
            // GG-T1033: the skill hexagon replaces talent trees. A save from before stands each hero on the board (below, once
            // content is at hand, after aliases and cut classes): its Start path plus the cheapest route to every ability it
            // knows, free, and every talent point it spent comes back as SP.
            bool beforeBoard = s.Version < 19;
            if (s.Version < 19) s.Version = 19;
            // GG-T1292: Trust and cohesion (§25.20). An older save has neither, so every hero and pair starts at 0.
            if (s.Version < 20) s.Version = 20;
            if (s.Version < 21)
            {
                // GG-T1342: the Commander's name. An older save takes its profile's name, else the plain title.
                if (string.IsNullOrWhiteSpace(s.CommanderName))
                    s.CommanderName = string.IsNullOrWhiteSpace(profileName) ? Commander.Fallback : profileName.Trim();
                s.Version = 21;
            }
            s.MetHeroes ??= new HashSet<string>();
            if (s.Version < 22)
            {
                // GG-T1592: heroes met by the story. Everyone already in an older save counts as met, so no Joined scene plays.
                foreach (var h in s.Roster)
                    if (Bonds.Key(h?.ClassId) is string key) s.MetHeroes.Add(key);
                s.Version = 22;
            }
            // GG-T1419: the summon record. An older save has made no banner pulls.
            if (s.Version < 23) s.Version = 23;
            s.Summon ??= new SummonState();
            s.Summon.Pulls ??= new Dictionary<BannerKind, int>();
            s.Summon.NewHeroMiss ??= new Dictionary<BannerKind, int>();
            s.Summon.History ??= new List<PullRecord>();
            // GG-T1626: drill progress. An older save that finished the tutorial has, in effect, cleared every drill.
            bool beforeDrills = s.Version < 24;
            if (s.Version < 24) s.Version = 24;
            s.ClearedDrills ??= new HashSet<string>();
            // GG-T1392: the onboarding checkpoint. An older save is past onboarding.
            if (s.Version < 25)
            {
                Onboarding.Finish(s);
                s.Version = 25;
            }
            s.Onboarding ??= new OnboardingSave { Step = OnboardingStep.Done };
            // GG-T1778: the overworld. An older save is placed at its frontier (below, after Notes.Clear).
            bool beforeWorld = s.Version < 26;
            if (s.Version < 26) s.Version = 26;
            s.World ??= new WorldSave();
            s.World.FillNulls();
            // GG-T1790: travel. Before it the caravan was only ever away on an expedition (drained below), so an older save
            // (a v26 one may hold the old Camped=false default) is at camp.
            if (s.Version < 27)
            {
                s.World.Camped = true;
                s.Version = 27;
            }
            // GG-T1811: Mana Orbs retired as progression. An older save's owned orbs and carried Broken Mana Orbs are paid
            // out once (below, after Notes.Clear).
            bool beforeOrbRetire = s.Version < 28;
            if (s.Version < 28) s.Version = 28;
            s.Trust ??= new Dictionary<string, int>();
            s.Cohesion ??= new Dictionary<string, int>();
            s.TrustTierReached ??= new Dictionary<string, int>();
            s.CohesionTierReached ??= new Dictionary<string, int>();
            s.BondChoices ??= new HashSet<string>();
            // Any version: a lock the enum doesn't know (a hand-edited save) reads as unlocked.
            if (s.TurnModeLock is TurnMode lockMode && !Enum.IsDefined(typeof(TurnMode), lockMode)) s.TurnModeLock = null;
            // Any version: a variant the enum doesn't know (a hand-edited or future save) reads as the female default.
            foreach (var h in s.Roster)
                if (h != null && h.Variant != HeroVariant.Female && h.Variant != HeroVariant.Male) h.Variant = HeroVariant.Female;
            s.ShopBought ??= new HashSet<string>();
            s.CraftedRecipes ??= new HashSet<string>();
            s.Purchases ??= new PurchaseSave();
            s.Purchases.Granted ??= new Dictionary<string, PurchaseRecord>(StringComparer.Ordinal);
            foreach (var h in s.Roster)
            {
                h.Armor ??= new Dictionary<EquipmentSlot, EquippedArmor>();
                h.Jewelry ??= new Dictionary<EquipmentSlot, string>();
            }
            // GG-T52 (gear-proficiency gating): a save written before this shipped may already have a hero
            // wearing gear their class isn't nominally trained for (there was no gate to stop them). Rather
            // than strip currently-equipped gear out from under a returning player, auto-grant the matching
            // proficiency-unlock passive so Equipment.Fits keeps considering them proficient. Re-running this
            // on an already-fine save is a no-op (every check is already satisfied). No-op entirely without a
            // ContentDb (c == null), e.g. the version-only unit tests above that never touch gear/content.
            // Only for saves from before the gate (beforeGearGate): on a newer save it would re-grant, free, a proficiency
            // the Inn's Respec just refunded while the gear stayed on.
            Notes.Clear();
            if (c != null)
            {
                int merged = ResolveAbilityAliases(s, c.AbilityAliases);
                if (merged > 0) Notes.Add($"{merged} merged ability id(s) rewritten to the abilities they became (GG-T973)");
            }
            if (c != null) DropRetiredHeroes(s, c);
            if (c != null) ConvertCutHeroes(s, c);
            ReleaseLead(s);
            if (c != null && beforeBoard) PlaceOnBoard(s, c);
            if (c != null) EnsureVendors(s, c, beforeStoryVendors);
            JoinNewKeepers(s);
            if (c != null && beforeOrbRetire) RetireOwnedOrbs(s, c);
            if (c != null && beforeVendorTiers) VendorTiers.Grandfather(c, s);
            if (c != null && beforeDrills && s.TutorialDone) Drills.MarkAllCleared(c, s);
            if (c != null && beforeWorld) WorldSaveAdapter.PlaceAtFrontier(s, c);
            DrainExpedition(s); // after PlaceAtFrontier, which camps the caravan
            // GG-T923: every load catches the Codex up with what the save shows (idempotent); only the v16 back-fill marks it read.
            Codex.Sync(c, s, markRead: beforeCodex);
            if (c != null && beforeGearGate)
                foreach (var h in s.Roster)
                {
                    if (h == null) continue;
                    GrandfatherGearProficiency(c, h, h.WeaponId);
                    foreach (var piece in h.Armor.Values) GrandfatherGearProficiency(c, h, piece.ItemId);
                }
            return s;
        }

        /// <summary>What the last <see cref="Migrate"/> did that the player should hear about (e.g. a retired hero leaving,
        /// GG-T531). Core/Meta can't log, so the save store logs these after a load.</summary>
        public static readonly List<string> Notes = new List<string>();

        /// <summary>GG-T531: a hero whose class no longer exists (the Echo was cut; the Aetherist, GG-T570, became a Camp
        /// NPC) can't be built into a battle, so it leaves the roster and the party. Its gear goes back to the bag with its
        /// wear (as the v3 armor split did, nobody's gear vanishes); its levels and skill points can't move to a class it
        /// never was. Idempotent: a clean roster is untouched. Never throws: a hero it can't clear is still removed.</summary>
        public static void DropRetiredHeroes(SaveData s, ContentDb c)
        {
            if (s?.Roster == null || c == null) return;
            for (int i = s.Roster.Count - 1; i >= 0; i--)
            {
                var h = s.Roster[i];
                if (h == null) { s.Roster.RemoveAt(i); continue; }
                if (h.ClassId != null && c.TryClass(h.ClassId) != null) continue;
                ReturnGear(s, c, h, "retired hero");
                s.Roster.RemoveAt(i);
                s.Party?.RemoveAll(id => string.Equals(id, h.ClassId, StringComparison.OrdinalIgnoreCase));
                Notes.Add($"hero class '{h.ClassId ?? "(none)"}' no longer exists; removed from the roster, gear returned to the bag");
            }
        }

        /// <summary>Takes every piece a leaving hero wears off into the bag, with its wear (GG-T531, GG-T1064). Never throws.</summary>
        private static void ReturnGear(SaveData s, ContentDb c, HeroSave h, string who)
        {
            try
            {
                h.Armor ??= new Dictionary<EquipmentSlot, EquippedArmor>();
                h.Jewelry ??= new Dictionary<EquipmentSlot, string>();
                var slots = new List<EquipmentSlot> { EquipmentSlot.Weapon };
                slots.AddRange(h.Armor.Keys);
                slots.AddRange(h.Jewelry.Keys);
                foreach (var slot in slots)
                {
                    try { Equipment.Unequip(c, s, h, slot); }
                    catch (Exception e) { Notes.Add($"{who} '{h.ClassId}': couldn't return {slot} gear ({e.Message})"); }
                }
            }
            catch (Exception e) { Notes.Add($"{who} '{h.ClassId}': couldn't clear its gear ({e.Message})"); }
        }

        /// <summary>GG-T1064: a hero of a class cut from the roster (<see cref="ClassDef.IsCut"/>, GG-T1037) becomes its
        /// <see cref="ClassDef.CutInto"/> hero. When the save doesn't own that hero yet, the cut hero's place in the roster
        /// and party goes to a new one at the same experience: trained to its level, every skill point it has earned
        /// unspent (the old class's abilities and board don't carry over), the same look. When it does, the cut hero's
        /// experience folds into it (with the skill points those levels bring) and the cut hero leaves. Either way its gear
        /// goes back to the bag with its wear, and the party, Permadeath and Codex follow the new id (the cut class keeps
        /// its Codex entry). Idempotent: a save with no cut hero is untouched. Never throws.</summary>
        public static void ConvertCutHeroes(SaveData s, ContentDb c)
        {
            if (s?.Roster == null || c == null) return;
            for (int i = s.Roster.Count - 1; i >= 0; i--)
            {
                var h = s.Roster[i];
                var def = h?.ClassId == null ? null : c.TryClass(h.ClassId);
                if (def == null || !def.IsCut) continue;
                var into = c.TryClass(def.CutInto);
                if (into == null || into.Team != Team.Hero || into.IsCut)
                {
                    Notes.Add($"cut hero '{h.ClassId}': CutInto '{def.CutInto}' is not a hero in play; left as it is");
                    continue;
                }
                string from = h.ClassId, to = into.Id;
                try
                {
                    ReturnGear(s, c, h, "cut hero");
                    var owned = s.Hero(to);
                    if (owned != null)
                    {
                        int before = Progression.Level(owned);
                        owned.TotalXp += Math.Max(0, h.TotalXp);
                        owned.SkillPoints += Formulas.SkillPointsForLevelUp(before, Progression.Level(owned));
                        s.Roster.RemoveAt(i);
                        Notes.Add($"cut hero '{from}' folded into {into.Name} (experience kept, gear returned to the bag, GG-T1064)");
                    }
                    else
                    {
                        var nh = Progression.NewHero(c, to);
                        nh.TotalXp = Math.Max(0, h.TotalXp);
                        nh.SkillPoints += Formulas.SkillPointsForLevelUp(1, Progression.Level(nh));
                        nh.Variant = h.Variant;
                        nh.VariantPending = h.VariantPending;
                        Progression.TrainTo(c, nh);
                        s.Roster[i] = nh;
                        Codex.UnlockHero(s, nh);
                        Notes.Add($"cut hero '{from}' became {into.Name} (experience kept, gear returned to the bag, GG-T1064)");
                    }
                }
                catch (Exception e)
                {
                    Notes.Add($"couldn't convert cut hero '{from}': {e.Message}");
                    continue;
                }
                if (s.Party != null)
                {
                    bool inParty = s.Party.Exists(id => string.Equals(id, to, StringComparison.OrdinalIgnoreCase));
                    for (int p = s.Party.Count - 1; p >= 0; p--)
                    {
                        if (!string.Equals(s.Party[p], from, StringComparison.OrdinalIgnoreCase)) continue;
                        if (inParty) s.Party.RemoveAt(p);
                        else { s.Party[p] = to; inParty = true; }
                    }
                }
                if (s.LostGuests != null && s.LostGuests.Remove(from)) s.LostGuests.Add(to);
            }
        }

        /// <summary>GG-T1081: points a pre-hexagon hero paid for its talent-tree nodes: the recorded price per
        /// <see cref="HeroSave.TreeNodeCosts"/> key, else the node's own cost plus, in a tree that is not the hero's home tree, the flat off-tree premium
        /// (<see cref="LegacyOffTreePremium"/>; the per-step extra the adjacency board added is not recomputed). GG-T1092: node costs and
        /// home trees come from the embedded legacy tables below (frozen from skilltrees.json as it stood when talent trees were retired),
        /// so the refund doesn't depend on <see cref="ContentDb.SkillTrees"/>; a "tree/node" key not in the table refunds nothing, as before.</summary>
        private const int LegacyOffTreePremium = 1;

        private static int TalentPointsPaid(HeroSave h)
        {
            if (h?.TreeNodes == null) return 0;
            int total = 0;
            string home = LegacyHomeTree(h.ClassId);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in h.TreeNodes)
            {
                if (key == null || !seen.Add(key) || !LegacyTalentNodeCosts.TryGetValue(key, out int cost)) continue;
                if (h.TreeNodeCosts != null && h.TreeNodeCosts.TryGetValue(key, out var paid) && paid >= 0) { total += paid; continue; }
                string tree = key.Substring(0, key.IndexOf('/'));
                total += cost + (home != null && string.Equals(tree, home, StringComparison.Ordinal) ? 0 : LegacyOffTreePremium);
            }
            return total;
        }

        /// <summary>GG-T1092: a retired talent-tree node's cost by its "tree/node" key, or null when no such node existed.</summary>
        public static int? LegacyTalentNodeCost(string key) => key != null && LegacyTalentNodeCosts.TryGetValue(key, out int c) ? c : (int?)null;

        /// <summary>GG-T1092: the retired talent tree that was a class's home tree (its family tree, else its HomeTree), or null.</summary>
        public static string LegacyHomeTree(string classId) => classId != null && LegacyHomeTrees.TryGetValue(classId, out var t) ? t : null;

        /// <summary>GG-T1092: every "tree/node" key of the retired skilltrees.json and its cost (generated, do not edit).</summary>
        private static readonly Dictionary<string, int> LegacyTalentNodeCosts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["guardian/guardian_shield_wall"] = 1,
            ["guardian/guardian_iron_will"] = 1,
            ["guardian/aegisguard_opener"] = 1,
            ["guardian/aegisguard_stat1"] = 1,
            ["guardian/aegisguard_core"] = 2,
            ["guardian/aegisguard_stat2"] = 1,
            ["guardian/aegisguard_capstone"] = 2,
            ["guardian/juggernaut_opener"] = 1,
            ["guardian/juggernaut_stat1"] = 1,
            ["guardian/juggernaut_core"] = 2,
            ["guardian/juggernaut_stat2"] = 1,
            ["guardian/juggernaut_capstone"] = 2,
            ["guardian/warlord_opener"] = 1,
            ["guardian/warlord_stat1"] = 1,
            ["guardian/warlord_core"] = 2,
            ["guardian/warlord_stat2"] = 1,
            ["guardian/warlord_capstone"] = 2,
            ["guardian/deathguard_opener"] = 1,
            ["guardian/deathguard_stat1"] = 1,
            ["guardian/deathguard_core"] = 2,
            ["guardian/deathguard_stat2"] = 1,
            ["guardian/deathguard_capstone"] = 2,
            ["guardian/guardian_finale"] = 3,
            ["warrior/warrior_cleaving_strikes"] = 1,
            ["warrior/warrior_battle_fury"] = 1,
            ["warrior/berserker_opener"] = 1,
            ["warrior/berserker_stat1"] = 1,
            ["warrior/berserker_core"] = 2,
            ["warrior/berserker_stat2"] = 1,
            ["warrior/berserker_capstone"] = 2,
            ["warrior/duelist_opener"] = 1,
            ["warrior/duelist_stat1"] = 1,
            ["warrior/duelist_core"] = 2,
            ["warrior/duelist_stat2"] = 1,
            ["warrior/duelist_capstone"] = 2,
            ["warrior/brawler_opener"] = 1,
            ["warrior/brawler_stat1"] = 1,
            ["warrior/brawler_core"] = 2,
            ["warrior/brawler_stat2"] = 1,
            ["warrior/brawler_capstone"] = 2,
            ["warrior/attrition_opener"] = 1,
            ["warrior/attrition_stat1"] = 1,
            ["warrior/attrition_core"] = 2,
            ["warrior/attrition_stat2"] = 1,
            ["warrior/attrition_capstone"] = 2,
            ["warrior/warrior_finale"] = 3,
            ["rogue/rogue_light_fingers"] = 1,
            ["rogue/rogue_feint"] = 1,
            ["rogue/shadowstep_opener"] = 1,
            ["rogue/shadowstep_stat1"] = 1,
            ["rogue/shadowstep_core"] = 2,
            ["rogue/shadowstep_stat2"] = 1,
            ["rogue/shadowstep_capstone"] = 2,
            ["rogue/rogue_skirmisher_opener"] = 1,
            ["rogue/rogue_skirmisher_stat1"] = 1,
            ["rogue/rogue_skirmisher_core"] = 2,
            ["rogue/rogue_skirmisher_stat2"] = 1,
            ["rogue/rogue_skirmisher_capstone"] = 2,
            ["rogue/combobrawler_opener"] = 1,
            ["rogue/combobrawler_stat1"] = 1,
            ["rogue/combobrawler_core"] = 2,
            ["rogue/combobrawler_stat2"] = 1,
            ["rogue/combobrawler_capstone"] = 2,
            ["rogue/opportunist_opener"] = 1,
            ["rogue/opportunist_stat1"] = 1,
            ["rogue/opportunist_core"] = 2,
            ["rogue/opportunist_stat2"] = 1,
            ["rogue/opportunist_capstone"] = 2,
            ["rogue/rogue_finale"] = 3,
            ["ranger/ranger_focused_aim"] = 1,
            ["ranger/ranger_covering_fire"] = 1,
            ["ranger/marksman_opener"] = 1,
            ["ranger/marksman_stat1"] = 1,
            ["ranger/marksman_core"] = 2,
            ["ranger/marksman_capstone"] = 2,
            ["ranger/trapper_opener"] = 1,
            ["ranger/trapper_stat1"] = 1,
            ["ranger/trapper_core"] = 2,
            ["ranger/trapper_capstone"] = 2,
            ["ranger/skirmisher_opener"] = 1,
            ["ranger/skirmisher_stat1"] = 1,
            ["ranger/skirmisher_core"] = 2,
            ["ranger/skirmisher_capstone"] = 2,
            ["ranger/ranger_finale"] = 3,
            ["mage/mage_arcane_bolt"] = 1,
            ["mage/mage_chilling_frost"] = 1,
            ["mage/spellblade_opener"] = 1,
            ["mage/spellblade_stat1"] = 1,
            ["mage/spellblade_core"] = 2,
            ["mage/spellblade_stat2"] = 1,
            ["mage/spellblade_capstone"] = 2,
            ["mage/fieldcaster_opener"] = 1,
            ["mage/fieldcaster_stat1"] = 1,
            ["mage/fieldcaster_core"] = 2,
            ["mage/fieldcaster_stat2"] = 1,
            ["mage/fieldcaster_capstone"] = 2,
            ["mage/hexweaver_opener"] = 1,
            ["mage/hexweaver_stat1"] = 1,
            ["mage/hexweaver_core"] = 2,
            ["mage/hexweaver_stat2"] = 1,
            ["mage/hexweaver_capstone"] = 2,
            ["mage/warden_opener"] = 1,
            ["mage/warden_stat1"] = 1,
            ["mage/warden_core"] = 2,
            ["mage/warden_stat2"] = 1,
            ["mage/warden_capstone"] = 2,
            ["mage/mage_finale"] = 3,
            ["mage/mage_heavy_armor_proficiency"] = 2,
            ["cleric/cleric_mending_light"] = 1,
            ["cleric/cleric_sacred_cleanse"] = 1,
            ["cleric/aegiswardens_opener"] = 1,
            ["cleric/aegiswardens_stat1"] = 1,
            ["cleric/aegiswardens_core"] = 2,
            ["cleric/aegiswardens_stat2"] = 1,
            ["cleric/aegiswardens_capstone"] = 2,
            ["cleric/hexmenders_opener"] = 1,
            ["cleric/hexmenders_stat1"] = 1,
            ["cleric/hexmenders_core"] = 2,
            ["cleric/hexmenders_stat2"] = 1,
            ["cleric/hexmenders_capstone"] = 2,
            ["cleric/skirmishmenders_opener"] = 1,
            ["cleric/skirmishmenders_stat1"] = 1,
            ["cleric/skirmishmenders_core"] = 2,
            ["cleric/skirmishmenders_stat2"] = 1,
            ["cleric/skirmishmenders_capstone"] = 2,
            ["cleric/cleric_finale"] = 3,
            ["cleric/cleric_martial_weapons_proficiency"] = 2,
            ["alchemist/alchemist_quickening_brew"] = 1,
            ["alchemist/alchemist_binding_toxin"] = 1,
            ["alchemist/tempoweave_opener"] = 1,
            ["alchemist/tempoweave_core"] = 2,
            ["alchemist/tempoweave_stat2"] = 1,
            ["alchemist/tempoweave_capstone"] = 2,
            ["alchemist/fieldeng_opener"] = 1,
            ["alchemist/fieldeng_core"] = 2,
            ["alchemist/fieldeng_stat2"] = 1,
            ["alchemist/fieldeng_capstone"] = 2,
            ["alchemist/primalpack_opener"] = 1,
            ["alchemist/primalpack_core"] = 2,
            ["alchemist/primalpack_stat2"] = 1,
            ["alchemist/primalpack_capstone"] = 2,
            ["alchemist/alchemist_finale"] = 3,
            ["alchemist/alchemist_arcane_focus_proficiency"] = 2,
        };

        /// <summary>GG-T1092: each class's home tree id in the retired skilltrees.json (generated, do not edit).</summary>
        private static readonly Dictionary<string, string> LegacyHomeTrees = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["alchemist"] = "cleric",
            ["assassain"] = "warrior",
            ["barbarian"] = "warrior",
            ["basher"] = "warrior",
            ["blackninja"] = "rogue",
            ["blackwitch"] = "mage",
            ["bluelion"] = "alchemist",
            ["blueninja"] = "rogue",
            ["bruiser"] = "warrior",
            ["chromaninja"] = "rogue",
            ["cleric"] = "cleric",
            ["courier"] = "alchemist",
            ["darktemplar"] = "guardian",
            ["defender"] = "guardian",
            ["dervish"] = "alchemist",
            ["doctor"] = "cleric",
            ["drifter"] = "mage",
            ["duelist"] = "warrior",
            ["engineer"] = "alchemist",
            ["fencer"] = "warrior",
            ["fighter"] = "rogue",
            ["greenninja"] = "rogue",
            ["harbinger"] = "mage",
            ["jadeknight"] = "guardian",
            ["knight"] = "guardian",
            ["lancer"] = "rogue",
            ["machinist"] = "alchemist",
            ["martialartist"] = "rogue",
            ["monk"] = "cleric",
            ["myrmidon"] = "guardian",
            ["nighthunter"] = "rogue",
            ["odachi"] = "warrior",
            ["operative"] = "mage",
            ["paladin"] = "guardian",
            ["pandagirl"] = "warrior",
            ["prizefighter"] = "rogue",
            ["pugilist"] = "rogue",
            ["raider"] = "rogue",
            ["redmage"] = "mage",
            ["redninja"] = "rogue",
            ["ripper"] = "rogue",
            ["ritualist"] = "mage",
            ["ronin"] = "warrior",
            ["sage"] = "cleric",
            ["sellsword"] = "warrior",
            ["shieldmaiden"] = "guardian",
            ["sister"] = "cleric",
            ["slasher"] = "warrior",
            ["speedster"] = "rogue",
            ["streetfighter"] = "rogue",
            ["striker"] = "rogue",
            ["swordmaster"] = "warrior",
            ["tank"] = "guardian",
            ["technician"] = "alchemist",
            ["thief"] = "warrior",
            ["tinkerer"] = "alchemist",
            ["vampire"] = "warrior",
            ["warchief"] = "guardian",
            ["whiteninja"] = "cleric",
            ["whitewitch"] = "cleric",
            ["wildchild"] = "alchemist",
            ["yellowninja"] = "rogue",
            ["hexsniper"] = "ranger",
            ["hexwarden"] = "ranger",
            ["hexstalker"] = "ranger",
            ["monstertamer"] = "alchemist",
            ["herald"] = "cleric",
            ["tactician"] = "alchemist",
            ["warden"] = "guardian",
        };

        /// <summary>GG-T1033 (docs/design/skill-hexagon/PLAN.md, Saves): every hero of a save from before the skill hexagon
        /// takes its place on the board. Talent-tree nodes it bought are cleared and their points (TalentPointsPaid,
        /// what was paid) come back as unspent <see cref="HeroSave.SkillPoints"/>; it keeps every ability it knows. Its
        /// <see cref="HeroSave.HexNodes"/> become its class Start path (when it owns none yet) plus, free, the cheapest route
        /// (<see cref="SkillHexBoard.CheapestRoute"/>) to every known ability on the board, nearest first, each route
        /// preferring to pass through the other wanted abilities. Owning an ability node teaches it, as buying does. An
        /// ability off the board stays known with no node; a hero whose class has no Start path (a domesticated monster) only
        /// gets its refund. Run after alias rewrites and cut-class conversion so the ids are final. Never throws.</summary>
        public static void PlaceOnBoard(SaveData s, ContentDb c)
        {
            if (s?.Roster == null || c == null) return;
            var board = HeroBoard.Board(c);
            foreach (var h in s.Roster)
            {
                if (h == null) continue;
                try
                {
                    h.Known ??= new List<string>();
                    if (h.TreeNodes != null && h.TreeNodes.Count > 0)
                    {
                        int refund = TalentPointsPaid(h);
                        h.TreeNodes.Clear();
                        h.SkillPoints += refund;
                        if (refund > 0) Notes.Add($"{h.ClassId}: {refund} talent point(s) returned as skill points (GG-T1033)");
                    }
                    h.TreeNodeCosts?.Clear();
                    if (board == null) continue;
                    h.HexNodes ??= new List<string>();
                    var owned = new HashSet<(int Q, int R)>(SkillHexProgress.Owned(h).Nodes);
                    if (owned.Count == 0) owned.UnionWith(board.StartOf(h.ClassId));
                    if (owned.Count == 0) continue;
                    var wanted = new HashSet<(int Q, int R)>();
                    foreach (var id in h.Known)
                    {
                        var at = board.FindAbility(id);
                        if (at.HasValue && !owned.Contains(at.Value)) wanted.Add(at.Value);
                    }
                    while (wanted.Count > 0)
                    {
                        List<(int Q, int R)> best = null;
                        int bestCost = int.MaxValue;
                        (int Q, int R) bestTarget = default;
                        var unreachable = new List<(int Q, int R)>();
                        foreach (var target in wanted)
                        {
                            var route = board.CheapestRoute(owned, target, wanted);
                            if (route == null) { unreachable.Add(target); continue; }
                            int cost = board.SpCost(route, owned);
                            if (cost < bestCost || (cost == bestCost && target.CompareTo(bestTarget) < 0))
                            {
                                best = route; bestCost = cost; bestTarget = target;
                            }
                        }
                        foreach (var u in unreachable)
                        {
                            wanted.Remove(u);
                            Notes.Add($"{h.ClassId}: couldn't route to its known ability '{board.Node(u)?.Ability}' on the skill hexagon (GG-T1033)");
                        }
                        if (best == null) break;
                        foreach (var pt in best) owned.Add(pt);
                        wanted.ExceptWith(owned);
                    }
                    var keys = new List<(int Q, int R)>(owned);
                    keys.Sort();
                    foreach (var pt in keys)
                    {
                        var key = SkillHexProgress.Key(pt);
                        if (!h.HexNodes.Contains(key)) h.HexNodes.Add(key);
                        var n = board.Node(pt);
                        if (n != null && n.Kind == SkillHexNodeKind.Ability && !string.IsNullOrEmpty(n.Ability) && !h.Known.Contains(n.Ability))
                            h.Known.Add(n.Ability);
                    }
                }
                catch (Exception e) { Notes.Add($"couldn't place '{h.ClassId}' on the skill hexagon: {e.Message}"); }
            }
        }

        /// <summary>GG-T1811: Mana Orbs are no longer progression. A save from before (v27 or older) is paid once for every
        /// owned orb beyond the starting one and every Broken Mana Orb it carries (<see cref="ManaOrbs.RetirePayout"/>).
        /// Runs only on that version step; never throws.</summary>
        public static void RetireOwnedOrbs(SaveData s, ContentDb c)
        {
            if (s == null || c == null) return;
            try
            {
                ManaOrbs.RetirePayout(c, s, out int shards, out int dust);
                if (shards > 0 || dust > 0)
                    Notes.Add($"Mana Orbs retired: this save was paid {ShardText.Of(shards)} and {dust:N0} {DustEconomy.DustName} for its orbs (GG-T1811)");
            }
            catch (Exception e) { Notes.Add($"couldn't pay out Mana Orbs: {e.Message}"); }
        }

        /// <summary>GG-T570: a save that had already met every Camp keeper before a new one existed (the Aetherist) is a
        /// veteran Camp (<see cref="CampUnlocks"/>): it meets the newcomer on load too, so nothing it had open closes. A save
        /// past the newcomer's join stage gets them from <see cref="EnsureVendors"/> anyway. Idempotent; never throws.</summary>
        public static void JoinNewKeepers(SaveData s)
        {
            if (s == null) return;
            try
            {
                s.Flags ??= new HashSet<string>();
                if (CampNpcs.Joined(s, CampNpcs.Aetherist)) return;
                foreach (var npc in CampNpcs.All)
                    if (npc != CampNpcs.Aetherist && !CampNpcs.Joined(s, npc)) return;
                s.Flags.Add(ContentLoader.NpcFlagPrefix + CampNpcs.Aetherist);
                Notes.Add("the Manasmith joined (the save had met every other Camp keeper, GG-T570)");
            }
            catch (Exception e) { Notes.Add($"couldn't bring the Manasmith: {e.Message}"); }
        }

        /// <summary>GG-T1405: the fixed lead is gone. A save made under GG-T533 keeps its Warden in the roster as an ordinary
        /// hero who can now sit in the reserve, and nothing holds slot 0 for them any more. This only tidies the party the
        /// old lead rule shuffled: each member once, each one a roster hero, no more than the cap (extras step back to the
        /// reserve; no one leaves the roster). Idempotent; never throws.</summary>
        public static void ReleaseLead(SaveData s)
        {
            if (s == null) return;
            try
            {
                s.Roster ??= new List<HeroSave>();
                s.Party ??= new List<string>();
                var party = new List<string>();
                foreach (var id in s.Party)
                    if (id != null && s.Hero(id) != null && !party.Exists(p => string.Equals(p, id, StringComparison.OrdinalIgnoreCase))) party.Add(id);
                if (party.Count > Progression.MaxPartySize) party.RemoveRange(Progression.MaxPartySize, party.Count - Progression.MaxPartySize);
                if (party.Count == s.Party.Count) return; // only ever drops members, so the same count is the same party
                s.Party.Clear();
                s.Party.AddRange(party);
                Notes.Add("the party no longer holds a slot for a fixed lead (GG-T1405)");
            }
            catch (Exception e) { Notes.Add($"couldn't tidy the party: {e.Message}"); }
        }

        /// <summary>GG-T537: every Camp vendor whose story beat's stage this save has cleared is with the caravan: a save past
        /// the beat (made before vendors joined on stage clears, or one that skipped the scene) gets the vendor on load, never
        /// locked out. Nobody is ever removed: a vendor met earlier under the old triggers stays. <paramref name="markTold"/>
        /// (a save from before GG-T537) also takes every vendor beat it is past as told, so the scenes don't all play at
        /// the next Camp. Idempotent; never throws.</summary>
        public static void EnsureVendors(SaveData s, ContentDb c, bool markTold)
        {
            if (s == null || c == null) return;
            try
            {
                s.Flags ??= new HashSet<string>();
                s.SeenDialogues ??= new HashSet<string>();
                var stages = c.CampaignStages();
                foreach (var d in c.DialogueOrder)
                {
                    if (!DialogueRunner.IsVendorBeat(d)) continue;
                    string stage = d.Trigger.Id;
                    int at = stages.FindIndex(st => string.Equals(st.Id, stage, StringComparison.OrdinalIgnoreCase));
                    if (!(s.ClearedStages != null && s.ClearedStages.Contains(stage)) && !(at >= 0 && at <= s.HighestClearedStage)) continue;
                    foreach (var e in d.AllEffects())
                        if (e?.Type == DialogueEffectType.JoinNpc && s.Flags.Add(ContentLoader.NpcFlagPrefix + e.Id))
                            Notes.Add($"{DialogueRunner.Npc(e.Id)} joined (the save is past {stage}, GG-T537)");
                    if (markTold) s.SeenDialogues.Add(d.Id);
                }
            }
            catch (Exception e) { Notes.Add($"couldn't bring the Camp vendors: {e.Message}"); }
        }

        /// <summary>GG-T52: if <paramref name="itemId"/> is equipped but <paramref name="h"/> isn't nominally
        /// proficient in it, grant whichever proficiency-unlock passive would explain that (idempotent — a
        /// hero who's already proficient, or has nothing in that slot, is untouched).</summary>
        private static void GrandfatherGearProficiency(ContentDb c, HeroSave h, string itemId)
        {
            var item = itemId != null ? c.TryItem(itemId) : null;
            if (item == null) return;
            if (item.Slot == EquipmentSlot.Weapon && item.WeaponClass != WeaponClass.Simple &&
                !Progression.EffectiveWeaponProficiencies(c, h).Contains(item.WeaponClass))
            {
                var passive = item.WeaponClass == WeaponClass.Arcane ? "arcane_focus_proficiency" : "martial_weapons_proficiency";
                if (!h.Known.Contains(passive)) h.Known.Add(passive);
            }
            else if (Array.IndexOf(BattleEngine.ArmorSlots, item.Slot) >= 0 &&
                     item.ArmorWeight > Progression.EffectiveArmorProficiency(c, h))
            {
                if (!h.Known.Contains("heavy_armor_proficiency")) h.Known.Add("heavy_armor_proficiency");
            }
        }
    }
}
