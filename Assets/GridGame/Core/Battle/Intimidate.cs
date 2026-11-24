using System;
using System.Collections.Generic;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Core.Battle
{
    /// <summary>
    /// Intimidate (§7, GG-T884, the Blue Lion's skill): a roar that lays [Frightened] (<see cref="BuffDef.Frightens"/>) on
    /// every adjacent foe with the Lion as its fear source, so on its next turn each one backs away from the Lion
    /// (<c>BattleRunner.FleeTurn</c>) - into a firewall, onto the party's mine, out of a foe pincer round a hero or into a
    /// hero pincer line. The runner needs nothing new (a Cross r1 / Auto / EnemyOnly ability that applies the buff); this
    /// class predicts the flight for the hero bot and the view. Pure and deterministic: it moves units' <c>Pos</c> to look
    /// and restores them, never draws from the Rng.
    /// </summary>
    public static class Intimidate
    {
        /// <summary>What each hero freed from a foe pincer by a foe backing out of it is worth to the bot.</summary>
        public const float FreedValue = 10f;
        /// <summary>A frightened foe makes no attack on its next turn: worth this when it stands where it could hit a hero.</summary>
        public const float DeniedBlowValue = 2f;

        /// <summary>Does <paramref name="a"/> lay a Frightens debuff (a roar, not a buff for the caster)?</summary>
        public static bool Is(ContentDb c, AbilityDef a)
        {
            if (c == null || a == null || a.ApplyBuff == null || a.Filter != TargetFilter.EnemyOnly) return false;
            var b = c.TryBuff(a.ApplyBuff);
            return b != null && b.Frightens;
        }

        /// <summary>Would a Frightens debuff take on <paramref name="u"/> and make it flee? Not on a boss (Boss tag, rule
        /// break, boss phases) or a Heavy unit (both "Immune!"), nor a buff-immune, rooted or snake foe.</summary>
        public static bool WouldFlee(Unit u, BuffDef fear)
        {
            if (u == null || !u.Alive || !u.OnBoard || u.IsCorpse || u.IsEgg || u.IsRemains || u.Team != Team.Enemy) return false;
            if (u.Def.HasTag("Boss") || u.RuleBreak != RuleBreak.None || (u.Def.BossPhases != null && u.Def.BossPhases.Count > 0)) return false;
            if (u.IsHeavy || u.IsRooted || u.IsSnakeMember) return false;
            if (fear != null && u.Def.ImmuneBuffs != null && u.Def.ImmuneBuffs.Contains(fear.Id)) return false;
            return true;
        }

        /// <summary>
        /// The tiles a 1x1 <paramref name="foe"/> would step through fleeing <paramref name="src"/> on its next turn, in
        /// order, as <c>BattleRunner.FleeTurn</c> steps it: up to its MoveRange, the larger axis first, only steps that widen
        /// the gap, into a walkable tile no unit holds (a movable is shoved aside). An open hole ends the flight (it falls).
        /// Empty for a multi-tile foe (the bot doesn't guess a body's flight).
        /// </summary>
        public static List<Coord> FleePath(BattleState s, Unit foe, Unit src)
        {
            var path = new List<Coord>();
            if (s == null || foe == null || src == null || foe.IsMultiTile) return path;
            var at = foe.Pos;
            for (int i = 0; i < Math.Max(0, foe.Def.MoveRange); i++)
            {
                int dist = Gap(at, src);
                int dx = at.X - src.Pos.X, dy = at.Y - src.Pos.Y;
                var xStep = new Coord(dx >= 0 ? 1 : -1, 0);
                var yStep = new Coord(0, dy >= 0 ? 1 : -1);
                var first = Math.Abs(dx) >= Math.Abs(dy) ? xStep : yStep;
                var second = first == xStep ? yStep : xStep;
                Coord? next = null;
                if (CanStep(s, foe, src, at + first, dist)) next = at + first;
                else if (CanStep(s, foe, src, at + second, dist)) next = at + second;
                if (next == null) break;
                at = next.Value;
                path.Add(at);
                if (RegionBoard.IsPitFor(s, at, foe)) break;
            }
            return path;
        }

        private static int Gap(Coord c, Unit src)
        {
            int d = int.MaxValue;
            foreach (var t in src.Tiles()) d = Math.Min(d, c.Manhattan(t));
            return d;
        }

        private static bool CanStep(BattleState s, Unit foe, Unit src, Coord to, int dist)
        {
            if (!BattleState.InBounds(to) || !s.Walkable(to)) return false;
            var occ = s.UnitAt(to);
            if (occ != null && occ != foe) return false;
            return Gap(to, src) > dist;
        }

        /// <summary>The foes a roar from <paramref name="caster"/> would frighten, in the order the bot plays their flight
        /// (by id).</summary>
        public static List<Unit> Scared(BattleState s, AbilityDef a, Unit caster, Coord anchor)
        {
            var list = new List<Unit>();
            var fear = s?.Content?.TryBuff(a?.ApplyBuff);
            if (fear == null || !fear.Frightens) return list;
            foreach (var t in TargetResolver.Collect(s, a, caster, anchor))
                if (t != caster && WouldFlee(t, fear)) list.Add(t);
            list.Sort((x, y) => x.Id.CompareTo(y.Id));
            return list;
        }

        /// <summary>
        /// What the roar is worth to the hero bot, scored on the tiles the frightened foes would reach: per foe, the danger
        /// of each tile it backs through (a firewall, the party's mine, a telegraphed strike; a hole it falls into counts
        /// most) and a blow it no longer gets on a hero; then, with every foe moved to the end of its flight, each hero it
        /// frees from a foe pincer and each extra foe the party's next pincer would trap (it backed into a pincer line).
        /// Less as much for a hero the moves leave trapped. 0 when it frightens nobody or gains nothing.
        /// </summary>
        public static float Value(BattleState s, AbilityDef a, Unit caster, Coord anchor)
        {
            if (s == null || caster == null || !Is(s.Content, a)) return 0f;
            var scared = Scared(s, a, caster, anchor);
            if (scared.Count == 0) return 0f;
            float v = 0f;
            int trappedBefore = PincerPlanner.TrappedAllies(s, Team.Hero, out _, null);
            int nextBefore = GravityWell.NextPincerTrapped(s);
            var moved = new List<(Unit u, Coord from)>();
            int trappedAfter, nextAfter;
            try
            {
                foreach (var f in scared)
                {
                    if (Threatens(s, f)) v += DeniedBlowValue;
                    var path = FleePath(s, f, caster);
                    if (path.Count == 0) continue;
                    foreach (var c in path)
                    {
                        if (RegionBoard.IsPitFor(s, c, f)) { v += 12f; continue; }
                        v += 6f * Math.Min(2f, RegionBoard.DangerAt(s, c, f, Team.Hero));
                    }
                    moved.Add((f, f.Pos));
                    f.Pos = path[path.Count - 1];
                }
                if (moved.Count == 0) return v;
                trappedAfter = PincerPlanner.TrappedAllies(s, Team.Hero, out _, null);
                nextAfter = GravityWell.NextPincerTrapped(s);
            }
            finally
            {
                for (int i = moved.Count - 1; i >= 0; i--) moved[i].u.Pos = moved[i].from;
            }
            v += FreedValue * (trappedBefore - trappedAfter); // a hero left trapped reads as a loss
            v += 0.5f * GravityWell.TrappedValue * Math.Max(0, nextAfter - nextBefore);
            return Math.Max(0f, v);
        }

        /// <summary>Could <paramref name="f"/> hit a hero from where it stands on its next turn (one in reach now)?</summary>
        private static bool Threatens(BattleState s, Unit f)
        {
            foreach (var h in s.LivingList(Team.Hero))
                if (f.IsAdjacentTo(h) || f.DistanceTo(h) <= Math.Max(1, f.Def.AttackRange)) return true;
            return false;
        }
    }
}
