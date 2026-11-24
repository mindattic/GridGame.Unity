using System;
using System.Collections.Generic;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Core.Battle
{
    /// <summary>
    /// Gravity Well (§7, GG-T883, the Dark Templar's spell): a <see cref="RuleEffectKind.MoveUnits"/> rule effect that pulls
    /// every foe within its radius of the picked tile one tile toward it, nearest first (so an inner foe makes room for
    /// the one behind it). A Heavy foe resists unless it floats (<see cref="Unit.WontBudge"/>), a Rooted one holds. It
    /// clumps a scattered wave into pincer lines and area shapes. The runner moves the units; this class says what a cast
    /// would do for the hero bot, which only casts it when it raises the trapped count of the party's next pincer.
    /// Pure and deterministic: it moves units' <c>Pos</c> to look and restores them, never draws from the Rng.
    /// </summary>
    public static class GravityWell
    {
        /// <summary>How far a hero's next drag is looked at when counting what its pincer would trap.</summary>
        public const int DragReach = 3;
        /// <summary>What each extra foe the party's next pincer would trap is worth to the bot.</summary>
        public const float TrappedValue = 12f;

        /// <summary>The ability's pull toward its picked tile, or null when it has none.</summary>
        public static RuleEffectDef Pull(AbilityDef a)
        {
            if (a?.RuleEffects == null) return null;
            foreach (var e in a.RuleEffects)
                if (e.Kind == RuleEffectKind.MoveUnits && e.Move == RuleMove.Toward && e.Area == RuleArea.Target && e.Units == RuleUnits.OnArea && e.Amount > 0)
                    return e;
            return null;
        }

        public static bool Is(AbilityDef a) => Pull(a) != null;

        /// <summary>One step toward <paramref name="point"/> along the longer axis (x on a tie), as the runner steps a rule
        /// move; null when already there.</summary>
        public static Coord? StepToward(Coord from, Coord point)
        {
            if (point == from) return null;
            int dx = point.X - from.X, dy = point.Y - from.Y;
            return Math.Abs(dx) >= Math.Abs(dy) ? new Coord(Math.Sign(dx), 0) : new Coord(0, Math.Sign(dy));
        }

        /// <summary>The units a pull toward <paramref name="centre"/> takes, in the order it moves them: nearest the centre
        /// first, then by id.</summary>
        public static List<Unit> Caught(BattleState s, AbilityDef a, RuleEffectDef e, Coord centre)
        {
            var list = new List<Unit>();
            foreach (var u in s.Units)
            {
                if (!u.Alive || !u.OnBoard || u.IsCorpse || u.IsEgg || u.WontBudge || u.IsRooted) continue;
                if (e.Team == RuleTeam.Hero && u.Team != Team.Hero) continue;
                if (e.Team == RuleTeam.Enemy && u.Team != Team.Enemy) continue;
                if (u.Pos.Manhattan(centre) > Math.Max(0, e.Radius)) continue;
                if (RegionBoard.Immune(u, a?.Id)) continue;
                list.Add(u);
            }
            list.Sort((x, y) => { int d = x.Pos.Manhattan(centre).CompareTo(y.Pos.Manhattan(centre)); return d != 0 ? d : x.Id.CompareTo(y.Id); });
            return list;
        }

        /// <summary>
        /// Moves the caught units as the cast would (a free tile at a time; a unit, a movable or a block stops it) and
        /// returns each moved unit's old tile so <see cref="Restore"/> can put it back. Pits and traps are not looked at.
        /// </summary>
        public static List<(Unit u, Coord from)> Simulate(BattleState s, AbilityDef a, Coord centre)
        {
            var moved = new List<(Unit, Coord)>();
            var e = Pull(a);
            if (e == null) return moved;
            foreach (var u in Caught(s, a, e, centre))
            {
                var from = u.Pos;
                for (int step = 0; step < e.Amount; step++)
                {
                    var dir = StepToward(u.Pos, centre);
                    if (dir == null) break;
                    var next = u.Pos + dir.Value;
                    if (!s.FreeToEnter(next)) break;
                    u.Pos = next;
                }
                if (u.Pos != from) moved.Add((u, from));
            }
            return moved;
        }

        public static void Restore(List<(Unit u, Coord from)> moved)
        {
            for (int i = moved.Count - 1; i >= 0; i--) moved[i].u.Pos = moved[i].from;
        }

        /// <summary>
        /// The most foes the party's next pincer could trap: for each hero that can pincer, every free tile within
        /// <see cref="DragReach"/> of it (its own included) is tried, and the live, unarmored foes caught in the pincers it
        /// would close from there are counted.
        /// </summary>
        public static int NextPincerTrapped(BattleState s)
        {
            int best = 0;
            var caught = new HashSet<Unit>();
            foreach (var h in s.LivingList(Team.Hero))
            {
                if (h.IsCorpse || !h.CanPincer || h.WontBudge || h.IsRooted) continue;
                var origin = h.Pos;
                try
                {
                    for (int y = 0; y < BattleConfig.Rows; y++)
                        for (int x = 0; x < BattleConfig.Cols; x++)
                        {
                            var c = new Coord(x, y);
                            if (c.Manhattan(origin) > DragReach) continue;
                            if (c != origin && (!s.Walkable(c) || s.UnitAt(c) != null)) continue;
                            h.Pos = c;
                            caught.Clear();
                            foreach (var p in PincerDetector.Detect(s, Team.Hero, h))
                            {
                                if (p.A != h && p.B != h) continue;
                                foreach (var o in p.Opponents)
                                    if (o.Alive && !o.IsCorpse && !BattleEngine.IsArmored(s, o)) caught.Add(o);
                            }
                            if (caught.Count > best) best = caught.Count;
                        }
                }
                finally { h.Pos = origin; }
            }
            return best;
        }

        /// <summary>
        /// What casting the well on <paramref name="centre"/> is worth to the hero bot: <see cref="TrappedValue"/> for each
        /// foe it adds to the next pincer's catch, less as much for each hero it leaves trapped in a foe pincer. Nothing
        /// when it isn't a gravity well, moves nobody or adds nothing to the catch.
        /// </summary>
        public static float Value(BattleState s, AbilityDef a, Coord centre)
        {
            if (!Is(a)) return 0f;
            int before = NextPincerTrapped(s);
            int trappedBefore = PincerPlanner.TrappedAllies(s, Team.Hero, out _, null);
            var moved = Simulate(s, a, centre);
            if (moved.Count == 0) return 0f;
            int after, trappedAfter;
            try
            {
                after = NextPincerTrapped(s);
                trappedAfter = PincerPlanner.TrappedAllies(s, Team.Hero, out _, null);
            }
            finally { Restore(moved); }
            if (after <= before) return 0f;
            return TrappedValue * (after - before) - TrappedValue * Math.Max(0, trappedAfter - trappedBefore);
        }
    }
}
