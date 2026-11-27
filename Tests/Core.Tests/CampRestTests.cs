using GridGame.Core.World;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>Camp night rules (GG-T1697, 28.6): meal, 8-hour sleep, Well Rested until the next nightfall.</summary>
    public class CampRestTests
    {
        static WorldClock At(int hour, int minute = 0)
        {
            var clock = new WorldClock();
            clock.Advance(24 * 60 + hour * 60 + minute - clock.MinuteOfDay);
            return clock;
        }

        [Test]
        public void Meal_and_full_sleep_gives_well_rested_until_next_nightfall()
        {
            var clock = At(21);
            var state = new CampNight { Food = 10 };
            Assert.That(CampRest.Eat(state, 4), Is.True);
            Assert.That(state.Food, Is.EqualTo(6));
            Assert.That(CampRest.Sleep(state, clock), Is.True);
            Assert.That(clock.Hour, Is.EqualTo(7));
            Assert.That(clock.Period.Name, Is.EqualTo("Day"));
            Assert.That(CampRest.IsWellRested(state, clock), Is.True);
            Assert.That(state.WellRestedUntil % WorldTimeConfig.MinutesPerDay, Is.EqualTo(20 * 60));
        }

        [Test]
        public void No_meal_gives_nothing()
        {
            var clock = At(21);
            var state = new CampNight { Food = 10 };
            Assert.That(CampRest.Sleep(state, clock), Is.False);
            Assert.That(CampRest.IsWellRested(state, clock), Is.False);
            Assert.That(state.Food, Is.EqualTo(10));
        }

        [Test]
        public void Woken_early_gives_nothing()
        {
            var clock = At(21);
            var state = new CampNight { Food = 10 };
            CampRest.Eat(state, 4);
            CampRest.BreakCampEarly(state, clock, 3 * 60);
            Assert.That(CampRest.IsWellRested(state, clock), Is.False);
            Assert.That(state.AteMeal, Is.False);
        }

        [Test]
        public void Sleeping_too_short_a_night_gives_nothing()
        {
            var clock = At(5);
            var state = new CampNight { Food = 10 };
            CampRest.Eat(state, 4);
            Assert.That(CampRest.Sleep(state, clock), Is.False);
            Assert.That(clock.Hour, Is.EqualTo(7));
        }

        [Test]
        public void Short_of_food_refuses_the_meal()
        {
            var state = new CampNight { Food = 3 };
            Assert.That(CampRest.Eat(state, 4), Is.False);
            Assert.That(state.Food, Is.EqualTo(3));
            Assert.That(state.AteMeal, Is.False);
        }

        [Test]
        public void Sleeping_from_2300_lands_at_morning()
        {
            var clock = At(23);
            int day = clock.Day;
            var state = new CampNight { Food = 4 };
            CampRest.Eat(state, 4);
            Assert.That(CampRest.Sleep(state, clock), Is.True);
            Assert.That(clock.Day, Is.EqualTo(day + 1));
            Assert.That(clock.Hour, Is.EqualTo(7));
            Assert.That(clock.Minute, Is.EqualTo(0));
        }

        [Test]
        public void Buff_expires_at_nightfall()
        {
            var clock = At(23);
            var state = new CampNight { Food = 4 };
            CampRest.Eat(state, 4);
            CampRest.Sleep(state, clock);
            clock.Advance(12 * 60 + 59);
            Assert.That(clock.Hour, Is.EqualTo(19));
            Assert.That(CampRest.IsWellRested(state, clock), Is.True);
            clock.Advance(1);
            Assert.That(clock.Period.Name, Is.EqualTo("Night"));
            Assert.That(CampRest.IsWellRested(state, clock), Is.False);
        }

        [Test]
        public void Config_sets_the_numbers()
        {
            var config = new CampRestConfig { MealFoodPerHero = 2, SleepHours = 12 };
            var state = new CampNight { Food = 7 };
            Assert.That(CampRest.Eat(state, 4, config), Is.False);
            state.Food = 8;
            Assert.That(CampRest.Eat(state, 4, config), Is.True);
            var clock = At(21);
            Assert.That(CampRest.Sleep(state, clock, config), Is.False);
            Assert.That(new CampRestConfig().WellRestedHpBonus, Is.EqualTo(0.10f));
            Assert.That(new CampRestConfig().WellRestedExtraOrbs, Is.EqualTo(1));
        }
    }
}
