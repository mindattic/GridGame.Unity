using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>GG-T1346: the Camp hub's destinations as one list, grouped for the pillars, tiles and group menus.</summary>
    public class CampMenuTests
    {
        private static SaveData Fresh() => new SaveData { HighestClearedStage = -1 };
        private static CampDestination Find(System.Collections.Generic.List<CampDestination> menu, string id) => menu.Single(d => d.Id == id);

        [Test]
        public void Every_destination_sits_in_its_group()
        {
            var menu = CampMenu.Build(TestKit.Real, new SaveData { TestMode = true, HighestClearedStage = -1 }, new SettingsSave());
            Assert.That(Find(menu, CampMenu.Campaign).Group, Is.EqualTo(CampGroup.Campaign));
            Assert.That(Find(menu, CampMenu.Heroes).Group, Is.EqualTo(CampGroup.Heroes));
            Assert.That(Find(menu, CampMenu.Summon).Group, Is.EqualTo(CampGroup.Summon));
            Assert.That(Find(menu, CampMenu.Shop).Group, Is.EqualTo(CampGroup.Shop));
            foreach (var id in new[] { CampMenu.Blacksmith, CampMenu.Alchemist, CampMenu.Aetherist })
                Assert.That(Find(menu, id).Group, Is.EqualTo(CampGroup.Craft), id);
            foreach (var id in new[] { CampMenu.Mercenaries, CampMenu.Talk, CampMenu.Codex, CampMenu.Bestiary })
                Assert.That(Find(menu, id).Group, Is.EqualTo(CampGroup.Journal), id);
            Assert.That(CampMenu.In(menu, CampGroup.System).Select(d => d.Id), Does.Contain(CampMenu.Settings).And.Contain(CampMenu.Quit));
            Assert.That(menu.Select(d => d.Id), Is.Unique);
        }

        [Test]
        public void A_fresh_save_shows_closed_stations_with_their_unlock_hints()
        {
            var c = TestKit.Real;
            var menu = CampMenu.Build(c, Fresh(), new SettingsSave());
            var smith = Find(menu, CampMenu.Blacksmith);
            Assert.That(smith.Open, Is.False);
            Assert.That(smith.Hint, Is.EqualTo(CampUnlocks.JoinHint(c, CampNpcs.Blacksmith)));
            Assert.That(smith.Npc, Is.EqualTo(CampNpcs.Blacksmith));
            var summon = Find(menu, CampMenu.Summon);
            Assert.That(summon.Open, Is.False);
            Assert.That(summon.Hint, Is.EqualTo(CampUnlocks.MilestoneHint(c, CampUnlocks.SummonAfter)));
            Assert.That(Find(menu, CampMenu.Campaign).Open, Is.True);
            Assert.That(Find(menu, CampMenu.Settings).Open, Is.True);
        }

        [Test]
        public void A_group_tile_opens_with_any_member_and_dims_with_the_first_hint()
        {
            var c = TestKit.Real;
            var s = Fresh();
            var menu = CampMenu.Build(c, s, new SettingsSave());
            Assert.That(CampMenu.GroupOpen(menu, CampGroup.Craft), Is.True, "Items is always open");
            Assert.That(CampMenu.GroupOpen(menu, CampGroup.Summon), Is.False);
            Assert.That(CampMenu.GroupHint(menu, CampGroup.Summon), Is.EqualTo(CampUnlocks.MilestoneHint(c, CampUnlocks.SummonAfter)));
            s.HighestClearedStage = CampUnlocks.SummonAfter - 1;
            menu = CampMenu.Build(c, s, new SettingsSave());
            Assert.That(CampMenu.GroupOpen(menu, CampGroup.Summon), Is.True);
            Assert.That(CampMenu.GroupHint(menu, CampGroup.Summon), Is.Empty);
        }

        [Test]
        public void Something_waiting_badges_its_destination_and_group()
        {
            var c = TestKit.Real;
            if (!Features.Bounties) Assert.Ignore("Bounties are switched off");
            var s = new SaveData { TestMode = true, HighestClearedStage = -1 };
            Assert.That(Find(CampMenu.Build(c, s, new SettingsSave()), CampMenu.Bounties).Badge, Is.False);
            s.Bounty.Slain.Add("bounty_grimjaw");
            var menu = CampMenu.Build(c, s, new SettingsSave());
            Assert.That(Find(menu, CampMenu.Bounties).Badge, Is.True);
            Assert.That(Find(menu, CampMenu.Bounties).Value, Does.Contain("to collect"));
            Assert.That(CampMenu.GroupBadge(menu, CampGroup.Journal), Is.True);
        }

        [Test]
        public void Switched_off_systems_are_left_out()
        {
            bool was = Features.Enchanter;
            try
            {
                Features.Enchanter = false;
                Assert.That(CampMenu.Build(TestKit.Real, Fresh(), new SettingsSave()).Any(d => d.Id == CampMenu.Enchantress), Is.False);
                Features.Enchanter = true;
                Assert.That(Find(CampMenu.Build(TestKit.Real, Fresh(), new SettingsSave()), CampMenu.Enchantress).Group, Is.EqualTo(CampGroup.Craft));
            }
            finally { Features.Enchanter = was; }
        }

        [Test]
        public void Crucible_record_reads_endless_until_the_first_run()
        {
            var s = Fresh();
            Assert.That(CampMenu.CrucibleRecord(s, default), Is.EqualTo("Endless waves"));
        }
    }
}
