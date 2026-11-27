using System;
using System.Collections.Generic;

namespace GridGame.Core.World
{
    /// <summary>One part of the day (Dawn, Day, Dusk, Night) as hours on the 24-hour clock. <see cref="ToHour"/> may be
    /// earlier than <see cref="FromHour"/>: the period then runs past midnight (Night 20 to 5).</summary>
    public sealed class WorldPeriod
    {
        public string Name;
        public int FromHour;
        public int ToHour;

        /// <summary>The colour grade the world wears at the heart of this period (GG-T1679).</summary>
        public ColorGrade Grade = ColorGrade.Neutral;

        public WorldPeriod(string name, int fromHour, int toHour, ColorGrade? grade = null)
        {
            Name = name;
            FromHour = fromHour;
            ToHour = toHour;
            if (grade.HasValue) Grade = grade.Value;
        }

        /// <summary>The period's length in minutes; a period whose two hours match is the whole day.</summary>
        public int LengthMinutes
        {
            get
            {
                int hours = ((ToHour - FromHour) % 24 + 24) % 24;
                return (hours == 0 ? 24 : hours) * 60;
            }
        }

        public bool ContainsHour(int hour)
        {
            hour = ((hour % 24) + 24) % 24;
            if (FromHour == ToHour) return true;
            return FromHour < ToHour ? hour >= FromHour && hour < ToHour : hour >= FromHour || hour < ToHour;
        }
    }

    /// <summary>The periods of the world's day as data (GG-T1678), each with its colour grade (GG-T1679).</summary>
    public sealed class WorldTimeConfig
    {
        public const int MinutesPerDay = 24 * 60;

        /// <summary>The hour of day the clock shows when a world begins.</summary>
        public int StartHour = 7;

        /// <summary>Minutes at the end of each period over which its grade eases into the next period's, clamped to the
        /// period's length: Day and Night hold their grade until their last half hour, Dawn and Dusk are mostly transition.</summary>
        public int BlendMinutes = 30;

        public List<WorldPeriod> Periods =new List<WorldPeriod>();

        public static WorldTimeConfig Default()
        {
            return new WorldTimeConfig
            {
                StartHour = 7,
                Periods = new List<WorldPeriod>
                {
                    new WorldPeriod("Dawn", 5, 7, new ColorGrade
                    {
                        HueShift = -6f, Saturation = 1.05f, Brightness = 0.92f, Contrast = 0.9f,
                        TintR = 1f, TintG = 0.72f, TintB = 0.78f, TintStrength = 0.22f,
                    }),
                    new WorldPeriod("Day", 7, 18),
                    new WorldPeriod("Dusk", 18, 20, new ColorGrade
                    {
                        HueShift = -10f, Saturation = 1.1f, Brightness = 0.85f, Contrast = 1.15f,
                        TintR = 1f, TintG = 0.62f, TintB = 0.25f, TintStrength = 0.28f,
                    }),
                    new WorldPeriod("Night", 20, 5, new ColorGrade
                    {
                        HueShift = 12f, Saturation = 0.75f, Brightness = 0.6f, Contrast = 1f,
                        TintR = 0x5a / 255f, TintG = 0x3f / 255f, TintB = 0x9e / 255f, TintStrength = 0.5f,
                    }),
                },
            };
        }

        public int IndexAt(int hour)
        {
            for (int i = 0; i < Periods.Count; i++)
                if (Periods[i].ContainsHour(hour)) return i;
            throw new InvalidOperationException("No world period covers hour " + hour + ".");
        }

        public WorldPeriod PeriodAt(int hour) => Periods[IndexAt(hour)];
    }
}
