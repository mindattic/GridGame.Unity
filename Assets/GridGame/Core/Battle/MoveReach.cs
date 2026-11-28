using System;
using System.Collections.Generic;

namespace GridGame.Core.Battle
{
    /// <summary>An empty tile a hero can walk to this window whose drop traps live foes in a pincer (GG-T1945).</summary>
    public sealed class PincerSpot
    {
        public Coord Tile;
        /// <summary>The route there, as <see cref="MoveReach.Route"/> gives it (the hero's own tile not included).</summary>
        public List<Coord> Path;
        /// <summary>Every live, non-corpse foe the drop's pincers catch, in pincer order, each once.</summary>
        public List<int> FoeIds = new List<int>();
    }

    /// <summary>
    /// Where a hero can walk this window (§1.1): a breadth-first search over cardinal steps, each route kept only when the
    /// move rules (<see cref="BattleEngine.PreviewMove"/>) let the hero walk it in full, within a step budget. Neighbours are
    /// tried in a fixed order (N, E, S, W), so the same board always gives the same route. Pure: it previews on copies.
    /// Tap-to-move picks its route here (GG-T1977), and the pincer spots (GG-T1945) are the empty tiles it reaches whose drop
    /// closes a pincer (§1.2).
    /// </summary>
    public static class MoveReach
    {
        /// <summary>The shortest route from the hero to <paramref name="dest"/> within <paramref name="maxSteps"/>; null when
        /// there is none.</summary>
        public static List<Coord> Route(BattleState s, int heroId, Coord dest, int maxSteps)
        {
            List<Coord> found = null;
            Walk(s, heroId, maxSteps, (tile, path, _) =>
            {
                if (tile != dest) return false;
                found = path;
                return true;
            });
            return found;
        }

        /// <summary>Every empty tile within <paramref name="maxSteps"/> whose drop traps at least one live foe, in search order.</summary>
        public static List<PincerSpot> PincerSpots(BattleState s, int heroId, int maxSteps)
        {
            var spots = new List<PincerSpot>();
            Walk(s, heroId, maxSteps, (tile, path, pv) =>
            {
                if (s.UnitAt(tile) != null) return false;
                PincerSpot spot = null;
                foreach (var p in pv.Pincers)
                    foreach (var o in p.Opponents)
                    {
                        if (!o.Alive || o.IsCorpse) continue;
                        spot ??= new PincerSpot { Tile = tile, Path = path };
                        if (!spot.FoeIds.Contains(o.Id)) spot.FoeIds.Add(o.Id);
                    }
                if (spot != null) spots.Add(spot);
                return false;
            });
            return spots;
        }

        /// <summary>Visits each tile the hero can walk to, once, by its shortest route and that route's preview, until
        /// <paramref name="visit"/> returns true.</summary>
        private static void Walk(BattleState s, int heroId, int maxSteps, Func<Coord, List<Coord>, MovePreview, bool> visit)
        {
            var hero = s.UnitById(heroId);
            if (hero == null || maxSteps <= 0) return;
            var seen = new HashSet<Coord> { hero.Pos };
            var frontier = new Queue<List<Coord>>();
            frontier.Enqueue(new List<Coord>());
            while (frontier.Count > 0)
            {
                var path = frontier.Dequeue();
                if (path.Count >= maxSteps) continue;
                var at = path.Count > 0 ? path[path.Count - 1] : hero.Pos;
                foreach (var step in new[] { new Coord(at.X, at.Y - 1), new Coord(at.X + 1, at.Y), new Coord(at.X, at.Y + 1), new Coord(at.X - 1, at.Y) })
                {
                    if (step.X < 0 || step.Y < 0 || step.X >= BattleConfig.Cols || step.Y >= BattleConfig.Rows || !seen.Add(step)) continue;
                    var next = new List<Coord>(path) { step };
                    var pv = BattleEngine.PreviewMove(s, heroId, next);
                    if (pv.StepsTaken.Count < next.Count) continue;
                    if (visit(step, next, pv)) return;
                    frontier.Enqueue(next);
                }
            }
        }
    }
}
