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
    /// <summary>The slime's glob (GG-T894, BIBLE §14.23): slime02/slime03 lob a glob (charge_slime_glob) onto a free tile
    /// within 3 near the party while fewer than 4 slimes stand; left alone for 8 seconds it hatches a slime01 a level below
    /// its slime, a hero who steps on it squishes it for a little mana charge, and fire burns it away.</summary>
    public class SlimeGlobTests
    {
        /// <summary>Mini content plus the real slimeglobs rule, the real lob and element table, a "slimer" foe (slime portrait,
        /// immobile, the lob in its kit), a stand-in slime01 hatchling, and a "painter" rule that can lay a glob at the start.</summary>
        private static ContentDb Db(params RuleEffectDef[] atStart)
        {
            var db = Mini();
            db.Elements = Real.Elements;
            db.Abilities["charge_slime_glob"] = Real.TryAbility("charge_slime_glob");
            var st = new Stats { Strength = 5, Vitality = 20, Agility = 10, Speed = 10, Intelligence = 5 };
            db.Classes["slimer"] = new ClassDef { Id = "slimer", Name = "Slimer", Team = Team.Enemy, Tags = { "Beast" }, Portrait = "slime", BaseStats = st, MoveRange = 0, Abilities = { "charge_slime_glob" } };
            db.Classes["slime01"] = new ClassDef { Id = "slime01", Name = "Slime", Team = Team.Enemy, Tags = { "Beast" }, Portrait = "slime", BaseStats = st, MoveRange = 0 };
            var painter = new RegionRuleDef { Id = "painter", TileStates = { new TileStateDef { Id = "flame", Element = DamageType.Fire } } };
            if (atStart.Length > 0) painter.Hooks.Add(new RuleHookDef { On = RuleHook.BattleStart, Effects = atStart.ToList() });
            RegionRuleCatalog.Index(db, new List<RegionRuleDef> { Real.TryRegionRule("slimeglobs"), painter });
            db.Abilities["flame_toss"] = new AbilityDef
            {
                Id = "flame_toss", Name = "flame_toss", Kind = AbilityKind.Skill, Shape = TargetShape.SingleTile, Mode = TargetMode.PickTile, Filter = TargetFilter.EmptyOnly,
                RuleEffects = { new RuleEffectDef { Kind = RuleEffectKind.MarkTiles, Area = RuleArea.Target, State = "flame" } },
            };
            return db;
        }

        private static StageDef SlimeStage(int level, params (string cls, int x, int y)[] foes)
        {
            var st = Stage(foes);
            foreach (var e in st.Waves[0].Enemies) e.Level = level;
            return st;
        }

        private static BattleState Setup(ContentDb db, StageDef stage, params (string cls, int x, int y, string bar)[] heroes)
        {
            var setup = new BattleSetup { Stage = stage, Seed = 7, UnlockedSlots = 5 };
            foreach (var h in heroes)
            {
                var spec = new HeroSpec { ClassId = h.cls, Start = new Coord(h.x, h.y) };
                if (h.bar != null) spec.Bar.Add(h.bar);
                setup.Party.Add(spec);
            }
            return BattleEngine.Create(db, setup, new List<BattleEvent>());
        }

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

        private static Unit Foe(BattleState s, string cls) => s.Units.First(u => u.Team == Team.Enemy && u.Def.Id == cls);

        private static bool AnyGlob(BattleState s) => s.Region.Tiles.Values.Any(m => m.State == "slime_glob");

        // ── Content ──────────────────────────────────────────────────────────────────────────────

        [Test]
        public void The_glob_content_loads_and_validates()
        {
            var problems = ContentLoader.Validate(Real);
            var mine = problems.Where(p => p.Contains("slime_glob") || p.Contains("slimeglobs") || p.Contains("charge_slime_glob")
                                           || p.Contains("class slime02") || p.Contains("class slime03")).ToList();
            Assert.That(mine, Is.Empty, string.Join("\n", mine));

            var glob = Real.TryTileState("slime_glob");
            Assert.That(glob, Is.Not.Null);
            Assert.That(glob.LifeSeconds, Is.EqualTo(8f));
            Assert.That(glob.SquishedBy, Is.EqualTo(RuleTeam.Hero));
            Assert.That(glob.Blocks || glob.Hazard, Is.False, "a glob is walked onto, not around");
            var hatch = glob.OnExpire.Single();
            Assert.That(hatch.Kind, Is.EqualTo(RuleEffectKind.SpawnUnit));
            Assert.That(hatch.Class, Is.EqualTo("slime01"));
            Assert.That(hatch.FromOwnerLevel, Is.True);
            Assert.That(hatch.Level, Is.EqualTo(-1), "a level below its slime");
            var squish = glob.OnSquish.Single();
            Assert.That(squish.Kind, Is.EqualTo(RuleEffectKind.GrantCharge));
            Assert.That(squish.Amount, Is.InRange(1, BattleConfig.ManaChargePerOrb / 2), "a little charge, not an orb");

            var fire = Real.Elements.Of(DamageType.Fire);
            Assert.That(fire.Tiles.Any(t => t.On == "slime_glob" && t.Kind == TileReactionKind.Clear), Is.True, "fire burns it away");

            var lob = Real.TryAbility("charge_slime_glob");
            Assert.That(lob.Mode, Is.EqualTo(TargetMode.PickTile));
            Assert.That(lob.Filter, Is.EqualTo(TargetFilter.EmptyOnly));
            Assert.That(lob.RuleEffects.Any(e => e.Kind == RuleEffectKind.MarkTiles && e.Area == RuleArea.Target && e.State == "slime_glob"), Is.True);
            foreach (var id in new[] { "slime02", "slime03" })
                Assert.That(Real.TryClass(id).Abilities, Does.Contain("charge_slime_glob"), id);

            foreach (var stage in new[] { "greenvalley-01", "greenvalley-02" })
            {
                var st = Real.Stages[stage];
                Assert.That(st.Waves.SelectMany(w => w.Enemies).Any(e => e.Class == "slime02" || e.Class == "slime03"), Is.True, $"{stage} has a glob-lobbing slime");
            }
        }

        // ── The lob ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void A_slime_plans_its_glob_on_a_free_tile_within_three_nearest_the_party()
        {
            var s = Setup(Db(), SlimeStage(3, ("slimer", 2, 1)), ("knight", 0, 7, null), ("cleric", 5, 7, null));
            var slimer = Foe(s, "slimer");
            var plan = EnemyPlanner.PlanLob(s, slimer);
            Assert.That(plan, Is.Not.Null);
            var (a, at) = plan.Value;
            Assert.That(a.Id, Is.EqualTo("charge_slime_glob"));
            Assert.That(at.Manhattan(slimer.Pos), Is.InRange(1, EnemyPlanner.LobRange));
            Assert.That(s.FreeForSpawn(at) && s.Region.At(at) == null, Is.True, "on a free tile");
            int Near(Coord c) => s.LivingList(Team.Hero).Min(h => c.Manhattan(h.Pos));
            for (int y = 0; y < BattleConfig.Rows; y++)
                for (int x = 0; x < BattleConfig.Cols; x++)
                {
                    var c = new Coord(x, y);
                    int d = c.Manhattan(slimer.Pos);
                    if (d == 0 || d > EnemyPlanner.LobRange || !s.FreeForSpawn(c)) continue;
                    Assert.That(Near(at), Is.LessThanOrEqualTo(Near(c)), $"no free tile in reach is nearer the party than {at} ({c})");
                }
        }

        [Test]
        public void No_glob_while_four_slimes_stand_or_a_hero_is_adjacent()
        {
            var crowd = Setup(Db(), SlimeStage(3, ("slimer", 2, 1), ("slime01", 0, 0), ("slime01", 4, 0), ("slime01", 5, 1)), ("knight", 0, 7, null), ("cleric", 5, 7, null));
            Assert.That(EnemyPlanner.PlanLob(crowd, Foe(crowd, "slimer")), Is.Null, "four slimes are enough");

            var close = Setup(Db(), SlimeStage(3, ("slimer", 2, 1)), ("knight", 2, 2, null), ("cleric", 5, 7, null));
            Assert.That(EnemyPlanner.PlanLob(close, Foe(close, "slimer")), Is.Null, "a hero at its side gets hit instead");
        }

        [Test]
        public void A_lobbed_glob_left_alone_hatches_a_slime_a_level_below_its_own()
        {
            var s = Setup(Db(), SlimeStage(3, ("slimer", 2, 1)), ("knight", 0, 7, null), ("cleric", 5, 7, null));
            var slimer = Foe(s, "slimer");
            GuardUntil(s, () => AnyGlob(s));
            var kv = s.Region.Tiles.First(t => t.Value.State == "slime_glob");
            Assert.That(kv.Value.OwnerTeam, Is.EqualTo(Team.Enemy));
            Assert.That(kv.Value.OwnerId, Is.EqualTo(slimer.Id), "the glob is the slime's");
            Assert.That(slimer.Cooldowns.TryGetValue("charge_slime_glob", out var cd) && cd > 0, Is.True, "and the lob recharges");

            var at = kv.Key;
            var ev = GuardUntil(s, () => s.Region.At(at)?.State != "slime_glob");
            var arrived = ev.OfType<ReinforcementsArrivedEvent>().First();
            var hatchling = s.UnitById(arrived.Spawned.Single());
            Assert.That(hatchling.Def.Id, Is.EqualTo("slime01"));
            Assert.That(hatchling.Level, Is.EqualTo(slimer.Level - 1), "a level below the slime that lobbed it");
            AssertInvariants(s);
        }

        // ── Squish and burn ─────────────────────────────────────────────────────────────────────

        private static RuleEffectDef GlobOn(int x, int y) =>
            new RuleEffectDef { Kind = RuleEffectKind.MarkTiles, Area = RuleArea.Tiles, Tiles = { new Coord(x, y) }, State = "slime_glob" };

        [Test]
        public void A_hero_squishing_a_glob_banks_a_little_mana_charge()
        {
            var st = SlimeStage(1, ("tank", 5, 0));
            st.Rules.Add("painter");
            var s = Setup(Db(GlobOn(1, 6)), st, ("knight", 1, 7, "fireball"), ("cleric", 4, 7, null));
            Assert.That(s.Region.At(new Coord(1, 6))?.State, Is.EqualTo("slime_glob"));
            var knight = Hero(s, "knight");
            int before = s.ManaIn(knight.Id, 0);
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = knight.Id, Path = P((1, 6)) });
            Assert.That(ev.OfType<SquishedEvent>().Single().UnitId, Is.EqualTo(knight.Id));
            Assert.That(s.Region.At(new Coord(1, 6)), Is.Null, "squished");
            var charged = ev.OfType<ManaChargedEvent>().Single(c => c.Reason == "rule:slime_glob");
            Assert.That(charged.Amount, Is.EqualTo(Real.TryTileState("slime_glob").OnSquish.Single().Amount));
            Assert.That(charged.SourceId, Is.EqualTo(knight.Id));
            Assert.That(s.ManaIn(knight.Id, 0), Is.GreaterThanOrEqualTo(System.Math.Min(s.ManaFull(knight, 0), before + charged.Amount)));
            var later = GuardUntil(s, () => false, 12);
            Assert.That(later.OfType<ReinforcementsArrivedEvent>().Any(), Is.False, "a squished glob never hatches");
            AssertInvariants(s);
        }

        [Test]
        public void Fire_burns_a_glob_away_before_it_hatches()
        {
            var st = SlimeStage(1, ("tank", 5, 0));
            st.Rules.Add("painter");
            var s = Setup(Db(GlobOn(2, 4)), st, ("knight", 0, 7, "flame_toss"), ("cleric", 4, 7, null));
            var knight = Hero(s, "knight");
            BattleEngine.Apply(s, new UseSlotCommand { HeroId = knight.Id, Slot = 0, Target = new Coord(2, 4) });
            Assert.That(s.Region.At(new Coord(2, 4)), Is.Null, "the flame burns the glob and is spent");
            var later = GuardUntil(s, () => false, 12);
            Assert.That(later.OfType<ReinforcementsArrivedEvent>().Any(), Is.False, "nothing hatches from ash");
            AssertInvariants(s);
        }
    }
}
