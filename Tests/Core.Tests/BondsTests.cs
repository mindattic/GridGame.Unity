using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

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

        // ── Battle credits (GG-T1573) ──

        [Test]
        public void A_victory_pays_pair_credits_as_cohesion_capped_per_battle()
        {
            var s = NewSave();
            var credits = new Dictionary<string, int>
            {
                ["barbarian|cleric"] = BondsConfig.PairCreditCapPerBattle + 4,
                ["cleric|warden"] = 2,
            };
            var report = Bonds.ApplyBattle(s, credits, new string[0]);
            Assert.That(Bonds.Cohesion(s, "barbarian", "cleric"), Is.EqualTo(BondsConfig.PairCreditCapPerBattle));
            Assert.That(Bonds.Cohesion(s, "cleric", "warden"), Is.EqualTo(2));
            Assert.That(Bonds.Cohesion(s, "barbarian", "warden"), Is.EqualTo(0));
            var capped = report.Cohesion.Single(b => b.Key == "barbarian|cleric");
            Assert.That(capped.Before, Is.EqualTo(0));
            Assert.That(capped.After, Is.EqualTo(BondsConfig.PairCreditCapPerBattle));
            Assert.That(report.Cohesion.Select(b => b.Key), Is.EquivalentTo(new[] { "barbarian|cleric", "cleric|warden" }));
            Assert.That(report.Trust, Is.Empty);
        }

        [Test]
        public void A_victory_pays_victory_trust_once_per_fought_hero()
        {
            var s = NewSave();
            Bonds.AddTrust(s, "cleric", 12);
            var report = Bonds.ApplyBattle(s, null, new[] { "barbarian", "Cleric", "cleric" });
            Assert.That(Bonds.Trust(s, "barbarian"), Is.EqualTo(BondsConfig.VictoryTrust));
            Assert.That(Bonds.Trust(s, "cleric"), Is.EqualTo(12 + BondsConfig.VictoryTrust));
            Assert.That(Bonds.Trust(s, "warden"), Is.EqualTo(0), "a hero who sat the battle out gains nothing");
            var cleric = report.Trust.Single(b => b.Key == "cleric");
            Assert.That(cleric.Before, Is.EqualTo(12));
            Assert.That(cleric.After, Is.EqualTo(12 + BondsConfig.VictoryTrust));
            Assert.That(report.Trust.Count, Is.EqualTo(2));
        }

        [Test]
        public void Guests_domesticated_monsters_and_mercs_earn_no_battle_bonds()
        {
            var s = NewSave();
            s.Roster.Add(new HeroSave { ClassId = "wolf", Domesticated = true });
            s.Roster.Add(new HeroSave { ClassId = "knight" });
            s.Mercenaries.Contracts.Add(new MercContract { ClassId = "knight", BattlesLeft = 2, Term = 2 });
            var credits = new Dictionary<string, int> { ["cleric|wolf"] = 3, ["cleric|knight"] = 3, ["cleric|guest"] = 3 };
            var report = Bonds.ApplyBattle(s, credits, new[] { "wolf", "knight", "guest", "cleric" });
            Assert.That(report.Trust.Select(b => b.Key), Is.EqualTo(new[] { "cleric" }));
            Assert.That(report.Cohesion, Is.Empty);
            Assert.That(s.Trust.ContainsKey("wolf") || s.Trust.ContainsKey("knight") || s.Trust.ContainsKey("guest"), Is.False);
            Assert.That(s.Cohesion, Is.Empty);
        }

        [Test]
        public void A_won_commit_pays_a_downed_hero_victory_trust_and_reports_the_bonds()
        {
            var c = Real; var s = Progression.NewGame(c, "Bonds");
            var st = BattleEngine.Create(c, BattleFactory.Build(c, s, c.CampaignStages()[0].Id, 5), new List<BattleEvent>());
            st.Phase = BattlePhase.Victory;
            var fought = st.Units.Where(u => u.IsHero && !u.IsVisitor && !u.IsGuest).ToList();
            var downed = fought[0];
            downed.Hp = 0;
            var before = fought.ToDictionary(u => Bonds.Key(u.Def.Id), u => Bonds.Trust(s, u.Def.Id));

            var rep = BattleFactory.Commit(c, s, st);

            Assert.That(Bonds.Trust(s, downed.Def.Id), Is.EqualTo(before[Bonds.Key(downed.Def.Id)] + BondsConfig.VictoryTrust), "a downed hero still fought");
            foreach (var u in fought.Where(u => Bonds.HasTrust(s, u.Def.Id)))
            {
                var change = rep.BondChanges.Trust.Single(b => b.Key == Bonds.Key(u.Def.Id));
                Assert.That(change.Before, Is.EqualTo(before[change.Key]));
                Assert.That(change.After, Is.EqualTo(before[change.Key] + BondsConfig.VictoryTrust));
            }
        }

        // ── Core: what fills the pair credits (GG-T1301) ──

        [Test]
        public void A_hero_pincer_credits_the_pair()
        {
            var (s, _) = Battle(Mini(), Stage(("tank", 1, 0)), ("knight", 0, 0), ("cleric", 2, 0));
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "knight").Id, Path = P((0, 1), (0, 0)) });
            Assert.That(ev.OfType<PincerFormedEvent>().Any(), Is.True);
            Assert.That(s.Results.PairCredits.TryGetValue("cleric|knight", out int n) ? n : 0, Is.EqualTo(1));
        }

        [Test]
        public void A_heal_credits_the_pair()
        {
            var (s, _) = Battle(Mini(), Stage(("tank", 1, 0)), ("knight", 0, 0), ("cleric", 2, 0));
            var knight = Hero(s, "knight");
            var cleric = Hero(s, "cleric");
            knight.Hp = knight.MaxHp / 2;
            cleric.Bar[0] = new AbilitySlot { AbilityId = "heal" };
            s.Bank.Add(1);
            BattleEngine.Apply(s, new UseSlotCommand { HeroId = cleric.Id, Slot = 0, Target = knight.Pos });
            Assert.That(knight.Hp, Is.GreaterThan(knight.MaxHp / 2));
            Assert.That(s.Results.PairCredits.TryGetValue("cleric|knight", out int n) ? n : 0, Is.EqualTo(1));
        }
    }
}
