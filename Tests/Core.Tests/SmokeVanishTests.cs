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
    /// <summary>Smokebomb (§7, GG-T861, GG-T1916, the Black Ninja's Ninjutsu): smoke on the ninja's tile and its 4 neighbours
    /// (blows aimed into it roll as if Blind, the party's own smoke never fouls the party), then the ninja vanishes (off the
    /// board, untargetable) and on its next turn reappears on a random free safe tile at least 2 away (battle Rng, so a seed
    /// replays it), a foe's charge locked on it re-plans, and a Rooted ninja stays put.</summary>
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
            Assert.That(a.Name, Is.EqualTo("Smokebomb"), "the owner's name; the Smoke Bomb item and the Smoke Veil skill stay apart");
            Assert.That(a.Category, Is.EqualTo("Ninjutsu"));
            Assert.That(a.RuleEffects.Single().State, Is.EqualTo("smoke"));
            Assert.That(Real.TryTileState("smoke").Obscures, Is.True);
            Assert.That(Real.TryTileState("smoke").Affects, Is.EqualTo(TileAffects.FoesOfOwner));
            Assert.That(Real.Classes["blackninja"].Abilities, Does.Contain(Id));
            Assert.That(Abilities.HasEffect(a), Is.True);
            Assert.That(Abilities.MechanicKey(a), Does.Contain("vanish"));
            var mine = ContentLoader.Validate(Real).Where(p => p.Contains(Id) || p.Contains("state smoke")).ToList();
            Assert.That(mine, Is.Empty, string.Join("\n", mine));
        }

        /// <summary>The Smokebomb as the window's last action, so the clock runs on to the party's next turn.</summary>
        private static List<BattleEvent> VanishAndWait(BattleState s, Unit ninja)
        {
            ninja.Bar[0] = new AbilitySlot { AbilityId = Id };
            ninja.ActedThisWindow = false;
            return Turn(s, new UseSlotCommand { HeroId = ninja.Id, Slot = 0, Target = ninja.Pos });
        }

        [Test]
        public void The_ninja_drops_smoke_on_its_tile_and_neighbours_then_vanishes_off_the_board()
        {
            var s = Setup();
            var ninja = Hero(s, "knight");
            var from = ninja.Pos;

            var ev = Vanish(s, ninja);

            foreach (var c in new[] { from, new Coord(1, 4), new Coord(3, 4), new Coord(2, 3), new Coord(2, 5) })
                Assert.That(Smoky(s, c), Is.True, $"smoke at {c}");
            Assert.That(Smoky(s, new Coord(3, 3)), Is.False, "a diamond, not a square");
            Assert.That(ninja.Vanished, Is.True);
            Assert.That(ninja.OnBoard, Is.False, "off the board");
            Assert.That(ninja.Hp, Is.GreaterThan(0), "gone, not dead");
            var v = ev.OfType<VanishedEvent>().Single();
            Assert.That(v.From, Is.EqualTo(from));
            Assert.That(ev.OfType<TeleportedEvent>(), Is.Empty, "it does not land until its next turn");
            Assert.That(ev.FindIndex(e => e is RegionTilesEvent r && r.State == "smoke"), Is.LessThan(ev.IndexOf(v)), "smoke first, then gone");
            Assert.That(ninja.Cooldowns[Id], Is.GreaterThan(0));
        }

        [Test]
        public void A_vanished_ninja_is_untargetable_and_still_holds_the_party_in_the_fight()
        {
            var s = Battle(Db(), Stage(("tank", 3, 4)), ("knight", 2, 4)).state;
            var ninja = Hero(s, "knight");
            var from = ninja.Pos;

            Vanish(s, ninja);

            Assert.That(ninja.Alive, Is.False, "nothing can aim at it");
            Assert.That(s.UnitAt(from), Is.Null, "no unit on the tile it left");
            Assert.That(s.LivingList(Team.Hero), Does.Not.Contain(ninja));
            Assert.That(s.Phase, Is.Not.EqualTo(BattlePhase.Defeat), "the party's only hero is hiding, not fallen");
        }

        [Test]
        public void On_its_next_turn_the_ninja_reappears_on_a_random_legal_tile_at_least_two_away()
        {
            var s = Setup();
            var ninja = Hero(s, "knight");
            var from = ninja.Pos;

            var ev = VanishAndWait(s, ninja);

            Assert.That(ninja.Vanished, Is.False);
            Assert.That(ninja.OnBoard, Is.True);
            Assert.That(ninja.Pos.Manhattan(from), Is.GreaterThanOrEqualTo(SmokeVanish.MinHop));
            Assert.That(s.Walkable(ninja.Pos), Is.True);
            Assert.That(s.Units.Count(u => u != ninja && u.Alive && u.Occupies(ninja.Pos)), Is.Zero, "nobody else on its tile");
            var r = ev.OfType<ReappearedEvent>().Single();
            Assert.That((r.From, r.To), Is.EqualTo((from, ninja.Pos)));
            int ri = ev.IndexOf(r);
            Assert.That(ev.IndexOf(ev.OfType<VanishedEvent>().Single()), Is.LessThan(ri));
            Assert.That(ev[ri + 1], Is.InstanceOf<TeleportedEvent>(), "the view's teleport follows the reappearance");
            AssertInvariants(s);
        }

        [Test]
        public void The_landing_comes_from_the_battle_rng_so_a_seed_replays_it()
        {
            var a = Setup(); var b = Setup();
            VanishAndWait(a, Hero(a, "knight"));
            VanishAndWait(b, Hero(b, "knight"));
            Assert.That(Hero(a, "knight").OnBoard, Is.True);
            Assert.That(Hero(a, "knight").Pos, Is.EqualTo(Hero(b, "knight").Pos));
        }

        [Test]
        public void The_abilities_sheet_files_it_under_ninjutsu()
        {
            var s = Setup();
            var ninja = Hero(s, "knight");
            ninja.Bar[0] = new AbilitySlot { AbilityId = Id };
            var path = GridGame.Core.Rules.ActionTree.PathTo(GridGame.Core.Rules.ActionTree.For(s, ninja), 0);
            Assert.That(path.Select(n => n.Name), Is.EqualTo(new[] { "Abilities", "Ninjutsu", "Smokebomb" }));
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
            Assert.That(ninja.OnBoard && !ninja.Vanished, Is.True);
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