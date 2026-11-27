using GridGame.Meta;
using NUnit.Framework;

namespace GridGame.Tests
{
    public class BondsTests
    {
        private static SaveData NewSave() => new SaveData
        {
            Roster = { new HeroSave { ClassId = "barbarian" }, new HeroSave { ClassId = "cleric" }, new HeroSave { ClassId = "warden" } }
        };

        [Test]
        public void Pair_key_is_unordered()
        {
            Assert.That(Bonds.PairKey("Cleric", "barbarian"), Is.EqualTo("barbarian|cleric"));
            Assert.That(Bonds.PairKey("barbarian", "cleric"), Is.EqualTo(Bonds.PairKey("cleric", "barbarian")));
            Assert.That(Bonds.PairKey("cleric", "cleric"), Is.Null);
        }

        [Test]
        public void Tiers_begin_at_the_config_floors()
        {
            Assert.That(Bonds.Tier(0), Is.EqualTo(0));
            Assert.That(Bonds.Tier(9), Is.EqualTo(0));
            Assert.That(Bonds.Tier(10), Is.EqualTo(1));
            Assert.That(Bonds.Tier(44), Is.EqualTo(2));
            Assert.That(Bonds.Tier(70), Is.EqualTo(4));
            Assert.That(Bonds.Tier(100), Is.EqualTo(5));
        }

        [Test]
        public void Trust_clamps_and_never_drops_below_the_highest_tier_reached()
        {
            var s = NewSave();
            Assert.That(Bonds.AddTrust(s, "barbarian", 30), Is.EqualTo(30));
            Assert.That(Bonds.AddTrust(s, "barbarian", -20), Is.EqualTo(25));
            Assert.That(Bonds.TrustTier(s, "barbarian"), Is.EqualTo(2));
            Assert.That(Bonds.AddTrust(s, "barbarian", 500), Is.EqualTo(BondsConfig.Max));
            Assert.That(Bonds.AddTrust(s, "barbarian", -500), Is.EqualTo(BondsConfig.Max));
        }

        [Test]
        public void Cohesion_is_one_bond_per_unordered_pair()
        {
            var s = NewSave();
            Assert.That(Bonds.AddCohesion(s, "warden", "cleric", 12), Is.EqualTo(12));
            Assert.That(Bonds.Cohesion(s, "cleric", "warden"), Is.EqualTo(12));
            Assert.That(Bonds.AddCohesion(s, "cleric", "cleric", 5), Is.EqualTo(0));
        }

        [Test]
        public void Domesticated_monsters_and_heroes_off_the_roster_have_no_bonds()
        {
            var s = NewSave();
            s.Roster.Add(new HeroSave { ClassId = "wolf", Domesticated = true });
            Assert.That(Bonds.HasTrust(s, "wolf"), Is.False);
            Assert.That(Bonds.HasCohesion(s, "wolf"), Is.False);
            Assert.That(Bonds.AddTrust(s, "knight", 5), Is.EqualTo(0));
        }

        [Test]
        public void A_seed_counts_only_on_the_first_join()
        {
            var s = NewSave();
            Assert.That(Bonds.SeedTrust(s, "cleric", JoinRoute.StoryRecruit), Is.EqualTo(BondsConfig.StoryRecruitSeed));
            Bonds.AddTrust(s, "cleric", 3);
            Assert.That(Bonds.SeedTrust(s, "cleric", JoinRoute.Rapport), Is.EqualTo(BondsConfig.StoryRecruitSeed + 3));
        }

        [Test]
        public void A_choice_pays_once_per_save()
        {
            var s = NewSave();
            Assert.That(Bonds.PayChoiceOnce(s, "camp_talk", "kind"), Is.True);
            Assert.That(Bonds.PayChoiceOnce(s, "camp_talk", "kind"), Is.False);
            Assert.That(Bonds.ClampChoice(25), Is.EqualTo(BondsConfig.ChoiceMax));
        }

        [Test]
        public void Bonds_round_trip_through_a_save()
        {
            var s = NewSave();
            Bonds.AddTrust(s, "barbarian", 47);
            Bonds.AddCohesion(s, "barbarian", "cleric", 26);
            Bonds.PayChoiceOnce(s, "d", "c");
            var back = SaveMigrations.Migrate(SaveFiles.Deserialize(SaveFiles.Serialize(s)));
            Assert.That(Bonds.Trust(back, "barbarian"), Is.EqualTo(47));
            Assert.That(Bonds.TrustTierReached(back, "barbarian"), Is.EqualTo(3));
            Assert.That(Bonds.Cohesion(back, "cleric", "barbarian"), Is.EqualTo(26));
            Assert.That(back.BondChoices, Does.Contain("d/c"));
        }

        [Test]
        public void A_v19_save_migrates_with_every_bond_at_zero()
        {
            var s = NewSave();
            s.Version = 19;
            s.Trust = null;
            s.Cohesion = null;
            s.TrustTierReached = null;
            s.CohesionTierReached = null;
            s.BondChoices = null;
            var back = SaveMigrations.Migrate(s);
            Assert.That(back.Version, Is.EqualTo(SaveData.CurrentVersion));
            Assert.That(back.Trust, Is.Empty);
            Assert.That(back.Cohesion, Is.Empty);
            Assert.That(back.BondChoices, Is.Empty);
            Assert.That(Bonds.Trust(back, "barbarian"), Is.EqualTo(0));
            Assert.That(SaveMigrations.Migrate(back).Version, Is.EqualTo(SaveData.CurrentVersion));
        }
    }
}
