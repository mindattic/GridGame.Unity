using System;
using System.Collections.Generic;

namespace GridGame.Core.World
{
    /// <summary>Tuning for woken Mana Waystones (BIBLE 28.8): Mana Dust gathering at each stone and how far each eases the Manalith.</summary>
    public sealed class WaystoneDustConfig
    {
        public double DustPerHour = 0.5;
        public int CapPerStone = 12;
        public float ManalithStepPerStone = 0.006f;
        public float MinSizeFactor = 0.7f;
    }

    /// <summary>Mana Dust gathering at woken waystones (BIBLE 28.8, GG-T1693): each woken stone accrues Dust per world hour
    /// from the minute it was woken or last collected, up to a per-stone cap, paid out when the caravan passes or camps inside
    /// its circle. Core returns the Dust amount; adding it to the wallet is the caller's job (Meta/DustEconomy).</summary>
    public sealed class WaystoneDust
    {
        public static readonly WaystoneDustConfig Default = new WaystoneDustConfig();

        public readonly WaystoneState State;
        public readonly WaystoneDustConfig Config;
        public readonly Dictionary<string, double> SinceMinutes = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        public WaystoneDust(WaystoneState state, WaystoneDustConfig config = null)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            Config = config ?? Default;
        }

        /// <summary>Wakes the stone (see <see cref="WaystoneState.Wake"/>) and starts its Dust clock at <paramref name="nowMinutes"/>.</summary>
        public bool Wake(string nodeId, string partyNodeId, double nowMinutes)
        {
            bool wasWoken = State.IsWoken(nodeId);
            if (!State.Wake(nodeId, partyNodeId)) return false;
            if (!wasWoken) SinceMinutes[State.Region.TryNode(nodeId).Id] = nowMinutes;
            return true;
        }

        /// <summary>Dust waiting at a woken stone, whole units, capped; 0 for a dormant or unknown stone.</summary>
        public int Pending(string nodeId, double nowMinutes)
        {
            if (!State.IsWoken(nodeId)) return 0;
            if (!SinceMinutes.TryGetValue(nodeId, out double since)) return 0;
            double hours = Math.Max(0.0, nowMinutes - since) / 60.0;
            double dust = Math.Min(hours * Config.DustPerHour, Config.CapPerStone);
            return Math.Max(0, (int)Math.Floor(dust + 1e-9));
        }

        /// <summary>Takes the Dust waiting at the stone and restarts its clock at <paramref name="nowMinutes"/>.</summary>
        public int Collect(string nodeId, double nowMinutes)
        {
            int dust = Pending(nodeId, nowMinutes);
            if (State.IsWoken(nodeId) && SinceMinutes.ContainsKey(nodeId)) SinceMinutes[nodeId] = nowMinutes;
            return dust;
        }

        /// <summary>Collects every woken stone whose circle contains the point (passing through or camping), summed.</summary>
        public int CollectAllInRange(double x, double y, double nowMinutes)
        {
            int total = 0;
            foreach (var c in State.Circles())
                if (c.Contains(x, y)) total += Collect(c.NodeId, nowMinutes);
            return total;
        }

        /// <summary>The Manalith's size once <paramref name="wokenCount"/> stones are woken: each eases it by a step of the
        /// base size, never below <see cref="WaystoneDustConfig.MinSizeFactor"/> of it.</summary>
        public static float ManalithSize(float baseSize, int wokenCount, WaystoneDustConfig config = null)
        {
            var c = config ?? Default;
            float eased = baseSize * (1f - c.ManalithStepPerStone * Math.Max(0, wokenCount));
            return Math.Max(baseSize * c.MinSizeFactor, eased);
        }
    }
}
