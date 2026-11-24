using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>The Choir (GG-T335, BIBLE §14.17): the Pale Concord's engineer-priests. A Cantor whose Tithe Hymn reaches the
    /// whole party and can Silence it, a Psalmist that Empowers the Concord, and a Confessor that heals, cleanses and wards
    /// its most wounded ally. GG-T902: they paint runes, and three singers standing on runes at once close the circle,
    /// which sings up a Hollow while the Concord is thin or empowers the singers; a hero's step scuffs a rune.</summary>
    public class ChoirTests
    {
        private static readonly string[] Family = { "cultist01", "cultist02", "cultist03" };

        [Test]
        public void The_choir_validates_plays_three_roles_and_sings_in_halcyon()
        {
            var db = Real;
            var problems = ContentLoader.Validate(db).Where(p => p.Contains("cultist") || p.Contains("choir") || p.Contains("tithe_hymn")
                                                                 || p.Contains("psalm_of_certainty") || p.Contains("absolution")).ToList();
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            var halcyon = db.Campaign.Themes.Where(t => t.Id == "cityruins").SelectMany(t => t.Stages).ToList();
            foreach (var id in Family)
            {
                var c = db.Classes[id];
                Assert.That(c.HasTag("Sentient"), Is.True, $"{id}: the Concord's own");
                Assert.That(c.DropTable, Is.EqualTo("choir"), id);
                Assert.That(c.Resistances[DamageType.Arcane], Is.LessThan(1f), $"{id}: the hymn is aether");
                Assert.That(c.ChargeAbility, Does.StartWith("charge_"), $"{id}: every Choir role is a song");
                Assert.That(halcyon.Any(s => db.Stages[s].Waves.Any(w => w.Enemies.Any(e => e.Class == id))), $"{id} sings in Halcyon");
            }
            Assert.That(Family.Select(id => db.Classes[id].Brain).Distinct().Count(), Is.EqualTo(3), "three roles");
        }

        [Test]
        public void The_tithe_hymn_reaches_every_hero()
        {
            var (s, _) = Battle(Real, Stage(("cultist01", 2, 0)), ("paladin", 0, 7), ("cleric", 5, 7), ("redmage", 3, 6));
            var seen = new List<BattleEvent>();
            for (int i = 0; i < 16 && !seen.OfType<AbilityResolvedEvent>().Any(e => e.AbilityId == "charge_tithe_hymn") && !s.IsOver; i++)
                seen.AddRange(Turn(s, new ShieldCommand()));
            var hymn = seen.OfType<AbilityResolvedEvent>().FirstOrDefault(e => e.AbilityId == "charge_tithe_hymn");
            Assert.That(hymn, Is.Not.Null, "the cantor charges its hymn");
            foreach (var h in s.Units.Where(u => u.IsHero)) Assert.That(hymn.Targets, Does.Contain(h.Id), h.Def.Id);
            Assert.That(Real.Abilities["charge_tithe_hymn"].ApplyBuff, Is.EqualTo("silenced"));
        }

        [Test]
        public void The_confessor_absolves_its_most_wounded_ally()
        {
            var (s, _) = Battle(Real, Stage(("cultist03", 1, 0), ("cultist01", 4, 0)), ("paladin", 0, 7), ("cleric", 5, 7));
            var cantor = s.Units.Single(u => u.Def.Id == "cultist01");
            cantor.Hp = cantor.MaxHp / 3;
            cantor.Buffs.Add(new BuffInstance { Def = Real.Buffs["blinded"], Remaining = 3 });
            int before = cantor.Hp;
            var seen = new List<BattleEvent>();
            for (int i = 0; i < 16 && !seen.OfType<AbilityResolvedEvent>().Any(e => e.AbilityId == "charge_absolution") && !s.IsOver; i++)
                seen.AddRange(Turn(s, new ShieldCommand()));
            var rite = seen.OfType<AbilityResolvedEvent>().FirstOrDefault(e => e.AbilityId == "charge_absolution");
            Assert.That(rite, Is.Not.Null, "the confessor charges absolution on a wounded ally");
            Assert.That(rite.Targets, Does.Contain(cantor.Id));
            Assert.That(cantor.Hp, Is.GreaterThan(before));
            Assert.That(cantor.HasBuff("blinded"), Is.False, "its debuffs are cleansed");
            Assert.That(cantor.HasBuff("ward"), Is.True);
        }

        [Test]
        public void The_psalm_empowers_the_concord_near_the_psalmist()
        {
            var (s, _) = Battle(Real, Stage(("cultist02", 2, 0), ("cultist03", 4, 0)), ("paladin", 0, 7), ("cleric", 5, 7));
            var confessor = s.Units.Single(u => u.Def.Id == "cultist03");
            var psalmist = s.Units.Single(u => u.Def.Id == "cultist02");
            var seen = new List<BattleEvent>();
            for (int i = 0; i < 16 && !seen.OfType<AbilityResolvedEvent>().Any(e => e.AbilityId == "charge_psalm_of_certainty") && !s.IsOver; i++)
            {
                confessor.Pos = new GridGame.Core.Coord(psalmist.Pos.X + (psalmist.Pos.X < 5 ? 1 : -1), psalmist.Pos.Y); // keep it beside the singer
                seen.AddRange(Turn(s, new ShieldCommand()));
            }
            var psalm = seen.OfType<AbilityResolvedEvent>().FirstOrDefault(e => e.AbilityId == "charge_psalm_of_certainty");
            Assert.That(psalm, Is.Not.Null, "the psalmist charges its psalm");
            Assert.That(seen.OfType<BuffAppliedEvent>().Any(b => b.UnitId == confessor.Id && b.BuffId == "empowered"), Is.True);
        }

        // GG-T902 (BIBLE §14.17): the rune circle. Mini content plus the real rune, the real choirrunes rule and the real
        // Empowered; "singer" is a Choir stand-in (one step a turn, the rune in its kit, no song), "cyberzombie00" a
        // stand-in for the Hollow the circle sings up, "tank" an immobile filler that thickens the Concord.

        private static ContentDb RuneDb()
        {
            var db = Mini();
            db.Abilities["choir_rune"] = Real.TryAbility("choir_rune");
            db.Buffs["empowered"] = Real.Buffs["empowered"];
            var st = new Stats { Strength = 5, Vitality = 20, Agility = 10, Speed = 10, Intelligence = 5 };
            db.Classes["singer"] = new ClassDef { Id = "singer", Name = "Singer", Team = Team.Enemy, Tags = { "Humanoid", "Sentient" }, BaseStats = st, MoveRange = 1, Abilities = { "choir_rune" } };
            db.Classes["cyberzombie00"] = new ClassDef { Id = "cyberzombie00", Name = "Hollow", Team = Team.Enemy, Tags = { "Humanoid" }, BaseStats = st, MoveRange = 0 };
            RegionRuleCatalog.Index(db, new List<RegionRuleDef> { Real.TryRegionRule("choirrunes") });
            return db;
        }

        private static BattleState RuneBattle(int level, (string cls, int x, int y)[] foes, params (string cls, int x, int y)[] heroes)
        {
            var stage = Stage(foes);
            foreach (var e in stage.Waves[0].Enemies) e.Level = level;
            return Battle(RuneDb(), stage, heroes).state;
        }

        private static void Rune(BattleState s, int x, int y, Team owner = Team.Enemy) =>
            s.Region.Tiles[new Coord(x, y)] = new TileMark { State = "choir_rune", Rule = "choirrunes", OwnerTeam = owner };

        private static List<BattleEvent> GuardUntil(BattleState s, System.Func<bool> done, int max = 40)
        {
            var all = new List<BattleEvent>();
            for (int i = 0; i < max && !s.IsOver && !done(); i++)
            {
                all.AddRange(BattleEngine.Apply(s, new ShieldCommand()));
                AssertInvariants(s);
            }
            return all;
        }

        private static int Runes(BattleState s) => s.Region.Tiles.Values.Count(m => m.State == "choir_rune");

        [Test]
        public void The_rune_circle_content_validates_and_every_choir_role_paints_runes()
        {
            var problems = ContentLoader.Validate(Real).Where(p => p.Contains("choir_rune") || p.Contains("choirrunes")).ToList();
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            var rune = Real.TryTileState("choir_rune");
            Assert.That(rune, Is.Not.Null);
            Assert.That(rune.SquishedBy, Is.EqualTo(RuleTeam.Hero), "a hero's step scuffs a rune");
            Assert.That(rune.Blocks || rune.Hazard, Is.False, "a rune is stood on, not walked around");
            Assert.That(rune.Circle, Is.Not.Null);
            Assert.That(rune.Circle.Size, Is.EqualTo(3), "three singers close the circle");
            var summon = rune.Circle.OnSummon.Single();
            Assert.That(summon.Kind, Is.EqualTo(RuleEffectKind.SpawnUnit));
            Assert.That(Real.TryClass(summon.Class), Is.Not.Null);
            Assert.That(rune.Circle.SummonBelow, Is.GreaterThan(rune.Circle.Size), "a whole Choir is thin enough to summon");
            var empower = rune.Circle.OnEmpower.Single();
            Assert.That(empower.Kind, Is.EqualTo(RuleEffectKind.ApplyBuff));
            Assert.That(empower.Buff, Is.EqualTo("empowered"));
            Assert.That(empower.Area, Is.EqualTo(RuleArea.WithState), "it reaches the singers on the runes");

            var a = Real.TryAbility("choir_rune");
            Assert.That(a.Mode, Is.EqualTo(TargetMode.PickTile));
            Assert.That(a.Filter, Is.EqualTo(TargetFilter.EmptyOnly));
            Assert.That(a.CooldownTurns, Is.GreaterThan(0));
            foreach (var id in Family)
            {
                Assert.That(Real.TryClass(id).Abilities, Does.Contain("choir_rune"), id);
                Assert.That(EnemyPlanner.RuneAbility(Real, Real.TryClass(id), out var st)?.Id, Is.EqualTo("choir_rune"), id);
                Assert.That(st, Is.SameAs(rune));
            }
        }

        [Test]
        public void A_singer_paints_its_rune_beside_itself_on_the_side_away_from_the_party()
        {
            var s = RuneBattle(3, new[] { ("singer", 2, 1), ("singer", 0, 0), ("singer", 5, 0) }, ("knight", 0, 7), ("cleric", 5, 7));
            var singer = At(s, 2, 1);
            var plan = EnemyPlanner.PlanRune(s, singer);
            Assert.That(plan, Is.Not.Null);
            Assert.That(plan.Kind, Is.EqualTo(RunePlanKind.Paint));
            Assert.That(plan.Ability.Id, Is.EqualTo("choir_rune"));
            Assert.That(plan.At.Manhattan(singer.Pos), Is.EqualTo(1), "the nearest ring first");
            Assert.That(s.FreeForSpawn(plan.At) && s.Region.At(plan.At) == null, Is.True);
            int Near(Coord c) => s.LivingList(Team.Hero).Min(h => c.Manhattan(h.Pos));
            foreach (var n in new[] { new Coord(1, 1), new Coord(3, 1), new Coord(2, 0), new Coord(2, 2) })
                if (s.FreeForSpawn(n)) Assert.That(Near(plan.At), Is.GreaterThanOrEqualTo(Near(n)), $"{plan.At} is no nearer the party than {n}");
            Assert.That(EnemyPlanner.PlanLob(s, singer), Is.Null, "a rune is never lobbed at the party");
        }

        [Test]
        public void No_ritual_with_fewer_than_three_singers_or_a_hero_at_its_side()
        {
            var two = RuneBattle(3, new[] { ("singer", 2, 1), ("singer", 4, 1) }, ("knight", 0, 7), ("cleric", 5, 7));
            Assert.That(EnemyPlanner.PlanRune(two, At(two, 2, 1)), Is.Null, "two can never close a circle of three: they fight");

            var close = RuneBattle(3, new[] { ("singer", 2, 1), ("singer", 0, 0), ("singer", 5, 0) }, ("knight", 2, 2), ("cleric", 5, 7));
            Assert.That(EnemyPlanner.PlanRune(close, At(close, 2, 1)), Is.Null, "a hero at its side is answered first");
        }

        [Test]
        public void Singers_walk_to_their_own_runes_and_hold_them()
        {
            var s = RuneBattle(3, new[] { ("singer", 0, 0), ("singer", 3, 0), ("singer", 5, 2) }, ("knight", 0, 7), ("cleric", 5, 7));
            Rune(s, 0, 0); Rune(s, 3, 2); Rune(s, 5, 4);
            var holder = At(s, 0, 0);
            var hold = EnemyPlanner.PlanRune(s, holder);
            Assert.That(hold.Kind, Is.EqualTo(RunePlanKind.Hold), "on a rune it stays");

            var a = At(s, 3, 0);
            var walk = EnemyPlanner.PlanRune(s, a);
            Assert.That(walk.Kind, Is.EqualTo(RunePlanKind.Walk));
            Assert.That(walk.Rune, Is.EqualTo(new Coord(3, 2)), "the nearest free rune");
            Assert.That(walk.At, Is.EqualTo(new Coord(3, 1)), "one step closer");

            var b = At(s, 5, 2);
            var other = EnemyPlanner.PlanRune(s, b);
            Assert.That(other.Kind, Is.EqualTo(RunePlanKind.Walk));
            Assert.That(other.Rune, Is.EqualTo(new Coord(5, 4)), "two singers never chase the same rune");
        }

        [Test]
        public void The_circle_closes_on_the_third_singer_and_sings_up_a_hollow_while_the_concord_is_thin()
        {
            var s = RuneBattle(4, new[] { ("singer", 0, 1), ("singer", 2, 1), ("singer", 5, 0) }, ("knight", 0, 7), ("cleric", 5, 7));
            Rune(s, 0, 1); Rune(s, 2, 1); Rune(s, 5, 1);
            var third = At(s, 5, 0);
            var ev = GuardUntil(s, () => Runes(s) == 0, 12);
            var circle = ev.OfType<RuneCircleEvent>().Single();
            Assert.That(circle.UnitId, Is.EqualTo(third.Id), "the third singer steps in and closes it");
            Assert.That(circle.Summoned, Is.True);
            Assert.That(circle.Singers.Count, Is.EqualTo(3));
            var arrived = ev.OfType<ReinforcementsArrivedEvent>().Single();
            var hollow = s.UnitById(arrived.Spawned.Single());
            Assert.That(hollow.Def.Id, Is.EqualTo("cyberzombie00"));
            Assert.That(hollow.Level, Is.EqualTo(third.Level), "sung up at its singer's level");
            Assert.That(Runes(s), Is.Zero, "the runes are spent");
            AssertInvariants(s);
        }

        [Test]
        public void A_full_concord_closes_the_circle_to_empower_its_singers_instead()
        {
            var s = RuneBattle(4, new[] { ("singer", 0, 1), ("singer", 2, 1), ("singer", 5, 0), ("tank", 1, 0), ("tank", 3, 0) }, ("knight", 0, 7), ("cleric", 5, 7));
            Rune(s, 0, 1); Rune(s, 2, 1); Rune(s, 5, 1);
            var singers = s.Units.Where(u => u.Def.Id == "singer").ToList();
            var ev = GuardUntil(s, () => Runes(s) == 0, 12);
            var circle = ev.OfType<RuneCircleEvent>().Single();
            Assert.That(circle.Summoned, Is.False);
            Assert.That(ev.OfType<ReinforcementsArrivedEvent>().Any(), Is.False, "five of the Concord stand: no summons");
            foreach (var u in singers) Assert.That(u.HasBuff("empowered"), Is.True, $"singer {u.Id} is empowered");
            foreach (var t in s.Units.Where(u => u.Def.Id == "tank")) Assert.That(t.HasBuff("empowered"), Is.False, "only the singers on the runes");
            AssertInvariants(s);
        }

        [Test]
        public void A_hero_stepping_on_a_rune_scuffs_it_and_the_circle_cannot_close()
        {
            var s = RuneBattle(4, new[] { ("singer", 0, 0), ("singer", 2, 0), ("singer", 4, 0) }, ("knight", 1, 7), ("cleric", 5, 7));
            Rune(s, 0, 0); Rune(s, 2, 0); Rune(s, 1, 6);
            var knight = Hero(s, "knight");
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = knight.Id, Path = P((1, 6)) });
            Assert.That(ev.OfType<SquishedEvent>().Single().State, Is.EqualTo("choir_rune"));
            Assert.That(s.Region.At(new Coord(1, 6)), Is.Null, "scuffed out");
            Assert.That(Runes(s), Is.EqualTo(2));
            AssertInvariants(s);
        }

        [Test]
        public void Left_alone_a_choir_paints_walks_and_closes_its_circle()
        {
            var s = RuneBattle(4, new[] { ("singer", 0, 0), ("singer", 2, 0), ("singer", 4, 0) }, ("knight", 0, 7), ("cleric", 5, 7));
            var ev = GuardUntil(s, () => false, 16);
            Assert.That(ev.OfType<AnnounceEvent>().Any(a => a.Text.Contains("paints a rune")), Is.True, "they paint");
            Assert.That(ev.OfType<RuneCircleEvent>().Any(), Is.True, "and close the circle");
            AssertInvariants(s);
        }
    }
}
