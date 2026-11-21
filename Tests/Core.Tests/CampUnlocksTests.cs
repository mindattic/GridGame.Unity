using System.Linq;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>GG-T174: Camp stations open one at a time; a closed one says when it arrives; nothing used ever relocks.</summary>
    public class CampUnlocksTests
    {
        private static SaveData Fresh() => new SaveData { HighestClearedStage = -1 };

        [Test]
        public void A_fresh_save_has_every_station_closed_until_its_keeper_or_milestone()
        {
            var s = Fresh();
            Assert.That(CampUnlocks.NpcOpen(s, CampNpcs.Merchant), Is.False);
            Assert.That(CampUnlocks.BountiesOpen(s), Is.False);
            Assert.That(CampUnlocks.SummonOpen(s), Is.False);
            Assert.That(CampUnlocks.CrucibleOpen(s), Is.False);
            Assert.That(CampUnlocks.CaravanOpen(s), Is.False);
            Assert.That(CampUnlocks.DiamondStoreOpen(s), Is.False);
        }

        /// <summary>GG-T537: one at a time: the Summon Circle (with the Diamond Store), then Bounties, then the Crucible,
        /// then the Caravan, each on its own stage clear.</summary>
        [Test]
        public void Milestone_stations_open_as_stages_are_cleared()
        {
            var s = Fresh();
            s.HighestClearedStage = CampUnlocks.SummonAfter - 1;
            Assert.That(CampUnlocks.SummonOpen(s) && CampUnlocks.DiamondStoreOpen(s), Is.True);
            Assert.That(CampUnlocks.BountiesOpen(s), Is.False);
            s.HighestClearedStage = CampUnlocks.BountiesAfter - 1;
            Assert.That(CampUnlocks.BountiesOpen(s), Is.True);
            Assert.That(CampUnlocks.CrucibleOpen(s), Is.False);
            s.HighestClearedStage = CampUnlocks.CrucibleAfter - 1;
            Assert.That(CampUnlocks.CrucibleOpen(s), Is.True);
            Assert.That(CampUnlocks.CaravanOpen(s), Is.False);
            s.HighestClearedStage = CampUnlocks.CaravanAfter - 1;
            Assert.That(CampUnlocks.CaravanOpen(s), Is.True);
        }

        /// <summary>GG-T537: each keeperless station's story beat (its OpenStation effect) sits on the very stage whose clear
        /// opens it, so the scene, the results line and the Camp row agree.</summary>
        [Test]
        public void Each_station_beat_is_on_the_stage_that_opens_it()
        {
            var c = TestKit.Real;
            var stages = c.CampaignStages();
            foreach (var (station, after) in new[] { (CampUnlocks.Summon, CampUnlocks.SummonAfter), (CampUnlocks.Bounties, CampUnlocks.BountiesAfter),
                                                     (CampUnlocks.Crucible, CampUnlocks.CrucibleAfter), (CampUnlocks.Caravan, CampUnlocks.CaravanAfter) })
            {
                var beat = c.DialogueOrder.Single(d => DialogueRunner.IsVendorBeat(d) && d.AllEffects().Any(e => e.Type == DialogueEffectType.OpenStation && e.Id == station));
                Assert.That(beat.Trigger.Id, Is.EqualTo(stages[after - 1].Id), station);
            }
        }

        [Test]
        public void A_station_already_used_never_locks_again()
        {
            var s = Fresh();
            s.Bounty.Taken.Add("bounty_grimjaw");
            s.Recruits = 1;
            s.Crucible.Runs = 1;
            s.Caravan["wagon"] = 1;
            Assert.That(CampUnlocks.BountiesOpen(s) && CampUnlocks.SummonOpen(s) && CampUnlocks.CrucibleOpen(s) && CampUnlocks.CaravanOpen(s), Is.True);
            var bought = Fresh();
            bought.Purchases.Granted["order-1"] = new PurchaseRecord();
            Assert.That(CampUnlocks.DiamondStoreOpen(bought), Is.True, "a save that bought Diamonds keeps the store");
        }

        [Test]
        public void Bot_and_new_game_plus_saves_see_everything()
        {
            Assert.That(CampUnlocks.CaravanOpen(new SaveData { TestMode = true, HighestClearedStage = -1 }), Is.True);
            Assert.That(CampUnlocks.NpcOpen(new SaveData { TestMode = true }, CampNpcs.Enchantress), Is.True);
            Assert.That(CampUnlocks.CaravanOpen(new SaveData { NewGamePlus = 1, HighestClearedStage = -1 }), Is.True);
        }

        [Test]
        public void Join_hints_name_the_story_beat_that_recruits_each_keeper()
        {
            var c = TestKit.Real;
            Assert.That(CampUnlocks.JoinHint(c, CampNpcs.Merchant), Is.EqualTo("Joins after The Tithe Moors I"));
            Assert.That(CampUnlocks.JoinHint(c, CampNpcs.Innkeeper), Is.EqualTo("Joins after The Tithe Moors II"));
            Assert.That(CampUnlocks.JoinHint(c, CampNpcs.Blacksmith), Is.EqualTo("Joins after The Tithe Moors III"));
            Assert.That(CampUnlocks.JoinHint(c, CampNpcs.Alchemist), Is.EqualTo("Joins after The Lantern Road I"));
            foreach (var npc in CampNpcs.All)
                Assert.That(CampUnlocks.JoinHint(c, npc), Does.StartWith("Joins after "), npc + " has no stage-clear join beat");
            Assert.That(CampUnlocks.SmithWorkshopHint(c), Is.EqualTo("Opens when the Alchemist joins, after The Lantern Road I"));
        }

        [Test]
        public void The_blacksmith_opens_with_only_repair_until_the_alchemist_joins()
        {
            var s = Fresh();
            DialogueRunner.ApplyEffect(TestKit.Real, s, new GridGame.Core.Content.DialogueEffect { Type = GridGame.Core.Content.DialogueEffectType.JoinNpc, Id = CampNpcs.Blacksmith });
            Assert.That(CampUnlocks.NpcOpen(s, CampNpcs.Blacksmith), Is.True);
            Assert.That(CampUnlocks.SmithWorkshopOpen(s), Is.False, "Forge/Upgrade/Salvage wait");
            DialogueRunner.ApplyEffect(TestKit.Real, s, new GridGame.Core.Content.DialogueEffect { Type = GridGame.Core.Content.DialogueEffectType.JoinNpc, Id = CampNpcs.Alchemist });
            Assert.That(CampUnlocks.SmithWorkshopOpen(s), Is.True);
            Assert.That(CampUnlocks.SmithWorkshopOpen(new SaveData { TestMode = true }), Is.True);
            Assert.That(CampUnlocks.SmithWorkshopOpen(new SaveData { NewGamePlus = 1 }), Is.True);
        }

        [Test]
        public void Milestone_hints_name_a_real_stage()
        {
            var c = TestKit.Real;
            var stages = c.CampaignStages();
            Assert.That(CampUnlocks.MilestoneHint(c, CampUnlocks.BountiesAfter), Is.EqualTo("Opens after " + stages[CampUnlocks.BountiesAfter - 1].Name));
            Assert.That(CampUnlocks.MilestoneHint(c, CampUnlocks.SummonAfter), Is.EqualTo("Opens after The Lantern Road II"));
        }
    }
}
