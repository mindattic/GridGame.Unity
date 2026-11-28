using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
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
            GrantMana(s, 1);
            BattleEngine.Apply(s, new UseSlotCommand { HeroId = cleric.Id, Slot = 0, Target = knight.Pos });
            Assert.That(knight.Hp, Is.GreaterThan(knight.MaxHp / 2));
            Assert.That(s.Results.PairCredits.TryGetValue("cleric|knight", out int n) ? n : 0, Is.EqualTo(1));
        }

        // ── Core: bonded pincer partners (§25.20, GG-T1308) ──

        private static (int bondTier, int damage) KnightClericPincer(int? tier)
        {
            var (s, _) = Battle(Mini(), Stage(("tank", 1, 0)), ("knight", 0, 0), ("cleric", 2, 0));
            if (tier.HasValue) s.Cohesion["cleric|knight"] = tier.Value;
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "knight").Id, Path = P((0, 1), (0, 0)) });
            var formed = ev.OfType<PincerFormedEvent>().Single();
            return (formed.BondTier, ev.OfType<DamagedEvent>().Where(d => d.Hit != HitKind.Miss).Sum(d => d.Amount));
        }

        [Test]
        public void A_bonded_hero_pincer_hits_harder_and_carries_its_tier()
        {
            var plain = KnightClericPincer(null);
            var bonded = KnightClericPincer(2);
            Assert.That(plain.bondTier, Is.EqualTo(0));
            Assert.That(bonded.bondTier, Is.EqualTo(2));
            Assert.That(bonded.damage, Is.GreaterThan(plain.damage));
        }

        [Test]
        public void An_explicit_tier_0_pair_is_unbonded()
        {
            Assert.That(KnightClericPincer(0), Is.EqualTo(KnightClericPincer(null)));
        }

        [Test]
        public void An_enemy_pincer_has_no_bond_tier()
        {
            var db = Mini();
            var st = new Stats { Strength = 5, Vitality = 2, Agility = 10, Speed = 10, Intelligence = 10 };
            db.Classes["frog"] = new ClassDef { Id = "frog", Name = "Frog", Team = Team.Enemy, Tags = { "Aquatic", "Sentient" }, BaseStats = st, MoveRange = 0 };
            var setup = new BattleSetup { Stage = Stage(("frog", 0, 3), ("frog", 2, 3)), Seed = 7, UnlockedSlots = 5 };
            setup.Cohesion["cleric|knight"] = 3;
            setup.Cohesion["frog|frog"] = 3;
            setup.Party.Add(new HeroSpec { ClassId = "knight", Start = new Coord(1, 3) });
            setup.Party.Add(new HeroSpec { ClassId = "cleric", Start = new Coord(5, 7) });
            var s = BattleEngine.Create(db, setup, new List<BattleEvent>());
            s.TurnEntryOf(At(s, 0, 3).Id).Ip = BattleConfig.BarLength - 1;
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            var formed = ev.OfType<PincerFormedEvent>().Where(p => p.Team == Team.Enemy).ToList();
            Assert.That(formed, Is.Not.Empty);
            Assert.That(formed.All(p => p.BondTier == 0), Is.True);
        }

        // ── Core: bonded supporters (§25.20, GG-T1309) ──

        /// <summary>knight(1,3) and cleric(3,3) flank the tank on (2,3); a scout on (1,4) links the mage (Range 1) on (1,5),
        /// two tiles along the chain from the knight.</summary>
        private static (BattleState s, Unit knight, Unit cleric) ReachChain(int? tier)
        {
            var db = Mini();
            db.Classes["scout"] = new ClassDef { Id = "scout", Name = "Scout", Team = Team.Hero, Tags = { "Humanoid" }, BaseStats = db.Classes["knight"].BaseStats };
            db.Classes["mage"].Support = new SupportDef { Effect = SupportEffect.Heal, Magnitude = 0.1f, Range = 1 };
            var setup = new BattleSetup { Stage = Stage(("tank", 2, 3)), Seed = 7, UnlockedSlots = 5 };
            if (tier.HasValue) setup.Cohesion["knight|mage"] = tier.Value;
            setup.Party.Add(new HeroSpec { ClassId = "knight", Start = new Coord(1, 3) });
            setup.Party.Add(new HeroSpec { ClassId = "cleric", Start = new Coord(3, 3) });
            setup.Party.Add(new HeroSpec { ClassId = "scout", Start = new Coord(1, 4) });
            setup.Party.Add(new HeroSpec { ClassId = "mage", Start = new Coord(1, 5) });
            var s = BattleEngine.Create(db, setup, new List<BattleEvent>());
            return (s, Hero(s, "knight"), Hero(s, "cleric"));
        }

        [Test]
        public void A_tier_3_bonded_supporter_reaches_one_tile_further()
        {
            foreach (var (tier, reaches) in new (int?, bool)[] { (null, false), (BattleConfig.BondedReachTier - 1, false), (BattleConfig.BondedReachTier, true), (5, true) })
            {
                var (s, knight, cleric) = ReachChain(tier);
                var found = GridGame.Core.Rules.PincerDetector.FindSupporters(s, knight, cleric);
                Assert.That(found.Any(u => u.Def.Id == "scout"), Is.True, $"tier {tier}: the scout touches the knight");
                Assert.That(found.Any(u => u.Def.Id == "mage"), Is.EqualTo(reaches), $"tier {tier}");
            }
        }

        /// <summary>The cleric closes a pincer on the tank with the knight; the mage on (1,4) lends the knight 30% crit.</summary>
        private static SupportActionEvent CritSupport(int? tier)
        {
            var db = Mini();
            db.Classes["mage"].Support = new SupportDef { Effect = SupportEffect.Crit, Magnitude = 0.3f, Range = 1 };
            var setup = new BattleSetup { Stage = Stage(("tank", 2, 3)), Seed = 7, UnlockedSlots = 5 };
            if (tier.HasValue) setup.Cohesion["knight|mage"] = tier.Value;
            setup.Party.Add(new HeroSpec { ClassId = "knight", Start = new Coord(1, 3) });
            setup.Party.Add(new HeroSpec { ClassId = "cleric", Start = new Coord(3, 4) });
            setup.Party.Add(new HeroSpec { ClassId = "mage", Start = new Coord(1, 4) });
            var s = BattleEngine.Create(db, setup, new List<BattleEvent>());
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((3, 3)) });
            return ev.OfType<SupportActionEvent>().Single(e => e.SupporterId == Hero(s, "mage").Id);
        }

        [Test]
        public void A_tier_4_bonded_supporter_supports_at_the_bonded_magnitude()
        {
            var plain = CritSupport(null);
            var three = CritSupport(BattleConfig.BondedSupportTier - 1);
            var four = CritSupport(BattleConfig.BondedSupportTier);
            Assert.That(plain.BondTier, Is.EqualTo(0));
            Assert.That(plain.Amount, Is.EqualTo(0.3f).Within(1e-4));
            Assert.That(three.BondTier, Is.EqualTo(BattleConfig.BondedSupportTier - 1));
            Assert.That(three.Amount, Is.EqualTo(0.3f).Within(1e-4));
            Assert.That(four.BondTier, Is.EqualTo(BattleConfig.BondedSupportTier));
            Assert.That(four.Amount, Is.EqualTo(0.3f * (1f + BattleConfig.BondedSupportBonus)).Within(1e-4));
        }

        [Test]
        public void The_factory_fills_setup_cohesion_only_for_bonded_party_pairs()
        {
            var c = Real; var s = Progression.NewGame(c, "Bonds");
            Assert.That(BattleFactory.Build(c, s, c.CampaignStages()[0].Id, 5).Cohesion, Is.Empty);

            var party = s.Party.Where(id => Bonds.HasCohesion(s, id)).ToList();
            Assert.That(party.Count, Is.GreaterThanOrEqualTo(2));
            Bonds.AddCohesion(s, party[0], party[1], 30);
            var setup = BattleFactory.Build(c, s, c.CampaignStages()[0].Id, 5);
            Assert.That(setup.Cohesion.Keys, Is.EquivalentTo(new[] { Bonds.PairKey(party[0], party[1]) }));
            Assert.That(setup.Cohesion[Bonds.PairKey(party[0], party[1])], Is.EqualTo(Bonds.CohesionTier(s, party[0], party[1])).And.GreaterThan(0));
        }
    }
}
