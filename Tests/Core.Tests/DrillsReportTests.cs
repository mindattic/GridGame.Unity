using System;
using System.Collections.Generic;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Meta;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>GG-T1650: Drills.Report (GG-T1647) reads a finished drill and writes nothing; Drills.Commit writes.</summary>
    public class DrillsReportTests
    {
        private static BattleState FinishedDrill(ContentDb c, SaveData s, out string foe)
        {
            var id = c.Campaign.Tutorial.First(t => t != null && Drills.IsDrill(c.Stages[t]));
            var b = BattleEngine.Create(c, BattleFactory.Build(c, s, id, 5), new List<BattleEvent>());
            foe = b.Units.First(u => !u.IsHero).Def.Id;
            b.Results.Seen.Add(foe);
            b.Results.Kills[foe] = 2;
            b.Results.Rounds = 4;
            b.Results.HeroTurns = 6;
            b.Results.ActionsTaken = 9;
            b.Phase = BattlePhase.Victory;
            return b;
        }

        [Test]
        public void Report_leaves_the_save_as_it_was_and_carries_the_battle()
        {
            var c = TestKit.Real;
            var s = Progression.NewGame(c, "Drill");
            var b = FinishedDrill(c, s, out _);
            var updated = s.UpdatedUtc;
            var bestiary = s.Bestiary.Count;
            var flags = s.Flags.ToList();
            var codex = s.Codex.ToList();

            var rep = Drills.Report(b);

            Assert.That(s.Bestiary.Count, Is.EqualTo(bestiary));
            Assert.That(s.ClearedDrills, Is.Empty);
            Assert.That(s.Flags, Is.EquivalentTo(flags));
            Assert.That(s.Codex, Is.EquivalentTo(codex));
            Assert.That(s.UpdatedUtc, Is.EqualTo(updated));
            Assert.That(rep.Victory, Is.True);
            Assert.That(rep.StageId, Is.EqualTo(b.Stage.Id));
            Assert.That(rep.Rounds, Is.EqualTo(4));
            Assert.That(rep.HeroTurns, Is.EqualTo(6));
            Assert.That(rep.Actions, Is.EqualTo(9));
            Assert.That(rep.PartyTurns, Is.EqualTo(b.Results.PartyTurns));
            Assert.That(rep.Practice, Is.False);
            rep.Practice = true;
            Assert.That(rep.Practice, Is.True);
        }

        [Test]
        public void Commit_writes_the_bestiary_the_clear_and_the_save_time()
        {
            var c = TestKit.Real;
            var s = Progression.NewGame(c, "Drill");
            var b = FinishedDrill(c, s, out var foe);
            s.UpdatedUtc = DateTime.UtcNow.AddDays(-1);
            var updated = s.UpdatedUtc;

            var rep = Drills.Commit(c, s, b);

            Assert.That(rep.Victory, Is.True);
            Assert.That(rep.FirstClear, Is.True);
            Assert.That(Drills.IsCleared(s, b.Stage.Id), Is.True);
            Assert.That(s.Bestiary[foe].Seen, Is.True);
            Assert.That(s.Bestiary[foe].Defeated, Is.EqualTo(2));
            Assert.That(s.UpdatedUtc, Is.GreaterThan(updated));
        }
    }
}
