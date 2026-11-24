using System;
using System.Collections.Generic;
using GridGame.Core.Content;

namespace GridGame.Core.Battle
{
    /// <summary>
    /// Magnetize (§7, GG-T878, the Tinkerer's spell): every plated or Tech-tagged foe within the ability's radius of the
    /// picked tile is dragged up to <see cref="AbilityDef.Magnetizes"/> tiles toward it, nearest first (so the inner foe
    /// makes room and the next one stacks up behind it), a free tile at a time, and its plate swings to face the tile. A
    /// Heavy foe resists the pull unless it floats, a Rooted one holds, a boss, snake or remains won't budge: each of those
    /// still turns its plate, which is the other half of the point - an armored line, plates all facing one tile, is open
    /// from the far side for one pincer. The runner moves the units (<c>BattleRunner.ResolveMagnetize</c>); this class says
    /// who is caught, where each steps and what a cast is worth to the hero bot. Deterministic, never draws from the Rng.
    /// </summary>
    public static class Magnetize
    {
        /// <summary>The tag that makes an unplated foe magnetic (gremlin gadgets, cybernetics).</summary>
        public const string TechTag = "Tech";

        /// <summary>A foe the magnet grips: on the board, a real fighter (no corpse, egg or remains), with an unbroken
        /// plate or a Tech tag.</summary>
        public static bool IsMagnetic(Unit u) =>
            u != null && u.Alive && u.OnBoard && !u.IsCorpse && !u.IsEgg && !u.IsRemains && !u.IsNeutral
            && (u.HasPlate || (u.Def != null && u.Def.HasTag(TechTag)));

        /// <summary>Is the pull held off <paramref name="u"/> (it still turns its plate)? Heavy (unless Floating), Rooted,
        /// a boss, snake or remains.</summary>
        public static bool Holds(Unit u) => u.WontBudge || u.IsRooted;

        /// <summary>The foes of <paramref name="caster"/> a cast at <paramref name="anchor"/> grips, in the order the pull
        /// takes them: nearest the tile first (footprint Manhattan), then by id.</summary>
        public static List<Unit> Caught(BattleState s, AbilityDef a, Unit caster, Coord anchor)
        {
            var list = new List<Unit>();
            if (a == null || a.Magnetizes <= 0 || caster == null) return list;
            foreach (var u in s.Units)
            {
                if (u.Team == caster.Team || !IsMagnetic(u)) continue;
                if (Distance(u, anchor) > Math.Max(0, a.Radius)) continue;
                list.Add(u);
            }
            list.Sort((x, y) => { int d = Distance(x, anchor).CompareTo(Distance(y, anchor)); return d != 0 ? d : x.Id.CompareTo(y.Id); });
            return list;
        }

        /// <summary>Footprint Manhattan distance from <paramref name="u"/> to <paramref name="c"/> (0 when it stands on it).</summary>
        public static int Distance(Unit u, Coord c)
        {
            int best = int.MaxValue;
            foreach (var t in u.Tiles()) best = Math.Min(best, t.Manhattan(c));
            return best;
        }

        /// <summary>One step from <paramref name="from"/> toward <paramref name="to"/>, the longer axis first (x on a tie);
        /// null when already there.</summary>
        public static Coord? StepToward(Coord from, Coord to)
        {
            if (from == to) return null;
            int dx = to.X - from.X, dy = to.Y - from.Y;
            return Math.Abs(dx) >= Math.Abs(dy) ? new Coord(Math.Sign(dx), 0) : new Coord(0, Math.Sign(dy));
        }

