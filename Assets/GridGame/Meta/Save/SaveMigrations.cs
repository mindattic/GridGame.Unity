using System;
using System.Collections.Generic;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Meta
{
    public static class SaveMigrations
    {
        /// <summary>GG-T1790: travel replaced expeditions. A save loaded mid-expedition is on the road with the orbs it
        /// carried, as <see cref="BattleConfig.ManaPointsPerOrb"/> points each (GG-T1927; wounds stay as saved); the legacy
        /// <see cref="SaveData.Expedition"/> is then cleared. Any version.</summary>
        public static void DrainExpedition(SaveData s)
        {
            if (s.Expedition == null) return;
            s.World ??= new WorldSave();
            s.World.CarriedMana = Math.Max(0, s.Expedition.CarriedOrbs) * BattleConfig.ManaPointsPerOrb;
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
            // GG-T1927 (§3.1.0): mana carried on the road is party pool points. An older save's carried whole orbs become
            // ManaPointsPerOrb points each.
            if (s.Version < 29)
            {
                s.World.CarriedMana = Math.Max(0, s.World.CarriedOrbs ?? 0) * BattleConfig.ManaPointsPerOrb;
                s.Version = 29;
            }
            s.World.CarriedOrbs = null;
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
