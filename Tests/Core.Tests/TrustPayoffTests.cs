using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Rules;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T1581: a hero's Trust tier fights with it — crit, evasion and once-per-battle Resolve.</summary>
    public class TrustPayoffTests
    {
        [Test]
        public void Meta_bond_payoffs_alias_the_battle_config()
        {
            Assert.That(BondsConfig.CritPerTrustTier, Is.EqualTo(BattleConfig.CritPerTrustTier));
            Assert.That(BondsConfig.EvasionPerTrustTier, Is.EqualTo(BattleConfig.EvasionPerTrustTier));
            Assert.That(BondsConfig.ResolveTier, Is.EqualTo(BattleConfig.ResolveTier));
            Assert.That(BondsConfig.ResolveHp, Is.EqualTo(BattleConfig.ResolveHp));
            Assert.That(ClutchKinds.Id(ClutchKind.Resolve), Is.EqualTo("resolve"));
        }

        [Test]
        public void Spec_carries_the_saves_trust_tier_into_the_unit()
        {
            var s = new SaveData { Roster = { new HeroSave { ClassId = "knight" } } };
            Bonds.AddTrust(s, "knight", 100);
            var h = s.Hero("knight") ?? s.Roster[0];
            Assert.That(BattleFactory.SpecFor(Real, h).TrustTier, Is.EqualTo(0), "no save, no trust");
            var spec = BattleFactory.SpecFor(Real, h, s);
            Assert.That(spec.TrustTier, Is.EqualTo(Bonds.TrustTier(s, "knight")));
            Assert.That(spec.TrustTier, Is.GreaterThan(0));

            var db = Mini();
            var setup = new BattleSetup { Stage = Stage(("wolf", 0, 0)), Seed = 1, UnlockedSlots = 5 };
            setup.Party.Add(new HeroSpec { ClassId = "knight", Start = new Coord(4, 7), TrustTier = 3 });
            var state = BattleEngine.Create(db, setup, new List<BattleEvent>());
            Assert.That(Hero(state, "knight").TrustTier, Is.EqualTo(3));
        }

        [Test]
        public void Each_trust_tier_adds_evasion()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("wolf", 0, 0)), ("knight", 4, 7));
            var k = Hero(s, "knight");
            float before = Formulas.Evasion(k);
            k.TrustTier = 2;
            Assert.That(Formulas.Evasion(k), Is.EqualTo(System.Math.Min(Formulas.EvasionCap, before + 2 * BattleConfig.EvasionPerTrustTier * 100f)).Within(0.001f));
        }

        [Test]
        public void Resolve_survives_one_lethal_blow_per_battle()
        {
            var db = Mini();
            db.Classes["wolf"].BaseStats.Strength = 1000;
            bool resolved = false;
            for (int seed = 1; seed < 40 && !resolved; seed++)
            {
                var setup = new BattleSetup { Stage = Stage(("wolf", 4, 1)), Seed = seed, UnlockedSlots = 5 };
                setup.Party.Add(new HeroSpec { ClassId = "knight", Start = new Coord(4, 0), TrustTier = BattleConfig.ResolveTier });
                setup.Party.Add(new HeroSpec { ClassId = "cleric", Start = new Coord(0, 7) });
                var s = BattleEngine.Create(db, setup, new List<BattleEvent>());
                var k = Hero(s, "knight");
                var wolf = s.Units.First(u => u.Def.Id == "wolf" && u.Alive);
                var all = new List<BattleEvent>();
                for (int i = 0; i < 6 && !all.Any(e => e is DamagedEvent d && d.TargetId == k.Id && d.Hit != HitKind.Miss); i++)
                {
                    s.TurnEntryOf(wolf.Id).Ip = BattleConfig.BarLength - 1;
                    all.AddRange(BattleEngine.Apply(s, new ShieldCommand()));
                }
                var hit = all.OfType<DamagedEvent>().FirstOrDefault(d => d.TargetId == k.Id && d.Hit != HitKind.Miss);
                if (hit == null) continue;
                Assert.That(k.Hp, Is.EqualTo(BattleConfig.ResolveHp));
                Assert.That(k.ResolveUsed, Is.True);
                Assert.That(all.OfType<ResolveEvent>().Any(r => r.UnitId == k.Id), Is.True);
                Assert.That(all.OfType<ClutchEvent>().Any(c => c.CasterId == k.Id && c.Kind == ClutchKind.Resolve), Is.True);
                resolved = true;
            }
            Assert.That(resolved, Is.True, "the wolf should land a blow on the knight within 40 seeds");
        }
    }
}
