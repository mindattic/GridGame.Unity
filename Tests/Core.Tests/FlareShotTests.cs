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
    /// <summary>The Hexstalker's Flare Shot (GG-T888, BIBLE §7, §14.20.6 and §14.23): a flare on a tile shows every hidden
    /// mark within 2 for good and lights the same tiles with <c>flare-light</c> for about two turns: a Lights tile (no foe on
    /// it hides in the dark), and heroes crit a foe standing in it +15 points more often. The hero bot fires it over a
    /// suspected mine or into the dark.</summary>
    public class FlareShotTests
    {
        private static readonly Coord Spot = new Coord(2, 4);

        /// <summary>Mini content plus the real holy-light rule (flare-light's home), the landmines rule and the Flare Shot.</summary>
        private static ContentDb Db()
        {
            var db = Mini();
            db.Elements = Real.Elements;
            db.Abilities["hexstalker_flare_shot"] = Real.TryAbility("hexstalker_flare_shot");
            RegionRuleCatalog.Index(db, new List<RegionRuleDef> { Real.TryRegionRule("holy-light"), Real.TryRegionRule("landmines") });
            return db;
        }

        /// <summary>The knight carries the Flare Shot in slot 0.</summary>
        private static BattleState Setup(StageDef stage, (int x, int y) knight, (int x, int y) cleric)
        {
            var setup = new BattleSetup { Stage = stage, Seed = 7, UnlockedSlots = 5 };
            var k = new HeroSpec { ClassId = "knight", Start = new Coord(knight.x, knight.y) };
            k.Bar.Add("hexstalker_flare_shot");
            setup.Party.Add(k);
            setup.Party.Add(new HeroSpec { ClassId = "cleric", Start = new Coord(cleric.x, cleric.y) });
            return BattleEngine.Create(Db(), setup, new List<BattleEvent>());
        }

        private static List<BattleEvent> Flare(BattleState s, Coord at) =>
            BattleEngine.Apply(s, new UseSlotCommand { HeroId = Hero(s, "knight").Id, Slot = 0, Target = at });

        /// <summary>A foe's mine already hidden from the party (it showed, then faded).</summary>
        private static TileMark FoeMine(BattleState s, Coord at)
        {
            var m = new TileMark { State = "buriedmine", Rule = "landmines", Hazard = true, OwnerTeam = Team.Enemy, Hidden = true };
            s.Region.Tiles[at] = m;
            return m;
        }

        private static string StateAt(BattleState s, Coord c) => s.Region.At(c)?.State;

        // ── Content ──────────────────────────────────────────────────────────────────────────────

        [Test]
        public void The_flare_shot_content_loads_and_validates()
        {
            var problems = ContentLoader.Validate(Real);
            var mine = problems.Where(p => p.Contains("flare-light") || p.Contains("hexstalker_flare_shot")).ToList();
            Assert.That(mine, Is.Empty, string.Join("\n", mine));
            var a = Real.TryAbility("hexstalker_flare_shot");
            Assert.That(a, Is.Not.Null);
            Assert.That(a.Mode, Is.EqualTo(TargetMode.PickTile));
            Assert.That(a.CooldownTurns, Is.EqualTo(3));
            var reveal = a.RuleEffects.Single(fx => fx.Kind == RuleEffectKind.RevealTiles);
            var mark = a.RuleEffects.Single(fx => fx.Kind == RuleEffectKind.MarkTiles);
            Assert.That(reveal.Radius, Is.EqualTo(2));
            Assert.That(mark.Radius, Is.EqualTo(2));
            Assert.That(mark.State, Is.EqualTo("flare-light"));
            Assert.That(mark.KeepMarks, Is.True, "it never paints over a mark already there");
            Assert.That(a.RuleEffects.IndexOf(reveal), Is.LessThan(a.RuleEffects.IndexOf(mark)), "it reveals before it lights");
            Assert.That(Real.Classes["hexstalker"].Abilities, Does.Contain("hexstalker_flare_shot"));
            var st = Real.TryTileState("flare-light");
            Assert.That(st, Is.Not.Null);
            Assert.That(st.Lights, Is.True);
            Assert.That(st.Hazard, Is.False);
            Assert.That(st.CritTakenBonus, Is.EqualTo(0.15f).Within(1e-6f));
            Assert.That(st.LifeSeconds, Is.EqualTo(10f), "about two turns");
            Assert.That(Real.TileStateRule["flare-light"], Is.EqualTo("holy-light"));
        }

        [Test]
        public void A_crit_taken_bonus_outside_0_to_1_is_reported()
        {
            var st = new TileStateDef { Id = "bad-light", Lights = true, CritTakenBonus = 1.5f };
            var db = Mini();
            RegionRuleCatalog.Index(db, new List<RegionRuleDef> { new RegionRuleDef { Id = "bad-rule", TileStates = { st } } });
            var p = new List<string>();
            RegionRuleCatalog.Validate(db, p);
            Assert.That(p, Has.Some.Contains("CritTakenBonus"));
        }

        // ── Light and reveal ─────────────────────────────────────────────────────────────────────

        [Test]
        public void A_flare_lights_every_tile_within_2_as_the_party()
        {
            var s = Setup(Stage(("tank", 5, 0)), (0, 7), (4, 7));
            var ev = Flare(s, Spot);
            for (int y = 0; y < BattleConfig.Rows; y++)
                for (int x = 0; x < BattleConfig.Cols; x++)
                {
                    var c = new Coord(x, y);
                    if (c.Manhattan(Spot) <= 2) Assert.That(StateAt(s, c), Is.EqualTo("flare-light"), c.ToString());
                    else Assert.That(StateAt(s, c), Is.Not.EqualTo("flare-light"), c.ToString());
                }
            var m = s.Region.At(Spot);
            Assert.That(m.OwnerTeam, Is.EqualTo(Team.Hero));
            Assert.That(m.Hidden, Is.False);
            Assert.That(ev.OfType<RegionRuleEvent>().Any(), Is.False, "a hero's flare announces no rule");
            AssertInvariants(s);
        }

        [Test]
        public void A_flare_shows_a_hidden_mine_in_reach_and_keeps_it()
        {
            var s = Setup(Stage(("tank", 5, 0)), (0, 7), (4, 7));
            var near = new Coord(3, 5);
            var far = new Coord(5, 6);
            var m = FoeMine(s, near);
            var f = FoeMine(s, far);
            Assert.That(RegionBoard.Sees(m, Team.Hero), Is.False, "the party can't see the foe's mine");
            var ev = Flare(s, Spot);
            Assert.That(ev.OfType<TileRevealedEvent>().Any(r => r.At == near && r.State == "buriedmine" && r.Cause == "revealed"), Is.True);
            Assert.That(RegionBoard.Sees(s.Region.At(near), Team.Hero), Is.True, "shown for good");
            Assert.That(StateAt(s, near), Is.EqualTo("buriedmine"), "shown, not set off nor painted over");
            Assert.That(f.Hidden, Is.True, "a mine out of reach stays hidden");
        }

        [Test]
        public void The_light_goes_out_after_about_two_turns()
        {
            var s = Setup(Stage(("tank", 5, 0)), (0, 7), (4, 7));
            Flare(s, Spot);
            var m = s.Region.At(Spot);
            Assert.That(m.State, Is.EqualTo("flare-light"));
            Assert.That(m.LifeTicks, Is.GreaterThan(0).And.LessThanOrEqualTo(10 * BattleConfig.TicksPerSecond));
            for (int i = 0; i < 60 && !s.IsOver && s.Region.At(Spot) != null; i++) BattleEngine.Apply(s, new ShieldCommand());
            Assert.That(StateAt(s, Spot), Is.Not.EqualTo("flare-light"), "the flare burns out");
        }

        [Test]
        public void A_foe_in_the_flare_light_is_seen_in_the_dark()
        {
            var s = Setup(Stage(("tank", 2, 2)), (0, 7), (5, 7));
            s.Region.Flags.Add(RuleFlags.Darkness);
            var tank = s.Units.Single(u => u.Def.Id == "tank");
            Assert.That(RegionBoard.Hidden(s, tank), Is.True, "far from every hero in the dark");
            Flare(s, new Coord(2, 3));
            Assert.That(StateAt(s, tank.Pos), Is.EqualTo("flare-light"));
            Assert.That(RegionBoard.Hidden(s, tank), Is.False, "the flare shows it");
        }

        // ── The crit edge ────────────────────────────────────────────────────────────────────────

        [Test]
        public void Heroes_crit_a_foe_in_the_party_flare_light_more_often()
        {
            var s = Setup(Stage(("tank", 2, 3)), (2, 7), (5, 7));
            var tank = s.Units.Single(u => u.Def.Id == "tank");
            var knight = Hero(s, "knight");
            Assert.That(RegionBoard.CritTakenBonus(s, knight, tank), Is.EqualTo(0f), "no light, no edge");
            Flare(s, new Coord(2, 4));
            Assert.That(StateAt(s, tank.Pos), Is.EqualTo("flare-light"));
            Assert.That(RegionBoard.CritTakenBonus(s, knight, tank), Is.EqualTo(0.15f).Within(1e-6f), "+15 crit points on a lit foe");
            Assert.That(RegionBoard.CritTakenBonus(s, tank, knight), Is.EqualTo(0f), "the foe gets nothing from the party's light");
            Assert.That(RegionBoard.CritTakenBonus(s, knight, Hero(s, "cleric")), Is.EqualTo(0f), "never against a friend");
            Assert.That(knight.SupportCritBonus, Is.EqualTo(0f), "nothing stays lent outside a blow");
        }

        [Test]
        public void A_foe_painted_light_favours_the_foes()
        {
            var s = Setup(Stage(("tank", 2, 3)), (2, 4), (5, 7));
            var tank = s.Units.Single(u => u.Def.Id == "tank");
            var knight = Hero(s, "knight");
            s.Region.Tiles[knight.Pos] = new TileMark { State = "flare-light", Rule = "holy-light", OwnerTeam = Team.Enemy, OwnerId = tank.Id };
            Assert.That(RegionBoard.CritTakenBonus(s, tank, knight), Is.EqualTo(0.15f).Within(1e-6f));
            Assert.That(RegionBoard.CritTakenBonus(s, knight, tank), Is.EqualTo(0f));
        }

        // ── The hero bot ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void The_hero_bot_fires_it_over_a_suspected_mine()
        {
            var s = Setup(Stage(("tank", 5, 0)), (2, 7), (4, 7));
            var a = s.Content.TryAbility("hexstalker_flare_shot");
            Assert.That(HeroBot.FlareValue(s, a, Spot), Is.EqualTo(0f), "nothing to light: no value");
            FoeMine(s, new Coord(2, 5));
            Assert.That(HeroBot.FlareValue(s, a, Spot), Is.GreaterThan(0f), "a hidden mine in reach, near the party");
            Assert.That(HeroBot.FlareValue(s, a, Spot), Is.GreaterThan(HeroBot.FlareValue(s, a, new Coord(4, 2))), "over it beats away from it");
            Assert.That(HeroBot.FlareValue(s, s.Content.TryAbility("fireball"), Spot), Is.EqualTo(0f), "only a light counts");
        }

        [Test]
        public void The_hero_bot_wont_flare_the_partys_own_hidden_mine()
        {
            var s = Setup(Stage(("tank", 5, 0)), (2, 7), (4, 7));
            var a = s.Content.TryAbility("hexstalker_flare_shot");
            s.Region.Tiles[new Coord(2, 4)] = new TileMark { State = "buriedmine", Rule = "landmines", Hazard = true, OwnerTeam = Team.Hero, Hidden = true };
            Assert.That(HeroBot.FlareValue(s, a, new Coord(2, 3)), Is.LessThan(0f), "it would show the party's ambush to the foes");
        }

        [Test]
        public void The_hero_bot_fires_it_into_the_dark()
        {
            var s = Setup(Stage(("tank", 2, 1)), (2, 7), (5, 7));
            var a = s.Content.TryAbility("hexstalker_flare_shot");
            Assert.That(HeroBot.FlareValue(s, a, new Coord(2, 3)), Is.EqualTo(0f), "no darkness: nothing to light");
            s.Region.Flags.Add(RuleFlags.Darkness);
            Assert.That(RegionBoard.Hidden(s, s.Units.Single(u => u.Def.Id == "tank")), Is.True);
            float dark = HeroBot.FlareValue(s, a, new Coord(2, 3));
            Assert.That(dark, Is.GreaterThan(0f), "dark tiles near the party while a foe hides");
            Assert.That(dark, Is.GreaterThan(HeroBot.FlareValue(s, a, new Coord(2, 7))), "the party already sees around itself");
        }
    }
}
