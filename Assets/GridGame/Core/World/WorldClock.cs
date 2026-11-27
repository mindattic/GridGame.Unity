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
        public float PeriodProgress
        {
            get
            {
                var period = Period;
                int since = ((MinuteOfDay - period.FromHour * 60) % WorldTimeConfig.MinutesPerDay + WorldTimeConfig.MinutesPerDay) % WorldTimeConfig.MinutesPerDay;
                return since / (float)period.LengthMinutes;
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
