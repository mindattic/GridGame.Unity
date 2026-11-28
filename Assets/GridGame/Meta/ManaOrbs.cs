using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;

namespace GridGame.Meta
{
    /// <summary>
    /// What became of owned Mana Orbs (GG-T1811, BIBLE §3.1.0): the owner retired them as a progression item ("RETIRE them
    /// as a progression item", GG-T1805), so nothing counts owned orbs and nothing caps a battle's mana. The former orb
    /// sources pay replacement rewards, all pending owner:
    /// <list type="bullet">
    /// <item>a <see cref="ThemeDef.MilestoneOrb"/> theme's first full clear pays <see cref="MilestoneShards"/> Mana Shards;</item>
    /// <item>a Halcyon ruin stage's find (<see cref="ThemeDef.OrbFindChance"/>) pays <see cref="HalcyonFindDust"/> Mana Dust
    /// and one piece of gear (<see cref="HalcyonGear"/>);</item>
    /// <item>Broken Mana Orbs no longer drop and the Manasmith no longer restores them; one still carried pays
    /// <see cref="BrokenOrbDust"/> Mana Dust once, through the save migration (<see cref="RetirePayout"/>).</item>
    /// </list>
    /// </summary>
    public static class ManaOrbs
    {
        /// <summary>The retired loot item: a cracked Halcyon battery. It no longer drops; the save migration cashes it in.</summary>
        public const string BrokenOrbId = "mat_broken_mana_orb";
        /// <summary>Mana Shards a milestone theme's first full clear pays in place of its orb.</summary>
        public const int MilestoneShards = 3;
        /// <summary>Mana Dust a Halcyon ruin stage's find pays in place of its orb.</summary>
        public const int HalcyonFindDust = 15;
        /// <summary>Mana Dust paid once for each Broken Mana Orb carried, and for each owned orb a save had restored from one.</summary>
        public const int BrokenOrbDust = 20;
        /// <summary>GG-T1842: how many progress tiers above the ruin's own shop shelf (<see cref="VendorTiers.RegionTier"/>) a
        /// Halcyon find's gear sits; the tier picks the rarity through <see cref="VendorTiers.RarityTier"/>.</summary>
        public const int HalcyonGearTiersAbove = 1;

        /// <summary>A won campaign stage's former orb rewards: Shards for a milestone theme just paid, and the Halcyon find's
        /// Dust. The find is rolled from <see cref="SaveData.OrbRolls"/>, so a save replays it identically; each stage's find
        /// is collected at most once (<see cref="SaveData.OrbFinds"/>).</summary>
        public static void AfterVictory(ContentDb c, SaveData s, BattleReport rep, string stageId, ThemeDef milestone)
        {
            if (c == null || s == null || rep == null) return;
            s.OrbFinds ??= new HashSet<string>();
            if (milestone != null && milestone.MilestoneOrb)
            {
                DiamondExchange.Add(s, MilestoneShards);
                rep.OrbRewards.Add($"{ShardText.Of(MilestoneShards)}: {milestone.Name} cleared");
            }

            var theme = c.ThemeOf(stageId);
            Rng rng = null;
            if (theme != null && theme.OrbFindChance > 0f && !s.OrbFinds.Contains(stageId) && (rng = Roll(s, stageId)).Chance(theme.OrbFindChance))
            {
                s.OrbFinds.Add(stageId);
                s.AddItem(DustEconomy.DustId, HalcyonFindDust);
                rep.DustEarned += HalcyonFindDust;
                var gear = HalcyonGear(c, s, theme, rng);
                if (gear != null)
                {
                    var paid = new BattleReport();
                    BattleFactory.GrantItems(c, s, paid, new Dictionary<string, int> { [gear.Id] = 1 });
                    foreach (var kv in paid.Items) { rep.Items.TryGetValue(kv.Key, out var had); rep.Items[kv.Key] = had + kv.Value; }
                    rep.Gold += paid.Gold;
                    rep.AutoSold += paid.AutoSold;
                }
                rep.OrbRewards.Add($"{HalcyonFindDust:N0} {DustEconomy.DustName}{(gear != null ? " and " + gear.Name : "")}: found in the ruins of {theme.Name}");
            }
        }

