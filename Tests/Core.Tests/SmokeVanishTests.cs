using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>Smoke Vanish (§7, GG-T861, the Black Ninja): smoke on the ninja's tile and its 4 neighbours (blows aimed into it
    /// roll as if Blind, the party's own smoke never fouls the party), then the ninja reappears on a random free safe tile at
    /// least 2 away (battle Rng, so a seed replays it), a foe's charge locked on it re-plans, and a Rooted ninja stays put.</summary>
    public class SmokeVanishTests
    {
        private const string Id = "blackninja_smoke_vanish";

        /// <summary>Mini plus the shipped Smoke Vanish, the real smoke rule, a Blind buff and Rooted.</summary>
        private static ContentDb Db()
        {
            var db = Mini();
            db.Abilities[Id] = Real.Ability(Id);
            RegionRuleCatalog.Index(db, new List<RegionRuleDef> { Real.TryRegionRule("smoke") });
            db.Buffs["blinded"] = new BuffDef { Id = "blinded", Name = "Blinded", Unit = DurationUnit.Turns, Duration = 2, AccuracyMultiplier = 0.6f };
            db.Buffs["rooted"] = new BuffDef { Id = "rooted", Name = "Rooted", Unit = DurationUnit.Turns, Duration = 3, Roots = true };
            return db;
        }

        /// <summary>The ninja (a knight) at (2,4), the mage beside it at (2,5), a sturdy foe beside it at (3,4).</summary>
        private static BattleState Setup() =>
            Battle(Db(), Stage(("tank", 3, 4)), ("knight", 2, 4), ("mage", 2, 5)).state;

        private static List<BattleEvent> Vanish(BattleState s, Unit ninja)
        {
            ninja.Bar[0] = new AbilitySlot { AbilityId = Id };
            ninja.ActedThisWindow = false;
            return BattleEngine.Apply(s, new UseSlotCommand { HeroId = ninja.Id, Slot = 0, Target = ninja.Pos });
        }

        private static bool Smoky(BattleState s, Coord c) => s.Region.At(c)?.State == "smoke";

        [Test]
        public void Smoke_vanish_ships_as_the_black_ninjas_self_skill_with_its_obscuring_smoke()
        {
            var a = Real.Ability(Id);
            Assert.That(a.Kind, Is.EqualTo(AbilityKind.Skill));
            Assert.That(a.Shape, Is.EqualTo(TargetShape.Self));
            Assert.That(a.Mode, Is.EqualTo(TargetMode.Auto));
            Assert.That(a.Vanishes, Is.True);
            Assert.That(a.IsTeleport, Is.False);
            Assert.That(a.Name, Is.EqualTo("Smoke Vanish"), "not the Smoke Bomb item nor the Smoke Veil skill");
            Assert.That(a.RuleEffects.Single().State, Is.EqualTo("smoke"));
            Assert.That(Real.TryTileState("smoke").Obscures, Is.True);
            Assert.That(Real.TryTileState("smoke").Affects, Is.EqualTo(TileAffects.FoesOfOwner));
            Assert.That(Real.Classes["blackninja"].Abilities, Does.Contain(Id));
            Assert.That(Abilities.HasEffect(a), Is.True);
            Assert.That(Abilities.MechanicKey(a), Does.Contain("vanish"));
            var mine = ContentLoader.Validate(Real).Where(p => p.Contains(Id) || p.Contains("state smoke")).ToList();
            Assert.That(mine, Is.Empty, string.Join("\n", mine));
        }

        [Test]
        public void The_ninja_drops_smoke_on_its_tile_and_neighbours_then_reappears_at_least_two_away()
        {
            var s = Setup();
            var ninja = Hero(s, "knight");
            var from = ninja.Pos;

            var ev = Vanish(s, ninja);

            Assert.That(ninja.Pos.Manhattan(from), Is.GreaterThanOrEqualTo(SmokeVanish.MinHop));
            foreach (var c in new[] { from, new Coord(1, 4), new Coord(3, 4), new Coord(2, 3), new Coord(2, 5) })
                Assert.That(Smoky(s, c), Is.True, $"smoke at {c}");
            Assert.That(Smoky(s, new Coord(3, 3)), Is.False, "a diamond, not a square");
            var v = ev.OfType<VanishedEvent>().Single();
            Assert.That((v.From, v.To), Is.EqualTo((from, ninja.Pos)));
            int vi = ev.IndexOf(v);
            Assert.That(ev[vi + 1], Is.InstanceOf<TeleportedEvent>(), "the view's teleport follows the vanish");
            Assert.That(ev.FindIndex(e => e is RegionTilesEvent r && r.State == "smoke"), Is.LessThan(vi), "smoke first, then gone");
            Assert.That(ninja.Cooldowns[Id], Is.GreaterThan(0));
            AssertInvariants(s);
        }

        [Test]
        public void The_landing_comes_from_the_battle_rng_so_a_seed_replays_it()
        {
            var a = Setup(); var b = Setup();
            Vanish(a, Hero(a, "knight"));
            Vanish(b, Hero(b, "knight"));
            Assert.That(Hero(a, "knight").Pos, Is.EqualTo(Hero(b, "knight").Pos));
        }

        [Test]
        public void Landings_skip_occupied_tiles_and_the_cloud_it_leaves()
        {
            var s = Setup();
            var ninja = Hero(s, "knight");
            var lands = SmokeVanish.Landings(s, ninja, ninja.Pos);
            Assert.That(lands, Is.Not.Empty);
            Assert.That(lands.All(c => c.Manhattan(ninja.Pos) >= 2), Is.True);
            Assert.That(lands, Does.Not.Contain(new Coord(3, 4)).And.Not.Contain(new Coord(2, 5)));
        }

        [Test]
        public void Blows_aimed_into_the_partys_smoke_roll_as_if_blind_but_the_party_aims_clear()
        {
            var s = Setup();
            var ninja = Hero(s, "knight");
            var mage = Hero(s, "mage");
            var tank = s.Units.First(u => u.Def.Id == "tank");
            Vanish(s, ninja);

            Assert.That(RegionBoard.ObscuredAim(s, tank, mage), Is.EqualTo(0.6f), "a foe aiming at the mage in the smoke is Blind");
            Assert.That(RegionBoard.ObscuredAim(s, mage, tank), Is.EqualTo(1f), "the party's own smoke never fouls the party");
            Assert.That(RegionBoard.ObscuredAim(s, tank, ninja), Is.EqualTo(1f), "the ninja stands clear now");
            tank.Buffs.Add(new BuffInstance { Def = s.Content.Buffs["blinded"], Remaining = 2 });
            Assert.That(RegionBoard.ObscuredAim(s, tank, mage), Is.EqualTo(1f), "never stacked on a real Blind");
            Assert.That(tank.AimMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void A_foes_charge_locked_on_the_ninja_re_plans_onto_the_nearest_other_hero()
        {
            var s = Setup();
            var ninja = Hero(s, "knight");
            var mage = Hero(s, "mage");
            var tank = s.Units.First(u => u.Def.Id == "tank");
            var entry = new TimelineEntry
            {
                Id = s.NextEntryId++, Kind = TimelineKind.Cast, OwnerId = tank.Id, AbilityId = "charge_bolt", CasterTeam = Team.Enemy,
                IsCharge = true, TargetUnitId = ninja.Id, TargetTile = ninja.Pos, TotalCastTicks = 500, RemainingTicks = 500,
            };
            s.Timeline.Add(entry);

            var ev = Vanish(s, ninja);

            Assert.That(entry.TargetUnitId, Is.EqualTo(mage.Id));
            Assert.That(entry.TargetTile, Is.EqualTo(mage.Pos));
            var r = ev.OfType<ChargeReplannedEvent>().Single();
            Assert.That((r.EntryId, r.OldTargetId, r.NewTargetId), Is.EqualTo((entry.Id, ninja.Id, mage.Id)));
        }

        [Test]
        public void A_rooted_ninja_drops_the_smoke_but_cannot_blink()
        {
            var s = Setup();
            var ninja = Hero(s, "knight");
            var from = ninja.Pos;
            ninja.Buffs.Add(new BuffInstance { Def = s.Content.Buffs["rooted"], Remaining = 3 });

            var ev = Vanish(s, ninja);

            Assert.That(ninja.Pos, Is.EqualTo(from));
            Assert.That(Smoky(s, from), Is.True);
            Assert.That(ev.OfType<StepBlockedEvent>().Any(b => b.UnitId == ninja.Id && b.Reason == "rooted"), Is.True);
            Assert.That(ev.OfType<VanishedEvent>(), Is.Empty);
        }

        [Test]
        public void The_bot_vanishes_when_threatened_and_never_idly()
        {
            var s = Setup();
            Assert.That(SmokeVanish.Value(s, Hero(s, "knight")), Is.GreaterThan(0f), "a foe beside it");

            var calm = Battle(Db(), Stage(("tank", 5, 0)), ("knight", 0, 7), ("mage", 1, 7)).state;
            Assert.That(SmokeVanish.Value(calm, Hero(calm, "knight")), Is.LessThanOrEqualTo(0f), "no threat, no pincer odds");
        }
    }
}