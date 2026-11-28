using System.Collections.Generic;
using System.Linq;
using GridGame.Core.Battle;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T189 (§2.5), GG-T1807 (§3.1.0): Guard ends the turn early and banks the unused turn time as a damped
    /// mana gain to every spell meter.</summary>
    public class GuardBankTests
    {
        private static Unit Wolf(BattleState s) => s.Units.First(u => u.Def.Id == "wolf" && u.Alive);

        /// <summary>A knight with Fireball (full at 2 x 100) and a cleric with Heal (full at 1 x 100), the wolf
        /// <paramref name="wolfIp"/> into its bar.</summary>
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

        private static ManaMeterDelta Delta(ManaGainedEvent g, Unit u, int slot) =>
            g.Meters.SingleOrDefault(d => d.UnitId == u.Id && d.Slot == slot);

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
        public void Guard_adds_the_same_points_to_every_spell_meter()
        {
            var (s, k, c) = Party(0);
            int expected = BattleEngine.GuardCharge(s);
            Assert.That(expected, Is.GreaterThan(0));
            Assert.That(BattleEngine.DampedCharge(s, expected), Is.EqualTo(expected), "no meter is full, so nothing is damped");

            var ev = BattleEngine.Apply(s, new ShieldCommand());

            var g = GuardGain(ev);
            Assert.That(g, Is.Not.Null);
            Assert.That(g.Amount, Is.EqualTo(expected));
            Assert.That(g.Kind, Is.EqualTo(ManaGainKind.Earned));
            Assert.That(g.Meters.Count, Is.EqualTo(2), "one meter per Spell slot of every living hero");
            Assert.That(Delta(g, k, 0).After, Is.EqualTo(System.Math.Min(expected, s.ManaFull(k, 0))));
            Assert.That(Delta(g, c, 0).After, Is.EqualTo(System.Math.Min(expected, s.ManaFull(c, 0))));
            Assert.That(Delta(g, k, 0).Full, Is.EqualTo(2 * BattleConfig.ManaPointsPerOrb), "Fireball costs 2");
            Assert.That(Delta(g, c, 0).Full, Is.EqualTo(1 * BattleConfig.ManaPointsPerOrb), "Heal costs 1, so the same points fill it twice as fast");
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
        public void Guard_is_damped_by_the_share_of_full_meters()
        {
            var (s, k, c) = Party(0);
            s.SetMana(c.Id, 0, s.ManaFull(c, 0)); // one of the two meters is full
            int raw = BattleEngine.GuardCharge(s);
            int expected = System.Math.Max(1, (int)System.Math.Round(raw * (1f - BattleConfig.ChargeFullnessDamping * (1 / 2f))));

            var ev = BattleEngine.Apply(s, new ShieldCommand());

            var g = GuardGain(ev);
            Assert.That(g.Amount, Is.EqualTo(expected));
            Assert.That(g.Kind, Is.EqualTo(ManaGainKind.Earned));
            Assert.That(Delta(g, k, 0).Delta, Is.EqualTo(System.Math.Min(expected, s.ManaFull(k, 0))));
            Assert.That(Delta(g, c, 0), Is.Null, "a full meter takes nothing");
        }

        [Test]
        public void Guard_fills_a_meter_only_to_its_full_and_spills_nothing()
        {
            var (s, k, c) = Party(0);
            int gain = BattleEngine.GuardCharge(s);
            const int room = 1;
            Assert.That(gain, Is.GreaterThan(room));
            s.SetMana(c.Id, 0, s.ManaFull(c, 0) - room);

            var ev = BattleEngine.Apply(s, new ShieldCommand());

            var g = GuardGain(ev);
            Assert.That(Delta(g, c, 0).Delta, Is.EqualTo(room));
            Assert.That(Delta(g, c, 0).After, Is.EqualTo(s.ManaFull(c, 0)));
            Assert.That(g.Readied.Select(d => (d.UnitId, d.Slot)), Is.EqualTo(new[] { (c.Id, 0) }));
            Assert.That(ev.OfType<SpellReadyEvent>().Count(r => r.UnitId == c.Id && r.Slot == 0), Is.EqualTo(1));
            Assert.That(Delta(g, k, 0).Delta, Is.EqualTo(gain), "the Heal's leftover does not spill into the Fireball");
        }

        [Test]
        public void Guard_earns_nothing_while_every_spell_meter_is_full()
        {
            var (s, k, c) = Party(0);
            s.SetMana(k.Id, 0, s.ManaFull(k, 0));
            s.SetMana(c.Id, 0, s.ManaFull(c, 0));
            var ev = BattleEngine.Apply(s, new ShieldCommand());
            Assert.That(GuardGain(ev), Is.Null);
            Assert.That(s.ManaIn(k.Id, 0), Is.EqualTo(s.ManaFull(k, 0)));
            Assert.That(s.ManaIn(c.Id, 0), Is.EqualTo(s.ManaFull(c, 0)));
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
