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
    /// <summary>The scorpion's burrow ambush (GG-T901, BIBLE §14.23): with no hero adjacent it goes under (off the board,
    /// untargetable) leaving a hidden burrow mark on its tile, and surfaces under the nearest hero 1.5 s later as a queued,
    /// telegraphed hazard that strikes and poisons whoever stands there; a hero stepping on the burrow, or a reveal, forces
    /// it up early with the ambush called off. A burrowed foe holds its wave open.</summary>
    public class BurrowAmbushTests
    {
        /// <summary>Mini content plus the real burrows rule, the real Burrow Ambush and Poisoned, a "digger" foe (scorpion
        /// portrait, immobile so only the burrow moves it, the burrow in its kit), and a hero "flare" that reveals a tile.</summary>
        private static ContentDb Db()
        {
            var db = Mini();
            db.Abilities["scorpion_burrow"] = Real.TryAbility("scorpion_burrow");
            db.Buffs["poisoned"] = Real.TryBuff("poisoned");
            var st = new Stats { Strength = 12, Vitality = 20, Agility = 10, Speed = 10, Intelligence = 5 };
            db.Classes["digger"] = new ClassDef { Id = "digger", Name = "Digger", Team = Team.Enemy, Tags = { "Beast" }, Portrait = "scorpion", BaseStats = st, MoveRange = 0, Abilities = { "scorpion_burrow" } };
            RegionRuleCatalog.Index(db, new List<RegionRuleDef> { Real.TryRegionRule("burrows") });
            db.Abilities["flare"] = new AbilityDef
            {
                Id = "flare", Name = "flare", Kind = AbilityKind.Skill, Shape = TargetShape.SingleTile, Mode = TargetMode.PickTile, Filter = TargetFilter.EmptyOnly,
                RuleEffects = { new RuleEffectDef { Kind = RuleEffectKind.RevealTiles, Area = RuleArea.Target } },
            };
            return db;
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

        private static Unit Digger(BattleState s) => s.Units.First(u => u.Def.Id == "digger");

        private static TimelineEntry Ambush(BattleState s, Unit u) =>
            s.Timeline.FirstOrDefault(e => e.Kind == TimelineKind.Hazard && e.SourceUnitId == u.Id && e.RuleEvent == "burrow_ambush");

        // ── Content ──────────────────────────────────────────────────────────────────────────────

        [Test]
        public void The_burrow_content_loads_and_validates()
        {
            var problems = ContentLoader.Validate(Real);
            var mine = problems.Where(p => p.Contains("burrow_mark") || p.Contains("burrow_ambush") || p.Contains("burrows")
                                           || p.Contains("scorpion_burrow") || p.Contains("class scorpion")).ToList();
            Assert.That(mine, Is.Empty, string.Join("\n", mine));

            var a = Real.TryAbility("scorpion_burrow");
            Assert.That(a, Is.Not.Null);
            var burrow = a.RuleEffects.Single(e => e.Kind == RuleEffectKind.Burrow);
            Assert.That(burrow.State, Is.EqualTo("burrow_mark"));
            Assert.That(burrow.Event, Is.EqualTo("burrow_ambush"));
            Assert.That(a.BaseDamage, Is.GreaterThan(0), "it strikes as it surfaces");
            Assert.That(a.ApplyBuff, Is.EqualTo("poisoned"), "and poisons");
            Assert.That(a.CooldownTurns, Is.GreaterThan(0));

            var mark = Real.TryTileState("burrow_mark");
            Assert.That(mark.VisibleSeconds, Is.GreaterThan(0f), "seen going under, then hidden");
            Assert.That(mark.Blocks || mark.Hazard, Is.False, "a hero walks onto it to dig it out");
            var ev = Real.TryRuleEvent("burrow_ambush");
            Assert.That(ev.Seconds, Is.EqualTo(1.5f));
            Assert.That(ev.Hazard, Is.True, "the ambush tile is telegraphed as danger");
            Assert.That(ev.Effects.Any(e => e.Kind == RuleEffectKind.Surface), Is.True);
            var hook = Real.TryRegionRule("burrows").Hooks.Single();
            Assert.That(hook.On, Is.EqualTo(RuleHook.EnterTile));
            Assert.That(hook.State, Is.EqualTo("burrow_mark"));
            Assert.That(hook.Team, Is.EqualTo(RuleTeam.Hero));
            Assert.That(hook.Effects.Any(e => e.Kind == RuleEffectKind.Unburrow), Is.True);

            Assert.That(Real.TryClass("scorpion").Abilities, Does.Contain("scorpion_burrow"));
            Assert.That(Real.Stages.Values.Any(st => st.Waves.SelectMany(w => w.Enemies).Any(e => e.Class == "scorpion")), Is.True, "scorpions fight somewhere");
        }

        // ── The plan ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void A_scorpion_burrows_only_with_no_hero_adjacent_and_off_cooldown()
        {
            var s = Setup(Db(), Stage(("digger", 2, 1)), ("knight", 2, 6, null), ("cleric", 5, 7, null));
            var d = Digger(s);
            Assert.That(EnemyPlanner.PlanBurrow(s, d)?.Id, Is.EqualTo("scorpion_burrow"));
            d.Cooldowns["scorpion_burrow"] = 2;
            Assert.That(EnemyPlanner.PlanBurrow(s, d), Is.Null, "on cooldown");

            var close = Setup(Db(), Stage(("digger", 2, 1)), ("knight", 2, 2, null), ("cleric", 5, 7, null));
            Assert.That(EnemyPlanner.PlanBurrow(close, Digger(close)), Is.Null, "a hero at its side gets stung instead");
        }

        // ── Going under ──────────────────────────────────────────────────────────────────────────

        [Test]
        public void It_goes_under_leaving_its_mark_and_queues_the_ambush_on_the_nearest_hero()
        {
            var s = Setup(Db(), Stage(("digger", 2, 1), ("tank", 7, 0)), ("knight", 2, 5, null), ("cleric", 6, 7, null));
            var d = Digger(s);
            var knight = Hero(s, "knight");
            var at = d.Pos;
            var ev = GuardUntil(s, () => d.Burrowed);
            Assert.That(d.Burrowed, Is.True);
            var went = ev.OfType<BurrowedEvent>().Single();
            Assert.That(went.UnitId, Is.EqualTo(d.Id));
            Assert.That(went.At, Is.EqualTo(at));
            Assert.That(went.TargetId, Is.EqualTo(knight.Id), "the nearest hero");
            Assert.That(went.Ambush, Is.EqualTo(knight.Pos));

            Assert.That(d.Alive || d.OnBoard, Is.False, "off the board");
            Assert.That(s.UnitAt(at), Is.Null, "untargetable: nothing stands on its tile");
            Assert.That(s.LivingList(Team.Enemy), Does.Not.Contain(d));
            Assert.That(s.TurnEntryOf(d.Id), Is.Null, "its turn is off the bar while it is under");
            var mark = s.Region.At(at);
            Assert.That(mark?.State, Is.EqualTo("burrow_mark"));
            Assert.That(mark.OwnerId, Is.EqualTo(d.Id));
            Assert.That(mark.OwnerTeam, Is.EqualTo(Team.Enemy));
            Assert.That(d.Cooldowns.TryGetValue("scorpion_burrow", out var cd) && cd > 0, Is.True, "the burrow recharges");

            var amb = Ambush(s, d);
            Assert.That(amb, Is.Not.Null, "a queued ambush");
            Assert.That(amb.HazardTiles, Is.EqualTo(new List<Coord> { knight.Pos }), "telegraphed under the hero");
            Assert.That(amb.RemainingTicks, Is.LessThanOrEqualTo(Formulas.SecondsToTicks(1.5f)));
            AssertInvariants(s);
        }

        [Test]
        public void The_ambush_surfaces_beside_the_hero_on_its_tile_and_strikes_and_poisons_them()
        {
            var s = Setup(Db(), Stage(("digger", 2, 1), ("tank", 7, 0)), ("knight", 2, 5, null), ("cleric", 6, 7, null));
            var d = Digger(s);
            var knight = Hero(s, "knight");
            GuardUntil(s, () => d.Burrowed);
            var burrow = d.Pos;
            int hp = knight.Hp;
            var ev = GuardUntil(s, () => !d.Burrowed);
            var up = ev.OfType<SurfacedEvent>().Single();
            Assert.That(up.Forced, Is.False);
            Assert.That(up.VictimId, Is.EqualTo(knight.Id), "whoever stands on the ambush tile");
            Assert.That(d.Alive, Is.True, "back on the board");
            Assert.That(d.Pos.Manhattan(knight.Pos), Is.EqualTo(1), "it bursts up right beside them");
            Assert.That(s.Region.At(burrow), Is.Null, "its burrow closes");
            Assert.That(s.TurnEntryOf(d.Id), Is.Not.Null, "its turn is back on the bar");
            Assert.That(ev.OfType<HazardLandedEvent>().Any(h => h.EventId == "burrow_ambush"), Is.True);
            Assert.That(ev.OfType<DamagedEvent>().Any(x => x.TargetId == knight.Id && x.SourceId == d.Id), Is.True, "it strikes");
            Assert.That(knight.Hp, Is.LessThan(hp));
            Assert.That(knight.HasBuff("poisoned"), Is.True, "and poisons");
            AssertInvariants(s);
        }

        // ── Forced up ────────────────────────────────────────────────────────────────────────────

        [Test]
        public void A_hero_stepping_on_the_burrow_forces_it_up_and_calls_the_ambush_off()
        {
            var s = Setup(Db(), Stage(("digger", 2, 2), ("tank", 7, 0)), ("knight", 2, 5, null), ("cleric", 6, 7, null));
            var d = Digger(s);
            var knight = Hero(s, "knight");
            GuardUntil(s, () => d.Burrowed);
            Assert.That(d.Burrowed, Is.True);
            var burrow = d.Pos;
            var amb = Ambush(s, d);
            Assert.That(amb, Is.Not.Null);

            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = knight.Id, Path = P((2, 4), (2, 3), (2, 2)) });
            Assert.That(knight.Pos, Is.EqualTo(burrow));
            var up = ev.OfType<SurfacedEvent>().First();
            Assert.That(up.Forced, Is.True);
            Assert.That(up.VictimId, Is.EqualTo(-1), "forced up, it strikes nobody");
            Assert.That(ev.OfType<HazardRetimedEvent>().Any(h => h.EntryId == amb.Id && h.Cancelled), Is.True, "the ambush is called off");
            Assert.That(ev.OfType<HazardLandedEvent>().Any(h => h.EntryId == amb.Id), Is.False);
            Assert.That(d.Burrowed, Is.False);
            Assert.That(d.OnBoard, Is.True);
            Assert.That(d.Pos.Manhattan(burrow), Is.EqualTo(1), "beside its burrow, the hero standing on it");
            Assert.That(s.Region.At(burrow), Is.Null);
            Assert.That(knight.HasBuff("poisoned"), Is.False, "no sting");
            AssertInvariants(s);
        }

        [Test]
        public void Revealing_the_burrow_forces_it_up_on_its_own_tile()
        {
            var s = Setup(Db(), Stage(("digger", 2, 1), ("tank", 7, 0)), ("knight", 2, 5, "flare"), ("cleric", 6, 7, null));
            var d = Digger(s);
            var knight = Hero(s, "knight");
            GuardUntil(s, () => d.Burrowed);
            var burrow = d.Pos;
            var amb = Ambush(s, d);
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = knight.Id, Slot = 0, Target = burrow });
            var up = ev.OfType<SurfacedEvent>().First();
            Assert.That(up.Forced, Is.True);
            Assert.That(up.To, Is.EqualTo(burrow), "its own tile is free, so it comes up there");
            Assert.That(ev.OfType<HazardRetimedEvent>().Any(h => h.EntryId == amb.Id && h.Cancelled), Is.True);
            Assert.That(d.Alive, Is.True);
            Assert.That(knight.HasBuff("poisoned"), Is.False);
            AssertInvariants(s);
        }

        [Test]
        public void A_lone_burrowed_scorpion_holds_its_wave_open_and_comes_back_up()
        {
            var s = Setup(Db(), Stage(("digger", 2, 1)), ("knight", 2, 5, null), ("cleric", 6, 7, null));
            var d = Digger(s);
            GuardUntil(s, () => d.Burrowed);
            Assert.That(d.Burrowed, Is.True);
            Assert.That(s.IsOver, Is.False, "a foe under the board is still in the fight");
            AssertInvariants(s);
            GuardUntil(s, () => !d.Burrowed);
            Assert.That(d.Alive, Is.True);
            Assert.That(s.IsOver, Is.False);
            AssertInvariants(s);
        }
    }
}
