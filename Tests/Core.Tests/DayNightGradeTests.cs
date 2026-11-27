using GridGame.Core.World;
using NUnit.Framework;

namespace GridGame.Tests
{
    /// <summary>The day/night colour grades as data (GG-T1679, 28.3): neutral noon, purple (not black) midnight, smooth blends.</summary>
    public class DayNightGradeTests
    {
        static WorldClock At(int hour, int minute = 0)
        {
            var clock = new WorldClock();
            clock.Advance(24 * 60 + hour * 60 + minute - clock.MinuteOfDay);
            return clock;
        }

        static void Grey(WorldClock clock, out float r, out float g, out float b)
        {
            r = g = b = 0.5f;
            clock.CurrentGrade.ApplyToRgb(ref r, ref g, ref b);
        }

        [Test]
        public void Mid_grey_at_noon_is_unchanged()
        {
            Grey(At(12), out var r, out var g, out var b);
            Assert.That(r, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(g, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(b, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void Mid_grey_at_midnight_is_purple_and_not_near_black()
        {
            Grey(At(0), out var r, out var g, out var b);
            Assert.That(b, Is.GreaterThan(r));
            Assert.That(r, Is.GreaterThan(g));
            Assert.That(0.299f * r + 0.587f * g + 0.114f * b, Is.GreaterThan(0.25f));
        }

        [Test]
        public void Grade_holds_mid_period_and_blends_over_the_last_blend_minutes()
        {
            var day = WorldTimeConfig.Default().PeriodAt(12).Grade;
            var dusk = WorldTimeConfig.Default().PeriodAt(18).Grade;
            Assert.That(At(12).CurrentGrade.TintStrength, Is.EqualTo(day.TintStrength));
            Assert.That(At(17, 29).CurrentGrade.TintStrength, Is.EqualTo(day.TintStrength));
            Assert.That(At(17, 45).CurrentGrade.TintStrength, Is.EqualTo((day.TintStrength + dusk.TintStrength) / 2f).Within(1e-4f));
            Assert.That(At(18).CurrentGrade.TintStrength, Is.EqualTo(dusk.TintStrength).Within(1e-4f));
        }

        [Test]
        public void Grade_is_continuous_across_every_period_boundary()
        {
            foreach (int hour in new[] { 5, 7, 18, 20 })
            {
                var before = At(hour - 1, 59).CurrentGrade;
                var after = At(hour).CurrentGrade;
                Assert.That(System.Math.Abs(before.Brightness - after.Brightness), Is.LessThan(0.05f), "hour " + hour);
                Assert.That(System.Math.Abs(before.TintStrength - after.TintStrength), Is.LessThan(0.05f), "hour " + hour);
            }
        }

        [Test]
        public void Night_blends_into_dawn_across_the_midnight_wrap()
        {
            var night = WorldTimeConfig.Default().PeriodAt(22).Grade;
            Assert.That(At(3).CurrentGrade.Brightness, Is.EqualTo(night.Brightness));
            Assert.That(At(4, 45).CurrentGrade.Brightness, Is.GreaterThan(night.Brightness));
        }

        [Test]
        public void Dawn_is_warm_rose_and_dusk_is_amber()
        {
            var dawn = WorldTimeConfig.Default().PeriodAt(6).Grade;
            var dusk = WorldTimeConfig.Default().PeriodAt(19).Grade;
            Assert.That(dawn.Contrast, Is.LessThan(1f));
            Assert.That(dawn.TintR, Is.GreaterThan(dawn.TintG));
            Assert.That(dawn.TintB, Is.GreaterThan(dawn.TintG));
            Assert.That(dusk.TintR, Is.GreaterThan(dusk.TintG));
            Assert.That(dusk.TintG, Is.GreaterThan(dusk.TintB));
            Assert.That(dusk.Brightness, Is.LessThan(1f));
        }
    }
}
