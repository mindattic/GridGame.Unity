using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>The aim-mode catalogue (GG-T2141, plan abilities-screen-and-info §3): every ability aims one way (none, unit,
    /// multi, direction, tile); a PickMulti carries up to MaxPicks units in one command and resolves each in pick order; a
    /// PickDirection picks the caster's neighbour tile one of four ways and anchors its shape there.</summary>
    public class AimModeTests
    {
        private static ContentDb Db()
        {
            var db = Mini();
            db.Abilities["volley"] = new AbilityDef
            {
                Id = "volley", Name = "Volley", Kind = AbilityKind.Skill, BaseDamage = 5, DamageType = DamageType.Physical,
                Shape = TargetShape.SingleActor, Mode = TargetMode.PickMulti, MaxPicks = 2, Filter = TargetFilter.EnemyOnly,
            };
            db.Abilities["sweep"] = new AbilityDef
            {
                Id = "sweep", Name = "Sweep", Kind = AbilityKind.Skill, BaseDamage = 5, DamageType = DamageType.Physical,
                Shape = TargetShape.Row, Mode = TargetMode.PickDirection, Filter = TargetFilter.EnemyOnly,
            };
            return db;
        }

        private static BattleState Setup(string ability, (int x, int y) knight, params (string cls, int x, int y)[] foes)
        {
            var setup = new BattleSetup { Stage = Stage(foes), Seed = 7, UnlockedSlots = 5 };
            var k = new HeroSpec { ClassId = "knight", Start = new Coord(knight.x, knight.y) };
            k.Bar.Add(ability);
            setup.Party.Add(k);
            return BattleEngine.Create(Db(), setup, new List<BattleEvent>());
        }

        // ── The catalogue ────────────────────────────────────────────────────────────────────────

        [Test]
        public void Every_mode_maps_to_its_aim()
        {
            Assert.That(TargetResolver.AimOf(new AbilityDef { Mode = TargetMode.Auto }), Is.EqualTo(Aim.None));
            Assert.That(TargetResolver.AimOf(new AbilityDef { Mode = TargetMode.PickActor }), Is.EqualTo(Aim.Unit));
            Assert.That(TargetResolver.AimOf(new AbilityDef { Mode = TargetMode.PickMulti, MaxPicks = 2 }), Is.EqualTo(Aim.Multi));
            Assert.That(TargetResolver.AimOf(new AbilityDef { Mode = TargetMode.PickDirection }), Is.EqualTo(Aim.Direction));
            Assert.That(TargetResolver.AimOf(new AbilityDef { Mode = TargetMode.PickTile }), Is.EqualTo(Aim.Tile));
            Assert.That(TargetResolver.AimOf(new AbilityDef { Mode = TargetMode.PickActor, Kind = AbilityKind.Passive }), Is.EqualTo(Aim.None), "a passive never aims");
        }

        [Test]
        public void The_real_content_uses_every_aim_and_validates()
        {
            var aims = Real.Abilities.Values.Select(TargetResolver.AimOf).Distinct().ToList();
            Assert.That(aims, Is.EquivalentTo(new[] { Aim.None, Aim.Unit, Aim.Multi, Aim.Direction, Aim.Tile }));

            var turret = Real.TryAbility("machinist_turret");
            Assert.That(TargetResolver.AimOf(turret), Is.EqualTo(Aim.Multi));
            Assert.That(turret.MaxPicks, Is.EqualTo(3));
            Assert.That(TargetResolver.AimOf(Real.TryAbility("skirmisher_runningriot")), Is.EqualTo(Aim.Direction));
            Assert.That(TargetResolver.AimOf(Real.TryAbility("blackninja_smoke_vanish")), Is.EqualTo(Aim.None), "the Smokebomb stays a self move");

            var problems = ContentLoader.Validate(Real).Where(p => p.Contains("PickMulti") || p.Contains("PickDirection") || p.Contains("MaxPicks")).ToList();
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void Validate_flags_a_bad_multi_pick_and_a_bad_direction()
        {
            var db = Db();
            db.Abilities["one_pick"] = new AbilityDef { Id = "one_pick", Name = "x", Kind = AbilityKind.Skill, Shape = TargetShape.SingleActor, Mode = TargetMode.PickMulti, MaxPicks = 1, Filter = TargetFilter.EnemyOnly };
            db.Abilities["stray_picks"] = new AbilityDef { Id = "stray_picks", Name = "x", Kind = AbilityKind.Skill, Shape = TargetShape.SingleActor, Mode = TargetMode.PickActor, MaxPicks = 3, Filter = TargetFilter.EnemyOnly };
            db.Abilities["self_way"] = new AbilityDef { Id = "self_way", Name = "x", Kind = AbilityKind.Skill, Shape = TargetShape.Self, Mode = TargetMode.PickDirection, Filter = TargetFilter.Any };
            var problems = ContentLoader.Validate(db);
            Assert.That(problems.Any(p => p.Contains("one_pick") && p.Contains("PickMulti")), string.Join("\n", problems));
            Assert.That(problems.Any(p => p.Contains("stray_picks") && p.Contains("MaxPicks")), string.Join("\n", problems));
            Assert.That(problems.Any(p => p.Contains("self_way") && p.Contains("PickDirection")), string.Join("\n", problems));
        }

        // ── Direction ────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Direction_tiles_are_the_four_neighbours_past_the_footprint()
        {
            var s = Setup("sweep", (3, 4), ("tank", 0, 0), ("boss", 4, 0));
            var knight = Hero(s, "knight");
            Assert.That(TargetResolver.DirectionTile(knight, Coord.Up), Is.EqualTo(new Coord(3, 3)));
            Assert.That(TargetResolver.DirectionTile(knight, Coord.Right), Is.EqualTo(new Coord(4, 4)));
            Assert.That(TargetResolver.DirectionTile(knight, Coord.Down), Is.EqualTo(new Coord(3, 5)));
            Assert.That(TargetResolver.DirectionTile(knight, Coord.Left), Is.EqualTo(new Coord(2, 4)));

            var boss = s.Units.First(u => u.Def.Id == "boss");
            Assert.That(TargetResolver.DirectionTile(boss, Coord.Right), Is.EqualTo(new Coord(boss.Pos.X + 2, boss.Pos.Y)), "a 2x2 steps past its own tiles");
            Assert.That(TargetResolver.DirectionTile(s.UnitAt(new Coord(0, 0)), Coord.Left), Is.Null, "off the board");
        }

        [Test]
        public void A_direction_pick_is_only_a_neighbour_tile_and_hits_the_row_there()
        {
            var s = Setup("sweep", (3, 4), ("tank", 0, 3), ("tank", 5, 7));
            var knight = Hero(s, "knight");
            var a = BattleEngine.SlotAbility(s, knight, 0);
            Assert.That(TargetResolver.IsValidPick(s, a, knight, new Coord(3, 3)), Is.True, "up: the row above holds a foe");
            Assert.That(TargetResolver.IsValidPick(s, a, knight, new Coord(0, 3)), Is.False, "not a neighbour, though its row holds a foe");
            Assert.That(TargetResolver.IsValidPick(s, a, knight, new Coord(3, 5)), Is.False, "down: nobody in that row");
            Assert.That(BattleEngine.ValidTargets(s, knight.Id, 0), Is.EquivalentTo(new[] { new Coord(3, 3) }));

            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = knight.Id, Slot = 0, Target = new Coord(3, 3) });
            Assert.That(ev.OfType<DamagedEvent>().Select(e => e.TargetId), Does.Contain(s.UnitAt(new Coord(0, 3)).Id));
        }

        // ── Multi ────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void A_multi_pick_resolves_on_every_picked_unit()
        {
            var s = Setup("volley", (3, 7), ("tank", 1, 1), ("tank", 5, 1), ("tank", 3, 3));
            var knight = Hero(s, "knight");
            int a = At(s, 1, 1).Id, b = At(s, 5, 1).Id, c = At(s, 3, 3).Id;
            var cmd = new UseSlotCommand { HeroId = knight.Id, Slot = 0, Target = new Coord(5, 1), Targets = { new Coord(5, 1), new Coord(1, 1) } };
            var ev = BattleEngine.Apply(s, cmd);
            var hit = ev.OfType<DamagedEvent>().Select(e => e.TargetId).ToList();
            Assert.That(hit, Does.Contain(a).And.Contain(b));
            Assert.That(hit, Does.Not.Contain(c));
            Assert.That(hit.IndexOf(b), Is.LessThan(hit.IndexOf(a)), "in pick order");
        }

        [Test]
        public void A_multi_pick_refuses_too_many_or_the_same_unit_twice()
        {
            var s = Setup("volley", (3, 7), ("tank", 1, 1), ("tank", 5, 1), ("tank", 3, 3));
            var knight = Hero(s, "knight");
            var a = BattleEngine.SlotAbility(s, knight, 0);
            var picks3 = new List<Coord> { new Coord(1, 1), new Coord(5, 1), new Coord(3, 3) };
            Assert.That(TargetResolver.IsValidPicks(s, a, knight, picks3), Is.False, "MaxPicks is 2");
            Assert.That(TargetResolver.IsValidPicks(s, a, knight, new List<Coord> { new Coord(1, 1), new Coord(1, 1) }), Is.False, "the same foe twice");
            Assert.That(TargetResolver.IsValidPicks(s, a, knight, new List<Coord> { new Coord(1, 1) }), Is.True, "fewer than MaxPicks is fine");

            var cmd = new UseSlotCommand { HeroId = knight.Id, Slot = 0, Target = picks3[0] };
            cmd.Targets.AddRange(picks3);
            var ev = BattleEngine.Apply(s, cmd);
            Assert.That(ev.OfType<ActionBeganEvent>(), Is.Empty, "refused");
        }

        // ── The command ──────────────────────────────────────────────────────────────────────────

        [Test]
        public void The_codec_round_trips_picks_and_reads_an_old_single_pick()
        {
            var multi = new UseSlotCommand { HeroId = 2, Slot = 1, Target = new Coord(1, 1), Targets = { new Coord(1, 1), new Coord(4, 2) } };
            var json = CommandCodec.ToJson(multi);
            Assert.That(CommandCodec.ToJson(CommandCodec.FromJson(json)), Is.EqualTo(json));
            Assert.That(((UseSlotCommand)CommandCodec.FromJson(json)).Picks, Is.EqualTo(new[] { new Coord(1, 1), new Coord(4, 2) }));

            var single = CommandCodec.ToJObject(new UseSlotCommand { HeroId = 2, Slot = 0, Target = new Coord(3, 3) });
            Assert.That(single["targets"], Is.Null, "a single pick encodes as it always did");

            var old = (UseSlotCommand)CommandCodec.FromJObject(JObject.Parse("{\"type\":\"use\",\"hero\":2,\"slot\":0,\"target\":[3,3],\"player\":0}"));
            Assert.That(old.Targets, Is.Empty);
            Assert.That(old.Picks, Is.EqualTo(new[] { new Coord(3, 3) }));
        }

        [Test]
        public void The_aim_text_reads_each_new_mode()
        {
            var db = Db();
            Assert.That(TargetText.How(db.Abilities["volley"]), Does.Contain("up to 2"));
            Assert.That(TargetText.Who(db.Abilities["volley"]), Does.StartWith("Up to 2 foes"));
            Assert.That(TargetText.How(db.Abilities["sweep"]), Does.Contain("arrows").And.Contain("the whole row"));
        }
    }
}
