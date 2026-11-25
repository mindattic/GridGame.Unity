using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Bot;
using GridGame.Core.Content;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    public class BotTests
    {
        // ── HeroBot.LPaths ──────────────────────────────────────────────────

        [Test]
        public void LPaths_yields_one_path_for_a_straight_line()
        {
            var paths = HeroBot.LPaths(new Coord(1, 2), new Coord(4, 2)).ToList();
            Assert.That(paths, Has.Exactly(1).Items);
            Assert.That(paths[0], Is.EqualTo(new List<Coord> { new Coord(2, 2), new Coord(3, 2), new Coord(4, 2) }));
        }

        [Test]
        public void LPaths_yields_both_L_orders_for_a_diagonal_destination()
        {
            var from = new Coord(1, 1);
            var to = new Coord(3, 4);
            var paths = HeroBot.LPaths(from, to).ToList();
            Assert.That(paths.Count, Is.EqualTo(2));
            foreach (var p in paths) Assert.That(p[p.Count - 1], Is.EqualTo(to));
            // First order moves X before Y: it turns the corner at (to.X, from.Y).
            Assert.That(paths[0], Does.Contain(new Coord(to.X, from.Y)));
            Assert.That(paths[0], Does.Not.Contain(new Coord(from.X, to.Y)));
            // Second order moves Y before X: it turns the corner at (from.X, to.Y).
            Assert.That(paths[1], Does.Contain(new Coord(from.X, to.Y)));
            Assert.That(paths[1], Does.Not.Contain(new Coord(to.X, from.Y)));
        }

        [Test]
        public void LPaths_yields_nothing_when_from_equals_to()
        {
            var here = new Coord(2, 5);
            var paths = HeroBot.LPaths(here, here).ToList();
            Assert.That(paths, Has.Exactly(1).Items);
            Assert.That(paths[0], Is.Empty);
        }

        // ── HeroBot.Decide / BestMove ───────────────────────────────────────

        [Test]
        public void BestMove_returns_null_when_no_hero_can_move()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("wolf", 0, 0)), ("knight", 5, 7), ("cleric", 4, 7));
            foreach (var h in s.LivingList(Team.Hero)) h.Buffs.Add(new BuffInstance { Def = db.Buffs["sleep"], Remaining = 3 });
            var bot = new HeroBot(1);
            Assert.That(bot.BestMove(s), Is.Null);
        }

        [Test]
        public void Decide_is_deterministic_for_a_fixed_seed_and_battle_state()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("wolf", 2, 2), ("tank", 4, 5)), ("knight", 0, 0), ("mage", 5, 7));
            var mage = Hero(s, "mage");
            mage.Bar[0] = new AbilitySlot { AbilityId = "fireball" };
            s.Bank.Add(2);

            var bot1 = new HeroBot(12345, randomness: 0f) { SkillLevel = 50f };
            var bot2 = new HeroBot(12345, randomness: 0f) { SkillLevel = 50f };
            var cmd1 = bot1.Decide(s);
            var cmd2 = bot2.Decide(s);
            AssertSameCommand(cmd1, cmd2);
        }

        [Test]
        public void SkillLevel_100_always_picks_the_clearly_best_move()
        {
            // bible §1.4 setup: C(2,3) g(3,3) g(4,3) K(5,3) — dragging K to (4,3) shoves the far goblin
            // and pincers the near one with the cleric, a huge score lead over any other destination.
            var db = Mini();
            var (s, _) = Battle(db, Stage(("goblin", 3, 3), ("goblin", 4, 3)), ("cleric", 2, 3), ("knight", 5, 3));
            var cleric = Hero(s, "cleric");
            cleric.Buffs.Add(new BuffInstance { Def = db.Buffs["sleep"], Remaining = 3 }); // only the knight may move
            var knight = Hero(s, "knight");

            for (int seed = 1; seed <= 10; seed++)
            {
                var bot = new HeroBot(seed) { SkillLevel = 100f };
                var decided = bot.Decide(s);
                Assert.That(decided, Is.TypeOf<MoveCommand>());
                var cmd = (MoveCommand)decided;
                Assert.That(cmd.HeroId, Is.EqualTo(knight.Id));
                Assert.That(cmd.Path, Is.EqualTo(new List<Coord> { new Coord(4, 3) }));
            }
        }

        [Test]
        public void Randomness_and_softened_skill_still_only_return_legal_commands()
        {
            var db = Mini();
            var (s, _) = Battle(db, Stage(("wolf", 2, 2), ("tank", 3, 5)), ("knight", 0, 0), ("mage", 5, 7));
            var mage = Hero(s, "mage");
            mage.Bar[0] = new AbilitySlot { AbilityId = "fireball" };
            s.Bank.Add(6);

            for (int seed = 1; seed <= 40; seed++)
            {
                var bot = new HeroBot(seed, randomness: 1f) { SkillLevel = 30f };
                var cmd = bot.Decide(s);
                AssertLegal(s, cmd);
            }
        }

        private static void AssertLegal(BattleState s, BattleCommand cmd)
        {
            switch (cmd)
            {
                case MoveCommand mc:
                    Assert.That(BattleEngine.CanMove(s, mc.HeroId), Is.True, "move command on a hero who cannot move");
                    var pv = BattleEngine.PreviewMove(s, mc.HeroId, mc.Path);
                    Assert.That(pv.StepsTaken.Count, Is.EqualTo(mc.Path.Count));
                    break;
                case UseSlotCommand uc:
                    Assert.That(BattleEngine.GetSlotStatus(s, uc.HeroId, uc.Slot), Is.EqualTo(SlotStatus.Ready));
                    var hero = s.UnitById(uc.HeroId);
                    var ability = BattleEngine.SlotAbility(s, hero, uc.Slot);
                    if (ability.Mode != TargetMode.Auto)
                        Assert.That(BattleEngine.ValidTargets(s, uc.HeroId, uc.Slot), Does.Contain(uc.Target));
                    break;
                case ShieldCommand _:
                    break;
                default:
                    Assert.Fail($"Unexpected command type {cmd?.GetType()}");
                    break;
            }
        }

        private static void AssertSameCommand(BattleCommand a, BattleCommand b)
        {
            Assert.That(b.GetType(), Is.EqualTo(a.GetType()));
            switch (a)
            {
                case MoveCommand ma when b is MoveCommand mb:
                    Assert.That(mb.HeroId, Is.EqualTo(ma.HeroId));
                    Assert.That(mb.Path, Is.EqualTo(ma.Path));
                    break;
                case UseSlotCommand ua when b is UseSlotCommand ub:
                    Assert.That(ub.HeroId, Is.EqualTo(ua.HeroId));
                    Assert.That(ub.Slot, Is.EqualTo(ua.Slot));
                    Assert.That(ub.Target, Is.EqualTo(ua.Target));
                    break;
                case ShieldCommand _ when b is ShieldCommand _:
                    break;
                default:
                    Assert.Fail($"Mismatched commands {a} vs {b}");
                    break;
            }
        }

        // ── MetaHeuristics ──────────────────────────────────────────────────

        [Test]
        public void Score_of_null_item_is_zero()
        {
            Assert.That(MetaHeuristics.Score(null), Is.EqualTo(0f));
        }

        [Test]
        public void Score_sums_stats_plus_half_base_damage()
        {
            var item = new ItemDef
            {
                Id = "blade",
                Name = "Blade",
                Type = ItemType.Equipment,
                BaseDamage = 10,
                Stats = new Stats { Strength = 3, Vitality = 2, Agility = 1, Speed = 4, Intelligence = 5, Wisdom = 6, Luck = 7 },
            };
            // 3+2+1+4+5+6+7 = 28, plus half of 10 base damage = 33.
            Assert.That(MetaHeuristics.Score(item), Is.EqualTo(33f));
        }

        [Test]
        public void BestFirst_orders_items_by_descending_score()
        {
            var weak = new ItemDef { Id = "weak", Name = "Weak", Type = ItemType.Equipment, Stats = new Stats { Strength = 1 } };
            var strong = new ItemDef { Id = "strong", Name = "Strong", Type = ItemType.Equipment, Stats = new Stats { Strength = 10 } };
            var mid = new ItemDef { Id = "mid", Name = "Mid", Type = ItemType.Equipment, Stats = new Stats { Strength = 5 } };
            var ordered = MetaHeuristics.BestFirst(new[] { weak, strong, mid }).ToList();
            Assert.That(ordered, Is.EqualTo(new[] { strong, mid, weak }));
        }

        #region GG-T1106: HeroBot under HeroTimeline (Mode C) finishes a stage, replays, and counts hero turns bought (GG-T812)

        /// <summary>Plays the first campaign stage in HeroTimeline with a fresh bot from <paramref name="makeBot"/> to the end
        /// (no soft-lock), plays it again with a second bot from the same seed (same commands), then replays the commands
        /// into a fresh battle (same events, clock and active hero).</summary>
        private static BattleState PlayHeroTimelineStage(ContentDb db, int seed, System.Func<HeroBot> makeBot)
        {
            var save = GridGame.Meta.Progression.NewGame(db, "ModeC");
            var setup = GridGame.Meta.BattleFactory.Build(db, save, db.CampaignStages()[0].Id, seed, 1, TurnMode.HeroTimeline);
            var commands = new List<BattleCommand>();
            var played = new StringBuilder();
            int heroActions = 0;
            var final = BotRunner.Play(db, setup, makeBot(), 3000, (cmd, ev, st) =>
            {
                if (cmd != null) commands.Add(cmd);
                foreach (var e in ev) played.AppendLine(e.ToString());
                if (cmd != null && BattleEngine.CommandHeroId(cmd) >= 0 && !ev.OfType<AbilityRefusedEvent>().Any()) heroActions++;
            });
            Assert.That(final.IsOver, Is.True, "the bot finished the stage (no soft-lock)");
            Assert.That(final.TurnMode, Is.EqualTo(TurnMode.HeroTimeline));
            Assert.That(heroActions, Is.GreaterThan(1), "the bot played heroes on their own turns");

            var again = new List<BattleCommand>();
            BotRunner.Play(db, setup, makeBot(), 3000, (cmd, ev, st) => { if (cmd != null) again.Add(cmd); });
            Assert.That(again.Select(c => c.ToString()), Is.EqualTo(commands.Select(c => c.ToString())), "the bot is deterministic for a seed");

            var opening = new List<BattleEvent>();
            var s = BattleEngine.Create(db, setup, opening);
            var replayed = new StringBuilder();
            foreach (var e in opening) replayed.AppendLine(e.ToString());
            foreach (var cmd in commands) foreach (var e in BattleEngine.Apply(s, cmd)) replayed.AppendLine(e.ToString());
            Assert.That(replayed.ToString(), Is.EqualTo(played.ToString()), "the battle replays identically");
            Assert.That(s.Clock, Is.EqualTo(final.Clock));
            Assert.That(s.Phase, Is.EqualTo(final.Phase));
            Assert.That(s.ActiveHeroId, Is.EqualTo(final.ActiveHeroId));
            Assert.That(s.Results.ActionsTaken, Is.EqualTo(final.Results.ActionsTaken));
            return final;
        }

        [Test]
        public void HeroTimeline_smart_bot_finishes_a_stage_and_it_replays_identically()
        {
            var bot = new HeroBot(11);
            Assert.That(bot.TimelineAware, Is.True);
            PlayHeroTimelineStage(Real, 11, () => new HeroBot(11));
        }

        [Test]
        public void HeroTimeline_casual_bot_finishes_a_stage_and_it_replays_identically()
        {
            var bot = new HeroBot(13, randomness: 0.5f);
            Assert.That(bot.TimelineAware, Is.False);
            PlayHeroTimelineStage(Real, 13, () => new HeroBot(13, randomness: 0.5f));
        }

        private static readonly MethodInfo HeroTurnsBoughtMethod =
            typeof(HeroBot).GetMethod("HeroTurnsBought", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);

        private static int HeroTurnsBought(BattleState s, int before, int after) =>
            (int)HeroTurnsBoughtMethod.Invoke(null, new object[] { s, before, after });

        /// <summary>Three heroes of distinct Speed (so their icons arrive on distinct ticks) against one goblin.</summary>
        private static BattleState TimelineTrio(TurnMode mode)
        {
            var db = Mini();
            Stats Spd(float speed) => new Stats { Strength = 20, Vitality = 10, Agility = 10, Speed = speed, Intelligence = 10 };
            db.Classes["sprinter"] = new ClassDef { Id = "sprinter", Name = "Sprinter", Team = Team.Hero, Tags = { "Humanoid" }, BaseStats = Spd(24) };
            db.Classes["plodder"] = new ClassDef { Id = "plodder", Name = "Plodder", Team = Team.Hero, Tags = { "Humanoid" }, BaseStats = Spd(8) };
            var setup = new BattleSetup { Stage = Stage(("goblin", 5, 0)), Seed = 7, UnlockedSlots = 5, TurnMode = mode };
            setup.Party.Add(new HeroSpec { ClassId = "sprinter", Start = new Coord(0, 6) });
            setup.Party.Add(new HeroSpec { ClassId = "knight", Start = new Coord(2, 6) });
            setup.Party.Add(new HeroSpec { ClassId = "plodder", Start = new Coord(4, 6) });
            return BattleEngine.Create(db, setup, new List<BattleEvent>());
        }

        [Test]
        public void HeroTurnsBought_counts_hero_icons_between_the_foes_old_and_new_act_ticks()
        {
            Assert.That(HeroTurnsBoughtMethod, Is.Not.Null);
            var s = TimelineTrio(TurnMode.HeroTimeline);
            var heroes = s.Units.Where(u => u.IsHero && u.Alive).ToList();
            Assert.That(heroes.Count, Is.EqualTo(3));
            var t = heroes.ToDictionary(h => h, h => BattleEngine.TicksUntilTurn(s, h.Id));
            foreach (var h in heroes)
            {
                Assert.That(TurnRules.IsHeroIcon(s, h), Is.True, h.Def.Id);
                Assert.That(t[h], Is.GreaterThan(0), h.Def.Id + " has an icon on the bar (the active hero's re-entered one too)");
            }
            int max = t.Values.Max();

            Assert.That(HeroTurnsBought(s, 0, max), Is.EqualTo(3), "every hero icon arrives within (0, latest]");
            Assert.That(HeroTurnsBought(s, 0, max + 50), Is.EqualTo(3), "an icon counts once, not per lap");
            Assert.That(HeroTurnsBought(s, max, max + 50), Is.EqualTo(0), "none arrives after the latest");
            Assert.That(HeroTurnsBought(s, 5, 5), Is.EqualTo(0), "no delay buys nothing");
            Assert.That(HeroTurnsBought(s, 10, 5), Is.EqualTo(0), "a foe brought forward buys nothing");
            Assert.That(HeroTurnsBought(s, -1, max), Is.EqualTo(0), "a foe with no turn icon buys nothing");

            foreach (var h in heroes)
            {
                int expectThrough = t.Values.Count(v => v > t[h] - 1 && v <= t[h]);
                Assert.That(HeroTurnsBought(s, t[h] - 1, t[h]), Is.EqualTo(expectThrough), h.Def.Id + ": arriving on the foe's new tick counts (a hero wins a tie)");
                int expectAfter = t.Values.Count(v => v > t[h] && v <= max);
                Assert.That(HeroTurnsBought(s, t[h], max), Is.EqualTo(expectAfter), h.Def.Id + ": arriving on the foe's old tick doesn't (it came first anyway)");
            }

            // A fallen hero's icon brings no turn.
            var gone = heroes[0];
            gone.Hp = 0;
            gone.OnBoard = false;
            Assert.That(HeroTurnsBought(s, 0, max + 50), Is.EqualTo(2), "a fallen hero is not counted");
        }

        [TestCase(TurnMode.PartyWindow)]
        [TestCase(TurnMode.UnmovedThisRound)]
        public void HeroTurnsBought_is_zero_outside_HeroTimeline(TurnMode mode)
        {
            Assert.That(HeroTurnsBoughtMethod, Is.Not.Null);
            var s = TimelineTrio(mode);
            Assert.That(TurnRules.HeroesOnTimeline(s), Is.False);
            Assert.That(HeroTurnsBought(s, 0, 100000), Is.EqualTo(0), mode.ToString());
        }

        #endregion
    }
}
