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
    /// <summary>GG-T865: the Assassin's Shadow Swap (bible §7): trade places with a foe within 3, dropping it where the
    /// Assassin stood (into a firewall, onto a mine, beside a pit, into a pincer line) with both running the EnterTile
    /// hooks. Heavy foes refuse unless Floating, Rooted ones hold, and the HeroBot only swaps for the hazard or pincer it
    /// makes.</summary>
    public class ShadowSwapTests
    {
        private const string Id = "assassain_shadow_swap";

        /// <summary>Mini plus the shipped Shadow Swap, the real element table and the firewalls rule; no loot drops.</summary>
        private static ContentDb Db()
        {
            var db = Mini();
            db.Elements = Real.Elements;
            db.Abilities[Id] = Real.Ability(Id);
            RegionRuleCatalog.Index(db, new List<RegionRuleDef> { Real.TryRegionRule("firewalls") });
            db.Classes["goblin"].DropTable = null;
            db.Classes["tank"].DropTable = null;
            return db;
        }

        private static List<BattleEvent> Swap(BattleState s, Unit hero, Unit target)
        {
            hero.Bar[0] = new AbilitySlot { AbilityId = Id };
            hero.ActedThisWindow = false; // one action a window (GG-T308)
            hero.Cooldowns.Clear(); // these tests are about the swap, not its cooldown
            return BattleEngine.Apply(s, new UseSlotCommand { HeroId = hero.Id, Slot = 0, Target = target.Pos });
        }

        private static Unit Enemy(BattleState s, string cls) => s.Units.First(u => u.Def.Id == cls && u.Alive);

        private static void Give(Unit u, BuffDef d) => u.Buffs.Add(new BuffInstance { Def = d, Remaining = d.Duration });

        private static void PaintFirewall(BattleState s, Coord at)
        {
            if (!s.Region.Rules.Contains("firewalls")) s.Region.Rules.Add("firewalls");
            s.Region.Tiles[at] = new TileMark { State = "firewall", Rule = "firewalls", Hazard = s.Content.TryTileState("firewall")?.Hazard ?? true, OwnerTeam = Team.Hero };
        }

        [Test]
        public void Shadow_swap_is_the_assassins_ranged_swap_skill()
        {
            var a = Real.Ability(Id);
            Assert.That(a.Kind, Is.EqualTo(AbilityKind.Skill));
            Assert.That(a.Mode, Is.EqualTo(TargetMode.PickActor));
            Assert.That(a.Filter, Is.EqualTo(TargetFilter.EnemyOnly));
            Assert.That(a.SwapsPlaces, Is.True);
            Assert.That(a.MaxRange, Is.EqualTo(3));
            Assert.That(a.AdjacentOnly, Is.False);
            Assert.That(a.BaseDamage, Is.EqualTo(0), "it does nothing but move: never superfluous");
            Assert.That(a.CooldownTurns, Is.GreaterThan(0));
            Assert.That(Real.Classes["assassain"].Abilities, Does.Contain(Id));
            var mine = ContentLoader.Validate(Real).Where(p => p.Contains(Id)).ToList();
            Assert.That(mine, Is.Empty, string.Join("\n", mine));
        }

        [Test]
        public void It_picks_a_foe_within_three_tiles_and_no_further()
        {
            var (s, _) = Battle(Db(), Stage(("goblin", 2, 3), ("tank", 6, 6)), ("knight", 2, 6), ("mage", 1, 6));
            var a = s.Content.TryAbility(Id);
            var k = Hero(s, "knight");
            Assert.That(TargetResolver.IsValidPick(s, a, k, new Coord(2, 3)), Is.True, "3 tiles off");
            Assert.That(TargetResolver.IsValidPick(s, a, k, new Coord(6, 6)), Is.False, "4 tiles off");
            Assert.That(TargetResolver.IsValidPick(s, a, k, new Coord(1, 6)), Is.False, "not an ally");
            Assert.That(TargetResolver.IsValidPick(s, a, k, new Coord(3, 3)), Is.False, "not an empty tile");
        }

        [Test]
        public void The_assassin_and_the_foe_trade_tiles_with_a_swap_callout()
        {
            var (s, _) = Battle(Db(), Stage(("goblin", 2, 3)), ("knight", 2, 6), ("mage", 5, 7));
            var gob = Enemy(s, "goblin");
            var k = Hero(s, "knight");
            var ev = Swap(s, k, gob);
            Assert.That(k.Pos, Is.EqualTo(new Coord(2, 3)));
            Assert.That(gob.Pos, Is.EqualTo(new Coord(2, 6)));
            var moved = ev.OfType<DisplacedEvent>().ToList();
            Assert.That(moved.Any(d => d.UnitId == k.Id && d.To == new Coord(2, 3)), Is.True);
            Assert.That(moved.Any(d => d.UnitId == gob.Id && d.To == new Coord(2, 6)), Is.True);
            Assert.That(ev.OfType<AnnounceEvent>().Any(x => x.Text == "Swap!"), Is.True);
            AssertInvariants(s);
        }

        [Test]
        public void A_foe_dropped_into_the_firewall_the_assassin_stood_in_burns()
        {
            var (s, _) = Battle(Db(), Stage(("tank", 2, 3)), ("knight", 2, 6), ("mage", 5, 7));
            var tank = Enemy(s, "tank");
            var k = Hero(s, "knight");
            PaintFirewall(s, new Coord(2, 6));
            Assert.That(tank.HasBuff("burning"), Is.False);
            Swap(s, k, tank);
            Assert.That(tank.Pos, Is.EqualTo(new Coord(2, 6)), "dropped where the assassin stood");
            Assert.That(tank.HasBuff("burning"), Is.True, "its EnterTile hook ran for the foe");
            Assert.That(k.HasBuff("burning"), Is.False, "the assassin stepped out of it");
            AssertInvariants(s);
        }

        [Test]
        public void A_heavy_foe_refuses_unless_it_floats_and_a_rooted_one_holds()
        {
            var db = Db();
            db.Classes["goblin"].Heavy = true;
            var (s, _) = Battle(db, Stage(("goblin", 2, 3), ("wolf", 4, 6)), ("knight", 2, 6), ("mage", 5, 7));
            var gob = Enemy(s, "goblin"); var wolf = Enemy(s, "wolf");
            var k = Hero(s, "knight");

            var ev = Swap(s, k, gob);
            Assert.That((k.Pos, gob.Pos), Is.EqualTo((new Coord(2, 6), new Coord(2, 3))), "Heavy: it won't trade places");
            Assert.That(ev.OfType<AnnounceEvent>().Any(x => x.Text == "Too heavy!"), Is.True);
            Assert.That(HeroBot.SwapValue(s, k, gob), Is.EqualTo(-5f), "the bot won't try");

            Give(wolf, new BuffDef { Id = "rooted", Name = "Rooted", Kind = BuffKind.Debuff, Unit = DurationUnit.Turns, Duration = 2, Roots = true });
            Swap(s, k, wolf);
            Assert.That((k.Pos, wolf.Pos), Is.EqualTo((new Coord(2, 6), new Coord(4, 6))), "Rooted: it holds");
            Assert.That(HeroBot.SwapValue(s, k, wolf), Is.EqualTo(-5f));

            Give(gob, new BuffDef { Id = Unit.FloatingBuffId, Name = "Floating", Kind = BuffKind.Buff, Unit = DurationUnit.Turns, Duration = 3 });
            Swap(s, k, gob);
            Assert.That((k.Pos, gob.Pos), Is.EqualTo((new Coord(2, 3), new Coord(2, 6))), "a floating Heavy foe is swapped");
            AssertInvariants(s);
        }

        [Test]
        public void A_foe_dropped_into_a_pincer_line_is_caught_at_once()
        {
            // The knight stands between the cleric and the mage in column 2; the goblin is two tiles to its right.
            var (s, _) = Battle(Db(), Stage(("goblin", 4, 2)), ("cleric", 2, 1), ("knight", 2, 2), ("mage", 2, 3));
            var gob = Enemy(s, "goblin");
            var k = Hero(s, "knight");
            Assert.That(HeroBot.SwapValue(s, k, gob), Is.GreaterThan(0f), "the bot sees the pincer it would close");
            Assert.That((k.Pos, gob.Pos), Is.EqualTo((new Coord(2, 2), new Coord(4, 2))), "looking moves nothing");

            var ev = Swap(s, k, gob);
            Assert.That(ev.OfType<PincerFormedEvent>().Any(p => p.Team == Team.Hero && p.Opponents.Contains(gob.Id)), Is.True,
                "dropped between the cleric and the mage: pincered");
            AssertInvariants(s);
        }

        [Test]
        public void The_bot_values_a_swap_by_the_hazard_it_makes_and_not_for_its_own_sake()
        {
            var (plain, _) = Battle(Db(), Stage(("tank", 2, 3)), ("knight", 2, 6));
            Assert.That(HeroBot.SwapValue(plain, Hero(plain, "knight"), Enemy(plain, "tank")), Is.EqualTo(-1f), "no hazard, no pincer: superfluous");

            var (fire, _) = Battle(Db(), Stage(("tank", 2, 3)), ("knight", 2, 6));
            PaintFirewall(fire, new Coord(2, 6));
            Assert.That(HeroBot.SwapValue(fire, Hero(fire, "knight"), Enemy(fire, "tank")), Is.GreaterThan(0f), "dropping a foe into the party's firewall");

            var (back, _) = Battle(Db(), Stage(("tank", 2, 3)), ("knight", 2, 6));
            PaintFirewall(back, new Coord(2, 3));
            Assert.That(HeroBot.SwapValue(back, Hero(back, "knight"), Enemy(back, "tank")), Is.LessThan(0f), "swapping the assassin into the fire");
        }
    }
}
