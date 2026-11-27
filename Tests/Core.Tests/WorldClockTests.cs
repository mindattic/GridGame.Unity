using GridGame.Core.World;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>The world clock and its day periods (GG-T1678, 28.3): boundaries, wrap past midnight, days, progress.</summary>
    public class WorldClockTests
    {
        static WorldClock At(int hour, int minute = 0)
        {
            var clock = new WorldClock();
            clock.Advance(24 * 60 + hour * 60 + minute - clock.MinuteOfDay);
            return clock;
        }

        [Test]
        public void Starts_at_seven_on_day_one_in_the_day_period()
        {
            var clock = new WorldClock();
            Assert.That(clock.Hour, Is.EqualTo(7));
            Assert.That(clock.Minute, Is.EqualTo(0));
            Assert.That(clock.Day, Is.EqualTo(1));
            Assert.That(clock.Period.Name, Is.EqualTo("Day"));
        }

        [TestCase(4, 59, "Night")]
        [TestCase(5, 0, "Dawn")]
        [TestCase(6, 59, "Dawn")]
        [TestCase(7, 0, "Day")]
        [TestCase(17, 59, "Day")]
        [TestCase(18, 0, "Dusk")]
        [TestCase(19, 59, "Dusk")]
        [TestCase(20, 0, "Night")]
        [TestCase(23, 59, "Night")]
        [TestCase(0, 0, "Night")]
        [TestCase(2, 30, "Night")]
        public void Period_boundaries_including_the_wrap_past_midnight(int hour, int minute, string expected)
        {
            Assert.That(At(hour, minute).Period.Name, Is.EqualTo(expected));
        }

        [Test]
        public void Advance_across_days_rolls_the_day_and_the_time()
        {
            var clock = new WorldClock();
            clock.Advance(17 * 60 + 30);
            Assert.That(clock.Day, Is.EqualTo(2));
            Assert.That(clock.Hour, Is.EqualTo(0));
            Assert.That(clock.Minute, Is.EqualTo(30));
            Assert.That(clock.Period.Name, Is.EqualTo("Night"));
            clock.Advance(2 * 24 * 60);
            Assert.That(clock.Day, Is.EqualTo(4));
            Assert.That(clock.Minute, Is.EqualTo(30));
        }

        [Test]
        public void Advance_ignores_negative_minutes()
        {
            var clock = new WorldClock();
            clock.Advance(-30);
            Assert.That(clock.Minutes, Is.EqualTo(7 * 60));
        }

        [Test]
        public void Progress_is_zero_at_the_start_of_a_period_and_rises_toward_one()
        {
            Assert.That(At(7).PeriodProgress, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(At(12, 30).PeriodProgress, Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(At(6).PeriodProgress, Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(At(17, 59).PeriodProgress, Is.LessThan(1f));
            Assert.That(At(17, 59).PeriodProgress, Is.GreaterThan(0.99f));
        }

        [Test]
        public void Progress_runs_through_the_night_across_midnight()
        {
            Assert.That(At(20).PeriodProgress, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(At(0).PeriodProgress, Is.EqualTo(4f / 9f).Within(1e-6f));
            Assert.That(At(0, 30).PeriodProgress, Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(At(4, 59).PeriodProgress, Is.LessThan(1f));
        }

        [Test]
        public void Progress_stays_in_range_for_every_minute_of_the_day()
        {
            var clock = At(0);
            for (int i = 0; i < 24 * 60; i++)
            {
                Assert.That(clock.PeriodProgress, Is.GreaterThanOrEqualTo(0f));
                Assert.That(clock.PeriodProgress, Is.LessThan(1f));
                clock.Advance(1);
            }
        }

        [Test]
        public void Time_text_uses_the_twelve_hour_clock()
        {
            Assert.That(At(0, 5).TimeText, Is.EqualTo("12:05 AM"));
            Assert.That(At(12).TimeText, Is.EqualTo("12:00 PM"));
            Assert.That(At(7, 9).TimeText, Is.EqualTo("7:09 AM"));
            Assert.That(At(23, 59).TimeText, Is.EqualTo("11:59 PM"));
        }
    }
}
