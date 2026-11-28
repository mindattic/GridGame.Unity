using System.Linq;
using GridGame.App.Dev;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;
using UnityEngine;

namespace GridGame.App.Tests
{
    /// <summary>GG-T1801: a scenario's generated save stands coherently at its target.</summary>
    public class ScenarioBuilderTests
    {
        private static ContentDb content;
        private static ContentDb C => content ??= ContentLoader.Load(name => Resources.Load<TextAsset>("Content/" + name)?.text);

        [Test]
        public void First_boss_resolves_to_the_greenvalley_captain()
        {
            var t = ScenarioBuilder.Resolve(C, "first-boss");
            Assert.That(t.Kind, Is.EqualTo(ScenarioKind.Battle));
            Assert.That(t.StageId, Is.EqualTo("greenvalley-03"));
            Assert.That(t.Wave, Is.EqualTo(4));
            Assert.That(t.IsBoss, Is.True);
        }

        [Test]
        public void Every_stage_before_the_target_is_cleared()
        {
            var r = ScenarioBuilder.Build(C, ScenarioDef.ForTarget("first-boss"));
            var stages = C.CampaignStages();
            int index = stages.FindIndex(s => s.Id == "greenvalley-03");
            Assert.That(r.Save.ClearedStages.Count, Is.EqualTo(index));
            Assert.That(r.Save.HighestClearedStage, Is.EqualTo(index - 1));
            Assert.That(stages.Take(index).All(s => r.Save.ClearedStages.Contains(s.Id)), Is.True);
        }

        [Test]
        public void The_party_stands_at_the_recommended_level_plus_one_for_a_boss()
        {
            var r = ScenarioBuilder.Build(C, ScenarioDef.ForTarget("first-boss"));
            int rec = Progression.StageRecommendedLevel(C, r.Save, C.Stage("greenvalley-03"));
            Assert.That(r.Level, Is.EqualTo(rec + 1));
            Assert.That(r.Save.Party.Select(r.Save.Hero).All(h => Progression.Level(h) >= rec + 1), Is.True);
        }

        [Test]
        public void No_story_beat_is_left_pending_before_the_target()
        {
            foreach (var target in new[] { "first-boss", "overworld", "final-boss" })
            {
                var r = ScenarioBuilder.Build(C, ScenarioDef.ForTarget(target));
                Assert.That(r.Save.TestMode, Is.False, target);
                Assert.That(DialogueRunner.PendingBeat(C, r.Save, r.Target.StageId)?.Id, Is.Null, target);
            }
        }
    }
}
