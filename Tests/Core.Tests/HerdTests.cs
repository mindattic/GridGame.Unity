using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>The Herd (GG-T339, BIBLE §14.18): Street kids spliced with horns and bull-mass, muscle for hire. Their
    /// loyalty is the Stampede — when any ally falls, every bull still standing charges and its next turn comes sooner —
    /// and the crew is a Boss that feeds it, a Bouncer that guards, a Gorer that charges down a line and a Breaker.</summary>
    public class HerdTests
    {
        private static readonly string[] Family = { "minotaur01", "minotaur02", "minotaur03", "minotaur04" };

        [Test]
        public void The_herd_validates_plays_four_roles_and_works_the_street()
        {
            var db = Real;
            var problems = ContentLoader.Validate(db).Where(p => p.Contains("minotaur") || p.Contains("herd") || p.Contains("stampede")
                                                                 || p.Contains("bull_rush") || p.Contains("brass_knuckles")).ToList();
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            // Street territory: the Tithe Moors and Halcyon's underspire.
            var street = db.Campaign.Themes.Where(t => t.Id == "greenvalley" || t.Id == "cityruins").SelectMany(t => t.Stages).ToList();
            foreach (var id in Family)
            {
                var c = db.Classes[id];
                Assert.That(c.HasTag("Sentient"), Is.True, $"{id}: a crew that plans its pincers");
                Assert.That(c.DropTable, Is.EqualTo("herd"), id);
                Assert.That(c.ArtSource, Is.EqualTo(id), $"{id}: owner art, never overwritten by import_art");
                Assert.That(new[] { 8f, 11f, 15f, 18f }, Does.Contain(c.BaseStats.Speed), $"{id}: a speed tier");
                Assert.That(c.Abilities, Does.Contain("herd_stampede"), $"{id}: every bull stampedes");
                Assert.That(c.Resistances[DamageType.Arcane], Is.LessThan(1f), $"{id}: the splice holds its aether in");
                Assert.That(c.Support?.Label, Is.Not.Null.And.Not.Empty, $"{id}: its own named support");
                Assert.That(street.Any(s => db.Stages[s].Waves.Any(w => w.Enemies.Any(e => e.Class == id))), $"{id} works the Street");
            }
            Assert.That(Family.Select(id => db.Classes[id].Brain).Distinct().Count(), Is.EqualTo(4), "four roles");
            Assert.That(db.Classes["minotaur03"].ChargeShape, Is.EqualTo(ChargeShape.Line), "the gorer runs down a line");
            Assert.That(GridGame.Meta.Abilities.IsHeroUsable(db.Abilities["stampede_rush"]), Is.False, "a free enemy-only rush is never learnable");
        }

        [Test]
        public void When_an_ally_falls_the_herd_stampedes_forward_on_the_bar()
        {
            var (s, _) = Battle(Real, Stage(("ganger03", 2, 3), ("minotaur02", 5, 0)), ("paladin", 1, 3), ("barbarian", 3, 4), ("cleric", 5, 7));
            var ganger = s.Units.Single(u => u.Def.Id == "ganger03");
            var bouncer = s.Units.Single(u => u.Def.Id == "minotaur02");
            ganger.Hp = 1;
            var ev = Turn(s, new MoveCommand { HeroId = Hero(s, "barbarian").Id, Path = P((3, 3)) });
            Assume.That(ganger.Alive, Is.False, "the pincer fells the ganger");
            Assert.That(ev.OfType<TriggeredEvent>().Any(t => t.UnitId == bouncer.Id && t.AbilityId == "herd_stampede"), Is.True, "the bull answers its crew");
            var rush = ev.OfType<HastenedEvent>().FirstOrDefault(h => h.UnitId == bouncer.Id);
            Assert.That(rush, Is.Not.Null, "its icon lurches forward");
            Assert.That(rush.ToU, Is.LessThan(rush.FromU), "sooner on the bar");
        }

        [Test]
        public void The_bull_rush_tramples_the_hero_on_its_line_and_spares_the_one_off_it()
        {
            var (s, _) = Battle(Real, Stage(("minotaur03", 2, 0)), ("paladin", 2, 5), ("cleric", 5, 7));
            var paladin = Hero(s, "paladin"); var cleric = Hero(s, "cleric");
            var seen = new List<BattleEvent>();
            for (int i = 0; i < 16 && !seen.OfType<AbilityResolvedEvent>().Any(e => e.AbilityId == "charge_bull_rush") && !s.IsOver; i++)
                seen.AddRange(Turn(s, new ShieldCommand()));
            var queued = seen.OfType<CastQueuedEvent>().FirstOrDefault(e => e.AbilityId == "charge_bull_rush");
            Assert.That(queued, Is.Not.Null, "the gorer lowers its horns");
            Assert.That(queued.LineTiles, Is.Not.Empty, "its line is marked");
            var resolved = seen.OfType<AbilityResolvedEvent>().First(e => e.AbilityId == "charge_bull_rush");
            Assert.That(resolved.Targets, Does.Not.Contain(cleric.Id), "a hero off the line is untouched");
            if (resolved.Targets.Contains(paladin.Id))
                Assert.That(seen.OfType<DamagedEvent>().Any(d => d.TargetId == paladin.Id && d.Amount > 0), Is.True, "the hero on the line is trampled");
        }

        [Test]
        public void The_boss_feeds_its_wounded_crew_and_rushes_them()
        {
            var (s, _) = Battle(Real, Stage(("minotaur01", 1, 0), ("minotaur02", 2, 0)), ("paladin", 0, 7), ("cleric", 5, 7));
            var boss = s.Units.Single(u => u.Def.Id == "minotaur01");
            var bouncer = s.Units.Single(u => u.Def.Id == "minotaur02");
            bouncer.Hp = bouncer.MaxHp / 3;
            int before = bouncer.Hp;
            var seen = new List<BattleEvent>();
            for (int i = 0; i < 16 && !seen.OfType<AbilityResolvedEvent>().Any(e => e.AbilityId == "charge_feed_the_herd") && !s.IsOver; i++)
            {
                var beside = new Coord(boss.Pos.X + (boss.Pos.X < 5 ? 1 : -1), boss.Pos.Y);
                if (s.UnitAt(beside) == null) bouncer.Pos = beside; // keep the crew within the boss's reach
                seen.AddRange(Turn(s, new ShieldCommand()));
            }
            var feed = seen.OfType<AbilityResolvedEvent>().FirstOrDefault(e => e.AbilityId == "charge_feed_the_herd");
            Assert.That(feed, Is.Not.Null, "the boss pays its wounded crew");
            Assert.That(feed.Targets, Does.Contain(bouncer.Id));
            Assert.That(bouncer.Hp, Is.GreaterThan(before));
            Assert.That(seen.OfType<HastenedEvent>().Any(h => h.UnitId == bouncer.Id), Is.True, "fed, it rushes its next turn");
        }

        [Test]
        public void The_bouncers_brass_knuckles_can_slow_a_hero()
        {
            var k = Real.Abilities["brass_knuckles"];
            Assert.That(k.Trigger.On, Is.EqualTo(TriggerOn.OnLandedHit));
            Assert.That(k.Trigger.Effects.Single().Buff, Is.EqualTo("slowed"));
            Assert.That(Real.Classes["minotaur02"].ArmorPlate, Is.GreaterThan(0), "a thick front");
            Assert.That(Real.Classes["minotaur02"].Brain, Is.EqualTo(Brain.Guardian), "paid to stand in front of the crew");
        }

        // ── GG-T897 (§14.18): the Herd's rage ──

        private static void Enrage(Unit u) => u.Buffs.Add(new BuffInstance { Def = Real.Buffs["raging"], Remaining = 99 });

        /// <summary>Shield turns until the gorer's Bull Rush is marked (then <paramref name="onQueued"/> runs once) and
        /// resolves; every event seen.</summary>
        private static List<BattleEvent> RunBullRush(BattleState s, System.Action<CastQueuedEvent> onQueued = null)
        {
            var seen = new List<BattleEvent>();
            bool queued = false;
            for (int i = 0; i < 16 && !seen.OfType<AbilityResolvedEvent>().Any(e => e.AbilityId == "charge_bull_rush") && !s.IsOver; i++)
            {
                seen.AddRange(Turn(s, new ShieldCommand()));
                var q = seen.OfType<CastQueuedEvent>().FirstOrDefault(e => e.AbilityId == "charge_bull_rush");
                if (q != null && !queued && !seen.OfType<AbilityResolvedEvent>().Any(e => e.AbilityId == "charge_bull_rush"))
                {
                    queued = true;
                    onQueued?.Invoke(q);
                }
            }
            return seen;
        }

        [Test]
        public void The_gorer_and_the_breaker_rage_below_half_hp_and_when_one_of_the_herd_falls()
        {
            var db = Real;
            var raging = db.Buffs["raging"];
            Assert.That(raging.Rages, Is.True);
            Assert.That(raging.Kind, Is.EqualTo(BuffKind.Buff));
            Assert.That(raging.OutgoingDamageMultiplier, Is.GreaterThan(1f), "outgoing damage up");
            foreach (var id in new[] { "minotaur03", "minotaur04" })
            {
                Assert.That(db.Classes[id].Abilities, Does.Contain("herd_rage").And.Contain("herd_rage_kin"), id);
                Assert.That(db.Classes[id].Abilities, Does.Contain("herd_stampede"), $"{id}: rage rides alongside the Stampede");
            }
            var low = db.Abilities["herd_rage"].Trigger;
            Assert.That(low.On, Is.EqualTo(TriggerOn.OnLowHp));
            Assert.That(low.Threshold, Is.EqualTo(0.5f));
            Assert.That(low.Effects.Single().Buff, Is.EqualTo("raging"));
            var kin = db.Abilities["herd_rage_kin"].Trigger;
            Assert.That(kin.On, Is.EqualTo(TriggerOn.OnAllyKO));
            Assert.That(kin.Condition.FallenFamily, Is.EqualTo("herd"), "its own kind, not the gangers that hired it");
            Assert.That(kin.Effects.Single().Buff, Is.EqualTo("raging"));
        }

        [Test]
        public void A_raging_bull_is_heavy_and_unafraid()
        {
            var (s, _) = Battle(Real, Stage(("minotaur03", 2, 0)), ("paladin", 2, 5), ("cleric", 5, 7));
            var gorer = s.Units.Single(u => u.Def.Id == "minotaur03");
            Assert.That(gorer.IsHeavy || gorer.IsRaging, Is.False, "calm, it is an ordinary bull");
            Enrage(gorer);
            Assert.That(gorer.IsRaging, Is.True);
            Assert.That(gorer.IsHeavy, Is.True, "Heavy while raging: no shove, pull, swap or drag moves it");
            Assert.That(gorer.WontBudge, Is.True);
        }

        [Test]
        public void When_one_of_the_herd_falls_the_gorer_rages_but_not_for_a_hired_ganger()
        {
            {
                var (s, _) = Battle(Real, Stage(("minotaur04", 2, 3), ("minotaur03", 5, 0)), ("paladin", 1, 3), ("barbarian", 3, 4), ("cleric", 5, 7));
                var breaker = s.Units.Single(u => u.Def.Id == "minotaur04");
                var gorer = s.Units.Single(u => u.Def.Id == "minotaur03");
                breaker.Hp = 1;
                var ev = Turn(s, new MoveCommand { HeroId = Hero(s, "barbarian").Id, Path = P((3, 3)) });
                Assume.That(breaker.Alive, Is.False, "the pincer fells the breaker");
                Assert.That(ev.OfType<TriggeredEvent>().Any(t => t.UnitId == gorer.Id && t.AbilityId == "herd_rage_kin"), Is.True, "the gorer sees red");
                Assert.That(ev.OfType<BuffAppliedEvent>().Any(b => b.UnitId == gorer.Id && b.BuffId == "raging"), Is.True);
                Assert.That(ev.OfType<TriggeredEvent>().Any(t => t.UnitId == gorer.Id && t.AbilityId == "herd_stampede"), Is.True, "and still stampedes");
            }
            {
                var (s, _) = Battle(Real, Stage(("ganger03", 2, 3), ("minotaur03", 5, 0)), ("paladin", 1, 3), ("barbarian", 3, 4), ("cleric", 5, 7));
                var ganger = s.Units.Single(u => u.Def.Id == "ganger03");
                var gorer = s.Units.Single(u => u.Def.Id == "minotaur03");
                ganger.Hp = 1;
                var ev = Turn(s, new MoveCommand { HeroId = Hero(s, "barbarian").Id, Path = P((3, 3)) });
                Assume.That(ganger.Alive, Is.False, "the pincer fells the ganger");
                Assert.That(ev.OfType<TriggeredEvent>().Any(t => t.UnitId == gorer.Id && t.AbilityId == "herd_rage_kin"), Is.False, "hired muscle doesn't rage for the hirer");
                Assert.That(ev.OfType<TriggeredEvent>().Any(t => t.UnitId == gorer.Id && t.AbilityId == "herd_stampede"), Is.True, "it only stampedes");
            }
        }

        [Test]
        public void A_hit_that_drops_the_bull_below_half_hp_enrages_it()
        {
            var (s, _) = Battle(Real, Stage(("minotaur03", 2, 3)), ("paladin", 1, 3), ("barbarian", 3, 4), ("cleric", 5, 7));
            var gorer = s.Units.Single(u => u.Def.Id == "minotaur03");
            gorer.Hp = gorer.MaxHp / 2 + 1;
            var ev = Turn(s, new MoveCommand { HeroId = Hero(s, "barbarian").Id, Path = P((3, 3)) });
            Assume.That(gorer.Alive && gorer.HpFraction < 0.5f, "the pincer wounds it past half without felling it");
            Assert.That(ev.OfType<TriggeredEvent>().Any(t => t.UnitId == gorer.Id && t.AbilityId == "herd_rage"), Is.True);
            Assert.That(gorer.IsRaging, Is.True);
        }

        [Test]
        public void A_raging_gorer_runs_its_line_to_the_edge_knocking_the_hero_on_it_aside()
        {
            var (s, _) = Battle(Real, Stage(("minotaur03", 2, 0)), ("paladin", 2, 5), ("cleric", 5, 7));
            var gorer = s.Units.Single(u => u.Def.Id == "minotaur03");
            var paladin = Hero(s, "paladin"); var cleric = Hero(s, "cleric");
            Enrage(gorer);
            var seen = RunBullRush(s);
            var run = seen.OfType<RampagedEvent>().FirstOrDefault(e => e.UnitId == gorer.Id);
            Assert.That(run, Is.Not.Null, "raging, the bull runs the line itself");
            Assert.That(run.Path, Is.Not.Empty);
            Assert.That(run.Trampled, Does.Not.Contain(cleric.Id), "a hero off the line is untouched");
            if (gorer.Alive) Assert.That(gorer.Pos, Is.EqualTo(run.Path.Last()), "it ends where its run did");
            Assert.That(gorer.Pos, Is.Not.EqualTo(run.From), "it actually travelled");
            var line = seen.OfType<CastQueuedEvent>().First(e => e.AbilityId == "charge_bull_rush").LineTiles;
            Assert.That(run.Path.Last(), Is.EqualTo(line.Last()), "nothing in the way: to the board's edge");
            if (run.Trampled.Contains(paladin.Id))
            {
                Assert.That(seen.OfType<DamagedEvent>().Any(d => d.TargetId == paladin.Id && d.Amount > 0), Is.True, "trampled");
                if (paladin.Alive)
                {
                    var aside = seen.OfType<DisplacedEvent>().FirstOrDefault(d => d.UnitId == paladin.Id && d.ByUnitId == gorer.Id);
                    Assert.That(aside, Is.Not.Null, "knocked aside");
                    Assert.That(line, Does.Not.Contain(paladin.Pos), "off the line");
                    Assert.That(aside.From.Manhattan(aside.To), Is.EqualTo(1), "one tile beside the line");
                }
            }
            AssertInvariants(s);
        }

        [Test]
        public void A_raging_gorer_pulls_up_in_front_of_stone_and_spares_the_hero_beyond()
        {
            var (s, _) = Battle(Real, Stage(("minotaur03", 2, 0)), ("paladin", 2, 6), ("cleric", 5, 7));
            var gorer = s.Units.Single(u => u.Def.Id == "minotaur03");
            var paladin = Hero(s, "paladin");
            Enrage(gorer);
            Coord? stone = null;
            var seen = RunBullRush(s, q =>
            {
                // A stone dropped onto the marked line, two tiles out, after it was marked.
                Assume.That(q.LineTiles.Count, Is.GreaterThanOrEqualTo(3));
                stone = q.LineTiles[1];
                s.Blocks.Add(stone.Value);
            });
            Assume.That(stone, Is.Not.Null);
            var run = seen.OfType<RampagedEvent>().FirstOrDefault(e => e.UnitId == gorer.Id);
            Assert.That(run, Is.Not.Null);
            Assert.That(run.Path.Count, Is.LessThanOrEqualTo(1), "it stops in front of the stone");
            Assert.That(run.Path, Does.Not.Contain(stone.Value));
            Assert.That(run.Trampled, Does.Not.Contain(paladin.Id), "the hero beyond the stone is spared");
            Assert.That(gorer.Pos, Is.Not.EqualTo(stone.Value));
            AssertInvariants(s);
        }

        [Test]
        public void A_rooted_raging_gorer_cannot_start_its_run()
        {
            var (s, _) = Battle(Real, Stage(("minotaur03", 2, 0)), ("paladin", 2, 5), ("cleric", 5, 7));
            var gorer = s.Units.Single(u => u.Def.Id == "minotaur03");
            Enrage(gorer);
            Coord at = gorer.Pos;
            var seen = RunBullRush(s, q =>
            {
                at = gorer.Pos;
                gorer.Buffs.Add(new BuffInstance { Def = Real.Buffs["rooted"], Remaining = 99 });
            });
            Assume.That(seen.OfType<AbilityResolvedEvent>().Any(e => e.AbilityId == "charge_bull_rush"), "the charge resolved");
            Assert.That(seen.OfType<RampagedEvent>().Any(), Is.False, "rooted, it can't run the line");
            Assert.That(gorer.Pos, Is.EqualTo(at), "held to its tile");
        }
    }
}
