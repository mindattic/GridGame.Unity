using System.Linq;
using GridGame.Core.Battle;
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
        public void A_new_game_owns_no_orbs_and_its_battle_has_no_orb_cap()
        {
            var c = Real;
            var s = Progression.NewGame(c, "T");
            Assert.That(s.OwnedOrbs, Is.EqualTo(0));
            Assert.That(BattleFactory.Build(c, s, c.CampaignStages()[0].Id, 1).OrbCapacity, Is.EqualTo(0), "nothing caps a battle's mana");
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
        public void Unspent_orbs_settle_into_one_dust_each()
        {
            Assert.That(DustEconomy.SettleOrbs(5), Is.EqualTo(5 * DustEconomy.DustPerOrbLeft));
            Assert.That(DustEconomy.SettleOrbs(-1), Is.EqualTo(0));
        }
    }
}
