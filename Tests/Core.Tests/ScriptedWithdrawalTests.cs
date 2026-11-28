using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T2070: a stage's scripted withdrawal (StageDef.Withdraw) ends the battle as BattlePhase.Withdrawn once
    /// its unyielding-plate clangs are reached, with the party's line to the Commander; neither a win nor a defeat.</summary>
    public class ScriptedWithdrawalTests
    {
        private static ContentDb Plated()
        {
            var db = Mini();
            var gob = db.Classes["goblin"];
            gob.ArmorPlate = 40;
            gob.PlateUnyielding = true;
            gob.BaseStats.Vitality = 60;
            gob.DropTable = null;
            return db;
        }

        [Test]
        public void The_battle_withdraws_once_the_plate_has_clanged_enough()
        {
            var stage = Stage(("goblin", 2, 3), ("tank", 0, 0));
            stage.Withdraw = new WithdrawDef { AfterPlateClangs = 1, Line = "It won't dent, Commander. Fall back!" };
            var (s, _) = Battle(Plated(), stage, ("knight", 2, 4), ("cleric", 2, 0));

            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((2, 1), (2, 2)) });

            Assert.That(s.Results.PlateClangs, Is.GreaterThanOrEqualTo(1));
            Assert.That(s.Phase, Is.EqualTo(BattlePhase.Withdrawn));
            var end = ev.OfType<BattleEndedEvent>().Single();
            Assert.That(end.Victory, Is.False);
            Assert.That(end.Withdrawn, Is.True);
            Assert.That(end.Line, Is.EqualTo(stage.Withdraw.Line));
        }

        [Test]
        public void Without_a_withdraw_the_clangs_change_nothing()
        {
            var (s, _) = Battle(Plated(), Stage(("goblin", 2, 3), ("tank", 0, 0)), ("knight", 2, 4), ("cleric", 2, 0));

            var ev = BattleEngine.Apply(s, new MoveCommand { HeroId = Hero(s, "cleric").Id, Path = P((2, 1), (2, 2)) });

            Assert.That(s.Results.PlateClangs, Is.GreaterThanOrEqualTo(1));
            Assert.That(s.IsOver, Is.False);
            Assert.That(ev.OfType<BattleEndedEvent>().Any(), Is.False);
        }

        [Test]
        public void A_withdraw_needs_a_trigger_and_a_line()
        {
            var db = Plated();
            var stage = Stage(("goblin", 2, 3));
            stage.Id = "withdraw_bad";
            stage.Withdraw = new WithdrawDef();
            db.Stages[stage.Id] = stage;

            var problems = ContentLoader.Validate(db);

            Assert.That(problems, Has.Some.Contains("Withdraw needs AfterPlateClangs or AfterTurns"));
            Assert.That(problems, Has.Some.Contains("Withdraw needs a Commander Line"));
        }
    }
}
