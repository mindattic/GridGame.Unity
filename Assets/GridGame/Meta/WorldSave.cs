using System;
using System.Collections.Generic;

namespace GridGame.Meta
{
    /// <summary>GG-T1778: the overworld part of a save (BIBLE §28.13). Plain data; <see cref="WorldSaveAdapter"/> turns it into
    /// and out of the Core.World types. Save v26: an older save is placed camped at its furthest cleared stage's node.</summary>
    public sealed class WorldSave
    {
        /// <summary>The region the caravan is in; null is not on the overworld yet.</summary>
        public string RegionId;
        /// <summary>The node the caravan stands at, or set off from when <see cref="Route"/> is non-empty.</summary>
        public string NodeId;
        /// <summary>The route being travelled, as node ids from start to destination; empty when standing at a node.</summary>
        public List<string> Route = new List<string>();
        /// <summary>How far along <see cref="Route"/> the caravan has come, in art pixels.</summary>
        public double Distance;
        /// <summary>At camp, off the road (<see cref="TravelRules.OnTheRoad"/>). GG-T1790: a new game starts at camp.</summary>
        public bool Camped = true;
        /// <summary>GG-T1785, GG-T1927: the party mana pool's points after the last battle won on the road (§3.1.0); they start
        /// the next battle while the caravan stays on the road (<see cref="TravelRules.StartMana"/>). 0 at camp.</summary>
        public int CarriedMana;
        /// <summary>Legacy (save v28 and older): carried whole orbs. Save v29 turns them into <see cref="CarriedMana"/> at
        /// <see cref="GridGame.Core.Battle.BattleConfig.ManaPointsPerOrb"/> points each and clears this, so it is never written.</summary>
        public int? CarriedOrbs;
        /// <summary><see cref="GridGame.Core.World.WorldClock.Minutes"/>.</summary>
        public long ClockMinutes;
        public DangerSave Danger = new DangerSave();
        /// <summary>The camp night (<see cref="GridGame.Core.World.CampNight"/>).</summary>
        public int Food = 10;
        public bool AteMeal;
        public long SleptFrom = -1;
        public long WellRestedUntil;
        /// <summary>The pitched camp; null when none is pitched.</summary>
        public CampSave Camp;
        /// <summary>Per-region progress, keyed by region id.</summary>
        public Dictionary<string, RegionSave> Regions = new Dictionary<string, RegionSave>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Hunts taken per region, keyed by region id.</summary>
        public Dictionary<string, int> HuntCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Seeds the Wild events.</summary>
        public int WildSeed;

        /// <summary>Fills in any collection a hand-edited or partial save left null.</summary>
        public void FillNulls()
        {
            Route ??= new List<string>();
            Danger ??= new DangerSave();
            Regions ??= new Dictionary<string, RegionSave>(StringComparer.OrdinalIgnoreCase);
            HuntCounts ??= new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in Regions.Values)
            {
                if (r == null) continue;
                r.Woken ??= new List<string>();
                r.GuardiansCleared ??= new List<string>();
                r.DustSince ??= new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                r.RevealedEdges ??= new List<string>();
            }
        }
    }

    /// <summary><see cref="GridGame.Core.World.DangerState"/>.</summary>
    public sealed class DangerSave
    {
        public double Level;
        public int Seed;
        public double Travelled;
        public int Epoch;
    }

    /// <summary>A pitched camp: <see cref="GridGame.Core.World.CampSite.Pitch"/> at (X, Y) with this seed and radius gives it back.</summary>
    public sealed class CampSave
    {
        public double X, Y, Radius;
        public int Seed;
    }

    /// <summary>One region's progress: its fog of war, waystones and revealed roads.</summary>
    public sealed class RegionSave
    {
        /// <summary><see cref="GridGame.Core.World.FogMask.ToBase64"/>, with the size and cell it was made at.</summary>
        public string FogBase64;
        public int FogW, FogH, FogCell;
        public List<string> Woken = new List<string>();
        public List<string> GuardiansCleared = new List<string>();
        /// <summary>World minute each woken waystone's Dust clock started (<see cref="GridGame.Core.World.WaystoneDust.SinceMinutes"/>).</summary>
        public Dictionary<string, double> DustSince = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        /// <summary>Edges drawn on the map, as "A-B" keys.</summary>
        public List<string> RevealedEdges = new List<string>();
    }
}
