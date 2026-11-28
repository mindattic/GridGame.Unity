using System.Linq;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>Mana Orbs retired as progression (GG-T1811, BIBLE §3.1.0) and full spell meters settling into Mana Dust
    /// (GG-T676, §3.1.4.2).</summary>
    public class ManaOrbsTests
    {
        [Test]
        public void A_new_game_owns_no_orbs()
        {
            var c = Real;
            var s = Progression.NewGame(c, "T");
            Assert.That(s.OwnedOrbs, Is.EqualTo(0));
        }

        [Test]
        public void An_old_save_is_paid_once_for_its_owned_orbs_and_broken_orbs()
        {
            var c = Real;
            var s = Progression.NewGame(c, "T");
            var milestones = c.Campaign.Themes.Where(t => t.MilestoneOrb).Take(2).ToList();
            foreach (var t in milestones) s.Milestones.Add(t.Id);
            s.OrbFinds.Add("some_ruin_stage");
            s.OwnedOrbs = BattleConfig.StartOwnedOrbs + milestones.Count + 1 + 1; // two milestones, a find, a restored orb
            s.AddItem(ManaOrbs.BrokenOrbId, 2);
            int shards = DiamondExchange.Have(s), dust = DustEconomy.Have(s);
            s.Version = 27;
            SaveMigrations.Migrate(s, c);
            Assert.That(DiamondExchange.Have(s) - shards, Is.EqualTo(milestones.Count * ManaOrbs.MilestoneShards));
            Assert.That(DustEconomy.Have(s) - dust, Is.EqualTo(ManaOrbs.HalcyonFindDust + 3 * ManaOrbs.BrokenOrbDust));
            Assert.That(s.Count(ManaOrbs.BrokenOrbId), Is.EqualTo(0));
            int after = DustEconomy.Have(s);
            SaveMigrations.Migrate(s, c);
            Assert.That(DustEconomy.Have(s), Is.EqualTo(after), "paid once");
        }

        [Test]
        public void A_halcyon_find_pays_dust_and_one_wearable_piece_of_gear_a_tier_above_the_ruins_shelf()
        {
            var c = Real;
            var s = Progression.NewGame(c, "T");
            var theme = c.Campaign.Themes.First(t => t.OrbFindChance > 0f);
            var stage = theme.Stages[0];
            var rep = new BattleReport();
            int dust = DustEconomy.Have(s);
            for (int i = 0; i < 200 && !s.OrbFinds.Contains(stage); i++) ManaOrbs.AfterVictory(c, s, rep, stage, null);
            Assert.That(s.OrbFinds, Does.Contain(stage), "a find turns up within 200 victories");
            Assert.That(DustEconomy.Have(s) - dust, Is.EqualTo(ManaOrbs.HalcyonFindDust));
            var gear = rep.Items.Keys.Select(c.TryItem).Single(i => i.Type == ItemType.Equipment);
            int tier = System.Math.Min(VendorTiers.Count, VendorTiers.RegionTier(c, theme.Id) + ManaOrbs.HalcyonGearTiersAbove);
            Assert.That(VendorTiers.RarityTier(gear.Rarity), Is.LessThanOrEqualTo(tier));
            Assert.That(s.Party.Select(s.Hero).Any(h => Equipment.ProficiencyGate(c, h, gear) == null), "someone in the party can wear it");
            Assert.That(rep.OrbRewards.Single(), Does.Contain(gear.Name), "the Bonus row names it");

            var again = Progression.NewGame(c, "T");
            again.OrbRolls = 7;
            var a = ManaOrbs.HalcyonGear(c, again, theme, new GridGame.Core.Rng(42));
            Assert.That(ManaOrbs.HalcyonGear(c, again, theme, new GridGame.Core.Rng(42)), Is.SameAs(a), "seeded: the same roll picks the same piece");
        }

        [Test]
        public void Unspent_mana_settles_into_one_dust_per_whole_100_points()
        {
            Assert.That(DustEconomy.SettleMana(500), Is.EqualTo(5 * DustEconomy.DustPerOrbLeft));
            Assert.That(DustEconomy.SettleMana(599), Is.EqualTo(5 * DustEconomy.DustPerOrbLeft), "a part-100 remainder pays nothing");
            Assert.That(DustEconomy.SettleMana(99), Is.EqualTo(0));
            Assert.That(DustEconomy.SettleMana(-1), Is.EqualTo(0));
        }
    }
}
