using System;
using GridGame.Core.Content;

namespace GridGame.Core.Battle
{
    /// <summary>
    /// Swipe Boon (§7, GG-T866, the Thief's skill): lifts one boon off an adjacent foe and wears it, with the time it had
    /// left. The counter to the foes' supporters (Close Ranks' [Protection], Battle Cry's [Empowered], Battle Hymn) and
    /// to their casters' Ward, Regen and Blurred. Only these five boons can be swiped (<see cref="Stealable"/>,
    /// in the order the Thief wants them); of several, the first in that order goes, and of two of the same, the one with
    /// more time left. Deterministic: no Rng.
    /// </summary>
    public static class SwipeBoon
    {
        /// <summary>The boons a swipe can take, best first.</summary>
        public static readonly string[] Stealable = { "empowered", "ward", "protection", "blurred", "regen" };

        /// <summary>Is this a boon a swipe can lift?</summary>
        public static bool CanSteal(BuffInstance b) =>
            b != null && b.Def != null && b.Def.Kind == BuffKind.Buff && Array.IndexOf(Stealable, b.Def.Id) >= 0;

        /// <summary>The boon a swipe would take off <paramref name="foe"/>: the first <see cref="Stealable"/> it carries,
        /// the instance with the most time left. Null when it carries none.</summary>
        public static BuffInstance Pick(Unit foe)
        {
            if (foe == null) return null;
            foreach (var id in Stealable)
            {
                BuffInstance best = null;
                foreach (var b in foe.Buffs)
                    if (CanSteal(b) && b.Def.Id == id && (best == null || b.Remaining > best.Remaining)) best = b;
                if (best != null) return best;
            }
            return null;
        }

        /// <summary>Can <paramref name="caster"/> swipe from <paramref name="foe"/> right now: a living foe beside it
        /// carrying a stealable boon?</summary>
        public static bool Can(BattleState s, Unit caster, Unit foe)
        {
            if (caster == null || foe == null || foe == caster || !caster.Alive || !foe.Alive) return false;
            if (foe.IsNeutral || foe.Team == caster.Team || foe.IsCorpse || foe.IsRemains || foe.IsEgg) return false;
            return caster.IsAdjacentTo(foe) && Pick(foe) != null;
        }

        /// <summary>How much the Thief wants each boon: a damage boost or a heavy ward most, then the lighter guards, then
        /// a regen.</summary>
        public static float Worth(BuffDef d)
        {
            if (d == null) return 0f;
            switch (d.Id)
            {
                case "empowered": return 8f;
                case "ward": return 8f;
                case "protection": return 5f;
                case "blurred": return 5f;
                case "regen": return 4f;
                default: return 0f;
            }
        }

        /// <summary>What the hero bot makes of swiping from <paramref name="foe"/>: nothing unless the foe is beside the
        /// Thief and carries a stealable boon; otherwise the boon's <see cref="Worth"/> plus a little for the time it has
        /// left, counted twice (the foe loses it and the Thief gains it).</summary>
        public static float Value(BattleState s, Unit caster, Unit foe)
        {
            if (!Can(s, caster, foe)) return 0f;
            var b = Pick(foe);
            int left = b.Def.Unit == DurationUnit.Ticks ? (b.Remaining + 1) / 2 : b.Remaining;
            return 1.5f * (Worth(b.Def) + 2f * Math.Min(3, Math.Max(1, left)));
        }
    }
}
