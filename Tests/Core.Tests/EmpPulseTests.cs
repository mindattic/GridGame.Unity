using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Bot;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>The Technician's EMP Pulse (GG-T879, BIBLE §7): a Square(1) spell that touches only Tech-tagged foes,
    /// silencing each and staggering its held cast (cancelled past its own length), and whose "emp" tile reactions show and
    /// set off the mines it covers and fry a gremlin's shock mine. The hero bot pulses charging tech foes and seen mines.</summary>
    public class EmpPulseTests
    {
        private static readonly Coord Spot = new Coord(2, 4);

        /// <summary>Mini plus a Tech gremlin, the real Silenced, element table, landmines rule, shock mine, the real Buried
        /// Charge (to lay a mine) and an instant copy of the real EMP Pulse.</summary>
        private static ContentDb Db()
        {
            var db = Mini();
            db.Elements = Real.Elements;
            db.Buffs["silenced"] = Real.Buffs["silenced"];
            db.Traps["shock_mine"] = Real.Traps["shock_mine"];
            db.Abilities["engineer_buried_charge"] = Real.TryAbility("engineer_buried_charge");
            RegionRuleCatalog.Index(db, new List<RegionRuleDef> { Real.TryRegionRule("landmines") });
            db.Classes["gremlin"] = new ClassDef
            {
                Id = "gremlin", Name = "Gremlin", Team = Team.Enemy, Tags = { "Humanoid", EmpPulse.Tag },
                BaseStats = new Stats { Strength = 5, Vitality = 50, Agility = 10, Speed = 1, Intelligence = 10 }, MoveRange = 0,
            };
            var r = Real.TryAbility("technician_emp");
            db.Abilities["emp"] = new AbilityDef
            {
                Id = "emp", Name = r.Name, Kind = AbilityKind.Skill, Shape = r.Shape, Radius = r.Radius, Mode = r.Mode, Filter = r.Filter,
                DamageType = r.DamageType, ApplyBuff = r.ApplyBuff, ApplyBuffChance = 1f, OnlyVsTag = r.OnlyVsTag, CastStagger = r.CastStagger, TileReaction = r.TileReaction,
            };
            return db;
        }

        /// <summary>The knight carries the Buried Charge in slot 0 and the pulse in slot 1; the cleric stands by.</summary>
        private static BattleState Setup(StageDef stage, (int x, int y) knight, (int x, int y) cleric)
        {
            var setup = new BattleSetup { Stage = stage, Seed = 7, UnlockedSlots = 5 };
            var k = new HeroSpec { ClassId = "knight", Start = new Coord(knight.x, knight.y) };
            k.Bar.Add("engineer_buried_charge");
            k.Bar.Add("emp");
            setup.Party.Add(k);
            setup.Party.Add(new HeroSpec { ClassId = "cleric", Start = new Coord(cleric.x, cleric.y) });
            return BattleEngine.Create(Db(), setup, new List<BattleEvent>());
        }

        private static List<BattleEvent> Pulse(BattleState s, Coord at)
        {
            var k = Hero(s, "knight");
            k.ActedThisWindow = false;
            return BattleEngine.Apply(s, new UseSlotCommand { HeroId = k.Id, Slot = 1, Target = at });
        }

        private static Unit Foe(BattleState s, string cls) => s.Units.First(u => u.Def.Id == cls && u.Alive);

        /// <summary>A charge the foe holds, far from landing.</summary>
        private static TimelineEntry Charge(BattleState s, Unit foe, int total = 100, int accumulated = 0)
        {
            var e = new TimelineEntry
            {
                Id = 900 + foe.Id, Kind = TimelineKind.Cast, OwnerId = foe.Id, CasterTeam = Team.Enemy, AbilityId = "charge_bolt",
                IsCharge = true, TotalCastTicks = total, RemainingTicks = total, AccumulatedDelayTicks = accumulated, TargetUnitId = Hero(s, "knight").Id,
            };
            s.Timeline.Add(e);
            return e;
        }

        private static List<BattleEvent> GuardUntil(BattleState s, System.Func<bool> done, int max = 40)
        {
            var all = new List<BattleEvent>();
            for (int i = 0; i < max && !s.IsOver && !done(); i++) all.AddRange(BattleEngine.Apply(s, new ShieldCommand()));
            return all;
        }

        // ── Content ──────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Emp_pulse_ships_as_the_technicians_tech_only_square_spell()
        {
            var problems = ContentLoader.Validate(Real).Where(p => p.Contains("technician_emp") || p.Contains("'emp'") || p.Contains("GG-T879")).ToList();
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            var a = Real.TryAbility("technician_emp");
            Assert.That(a, Is.Not.Null);
            Assert.That(a.Kind, Is.EqualTo(AbilityKind.Spell));
            Assert.That((a.Shape, a.Radius, a.Mode), Is.EqualTo((TargetShape.Square, 1, TargetMode.PickTile)));
            Assert.That(a.OnlyVsTag, Is.EqualTo(EmpPulse.Tag));
            Assert.That(a.ApplyBuff, Is.EqualTo("silenced"));
            Assert.That(a.CastStagger, Is.GreaterThan(0f));
            Assert.That(a.TileReaction, Is.EqualTo("emp"));
            Assert.That(EmpPulse.Is(a), Is.True);
            Assert.That(Real.Classes["technician"].Abilities, Does.Contain("technician_emp"));
            foreach (var id in new[] { "techgremlin00", "cyberzombie00", "mechaarmor00", "purpleprototype00" })
                Assert.That(Real.Classes[id].HasTag(EmpPulse.Tag), Is.True, id);
            var emp = Real.Elements.Reaction("emp");
            Assert.That(emp, Is.Not.Null);
            Assert.That(emp.Tiles.Any(t => t.On == "mine" && t.Into == "armedmine"), Is.True);
            Assert.That(emp.Tiles.Any(t => t.On == "buriedmine" && t.Into == "armedmine"), Is.True);
            Assert.That(emp.Tiles.Any(t => t.On == "shock_mine" && t.Kind == TileReactionKind.Clear), Is.True);
        }

        // ── Foes ─────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void The_pulse_silences_tech_foes_and_leaves_the_rest_alone()
        {
            var s = Setup(Stage(("gremlin", 2, 3), ("goblin", 3, 3)), (2, 6), (4, 7));
            var gremlin = Foe(s, "gremlin");
            var goblin = Foe(s, "goblin");
            Assert.That(TargetResolver.Collect(s, s.Content.TryAbility("emp"), Hero(s, "knight"), new Coord(2, 3)), Is.EquivalentTo(new[] { gremlin }));
            var ev = Pulse(s, new Coord(2, 3));
            Assert.That(gremlin.IsSilenced, Is.True, "the gremlin's relays are jammed");
            Assert.That(goblin.IsSilenced, Is.False, "a goblin has no tech to jam");
            Assert.That(ev.OfType<AbilityResolvedEvent>().Single(r => r.AbilityId == "emp").Targets, Is.EquivalentTo(new[] { gremlin.Id }));
        }

        [Test]
        public void A_pulse_over_no_tech_foe_and_no_mine_is_no_pick()
        {
            var s = Setup(Stage(("goblin", 2, 3), ("gremlin", 5, 0)), (2, 6), (4, 7));
            Assert.That(TargetResolver.IsValidPick(s, s.Content.TryAbility("emp"), Hero(s, "knight"), new Coord(2, 3)), Is.False);
            Assert.That(TargetResolver.IsValidPick(s, s.Content.TryAbility("emp"), Hero(s, "knight"), new Coord(5, 1)), Is.True);
        }

        [Test]
        public void The_pulse_staggers_a_charging_tech_foes_cast()
        {
            var s = Setup(Stage(("gremlin", 2, 3), ("witch", 3, 3)), (2, 6), (4, 7));
            var gremlin = Foe(s, "gremlin");
            s.Timeline.RemoveAll(e => e.Kind == TimelineKind.Cast);
            var cast = Charge(s, gremlin);
            int before = cast.RemainingTicks;
            var ev = Pulse(s, new Coord(2, 3));
            int slip = Formulas.SecondsToTicks(s.Content.TryAbility("emp").CastStagger);
            var st = ev.OfType<CastStaggeredEvent>().SingleOrDefault(x => x.CasterId == gremlin.Id);
            Assert.That(st, Is.Not.Null, "the gremlin's charge slips");
            Assert.That(st.DelayTicks, Is.EqualTo(slip));
            Assert.That(st.Resisted, Is.False, "no Wisdom shrug");
            Assert.That(s.Timeline.Contains(cast), Is.True, "slipped, not cancelled");
            Assert.That(cast.AccumulatedDelayTicks, Is.EqualTo(slip));
            Assert.That(ev.OfType<CastStaggeredEvent>().Any(x => x.CasterId == Foe(s, "witch").Id), Is.False, "the witch is no machine");
        }

        [Test]
        public void A_slip_past_the_casts_own_length_cancels_it()
        {
            var s = Setup(Stage(("gremlin", 2, 3)), (2, 6), (4, 7));
            var gremlin = Foe(s, "gremlin");
            s.Timeline.RemoveAll(e => e.Kind == TimelineKind.Cast);
            var cast = Charge(s, gremlin, total: 100, accumulated: 99);
            var ev = Pulse(s, new Coord(2, 3));
            Assert.That(ev.OfType<CastCancelledEvent>().Any(c => c.CasterId == gremlin.Id && c.Reason == "interrupted"), Is.True);
            Assert.That(s.Timeline.Contains(cast), Is.False);
        }

        // ── Mines ────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void The_pulse_shows_a_hidden_mine_and_sets_it_off()
        {
            var s = Setup(Stage(("tank", 5, 0)), (0, 7), (4, 7));
            BattleEngine.Apply(s, new UseSlotCommand { HeroId = Hero(s, "knight").Id, Slot = 0, Target = Spot });
            GuardUntil(s, () => s.Region.At(Spot)?.Hidden == true);
            var m = s.Region.At(Spot);
            m.OwnerTeam = Team.Enemy; m.OwnerId = -1; // a sapper's mine the party can't see
            Assert.That(RegionBoard.Sees(m, Team.Hero), Is.False);
            var ev = Pulse(s, Spot);
            Assert.That(ev.OfType<TileRevealedEvent>().Any(r => r.At == Spot && r.State == "buriedmine"), Is.True, "it shows");
            Assert.That(s.Region.At(Spot)?.State, Is.EqualTo("armedmine"), "and arms");
            Assert.That(ev.OfType<HazardQueuedEvent>().Any(q => q.EventId == "mineblast"), Is.True, "its fuse runs");
        }

        [Test]
        public void The_pulse_fries_a_gremlins_shock_mine()
        {
            var s = Setup(Stage(("tank", 5, 0)), (0, 7), (4, 7));
            var at = new Coord(1, 2);
            s.Traps.Add(new Trap { At = at, TrapId = "shock_mine", OwnerId = Foe(s, "tank").Id });
            Assert.That(TargetResolver.IsValidPick(s, s.Content.TryAbility("emp"), Hero(s, "knight"), at), Is.True);
            var ev = Pulse(s, at);
            Assert.That(ev.OfType<TrapClearedEvent>().Any(t => t.At == at && t.TrapId == "shock_mine"), Is.True);
            Assert.That(s.TrapAt(at), Is.Null);
        }

        // ── The hero bot ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void The_hero_bot_pulses_charging_tech_foes_and_seen_mines()
        {
            var s = Setup(Stage(("gremlin", 2, 1), ("witch", 5, 1)), (0, 7), (4, 7));
            s.Timeline.RemoveAll(e => e.Kind == TimelineKind.Cast);
            var a = s.Content.TryAbility("emp");
            var knight = Hero(s, "knight");
            var gremlin = Foe(s, "gremlin");
            float idle = EmpPulse.Value(s, knight, a, gremlin.Pos);
            Charge(s, gremlin);
            float charging = EmpPulse.Value(s, knight, a, gremlin.Pos);
            Assert.That(charging, Is.GreaterThan(idle), "a charging tech foe is the prize");
            Assert.That(EmpPulse.Value(s, knight, a, new Coord(5, 1)), Is.EqualTo(0f), "a witch alone: nothing to jam");

            s.Region.Tiles[new Coord(1, 4)] = new TileMark { State = "mine", Rule = "landmines", Hazard = true, OwnerTeam = Team.Enemy };
            Assert.That(EmpPulse.Value(s, knight, a, new Coord(1, 4)), Is.GreaterThan(0f), "a seen mine with no hero beside it");
            s.Region.Tiles[new Coord(1, 6)] = new TileMark { State = "mine", Rule = "landmines", Hazard = true, OwnerTeam = Team.Enemy };
            Assert.That(EmpPulse.Value(s, knight, a, new Coord(1, 6)), Is.LessThan(0f), "setting off a mine beside the knight hurts the party");
            var hidden = new TileMark { State = "buriedmine", Rule = "landmines", Hazard = true, OwnerTeam = Team.Enemy, Hidden = true };
            s.Region.Tiles[new Coord(5, 5)] = hidden;
            Assert.That(EmpPulse.Value(s, knight, a, new Coord(5, 5)), Is.EqualTo(0f), "a hidden mine it has no cause to suspect is no reason");
        }
    }
}
