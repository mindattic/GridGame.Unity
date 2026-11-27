using System;

namespace GridGame.Core.World
{
    /// <summary>
    /// The overworld's clock (GG-T1678), ported from Unity.Rea.Overworld Core/Simulation/WorldClock.cs (Sun.vb's theTime and
    /// checkDayperiod) and Core/Content/TimeData.cs. The original's tick, command and light-fade plumbing and its calendar
    /// names are dropped: this is minutes since the world began, advanced by the caller, with the period they fall in.
    /// </summary>
    public sealed class WorldClock
    {
        readonly WorldTimeConfig config;

        public WorldClock(WorldTimeConfig config = null)
        {
            this.config = config ?? WorldTimeConfig.Default();
            if (this.config.Periods.Count == 0) throw new ArgumentException("The world day needs at least one period.", nameof(config));
            if (this.config.StartHour < 0 || this.config.StartHour > 23) throw new ArgumentOutOfRangeException(nameof(config));
            Minutes = this.config.StartHour * 60L;
        }

        public WorldTimeConfig Config => config;

        /// <summary>Minutes since the world began, at midnight starting day 1.</summary>
        public long Minutes { get; private set; }

        /// <summary>Moves the clock on; negative minutes are ignored.</summary>
        public void Advance(long minutes)
        {
            if (minutes > 0) Minutes += minutes;
        }

        /// <summary>The day, counting from 1.</summary>
        public int Day => (int)(Minutes / WorldTimeConfig.MinutesPerDay) + 1;

        public int MinuteOfDay => (int)(Minutes % WorldTimeConfig.MinutesPerDay);

        public int Hour => MinuteOfDay / 60;

        public int Minute => MinuteOfDay % 60;

        public int PeriodIndex => config.IndexAt(Hour);

        public WorldPeriod Period => config.Periods[PeriodIndex];

        /// <summary>How far through the current period the clock is, 0 at its first minute up to (not including) 1.</summary>
        public float PeriodProgress => MinutesIntoPeriod / (float)Period.LengthMinutes;

        int MinutesIntoPeriod => ((MinuteOfDay - Period.FromHour * 60) % WorldTimeConfig.MinutesPerDay + WorldTimeConfig.MinutesPerDay) % WorldTimeConfig.MinutesPerDay;

        /// <summary>The colour grade now (GG-T1679): the period's own grade, easing linearly into the next period's over the
        /// last <see cref="WorldTimeConfig.BlendMinutes"/> of the period so the change at each boundary is continuous.</summary>
        public ColorGrade CurrentGrade
        {
            get
            {
                var period = Period;
                int blend = Math.Min(Math.Max(config.BlendMinutes, 0), period.LengthMinutes);
                int left = period.LengthMinutes - MinutesIntoPeriod;
                if (blend == 0 || left > blend) return period.Grade;
                var next = config.Periods[(PeriodIndex + 1) % config.Periods.Count];
                return ColorGrade.Lerp(period.Grade, next.Grade, 1f - left / (float)blend);
            }
        }

        /// <summary>The time as "7:05 AM" (12 at midnight, PM from noon).</summary>
        public string TimeText
        {
            get
            {
                int h = Hour == 0 ? 12 : Hour > 12 ? Hour - 12 : Hour;
                return h + ":" + Minute.ToString("00") + (Hour < 12 ? " AM" : " PM");
            }
        }
    }
}
