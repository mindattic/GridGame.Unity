using System;
using GridGame.Core.World;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>The overworld Difficulty function (GG-T1700, 28.9): tier band, level offset and elite chance from the factors.</summary>
    public class WorldDifficultyTests
    {
        static DifficultyInput Calm(int region = 5) => new DifficultyInput
        {
            RegionIndex = region,
            Kind = BattleKind.Hunt,
            Weather = WeatherKind.Clear,
            PartyLevel = 10,
            PartySize = 3,
        };

        static void AssertNotEasier(DifficultyResult harder, DifficultyResult easier)
        {
            Assert.That(harder.TierMin, Is.GreaterThanOrEqualTo(easier.TierMin));
            Assert.That(harder.TierMax, Is.GreaterThanOrEqualTo(easier.TierMax));
            Assert.That(harder.LevelOffset, Is.GreaterThanOrEqualTo(easier.LevelOffset));
            Assert.That(harder.EliteChance, Is.GreaterThanOrEqualTo(easier.EliteChance));
        }

        static void AssertMonotonic(Func<DifficultyInput, int, DifficultyInput> step, int steps)
        {
            var first = Difficulty.Evaluate(step(Calm(), 0));
            var previous = first;
            for (int i = 1; i <= steps; i++)
            {
                var next = Difficulty.Evaluate(step(Calm(), i));
                AssertNotEasier(next, previous);
                previous = next;
            }
            Assert.That(previous.TierMin > first.TierMin || previous.LevelOffset > first.LevelOffset || previous.EliteChance > first.EliteChance, Is.True);
        }

        [Test]
        public void Region_raises_the_base_tier()
        {
            AssertMonotonic((input, i) => { input.RegionIndex = i; return input; }, 14);
            Assert.That(Difficulty.Evaluate(Calm(14)).TierMin, Is.GreaterThan(Difficulty.Evaluate(Calm(0)).TierMin));
        }

        [Test]
        public void Distance_from_road_pushes_up()
        {
            AssertMonotonic((input, i) => { input.DistanceFromRoad = i * 100f; return input; }, 20);
        }

        [Test]
        public void Outside_all_circles_pushes_up()
        {
            AssertMonotonic((input, i) => { input.OutsideAllCircles = i > 0; return input; }, 1);
        }

        [Test]
        public void Night_pushes_up()
        {
            AssertMonotonic((input, i) => { input.IsNight = i > 0; return input; }, 1);
        }

        [Test]
        public void Danger_pushes_up()
        {
            AssertMonotonic((input, i) => { input.DangerAtTrigger = i / 10f; return input; }, 10);
        }

        [Test]
        public void Bad_weather_pushes_up()
        {
            var weathers = new[] { WeatherKind.Clear, WeatherKind.Overcast, WeatherKind.Mist, WeatherKind.Rain, WeatherKind.Storm };
            AssertMonotonic((input, i) => { input.Weather = weathers[i]; return input; }, weathers.Length - 1);
        }

        [Test]
        public void Hunting_an_area_rises_slowly_with_a_cap()
        {
            AssertMonotonic((input, i) => { input.AreaHuntCount = i; return input; }, 30);
            var once = Calm();
            once.AreaHuntCount = 1;
            Assert.That(Difficulty.Evaluate(once).LevelOffset, Is.EqualTo(Difficulty.Evaluate(Calm()).LevelOffset));
            var many = Calm();
            many.AreaHuntCount = 1000;
            var capped = Calm();
            capped.AreaHuntCount = 100;
            Assert.That(Difficulty.Evaluate(many), Is.EqualTo(Difficulty.Evaluate(capped)));
        }

        [Test]
        public void Harder_kinds_push_up()
        {
            var hunt = Difficulty.Evaluate(Calm());
            foreach (var kind in new[] { BattleKind.Ambush, BattleKind.Defend, BattleKind.Guardian, BattleKind.WildBoss })
            {
                var input = Calm();
                input.Kind = kind;
                AssertNotEasier(Difficulty.Evaluate(input), hunt);
            }
        }

        [Test]
        public void Woken_waystones_ease()
        {
            var input = Calm();
            input.IsNight = true;
            input.OutsideAllCircles = true;
            var bare = Difficulty.Evaluate(input);
            input.NearbyWokenWaystones = 2;
            var eased = Difficulty.Evaluate(input);
            AssertNotEasier(bare, eased);
            Assert.That(eased.LevelOffset, Is.LessThan(bare.LevelOffset));
        }

        [Test]
        public void Small_party_eases()
        {
            var input = Calm();
            input.IsNight = true;
            var full = Difficulty.Evaluate(input);
            input.PartySize = 1;
            var alone = Difficulty.Evaluate(input);
            AssertNotEasier(full, alone);
            Assert.That(alone.LevelOffset, Is.LessThan(full.LevelOffset));
        }

        [Test]
        public void Clamps_at_the_extremes()
        {
            var config = new DifficultyConfig();
            var worst = new DifficultyInput
            {
                RegionIndex = 99,
                Kind = BattleKind.WildBoss,
                DistanceFromRoad = 1e9f,
                OutsideAllCircles = true,
                DangerAtTrigger = 50f,
                IsNight = true,
                Weather = WeatherKind.Storm,
                PartyLevel = 99,
                PartySize = 3,
                AreaHuntCount = int.MaxValue,
                StoryProgress = int.MaxValue,
            };
            var hard = Difficulty.Evaluate(worst, config);
            Assert.That(hard.TierMin, Is.EqualTo(Difficulty.MaxTier));
            Assert.That(hard.TierMax, Is.EqualTo(Difficulty.MaxTier));
            Assert.That(hard.LevelOffset, Is.EqualTo(config.MaxLevelOffset));
            Assert.That(hard.EliteChance, Is.EqualTo(config.MaxEliteChance));

            var best = new DifficultyInput
            {
                RegionIndex = -7,
                Kind = BattleKind.GameAnimals,
                DistanceFromRoad = -500f,
                DangerAtTrigger = -3f,
                PartyLevel = 1,
                PartySize = 0,
                AreaHuntCount = -10,
                StoryProgress = -10,
                NearbyWokenWaystones = int.MaxValue,
            };
            var easy = Difficulty.Evaluate(best, config);
            Assert.That(easy.TierMin, Is.EqualTo(Difficulty.MinTier));
            Assert.That(easy.TierMax, Is.GreaterThanOrEqualTo(easy.TierMin));
            Assert.That(easy.LevelOffset, Is.EqualTo(0), "foes never drop below level 1");
            Assert.That(easy.EliteChance, Is.EqualTo(0f));

            best.PartyLevel = 50;
            Assert.That(Difficulty.Evaluate(best, config).LevelOffset, Is.EqualTo(config.MinLevelOffset));
        }

        [Test]
        public void Story_kinds_keep_their_authored_level()
        {
            foreach (var kind in new[] { BattleKind.Story, BattleKind.SideStory })
            {
                var calm = Calm();
                calm.Kind = kind;
                var stormy = calm;
                stormy.IsNight = true;
                stormy.OutsideAllCircles = true;
                stormy.DistanceFromRoad = 2000f;
                stormy.DangerAtTrigger = 1f;
                stormy.Weather = WeatherKind.Storm;
                stormy.AreaHuntCount = 20;
                stormy.NearbyWokenWaystones = 3;
                stormy.PartySize = 1;

                var result = Difficulty.Evaluate(stormy);
                Assert.That(result, Is.EqualTo(Difficulty.Evaluate(calm)));
                Assert.That(result.LevelOffset, Is.EqualTo(0));
                Assert.That(result.EliteChance, Is.EqualTo(0f));
            }
        }

        [Test]
        public void First_region_daytime_on_road_hunt_is_the_easiest_band()
        {
            var easiest = Difficulty.Evaluate(Calm(0));
            Assert.That(easiest.TierMin, Is.EqualTo(Difficulty.MinTier));
            Assert.That(easiest.LevelOffset, Is.EqualTo(0));

            var pushes = new Func<DifficultyInput, DifficultyInput>[]
            {
                i => { i.RegionIndex = 1; return i; },
                i => { i.IsNight = true; return i; },
                i => { i.OutsideAllCircles = true; return i; },
                i => { i.DistanceFromRoad = 600f; return i; },
                i => { i.DangerAtTrigger = 1f; return i; },
                i => { i.Weather = WeatherKind.Storm; return i; },
                i => { i.Kind = BattleKind.WildBoss; return i; },
                i => { i.AreaHuntCount = 10; return i; },
            };
            foreach (var push in pushes) AssertNotEasier(Difficulty.Evaluate(push(Calm(0))), easiest);
        }

        [Test]
        public void Deterministic()
        {
            var input = Calm(7);
            input.IsNight = true;
            input.DangerAtTrigger = 0.37f;
            input.DistanceFromRoad = 333f;
            input.Weather = WeatherKind.Rain;
            Assert.That(Difficulty.Evaluate(input), Is.EqualTo(Difficulty.Evaluate(input)));
        }
    }
}
