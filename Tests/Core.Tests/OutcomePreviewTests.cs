using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>The outcome preview behind the confirm card and the drag preview (GG-T252, POL-D1).</summary>
    public class OutcomePreviewTests
    {
        private static MoveCommand Pincer(BattleState s) =>
            new MoveCommand { HeroId = s.Units.First(u => u.Def.Id == "cleric").Id, Path = new List<Coord> { new Coord(2, 1) } };

        private static List<(int, int)> Meters(BattleState s) =>
            s.SpellMana.OrderBy(kv => kv.Key).SelectMany(kv => kv.Value.Select(p => (kv.Key, p))).ToList();

        [Test]
        public void A_pincer_preview_shows_damage_on_the_flanked_foe()
        {
            var (s, _) = Battle(Mini(), Stage(("goblin", 2, 2)), ("knight", 2, 3), ("cleric", 2, 0));
            var gob = s.Units.First(u => u.Def.Id == "goblin");
            var p = OutcomePreviewer.Preview(s, Pincer(s));
            Assert.That(p.Refused, Is.False);
            var t = p.Of(gob.Id);
            Assert.That(t, Is.Not.Null);
            Assert.That(t.DamageMax, Is.GreaterThan(0));
            Assert.That(t.DamageMin, Is.LessThanOrEqualTo(t.DamageMax));
            Assert.That(t.KillChance, Is.InRange(0f, 1f));
        }

        [Test]
        public void Previewing_never_changes_the_real_battle()
        {
            var (s, _) = Battle(Mini(), Stage(("goblin", 2, 2)), ("knight", 2, 3), ("cleric", 2, 0));
            var k = s.Units.First(u => u.Def.Id == "knight");
            k.Bar[0] = new AbilitySlot { AbilityId = "fireball" };
            s.SetMana(k.Id, 0, 1);
            var before = s.Units.Select(u => (u.Id, u.Hp, u.Pos)).ToList();
            long clock = s.Clock;
            var meters = Meters(s);
            var rng = s.Rng.Clone();
            OutcomePreviewer.Preview(s, Pincer(s));
            Assert.That(s.Units.Select(u => (u.Id, u.Hp, u.Pos)).ToList(), Is.EqualTo(before));
            Assert.That(s.Clock, Is.EqualTo(clock));
            Assert.That(Meters(s), Is.EqualTo(meters), "no spell meter moves");
            Assert.That(s.Rng.NextUInt(), Is.EqualTo(rng.NextUInt()), "the real dice are untouched");
        }

        [Test]
        public void The_preview_matches_what_the_command_then_does()
        {
            var (s, _) = Battle(Mini(), Stage(("goblin", 2, 2)), ("knight", 2, 3), ("cleric", 2, 0));
            var gob = s.Units.First(u => u.Def.Id == "goblin");
            var p = OutcomePreviewer.Preview(s, Pincer(s));
            int hp = gob.Hp;
            var ev = BattleEngine.Apply(s, Pincer(s));
            int dealt = ev.TakeWhile(e => !(e is TurnStartedEvent)).OfType<DamagedEvent>().Where(d => d.TargetId == gob.Id).Sum(d => d.Amount);
            var t = p.Of(gob.Id);
            Assert.That(dealt, Is.InRange(t.DamageMin, t.DamageMax), "sample 0 replays the real dice, so the real hit is inside the range");
        }

        [Test]
        public void The_preview_reports_the_points_the_action_adds_to_every_spell()
        {
            var (s, _) = Battle(Mini(), Stage(("goblin", 2, 2)), ("knight", 2, 3), ("cleric", 2, 0));
            var k = s.Units.First(u => u.Def.Id == "knight");
            var c = s.Units.First(u => u.Def.Id == "cleric");
            k.Bar[0] = new AbilitySlot { AbilityId = "fireball" };
            c.Bar[0] = new AbilitySlot { AbilityId = "heal" };
            s.SetMana(c.Id, 0, s.ManaFull(c, 0) - 1); // any gain fills the Heal

            var p = OutcomePreviewer.Preview(s, Pincer(s));
            var ev = BattleEngine.Apply(s, Pincer(s));
            var mine = ev.TakeWhile(e => !(e is TurnStartedEvent)).ToList();
            int gained = mine.OfType<ManaGainedEvent>().Where(g => g.Amount > 0).Sum(g => g.Amount);
            int filled = mine.OfType<SpellReadyEvent>().Count();

            Assert.That(p.ManaMax, Is.GreaterThan(0), "a pincer earns mana");
            Assert.That(gained, Is.InRange(p.ManaMin, p.ManaMax), "points to every spell, as the command then adds them");
            Assert.That(filled, Is.InRange(p.FillsMin, p.FillsMax));
            Assert.That(p.FillsMax, Is.EqualTo(1), "only the Heal fills; the Fireball is still charging");
        }
    }
}