        /// <summary>GG-T1842: the Halcyon find's gear roll. The rarity is the highest one whose <see cref="VendorTiers.RarityTier"/>
        /// is open <see cref="HalcyonGearTiersAbove"/> tiers past the ruin's shop shelf; the pool is every piece of equipment of
        /// that rarity some drop table already holds that a hero in the party can wear (<see cref="Equipment.ProficiencyGate"/>),
        /// in id order, picked with the find's own roll. Null when the pool is empty.</summary>
        public static ItemDef HalcyonGear(ContentDb c, SaveData s, ThemeDef theme, Rng rng)
        {
            if (c == null || s == null || theme == null || rng == null) return null;
            int tier = Math.Min(VendorTiers.Count, VendorTiers.RegionTier(c, theme.Id) + HalcyonGearTiersAbove);
            var rarity = ((ItemRarity[])Enum.GetValues(typeof(ItemRarity))).Where(r => VendorTiers.RarityTier(r) <= tier).Max();
            var party = (s.Party ?? new List<string>()).Select(s.Hero).Where(h => h != null).ToList();
            var dropped = new HashSet<string>(c.DropTables.Values.SelectMany(t => t.Entries).Select(e => e.Item), StringComparer.OrdinalIgnoreCase);
            var pool = c.Items.Values
                .Where(i => i.Type == ItemType.Equipment && i.Rarity == rarity && dropped.Contains(i.Id)
                    && party.Any(h => Equipment.ProficiencyGate(c, h, i) == null))
                .OrderBy(i => i.Id, StringComparer.Ordinal).ToList();
            return pool.Count == 0 ? null : rng.Pick(pool);
        }

        /// <summary>The one-time payout for a save made while orbs were owned (GG-T1811 migration): every owned orb beyond
        /// the starting one is paid as the reward its source pays now (milestone orbs first, then Halcyon finds, the rest as
        /// orbs restored from Broken Mana Orbs), and every Broken Mana Orb still carried is taken and paid as Dust. Pays
        /// the Shards and Dust into the save and returns them.</summary>
        public static void RetirePayout(ContentDb c, SaveData s, out int shards, out int dust)
        {
            shards = 0; dust = 0;
            if (s == null) return;
            int beyond = Math.Max(0, s.OwnedOrbs - BattleConfig.StartOwnedOrbs);
            int milestones = 0;
            if (c?.Campaign?.Themes != null && s.Milestones != null)
                foreach (var t in c.Campaign.Themes)
                    if (t.MilestoneOrb && s.Milestones.Contains(t.Id)) milestones++;
            int fromMilestones = Math.Min(beyond, milestones);
            int fromFinds = Math.Min(beyond - fromMilestones, s.OrbFinds?.Count ?? 0);
            int restored = beyond - fromMilestones - fromFinds;
            int broken = Math.Max(0, s.Count(BrokenOrbId));
            shards = fromMilestones * MilestoneShards;
            dust = fromFinds * HalcyonFindDust + (restored + broken) * BrokenOrbDust;
            if (broken > 0) s.AddItem(BrokenOrbId, -broken);
            if (shards > 0) DiamondExchange.Add(s, shards);
            if (dust > 0) s.AddItem(DustEconomy.DustId, dust);
        }

        private static Rng Roll(SaveData s, string key)
        {
            var rng = new Rng(unchecked(s.OrbRolls * 7919 + 15485863 + StableHash(key)));
            s.OrbRolls++;
            return rng;
        }

        private static int StableHash(string text)
        {
            unchecked
            {
                int h = 17;
                foreach (var ch in text ?? "") h = h * 31 + ch;
                return h;
            }
        }
    }
}
