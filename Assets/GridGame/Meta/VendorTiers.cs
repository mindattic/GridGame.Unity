using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>
    /// GG-T538 (owner, 2026-11-21: "some are progress gated, some are training gated, the usual"): the Merchant's stock and
    /// the Blacksmith's and Alchemist's recipe lists start small and grow. Two axes, both tuned here (bible §25.0.10):
    /// <list type="bullet">
    /// <item><b>Progress</b>: <see cref="Count"/> tiers, tier N opening on the clear of <see cref="OpensAfter"/>[N-1] (a
    /// region's last stage). A Merchant item carries its tier in content (<see cref="ItemDef.ShopTier"/>). A Forge or Alchemy
    /// recipe's tier is derived (<see cref="RecipeTier"/>): the region where its materials first drop, floored by its
    /// result's rarity band and, for a set piece, the set's level band.</item>
    /// <item><b>Training</b>: the station's own rank (<see cref="StationProgress"/>, Apprentice/Journeyman/Master), raised
    /// by forges, brews, repairs and salvage; <see cref="RankXp"/> holds its thresholds. A rank gates a recipe's <c>Tier</c>
    /// and adds Masterwork chance.</item>
    /// </list>
    /// Nothing the player already has is taken away: an item they once bought stays on the shelf, a recipe they once
    /// crafted or learned stays in the list, and an older save is grandfathered on load (<see cref="Grandfather"/>).
    /// </summary>
    public static class VendorTiers
    {
        // ── Tuning ────────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Progress tier N (1-based) opens on the clear of this campaign stage; tier 1 is open from the start. Each
        /// is a region's last stage, so a region's goods arrive as the party reaches it: 2 the Lantern Road, 3 the Shardfall,
        /// 4 the Arkgrave, 5 the Anchor Wastes, 6 the Still and Halcyon, 7 the Cathedral and the Bloom, 8 the postgame.</summary>
        public static readonly string[] OpensAfter =
        {
            null, "greenvalley-03", "lanternroad-03", "desert-03", "swamp-03", "cave-03", "cityruins-03", "bloom-03",
        };

        /// <summary>The highest progress tier.</summary>
        public static int Count => OpensAfter.Length;

        /// <summary>A crafted result's rarity floors its recipe's progress tier: Junk/Common 1, Uncommon 2, Rare 3, Epic 5,
        /// Legendary 7. (The facility ladder, §25.12.2, gates the same rarities on gold; this gates them on the road.)</summary>
        public static int RarityTier(ItemRarity r)
        {
            switch (r)
            {
                case ItemRarity.Uncommon: return 2;
                case ItemRarity.Rare: return 3;
                case ItemRarity.Epic: return 5;
                case ItemRarity.Legendary: return 7;
                default: return 1;
            }
        }

        /// <summary>An armor-set piece's recipe opens no earlier than its set's level band: the highest set
        /// <see cref="ArmorSetDef.Level"/> in tier 1, 2, ... 7; anything above is tier 8.</summary>
        public static readonly int[] SetLevelCeiling = { 2, 3, 8, 11, 14, 18, 25 };

        /// <summary>Station rank thresholds (total station XP to reach Apprentice, Journeyman, Master). A forge or brew gives
        /// <see cref="Crafting.CraftXp"/>, a repair <see cref="Repair.RepairXp"/>, a salvage <see cref="Crafting.SalvageXp"/>.</summary>
        public static readonly int[] RankXp = { 0, 150, 450 };

        /// <summary>Locked rows a station previews from the next progress tier and from the next rank.</summary>
        public const int PreviewRows = 3;

        // ── Progress ──────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Stages that must be cleared to open progress tier <paramref name="tier"/> (0 for tier 1).</summary>
        public static int ClearsFor(ContentDb c, int tier) => c == null || tier <= 1 ? 0 : For(c).Clears[Math.Min(tier, Count) - 1];

        private static int[] ComputeClears(ContentDb c)
        {
            var stages = c.CampaignStages();
            var clears = new int[Count];
            for (int t = 1; t < Count; t++)
            {
                int i = stages.FindIndex(st => string.Equals(st.Id, OpensAfter[t], StringComparison.OrdinalIgnoreCase));
                clears[t] = i < 0 ? int.MaxValue : i + 1;
            }
            return clears;
        }

        /// <summary>A save that never sees the tiers: Test Mode and New Game+ (everything was open the first time through).</summary>
        public static bool Unlimited(SaveData s) => s != null && (s.TestMode || s.NewGamePlus > 0);

        /// <summary>The progress tier this save has reached (1 on a new save, <see cref="Count"/> for an unlimited one).</summary>
        public static int ProgressTier(ContentDb c, SaveData s)
        {
            if (s == null) return 1;
            if (Unlimited(s)) return Count;
            if (c == null) return 1;
            int cleared = s.HighestClearedStage + 1, tier = 1;
            for (int t = 2; t <= Count; t++) if (cleared >= ClearsFor(c, t)) tier = t;
            return tier;
        }

        /// <summary>"after The Lantern Road III": when progress tier <paramref name="tier"/> opens.</summary>
        public static string OpensHint(ContentDb c, int tier)
        {
            if (tier <= 1) return "now";
            var st = c?.TryStage(OpensAfter[Math.Min(tier, Count) - 1]);
            return "after " + (st?.Name ?? OpensAfter[Math.Min(tier, Count) - 1]);
        }

        /// <summary>The progress tier a region's goods arrive at: the last tier open by the time the party reaches the
        /// region's first stage.</summary>
        public static int RegionTier(ContentDb c, string themeId) => c == null ? Count : RegionTier(c, themeId, For(c).Clears);

        private static int RegionTier(ContentDb c, string themeId, int[] clears)
        {
            var stages = c.CampaignStages();
            var theme = c.Campaign.Themes.FirstOrDefault(t => string.Equals(t.Id, themeId, StringComparison.OrdinalIgnoreCase));
            if (theme == null || theme.Stages.Count == 0) return Count;
            int start = stages.FindIndex(st => string.Equals(st.Id, theme.Stages[0], StringComparison.OrdinalIgnoreCase));
            if (start < 0) return Count;
            int tier = 1;
            for (int t = 2; t <= Count; t++) if (clears[t - 1] <= start) tier = t;
            return tier;
        }

        // ── The Merchant ──────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>The stock tier of a vendor item (1..<see cref="Count"/>).</summary>
        public static int ShopTier(ItemDef i) => i == null ? Count : Math.Max(1, Math.Min(Count, i.ShopTier));

        /// <summary>The item is on this save's shelf: its tier is open, or the save has bought it before, or it is the
        /// Merchant's counter (§3.3.1, "there is always a way") to a wall some stage the party can reach puts up.</summary>
        public static bool OnShelf(ContentDb c, SaveData s, ItemDef i) =>
            i != null && (ShopTier(i) <= ProgressTier(c, s) || (s?.ShopBought?.Contains(i.Id) ?? false) || NeededCounter(c, s, i));

        /// <summary>GG-T371 kept whole under the tiers: every counter consumable (<see cref="ElementCounters.CountersFor"/>)
        /// of every campaign stage up to the next uncleared one is stocked whatever its tier, so no reachable fight asks for
        /// a build the party lacks without the Merchant selling the answer (The Tithe Moors I already asks for the Venom
        /// Flask, before the flasks' own tier).</summary>
        public static bool NeededCounter(ContentDb c, SaveData s, ItemDef i)
        {
            if (c == null || i == null || i.Type != ItemType.Consumable) return false;
            var upTo = For(c).CountersUpTo;
            if (upTo.Count == 0) return false;
            int reach = Math.Max(0, Math.Min(upTo.Count - 1, (s?.HighestClearedStage ?? -1) + 1));
            return upTo[reach].Contains(i.Id);
        }

        // ── Recipes ───────────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>The recipe lists that grow with progress: the Forge (and Upgrade) and the Alchemist. The Enchantress's
        /// enchants keep their own gate (the Alchemist's rank, §25.3.4).</summary>
        public static bool Gated(RecipeDef r) => r != null && r.Station != CraftStation.Enchant;

        /// <summary>The progress tier a recipe joins its station's list at (see the class summary). A recipe whose materials
        /// drop nowhere and can't be bought or made reads as the last tier, so nothing is hidden for good.</summary>
        public static int RecipeTier(ContentDb c, RecipeDef r) =>
            r == null || c == null ? Count : For(c).Recipes.TryGetValue(r.Id, out var t) ? t : Count;

        /// <summary>The earliest progress tier an item can be had at (dropped, bought or made), or 0 if never.</summary>
        public static int ItemTier(ContentDb c, string itemId) =>
            itemId != null && c != null && For(c).Items.TryGetValue(itemId, out var t) ? t : 0;

        /// <summary>The recipe is in this save's list: an Enchant, or its tier is open, or the save crafted or learned it.</summary>
        public static bool RecipeOpen(ContentDb c, SaveData s, RecipeDef r) =>
            r != null && (!Gated(r) || RecipeTier(c, r) <= ProgressTier(c, s)
                || (s?.CraftedRecipes?.Contains(r.Id) ?? false) || (s?.DiscoveredRecipes?.Contains(r.Id) ?? false));

        /// <summary>The station's next-rank line: "Journeyman at 150 XP: 40/150, about 8 more forges or repairs", or null at Master.</summary>
        public static string NextRankLine(SaveData s, StationKind k)
        {
            int lvl = StationProgress.Level(s, k);
            if (lvl >= RankXp.Length) return null;
            int xp = StationProgress.Xp(s, k), need = RankXp[lvl] - xp;
            int crafts = (need + Crafting.CraftXp - 1) / Crafting.CraftXp;
            string what = k == StationKind.Blacksmith ? (crafts == 1 ? "forge" : "forges") + " or repairs" : crafts == 1 ? "brew" : "brews";
            return $"{StationProgress.LevelNames[lvl]} at {RankXp[lvl]} XP: {xp}/{RankXp[lvl]}, about {crafts} more {what}";
        }

        // ── Existing saves ────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>A save from before the tiers (v12 or older) keeps what it has: every vendor item it owns counts as bought
        /// (it stays on the shelf), and every Forge or Alchemy recipe whose result it owns, carried or worn, counts as crafted
        /// (it stays in the list). Tiers its progress and rank already reach need nothing: they are read live. Idempotent.</summary>
        public static void Grandfather(ContentDb c, SaveData s)
        {
            if (c == null || s == null) return;
            s.ShopBought ??= new HashSet<string>();
            s.CraftedRecipes ??= new HashSet<string>();
            var owned = new HashSet<string>(s.Inventory.Where(kv => kv.Value > 0).Select(kv => kv.Key), StringComparer.OrdinalIgnoreCase);
            foreach (var h in s.Roster)
            {
                if (h == null) continue;
                if (h.WeaponId != null) owned.Add(h.WeaponId);
                if (h.Armor != null) foreach (var p in h.Armor.Values) if (p?.ItemId != null) owned.Add(p.ItemId);
                if (h.Jewelry != null) foreach (var j in h.Jewelry.Values) if (j != null) owned.Add(j);
            }
            foreach (var id in owned)
                if (c.TryItem(id) is ItemDef i && i.SoldByVendor && i.BaseCost > 0) s.ShopBought.Add(i.Id);
            foreach (var r in c.Recipes.Values)
                if (Gated(r) && owned.Contains(r.Result)) s.CraftedRecipes.Add(r.Id);
        }

        // ── Derivation (cached per content load) ──────────────────────────────────────────────────────────────────

        private sealed class Tiers
        {
            public readonly Dictionary<string, int> Items = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, int> Recipes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            /// <summary>Stages to clear for each progress tier (index tier-1).</summary>
            public int[] Clears;
            public int ItemCount, RecipeCount;
            /// <summary>Index i: every counter consumable the campaign's stages 0..i ask for (<see cref="NeededCounter"/>).</summary>
            public readonly List<HashSet<string>> CountersUpTo = new List<HashSet<string>>();
        }

        private static readonly ConditionalWeakTable<ContentDb, Tiers> Cache = new ConditionalWeakTable<ContentDb, Tiers>();

        /// <summary>The cached derivation, redone if content was added since (a test or tool that builds content in steps).</summary>
        private static Tiers For(ContentDb c)
        {
            var t = Cache.GetValue(c, Derive);
            if (t.ItemCount == c.Items.Count && t.RecipeCount == c.Recipes.Count) return t;
            lock (Cache)
            {
                Cache.Remove(c);
                return Cache.GetValue(c, Derive);
            }
        }

        private static Tiers Derive(ContentDb c)
        {
            const int Never = int.MaxValue;
            var t = new Tiers { Clears = ComputeClears(c), ItemCount = c.Items.Count, RecipeCount = c.Recipes.Count };
            var seenCounters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var st in c.CampaignStages())
            {
                foreach (var counter in ElementCounters.CountersFor(c, st)) seenCounters.Add(counter.Id);
                t.CountersUpTo.Add(new HashSet<string>(seenCounters, StringComparer.OrdinalIgnoreCase));
            }
            var item = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            void Offer(string id, int tier) { if (id != null && (!item.TryGetValue(id, out var cur) || tier < cur)) item[id] = tier; }

            // Drops: the region of the first campaign stage whose foes (or the chests they drop) carry the item.
            foreach (var st in c.CampaignStages())
            {
                var theme = c.ThemeOf(st.Id);
                int tier = theme == null ? Count : RegionTier(c, theme.Id, t.Clears);
                foreach (var w in st.Waves)
                    foreach (var e in w.Enemies)
                        if (c.TryClass(e.Class)?.DropTable is string table) Table(c, table, tier, Offer, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            }
            // The Merchant's own tiers.
            foreach (var i in c.Items.Values)
                if (i.SoldByVendor && i.BaseCost > 0) Offer(i.Id, ShopTier(i));

            // Recipes and what they make, to a fixed point: a recipe needs every input (and its base item), and its result
            // can then be had at the recipe's tier.
            var recipes = c.Recipes.Values.Where(r => c.TryItem(r.Result) != null).ToList();
            for (int pass = 0, changed = 1; changed > 0 && pass < 64; pass++)
            {
                changed = 0;
                foreach (var r in recipes)
                {
                    int tier = Floor(c, r);
                    foreach (var ic in r.Inputs) tier = Math.Max(tier, item.TryGetValue(ic.Item, out var v) ? v : Never);
                    if (r.BaseItem != null) tier = Math.Max(tier, item.TryGetValue(r.BaseItem, out var b) ? b : Never);
                    if (!t.Recipes.TryGetValue(r.Id, out var old) || old != tier) { t.Recipes[r.Id] = tier; changed++; }
                    if (tier != Never && (!item.TryGetValue(r.Result, out var have) || tier < have)) { item[r.Result] = tier; changed++; }
                }
            }
            foreach (var id in t.Recipes.Keys.ToList()) if (t.Recipes[id] == Never || t.Recipes[id] > Count) t.Recipes[id] = Count;
            foreach (var kv in item) t.Items[kv.Key] = Math.Min(kv.Value, Count);
            return t;
        }

        /// <summary>A recipe's tier floor from what it makes: the result's rarity band, and a set piece's level band.</summary>
        private static int Floor(ContentDb c, RecipeDef r)
        {
            var res = c.TryItem(r.Result);
            int tier = res == null ? 1 : RarityTier(res.Rarity);
            if (res != null && c.TryArmorSetOf(res.Id) is ArmorSetDef set)
            {
                int band = SetLevelCeiling.Length + 1;
                for (int i = 0; i < SetLevelCeiling.Length; i++) if (set.Level <= SetLevelCeiling[i]) { band = i + 1; break; }
                tier = Math.Max(tier, band);
            }
            return tier;
        }

        private static void Table(ContentDb c, string id, int tier, Action<string, int> offer, HashSet<string> seen)
        {
            if (id == null || !seen.Add(id) || !c.DropTables.TryGetValue(id, out var table)) return;
            foreach (var e in table.Entries)
            {
                offer(e.Item, tier);
                if (c.TryItem(e.Item)?.OpensTable is string chest) Table(c, chest, tier, offer, seen);
            }
        }
    }
}
