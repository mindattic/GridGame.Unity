using System;

namespace GridGame.Core.World
{
    /// <summary>Camp rest numbers as data (GG-T1697, BIBLE 28.6).</summary>
    public sealed class CampRestConfig
    {
        /// <summary>Food one hero eats at a meal; the party's meal costs this times the party size.</summary>
        public int MealFoodPerHero = 1;

        /// <summary>Hours of sleep a night needs to count as full.</summary>
        public int SleepHours = 8;

        /// <summary>Fraction of max HP Well Rested adds in every battle.</summary>
        public float WellRestedHpBonus = 0.10f;

        /// <summary>Points Well Rested grants the party mana pool before wave 1 of every battle (§3.1.0).</summary>
        public int WellRestedMana = 100;

        /// <summary>Name of the period whose start is the morning a night's sleep runs to.</summary>
        public string MorningPeriod = "Day";

        /// <summary>Name of the period whose start ends Well Rested.</summary>
        public string NightfallPeriod = "Night";
    }

    /// <summary>The caravan's camp-night state: Food, whether tonight's meal is eaten, and the Well Rested window.</summary>
    public sealed class CampNight
    {
        public int Food;
        public bool AteMeal;

        /// <summary>World minutes the last sleep began, or -1 when none yet.</summary>
        public long SleptFrom = -1;

        /// <summary>World minutes Well Rested lasts until (the next nightfall), or 0 when not rested.</summary>
        public long WellRestedUntil;
    }

    /// <summary>The rules of a camp night, pure over <see cref="CampNight"/> and <see cref="WorldClock"/> (GG-T1697, 28.6):
    /// a meal plus a full sleep gives Well Rested until the next nightfall; anything less gives nothing and costs nothing
    /// more than the meal. There is no starvation penalty.</summary>
    public static class CampRest
    {
        public static int MealCost(int partySize, CampRestConfig config = null)
        {
            config = config ?? new CampRestConfig();
            return Math.Max(partySize, 0) * config.MealFoodPerHero;
        }

        /// <summary>Spends the party's meal from Food; refused (nothing spent) when Food is short or tonight's meal is eaten.</summary>
        public static bool Eat(CampNight state, int partySize, CampRestConfig config = null)
        {
            if (state.AteMeal) return false;
            int cost = MealCost(partySize, config);
            if (state.Food < cost) return false;
            state.Food -= cost;
            state.AteMeal = true;
            return true;
        }

        /// <summary>Sleeps through to the next morning, moving the clock there. Returns true when Well Rested was granted:
        /// a meal was eaten and at least <see cref="CampRestConfig.SleepHours"/> passed.</summary>
        public static bool Sleep(CampNight state, WorldClock clock, CampRestConfig config = null)
        {
            config = config ?? new CampRestConfig();
            long from = clock.Minutes;
            long wake = NextStartOf(clock, config.MorningPeriod);
            clock.Advance(wake - from);
            state.SleptFrom = from;
            bool granted = state.AteMeal && wake - from >= config.SleepHours * 60L;
            state.AteMeal = false;
            state.WellRestedUntil = granted ? NextStartOf(clock, config.NightfallPeriod) : 0;
            return granted;
        }

        /// <summary>Camp is broken after only <paramref name="minutesSlept"/> minutes: the clock moves on, the meal is gone
        /// and nothing is gained.</summary>
        public static void BreakCampEarly(CampNight state, WorldClock clock, int minutesSlept = 0)
        {
            state.SleptFrom = clock.Minutes;
            clock.Advance(minutesSlept);
            state.AteMeal = false;
            state.WellRestedUntil = 0;
        }

        public static bool IsWellRested(CampNight state, WorldClock clock) => clock.Minutes < state.WellRestedUntil;

        /// <summary>The world minute the named period next starts, strictly after the clock's now.</summary>
        static long NextStartOf(WorldClock clock, string periodName)
        {
            var periods = clock.Config.Periods;
            int index = periods.FindIndex(p => p.Name == periodName);
            if (index < 0) throw new ArgumentException("The world day has no period named " + periodName + ".");
            long start = clock.Minutes - clock.MinuteOfDay + periods[index].FromHour * 60L;
            return start > clock.Minutes ? start : start + WorldTimeConfig.MinutesPerDay;
        }
    }
}
