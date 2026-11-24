using System;
using System.Collections.Generic;
using GridGame.Core;
using GridGame.Core.Battle;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>
    /// GG-T767 (bible §2.13): the <see cref="TurnMode"/> setting. PartyWindow is the default, the setup's mode reaches the
    /// state, the state clone keeps it, and a battle record stores it so a replay reproduces it.
    /// </summary>
    public class TurnModeTests
    {
        private static BattleSetup Setup(TurnMode? mode = null)
        {
            var setup = new BattleSetup { Stage = Stage(("tank", 5, 7)), Seed = 7, UnlockedSlots = 5 };
            setup.Party.Add(new HeroSpec { ClassId = "knight", Start = new Coord(0, 0) });
            if (mode.HasValue) setup.TurnMode = mode.Value;
            return setup;
        }

        [Test]
        public void Default_is_party_window()
        {
            Assert.That(default(TurnMode), Is.EqualTo(TurnMode.PartyWindow));
            Assert.That(new BattleSetup().TurnMode, Is.EqualTo(TurnMode.PartyWindow));
            var s = BattleEngine.Create(Mini(), Setup(), new List<BattleEvent>());
            Assert.That(s.TurnMode, Is.EqualTo(TurnMode.PartyWindow));
        }

        [TestCase(TurnMode.PartyWindow)]
        [TestCase(TurnMode.AnyHeroEveryTurn)]
        [TestCase(TurnMode.UnmovedThisRound)]
        [TestCase(TurnMode.HeroTimeline)]
        public void Setup_mode_reaches_the_state_and_the_clone_keeps_it(TurnMode mode)
        {
            var s = BattleEngine.Create(Mini(), Setup(mode), new List<BattleEvent>());
            Assert.That(s.TurnMode, Is.EqualTo(mode));
            Assert.That(s.Clone().TurnMode, Is.EqualTo(mode));
        }

        [TestCase(TurnMode.PartyWindow)]
        [TestCase(TurnMode.AnyHeroEveryTurn)]
        [TestCase(TurnMode.UnmovedThisRound)]
        [TestCase(TurnMode.HeroTimeline)]
        public void Recorded_mode_round_trips(TurnMode mode)
        {
            var setup = Setup(mode);
            var opening = new List<BattleEvent>();
            var s = BattleEngine.Create(Mini(), setup, opening);
            var doc = JObject.Parse(new BattleRecorder(s, setup, "test", opening).ToJson());

            Assert.That((string)doc[CommandCodec.TurnModeKey], Is.EqualTo(mode.ToString()), "stored by name");
            var read = CommandCodec.ReadTurnMode(doc);
            Assert.That(read, Is.EqualTo(mode));

            // A replay header written and read back gives the same mode, and a setup built from it the same state mode.
            Assert.That(CommandCodec.ReadTurnMode(CommandCodec.WriteTurnMode(new JObject(), mode)), Is.EqualTo(mode));
            var replay = Setup(read);
            Assert.That(BattleEngine.Create(Mini(), replay, new List<BattleEvent>()).TurnMode, Is.EqualTo(mode));
        }

        [Test]
        public void A_record_without_a_mode_reads_as_party_window_and_an_unknown_one_throws()
        {
            Assert.That(CommandCodec.ReadTurnMode(new JObject()), Is.EqualTo(TurnMode.PartyWindow));
            Assert.That(CommandCodec.ReadTurnMode(null), Is.EqualTo(TurnMode.PartyWindow));
            Assert.Throws<NotSupportedException>(() => CommandCodec.ReadTurnMode(new JObject { [CommandCodec.TurnModeKey] = "Chess" }));
            Assert.Throws<NotSupportedException>(() => CommandCodec.ReadTurnMode(new JObject { [CommandCodec.TurnModeKey] = "7" }));
        }
    }
}
