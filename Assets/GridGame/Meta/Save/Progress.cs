using System.Collections.Generic;
using GridGame.Core.Battle;
using GridGame.Core.Rules;

namespace GridGame.Meta
{
    /// <summary>The bounty board (§22.4, GG-T431): marked targets taken, slain and awaiting their reward, and paid.</summary>
    public sealed class BountySave
    {
        /// <summary>Bounties taken and still roaming their stage.</summary>
        public List<string> Taken = new List<string>();
        /// <summary>Targets killed whose reward waits at the board.</summary>
        public List<string> Slain = new List<string>();
        /// <summary>Bounties paid out; they never post again.</summary>
        public HashSet<string> Claimed = new HashSet<string>();
        /// <summary>How many times the board's open offers have turned over (seeds them).</summary>
        public int Refresh;
        /// <summary>Stage battles fought since the last turnover.</summary>
        public int Battles;
        /// <summary>The board has been used (a bounty taken, or an old kill contract held), so its Camp row never locks again.</summary>
        public bool Used;
        /// <summary>Retired pre-GG-T431 kill contract fields: read from old saves, then cleared by the migration.</summary>
        public string ActiveId;
        public int Progress;
    }

    public sealed class QuestSave
    {
        /// <summary>Quest id → progress (Defeat kills so far; ClearStage 1 once met).</summary>
        public Dictionary<string, int> Active = new Dictionary<string, int>();
        public HashSet<string> Done = new HashSet<string>();
    }

    /// <summary>The save's Crucible record (§22.10). <see cref="Runs"/> also seeds the next run, so a save replays
    /// the same gauntlet until it fights it.</summary>
    public sealed class CrucibleSave
    {
        public int Best;
        public int Runs;
        public int TotalWaves;
        /// <summary>The most gold one run has banked (wave pay plus drops, GG-T430); a worse run never lowers it.</summary>
        public int BestGold;
        /// <summary>GG-T945 (§2.13.1, §22.10): the records of every mode other than <see cref="TurnMode.PartyWindow"/>,
        /// whose record stays <see cref="Best"/>/<see cref="BestGold"/> so an older save loads and reads unchanged. Null
        /// (and left out of the JSON) until a run in another mode ends; the JSON keys it by mode name.
        /// <see cref="Runs"/> and <see cref="TotalWaves"/> stay shared: Runs seeds the next run whatever its mode.</summary>
        public Dictionary<TurnMode, CrucibleRecord> ByMode;

        /// <summary>The best waves survived in <paramref name="mode"/>'s runs (0 before the first).</summary>
        public int BestFor(TurnMode mode) =>
            mode == TurnMode.PartyWindow ? Best : ByMode != null && ByMode.TryGetValue(mode, out var r) ? r.Best : 0;

        /// <summary>The richest haul of <paramref name="mode"/>'s runs (0 before the first).</summary>
        public int BestGoldFor(TurnMode mode) =>
            mode == TurnMode.PartyWindow ? BestGold : ByMode != null && ByMode.TryGetValue(mode, out var r) ? r.BestGold : 0;

        /// <summary>Records one finished run of <paramref name="mode"/>: counts it, and raises that mode's best waves and
        /// best haul (never lowers them). True when the waves are a new best for the mode.</summary>
        public bool Record(TurnMode mode, int waves, int gold)
        {
            Runs++;
            TotalWaves += waves;
            bool newBest = waves > BestFor(mode);
            if (mode == TurnMode.PartyWindow)
            {
                if (newBest) Best = waves;
                if (gold > BestGold) BestGold = gold;
                return newBest;
            }
            ByMode ??= new Dictionary<TurnMode, CrucibleRecord>();
            if (!ByMode.TryGetValue(mode, out var r)) ByMode[mode] = r = new CrucibleRecord();
            if (newBest) r.Best = waves;
            if (gold > r.BestGold) r.BestGold = gold;
            return newBest;
        }
    }

    /// <summary>One turn mode's Crucible record (GG-T945): its best waves survived and its richest haul.</summary>
    public sealed class CrucibleRecord
    {
        public int Best;
        public int BestGold;
    }
}
