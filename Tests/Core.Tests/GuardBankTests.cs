using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T189 (§2.5), GG-T1915 (§3.1.0): Guard ends the turn early and banks the unused turn time as one damped
    /// earned gain into the party mana pool.</summary>
    public class GuardBankTests
    {
        private static Unit Wolf(BattleState s) => s.Units.First(u => u.Def.Id == "wolf" && u.Alive);

        /// <summary>A knight with Fireball (2 x 100) and a cleric with Heal (1 x 100), the wolf <paramref name="wolfIp"/>
        /// into its bar.</summary>
        private static (BattleState s, Unit knight, Unit cleric) Party(int wolfIp)
        {
            var (s, _) = Battle(Mini(), Stage(("wolf", 0, 0)), ("knight", 5, 7), ("cleric", 4, 7));
            var k = Hero(s, "knight");
            var c = Hero(s, "cleric");
            k.Bar[0] = new AbilitySlot { AbilityId = "fireball" };
            c.Bar[0] = new AbilitySlot { AbilityId = "heal" };
            Wolf(s).SetIp(s, wolfIp);
            return (s, k, c);
        }

        private static ManaGainedEvent GuardGain(List<BattleEvent> ev) =>
            ev.OfType<ManaGainedEvent>().SingleOrDefault(g => g.Reason == "guard");

        [Test]
        public void Guard_conversion_is_proportional_to_time_left_and_capped()
        {
            Assert.That(BattleEngine.GuardChargeFor(0f), Is.EqualTo(0));
            Assert.That(BattleEngine.GuardChargeFor(-3f), Is.EqualTo(0));
            Assert.That(BattleEngine.GuardChargeFor(float.NaN), Is.EqualTo(0));
            Assert.That(BattleEngine.GuardChargeFor(1.5f), Is.EqualTo((int)System.Math.Round(1.5f * BattleConfig.GuardChargePerSecond)));
            Assert.That(BattleEngine.GuardChargeFor(2f), Is.EqualTo(2 * BattleEngine.GuardChargeFor(1f)));
            Assert.That(BattleEngine.GuardChargeFor(1000f), Is.EqualTo(BattleConfig.GuardMaxCharge));
            Assert.That(BattleEngine.GuardChargeFor(float.MaxValue), Is.EqualTo(BattleConfig.GuardMaxCharge));
        }

        [Test]
        public void Guard_banks_only_the_turn_time_not_yet_used()
        {
            var (s, _) = Battle(Mini(), Stage(("wolf", 0, 0)), ("knight", 5, 7), ("cleric", 4, 7));
            Wolf(s).SetIp(s, BattleConfig.BarLength - 1); // the wolf is a tick from ACT: GG-T311, the window is that 0.1 s (no real-time floor)
            float w = BattleEngine.TurnWindowSeconds(s);
            Assert.That(w, Is.EqualTo(0.1f).Within(1e-4));
            Assert.That(BattleEngine.GuardCharge(s), Is.EqualTo(BattleEngine.GuardChargeFor(w)));
            Assert.That(BattleEngine.GuardCharge(s, w / 2f), Is.EqualTo(BattleEngine.GuardChargeFor(w / 2f)));
            Assert.That(BattleEngine.GuardCharge(s, w), Is.EqualTo(0));
            Assert.That(BattleEngine.GuardCharge(s, w + 5f), Is.EqualTo(0));
        }

        [Test]
        public void Guard_adds_its_charge_to_the_party_pool()
        {
            var (s, _, _) = Party(0);
            int expected = BattleEngine.GuardCharge(s);
            Assert.That(expected, Is.GreaterThan(0));
            Assert.That(BattleEngine.DampedCharge(s, expected), Is.EqualTo(expected), "the pool is empty, so nothing is damped");

            var ev = BattleEngine.Apply(s, new ShieldCommand());

            var g = GuardGain(ev);
            Assert.That(g, Is.Not.Null);
            Assert.That(g.Kind, Is.EqualTo(ManaGainKind.Earned));
            Assert.That(g.Amount, Is.EqualTo(expected));
            Assert.That((g.Before, g.After, g.Capacity, g.Wasted), Is.EqualTo((0, expected, BattleConfig.PartyManaCapacity, 0)));
            Assert.That(ManaIn(s), Is.EqualTo(expected), "one gain into the one pool");
            Assert.That(ev.Any(e => e is ManaGainedEvent m && m.Kind == ManaGainKind.Grant), Is.False, "Guard grants no orb");
        }

        [Test]
        public void Guard_earns_more_with_more_of_the_turn_ahead_up_to_the_cap()
        {
            var (near, _, _) = Party(BattleConfig.BarLength - 1);
            var (far, _, _) = Party(0);
            Assert.That(BattleEngine.GuardCharge(far), Is.GreaterThanOrEqualTo(BattleEngine.GuardCharge(near)));
            Assert.That(BattleEngine.GuardCharge(far), Is.LessThanOrEqualTo(BattleConfig.GuardMaxCharge));
        }

        [Test]
        public void Guard_is_damped_by_how_full_the_pool_is()
        {
            var (s, _, _) = Party(0);
            int before = BattleConfig.PartyManaCapacity / 2;
            s.SetPartyMana(before);
            int raw = BattleEngine.GuardCharge(s);
            int expected = System.Math.Max(1, (int)System.Math.Round(raw * (1f - BattleConfig.ChargeFullnessDamping * (before / (float)BattleConfig.PartyManaCapacity))));
            Assert.That(expected, Is.LessThan(raw));

            var ev = BattleEngine.Apply(s, new ShieldCommand());

            var g = GuardGain(ev);
            Assert.That(g.Kind, Is.EqualTo(ManaGainKind.Earned));
            Assert.That(g.Amount, Is.EqualTo(expected));
            Assert.That((g.Before, g.After), Is.EqualTo((before, before + expected)));
            Assert.That(ManaIn(s), Is.EqualTo(before + expected));
        }

        [Test]
        public void Guard_readies_a_spell_whose_price_the_pool_now_reaches()
        {
            var (s, k, c) = Party(0);
            const int room = 1;
            s.SetPartyMana(s.SpellPrice(c, 0) - room);
            int gain = BattleEngine.DampedCharge(s, BattleEngine.GuardCharge(s));
            Assert.That(gain, Is.GreaterThan(room));
            Assert.That(ManaIn(s) + gain, Is.LessThan(s.SpellPrice(k, 0)), "not enough for the Fireball");

            var ev = BattleEngine.Apply(s, new ShieldCommand());

            Assert.That(GuardGain(ev).Amount, Is.EqualTo(gain));
            Assert.That(s.SpellReady(c, 0), Is.True);
            Assert.That(s.SpellReady(k, 0), Is.False);
            Assert.That(ev.OfType<SpellReadyEvent>().Select(r => (r.UnitId, r.Slot)), Is.EqualTo(new[] { (c.Id, 0) }), "only the Heal crossed its price");
        }

        [Test]
        public void Guard_fills_the_pool_only_to_capacity()
        {
            var (s, _, _) = Party(0);
            const int room = 1;
            s.SetPartyMana(BattleConfig.PartyManaCapacity - room);
            int gain = BattleEngine.DampedCharge(s, BattleEngine.GuardCharge(s));
            Assert.That(gain, Is.GreaterThan(room));

            var ev = BattleEngine.Apply(s, new ShieldCommand());

            var g = GuardGain(ev);
            Assert.That(g.Amount, Is.EqualTo(room));
            Assert.That(g.After, Is.EqualTo(BattleConfig.PartyManaCapacity));
            Assert.That(g.Wasted, Is.EqualTo(gain - room), "the points past capacity are lost");
            Assert.That(ManaIn(s), Is.EqualTo(BattleConfig.PartyManaCapacity));
        }

        [Test]
        public void Guard_earns_nothing_while_the_pool_is_full()
        {
            var (s, _, _) = Party(0);
            ManaFull(s);
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            Assert.That(GuardGain(ev), Is.Null);
            Assert.That(ManaIn(s), Is.EqualTo(BattleConfig.PartyManaCapacity));
        }

        [Test]
        public void Guard_ends_the_turn_and_banks_before_the_enemy_acts()
        {
            var (s, _, _) = Party(BattleConfig.BarLength - 1);
            long clock = s.Clock;
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            int banked = ev.FindIndex(e => e is ManaGainedEvent g && g.Reason == "guard");
            int enemy = ev.FindIndex(e => e is TurnStartedEvent);
            Assert.That(banked, Is.GreaterThanOrEqualTo(0));
            Assert.That(enemy, Is.GreaterThan(banked), "the turn time is banked when Guard is pressed, then the enemy acts");
            Assert.That(s.Clock, Is.GreaterThan(clock));
            Assert.That(ev.Last() is PlayerControlEvent, Is.True);
        }

        [Test]
        public void Guard_drops_a_pending_drag_timer_bank_instead_of_stacking_it()
        {
            var (s, _, _) = Party(BattleConfig.BarLength - 1);
            s.PendingTimeBankSeconds = BattleConfig.DragTimerSeconds;
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            Assert.That(ev.Any(e => e is ManaGainedEvent g && g.Reason == "time bank"), Is.False);
            Assert.That(GuardGain(ev), Is.Not.Null);
        }
    }

    internal static class GuardBankTestExt
    {
        public static void SetIp(this Unit u, BattleState s, int ip) => s.TurnEntryOf(u.Id).Ip = ip;
    }
}
