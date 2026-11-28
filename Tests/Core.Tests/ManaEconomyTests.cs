using System.Linq;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T247, GG-T1807 (§3.1.0): per-spell mana meters — every gain to every meter, fullness damping on earned
    /// gains, undamped whole grants and the support factor.</summary>
    public class ManaEconomyTests
    {
        /// <summary>An earned gain of <paramref name="baseCharge"/> after any fullness damping (no meter full … all but one).</summary>
        public static bool IsDampedFrom(int amount, int baseCharge) =>
            amount <= baseCharge && amount >= System.Math.Max(1, (int)System.Math.Round(baseCharge * (1f - BattleConfig.ChargeFullnessDamping)));

        /// <summary>The engine's damping expression for <paramref name="full"/> of <paramref name="total"/> meters full.</summary>
        private static int Damped(int amount, int full, int total) =>
            System.Math.Max(1, (int)System.Math.Round(amount * (1f - BattleConfig.ChargeFullnessDamping * (full / (float)total))));

        /// <summary>The knight (Fireball, Heal) and the cleric (Heal, Quicken) pincer a tank: four meters, the first
        /// <paramref name="fullMeters"/> of them full. Returns the pincer's gain.</summary>
        private static int PincerCharge(int fullMeters)
        {
            var db = Mini();
            db.Classes["tank"].BaseStats.Vitality = 30; // dented, not killed: the same share both times
            var (s, _) = Battle(db, Stage(("tank", 2, 3)), ("knight", 1, 3), ("cleric", 3, 4));
            var meters = Kit(s);
            for (int i = 0; i < fullMeters; i++) s.SetMana(meters[i].u.Id, meters[i].slot, s.ManaFull(meters[i].u, meters[i].slot));
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((3, 3)) });
            return ev.OfType<ManaGainedEvent>().Single(c => c.Reason == "pincer").Amount;
        }

        private static (Unit u, int slot)[] Kit(BattleState s)
        {
            var k = Hero(s, "knight");
            var c = Hero(s, "cleric");
            k.Bar[0] = new AbilitySlot { AbilityId = "fireball" };
            k.Bar[1] = new AbilitySlot { AbilityId = "heal" };
            c.Bar[0] = new AbilitySlot { AbilityId = "heal" };
            c.Bar[1] = new AbilitySlot { AbilityId = "quicken" };
            return new[] { (k, 0), (k, 1), (c, 0), (c, 1) };
        }

        [Test]
        public void Only_spell_slots_carry_a_meter_full_at_cost_times_the_orb_points()
        {
            var (s, _) = Battle(Mini(), Stage(("tank", 2, 2)), ("knight", 2, 3), ("cleric", 5, 7));
            var k = Hero(s, "knight");
            k.Bar[0] = new AbilitySlot { AbilityId = "fireball" };
            k.Bar[1] = new AbilitySlot { AbilityId = "steal" };
            Assert.That(s.ManaFull(k, 0), Is.EqualTo(s.Content.Ability("fireball").Cost * BattleConfig.ManaPointsPerOrb));
            Assert.That(s.ManaFull(k, 1), Is.EqualTo(0), "a Skill has no meter");
            Assert.That(s.ManaFull(k, 2), Is.EqualTo(0), "an empty slot has no meter");
            Assert.That(s.ManaIn(k.Id, 0), Is.EqualTo(0), "meters start the battle empty");
            Assert.That(s.SpellReady(k, 0), Is.False);
            s.SetMana(k.Id, 0, s.ManaFull(k, 0));
            Assert.That(s.SpellReady(k, 0), Is.True, "a full meter can cast");
        }

        [Test]
        public void A_gain_fills_every_living_heros_meters_by_the_same_points_each_capped_at_its_full()
        {
            var (s, _) = Battle(Mini(), Stage(("tank", 2, 2)), ("knight", 2, 3), ("cleric", 5, 7), ("mage", 5, 6));
            var k = Hero(s, "knight");
            var c = Hero(s, "cleric");
            var m = Hero(s, "mage");
            k.Bar[0] = new AbilitySlot { AbilityId = "fireball" };
            k.Bar[1] = new AbilitySlot { AbilityId = "heal" };
            c.Bar[0] = new AbilitySlot { AbilityId = "heal" }; // the same spell in another slot is another meter
            m.Bar[0] = new AbilitySlot { AbilityId = "meteor" };
            const int downedPoints = 30;
            s.SetMana(m.Id, 0, downedPoints);
            m.Hp = 0;

            int one = BattleConfig.ManaPointsPerOrb, gain = one + one / 2;
            var g = BattleEngine.GainMana(s, gain, "test");

            Assert.That(s.ManaIn(k.Id, 0), Is.EqualTo(gain), "Fireball (2 x 100) takes the whole gain");
            Assert.That(s.ManaIn(k.Id, 1), Is.EqualTo(one), "Heal (1 x 100) stops at full");
            Assert.That(s.ManaIn(c.Id, 0), Is.EqualTo(one));
            Assert.That(s.ManaIn(m.Id, 0), Is.EqualTo(downedPoints), "a downed hero keeps its points and receives nothing");
            Assert.That(g.Readied.Select(d => (d.UnitId, d.Slot)), Is.EquivalentTo(new[] { (k.Id, 1), (c.Id, 0) }));

            BattleEngine.GainMana(s, -(one + one / 5), "siphon");
            Assert.That(s.ManaIn(k.Id, 0), Is.EqualTo(gain - (one + one / 5)));
            Assert.That(s.ManaIn(k.Id, 1), Is.EqualTo(0), "a siphon floors at 0");
            Assert.That(s.ManaIn(c.Id, 0), Is.EqualTo(0));
            Assert.That(s.ManaIn(m.Id, 0), Is.EqualTo(downedPoints));
        }

        [Test]
        public void Damping_runs_from_full_rate_with_no_full_meter_down_by_the_share_of_full_meters()
        {
            var (s, _) = Battle(Mini(), Stage(("tank", 2, 3)), ("knight", 1, 3), ("cleric", 3, 4));
            Assert.That(BattleEngine.ChargeDamping(s), Is.EqualTo(1f), "no meters at all: nothing to damp");
            var meters = Kit(s);
            Assert.That(BattleEngine.ChargeDamping(s), Is.EqualTo(1f));
            for (int i = 0; i < meters.Length; i++)
            {
                s.SetMana(meters[i].u.Id, meters[i].slot, s.ManaFull(meters[i].u, meters[i].slot));
                float share = (i + 1) / (float)meters.Length;
                Assert.That(BattleEngine.ChargeDamping(s), Is.EqualTo(1f - BattleConfig.ChargeFullnessDamping * share).Within(1e-5));
            }
            Assert.That(s.AllSpellsFull, Is.True);
            s.SetMana(meters[0].u.Id, meters[0].slot, 0);
            Assert.That(BattleEngine.DampedCharge(s, 1), Is.EqualTo(1), "a positive gain never rounds to nothing");
        }

        [Test]
        public void More_full_meters_charge_slower()
        {
            int empty = PincerCharge(0), mostlyFull = PincerCharge(3);
            Assert.That(mostlyFull, Is.LessThan(empty));
            Assert.That(mostlyFull, Is.EqualTo(Damped(empty, 3, 4)));
        }

        [Test]
        public void Earning_stops_while_every_meter_is_full()
        {
            var db = Mini();
            db.Classes["tank"].BaseStats.Vitality = 30;
            var (s, _) = Battle(db, Stage(("tank", 2, 3)), ("knight", 1, 3), ("cleric", 3, 4));
            foreach (var (u, slot) in Kit(s)) s.SetMana(u.Id, slot, s.ManaFull(u, slot));
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((3, 3)) });
            Assert.That(ev.Any(e => e is ManaGainedEvent), Is.False);
        }

        [Test]
        public void Whole_grants_ignore_fullness_damping_and_cap_at_each_full()
        {
            var db = Mini();
            db.Classes["knight"].BaseStats.Luck = 100; // steal always lands
            var (s, _) = Battle(db, Stage(("tank", 2, 2)), ("knight", 2, 3), ("cleric", 5, 7));
            var k = Hero(s, "knight");
            var c = Hero(s, "cleric");
            k.Bar[0] = new AbilitySlot { AbilityId = "steal" };
            k.Bar[1] = new AbilitySlot { AbilityId = "fireball" };
            c.Bar[0] = new AbilitySlot { AbilityId = "heal" };
            c.Bar[1] = new AbilitySlot { AbilityId = "quicken" };
            int one = BattleConfig.ManaPointsPerOrb;
            s.SetMana(c.Id, 0, s.ManaFull(c, 0));
            s.SetMana(c.Id, 1, one / 2);

            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = k.Id, Slot = 0, Target = k.Pos });

            var g = ev.OfType<ManaGainedEvent>().Single(e => e.Reason == "steal");
            Assert.That(g.Amount, Is.EqualTo(one), "one stolen orb is 100 points, undamped though a meter is full");
            Assert.That(g.Meters.Single(d => d.UnitId == k.Id && d.Slot == 1).Delta, Is.EqualTo(one));
            Assert.That(g.Meters.Single(d => d.UnitId == c.Id && d.Slot == 1).Delta, Is.EqualTo(one / 2), "capped at Quicken's full");
            Assert.That(g.Meters.Any(d => d.UnitId == c.Id && d.Slot == 0), Is.False, "a full meter takes nothing");
        }

        [Test]
        public void Mana_supporters_charge_at_the_support_factor()
        {
            var db = Mini();
            db.Classes["tank"].BaseStats.Vitality = 30;
            db.Classes["mage"].Support = new SupportDef { Effect = SupportEffect.Mana, Magnitude = 40, Target = SupportTarget.Attacker, Range = 2 };
            var (s, _) = Battle(db, Stage(("tank", 2, 3)), ("knight", 1, 3), ("cleric", 3, 4), ("mage", 1, 4));
            Hero(s, "knight").Bar[0] = new AbilitySlot { AbilityId = "fireball" };
            var mage = Hero(s, "mage");
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((3, 3)) });
            var support = ev.OfType<ManaGainedEvent>().Single(c => c.Reason == "support" && c.SourceId == mage.Id);
            Assert.That(IsDampedFrom(support.Amount, (int)System.Math.Round(40 * BattleConfig.SupportManaFactor)), Is.True);
            Assert.That(ev.OfType<ManaGainedEvent>().Any(g => g.Reason == "support" && g.SourceId == mage.Id), Is.True, "the gain reaches the meters");
        }
    }
}
