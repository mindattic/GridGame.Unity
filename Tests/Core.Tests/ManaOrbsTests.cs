using System.Linq;
using GridGame.Core.Battle;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>Owned Mana Orbs (GG-T677, BIBLE §3.1.4.3) and unspent orbs settling into Mana Dust (GG-T676, §3.1.4.2).</summary>
    public class ManaOrbsTests
    {
        [Test]
        public void Every_orb_source_adds_up_to_the_cap_and_a_new_game_holds_one()
        {
            var c = Real;
            int sources = BattleConfig.StartOwnedOrbs + BattleConfig.MaxBrokenOrbs
                + c.Campaign.Themes.Count(t => t.MilestoneOrb)
                + c.Campaign.Themes.Where(t => t.OrbFindChance > 0f).Sum(t => t.Stages.Count);
            Assert.That(sources, Is.EqualTo(BattleConfig.MaxOrbCapacity), "20 possible orbs (owner 2026-11-22)");
            var s = Progression.NewGame(c, "T");
            Assert.That(ManaOrbs.Owned(c, s), Is.EqualTo(1));
            Assert.That(BattleFactory.Build(c, s, c.CampaignStages()[0].Id, 1).OrbCapacity, Is.EqualTo(1), "the battle row fills what you own");
        }

        [Test]
        public void An_old_save_gets_the_orbs_its_milestones_earned()
        {
            var c = Real;
            var s = Progression.NewGame(c, "T");
            s.OwnedOrbs = 0;
            foreach (var t in c.Campaign.Themes.Where(t => t.MilestoneOrb).Take(3)) s.Milestones.Add(t.Id);
            SaveMigrations.GrantOwnedOrbs(s, c);
            Assert.That(s.OwnedOrbs, Is.EqualTo(1 + 3));
        }

        [Test]
        public void The_manasmith_restores_a_broken_orb_for_dust_and_gold()
        {
            var c = Real;
            var s = Progression.NewGame(c, "T");
            s.AddItem(ManaOrbs.BrokenOrbId, 1);
            s.AddItem(DustEconomy.DustId, ManaOrbs.RestoreDust);
            s.Gold = ManaOrbs.RestoreGold;
            Assert.That(ManaOrbs.Restore(c, s).Ok, Is.True);
            Assert.That((s.OwnedOrbs, s.Count(ManaOrbs.BrokenOrbId), DustEconomy.Have(s), s.Gold), Is.EqualTo((2, 0, 0, 0)));
            Assert.That(ManaOrbs.Restore(c, s).Ok, Is.False, "nothing left to restore");
        }

        [Test]
        public void Unspent_orbs_settle_into_one_dust_each()
        {
            Assert.That(DustEconomy.SettleOrbs(5), Is.EqualTo(5 * DustEconomy.DustPerOrbLeft));
            Assert.That(DustEconomy.SettleOrbs(-1), Is.EqualTo(0));
        }
    }
}
