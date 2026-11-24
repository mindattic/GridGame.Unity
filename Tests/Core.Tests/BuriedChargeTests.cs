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
    /// <summary>The Engineer's Buried Charge (GG-T876, BIBLE §7 and §14.23): it buries a <c>buriedmine</c> that shows for
    /// 2 s, then only the party sees it; whoever steps on it, a hero too, pops it out and it blows 1.5 s later on its tile
    /// and neighbours (the §14.22 armedmine and mineblast). The Disarm Kit clears it, an element's hit sets it off early, a
    /// reveal shows it; the hero bot buries it on a foe's likely path.</summary>
    public class BuriedChargeTests
    {
        private static readonly Coord Spot = new Coord(2, 4);

        /// <summary>Mini content plus the real landmines rule, element table and Buried Charge, a disarm tool, a fire bomb
        /// and a flare.</summary>
        private static ContentDb Db()
        {
            var db = Mini();
            db.Elements = Real.Elements;
            db.Abilities["engineer_buried_charge"] = Real.TryAbility("engineer_buried_charge");
            RegionRuleCatalog.Index(db, new List<RegionRuleDef> { Real.TryRegionRule("landmines") });
            db.Abilities["kit"] = new AbilityDef
            {
                Id = "kit", Name = "kit", Kind = AbilityKind.Skill, Shape = TargetShape.Square, Radius = 1, Mode = TargetMode.PickTile, Filter = TargetFilter.Any, TileReaction = "disarm",
            };
            db.Abilities["firebomb"] = new AbilityDef
            {
                Id = "firebomb", Name = "firebomb", Kind = AbilityKind.Skill, BaseDamage = 5, DamageType = DamageType.Fire, Shape = TargetShape.SingleTile, Mode = TargetMode.PickTile, Filter = TargetFilter.Any,
            };
            db.Abilities["flare"] = new AbilityDef
            {
                Id = "flare", Name = "flare", Kind = AbilityKind.Skill, Shape = TargetShape.SingleTile, Mode = TargetMode.PickTile, Filter = TargetFilter.EmptyOnly,
                RuleEffects = { new RuleEffectDef { Kind = RuleEffectKind.RevealTiles, Area = RuleArea.Target, Radius = 1 } },
            };
            return db;
        }

        /// <summary>The knight carries the Buried Charge in slot 0 and <paramref name="tool"/> (if any) in slot 1.</summary>
        private static BattleState Setup(StageDef stage, (int x, int y) knight, (int x, int y) cleric, string tool = null)
        {
            var setup = new BattleSetup { Stage = stage, Seed = 7, UnlockedSlots = 5 };
            var k = new HeroSpec { ClassId = "knight", Start = new Coord(knight.x, knight.y) };
            k.Bar.Add("engineer_buried_charge");
            if (tool != null) k.Bar.Add(tool);
            setup.Party.Add(k);
            setup.Party.Add(new HeroSpec { ClassId = "cleric", Start = new Coord(cleric.x, cleric.y) });
            return BattleEngine.Create(Db(), setup, new List<BattleEvent>());
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

        private static List<BattleEvent> Bury(BattleState s, Coord at) =>
            BattleEngine.Apply(s, new UseSlotCommand { HeroId = Hero(s, "knight").Id, Slot = 0, Target = at });

        private static string StateAt(BattleState s, Coord c) => s.Region.At(c)?.State;

        // ── Content ──────────────────────────────────────────────────────────────────────────────

        [Test]
        public void The_buried_charge_content_loads_and_validates()
        {
            var problems = ContentLoader.Validate(Real);
            var mine = problems.Where(p => p.Contains("buriedmine") || p.Contains("engineer_buried_charge")).ToList();
            Assert.That(mine, Is.Empty, string.Join("\n", mine));
            var a = Real.TryAbility("engineer_buried_charge");
            Assert.That(a, Is.Not.Null);
            Assert.That(a.Mode, Is.EqualTo(TargetMode.PickTile));
            Assert.That(a.Filter, Is.EqualTo(TargetFilter.EmptyOnly));
            Assert.That(a.RuleEffects.Any(fx => fx.Kind == RuleEffectKind.MarkTiles && fx.State == "buriedmine"), Is.True);
            Assert.That(Real.Classes["engineer"].Abilities, Does.Contain("engineer_buried_charge"));
            var st = Real.TryTileState("buriedmine");
            Assert.That(st, Is.Not.Null);
            Assert.That(st.VisibleSeconds, Is.EqualTo(2f));
            Assert.That(st.Affects, Is.EqualTo(TileAffects.Any));
            Assert.That(st.Hazard, Is.True);
            Assert.That(Real.TileStateRule["buriedmine"], Is.EqualTo("landmines"), "it reuses the landmines rule's armedmine and mineblast");
        }

        // ── Bury, fade, pop, blow ────────────────────────────────────────────────────────────────

        [Test]
        public void A_buried_charge_shows_then_hides_from_foes_and_the_party_still_sees_it()
        {
            var s = Setup(Stage(("tank", 5, 0)), (0, 7), (4, 7));
            var ev = Bury(s, Spot);
            var m = s.Region.At(Spot);
            Assert.That(m?.State, Is.EqualTo("buriedmine"));
            Assert.That(m.OwnerTeam, Is.EqualTo(Team.Hero));
            Assert.That(m.Hidden, Is.False, "seen by all for its first 2 s");
            Assert.That(ev.OfType<RegionRuleEvent>().Any(), Is.False, "a hero-laid charge announces no rule");
            GuardUntil(s, () => s.Region.At(Spot)?.Hidden == true);
            Assert.That(m.Hidden, Is.True, "then it fades from the foes' view");
            Assert.That(RegionBoard.Sees(m, Team.Enemy), Is.False);
            Assert.That(RegionBoard.Sees(m, Team.Hero), Is.True, "the party sees it faintly");
            AssertInvariants(s);
        }

        [Test]
        public void A_hero_stepping_on_the_charge_pops_it_and_is_caught_in_the_blast()
        {
            var s = Setup(Stage(("tank", 5, 0)), (0, 7), (2, 5));
            var cleric = Hero(s, "cleric");
            Bury(s, Spot);
            GuardUntil(s, () => s.Region.At(Spot)?.Hidden == true);
            int hp = cleric.Hp;
            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = cleric.Id, Path = P((2, 4)) });
            Assert.That(ev.OfType<TileRevealedEvent>().Any(r => r.At == Spot && r.State == "buriedmine" && r.Cause == "triggered"), Is.True, "it pops out");
            Assert.That(StateAt(s, Spot), Is.EqualTo("armedmine"), "and arms (Affects Any: the party's own charge too)");
            Assert.That(ev.OfType<HazardQueuedEvent>().Any(q => q.EventId == "mineblast"), Is.True, "its fuse runs");
            var later = GuardUntil(s, () => s.Region.At(Spot) == null);
            Assert.That(later.OfType<HazardLandedEvent>().Any(l => l.EventId == "mineblast" && l.Tiles.Contains(Spot)), Is.True, "it blows on its tile");
            Assert.That(!cleric.Alive || cleric.Hp < hp, Is.True, "the hero on it is caught");
            AssertInvariants(s);
        }

        [Test]
        public void A_foe_walking_onto_the_hidden_charge_sets_it_off()
        {
            var s = Setup(Stage(("wolf", 2, 3)), (2, 6), (4, 7));
            var wolf = s.Units.Single(u => u.Def.Id == "wolf");
            Bury(s, Spot);
            var m = s.Region.At(Spot);
            m.SightTicks = 0; m.Hidden = true; // the test skips the 2 s it shows: the wolf walks on without seeing it
            Assert.That(RegionBoard.DangerAt(s, Spot, wolf), Is.EqualTo(0f), "the wolf reads no danger it can't see");
            int hp = wolf.Hp;
            var ev = GuardUntil(s, () => StateAt(s, Spot) != "buriedmine");
            Assert.That(ev.OfType<TileRevealedEvent>().Any(r => r.At == Spot && r.Cause == "triggered"), Is.True);
            GuardUntil(s, () => s.Region.At(Spot) == null);
            Assert.That(!wolf.Alive || wolf.Hp < hp, Is.True, "the blast catches the foe that set it off");
        }

        // ── Counters ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void The_disarm_kit_clears_a_buried_charge()
        {
            var s = Setup(Stage(("tank", 5, 0)), (0, 7), (4, 7), "kit");
            Bury(s, Spot);
            GuardUntil(s, () => s.Region.At(Spot)?.Hidden == true);
            BattleEngine.Apply(s, new UseSlotCommand { HeroId = Hero(s, "knight").Id, Slot = 1, Target = Spot });
            Assert.That(s.Region.At(Spot), Is.Null, "pulled out of the ground");
            Assert.That(s.Timeline.Any(x => x.Kind == TimelineKind.Hazard), Is.False, "no blast queued");
        }

        [Test]
        public void An_elements_hit_shows_the_charge_and_sets_it_off()
        {
            var s = Setup(Stage(("tank", 5, 0)), (0, 7), (4, 7), "firebomb");
            Bury(s, Spot);
            GuardUntil(s, () => s.Region.At(Spot)?.Hidden == true);
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = Hero(s, "knight").Id, Slot = 1, Target = Spot });
            Assert.That(ev.OfType<TileRevealedEvent>().Any(r => r.At == Spot && r.Cause == "element"), Is.True);
            Assert.That(StateAt(s, Spot), Is.EqualTo("armedmine"));
        }

        [Test]
        public void A_reveal_effect_shows_the_charge_to_the_foes()
        {
            var s = Setup(Stage(("tank", 5, 0)), (0, 7), (4, 7), "flare");
            Bury(s, Spot);
            GuardUntil(s, () => s.Region.At(Spot)?.Hidden == true);
            var ev = BattleEngine.Apply(s, new UseSlotCommand { HeroId = Hero(s, "knight").Id, Slot = 1, Target = new Coord(2, 3) });
            Assert.That(ev.OfType<TileRevealedEvent>().Any(r => r.At == Spot && r.Cause == "revealed"), Is.True);
            Assert.That(RegionBoard.Sees(s.Region.At(Spot), Team.Enemy), Is.True);
            Assert.That(StateAt(s, Spot), Is.EqualTo("buriedmine"), "shown, not set off");
        }

        // ── The hero bot ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void The_hero_bot_buries_it_on_a_foes_likely_path()
        {
            var s = Setup(Stage(("wolf", 2, 1)), (2, 7), (4, 7));
            var a = s.Content.TryAbility("engineer_buried_charge");
            Assert.That(HeroBot.BuryValue(s, a, new Coord(2, 4)), Is.GreaterThan(0f), "on the wolf's straight walk to the knight");
            Assert.That(HeroBot.BuryValue(s, a, new Coord(0, 3)), Is.EqualTo(0f), "off its way");
            Assert.That(HeroBot.BuryValue(s, a, new Coord(2, 6)), Is.EqualTo(0f), "beside a hero: the blast would catch the party");
            Assert.That(HeroBot.BuryValue(s, a, new Coord(2, 3)), Is.GreaterThan(HeroBot.BuryValue(s, a, new Coord(2, 5))), "sooner is likelier");
            Assert.That(HeroBot.BuryValue(s, s.Content.TryAbility("fireball"), new Coord(2, 4)), Is.EqualTo(0f), "only a buried hazard counts");
        }
    }
}
