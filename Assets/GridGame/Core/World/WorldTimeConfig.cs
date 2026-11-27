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

        public WorldPeriod(string name, int fromHour, int toHour)
        {
            Name = name;
            FromHour = fromHour;
            ToHour = toHour;
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

    /// <summary>The periods of the world's day as data (GG-T1678). Colour grades per period belong to the grade table
    /// (GG-T1679), not here.</summary>
    public sealed class WorldTimeConfig
    {
        public const int MinutesPerDay = 24 * 60;

        /// <summary>The hour of day the clock shows when a world begins.</summary>
        public int StartHour = 7;

        public List<WorldPeriod> Periods = new List<WorldPeriod>();

        public static WorldTimeConfig Default()
        {
            return new WorldTimeConfig
            {
                StartHour = 7,
                Periods = new List<WorldPeriod>
                {
                    new WorldPeriod("Dawn", 5, 7),
                    new WorldPeriod("Day", 7, 18),
                    new WorldPeriod("Dusk", 18, 20),
                    new WorldPeriod("Night", 20, 5),
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