        /// <summary>The side <paramref name="u"/>'s plate swings to so it faces <paramref name="anchor"/> (its dominant axis,
        /// ties to the axis it already faces along); null when it stands on the tile itself (its plate stays put).</summary>
        public static Coord? FaceToward(Unit u, Coord anchor)
        {
            float cx = u.Pos.X + (u.W - 1) / 2f, cy = u.Pos.Y + (u.H - 1) / 2f;
            float dx = anchor.X - cx, dy = anchor.Y - cy;
            if (Math.Abs(dx) < 0.01f && Math.Abs(dy) < 0.01f) return null;
            if (Math.Abs(dx) > Math.Abs(dy) || (Math.Abs(dx) == Math.Abs(dy) && u.Facing.X != 0)) return new Coord(Math.Sign(dx), 0);
            return new Coord(0, Math.Sign(dy));
        }

        /// <summary>
        /// Moves and turns the caught foes as the cast would (a free tile at a time; a unit, a movable or a block stops it;
        /// pits and traps are not looked at) and returns what to put back with <see cref="Restore"/>.
        /// </summary>
        public static List<(Unit u, Coord pos, Coord facing)> Simulate(BattleState s, AbilityDef a, Unit caster, Coord anchor)
        {
            var undo = new List<(Unit, Coord, Coord)>();
            foreach (var u in Caught(s, a, caster, anchor))
            {
                undo.Add((u, u.Pos, u.Facing));
                if (!Holds(u))
                    for (int i = 0; i < a.Magnetizes; i++)
                    {
                        var dir = StepToward(u.Pos, anchor);
                        if (dir == null) break;
                        var next = u.Pos + dir.Value;
                        if (!s.FreeToEnter(next)) break;
                        u.Pos = next;
                    }
                if (u.PlateMax > 0) { var f = FaceToward(u, anchor); if (f != null) u.Facing = f.Value; }
            }
            return undo;
        }

        public static void Restore(List<(Unit u, Coord pos, Coord facing)> undo)
        {
            for (int i = undo.Count - 1; i >= 0; i--) { undo[i].u.Pos = undo[i].pos; undo[i].u.Facing = undo[i].facing; }
        }

        /// <summary>
        /// The most magnetic foes (of <paramref name="ids"/>) standing in one unbroken row or column of foes: the line one
        /// pincer would trap. Below 2 there is no line.
        /// </summary>
        public static int LongestLine(BattleState s, Team foes, HashSet<int> ids)
        {
            int best = 0;
            for (int axis = 0; axis < 2; axis++)
            {
                int outer = axis == 0 ? BattleConfig.Rows : BattleConfig.Cols, inner = axis == 0 ? BattleConfig.Cols : BattleConfig.Rows;
                for (int o = 0; o < outer; o++)
                {
                    int run = 0;
                    for (int i = 0; i < inner; i++)
                    {
                        var u = s.UnitAt(axis == 0 ? new Coord(i, o) : new Coord(o, i));
                        if (u == null || !u.Alive || u.Team != foes || u.IsCorpse) { run = 0; continue; }
                        if (ids.Contains(u.Id)) { run++; best = Math.Max(best, run); }
                    }
                }
            }
            return best;
        }

        /// <summary>
        /// What HeroBot pays for a cast at <paramref name="anchor"/> (GG-T878): only a pull that lines two or more gripped
        /// foes up in one row or column, longer than any such line before, is worth a turn - more for a longer line and
        /// for each plate it turns. Anything else (a lone foe, a line already standing) is worth nothing.
        /// </summary>
        public static float Value(BattleState s, Unit caster, AbilityDef a, Coord anchor)
        {
            var caught = Caught(s, a, caster, anchor);
            if (caught.Count < 2) return 0f;
            var ids = new HashSet<int>();
            foreach (var u in caught) ids.Add(u.Id);
            var foes = caught[0].Team;
            int before = LongestLine(s, foes, ids);
            int turned = 0;
            var undo = Simulate(s, a, caster, anchor);
            int after;
            try
            {
                after = LongestLine(s, foes, ids);
                foreach (var (u, _, facing) in undo) if (u.HasPlate && u.Facing != facing) turned++;
            }
            finally { Restore(undo); }
            if (after < 2 || after <= before) return 0f;
            return 8f + 6f * after + 1.5f * turned;
        }
    }
}
