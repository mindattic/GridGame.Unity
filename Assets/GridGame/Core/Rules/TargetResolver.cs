using System;
using System.Collections.Generic;
using GridGame.Core.Battle;
using GridGame.Core.Content;

namespace GridGame.Core.Rules
{
    /// <summary>Shape × mode × filter targeting (bible §5, GG-LAW-5).</summary>
    public static class TargetResolver
    {
        /// <param name="from">Where the ability comes from (the caster's tile): orients a <see cref="TargetShape.Wall"/>, a line
        /// of 2 × <paramref name="radius"/> + 1 through the anchor across the line from it (|dx| &gt; |dy|: a column, else a
        /// row; no <paramref name="from"/>: a row). Every other shape ignores it.</param>
        public static List<Coord> Tiles(Coord anchor, TargetShape shape, int radius, Coord? from = null)
        {
            var list = new List<Coord>();
            bool wallColumn = shape == TargetShape.Wall && from.HasValue
                && Math.Abs(anchor.X - from.Value.X) > Math.Abs(anchor.Y - from.Value.Y);
            for (int y = 0; y < BattleConfig.Rows; y++)
                for (int x = 0; x < BattleConfig.Cols; x++)
                {
                    var c = new Coord(x, y);
                    int dx = Math.Abs(x - anchor.X), dy = Math.Abs(y - anchor.Y);
                    bool hit;
                    switch (shape)
                    {
                        case TargetShape.Self:
                        case TargetShape.SingleActor:
                        case TargetShape.SingleTile: hit = dx == 0 && dy == 0; break;
                        case TargetShape.Square: hit = Math.Max(dx, dy) <= radius; break;
                        case TargetShape.Diamond: hit = dx + dy <= radius; break;
                        case TargetShape.Cross: hit = (dx == 0 && dy <= radius) || (dy == 0 && dx <= radius); break;
                        case TargetShape.Plus: hit = dx == 0 || dy == 0; break;
                        case TargetShape.Row: hit = dy == 0; break;
                        case TargetShape.Column: hit = dx == 0; break;
                        case TargetShape.Wall: hit = wallColumn ? dx == 0 && dy <= radius : dy == 0 && dx <= radius; break;
                        default: hit = false; break;
                    }
                    if (hit) list.Add(c);
                }
            return list;
        }

        public static bool Passes(TargetFilter filter, Unit caster, Unit u)
        {
            // A neutral recruitable (GG-T132) is nobody's foe: only a hero's ally-only ability (a heal, a ward) reaches it.
            if (u.IsNeutral) return filter == TargetFilter.AllyOnly && caster.IsHero;
            switch (filter)
            {
                case TargetFilter.EnemyOnly: return u.Team != caster.Team;
                case TargetFilter.AllyOnly: return u.Team == caster.Team;
                case TargetFilter.EmptyOnly: return false;
                default: return true;
            }
        }

        /// <summary>Distinct living units affected by an ability anchored at <paramref name="anchor"/>.</summary>
        public static List<Unit> Collect(BattleState s, AbilityDef a, Unit caster, Coord anchor)
        {
            var result = new List<Unit>();
            if (a.Shape == TargetShape.AllEnemies || a.Shape == TargetShape.AllAllies)
            {
                var team = a.Shape == TargetShape.AllAllies ? caster.Team : Opposite(caster.Team);
                foreach (var u in s.Units)
                    if (u.Alive && u.Team == team && Passes(a.Filter, caster, u) && Tagged(a, u)) result.Add(u);
                return result;
            }
            if (a.Shape == TargetShape.Self)
            {
                if (caster.Alive) result.Add(caster);
                return result;
            }
            foreach (var c in Tiles(anchor, a.Shape, a.Radius, caster?.Pos))
            {
                var u = s.UnitAt(c);
                if (u != null && !result.Contains(u) && Passes(a.Filter, caster, u) && Tagged(a, u)) result.Add(u);
            }
            return result;
        }

        /// <summary>GG-T879: an ability with <see cref="AbilityDef.OnlyVsTag"/> catches only units whose class carries it.</summary>
        private static bool Tagged(AbilityDef a, Unit u) => a.OnlyVsTag == null || (u.Def != null && u.Def.HasTag(a.OnlyVsTag));

        public static Team Opposite(Team t) => t == Team.Hero ? Team.Enemy : Team.Hero;

        /// <summary>Is <paramref name="target"/> a legal pick for this ability (for PickActor / PickTile)?</summary>
        public static bool IsValidPick(BattleState s, AbilityDef a, Unit caster, Coord target)
        {
            if (!BattleState.InBounds(target)) return false;
            var occ = s.UnitAt(target);
            if (a.Mode == TargetMode.Auto) return true;
            if (a.Filter == TargetFilter.EmptyOnly)
            {
                if (occ != null || s.TrapAt(target) != null) return false;
                if (a.Vaults) return PoleVault.Over(s, caster, target) != null; // GG-T868: the free tile beyond a unit beside the caster
                if (!a.AdjacentOnly) return true;
                foreach (var t in caster.Tiles()) if (t.IsCardinalNeighbor(target)) return true;
                return false;
            }
            if (a.AdjacentOnly && (occ == null || !caster.IsAdjacentTo(occ))) return false;
            if (a.MaxRange > 0 && a.Mode == TargetMode.PickActor && (occ == null || caster.DistanceTo(occ) > a.MaxRange)) return false; // GG-T865
            if (a.RedirectsDebuffs && !RedirectFlow.Can(s, caster, occ)) return false; // GG-T887: an ally with a debuff, a foe to take it
            if (a.StealsBoon && !SwipeBoon.Can(s, caster, occ)) return false; // GG-T866: a foe beside the Thief wearing a boon
            // A tame (GG-T303) only ever picks a tameable monster: never a Sentient foe, a boss or remains.
            if (a.Tame > 0f && a.Mode == TargetMode.PickActor && !Taming.Tameable(occ)) return false;
            if (a.Mode == TargetMode.PickActor) return occ != null && Passes(a.Filter, caster, occ);
            if (a.Magnetizes > 0) return Magnetize.Caught(s, a, caster, target).Count > 0; // GG-T878: a tile some metal foe is gripped toward
            // PickTile: any in-bounds tile whose shape catches at least one valid unit, or a trap its tile reactions clear (GG-T371).
            return Collect(s, a, caster, target).Count > 0 || TileReactions.ReachesTrap(s, a, caster, target);
        }

        /// <summary>Auto-mode anchor: the caster's own tile (Self / Cross around caster / All*).</summary>
        public static Coord AutoAnchor(Unit caster) => caster.Pos;
    }
}
