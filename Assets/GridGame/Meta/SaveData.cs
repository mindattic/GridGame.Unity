using System;
using System.Collections.Generic;
using GridGame.Core.Battle;
using GridGame.Core.Content;

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
        public const int CurrentVersion = 29;

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
        /// <summary>Bag pieces already shown as upgrades on Heroes › Equip, so the Heroes tab's New badge clears (GG-T2061,
        /// <see cref="AutoEquip.FirstNew"/>).</summary>
        public HashSet<string> SeenGear = new HashSet<string>();
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
}
