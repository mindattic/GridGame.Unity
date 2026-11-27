using System.Collections.Generic;
using GridGame.Core.Content;
using GridGame.Core.World;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>The battle library picker (GG-T1701, 28.9): band, widening, cross-region fallback, recency, determinism.</summary>
    public class BattlePickerTests
    {
        static LibraryBattle B(string id, string region, int tier, BattleKind kind = BattleKind.Ambush) =>
            new LibraryBattle { Id = id, Region = region, Kind = kind, Tier = tier, Stage = new StageDef { Id = id } };

        static DifficultyResult Band(int min, int max) =>
            new DifficultyResult { TierMin = min, TierMax = max, LevelOffset = 2, EliteChance = 0.25f };

        [Test]
        public void Picks_Within_Band()
        {
            var lib = new List<LibraryBattle> { B("a", "r1", 2), B("b", "r1", 4), B("c", "r1", 5), B("d", "r1", 9) };
            for (int seed = 0; seed < 30; seed++)
            {
                var p = BattlePicker.Pick(lib, "r1", BattleKind.Ambush, Band(4, 5), seed);
                Assert.That(p.Battle.Id, Is.AnyOf("b", "c"));
                Assert.That(p.LevelOffset, Is.EqualTo(2));
                Assert.That(p.EliteChance, Is.EqualTo(0.25f));
            }
        }

        [Test]
        public void Ignores_Other_Kinds()
        {
            var lib = new List<LibraryBattle> { B("a", "r1", 4, BattleKind.Hunt), B("b", "r1", 4) };
            Assert.That(BattlePicker.Pick(lib, "r1", BattleKind.Ambush, Band(4, 4), 1).Battle.Id, Is.EqualTo("b"));
        }

        [Test]
        public void Widens_When_Band_Is_Empty()
        {
            var lib = new List<LibraryBattle> { B("a", "r1", 7), B("far", "r1", 10) };
            Assert.That(BattlePicker.Pick(lib, "r1", BattleKind.Ambush, Band(4, 5), 3).Battle.Id, Is.EqualTo("a"));
        }

        [Test]
        public void Prefers_Narrower_Widening()
        {
            var lib = new List<LibraryBattle> { B("near", "r1", 6), B("far", "r1", 8) };
            for (int seed = 0; seed < 20; seed++)
                Assert.That(BattlePicker.Pick(lib, "r1", BattleKind.Ambush, Band(4, 5), seed).Battle.Id, Is.EqualTo("near"));
        }

        [Test]
        public void Falls_Back_To_Other_Region_In_Band()
        {
            var lib = new List<LibraryBattle> { B("a", "r2", 4), B("b", "r2", 9) };
            Assert.That(BattlePicker.Pick(lib, "r1", BattleKind.Ambush, Band(4, 5), 3).Battle.Id, Is.EqualTo("a"));
        }

        [Test]
        public void Same_Region_Widened_Beats_Other_Region_In_Band()
        {
            var lib = new List<LibraryBattle> { B("home", "r1", 7), B("away", "r2", 4) };
            Assert.That(BattlePicker.Pick(lib, "r1", BattleKind.Ambush, Band(4, 5), 3).Battle.Id, Is.EqualTo("home"));
        }

        [Test]
        public void Null_When_Nothing_Fits()
        {
            var lib = new List<LibraryBattle> { B("a", "r2", 10), B("b", "r1", 4, BattleKind.Hunt) };
            Assert.That(BattlePicker.Pick(lib, "r1", BattleKind.Ambush, Band(2, 3), 3), Is.Null);
            Assert.That(BattlePicker.Pick(new List<LibraryBattle>(), "r1", BattleKind.Ambush, Band(2, 3), 3), Is.Null);
            Assert.That(BattlePicker.Pick(null, "r1", BattleKind.Ambush, Band(2, 3), 3), Is.Null);
        }

        [Test]
        public void Avoids_Recently_Played_When_Alternative_Exists()
        {
            var lib = new List<LibraryBattle> { B("a", "r1", 4), B("b", "r1", 4) };
            var recent = new HashSet<string> { "a" };
            for (int seed = 0; seed < 30; seed++)
                Assert.That(BattlePicker.Pick(lib, "r1", BattleKind.Ambush, Band(4, 4), seed, recent).Battle.Id, Is.EqualTo("b"));
        }

        [Test]
        public void Repeats_When_Only_Choice_Was_Recent()
        {
            var lib = new List<LibraryBattle> { B("a", "r1", 4) };
            var recent = new HashSet<string> { "a" };
            Assert.That(BattlePicker.Pick(lib, "r1", BattleKind.Ambush, Band(4, 4), 1, recent).Battle.Id, Is.EqualTo("a"));
        }

        [Test]
        public void Deterministic_For_Seed_And_Library_Order()
        {
            var lib = new List<LibraryBattle>();
            for (int i = 0; i < 12; i++) lib.Add(B("b" + i, "r1", 4));
            var rev = new List<LibraryBattle>(lib);
            rev.Reverse();
            for (int seed = 0; seed < 20; seed++)
            {
                var one = BattlePicker.Pick(lib, "r1", BattleKind.Ambush, Band(3, 5), seed).Battle.Id;
                Assert.That(BattlePicker.Pick(lib, "r1", BattleKind.Ambush, Band(3, 5), seed).Battle.Id, Is.EqualTo(one));
                Assert.That(BattlePicker.Pick(rev, "r1", BattleKind.Ambush, Band(3, 5), seed).Battle.Id, Is.EqualTo(one));
            }
        }
    }
}
