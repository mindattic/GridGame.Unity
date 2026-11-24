using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>Tactical tiles (GG-T842, BIBLE §14.23): a painted mark records the team and unit that painted it, and a
    /// state's <see cref="TileStateDef.Affects"/> decides whom its hooks and OnMark effects reach. A hero's firewall burns
    /// foes, never the party; foes steer round it and can be shoved into it; it never announces a region rule. A state with VisibleSeconds hides
    /// from the other side once they run out (GG-T843) until it goes off, is revealed or an element hits it.</summary>
    public class TacticalTileTests
    {
        /// <summary>Mini content plus a "tactics" rule: a hero-side firewall (hazard, foes of its painter only, 5 Fire on
        /// entering), a burst that hits foes as it is painted, and the hero abilities that lay them.</summary>
        private static ContentDb Db(params RuleEffectDef[] atStart)
        {
            var db = Mini();
            var rule = new RegionRuleDef
            {
                Id = "tactics",
                TileStates =
                {
                    new TileStateDef { Id = "firewall", Hazard = true, Affects = TileAffects.FoesOfOwner },
                    // GG-T843: a mine seen by all for half a second, then hidden from the other side; a flame carries fire.
                    new TileStateDef { Id = "mine", Hazard = true, Affects = TileAffects.FoesOfOwner, VisibleSeconds = 0.5f },
                    new TileStateDef { Id = "flame", Element = DamageType.Fire },
                    new TileStateDef
                    {
                        Id = "burst", Affects = TileAffects.FoesOfOwner,
                        OnMark = { new RuleEffectDef { Kind = RuleEffectKind.Damage, Area = RuleArea.Self, Amount = 3, DamageType = DamageType.Fire } },
                    },
                    // GG-T844: a slime glob hatches a goblin when it runs out, unless a hero squishes it first.
                    new TileStateDef
                    {
                        Id = "glob", LifeSeconds = 2f, SquishedBy = RuleTeam.Hero,
                        OnExpire = { new RuleEffectDef { Kind = RuleEffectKind.SpawnUnit, Area = RuleArea.Self, Class = "goblin", Level = 1 } },
                        OnSquish = { new RuleEffectDef { Kind = RuleEffectKind.SetFlag, Flag = "squished" } },
                    },
                },
                Hooks =
                {
                    new RuleHookDef
                    {
                        On = RuleHook.EnterTile, State = "firewall",
                        Effects = { new RuleEffectDef { Kind = RuleEffectKind.Damage, Units = RuleUnits.Subject, Amount = 5, DamageType = DamageType.Fire } },
                    },
                    new RuleHookDef
                    {
                        On = RuleHook.EnterTile, State = "mine",
                        Effects = { new RuleEffectDef { Kind = RuleEffectKind.Damage, Units = RuleUnits.Subject, Amount = 4, DamageType = DamageType.Fire } },
                    },
                },
            };
            if (atStart.Length > 0) rule.Hooks.Add(new RuleHookDef { On = RuleHook.BattleStart, Effects = atStart.ToList() });
            RegionRuleCatalog.Index(db, new List<RegionRuleDef> { rule });
            db.Abilities["firewall"] = Layer("firewall", "firewall", 0);
            db.Abilities["firewall_wide"] = Layer("firewall_wide", "firewall", 1);
            db.Abilities["burst_toss"] = Layer("burst_toss", "burst", 1);
            db.Abilities["mine_lay"] = Layer("mine_lay", "mine", 0);
            db.Abilities["flame_toss"] = Layer("flame_toss", "flame", 0);
            db.Abilities["flare"] = new AbilityDef
            {
                Id = "flare", Name = "flare", Kind = AbilityKind.Skill, Shape = TargetShape.SingleTile, Mode = TargetMode.PickTile, Filter = TargetFilter.EmptyOnly,
                RuleEffects = { new RuleEffectDef { Kind = RuleEffectKind.RevealTiles, Area = RuleArea.Target, Radius = 1 } },
            };
            return db;
        }

        private static AbilityDef Layer(string id, string state, int radius) => new AbilityDef
        {
            Id = id, Name = id, Kind = AbilityKind.Skill, Shape = TargetShape.SingleTile, Mode = TargetMode.PickTile, Filter = TargetFilter.EmptyOnly,
            RuleEffects = { new RuleEffectDef { Kind = RuleEffectKind.MarkTiles, Area = RuleArea.Target, Radius = radius, State = state } },
        };

        private static BattleState Setup(ContentDb db, StageDef stage, (string cls, int x, int y, string bar)[] heroes)
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

        private static bool HookRan(BattleState s) => s.Region.HookCounts.TryGetValue("tactics#0", out int n) && n > 0;

        /// <summary>A stage whose BattleStart paints a board-owned mine on (1,6) (GG-T843: hidden from both sides once it hides).</summary>
        private static BattleState BoardMine(string knightBar)
        {
            var paint = new RuleEffectDef { Kind = RuleEffectKind.MarkTiles, Area = RuleArea.Tiles, Tiles = { new Coord(1, 6) }, State = "mine" };
            var st = Stage(("tank", 5, 0));
            st.Rules.Add("tactics");
            return Setup(Db(paint), st, new[] { ("knight", 1, 7, knightBar), ("cleric", 4, 7, (string)null) });
        }

        [Test]
        public void A_hero_ability_paints_as_the_party_and_announces_no_rule()
        {
            var s = Setup(Db(), Stage(("tank", 5, 0)), new[] { ("knight", 0, 6, "firewall"), ("cleric", 4, 7, (string)null) });
            var knight = Hero(s, "knight");
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = knight.Id, Slot = 0, Target = new Coord(2, 4) });
            var m = s.Region.At(new Coord(2, 4));
            Assert.That(m?.State, Is.EqualTo("firewall"));
            Assert.That(m.OwnerTeam, Is.EqualTo(Team.Hero), "the mark records the side that laid it");
            Assert.That(m.OwnerId, Is.EqualTo(knight.Id), "and the hero who did");
            Assert.That(s.Region.Rules, Does.Contain("tactics"), "its hooks come into play");
            Assert.That(ev.OfType<RegionRuleEvent>().Any(), Is.False, "a hero-laid state never announces a region rule");
            AssertInvariants(s);
        }

        [Test]
        public void A_hero_laid_hazard_is_danger_to_foes_only()
        {
            var s = Setup(Db(), Stage(("wolf", 5, 0)), new[] { ("knight", 0, 6, "firewall"), ("cleric", 4, 7, (string)null) });
            var knight = Hero(s, "knight");
            BattleEngine.Apply(s, new UseSlotCommand { HeroId = knight.Id, Slot = 0, Target = new Coord(2, 4) });
            var wolf = s.Units.Single(u => u.Def.Id == "wolf");
            Assert.That(RegionBoard.DangerAt(s, new Coord(2, 4), knight), Is.EqualTo(0f), "the party walks its own firewall");
            Assert.That(RegionBoard.DangerAt(s, new Coord(2, 4), wolf), Is.GreaterThan(0f), "foes read it as danger and steer round it");
            Assert.That(RegionBoard.Affects(s, s.Region.At(new Coord(2, 4)), wolf), Is.True);
            Assert.That(RegionBoard.Affects(s, s.Region.At(new Coord(2, 4)), knight), Is.False);
        }

        [Test]
        public void A_hero_walks_through_the_partys_own_firewall_unhurt()
        {
            var s = Setup(Db(), Stage(("tank", 5, 0)), new[] { ("knight", 0, 6, "firewall"), ("cleric", 2, 5, (string)null) });
            var knight = Hero(s, "knight");
            var cleric = Hero(s, "cleric");
            BattleEngine.Apply(s, new UseSlotCommand { HeroId = knight.Id, Slot = 0, Target = new Coord(2, 4) });
            int hp = cleric.Hp;
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = cleric.Id, Path = P((2, 4)) });
            Assert.That(cleric.Pos, Is.EqualTo(new Coord(2, 4)));
            Assert.That(cleric.Hp, Is.EqualTo(hp), "its EnterTile hook passes over the party");
            Assert.That(HookRan(s), Is.False);
            Assert.That(ev.OfType<DamagedEvent>().Any(d => d.TargetId == cleric.Id), Is.False);
            AssertInvariants(s);
        }

        [Test]
        public void A_foe_shoved_into_a_hero_laid_firewall_burns()
        {
            var db = Db();
            var s = Setup(db, Stage(("tank", 2, 4)), new[] { ("knight", 0, 6, "firewall_wide"), ("cleric", 2, 5, "shield_bash") });
            var knight = Hero(s, "knight");
            var tank = s.Units.Single(u => u.Def.Id == "tank");
            BattleEngine.Apply(s, new UseSlotCommand { HeroId = knight.Id, Slot = 0, Target = new Coord(2, 2) });
            Assert.That(s.Region.At(new Coord(2, 3))?.State, Is.EqualTo("firewall"), "the wall spreads round the picked tile");
            Assert.That(RegionBoard.ShoveFloorValue(s, tank, new Coord(0, -1), 2), Is.GreaterThan(0f), "the bot sees the shove ends in the fire");
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = Hero(s, "cleric").Id, Slot = 0, Target = tank.Pos });
            Assert.That(HookRan(s), Is.True, "the shoved foe ran the firewall's EnterTile hook");
            Assert.That(ev.OfType<DamagedEvent>().Any(d => d.TargetId == tank.Id && d.Note == "Hazard"), Is.True, "and took its fire");
            AssertInvariants(s);
        }

        [Test]
        public void A_burst_hits_the_foes_it_is_painted_under_and_spares_the_party()
        {
            var s = Setup(Db(), Stage(("tank", 3, 3)), new[] { ("knight", 0, 6, "burst_toss"), ("cleric", 3, 5, (string)null) });
            var knight = Hero(s, "knight");
            var cleric = Hero(s, "cleric");
            var tank = s.Units.Single(u => u.Def.Id == "tank");
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = knight.Id, Slot = 0, Target = new Coord(3, 4) });
            Assert.That(s.Region.At(new Coord(3, 5))?.OwnerTeam, Is.EqualTo(Team.Hero), "painted under the cleric too");
            Assert.That(ev.OfType<DamagedEvent>().Any(d => d.TargetId == tank.Id && d.Note == "Hazard"), Is.True, "OnMark reaches the foe");
            Assert.That(ev.OfType<DamagedEvent>().Any(d => d.TargetId == cleric.Id), Is.False, "and passes over the party");
            AssertInvariants(s);
        }

        [Test]
        public void A_board_painted_mark_works_on_everyone_whatever_its_Affects()
        {
            var paint = new RuleEffectDef { Kind = RuleEffectKind.MarkTiles, Area = RuleArea.Tiles, Tiles = { new Coord(1, 6) }, State = "firewall" };
            var st = Stage(("tank", 5, 0));
            st.Rules.Add("tactics");
            var s = Setup(Db(paint), st, new[] { ("knight", 1, 7, (string)null), ("cleric", 4, 7, (string)null) });
            var knight = Hero(s, "knight");
            Assert.That(s.Region.At(new Coord(1, 6)).OwnerTeam, Is.EqualTo(Team.Neutral), "a rule's own hook paints for nobody");
            Assert.That(RegionBoard.DangerAt(s, new Coord(1, 6), knight), Is.GreaterThan(0f));
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = knight.Id, Path = P((1, 6)) });
            Assert.That(HookRan(s), Is.True);
            Assert.That(ev.OfType<DamagedEvent>().Any(d => d.TargetId == knight.Id && d.Note == "Hazard"), Is.True);
        }

        [Test]
        public void A_monster_laid_state_still_brings_and_announces_its_rule()
        {
            var db = Db();
            db.Classes["goblin"].OnDeathRule.Add(new RuleEffectDef { Kind = RuleEffectKind.MarkTiles, Area = RuleArea.Self, State = "firewall" });
            var s = Setup(db, Stage(("goblin", 2, 3), ("tank", 5, 0)), new[] { ("knight", 1, 3, (string)null), ("cleric", 3, 5, (string)null) });
            var gob = s.Units.Single(u => u.Def.Id == "goblin");
            gob.Hp = 1;
            var at = gob.Pos;
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((3, 4), (3, 3)) });
            Assert.That(gob.Alive, Is.False);
            var m = s.Region.At(at);
            Assert.That(m?.OwnerTeam, Is.EqualTo(Team.Enemy), "a foe's death paints as its side");
            Assert.That(m.OwnerId, Is.EqualTo(gob.Id));
            Assert.That(ev.OfType<RegionRuleEvent>().Any(r => r.RuleId == "tactics"), Is.True, "announced as ever");
            Assert.That(RegionBoard.DangerAt(s, at, Hero(s, "knight")), Is.GreaterThan(0f), "a foe-laid firewall burns the party");
        }

        // -- Hidden marks (GG-T843) --

        [Test]
        public void A_hero_mine_hides_from_foes_after_its_visible_seconds_and_the_party_still_knows()
        {
            var s = Setup(Db(), Stage(("wolf", 5, 0)), new[] { ("knight", 0, 6, "mine_lay"), ("cleric", 4, 7, (string)null) });
            var knight = Hero(s, "knight");
            var wolf = s.Units.Single(u => u.Def.Id == "wolf");
            var c = new Coord(2, 4);
            BattleEngine.Apply(s, new UseSlotCommand { HeroId = knight.Id, Slot = 0, Target = c });
            var m = s.Region.At(c);
            Assert.That(m?.State, Is.EqualTo("mine"));
            Assert.That(m.Hidden, Is.False, "seen by all at first");
            Assert.That(RegionBoard.Sees(m, Team.Enemy), Is.True);
            Assert.That(RegionBoard.DangerAt(s, c, wolf), Is.GreaterThan(0f), "while it shows, foes steer round it");
            var ev = GuardUntil(s, () => s.Region.At(c)?.Hidden == true);
            Assert.That(s.Region.At(c)?.Hidden, Is.True, "its VisibleSeconds ran out");
            Assert.That(ev.OfType<RegionTilesEvent>().Any(t => t.Change == "hidden" && t.State == "mine" && t.Tiles.Contains(c)), Is.True);
            Assert.That(RegionBoard.Sees(m, Team.Hero), Is.True, "the party keeps a faint view of its own mine");
            Assert.That(RegionBoard.Sees(m, Team.Enemy), Is.False, "foes don't");
            Assert.That(RegionBoard.DangerAt(s, c, wolf), Is.EqualTo(0f), "no cheating: the foe's planner reads no danger it can't see");
            Assert.That(RegionBoard.DangerAt(s, c, wolf, Team.Hero), Is.GreaterThan(0f), "the hero bot still knows the foe would get hurt there");
        }

        [Test]
        public void A_board_mine_hides_from_both_sides_and_shows_when_it_goes_off()
        {
            var s = BoardMine(null);
            var knight = Hero(s, "knight");
            var c = new Coord(1, 6);
            Assert.That(RegionBoard.DangerAt(s, c, knight), Is.GreaterThan(0f), "seen at first");
            GuardUntil(s, () => s.Region.At(c)?.Hidden == true);
            var m = s.Region.At(c);
            Assert.That(m?.Hidden, Is.True);
            Assert.That(RegionBoard.Sees(m, Team.Hero) || RegionBoard.Sees(m, Team.Enemy), Is.False, "a board-painted hidden mark: nobody sees it");
            Assert.That(RegionBoard.DangerAt(s, c, knight), Is.EqualTo(0f), "so the party's planners walk on in");
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = knight.Id, Path = P((1, 6)) });
            var rev = ev.OfType<TileRevealedEvent>().SingleOrDefault();
            Assert.That(rev, Is.Not.Null, "it goes off and shows");
            Assert.That(rev.Cause, Is.EqualTo("triggered"));
            Assert.That(rev.At, Is.EqualTo(c));
            Assert.That(ev.IndexOf(rev), Is.LessThan(ev.FindIndex(x => x is DamagedEvent d && d.TargetId == knight.Id)), "shown before its blow lands");
            Assert.That(m.Hidden, Is.False);
            Assert.That(RegionBoard.Sees(m, Team.Enemy), Is.True, "for everyone, for good");
            AssertInvariants(s);
        }

        [Test]
        public void A_reveal_effect_uncovers_a_hidden_mark()
        {
            var s = BoardMine("flare");
            var c = new Coord(1, 6);
            GuardUntil(s, () => s.Region.At(c)?.Hidden == true);
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = Hero(s, "knight").Id, Slot = 0, Target = c });
            Assert.That(ev.OfType<TileRevealedEvent>().Any(r => r.At == c && r.Cause == "revealed"), Is.True);
            Assert.That(s.Region.At(c).Hidden, Is.False);
            Assert.That(RegionBoard.Sees(s.Region.At(c), Team.Hero), Is.True);
            GuardUntil(s, () => false, 6);
            Assert.That(s.Region.At(c)?.Hidden, Is.False, "a revealed mark never hides again");
        }

        [Test]
        public void An_element_painted_on_a_hidden_mark_shows_it()
        {
            var s = BoardMine("flame_toss");
            var c = new Coord(1, 6);
            GuardUntil(s, () => s.Region.At(c)?.Hidden == true);
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = Hero(s, "knight").Id, Slot = 0, Target = c });
            Assert.That(ev.OfType<TileRevealedEvent>().Any(r => r.At == c && r.Cause == "element" && r.State == "mine"), Is.True);
        }

        [Test]
        public void Hiding_is_replay_deterministic()
        {
            string Run()
            {
                var s = BoardMine(null);
                var ev = GuardUntil(s, () => s.Region.At(new Coord(1, 6))?.Hidden == true);
                ev.AddRange(BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "knight").Id, Path = P((1, 6)) }));
                return string.Join("|", ev.Select(x => x.ToString()));
            }
            Assert.That(Run(), Is.EqualTo(Run()));
        }

        // ── Hatching and squishing (GG-T844) ──────────────────────────────────────────────────────

        private static BattleState GlobAt(Coord at, (string cls, int x, int y, string bar)[] heroes)
        {
            var paint = new RuleEffectDef { Kind = RuleEffectKind.MarkTiles, Area = RuleArea.Tiles, Tiles = { at }, State = "glob" };
            var st = Stage(("tank", 5, 0));
            st.Rules.Add("tactics");
            return Setup(Db(paint), st, heroes);
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

        [Test]
        public void A_glob_nobody_squishes_hatches_on_its_tile_and_joins_the_wave()
        {
            var s = GlobAt(new Coord(2, 4), new[] { ("knight", 0, 7, (string)null), ("cleric", 4, 7, (string)null) });
            Assert.That(s.Region.At(new Coord(2, 4))?.State, Is.EqualTo("glob"));
            var ev = GuardUntil(s, () => s.Region.At(new Coord(2, 4)) == null);
            var arrived = ev.OfType<ReinforcementsArrivedEvent>().Single();
            var gob = s.UnitById(arrived.Spawned.Single());
            Assert.That(gob.Def.Id, Is.EqualTo("goblin"));
            Assert.That(gob.Team, Is.EqualTo(Team.Enemy), "it fights for the wave");
            Assert.That(arrived.SpawnedAt.Single().At, Is.EqualTo(new Coord(2, 4)), "it hatches on the glob's own tile");
            Assert.That(s.Timeline.Any(t => t.Kind == TimelineKind.Turn && t.OwnerId == gob.Id), Is.True, "and takes its place on the timeline");
            int hatched = ev.IndexOf(arrived);
            int cleared = ev.FindIndex(x => x is RegionTilesEvent t && t.State == "glob" && t.Change == "cleared");
            Assert.That(cleared, Is.GreaterThan(hatched), "OnExpire runs before the mark clears");
            AssertInvariants(s);
        }

        [Test]
        public void A_glob_under_someone_hatches_on_the_nearest_free_tile()
        {
            var s = GlobAt(new Coord(1, 7), new[] { ("knight", 1, 7, (string)null), ("cleric", 4, 7, (string)null) });
            var knight = Hero(s, "knight");
            Assert.That(s.Region.At(knight.Pos)?.State, Is.EqualTo("glob"), "painted under the knight, who never stepped on it");
            var ev = GuardUntil(s, () => s.Region.At(new Coord(1, 7)) == null);
            var at = ev.OfType<ReinforcementsArrivedEvent>().Single().SpawnedAt.Single().At;
            Assert.That(at, Is.Not.EqualTo(new Coord(1, 7)), "never on an occupied tile");
            Assert.That(at.Manhattan(new Coord(1, 7)), Is.EqualTo(1), "but right beside it");
            AssertInvariants(s);
        }

        [Test]
        public void A_hero_stepping_on_a_glob_squishes_it_and_nothing_hatches()
        {
            var s = GlobAt(new Coord(1, 6), new[] { ("knight", 1, 7, (string)null), ("cleric", 4, 7, (string)null) });
            var knight = Hero(s, "knight");
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = knight.Id, Path = P((1, 6)) });
            var sq = ev.OfType<SquishedEvent>().Single();
            Assert.That(sq.UnitId, Is.EqualTo(knight.Id));
            Assert.That(sq.At, Is.EqualTo(new Coord(1, 6)));
            Assert.That(s.Region.At(new Coord(1, 6)), Is.Null, "the glob is gone");
            Assert.That(ev.OfType<RegionTilesEvent>().Any(t => t.State == "glob" && t.Change == "cleared"), Is.True);
            Assert.That(s.Region.Flags, Does.Contain("squished"), "its OnSquish ran");
            var later = GuardUntil(s, () => false, 12);
            Assert.That(later.OfType<ReinforcementsArrivedEvent>().Any(), Is.False, "a squished glob never hatches");
            Assert.That(s.Units.Any(u => u.Def.Id == "goblin"), Is.False);
            AssertInvariants(s);
        }

        [Test]
        public void Validation_reports_a_spawn_with_no_known_class_and_an_OnExpire_that_never_runs()
        {
            var db = Db();
            var rule = db.TryRegionRule("tactics");
            rule.TileStates.Add(new TileStateDef
            {
                Id = "dud", OnExpire = { new RuleEffectDef { Kind = RuleEffectKind.SpawnUnit, Area = RuleArea.Self, Class = "nope" } },
            });
            db.TileStates["dud"] = rule.TileStates.Last();
            var p = new List<string>();
            RegionRuleCatalog.Validate(db, p);
            Assert.That(p.Any(x => x.Contains("unknown class 'nope'")), Is.True);
            Assert.That(p.Any(x => x.Contains("OnExpire needs LifeSeconds")), Is.True);
            Assert.That(p.Any(x => x.Contains("state glob")), Is.False, "the glob itself is well formed");
        }
    }
}
