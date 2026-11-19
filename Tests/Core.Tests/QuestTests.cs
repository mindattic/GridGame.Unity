using System.Collections.Generic;
using System.IO;
using System.Linq;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using GridGame.Meta;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>Optional side quests (bible §22.11, GG-T237): content, the dialogue vocabulary, battle progress, hand-in.</summary>
    public class QuestTests
    {
        private static bool PortraitOnDisk(string id) =>
            File.Exists(Path.Combine(RepoRoot, "Assets", "Gaiden", "Resources", "Portraits", id + ".png"));

        private static SaveData NewSave() => Progression.NewGame(Real, "Quests");

        private static BattleState Won(SaveData s, string stageId, int seed = 7)
        {
            var b = BattleEngine.Create(Real, BattleFactory.Build(Real, s, Real.Stage(stageId), seed), new List<BattleEvent>());
            b.Phase = BattlePhase.Victory;
            return b;
        }

        [Test]
        public void Quest_content_is_valid_and_spread_over_every_main_biome()
        {
            var problems = ContentLoader.Validate(Real, PortraitOnDisk).Where(p => p.StartsWith("quest") || p.Contains("quest_")).ToList();
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            Assert.That(Real.QuestOrder.Count, Is.InRange(28, 40)); // GG-T434: 13 + 20 new, chains included
            var main = Real.Campaign.Themes.Where(t => t.Stages.All(id => Real.Stages[id].LevelOverride == null)).Select(t => t.Id); // GG-T344: six, with The Still
            Assert.That(main.All(t => Real.QuestOrder.Any(q => q.Theme == t)), Is.True, "every main biome has a quest");
            foreach (var q in Real.QuestOrder)
                Assert.That(Real.DialogueOrder.Any(d => d.AllEffects().Any(e => e.Type == DialogueEffectType.StartQuest && e.Id == q.Id)), Is.True, $"{q.Id} has a giver");
        }

        [Test]
        public void Validation_rejects_a_quest_nobody_gives_and_bad_references()
        {
            var db = ContentLoader.Load(name => { var f = Path.Combine(ContentDir, name + ".json"); return File.Exists(f) ? File.ReadAllText(f) : null; });
            db.QuestOrder.Add(new QuestDef { Id = "q_orphan", Name = "Orphan", Summary = "x", RewardGold = 1, Objective = new QuestObjective { Kind = QuestObjectiveKind.Defeat, Id = "paladin" } });
            db.Quests["q_orphan"] = db.QuestOrder.Last();
            var p = ContentLoader.Validate(db).Where(x => x.StartsWith("quest q_orphan")).ToList();
            Assert.That(p, Has.Some.Contains("no dialogue offers it"));
            Assert.That(p, Has.Some.Contains("Defeat needs an enemy class"));
        }

        [Test]
        public void A_quest_is_offered_only_after_its_unlock_stage_and_the_conversation_starts_it()
        {
            var s = NewSave();
            var q = Real.TryQuest("q_wells_wolves");
            Assert.That(Quests.State(Real, s, q), Is.EqualTo(QuestState.Locked));
            var offer = Real.Dialogues["quest_offer_wells_wolves"];
            Assert.That(DialogueRunner.All(Real, s, offer.Conditions), Is.False, "not offered before its stage");
            s.ClearedStages.Add(q.Unlock);
            Assert.That(DialogueRunner.All(Real, s, offer.Conditions), Is.True);
            s.Flags.Add("npc:innkeeper");
            s.SeenDialogues.Add("camp_innkeeper_chat");
            Assert.That(DialogueRunner.TalkTopic(Real, s, "npc:innkeeper")?.Id, Is.EqualTo("quest_offer_wells_wolves"), "an unheard offer comes before a heard chat");

            var r = new DialogueRunner(Real, s, offer);
            r.Choose(r.Choices.FindIndex(c => c.Effects.Any(e => e.Type == DialogueEffectType.StartQuest)));
            Assert.That(Quests.State(Real, s, q), Is.EqualTo(QuestState.Active));
            Assert.That(r.Log, Has.Some.Contains(q.Name));
            Assert.That(DialogueRunner.Check(Real, s, new DialogueCondition { Type = DialogueConditionType.QuestActive, Id = q.Id }), Is.True);
            Assert.That(DialogueRunner.All(Real, s, offer.Conditions), Is.False, "no longer offered once under way");
        }

        [Test]
        public void Saying_not_now_keeps_the_quest_in_the_journal_to_accept_later()
        {
            var s = NewSave();
            s.ClearedStages.Add("greenvalley-01");
            var offer = Real.Dialogues["quest_offer_wells_wolves"];
            var r = new DialogueRunner(Real, s, offer);
            r.Choose(r.Choices.FindIndex(c => !c.Effects.Any(e => e.Type == DialogueEffectType.StartQuest))); // labels are lorem (GG-T387)
            while (!r.Finished) r.Advance();
            Assert.That(Quests.Heard(Real, s).Select(q => q.Id), Does.Contain("q_wells_wolves"));
            Assert.That(Quests.Start(Real, s, "q_wells_wolves").Ok, Is.True);
            Assert.That(Quests.Start(Real, s, "q_wells_wolves").Ok, Is.False, "can't take it twice");
        }

        [Test]
        public void Defeat_quests_count_kills_on_victory_only_and_hand_in_pays_once()
        {
            var s = NewSave();
            var q = Real.TryQuest("q_wells_wolves");
            Quests.Start(Real, s, q.Id);
            var lost = Won(s, "greenvalley-01");
            lost.Phase = BattlePhase.Defeat;
            lost.Results.Kills[q.Objective.Id] = 3;
            BattleFactory.Commit(Real, s, lost);
            Assert.That(Quests.Progress(s, q), Is.EqualTo(0), "a defeat counts nothing");

            var win = Won(s, "greenvalley-01");
            win.Results.Kills[q.Objective.Id] = 3;
            var rep = BattleFactory.Commit(Real, s, win);
            Assert.That(Quests.Progress(s, q), Is.EqualTo(3));
            Assert.That(rep.QuestProgress, Has.Some.Contains("3/"));
            Assert.That(Quests.TurnIn(Real, s, q.Id).Ok, Is.False, "not finished yet");

            var win2 = Won(s, "greenvalley-01", 8);
            win2.Results.Kills[q.Objective.Id] = 9;
            BattleFactory.Commit(Real, s, win2);
            Assert.That(Quests.Progress(s, q), Is.EqualTo(Quests.Goal(q)), "capped at the goal");
            Assert.That(Quests.ReadyCount(Real, s), Is.EqualTo(1));

            int gold = s.Gold, potions = s.Count(q.RewardItem);
            Assert.That(Quests.TurnIn(Real, s, q.Id).Ok, Is.True);
            Assert.That(s.Gold, Is.EqualTo(gold + q.RewardGold));
            Assert.That(s.Count(q.RewardItem), Is.EqualTo(potions + 1));
            Assert.That(Quests.State(Real, s, q), Is.EqualTo(QuestState.Done));
            Assert.That(Quests.TurnIn(Real, s, q.Id).Ok, Is.False, "pays once");
            Assert.That(Quests.Start(Real, s, q.Id).Ok, Is.False, "a done quest doesn't restart");
        }

        [Test]
        public void Clear_stage_quests_check_the_party_size_and_nobody_falling()
        {
            var s = NewSave();
            var light = Real.TryQuest("q_travel_light");
            Assert.That(light.Objective.MaxHeroes, Is.EqualTo(3));
            Quests.Start(Real, s, light.Id);
            Summon.Join(Real, s, Real.Summon.Pool.First(id => s.Hero(id) == null));
            Assert.That(s.Party.Count, Is.EqualTo(4));
            BattleFactory.Commit(Real, s, Won(s, light.Objective.Id));
            Assert.That(Quests.IsReady(Real, s, light), Is.False, "four heroes is too many");
            s.Party.RemoveAt(3);
            BattleFactory.Commit(Real, s, Won(s, light.Objective.Id, 9));
            Assert.That(Quests.IsReady(Real, s, light), Is.True);

            var ward = Real.TryQuest("q_hold_the_ward");
            Quests.Start(Real, s, ward.Id);
            var fell = Won(s, ward.Objective.Id);
            fell.Units.First(u => u.IsHero).Hp = 0;
            BattleFactory.Commit(Real, s, fell);
            Assert.That(Quests.IsReady(Real, s, ward), Is.False, "someone fell");
            BattleFactory.Commit(Real, s, Won(s, ward.Objective.Id, 10));
            Assert.That(Quests.IsReady(Real, s, ward), Is.True);
            var sp = s.Party.ToDictionary(id => id, id => s.Hero(id).SkillPoints);
            Assert.That(Quests.TurnIn(Real, s, ward.Id).Ok, Is.True);
            foreach (var id in s.Party) Assert.That(s.Hero(id).SkillPoints, Is.EqualTo(sp[id] + ward.RewardSkillPoints));
        }

        [Test]
        public void Bring_quests_read_the_bag_and_take_the_items_on_hand_in_and_a_reward_flag_opens_a_follow_up()
        {
            var s = NewSave();
            var q = Real.TryQuest("q_grown_not_forged");
            Quests.Start(Real, s, q.Id);
            s.AddItem(q.Objective.Id, Quests.Goal(q) - 1);
            Assert.That(Quests.IsReady(Real, s, q), Is.False);
            s.AddItem(q.Objective.Id, 2);
            Assert.That(Quests.IsReady(Real, s, q), Is.True);
            Assert.That(Quests.TurnIn(Real, s, q.Id).Ok, Is.True);
            Assert.That(s.Count(q.Objective.Id), Is.EqualTo(1), "only the asked-for count is handed over");
            Assert.That(s.Flags, Does.Contain(q.RewardFlag));
            s.Flags.Add("npc:blacksmith");
            s.SeenDialogues.Add("camp_blacksmith_chat");
            Assert.That(DialogueRunner.TalkTopic(Real, s, "npc:blacksmith")?.Id, Is.EqualTo("quest_after_grown_not_forged"));
        }

        // ── GG-T434: family kills, recruits, levels, skills, recruit rewards and quest chains ──

        [Test]
        public void Content_has_every_new_objective_kind_and_three_chains_of_three_or_more()
        {
            foreach (var kind in new[] { QuestObjectiveKind.DefeatFamily, QuestObjectiveKind.Recruit, QuestObjectiveKind.ReachLevel, QuestObjectiveKind.LearnSkill, QuestObjectiveKind.Bring })
                Assert.That(Real.QuestOrder.Any(q => q.Objective.Kind == kind), Is.True, $"some quest uses {kind}");
            var chains = Real.QuestOrder.Where(q => q.Requires == null).Select(q => QuestCatalog.Chain(Real, q)).Where(ch => ch.Count >= 3).ToList();
            Assert.That(chains.Count, Is.GreaterThanOrEqualTo(3), "at least three chains of three or more steps");
            foreach (var q in Real.QuestOrder.Where(q => q.Requires != null))
                Assert.That(QuestCatalog.Chain(Real, q).IndexOf(q), Is.GreaterThan(0), $"{q.Id} sits after the step it requires");
        }

        [Test]
        public void Validation_rejects_unknown_references_and_a_looping_chain()
        {
            var db = ContentLoader.Load(name => { var f = Path.Combine(ContentDir, name + ".json"); return File.Exists(f) ? File.ReadAllText(f) : null; });
            void Add(QuestDef q) { db.QuestOrder.Add(q); db.Quests[q.Id] = q; }
            Add(new QuestDef { Id = "q_bad_family", Name = "x", Summary = "x", RewardGold = 1, Objective = new QuestObjective { Kind = QuestObjectiveKind.DefeatFamily, Id = "no_such_family" } });
            Add(new QuestDef { Id = "q_bad_recruit", Name = "x", Summary = "x", RewardGold = 1, Objective = new QuestObjective { Kind = QuestObjectiveKind.Recruit, Id = "wolf00" } });
            Add(new QuestDef { Id = "q_bad_level", Name = "x", Summary = "x", RewardGold = 1, Objective = new QuestObjective { Kind = QuestObjectiveKind.ReachLevel, Count = 150, Hero = "wolf00" } });
            Add(new QuestDef { Id = "q_bad_skill", Name = "x", Summary = "x", RewardGold = 1, RewardHero = "slime00", Objective = new QuestObjective { Kind = QuestObjectiveKind.LearnSkill, Id = "no_such_skill" } });
            Add(new QuestDef { Id = "q_loop_a", Name = "x", Summary = "x", RewardGold = 1, Requires = "q_loop_b", Objective = new QuestObjective { Kind = QuestObjectiveKind.Flag, Id = "f" } });
            Add(new QuestDef { Id = "q_loop_b", Name = "x", Summary = "x", RewardGold = 1, Requires = "q_loop_a", Objective = new QuestObjective { Kind = QuestObjectiveKind.Flag, Id = "f" } });
            Add(new QuestDef { Id = "q_lost_step", Name = "x", Summary = "x", RewardGold = 1, Requires = "q_nowhere", Objective = new QuestObjective { Kind = QuestObjectiveKind.Flag, Id = "f" } });
            var p = ContentLoader.Validate(db);
            Assert.That(p, Has.Some.Contains("unknown enemy family 'no_such_family'"));
            Assert.That(p, Has.Some.Contains("Recruit needs a hero class"));
            Assert.That(p, Has.Some.Contains("ReachLevel needs a level from 2 to 99"));
            Assert.That(p, Has.Some.Contains("unknown hero 'wolf00'"));
            Assert.That(p, Has.Some.Contains("unknown skill 'no_such_skill'"));
            Assert.That(p, Has.Some.Contains("RewardHero needs a hero class"));
            Assert.That(p, Has.Some.Contains("loops back on itself"));
            Assert.That(p, Has.Some.Contains("requires unknown quest 'q_nowhere'"));
        }

        [Test]
        public void A_chain_step_is_offered_only_after_the_step_before_it_is_done_and_the_journal_shows_the_chain()
        {
            var s = NewSave();
            var first = Real.TryQuest("q_moor_slimes");
            var second = Real.TryQuest("q_moor_harpies");
            Assert.That(second.Requires, Is.EqualTo(first.Id));
            s.ClearedStages.Add("greenvalley-01");
            s.ClearedStages.Add("greenvalley-02");
            Assert.That(Quests.State(Real, s, first), Is.EqualTo(QuestState.Available));
            Assert.That(Quests.State(Real, s, second), Is.EqualTo(QuestState.Locked), "the step before isn't done");
            Assert.That(DialogueRunner.All(Real, s, Real.Dialogues["quest_offer_moor_harpies"].Conditions), Is.False);
            Assert.That(Quests.ChainLine(Real, s, first), Does.StartWith("Part 1 of 3"));

            Quests.Start(Real, s, first.Id);
            var win = Won(s, "greenvalley-02");
            win.Results.Kills["slime00"] = 4;
            win.Results.Kills["slime02"] = 4;
            BattleFactory.Commit(Real, s, win);
            Assert.That(Quests.IsReady(Real, s, first), Is.True, "every slime of the family counts");
            Assert.That(Quests.TurnIn(Real, s, first.Id).Ok, Is.True);
            Assert.That(Quests.State(Real, s, second), Is.EqualTo(QuestState.Available));
            Assert.That(DialogueRunner.All(Real, s, Real.Dialogues["quest_offer_moor_harpies"].Conditions), Is.True);
            Assert.That(Quests.ChainLine(Real, s, second), Does.Contain(first.Name + " (done)"));
        }

        [Test]
        public void Recruit_level_and_skill_objectives_read_the_roster_live()
        {
            var s = NewSave();
            var recruit = Real.TryQuest("q_ronin_recruit");
            var level = Real.TryQuest("q_ronin_level");
            var skill = Real.TryQuest("q_ronin_guard");
            Quests.Start(Real, s, recruit.Id);
            Assert.That(Quests.IsReady(Real, s, recruit), Is.False);
            Summon.Join(Real, s, "ronin");
            Assert.That(Quests.IsReady(Real, s, recruit), Is.True);

            Quests.Start(Real, s, level.Id);
            var ronin = s.Hero("ronin");
            ronin.TotalXp = Formulas.TotalXpForLevel(level.Objective.Count - 1);
            Assert.That(Quests.IsReady(Real, s, level), Is.False);
            s.Hero("paladin").TotalXp = Formulas.TotalXpForLevel(level.Objective.Count + 5);
            Assert.That(Quests.IsReady(Real, s, level), Is.False, "another hero's level doesn't count for a named hero");
            ronin.TotalXp = Formulas.TotalXpForLevel(level.Objective.Count);
            Assert.That(Quests.IsReady(Real, s, level), Is.True);

            Quests.Start(Real, s, skill.Id);
            ronin.Known.Remove(skill.Objective.Id);
            Assert.That(Quests.IsReady(Real, s, skill), Is.False);
            ronin.Known.Add(skill.Objective.Id);
            Assert.That(Quests.IsReady(Real, s, skill), Is.True);
            Assert.That(Quests.Describe(Real, skill), Does.Contain(Real.Ability(skill.Objective.Id).Name));
        }

        [Test]
        public void A_recruit_reward_brings_the_hero_once()
        {
            var s = NewSave();
            var q = Real.QuestOrder.First(x => x.RewardHero != null && x.Objective.Kind == QuestObjectiveKind.Bring);
            Assert.That(s.Hero(q.RewardHero), Is.Null);
            Quests.Start(Real, s, q.Id);
            s.AddItem(q.Objective.Id, Quests.Goal(q));
            var r = Quests.TurnIn(Real, s, q.Id);
            Assert.That(r.Ok, Is.True);
            Assert.That(s.Hero(q.RewardHero), Is.Not.Null);
            Assert.That(r.Message, Does.Contain("joins"));
        }

        [Test]
        public void Old_saves_get_an_empty_journal()
        {
            var s = NewSave();
            s.Quests = null;
            SaveMigrations.Migrate(s, Real);
            Assert.That(s.Quests, Is.Not.Null);
            Assert.That(Quests.ReadyCount(Real, s), Is.EqualTo(0));
        }
    }
}
