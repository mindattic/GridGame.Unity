using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T247, GG-T1915 (§3.1.0): the party mana pool — every gain into one pool capped at its capacity, fullness
    /// damping on earned gains, undamped whole grants that lose what doesn't fit, the siphon floor, spending at cast and the
    /// support factor.</summary>
    public class ManaEconomyTests
    {
        private const int Capacity = BattleConfig.PartyManaCapacity;

        /// <summary>An earned gain of <paramref name="baseCharge"/> after any fullness damping (an empty pool … nearly full).</summary>
        public static bool IsDampedFrom(int amount, int baseCharge) =>
            amount <= baseCharge && amount >= System.Math.Max(1, (int)System.Math.Round(baseCharge * (1f - BattleConfig.ChargeFullnessDamping)));

        /// <summary>The engine's damping expression for a pool holding <paramref name="pool"/> points.</summary>
        private static int Damped(int amount, int pool) =>
            System.Math.Max(1, (int)System.Math.Round(amount * (1f - BattleConfig.ChargeFullnessDamping * (pool / (float)Capacity))));

        /// <summary>The knight and the cleric pincer a tank with <paramref name="pool"/> points already in the pool.</summary>
        private static (BattleState s, List<BattleEvent> ev) Pincer(int pool)
        {
            var db = Mini();
            db.Classes["tank"].BaseStats.Vitality = 30; // dented, not killed: the same share both times
            var (s, _) = Battle(db, Stage(("tank", 2, 3)), ("knight", 1, 3), ("cleric", 3, 4));
            s.SetPartyMana(pool);
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((3, 3)) });
            return (s, ev);
        }

        private static int PincerCharge(int pool) => Pincer(pool).ev.OfType<ManaGainedEvent>().Single(c => c.Reason == "pincer").Amount;

        /// <summary>The knight steals (it always lands) with <paramref name="pool"/> points already in the pool.</summary>
        private static (BattleState s, ManaGainedEvent g) Steal(int pool)
        {
            var db = Mini();
            db.Classes["knight"].BaseStats.Luck = 100;
            var (s, _) = Battle(db, Stage(("tank", 2, 2)), ("knight", 2, 3), ("cleric", 5, 7));
            var k = Hero(s, "knight");
            k.Bar[0] = new AbilitySlot { AbilityId = "steal" };
            s.SetPartyMana(pool);
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = k.Id, Slot = 0, Target = k.Pos });
            return (s, ev.OfType<ManaGainedEvent>().Single(e => e.Reason == "steal"));
        }

        [Test]
        public void A_spell_costs_its_cost_times_the_orb_points_from_one_pool_that_starts_empty()
        {
            var (s, _) = Battle(Mini(), Stage(("tank", 2, 2)), ("knight", 2, 3), ("cleric", 5, 7));
            var k = Hero(s, "knight");
            var c = Hero(s, "cleric");
            k.Bar[0] = new AbilitySlot { AbilityId = "fireball" };
            k.Bar[1] = new AbilitySlot { AbilityId = "steal" };
            c.Bar[0] = new AbilitySlot { AbilityId = "heal" };
            Assert.That(s.SpellPrice(k, 0), Is.EqualTo(s.Content.Ability("fireball").Cost * BattleConfig.ManaPointsPerOrb));
            Assert.That(s.SpellPrice(k, 1), Is.EqualTo(0), "a Skill costs nothing from the pool");
            Assert.That(s.SpellPrice(k, 2), Is.EqualTo(0), "an empty slot costs nothing");
            Assert.That(ManaIn(s), Is.EqualTo(0), "the pool starts the battle empty");
            Assert.That(s.SpellReady(k, 0), Is.False);

            s.SetPartyMana(s.SpellPrice(c, 0));
            Assert.That(s.SpellReady(c, 0), Is.True, "the pool reaches the Heal's price");
            Assert.That(s.SpellReady(k, 0), Is.False, "but not the Fireball's");
            Assert.That(BattleEngine.GetSlotStatus(s, k.Id, 0), Is.EqualTo(SlotStatus.Unaffordable));
            s.SetPartyMana(s.SpellPrice(k, 0));
            Assert.That(s.SpellReady(k, 0) && s.SpellReady(c, 0), Is.True, "one pool pays for every hero's spells");
        }

        [Test]
        public void A_gain_fills_the_one_pool_and_points_past_capacity_are_lost()
        {
            var (s, _) = Battle(Mini(), Stage(("tank", 2, 2)), ("knight", 2, 3), ("cleric", 5, 7));
            int gain = BattleConfig.ManaPointsPerOrb + BattleConfig.ManaPointsPerOrb / 2;

            var g = BattleEngine.GainMana(s, gain, "test");
            Assert.That(g.Kind, Is.EqualTo(ManaGainKind.Grant));
            Assert.That((g.Amount, g.Before, g.After, g.Capacity, g.Wasted), Is.EqualTo((gain, 0, gain, Capacity, 0)));
            Assert.That(ManaIn(s), Is.EqualTo(gain));

            Hero(s, "cleric").Hp = 0;
            g = BattleEngine.GainMana(s, Capacity, "test");
            Assert.That((g.Amount, g.Before, g.After), Is.EqualTo((Capacity - gain, gain, Capacity)), "a downed hero changes nothing in the party's pool");
            Assert.That(g.Wasted, Is.EqualTo(gain));
            Assert.That(ManaIn(s), Is.EqualTo(Capacity));
            Assert.That(s.PartyManaFull, Is.True);
            Assert.That(BattleEngine.GainMana(s, 1, "test"), Is.Null, "a full pool doesn't move");
        }

        [Test]
        public void A_siphon_takes_points_from_the_pool_and_floors_at_zero()
        {
            var (s, _) = Battle(Mini(), Stage(("tank", 2, 2)), ("knight", 2, 3), ("cleric", 5, 7));
            int one = BattleConfig.ManaPointsPerOrb, start = one + one / 5;
            s.SetPartyMana(start);

            var g = BattleEngine.GainMana(s, -one, "siphon");
            Assert.That(g.Kind, Is.EqualTo(ManaGainKind.Siphon));
            Assert.That((g.Amount, g.Before, g.After, g.Wasted), Is.EqualTo((-one, start, start - one, 0)));

            g = BattleEngine.GainMana(s, -one, "siphon");
            Assert.That((g.Amount, g.After), Is.EqualTo((-(start - one), 0)), "a siphon takes only what is there");
            Assert.That(ManaIn(s), Is.EqualTo(0));
            Assert.That(BattleEngine.GainMana(s, -one, "siphon"), Is.Null, "an empty pool has nothing to take");
        }

        [Test]
        public void Damping_runs_from_full_rate_at_an_empty_pool_down_by_pool_over_capacity()
        {
            var (s, _) = Battle(Mini(), Stage(("tank", 2, 3)), ("knight", 1, 3), ("cleric", 3, 4));
            Assert.That(BattleEngine.ChargeDamping(s), Is.EqualTo(1f), "an empty pool: nothing to damp");
            foreach (int pool in new[] { Capacity / 4, Capacity / 2, Capacity * 3 / 4, Capacity })
            {
                s.SetPartyMana(pool);
                Assert.That(BattleEngine.ChargeDamping(s), Is.EqualTo(1f - BattleConfig.ChargeFullnessDamping * (pool / (float)Capacity)).Within(1e-5));
            }
            s.SetPartyMana(Capacity - 1);
            Assert.That(BattleEngine.DampedCharge(s, 1), Is.EqualTo(1), "a positive gain never rounds to nothing");
            Assert.That(BattleEngine.DampedCharge(s, 0), Is.EqualTo(0));
        }

        [Test]
        public void A_fuller_pool_charges_slower()
        {
            int empty = PincerCharge(0), mostlyFull = PincerCharge(Capacity * 3 / 4);
            Assert.That(mostlyFull, Is.LessThan(empty));
            Assert.That(mostlyFull, Is.EqualTo(Damped(empty, Capacity * 3 / 4)));
        }

        [Test]
        public void Earning_stops_while_the_pool_is_full()
        {
            var (s, ev) = Pincer(Capacity);
            Assert.That(ev.Any(e => e is ManaGainedEvent), Is.False);
            Assert.That(ManaIn(s), Is.EqualTo(Capacity));
        }

        [Test]
        public void Whole_grants_ignore_fullness_damping()
        {
            int one = BattleConfig.ManaPointsPerOrb, pool = Capacity / 2;
            var (s, g) = Steal(pool);
            Assert.That(g.Kind, Is.EqualTo(ManaGainKind.Grant));
            Assert.That(g.Orbs, Is.EqualTo(1));
            Assert.That(g.Amount, Is.EqualTo(one), "one stolen orb is 100 points, undamped though the pool is half full");
            Assert.That(ManaIn(s), Is.EqualTo(pool + one));
        }

        [Test]
        public void A_grant_loses_the_points_past_capacity_and_reports_them()
        {
            int one = BattleConfig.ManaPointsPerOrb, room = one / 4;
            var (s, g) = Steal(Capacity - room);
            Assert.That((g.Amount, g.After, g.Wasted), Is.EqualTo((room, Capacity, one - room)));
            Assert.That(ManaIn(s), Is.EqualTo(Capacity));

            (s, g) = Steal(Capacity);
            Assert.That(g.Orbs, Is.EqualTo(1), "a grant that finds the pool full is still reported");
            Assert.That((g.Amount, g.Before, g.After, g.Wasted), Is.EqualTo((0, Capacity, Capacity, one)));
            Assert.That(ManaIn(s), Is.EqualTo(Capacity));
        }

        [Test]
        public void A_cast_spends_its_price_from_the_pool_at_cast_start()
        {
            var (s, _) = Battle(Mini(), Stage(("tank", 2, 2)), ("knight", 2, 3), ("cleric", 5, 7));
            var k = Hero(s, "knight");
            var c = Hero(s, "cleric");
            k.Bar[0] = new AbilitySlot { AbilityId = "fireball" };
            c.Bar[0] = new AbilitySlot { AbilityId = "meteor" };
            var tank = s.Units.First(u => u.Def.Id == "tank");
            int extra = BattleConfig.ManaPointsPerOrb / 2, price = s.SpellPrice(k, 0);

            s.SetPartyMana(price - 1);
            var refused = BattleEngine.Apply(s, new UseSlotCommand { HeroId = k.Id, Slot = 0, Target = tank.Pos });
            Assert.That(refused.OfType<AbilityRefusedEvent>().Any(), Is.True, "a pool below the price can't cast");
            Assert.That(refused.OfType<SpellManaSpentEvent>().Any(), Is.False);
            Assert.That(ManaIn(s), Is.EqualTo(price - 1));

            s.SetPartyMana(price + extra);
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = k.Id, Slot = 0, Target = tank.Pos });
            var spent = ev.OfType<SpellManaSpentEvent>().Single();
            Assert.That((spent.UnitId, spent.Slot, spent.AbilityId, spent.Points), Is.EqualTo((k.Id, 0, "fireball", price)));
            Assert.That(ManaIn(s), Is.EqualTo(extra), "only the price leaves the pool");

            int meteorPrice = s.SpellPrice(c, 0);
            s.SetPartyMana(meteorPrice);
            ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = c.Id, Slot = 0, Target = tank.Pos });
            Assert.That(ev.OfType<CastQueuedEvent>().Any(), Is.True, "Meteor has a cast time");
            Assert.That(ev.OfType<SpellManaSpentEvent>().Single().Points, Is.EqualTo(meteorPrice));
            Assert.That(ManaIn(s), Is.EqualTo(0), "the price is paid when the cast starts, not when it lands");
        }

        [Test]
        public void Mana_supporters_charge_at_the_support_factor()
        {
            var db = Mini();
            db.Classes["tank"].BaseStats.Vitality = 30;
            db.Classes["mage"].Support = new SupportDef { Effect = SupportEffect.Mana, Magnitude = 40, Target = SupportTarget.Attacker, Range = 2 };
            var (s, _) = Battle(db, Stage(("tank", 2, 3)), ("knight", 1, 3), ("cleric", 3, 4), ("mage", 1, 4));
            var mage = Hero(s, "mage");
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((3, 3)) });
            var support = ev.OfType<ManaGainedEvent>().Single(c => c.Reason == "support" && c.SourceId == mage.Id);
            Assert.That(support.Kind, Is.EqualTo(ManaGainKind.Earned));
            Assert.That(IsDampedFrom(support.Amount, (int)System.Math.Round(40 * BattleConfig.SupportManaFactor)), Is.True);
            Assert.That(support.After, Is.EqualTo(support.Before + support.Amount), "the gain reaches the pool");
        }
    }
}
