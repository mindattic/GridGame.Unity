using System;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Core.Battle
{
    /// <summary>
    /// Pole Vault (§7, GG-T868, the Lancer's skill): plant the spear and leap over a unit standing right beside the
    /// Lancer (ally or foe, Heavy included: nothing is moved) onto the free tile straight beyond it. The Lancer never
    /// enters the tile it clears, so a firewall, a mine or slime under the vaulted unit is crossed, not stepped on; only
    /// the landing tile's own hazards and traps spring. A Rooted Lancer can't vault. Deterministic: no Rng.
    /// </summary>
    public static class PoleVault
    {
        /// <summary>The unit a vault from <paramref name="caster"/> onto <paramref name="landing"/> clears, or null when it
        /// is no legal vault: a one-tile caster, a landing exactly two tiles away in a straight line, a living unit on the
        /// tile between, and the landing on the board, walkable and free of units, movables and traps.</summary>
        public static Unit Over(BattleState s, Unit caster, Coord landing)
        {
            if (s == null || caster == null || !caster.Alive || caster.IsMultiTile) return null;
            int dx = landing.X - caster.Pos.X, dy = landing.Y - caster.Pos.Y;
            if (!((Math.Abs(dx) == 2 && dy == 0) || (Math.Abs(dy) == 2 && dx == 0))) return null;
            if (!LandingFree(s, caster, landing)) return null;
            var mid = new Coord(caster.Pos.X + dx / 2, caster.Pos.Y + dy / 2);
            var over = s.UnitAt(mid);
            return over != null && over != caster && over.Alive ? over : null;
        }

        /// <summary>Can <paramref name="caster"/> come down on <paramref name="c"/>?</summary>
        public static bool LandingFree(BattleState s, Unit caster, Coord c)
        {
            if (!s.Walkable(c) || s.MovableAt(c) != null || s.TrapAt(c) != null) return false;
            foreach (var o in s.Units)
                if (o != caster && o.Alive && o.Occupies(c)) return false;
            return true;
        }

        /// <summary>The sides <paramref name="u"/> could walk out of from <paramref name="at"/>: a walkable cardinal
        /// neighbour with no foe, block, movable or hazard on it (an ally is no wall: a drag swaps past it).</summary>
        public static int OpenSides(BattleState s, Unit u, Coord at)
        {
            int open = 0;
            foreach (var d in Coord.Cardinals)
            {
                var n = at + d;
                if (!s.Walkable(n) || s.MovableAt(n) != null) continue;
                var o = s.UnitAt(n);
                if (o != null && o != u && o.Team != u.Team) continue;
                if (RegionBoard.DangerAt(s, n, u) > 0f) continue;
                open++;
            }
            return open;
        }

        /// <summary>Living foes cardinally beside <paramref name="u"/> were it standing on <paramref name="at"/>.</summary>
        private static int FoesBeside(BattleState s, Unit u, Coord at)
        {
            int n = 0;
            foreach (var o in s.Units)
            {
                if (!o.Alive || o.Team == u.Team || o.IsNeutral || o.IsCorpse) continue;
                foreach (var t in o.Tiles()) if (t.IsCardinalNeighbor(at)) { n++; break; }
            }
            return n;
        }

        /// <summary>
        /// What the hero bot thinks a vault onto <paramref name="landing"/> is worth (0 = not worth the turn): 10 per foe a
        /// pincer it closes on landing traps, plus a way out of a box (no open side where it stands, one where it lands),
        /// plus, when hurt, a step out of a crowd of foes; never onto a tile it can see is dangerous.
        /// </summary>
        public static float Value(BattleState s, Unit caster, Coord landing)
        {
            if (Over(s, caster, landing) == null) return 0f;
            if (RegionBoard.DangerAt(s, landing, caster) > 0f) return 0f;
            float v = 0f;
            var copy = s.Clone();
            var cu = copy.UnitById(caster.Id);
            cu.Pos = landing;
            foreach (var p in PincerDetector.Detect(copy, Team.Hero, cu)) v += 10f * p.Opponents.Count;
            if (OpenSides(s, caster, caster.Pos) == 0 && OpenSides(s, caster, landing) > 0) v += 8f; // boxed in: vault out
            if (caster.HpFraction < 0.5f)
            {
                int now = FoesBeside(s, caster, caster.Pos), after = FoesBeside(s, caster, landing);
                if (after < now) v += 2f * (now - after);
            }
            return v - 2f;
        }
    }
}
