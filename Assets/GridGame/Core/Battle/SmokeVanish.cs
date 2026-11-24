using System;
using System.Collections.Generic;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Core.Battle
{
    /// <summary>
    /// Smoke Vanish (§7, GG-T861, the Black Ninja's skill; the owner's "ninja drops a smoke bomb and reappears on a random
    /// square"): the ability's rule effects lay a smoke cloud (the <c>smoke</c> state, <see cref="TileStateDef.Obscures"/>)
    /// on the ninja's tile and its 4 neighbours, so blows aimed into it roll as if Blind; then the ninja vanishes and
    /// reappears on a random <see cref="Landings"/> tile, drawn from the battle Rng (one draw), and a pincer it lands in
    /// resolves at once, as Teleport's does. Every foe's charge locked on it re-plans onto the nearest other hero
    /// (<see cref="Replan"/>), or, with none left, falls on the smoke it left. A Rooted ninja drops the smoke and stays.
    /// Distinct from the Smoke Bomb item (an escape that ends the battle) and Smoke Veil (a thrown cloud that Blinds the foes
    /// it catches). The runner moves the unit (<c>BattleRunner.ResolveVanish</c>); this class picks the tiles and prices a
    /// cast for the hero bot.
    /// </summary>
    public static class SmokeVanish
    {
        /// <summary>Closest a landing may be to the tile left (Manhattan): out of its own cloud.</summary>
        public const int MinHop = 2;

        /// <summary>The tiles <paramref name="u"/> can reappear on, row-major: its whole footprint on walkable tiles nobody else
        /// stands on, no movable, no trap, no danger it would read (a hazard, a telegraphed landing; <see cref="RegionBoard.DangerAt"/>),
        /// at least <see cref="MinHop"/> from <paramref name="from"/>.</summary>
        public static List<Coord> Landings(BattleState s, Unit u, Coord from)
        {
            var list = new List<Coord>();
            if (s == null || u == null) return list;
            for (int y = 0; y < BattleConfig.Rows; y++)
                for (int x = 0; x < BattleConfig.Cols; x++)
                {
                    var c = new Coord(x, y);
                    if (c.Manhattan(from) < MinHop || !Open(s, u, c)) continue;
                    list.Add(c);
                }
            return list;
        }

        private static bool Open(BattleState s, Unit u, Coord anchor)
        {
            for (int dy = 0; dy < u.H; dy++)
                for (int dx = 0; dx < u.W; dx++)
                {
                    var t = new Coord(anchor.X + dx, anchor.Y + dy);
                    if (!s.Walkable(t) || s.MovableAt(t) != null || s.TrapAt(t) != null) return false;
                    foreach (var o in s.Units)
                        if (o != u && o.Alive && o.Occupies(t)) return false;
                    if (RegionBoard.DangerAt(s, t, u) > 0f) return false;
                }
            return true;
        }

        /// <summary>The hero a foe's charge locked on <paramref name="gone"/> turns to: the living, on-board hero nearest the
        /// charging foe (ties: lowest id), never <paramref name="gone"/>; null when there is none.</summary>
        public static Unit Replan(BattleState s, TimelineEntry charge, Unit gone)
        {
            var owner = s.UnitById(charge.OwnerId);
            Coord at = owner != null ? owner.Pos : charge.TargetTile;
            Unit best = null; int bestD = int.MaxValue;
            foreach (var h in s.Units)
            {
                if (h == gone || !h.Alive || !h.OnBoard || !h.IsHero || h.IsCorpse) continue;
                int d = h.Pos.Manhattan(at);
                if (d < bestD || (d == bestD && best != null && h.Id < best.Id)) { bestD = d; best = h; }
            }
            return best;
        }

        /// <summary>Is <paramref name="e"/> a foe's charge aimed at <paramref name="u"/> itself (not a line, which is aimed at tiles)?</summary>
        public static bool LockedOn(TimelineEntry e, Unit u) =>
            e.Kind == TimelineKind.Cast && e.IsCharge && e.CasterTeam != u.Team && e.TargetUnitId == u.Id && e.ChargeShape != ChargeShape.Line;

        /// <summary>
        /// What a Smoke Vanish is worth to the hero bot: when the ninja is threatened (a foe beside it, a charge locked on it,
        /// more when it is hurt) it pays to slip away; when a landing has pincer odds (the share of landings that close a
        /// pincer with the ninja as an end, times 10 per foe trapped on average) it pays to roll the dice. Less 2 for the
        /// cooldown, so with neither it is never cast. 0 when there is nowhere to land or the ninja is Rooted.
        /// </summary>
        public static float Value(BattleState s, Unit caster)
        {
            if (s == null || caster == null || !caster.Alive || caster.IsRooted) return 0f;
            var lands = Landings(s, caster, caster.Pos);
            if (lands.Count == 0) return 0f;
            float v = 0f;
            int beside = 0;
            foreach (var f in s.Units)
                if (f.Alive && f.OnBoard && f.Team != caster.Team && !f.IsNeutral && !f.IsCorpse && f.IsAdjacentTo(caster)) beside++;
            bool locked = false;
            foreach (var e in s.Timeline) if (LockedOn(e, caster)) { locked = true; break; }
            if (beside > 0 || locked)
            {
                v += 4f + 2f * Math.Min(beside, 3) + (locked ? 4f : 0f);
                if (caster.HpFraction < 0.5f) v += 6f;
            }
            var copy = s.Clone();
            var cu = copy.UnitById(caster.Id);
            float trapped = 0f;
            foreach (var c in lands)
            {
                cu.Pos = c;
                foreach (var p in PincerDetector.Detect(copy, Team.Hero, cu))
                    if (p.A.Id == cu.Id || p.B.Id == cu.Id) trapped += p.Opponents.Count;
            }
            v += 10f * trapped / lands.Count;
            return v - 2f;
        }
    }
}
