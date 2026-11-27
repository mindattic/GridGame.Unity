using System;
using GridGame.Core.Content;

namespace GridGame.Core.World
{
    /// <summary>Why a <see cref="Journey.Walk"/> stopped.</summary>
    public enum TravelStop
    {
        /// <summary>The minutes asked for ran out.</summary>
        Limit,
        /// <summary>The clock reached dusk: the camp prompt (BIBLE 28.3).</summary>
        Dusk,
        /// <summary>The player stopped now.</summary>
        Interrupted,
        Arrived,
        /// <summary>Speed is zero (weather), so walking on would only burn time.</summary>
        Stalled,
    }

    /// <summary>What one <see cref="Journey.Walk"/> did: the road covered and why it stopped.</summary>
    public sealed class JourneyStep
    {
        public TravelStep Step = new TravelStep();
        public TravelStop Stop;
        /// <summary>What to tell the player for <see cref="TravelStop.Dusk"/> and <see cref="TravelStop.Stalled"/>; else null.</summary>
        public string Message;
    }

    /// <summary>The caravan on its way (BIBLE 28.3, GG-T1682): walks minute by minute until the first stop event, so travel
    /// stops at dusk with the camp prompt, the player can Interrupt, arrival ends it and a zero-speed road stops it with a
    /// reason instead of spending the clock.</summary>
    public sealed class Journey
    {
        public const string CampPrompt = "Dusk is falling. Set up camp?";
        public const string StalledReason = "The caravan can't move in this weather.";

        public readonly TravelState State;

        /// <summary>The period whose start stops travel with <see cref="CampPrompt"/>.</summary>
        public string DuskPeriod = "Dusk";

        /// <summary>The player chose Continue at the camp prompt: dusk no longer stops the walk. Cleared once the clock is
        /// out of dusk and night, so the next dusk asks again.</summary>
        public bool ContinueIntoNight;

        public Journey(TravelState state)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
        }

        /// <summary>Walks up to <paramref name="maxMinutes"/> world minutes, stopping early at the first stop event.
        /// <paramref name="interrupt"/> is asked before each minute; true stops the walk where it is.</summary>
        public JourneyStep Walk(WorldClock clock, long maxMinutes, TravelConfig config = null, Func<bool> interrupt = null)
        {
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            config = config ?? TravelConfig.Default();
            var result = new JourneyStep();
            while (true)
            {
                if (State.Arrived) { result.Stop = TravelStop.Arrived; break; }
                string period = clock.Period.Name;
                bool dusk = Is(period, DuskPeriod);
                if (!dusk && !Is(period, config.NightPeriod)) ContinueIntoNight = false;
                if (dusk && !ContinueIntoNight) { result.Stop = TravelStop.Dusk; result.Message = CampPrompt; break; }
                if (interrupt != null && interrupt()) { result.Stop = TravelStop.Interrupted; break; }
                if (result.Step.Minutes >= maxMinutes) { result.Stop = TravelStop.Limit; break; }
                if (config.SpeedAt(clock) <= 0) { result.Stop = TravelStop.Stalled; result.Message = StalledReason; break; }

                var minute = Travel.Advance(State, clock, 1, config);
                result.Step.Minutes += minute.Minutes;
                result.Step.Distance += minute.Distance;
                result.Step.Passed.AddRange(minute.Passed);
                if (minute.ArrivedAt != null) result.Step.ArrivedAt = minute.ArrivedAt;
            }
            return result;
        }

        static bool Is(string period, string name) => string.Equals(period, name, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>What taking a roadsign gives: where the caravan lands, or why it can't go.</summary>
    public sealed class RoadsignTransfer
    {
        public bool Allowed;
        /// <summary>Why the sign refused; null when <see cref="Allowed"/>.</summary>
        public string Reason;
        /// <summary>The region the caravan moves to.</summary>
        public string Region;
        /// <summary>The node it arrives at on that region's map.</summary>
        public string Node;
        /// <summary>That region's sign it arrives at, which leads back.</summary>
        public RoadsignDef Arrival;
    }

    /// <summary>Roadsigns, the only way between regions (BIBLE 28.1, GG-T1682).</summary>
    public static class Roadsigns
    {
        public const string LockedReason = "the Commander can't read this road yet";

        /// <summary>The region's sign standing at a node, or null.</summary>
        public static RoadsignDef At(RegionDef region, string nodeId)
        {
            if (region == null || nodeId == null) return null;
            return region.Roadsigns.Find(s => s != null && string.Equals(s.Node, nodeId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Takes a sign: the region it leads to and the node of the sign it arrives at. A sign with a
        /// <see cref="RoadsignDef.RequiresStage"/> refuses with <see cref="LockedReason"/> until
        /// <paramref name="stageCleared"/> says that stage is cleared. Throws when the sign's road leads to a region or
        /// sign the world doesn't have.</summary>
        public static RoadsignTransfer Transfer(RoadsignDef sign, WorldDef world, Func<string, bool> stageCleared)
        {
            if (sign == null) throw new ArgumentNullException(nameof(sign));
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (sign.RequiresStage != null && (stageCleared == null || !stageCleared(sign.RequiresStage)))
                return new RoadsignTransfer { Reason = LockedReason };
            var region = world.TryRegion(sign.Leads)
                ?? throw new InvalidOperationException($"Roadsign '{sign.Id}' leads to region '{sign.Leads}', which the world doesn't have.");
            var arrival = region.TryRoadsign(sign.Arrives)
                ?? throw new InvalidOperationException($"Roadsign '{sign.Id}' arrives at sign '{sign.Arrives}', which region '{region.Id}' doesn't have.");
            return new RoadsignTransfer { Allowed = true, Region = region.Id, Node = arrival.Node, Arrival = arrival };
        }
    }
}
