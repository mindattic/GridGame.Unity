using System;
using GridGame.Core.Content;

namespace GridGame.Core.Battle
{
    /// <summary>
    /// Anchor Stance (§7, GG-T882, the Warden's skill): the Warden plants its shield and wears [Anchored] until its next
    /// action - a buff that <see cref="BuffDef.Roots"/> (it can't step and nothing drags it), makes it
    /// <see cref="BuffDef.Heavy"/> (the buff-based path to a temporary Heavy: <see cref="Unit.IsHeavy"/> reads either the
    /// class flag or such a buff) and <see cref="BuffDef.Braces"/> every ally beside it: no foe's shove, pull, hook, swap or
    /// body-shove and no rule drag (the anchor's heave, §14.20.3) moves a braced ally. The ally's own side still can (a
    /// drag swap is the player's choice). The buff is <see cref="BuffDef.EndsOnAction"/>: it lifts as the Warden's next
    /// command starts, never by the turn count. The answer to hook throwers, the anchor's heave and enemy shoves.
    /// This class holds the rule's pure parts (who is braced, what the hero bot thinks a cast is worth); the runner
    /// enforces it. Deterministic: no Rng.
    /// </summary>
    public static class AnchorStance
    {
        /// <summary>What each displacement the stance would stop is worth to the hero bot.</summary>
        public const float SpareValue = 10f;

        /// <summary>Does casting <paramref name="a"/> put a bracing stance on the caster?</summary>
        public static bool Is(BattleState s, AbilityDef a) =>
            a != null && s?.Content != null && s.Content.TryBuff(a.ApplyBuff) is BuffDef b && b.Braces;

        /// <summary>The ally whose stance braces <paramref name="u"/> against a move by <paramref name="by"/> (null for a
        /// rule's drag), or null when nothing does: a living, on-board unit of <paramref name="u"/>'s own side, cardinally
        /// beside it, wearing a <see cref="BuffDef.Braces"/> buff. A move by <paramref name="u"/>'s own side is never
        /// stopped (a friendly drag swap), nor is a unit braced by itself (its own buff roots it anyway).</summary>
        public static Unit BracedBy(BattleState s, Unit u, Unit by = null)
        {
            if (s == null || u == null || !u.Alive || !u.OnBoard) return null;
            if (by != null && by != u && by.Team == u.Team) return null;
            foreach (var o in s.Units)
                if (o != u && o.Alive && o.OnBoard && o.Team == u.Team && o.IsAnchoring && o.IsAdjacentTo(u)) return o;
            return null;
        }

        /// <summary>Would <paramref name="a"/> (a queued foe's cast) move what it lands on? A shove, a pull (a hook), a swap,
        /// a magnet's tug, or a rule move.</summary>
        public static bool Displaces(AbilityDef a)
        {
            if (a == null) return false;
            if (a.Knockback > 0 || a.Pull > 0 || a.SwapsPlaces || a.Magnetizes > 0) return true;
            if (a.RuleEffects != null)
                foreach (var e in a.RuleEffects)
                    if (e != null && e.Kind == RuleEffectKind.MoveUnits) return true;
            return false;
        }

        /// <summary>
        /// What casting the stance now is worth to the hero bot: each hero a foe's queued displacing cast is aimed at (its
        /// target, or a hero on its marked line) that the stance would hold - the caster itself, or an ally beside it -
        /// is worth <see cref="SpareValue"/>. 0 with no such cast in the queue, or while the caster already stands
        /// anchored, so it is never cast for its own sake.
        /// </summary>
        public static float Value(BattleState s, Unit caster)
        {
            if (s == null || caster == null || !caster.Alive || caster.IsAnchoring) return 0f;
            float value = 0f;
            foreach (var cast in s.Timeline)
            {
                if (cast.Kind != TimelineKind.Cast || cast.CasterTeam != Team.Enemy) continue;
                var owner = s.UnitById(cast.OwnerId);
                if (owner == null || !owner.Alive) continue;
                var ability = s.Content.TryAbility(cast.AbilityId);
                if (!Displaces(ability)) continue;
                var target = s.UnitById(cast.TargetUnitId);
                if (Holds(caster, target)) value += SpareValue;
                if (cast.LineTiles != null)
                    foreach (var tile in cast.LineTiles)
                    {
                        var h = s.UnitAt(tile);
                        if (h != null && h != target && Holds(caster, h)) value += SpareValue;
                    }
            }
            return value;
        }

        /// <summary>Would the caster's stance hold <paramref name="h"/>: the caster itself, or a hero beside it.</summary>
        private static bool Holds(Unit caster, Unit h) =>
            h != null && h.Alive && h.OnBoard && h.Team == caster.Team && (h == caster || h.IsAdjacentTo(caster));
    }
}
