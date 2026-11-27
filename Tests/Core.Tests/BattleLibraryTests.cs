using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GridGame.Core;
using GridGame.Core.Content;
using GridGame.Core.World;
using Newtonsoft.Json;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T1780: the battle library (GG-T1709, BIBLE §28.9): battles/index.json's files load into ContentDb.Battles,
    /// and <see cref="BattleLibrary.Validate"/> rejects duplicate ids, unknown classes, non-library kinds, tiers outside
    /// 1..10, off-board units and blocks, and a wave count outside 1..5 (GG-T1788).</summary>
    public class BattleLibraryTests
    {
        const string TestFile = "greenvalley/_test";

        /// <summary>A private copy of the real content, so these tests can swap its library without touching
        /// the shared <see cref="TestKit.Real"/>.</summary>
        private static ContentDb fresh;
        private static ContentDb Fresh => fresh ??= ContentLoader.Load(name =>
        {
            var p = Path.Combine(ContentDir, name + ".json");
            return File.Exists(p) ? File.ReadAllText(p) : null;
        });

        static bool IsLibraryProblem(string s) => s.StartsWith("battle", StringComparison.OrdinalIgnoreCase);

        /// <summary>A battle that passes every check: greenvalley, Ambush, tier 3, five waves of a real foe on the board.</summary>
        static LibraryBattle Valid(string id)
        {
            var stage = new StageDef { Blocks = { new Coord(0, 4) } };
            for (int i = 0; i < BattleLibrary.MaxWaves; i++)
                stage.Waves.Add(new WaveDef { Enemies = { new SpawnDef { Class = "wolf02", Level = 1, X = 1, Y = 1 } } });
            return new LibraryBattle { Id = id, Region = "greenvalley", Biome = "moor", Kind = BattleKind.Ambush, Tier = 3, Stage = stage };
        }

        /// <summary>Loads <paramref name="json"/> as the library's only file and returns the load problems it added
        /// plus <see cref="BattleLibrary.Validate"/>'s problems.</summary>
        static List<string> LoadJson(string json)
        {
            var db = Fresh;
            db.Battles.Clear();
            int before = db.LoadProblems.Count;
            var index = JsonConvert.SerializeObject(new[] { TestFile });
            BattleLibrary.Load(db, name =>
                name == BattleLibrary.IndexFile ? index :
                name == $"{BattleLibrary.Folder}/{TestFile}" ? json : null);
            var p = db.LoadProblems.Skip(before).ToList();
            db.LoadProblems.RemoveRange(before, db.LoadProblems.Count - before);
            BattleLibrary.Validate(db, p);
            return p;
        }

        static List<string> Load(params LibraryBattle[] battles) =>
            LoadJson(JsonConvert.SerializeObject(battles, ContentLoader.Settings));

        [Test]
        public void Greenvalley_examples_load_with_no_library_problems()
        {
            var db = Real;
            Assert.That(db.Battles.Where(b => b.Region == "greenvalley"), Is.Not.Empty);
            Assert.That(db.Battles.All(b => b.Stage != null && b.Stage.Id == b.Id), Is.True, "a battle's Stage takes its Id");
            var p = new List<string>(db.LoadProblems.Where(IsLibraryProblem));
            BattleLibrary.Validate(db, p);
            Assert.That(p, Is.Empty);
        }

        [Test]
        public void A_valid_battle_has_no_problems()
        {
            Assert.That(Load(Valid("t-ok")), Is.Empty);
            Assert.That(Fresh.Battles.Select(b => b.Id), Is.EqualTo(new[] { "t-ok" }));
        }

        [Test]
        public void A_duplicate_id_fails()
        {
            var p = Load(Valid("t-dup"), Valid("T-DUP"));
            Assert.That(p, Has.Some.Contains("id used twice"));
        }

        [Test]
        public void An_unknown_class_fails()
        {
            var b = Valid("t-class");
            b.Stage.Waves[2].Enemies[0].Class = "no_such_class";
            Assert.That(Load(b), Has.Some.Contains("unknown class 'no_such_class'"));
        }

        [Test]
        public void Kind_story_fails()
        {
            var b = Valid("t-story");
            b.Kind = BattleKind.Story;
            Assert.That(Load(b), Has.Some.Contains("is not a library kind"));
        }

        [Test]
        public void An_unknown_kind_string_throws_a_format_exception()
        {
            var json = JsonConvert.SerializeObject(new[] { Valid("t-kind") }, ContentLoader.Settings).Replace("\"Ambush\"", "\"Picnic\"");
            Assert.That(json, Does.Contain("Picnic"));
            Assert.That(() => LoadJson(json), Throws.TypeOf<FormatException>());
        }

        [TestCase(0)]
        [TestCase(11)]
        public void A_tier_outside_1_to_10_fails(int tier)
        {
            var b = Valid("t-tier");
            b.Tier = tier;
            Assert.That(Load(b), Has.Some.Contains($"Tier {tier} outside"));
        }

        [TestCase(1)]
        [TestCase(10)]
        public void Tiers_1_and_10_pass(int tier)
        {
            var b = Valid("t-tier-ok");
            b.Tier = tier;
            Assert.That(Load(b), Is.Empty);
        }

        [Test]
        public void An_off_board_unit_fails()
        {
            var b = Valid("t-unit");
            var e = b.Stage.Waves[0].Enemies[0];
            e.X = GridGame.Core.Battle.BattleConfig.Cols;
            e.Y = 0;
            Assert.That(Load(b), Has.Some.Contains("is off the board"));
        }

        [Test]
        public void A_random_unit_at_minus_one_passes()
        {
            var b = Valid("t-random");
            var e = b.Stage.Waves[0].Enemies[0];
            e.X = -1;
            e.Y = -1;
            Assert.That(Load(b), Is.Empty);
        }

        [Test]
        public void An_off_board_block_fails()
        {
            var b = Valid("t-block");
            b.Stage.Blocks.Add(new Coord(0, GridGame.Core.Battle.BattleConfig.Rows));
            Assert.That(Load(b), Has.Some.Contains($"block (0,{GridGame.Core.Battle.BattleConfig.Rows}) is off the board"));
        }

        static LibraryBattle WithWaves(string id, int waves)
        {
            var b = Valid(id);
            while (b.Stage.Waves.Count > waves) b.Stage.Waves.RemoveAt(b.Stage.Waves.Count - 1);
            while (b.Stage.Waves.Count < waves) b.Stage.Waves.Add(Valid("x").Stage.Waves[0]);
            return b;
        }

        [TestCase(0)]
        [TestCase(6)]
        public void A_wave_count_outside_1_to_5_fails(int waves)
        {
            var b = WithWaves("t-waves", waves);
            Assert.That(b.Stage.Waves.Count, Is.EqualTo(waves));
            Assert.That(Load(b), Has.Some.Contains($"needs {BattleLibrary.MinWaves}..{BattleLibrary.MaxWaves} waves, has {waves}"));
        }

        [TestCase(1)]
        [TestCase(5)]
        public void Wave_counts_1_and_5_pass(int waves)
        {
            var b = WithWaves("t-waves-ok", waves);
            Assert.That(b.Stage.Waves.Count, Is.EqualTo(waves));
            Assert.That(Load(b), Is.Empty);
        }
    }
}
