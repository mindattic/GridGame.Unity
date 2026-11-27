using System;
using System.Collections.Generic;
using GridGame.Core.Content;
using GridGame.Core.World;

namespace GridGame.Meta
{
    /// <summary>GG-T1778: <see cref="WorldSave"/> to and from the Core.World types (BIBLE §28.13).</summary>
    public static class WorldSaveAdapter
    {
        public static WorldClock Clock(WorldSave w, WorldTimeConfig config = null)
        {
            var clock = new WorldClock(config);
            if (w.ClockMinutes > 0) clock.Advance(w.ClockMinutes);
            return clock;
        }

        public static void Store(WorldSave w, WorldClock clock) => w.ClockMinutes = clock.Minutes;

        public static DangerState Danger(WorldSave w)
        {
            var d = w.Danger ?? new DangerSave();
            return new DangerState(d.Seed) { Level = d.Level, Travelled = d.Travelled, Epoch = d.Epoch };
        }

        public static void Store(WorldSave w, DangerState danger) =>
            w.Danger = new DangerSave { Level = danger.Level, Seed = danger.Seed, Travelled = danger.Travelled, Epoch = danger.Epoch };

        public static CampNight Night(WorldSave w) =>
            new CampNight { Food = w.Food, AteMeal = w.AteMeal, SleptFrom = w.SleptFrom, WellRestedUntil = w.WellRestedUntil };

        public static void Store(WorldSave w, CampNight night)
        {
            w.Food = night.Food;
            w.AteMeal = night.AteMeal;
            w.SleptFrom = night.SleptFrom;
            w.WellRestedUntil = night.WellRestedUntil;
        }

        /// <summary>The camp pitched again from its saved place and seed; null when none was pitched.</summary>
        public static CampSite Camp(WorldSave w, Func<double, double, bool> isWalkable, CampConfig config = null)
        {
            if (w.Camp == null) return null;
            config = config ?? new CampConfig();
            if (w.Camp.Radius > 0) config.CircleRadius = w.Camp.Radius;
            return CampSite.Pitch(w.Camp.X, w.Camp.Y, w.Camp.Seed, isWalkable, config);
        }

        /// <summary>The region's saved progress, made empty the first time it is asked for.</summary>
        public static RegionSave Region(WorldSave w, string regionId)
        {
            if (!w.Regions.TryGetValue(regionId, out var r) || r == null) w.Regions[regionId] = r = new RegionSave();
            return r;
        }

        /// <summary>The fog read back at the size it was saved at; a region never saved starts fully fogged.</summary>
        public static FogMask Fog(RegionSave r, RegionDef region)
        {
            if (r.FogW <= 0 || r.FogH <= 0) return new FogMask(region);
            return FogMask.FromBase64(r.FogW, r.FogH, r.FogBase64, r.FogCell > 0 ? r.FogCell : FogMask.DefaultCellSize);
        }

        public static void Store(RegionSave r, FogMask fog)
        {
            r.FogBase64 = fog.ToBase64();
            r.FogW = fog.Width;
            r.FogH = fog.Height;
            r.FogCell = fog.CellSize;
        }

        public static WaystoneState Waystones(RegionSave r, RegionDef region)
        {
            var state = new WaystoneState(region);
            foreach (var id in r.Woken) state.Woken.Add(id);
            foreach (var id in r.GuardiansCleared) state.GuardianCleared.Add(id);
            return state;
        }

        public static void Store(RegionSave r, WaystoneState state)
        {
            r.Woken = new List<string>(state.Woken);
            r.GuardiansCleared = new List<string>(state.GuardianCleared);
        }

        public static WaystoneDust Dust(RegionSave r, WaystoneState state, WaystoneDustConfig config = null)
        {
            var dust = new WaystoneDust(state, config);
            foreach (var kv in r.DustSince) dust.SinceMinutes[kv.Key] = kv.Value;
            return dust;
        }

        public static void Store(RegionSave r, WaystoneDust dust) =>
            r.DustSince = new Dictionary<string, double>(dust.SinceMinutes, StringComparer.OrdinalIgnoreCase);

        /// <summary>The trip in progress, rebuilt by pathfinding over only the roads joining the saved route's nodes, with the
        /// caravan placed <see cref="WorldSave.Distance"/> along it. Null when not travelling or the route no longer exists.</summary>
        public static TravelState Travel(WorldSave w, RegionDef region)
        {
            if (w.Route == null || w.Route.Count < 2 || region == null) return null;
            var joins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < w.Route.Count; i++)
            {
                joins.Add(w.Route[i - 1] + "\n" + w.Route[i]);
                joins.Add(w.Route[i] + "\n" + w.Route[i - 1]);
            }
            var route = new WorldGraph(region, e => joins.Contains(e.A + "\n" + e.B)).Find(w.Route[0], w.Route[w.Route.Count - 1]);
            if (route == null) return null;
            var state = new TravelState(route);
            if (state.LegCount == 0) return state;
            double left = Math.Max(0, w.Distance);
            int last = state.LegCount - 1;
            if (left >= state.TotalLength)
            {
                state.LegIndex = last;
                state.DistanceIntoLeg = state.LengthOfLeg(last);
                state.Arrived = true;
                return state;
            }
            int leg = 0;
            while (leg < last && left >= state.LengthOfLeg(leg)) left -= state.LengthOfLeg(leg++);
            state.LegIndex = leg;
            state.DistanceIntoLeg = left;
            return state;
        }

        public static void Store(WorldSave w, TravelState travel)
        {
            if (travel == null || travel.Route.Nodes == null || travel.Route.Nodes.Length < 2)
            {
                w.Route = new List<string>();
                w.Distance = 0;
                return;
            }
            w.Route = new List<string>(travel.Route.Nodes);
            w.Distance = travel.DistanceTravelled;
        }

        /// <summary>Save v26: an older save starts camped at the node of its furthest cleared stage in campaign order; with
        /// none cleared, or no world.json node for that stage, it is not on the overworld yet (RegionId null).</summary>
        public static void PlaceAtFrontier(SaveData s, ContentDb c)
        {
            var w = s.World;
            w.RegionId = null;
            w.NodeId = null;
            var stages = c.CampaignStages();
            if (s.HighestClearedStage < 0 || s.HighestClearedStage >= stages.Count || c.World?.Regions == null) return;
            string stageId = stages[s.HighestClearedStage].Id;
            foreach (var region in c.World.Regions)
            {
                var node = region?.Nodes.Find(n => n != null && n.Kind == NodeKind.Stage && string.Equals(n.StageId, stageId, StringComparison.OrdinalIgnoreCase));
                if (node == null) continue;
                w.RegionId = region.Id;
                w.NodeId = node.Id;
                w.Camped = true;
                w.Route = new List<string>();
                w.Distance = 0;
                return;
            }
        }
    }
}
