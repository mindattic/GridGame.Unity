using System.Linq;
using GridGame.Core;
using GridGame.Core.Battle;
using GridGame.Core.Content;
using GridGame.Core.Rules;
using NUnit.Framework;
using static GridGame.Tests.TestKit;

namespace GridGame.Tests
{
    /// <summary>GG-T1777: the overworld's game animals - the deer runs, the boar and the buffalo charge.</summary>
    public class GameAnimalTests
    {
        private static readonly string[] Animals = { "deer", "buffalo", "boar" };

        private static ContentDb WithAnimals()
        {
            var db = Mini();
            foreach (var id in Animals) db.Classes[id] = Real.Classes[id];
            return db;
        }

        [Test]
        public void Game_animal_classes_load_and_validate()
        {
            var db = Real;
            foreach (var id in Animals)
            {
                Assert.That(db.Classes.ContainsKey(id), Is.True, id);
                var c = db.Classes[id];
                Assert.That(c.Team, Is.EqualTo(Team.Enemy), id);
                Assert.That(c.HasTag("GameAnimal"), Is.True, id);
                Assert.That(db.DropTables.ContainsKey(c.DropTable), Is.True, id);
                Assert.That(string.IsNullOrWhiteSpace(c.Description), Is.False, id);
            }
            Assert.That(db.Classes["deer"].Brain, Is.EqualTo(Brain.Flee));
            Assert.That(db.Classes["boar"].Brain, Is.EqualTo(Brain.Berserker));
            Assert.That(db.Classes["buffalo"].Brain, Is.EqualTo(Brain.Berserker));
            Assert.That(db.Classes["buffalo"].Heavy, Is.True);
            var problems = ContentLoader.Validate(db).Where(p => Animals.Any(a => p.Contains($"class {a}:") || p.Contains($"droptable {a}"))).ToList();
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
            Assert.That(EnemyPlanner.DescribeBrain(Brain.Flee), Is.Not.EqualTo(EnemyPlanner.DescribeBrain(Brain.Hunter)));
        }

        [Test]
        public void Deer_moves_away_from_an_adjacent_hero()
        {
            var (s, _) = Battle(WithAnimals(), Stage(("deer", 3, 3)), ("knight", 3, 4), ("cleric", 0, 7));
            var deer = s.Units.First(u => u.Def.Id == "deer" && u.Alive);
            var knight = Hero(s, "knight");
            Assert.That(deer.HpFraction, Is.EqualTo(1f), "unhurt: it runs anyway");
            Assert.That(EnemyPlanner.Retreats(s, deer), Is.True);
            var plan = EnemyPlanner.PlanStep(s, deer);
            Assert.That(plan.Manhattan(knight.Pos), Is.GreaterThan(1), $"planned {plan}");
        }

        [TestCase("boar")]
        [TestCase("buffalo")]
        public void Charging_game_moves_toward_and_holds_on_its_prey(string id)
        {
            var (s, _) = Battle(WithAnimals(), Stage((id, 3, 1)), ("knight", 3, 5), ("cleric", 0, 7));
            var animal = s.Units.First(u => u.Def.Id == id && u.Alive);
            var knight = Hero(s, "knight");
            int before = animal.Pos.Manhattan(knight.Pos);
            Assert.That(EnemyPlanner.PlanStep(s, animal).Manhattan(knight.Pos), Is.LessThan(before), "it closes in");

            animal.Pos = new Coord(3, 4);
            Assert.That(animal.IsAdjacentTo(knight), Is.True);
            Assert.That(EnemyPlanner.PlanStep(s, animal).Manhattan(knight.Pos), Is.EqualTo(1), "adjacent, it stays to strike");
            Assert.That(EnemyPlanner.Retreats(s, animal), Is.False);
        }
    }
}
