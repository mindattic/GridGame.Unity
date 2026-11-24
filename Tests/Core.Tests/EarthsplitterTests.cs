using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Bot;
using GridGame.Core.Content;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>The Barbarian's Earthsplitter (GG-T869, BIBLE §7 and §14.21): a slam that cracks the picked empty tile beside
    /// the Barbarian and the two beyond it (the real crackingfloor <c>cracked</c>, painted along a <see cref="RuleArea.Line"/>).
    /// A Heavy foe on a crack breaks through at once and falls; anyone else crossing cracks it further. The hero bot aims
    /// it under a Heavy foe or across a foe's way in.</summary>
    public class EarthsplitterTests
    {
        private static readonly Coord Ahead1 = new Coord(2, 6), Ahead2 = new Coord(2, 5), Ahead3 = new Coord(2, 4);

        /// <summary>Mini content plus the real crackingfloor rule and Earthsplitter; the classes named are Heavy.</summary>
        private static ContentDb Db(params string[] heavy)
        {
            var db = Mini();
            db.Abilities["barbarian_earthsplitter"] = Real.TryAbility("barbarian_earthsplitter");
            RegionRuleCatalog.Index(db, new List<RegionRuleDef> { Real.TryRegionRule("crackingfloor") });
            foreach (var id in heavy) db.Classes[id].Heavy = true;
            return db;
        }

        /// <summary>The knight carries Earthsplitter in slot 0.</summary>
        private static BattleState Setup(ContentDb db, StageDef stage, (int x, int y) knight, (int x, int y) cleric)
        {
            var setup = new BattleSetup { Stage = stage, Seed = 7, UnlockedSlots = 5 };
            var k = new HeroSpec { ClassId = "knight", Start = new Coord(knight.x, knight.y) };
            k.Bar.Add("barbarian_earthsplitter");
            setup.Party.Add(k);
            setup.Party.Add(new HeroSpec { ClassId = "cleric", Start = new Coord(cleric.x, cleric.y) });
            return BattleEngine.Create(db, setup, new List<BattleEvent>());
        }

        private static List<BattleEvent> Slam(BattleState s, Coord at) =>
            BattleEngine.Apply(s, new UseSlotCommand { HeroId = Hero(s, "knight").Id, Slot = 0, Target = at });

        private static List<BattleEvent> GuardUntil(BattleState s, System.Func<List<BattleEvent>, bool> done, int max = 40)
        {
            var all = new List<BattleEvent>();
            for (int i = 0; i < max && !s.IsOver && !done(all); i++)
            {
                all.AddRange(BattleEngine.Apply(s, new ShieldCommand()));
                AssertInvariants(s);
            }
            return all;
        }

        private static string StateAt(BattleState s, Coord c) => s.Region.At(c)?.State;

        // ── Content ──────────────────────────────────────────────────────────────────────────────

        [Test]
        public void The_earthsplitter_content_loads_and_validates()
        {
            var problems = ContentLoader.Validate(Real);
            var mine = problems.Where(p => p.Contains("barbarian_earthsplitter") || p.Contains("state cracked") || p.Contains("HeavyBecomes")).ToList();
            Assert.That(mine, Is.Empty, string.Join("\n", mine));
            var a = Real.TryAbility("barbarian_earthsplitter");
            Assert.That(a, Is.Not.Null);
            Assert.That(a.Kind, Is.EqualTo(AbilityKind.Skill));
            Assert.That(a.Mode, Is.EqualTo(TargetMode.PickTile));
            Assert.That(a.Filter, Is.EqualTo(TargetFilter.EmptyOnly));
            Assert.That(a.AdjacentOnly, Is.True);
            Assert.That(a.RuleEffects.Any(fx => fx.Kind == RuleEffectKind.MarkTiles && fx.Area == RuleArea.Line && fx.Count == 3 && fx.State == "cracked"), Is.True);
            Assert.That(Real.Classes["barbarian"].Abilities, Does.Contain("barbarian_earthsplitter"));
            Assert.That(Real.TryTileState("cracked").HeavyBecomes, Is.EqualTo("hole"));
            var wave = Real.TryRegionRule("crackingfloor").Hooks.Single(h => h.On == RuleHook.WaveSpawn);
            Assert.That(wave.StageOnly, Is.True, "a hero's cracks never turn every wave into a cracking floor");
        }

        [Test]
        public void A_line_runs_straight_ahead_from_the_subject_and_stops_at_the_edge()
        {
            Assert.That(RegionRuleCatalog.LineTiles(new Coord(2, 7), new Coord(2, 6), 3), Is.EqualTo(new[] { Ahead1, Ahead2, Ahead3 }));
            Assert.That(RegionRuleCatalog.LineTiles(new Coord(2, 4), new Coord(3, 4), 2), Is.EqualTo(new[] { new Coord(3, 4), new Coord(4, 4) }));
            Assert.That(RegionRuleCatalog.LineTiles(new Coord(1, 3), new Coord(0, 3), 3), Is.EqualTo(new[] { new Coord(0, 3) }), "off-board tiles drop");
            Assert.That(RegionRuleCatalog.LineTiles(new Coord(1, 3), new Coord(1, 3), 3), Is.Empty);
        }

        // ── The slam ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void The_slam_cracks_three_tiles_ahead_as_the_party_and_announces_no_rule()
        {
            var s = Setup(Db(), Stage(("goblin", 5, 0)), (2, 7), (4, 7));
            var ev = Slam(s, Ahead1);
            foreach (var c in new[] { Ahead1, Ahead2, Ahead3 })
            {
                Assert.That(StateAt(s, c), Is.EqualTo("cracked"), $"{c} cracks");
                Assert.That(s.Region.At(c).OwnerTeam, Is.EqualTo(Team.Hero));
            }
            Assert.That(StateAt(s, new Coord(2, 3)), Is.Null, "three tiles, no more");
            Assert.That(ev.OfType<RegionRuleEvent>().Any(), Is.False, "a hero's slam announces no rule");
            Assert.That(s.Region.Tiles.Count, Is.EqualTo(3), "and the rule's wave cracks stay out of it");
            AssertInvariants(s);
        }

        [Test]
        public void A_heavy_foe_on_a_fresh_crack_breaks_through_and_falls_at_once()
        {
            var s = Setup(Db("tank"), Stage(("tank", 2, 5)), (2, 7), (4, 7));
            var tank = s.Units.Single(u => u.Def.Id == "tank");
            Assert.That(tank.IsHeavy, Is.True);
            int hp = tank.Hp;
            var ev = Slam(s, Ahead1);
            Assert.That(ev.OfType<BrokeThroughEvent>().Any(b => b.UnitId == tank.Id && b.At == Ahead2 && b.From == "cracked" && b.Into == "hole"), Is.True);
            Assert.That(ev.OfType<FellEvent>().Any(f => f.UnitId == tank.Id && f.Hole == Ahead2), Is.True, "it falls through");
            Assert.That(StateAt(s, Ahead2), Is.EqualTo("hole"));
            Assert.That(tank.Pos, Is.Not.EqualTo(Ahead2), "and climbs out beside the hole");
            Assert.That(tank.Hp, Is.LessThan(hp), "the fall costs it HP");
            Assert.That(StateAt(s, Ahead1), Is.EqualTo("cracked"));
            AssertInvariants(s);
        }

        [Test]
        public void A_light_foe_on_a_crack_does_not_fall()
        {
            var s = Setup(Db(), Stage(("goblin", 2, 5)), (2, 7), (4, 7));
            var goblin = s.Units.Single(u => u.Def.Id == "goblin");
            var ev = Slam(s, Ahead1);
            Assert.That(ev.OfType<BrokeThroughEvent>().Any(), Is.False);
            Assert.That(ev.OfType<FellEvent>().Any(), Is.False);
            Assert.That(goblin.Pos, Is.EqualTo(Ahead2));
            Assert.That(StateAt(s, Ahead2), Is.EqualTo("cracked"));
        }

        [Test]
        public void A_heavy_foe_walking_onto_a_crack_breaks_through()
        {
            var s = Setup(Db("wolf"), Stage(("wolf", 2, 2)), (2, 7), (4, 7));
            var wolf = s.Units.Single(u => u.Def.Id == "wolf");
            Slam(s, Ahead1);
            var ev = GuardUntil(s, all => all.OfType<BrokeThroughEvent>().Any());
            var broke = ev.OfType<BrokeThroughEvent>().FirstOrDefault();
            Assert.That(broke, Is.Not.Null, "the Heavy wolf's walk in crosses the cracks");
            Assert.That(broke.UnitId, Is.EqualTo(wolf.Id));
            Assert.That(ev.OfType<FellEvent>().Any(f => f.UnitId == wolf.Id && f.Hole == broke.At), Is.True);
        }

        [Test]
        public void Anyone_else_crossing_a_crack_cracks_it_further()
        {
            var s = Setup(Db(), Stage(("goblin", 5, 0)), (2, 7), (1, 5));
            Slam(s, Ahead1);
            var cleric = Hero(s, "cleric");
            BattleEngine.Apply(s, new MoveCommand { HeroId = cleric.Id, Path = P((2, 5), (3, 5)) });
            var m = s.Region.At(Ahead2);
            Assert.That(m?.State, Is.EqualTo("cracked"));
            Assert.That(m.Counter, Is.EqualTo(1), "one more crack under the crossing hero");
            Assert.That(s.Region.At(Ahead3).Counter, Is.EqualTo(0));
        }

        // ── The hero bot ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void The_hero_bot_aims_it_under_a_heavy_foe_or_across_a_foes_way_in()
        {
            var a = Real.TryAbility("barbarian_earthsplitter");
            var heavy = Setup(Db("tank"), Stage(("tank", 2, 5)), (2, 7), (4, 7));
            var knight = Hero(heavy, "knight");
            float under = HeroBot.EarthsplitValue(heavy, a, knight, Ahead1);
            Assert.That(under, Is.GreaterThan(0f), "a crack under the Heavy tank drops it");
            Assert.That(HeroBot.EarthsplitValue(heavy, a, knight, new Coord(1, 7)), Is.EqualTo(0f), "a crack nobody stands on or walks across is superfluous");

            var path = Setup(Db(), Stage(("wolf", 2, 2)), (2, 7), (4, 7));
            float choke = HeroBot.EarthsplitValue(path, a, Hero(path, "knight"), Ahead1);
            Assert.That(choke, Is.GreaterThan(0f), "across the wolf's straight walk in");
            Assert.That(under, Is.GreaterThan(choke), "dropping a Heavy foe beats a chokepoint");
            Assert.That(HeroBot.EarthsplitValue(path, path.Content.TryAbility("fireball"), Hero(path, "knight"), Ahead1), Is.EqualTo(0f), "only a heavy-breaking line counts");
        }
    }
}
