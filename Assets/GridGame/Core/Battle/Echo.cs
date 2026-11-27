using System;
using GridGame.Core.Content;
using GridGame.Core.Rules;

namespace GridGame.Core.Battle
{
    /// <summary>
    /// Echo (GG-T388): the move no other hero had — copying an ability. An Echo picks one unit and repeats the last Active
    /// ability that unit used this battle (<see cref="Unit.LastAbilityId"/>), cast by the echoer from where it stands.
    /// Encore (the Tactician's since the Echo hero was cut, GG-T531) picks an ally and repeats its move (an ally's Fireball, a second Heal, a Countermarch); the Mocking
    /// Echo's charge picks a hero and turns that hero's own move on the party. Aim: the unit the source last aimed at when
    /// the echoer can legally pick it, else (a foe-harming move copied from a foe) the source itself, else the source's
    /// last tile, else the legal pick nearest that tile (ties by row, then column) — deterministic, no dice.
    /// </summary>
    public static class Echo
    {
        /// <summary>A copy the echoer could cast right now.</summary>
        public readonly struct Plan
        {
            public readonly AbilityDef Ability;
            public readonly Coord Anchor;
            public Plan(AbilityDef ability, Coord anchor) { Ability = ability; Anchor = anchor; }
        }

        /// <summary>Can <paramref name="a"/> ever be copied? Skills and spells only, never another Echo, an escape, a
        /// teleport (the tile it needs is the copier's own), a tame or an enemy-only boss move.</summary>
        public static bool Copyable(AbilityDef a) =>
            a != null && (a.Kind == AbilityKind.Skill || a.Kind == AbilityKind.Spell) && !a.Echoes && !a.Escape && !a.IsTeleport && !a.Vanishes && a.Tame <= 0f;

        /// <summary>What <paramref name="caster"/> would cast by echoing <paramref name="source"/>, or null when there is
        /// nothing to echo (no move yet, an uncopyable one, nobody to revive, or no legal target).</summary>
        public static Plan? For(BattleState s, Unit caster, Unit source)
        {
            if (s == null || caster == null || source == null || !source.Alive || source == caster) return null;
            var a = s.Content.TryAbility(source.LastAbilityId);
            if (!Copyable(a)) return null;
            if (a.Revives)
            {
                bool fallen = s.Units.Exists(u => u.Team == caster.Team && !u.Alive && u.Hp <= 0 && !u.IsWildTamed && !u.IsCorpse && !u.IsRemains && !u.IsEgg && !u.IsSnakeSegment && !u.IsMultiTile);
                return fallen ? new Plan(a, TargetResolver.AutoAnchor(caster)) : (Plan?)null;
            }
            if (a.Mode == TargetMode.Auto)
            {
                var self = TargetResolver.AutoAnchor(caster);
                if (a.GrantsOrbs <= 0 && TargetResolver.Collect(s, a, caster, self).Count == 0) return null;
                return new Plan(a, self);
            }
            var last = source.LastTargetUnitId >= 0 ? s.UnitById(source.LastTargetUnitId) : null;
            if (last != null && last.Alive && Valid(s, a, caster, last.Pos)) return new Plan(a, last.Pos);
            if (a.Filter == TargetFilter.EnemyOnly && source.Team != caster.Team && Valid(s, a, caster, source.Pos)) return new Plan(a, source.Pos);
            if (Valid(s, a, caster, source.LastAnchor)) return new Plan(a, source.LastAnchor);
            var near = last != null && last.Alive ? last.Pos : source.LastAnchor;
            Coord? best = null;
            int bestD = int.MaxValue;
            for (int y = 0; y < BattleConfig.Rows; y++)
                for (int x = 0; x < BattleConfig.Cols; x++)
                {
                    var c = new Coord(x, y);
                    if (!Valid(s, a, caster, c)) continue;
                    int d = Math.Abs(c.X - near.X) + Math.Abs(c.Y - near.Y);
                    if (d < bestD) { best = c; bestD = d; }
                }
            return best == null ? (Plan?)null : new Plan(a, best.Value);
        }

        /// <summary>A legal pick that also reaches somebody (a tile pick must catch a unit).</summary>
        private static bool Valid(BattleState s, AbilityDef a, Unit caster, Coord c) =>
            BattleState.InBounds(c) && TargetResolver.IsValidPick(s, a, caster, c) && TargetResolver.Collect(s, a, caster, c).Count > 0;
    }
}
