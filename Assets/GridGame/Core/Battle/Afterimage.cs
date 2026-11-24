using System;
using System.Collections.Generic;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Core.Battle
{
    /// <summary>
    /// Afterimage (§1.2, §7, GG-T867, the Speedster's skill): a dash of up to <see cref="AbilityDef.Afterimage"/> tiles in a
    /// straight cardinal line to an empty tile, past anything in between, leaving an afterimage on the tile it left
    /// (<see cref="Unit.AfterimageAt"/>). For the rest of that action the afterimage is a pincer end of the Speedster's own:
    /// dash past a foe and it is caught between the Speedster and its afterimage (pincers from one hero), or the
    /// afterimage closes a line with another hero. Until the Speedster's next turn it draws foes' attacks (Taunting): a foe
    /// that can reach it goes for it, strikes it and it fades. Deterministic: no Rng.
    /// </summary>
    public static class Afterimage
    {
        /// <summary>Can <paramref name="caster"/> dash to <paramref name="to"/>: a one-tile unit, a straight cardinal line of 1..
        /// <paramref name="reach"/> tiles, onto a walkable tile with no unit or movable on it (what lies between doesn't matter).</summary>
        public static bool CanDash(BattleState s, Unit caster, Coord to, int reach)
        {
            if (s == null || caster == null || !caster.Alive || caster.IsMultiTile || reach <= 0) return false;
            var from = caster.Pos;
            if (from.X != to.X && from.Y != to.Y) return false;
            int d = from.Manhattan(to);
            return d >= 1 && d <= reach && s.FreeToEnter(to) && s.TrapAt(to) == null;
        }

        /// <summary>The tiles a dash from <paramref name="from"/> to <paramref name="to"/> crosses, both ends included.</summary>
        public static List<Coord> Line(Coord from, Coord to)
        {
            var line = new List<Coord> { from };
            var step = new Coord(Math.Sign(to.X - from.X), Math.Sign(to.Y - from.Y));
            if (step.X == 0 && step.Y == 0) return line;
            for (var c = from + step; ; c = c + step) { line.Add(c); if (c == to) break; }
            return line;
        }

        /// <summary>The pincers a dash to <paramref name="to"/> would close (on a copy of the board), for the bot and the
        /// preview: every pincer the Speedster or its afterimage is an end of.</summary>
        public static List<Pincer> Preview(BattleState s, Unit caster, Coord to)
        {
            var copy = s.Clone();
            var cu = copy.UnitById(caster.Id);
            cu.AfterimageAt = cu.Pos;
            cu.Pos = to;
            cu.AfterimagePincers = true;
            var mine = new List<Pincer>();
            foreach (var p in PincerDetector.Detect(copy, Team.Hero, cu))
                if ((p.A == cu || p.B == cu) && p.Opponents.Exists(o => o.Alive)) mine.Add(p);
            return mine;
        }

        /// <summary>HeroBot's worth of a dash (GG-T867): never superfluous - it is worth something only when the afterimage
        /// itself closes a pincer (10 a trapped foe, a bit more when the Speedster pincers alone), plus 8 a foe for the other
        /// pincers the landing makes; 0 otherwise.</summary>
        public static float Value(BattleState s, Unit caster, Coord to)
        {
            float through = 0f, other = 0f;
            foreach (var p in Preview(s, caster, to))
            {
                int foes = 0;
                foreach (var o in p.Opponents) if (o.Alive && !o.IsRemains) foes++;
                if (p.AAfterimage.HasValue || p.BAfterimage.HasValue) through += (p.A == p.B ? 12f : 10f) * foes;
                else other += 8f * foes;
            }
            return through <= 0f ? 0f : through + other - 2f;
        }

        /// <summary>The hero whose afterimage <paramref name="enemy"/> is drawn to (Taunting): a one-tile foe that can strike it
        /// this turn - within its reach for a ranged foe, within its steps plus one otherwise. Nearest first, then lower id.
        /// Null when no afterimage stands or none is in reach.</summary>
        public static Unit Lure(BattleState s, Unit enemy)
        {
            if (enemy == null || !enemy.Alive || enemy.Team != Team.Enemy || enemy.IsMultiTile || enemy.IsSnakeMember || enemy.IsRemains
                || enemy.IsEgg || enemy.IsImmobile || enemy.IsRooted && !enemy.IsRanged) return null;
            Unit best = null; int bestD = int.MaxValue;
            foreach (var h in s.Units)
            {
                if (!h.Alive || !h.IsHero || !(h.AfterimageAt is Coord at)) continue;
                int d = enemy.Pos.Manhattan(at);
                bool reach = enemy.IsRanged ? d <= enemy.Def.AttackRange : d - 1 <= Math.Max(0, enemy.Def.MoveRange) && !enemy.IsRooted;
                if (!reach) continue;
                if (d < bestD || (d == bestD && best != null && h.Id < best.Id)) { best = h; bestD = d; }
            }
            return best;
        }
    }
}
